using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ShaderKeywordRewriter;

string filePath = args[0];

Console.WriteLine($"Loading asset bundle from '{filePath}'");

var manager = new AssetsManager();

using FileStream readStream = File.OpenRead(filePath);
var bundleInstance = manager.LoadBundleFile(readStream, false);

Console.WriteLine("Bundle created in Unity " + bundleInstance.file.Header.EngineVersion);

AssetBundleCompressionType compressionType = AssetBundleCompressionType.None;

if (bundleInstance.file.DataIsCompressed)
{
    compressionType = bundleInstance.file.GetCompressionType();
    Console.WriteLine($"Decompressing using {compressionType}");
    bundleInstance.file = BundleHelper.UnpackBundle(bundleInstance.file);
}

var fileIndex = 0;
var assetsFileInstance = manager.LoadAssetsFileFromBundle(bundleInstance, fileIndex, false);
var assetsFile = assetsFileInstance.file;

// assetsFile.PrintFieldNodes("m_ValidKeywords");
// assetsFile.PrintFieldNodes("m_InvalidKeywords");

TypeTreeType typeTreeType = assetsFile.Metadata.FindTypeTreeTypeByID((int)AssetClassID.Material);
TypeTreeType typeTreeTypeWorkingCopy = new()
{
    Nodes = new List<TypeTreeNode>(typeTreeType.Nodes),
    StringBufferBytes = typeTreeType.StringBufferBytes,
};

typeTreeTypeWorkingCopy.AppendNode(-1, 1, 0x8000, "m_ValidKeywords", 0, TypeTreeNodeFlags.None,  "vector", 1);
typeTreeTypeWorkingCopy.AppendNode(-1, 2, 0xC000, "Array",           0, TypeTreeNodeFlags.Array, "Array",  1);
typeTreeTypeWorkingCopy.AppendNode(4,  3, 0,      "size",            0, TypeTreeNodeFlags.None,  "int",    1);
typeTreeTypeWorkingCopy.AppendNode(-1, 3, 0x8000, "data",            0, TypeTreeNodeFlags.None,  "string", 1);
typeTreeTypeWorkingCopy.AppendNode(-1, 4, 0x4001, "Array",           0, TypeTreeNodeFlags.Array, "Array",  1);
typeTreeTypeWorkingCopy.AppendNode(4,  5, 0x0001, "size",            0, TypeTreeNodeFlags.None,  "int",    1);
typeTreeTypeWorkingCopy.AppendNode(1,  5, 0x0001, "data",            0, TypeTreeNodeFlags.None,  "char",   1);

typeTreeTypeWorkingCopy.AppendNode(-1, 1, 0x8000, "m_InvalidKeywords", 0, TypeTreeNodeFlags.None,  "vector", 1);
typeTreeTypeWorkingCopy.AppendNode(-1, 2, 0xC000, "Array",             0, TypeTreeNodeFlags.Array, "Array",  1);
typeTreeTypeWorkingCopy.AppendNode(4,  3, 0,      "size",              0, TypeTreeNodeFlags.None,  "int",    1);
typeTreeTypeWorkingCopy.AppendNode(-1, 3, 0x8000, "data",              0, TypeTreeNodeFlags.None,  "string", 1);
typeTreeTypeWorkingCopy.AppendNode(-1, 4, 0x4001, "Array",             0, TypeTreeNodeFlags.Array, "Array",  1);
typeTreeTypeWorkingCopy.AppendNode(4,  5, 0x0001, "size",              0, TypeTreeNodeFlags.None,  "int",    1);
typeTreeTypeWorkingCopy.AppendNode(1,  5, 0x0001, "data",              0, TypeTreeNodeFlags.None,  "char",   1);

bool anyKeywordsUpdated = false;

Console.WriteLine("Updating materials");

