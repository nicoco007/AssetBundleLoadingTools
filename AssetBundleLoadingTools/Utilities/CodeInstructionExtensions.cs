using HarmonyLib;
using System;
using System.Linq;
using System.Reflection.Emit;

namespace AssetBundleLoadingTools.Utilities
{
    internal static class CodeInstructionExtensions
    {
        private static readonly OpCode[] loadLocalCodes =
        {
            OpCodes.Ldloc_S,
            OpCodes.Ldloc,
        };

        internal static bool LoadsLocal(this CodeInstruction instruction, int index)
        {
            if (!loadLocalCodes.Contains(instruction.opcode))
            {
                return false;
            }

            switch (instruction.operand)
            {
                case LocalBuilder localBuilder:
                    return localBuilder.LocalIndex == index;

                case int localIndex:
                    return index == localIndex;

                default:
                    throw new InvalidCastException();
            }
        }
    }
}
