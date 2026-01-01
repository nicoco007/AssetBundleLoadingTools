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
        
        [HarmonyPatch(nameof(MirrorRendererSO.RenderMirrorTexture))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RenderMirrorTextureTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return new CodeMatcher(instructions, generator)
                // when multi pass is enabled, use the eye position as the camera position (this allows the render texture caching to work properly)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(i => i.Calls(TransformGetPosition)),
                    new CodeMatch(OpCodes.Stloc_1))
                .ThrowIfInvalid("Vector3 position = transform.position not found")
                .SetAndAdvance(OpCodes.Ldarg_1, null) // Camera currentCamera
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0, null)) // Transform transform
                .SetAndAdvance(OpCodes.Call, GetCameraPositionMethod)

                // make stereoEnabled take into account multi pass
                // `bool stereoEnabled = current.stereoEnabled` => `bool stereoEnabled = current.stereoEnabled && XRSettings.stereoRenderingMode != StereoRenderingMode.MultiPass`
                .MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_1), // Camera currentCamera
                    new CodeMatch(i => i.Calls(CameraGetStereoEnabled)),
                    new CodeMatch(OpCodes.Stloc_3)) // bool stereoEnabled
                .ThrowIfInvalid("Set stereoEnabled not found")
                .Insert(new CodeInstruction(OpCodes.Ldc_I4_0))
                .CreateLabel(out Label setStereoEnabledFalse)
                .Advance(2)
                .CreateLabel(out Label afterSetStereoEnabled)
                .Advance(-2)
                .Insert(
                    new CodeInstruction(OpCodes.Brfalse, setStereoEnabledFalse), // set stereoEnabled to false immediately (short circuit)
                    new CodeInstruction(OpCodes.Call, XRSettingsGetStereoRenderingMode),
                    new CodeInstruction(OpCodes.Ldc_I4_0), // StereoRenderingMode.MultiPass
                    new CodeInstruction(OpCodes.Ceq),
                    new CodeInstruction(OpCodes.Ldc_I4_0), // compare again so we get (XRSettings.stereoRenderingMode == StereoRenderingMode.MultiPass) == false => XRSettings.stereoRenderingMode != StereoRenderingMode.MultiPass
                    new CodeInstruction(OpCodes.Ceq),
                    new CodeInstruction(OpCodes.Stloc, 3),
                    new CodeInstruction(OpCodes.Br, afterSetStereoEnabled)) // skip the part that sets stereoEnabled to false

                // replace `if (currentCamera.stereoEnabled) { ... }` with `if (stereoEnabled) { ... }` (use the local variable)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(i => i.Calls(CameraGetStereoEnabled)),
                    new CodeMatch(i => i.Branches(out Label? _)))
                .ThrowIfInvalid("RenderMirrorTexture currentCamera.stereoEnabled branch not found")
                .RemoveInstruction()
                .SetAndAdvance(OpCodes.Ldloc_3, null)
                .Instructions();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 GetCameraPosition(Camera camera, Transform transform)
        {
            if (camera.stereoEnabled && XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass)
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
