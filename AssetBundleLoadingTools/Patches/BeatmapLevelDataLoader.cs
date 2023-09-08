using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using UnityEngine;

namespace AssetBundleLoadingTools.Patches
{
    [HarmonyPatch(typeof(BeatmapLevelDataLoader), nameof(BeatmapLevelDataLoader.Dispose))]
    internal class BeatmapLevelDataLoader_Dispose
    {
        private static readonly MethodInfo AssetBundleUnloadAllAssetsMethod = AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.UnloadAllAssetBundles));
        private static readonly MethodInfo UnloadReplacementMethod = SymbolExtensions.GetMethodInfo(() => UnloadLoadedAssetBundles());

        internal static List<AssetBundle> LoadedAssetBundles { get; } = new();

        // remove the call to AssetBundle.UnloadAllAssetBundles and call our UnloadLoadedAssetBundles instead
        // this could be a prefix patch but I'd rather avoid skipping possible exising Postfix patches
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_1), new CodeMatch(i => i.Calls(AssetBundleUnloadAllAssetsMethod)))
                .ThrowIfInvalid("Call to UnloadAllAssetBundles not found")
                .RemoveInstructions(2)
                .Insert(new CodeInstruction(OpCodes.Call, UnloadReplacementMethod))
                .InstructionEnumeration();
        }

        private static void UnloadLoadedAssetBundles()
        {
            foreach (var assetBundle in LoadedAssetBundles)
            {
                if (assetBundle != null)
                {
                    assetBundle.Unload(true);
                }
            }

            LoadedAssetBundles.Clear();
        }
    }

    [HarmonyPatch(typeof(BeatmapLevelDataLoader))]
    internal class BeatmapLevelDataLoader_LoadBeatmapLevelDataAsync
    {
        private static readonly MethodInfo TaskCompletionSourceTrySetResultMethod = AccessTools.Method(typeof(TaskCompletionSource<BeatmapLevelDataSO>), nameof(TaskCompletionSource<BeatmapLevelDataSO>.TrySetResult));
        private static readonly MethodInfo AddToListMethod = SymbolExtensions.GetMethodInfo(() => AddToList(null!));

        // get the nested class representing the delegate created by assetBundleRequest.completed += () => { ... }
        private static readonly FieldInfo AssetBundleField = typeof(BeatmapLevelDataLoader)
            .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(t => t.GetField("assetBundle", BindingFlags.Public | BindingFlags.Instance))
            .Single(f => f != null);

        // get the method generated for the delegate (usually the only one in the class, but might as well be safe)
        private static MethodInfo TargetMethod() => AssetBundleField.DeclaringType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m => m.Name.Contains("<LoadBeatmapLevelDataAsync>"));

        // add a call to our AddToList method right before setting the result on the TaskCompletionSource so we can keep track of asset bundles that were loaded
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(i => i.Calls(TaskCompletionSourceTrySetResultMethod)))
                .ThrowIfInvalid("Call to TrySetResult not found")
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AssetBundleField),
                    new CodeInstruction(OpCodes.Call, AddToListMethod))
                .InstructionEnumeration();
        }

        private static void AddToList(AssetBundle assetBundle)
        {
            // assetBundle.Unload(true) technically might have been called but realistically it shouldn't happen - it's a base game bug if it does
            BeatmapLevelDataLoader_Dispose.LoadedAssetBundles.Add(assetBundle);
        }
    }
}