foreach (AssetFileInfo materialInfo in assetsFile.GetAssetsOfType(AssetClassID.Material))
{
    AssetTypeValueField materialBaseField = manager.GetBaseField(assetsFileInstance, materialInfo);

    Console.WriteLine("-> " + materialBaseField["m_Name"].AsString);

    materialBaseField.InitializeField(typeTreeTypeWorkingCopy, "m_ValidKeywords");
    materialBaseField.InitializeField(typeTreeTypeWorkingCopy, "m_InvalidKeywords");

    var shaderKeywords = materialBaseField["m_ShaderKeywords"].AsString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    
    AssetTypeValueField validKeywordsArray = materialBaseField["m_ValidKeywords.Array"];

    foreach (string shaderKeyword in shaderKeywords)
    {
        Console.WriteLine("--> " + shaderKeyword);

        AssetTypeValueField arrayValue = ValueBuilder.DefaultValueFieldFromArrayTemplate(validKeywordsArray);
        arrayValue.AsString = shaderKeyword;
        validKeywordsArray.Children.Add(arrayValue);
        anyKeywordsUpdated = true;
    }

    materialInfo.SetNewData(materialBaseField);
}

AssetPPtr? avatarDescriptorPointer = null;
List<AssetPPtr> scriptTypes = assetsFile.Metadata.ScriptTypes;
for (int i = 0; i < scriptTypes.Count; i++)
{
    AssetTypeReference assetsFileScriptInfo = AssetHelper.GetAssetsFileScriptInfo(manager, assetsFileInstance, i);
    if (assetsFileScriptInfo != null && assetsFileScriptInfo.AsmName == "CustomAvatar.dll" && assetsFileScriptInfo.Namespace == "CustomAvatar" && assetsFileScriptInfo.ClassName == "AvatarDescriptor")
    {
        avatarDescriptorPointer = scriptTypes[i];
        break;
    }
}

foreach (AssetFileInfo monoBehaviourInfo in assetsFile.GetAssetsOfType(AssetClassID.MonoBehaviour))
{
    AssetTypeValueField monoBehaviourBaseField = manager.GetBaseField(assetsFileInstance, monoBehaviourInfo);

    if (monoBehaviourBaseField["m_Script.m_FileID"].AsLong != avatarDescriptorPointer.FileId || monoBehaviourBaseField["m_Script.m_PathID"].AsLong != avatarDescriptorPointer.PathId)
    {
        continue;
    }

    monoBehaviourBaseField["author"].Value.AsString += " [updated by ShaderKeywordRewriter]";
    monoBehaviourInfo.SetNewData(monoBehaviourBaseField);
}

if (!anyKeywordsUpdated)
{
    Console.WriteLine("No shader keywords found in any materials; no changes needed");
    return;
}

typeTreeType.Nodes[0].Version = 8; // necessary for new fields to be read - doesn't seem to affect loading in 2019
typeTreeType.Nodes = typeTreeTypeWorkingCopy.Nodes;
typeTreeType.StringBufferBytes = typeTreeTypeWorkingCopy.StringBufferBytes;

bundleInstance.file.BlockAndDirInfo.DirectoryInfos[fileIndex].SetNewData(assetsFile);

Console.WriteLine("Writing updated data");

string tempPath = Path.GetTempFileName();
using (FileStream writeStream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
using (AssetsFileWriter writer = new(writeStream))
{
    // Pack doesn't use content replacers so we need to write uncompressed first
    bundleInstance.file.Write(writer);
}

void CompressToFile(string sourcePath, string targetPath)
{
    AssetBundleFile compressedBundle = new();

    using (FileStream readStream = File.OpenRead(sourcePath))
    using (AssetsFileReader reader = new(readStream))
    {
        compressedBundle.Read(reader);

        using (FileStream writeStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (AssetsFileWriter writer = new(writeStream))
        {
            // LZMA is the modern default
            compressedBundle.Pack(writer, compressionType);
        }
    }

    compressedBundle.Close();
}

string targetPath = Path.ChangeExtension(filePath, ".mod.avatar");
Console.WriteLine($"Compressing bundle and saving to '{targetPath}'");
CompressToFile(tempPath, targetPath);

File.Delete(tempPath);