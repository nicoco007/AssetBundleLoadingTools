using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.XR;

namespace AssetBundleLoadingTools.Patches
{
    [HarmonyPatch(typeof(BloomPrePassRendererSO), nameof(BloomPrePassRendererSO.GetCameraParams))]
    internal class BloomPrePassRendererSOMultiPass
    {
        private static readonly MethodInfo CameraGetStereoEnabled = AccessTools.DeclaredPropertyGetter(typeof(Camera), nameof(Camera.stereoEnabled));
        private static readonly MethodInfo XRSettingsGetStereoRenderingMode = AccessTools.DeclaredPropertyGetter(typeof(XRSettings), nameof(XRSettings.stereoRenderingMode));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            Label? stereoNotEnabledLabel = null;

            return new CodeMatcher(instructions, generator)
                // make the if statement take into account multi pass
                // if (camera.stereoEnabled && XRSettings.stereoRenderingMode != StereoRenderingMode.MultiPass)
                .MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(i => i.Calls(CameraGetStereoEnabled)),
                    new CodeMatch(i => i.opcode == OpCodes.Brfalse && i.Branches(out stereoNotEnabledLabel))) // bool stereoEnabled
                .ThrowIfInvalid("if (camera.stereoEnabled) not found")
                .Advance(1)
                .Insert(
                    new CodeInstruction(OpCodes.Call, XRSettingsGetStereoRenderingMode),
                    new CodeInstruction(OpCodes.Ldc_I4_0), // StereoRenderingMode.MultiPass
                    new CodeInstruction(OpCodes.Ceq),
                    new CodeInstruction(OpCodes.Brtrue, stereoNotEnabledLabel)) // branch to non-stereo if XRSettings.stereoRenderingMode == StereoRenderingMode.MultiPass)
                .InstructionEnumeration();
        }
    }
}
