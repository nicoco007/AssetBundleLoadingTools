using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.XR;

namespace AssetBundleLoadingTools.Patches
{
    /// <summary>
    /// This patch makes base game mirrors work properly when the stereo rendering mode is multi-pass.
    /// </summary>
    [HarmonyPatch(typeof(MirrorRendererSO))]
    internal class MirrorRendererSOMultiPass
    {
        private static readonly MethodInfo XRSettingsGetStereoRenderingMode = AccessTools.DeclaredPropertyGetter(typeof(XRSettings), nameof(XRSettings.stereoRenderingMode));
        private static readonly MethodInfo CameraGetStereoEnabled = AccessTools.DeclaredPropertyGetter(typeof(Camera), nameof(Camera.stereoEnabled));
        private static readonly MethodInfo TransformGetPosition = AccessTools.DeclaredPropertyGetter(typeof(Transform), nameof(Transform.position));
        private static readonly MethodInfo GetCameraPositionMethod = AccessTools.DeclaredMethod(typeof(MirrorRendererSOMultiPass), nameof(GetCameraPosition));
        private static readonly FieldInfo StereoTextureWidthField = AccessTools.DeclaredField(typeof(MirrorRendererSO), nameof(MirrorRendererSO._stereoTextureWidth));

        [HarmonyPatch(nameof(MirrorRendererSO.RenderMirrorTexture))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RenderMirrorTextureTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return new CodeMatcher(instructions, generator)
                .DeclareLocal(typeof(bool), out LocalBuilder isMultiPassEnabled)
                .DeclareLocal(typeof(bool), out LocalBuilder isStereoSinglePassEnabled)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(i => i.Calls(TransformGetPosition)),
                    new CodeMatch(OpCodes.Stloc_1))
                .ThrowIfInvalid("Vector3 position = transform.position not found")
                .InsertAndAdvance(
                    // store isMultiPassEnabled local variable
                    new CodeInstruction(OpCodes.Call, XRSettingsGetStereoRenderingMode),
                    new CodeInstruction(OpCodes.Ldc_I4_0), // XRSettings.StereoRenderingMode.MultiPass
                    new CodeInstruction(OpCodes.Ceq),
                    new CodeInstruction(OpCodes.Stloc_S, isMultiPassEnabled),

                    // store stereoEnabled
                    new CodeInstruction(OpCodes.Ldarg_1), // Camera currentCamera
                    new CodeInstruction(OpCodes.Call, CameraGetStereoEnabled),
                    new CodeInstruction(OpCodes.Stloc_3), // bool stereoEnabled

                    // store isStereoSinglePassEnabled
                    new CodeInstruction(OpCodes.Ldloc_3),
                    new CodeInstruction(OpCodes.Ldloc_S, isMultiPassEnabled),
                    new CodeInstruction(OpCodes.Not),
                    new CodeInstruction(OpCodes.And),
                    new CodeInstruction(OpCodes.Stloc_S, isStereoSinglePassEnabled))

                // when multi pass is enabled, use the eye position as the camera position (this allows the render texture caching to work properly)
                .SetAndAdvance(OpCodes.Ldarg_1, null) // Camera currentCamera
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_0), // Transform transform
                    new CodeInstruction(OpCodes.Ldloc_3), // bool stereoEnabled
                    new CodeInstruction(OpCodes.Ldloc_S, isMultiPassEnabled))
                .SetAndAdvance(OpCodes.Call, GetCameraPositionMethod)

                // remove original stereoEnabled assignment
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_1), // Camera currentCamera
                    new CodeMatch(i => i.Calls(CameraGetStereoEnabled)),
                    new CodeMatch(OpCodes.Stloc_3)) // bool stereoEnabled
                .ThrowIfInvalid("Set stereoEnabled not found")
                .RemoveInstructions(3)

                // replace `if (stereoEnabled) { ... }` with `if (isStereoSinglePassEnabled) { ... }` around BEATGAMES_STEREO_PASS keyword enable/disable logic
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldloc_3),
                    new CodeMatch(i => i.Branches(out Label? _)))
                .SetAndAdvance(OpCodes.Ldloc_S, isStereoSinglePassEnabled)

                // use half the stereo width when in multi pass
                .MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(i => i.LoadsField(StereoTextureWidthField)))
                .Advance(1)
                .CreateLabel(out Label label)
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_S, isMultiPassEnabled),
                    new CodeInstruction(OpCodes.Brfalse_S, label),
                    new CodeInstruction(OpCodes.Ldc_I4_2),
                    new CodeInstruction(OpCodes.Div))

                // replace `if (currentCamera.stereoEnabled) { ... }` with `if (isStereoSinglePassEnabled) { ... }`
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(i => i.Calls(CameraGetStereoEnabled)),
                    new CodeMatch(i => i.Branches(out Label? _)))
                .ThrowIfInvalid("RenderMirrorTexture currentCamera.stereoEnabled branch not found")
                .RemoveInstruction()
                .SetAndAdvance(OpCodes.Ldloc_S, isStereoSinglePassEnabled)
                .Instructions();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 GetCameraPosition(Camera camera, Transform transform, bool stereoEnabled, bool isMultiPassEnabled)
        {
            if (stereoEnabled && isMultiPassEnabled)
            {
                return camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0), camera.stereoActiveEye);
            }
            else
            {
                return transform.position;
            }
        }
    }
}
