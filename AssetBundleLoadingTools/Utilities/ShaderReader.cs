using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TMPro;
using Shader = UnityEngine.Shader;

namespace AssetBundleLoadingTools.Utilities
{
    /// <summary>
    /// Class for reading shader data and checking of SPI support.
    /// Adapted from <see href="https://github.com/ToniMacaroni/UnsafeShaderTools">UnsafeShaderTools</see>.
    /// </summary>
    /// <remarks>
    /// The pointer offsets may change from one Unity version to the next. This was tested against Unity 2022.3.33f1.
    /// </remarks>
    internal static class ShaderReader
    {
        private const string TexArrayKeyword = "SV_InstanceID";

        private static readonly Logger logger = Plugin.Log.GetChildLogger(nameof(ShaderReader));

        public static bool IsSinglePassInstancedSupported(Shader shader)
        {
            Log("Shader: " + shader.name);
            return CheckForInputSignatureElementUnsafe(shader, TexArrayKeyword);
        }
        
        public static unsafe bool CheckForInputSignatureElementUnsafe(Shader shader, string semanticName)
        {
            var ptr = shader.GetCachedPtr();
            if (ptr == IntPtr.Zero)
            {
                Log("Shader ptr was not valid");
                return false;
            }
            
            var dataPtrs = ReadShaderDataUnsafe(ptr);
            if (dataPtrs.Count < 1)
            {
                Log("No data ptrs");
                return false;
            }

            // http://timjones.io/blog/archive/2015/09/02/parsing-direct3d-shader-bytecode#parsing-shader-bytecode
            foreach (var dataPtr in dataPtrs)
            {
                Log("dataPtr: " + dataPtr.ToString("x"));
                int chunkCount = *(int*)(dataPtr + 28);
                Log("Chunk count: " + chunkCount.ToString("x"));
                for (int i = 0; i < chunkCount; i++)
                {
                    int chunkOffset = *(int*)(dataPtr + 32 + 4 * i);
                    Log("chunkOffset: " + chunkOffset);
                    IntPtr chunkPtr = dataPtr + chunkOffset;
                    Log("chunkPtr: " + chunkPtr.ToString("x"));
                    string chunkType = Encoding.ASCII.GetString((byte*)chunkPtr, 4);
                    Log(chunkType);

                    // input signature chunk
                    if (chunkType == "ISGN")
                    {
                        IntPtr chunkDataPtr = chunkPtr + 8;
                        int elementCount = *(int*)chunkDataPtr;
                        Log("elementCount: " + elementCount);

                        for (int j = 0; j < elementCount; j++)
                        {
                            // each entry is 24 bytes long
                            int nameOffset = *(int*)(chunkDataPtr + 8 + j * 24);
                            Log("nameOffset: " + nameOffset);
                            string? name = null;
                            IntPtr namePtr = chunkDataPtr + nameOffset;
                            Log("namePtr: " + namePtr.ToString("x"));

                            for (int k = 0; k < 256; k++)
                            {
                                if (*(byte*)(namePtr + k) == 0)
                                {
                                    name = Encoding.ASCII.GetString((byte*)namePtr, k);
                                    break;
                                }
                            }

                            Log("name: " + name);

                            if (name == semanticName)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            return false;
        }

        public static unsafe List<IntPtr> ReadShaderDataUnsafe(IntPtr shaderPtr)
        {
            var result = new List<IntPtr>();
            bool isDebugBuild = UnityEngine.Debug.isDebugBuild;

            Log($"Parsing shader ptr {shaderPtr.ToString("x")}");
            Log($"isDebugBuild: " + isDebugBuild);

            IntPtr intShaderPtr = *(IntPtr*)(shaderPtr + (isDebugBuild ? 7 : 5) * 8);
            Log($"intShaderPtr: {intShaderPtr.ToString("x")}");
            IntPtr* subShaderListPtr = *(IntPtr**)intShaderPtr;
            Log($"subShaderListPtr: {((IntPtr)subShaderListPtr).ToString("x")}");
            int numSubShaders = *(int*)(intShaderPtr + 16);
            Log("Subshaders: " + numSubShaders);

            for (int subShaderIdx = 0; subShaderIdx < numSubShaders; subShaderIdx++)
            {
                IntPtr subShaderPtr = subShaderListPtr[subShaderIdx];
                Log($"Parsing subshader ptr {subShaderPtr.ToString("x")}");

                IntPtr intListPtr = subShaderPtr + 128;
                Log($"intListPtr: {intListPtr.ToString("x")}");
                IntPtr* passListPtr = *(IntPtr**)intListPtr;
                Log($"passListPtr: {((IntPtr)passListPtr).ToString("x")}");
                int numPasses = *(int*)(intListPtr + 16);
                Log("Passes: " + numPasses);

                for (int passIdx = 0; passIdx < numPasses; passIdx++)
                {
                    IntPtr passPtr = passListPtr[passIdx * 2]; // *2 because of the list structure (each entry is 2 pointers wide)
                    Log($"Parsing pass ptr {passPtr.ToString("x")}");
                    IntPtr progPtr = *(IntPtr*)(passPtr + 46 * 8);
                    if (progPtr == IntPtr.Zero)
                    {
                        Log("No program in this pass");
                        continue;
                    }

                    Log($"progPtr: {progPtr.ToString("x")}");
                    IntPtr* subProgListPtr = *(IntPtr**)(progPtr + (isDebugBuild ? 16 : 8));
                    Log($"subProgListPtr: {((IntPtr)subProgListPtr).ToString("x")}");
                    int numSubProgs = *(int*)(progPtr + (isDebugBuild ? 4 : 3) * 8);
                    Log("Subprogs: " + numSubProgs);

                    for (int subProgIdx = 0; subProgIdx < numSubProgs; subProgIdx++)
                    {
                        IntPtr subProgPtr = subProgListPtr[subProgIdx];
                        Log($"Parsing subprog ptr {subProgPtr.ToString("x")}");

                        IntPtr dataptr = *(IntPtr*)(subProgPtr + 16);

                        if (dataptr == IntPtr.Zero)
                        {
                            Log("Data ptr was null in this subprog");
                            continue;
                        }

                        Log($"Data: {dataptr.ToString("x")}");

                        var shaderType = *(byte*)dataptr;

                        if (shaderType > 2)
                        {
                            continue;
                        }
                        
                        var dxbcAddr = dataptr + (shaderType == 1 ? 6 : 38);
                        var dxbc = *(byte*)dxbcAddr;

                        if (dxbc != 0x44)
                        {
                            Log($"No DXBC header for {dataptr.ToString("x")} (type {shaderType})");
                        }

                        Log($"Bytecode: {dxbcAddr.ToString("x")}");
                        result.Add(dxbcAddr);
                    }
                }
            }
            
            return result;
        }

        [Conditional("SHADER_DEBUG")]
        private static void Log(string message)
        {
            logger.Debug(message);
        }
    }
}