using AssetBundleLoadingTools.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
        private static readonly MethodInfo CreateOrUpdateMirrorCamera = AccessTools.DeclaredMethod(typeof(MirrorRendererSO), nameof(MirrorRendererSO.CreateOrUpdateMirrorCamera));
        private static readonly MethodInfo XRSettingsGetRenderMode = AccessTools.DeclaredPropertyGetter(typeof(XRSettings), nameof(XRSettings.stereoRenderingMode));
        private static readonly MethodInfo CameraGetStereoEnabled = AccessTools.DeclaredPropertyGetter(typeof(Camera), nameof(Camera.stereoEnabled));
        private static readonly MethodInfo GLSetInvertCulling = AccessTools.DeclaredPropertySetter(typeof(GL), nameof(GL.invertCulling));
        private static readonly MethodInfo GLFlushMethod = AccessTools.DeclaredMethod(typeof(GL), nameof(GL.Flush));
        private static readonly MethodInfo RenderMultiPassMethod = AccessTools.DeclaredMethod(typeof(MirrorRendererSOMultiPass), nameof(RenderMultiPass));
        private static readonly MethodInfo AddEyeRenderedMethod = AccessTools.DeclaredMethod(typeof(MirrorRendererSOMultiPass), nameof(AddEyeRendered));
        private static readonly MethodInfo CheckEyeAlreadyRenderedMethod = AccessTools.DeclaredMethod(typeof(MirrorRendererSOMultiPass), nameof(CheckEyeAlreadyRendered));

        internal static readonly Dictionary<int, HashSet<(MirrorRendererSO.CameraTransformData, Camera.MonoOrStereoscopicEye)>> renderedEyes = new();

        [HarmonyPatch(nameof(MirrorRendererSO.GetMirrorTexture))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetMirrorTextureTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            Label? useMonoTextureSizeLabel = null;

            return new CodeMatcher(instructions, generator)
                // change the render texture existence check so it only returns if the stereoActiveEye was already rendered
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(i => i.LoadsLocal(7)), // RenderTexture value
                    new CodeMatch(i => i.Calls(CreateOrUpdateMirrorCamera))) // right after the render textures dictionary is updated
                .ThrowIfInvalid("CreateOrUpdateMirrorCamera not found")
                .CreateLabel(out Label afterCreateRenderTextureLabel)
                .MatchBack(false,
                    new CodeMatch(i => i.Branches(out Label? _)),
                    new CodeMatch(i => i.LoadsLocal(7)), // RenderTexture value
                    new CodeMatch(OpCodes.Ret))
                .ThrowIfInvalid("Early return not found")
                .Advance(1)
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0), // this
                    new CodeInstruction(OpCodes.Ldloc_0), // Camera current
                    new CodeInstruction(OpCodes.Ldloc, 6), // CameraTransformData cameraTransformData
                    new CodeInstruction(OpCodes.Call, CheckEyeAlreadyRenderedMethod),
                    new CodeInstruction(OpCodes.Brfalse, afterCreateRenderTextureLabel))

                // don't make the render texture double wide when multi-pass is enabled
                .MatchForward(true,
                    new CodeMatch(i => i.LoadsLocal(4)), // bool stereoEnabled
                    new CodeMatch(i => i.Branches(out useMonoTextureSizeLabel)))
                .ThrowIfInvalid("Render texture creation stereoEnabled branch not found")
                .Advance(1)
                .Insert(
                    new CodeInstruction(OpCodes.Call, XRSettingsGetRenderMode),
                    new CodeInstruction(OpCodes.Ldc_I4_0), // StereoRenderingMode.MultiPass
                    new CodeInstruction(OpCodes.Ceq),
                    new CodeInstruction(OpCodes.Brtrue, useMonoTextureSizeLabel))

                // register rendered eye after rendering
                .MatchForward(false,
                    new CodeMatch(i => i.Calls(GLSetInvertCulling)), // right after the if (stereoEnabled) { ... } else { ... },
                    new CodeMatch(i => i.Calls(GLFlushMethod)))
                .CreateLabel(out Label afterIfStatementLabel)
                .Advance(2)
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0), // this
                    new CodeInstruction(OpCodes.Ldloc_0), // Camera current
                    new CodeInstruction(OpCodes.Ldloc, 6), // CameraTransformData cameraTransformData
                    new CodeInstruction(OpCodes.Call, AddEyeRenderedMethod))

                // add our branch when XRSettings.stereoRenderingMode is MultiPass
                .MatchBack(true,
                    new CodeMatch(i => i.Calls(CameraGetStereoEnabled)), // got back to the beginning of the if statement
                    new CodeMatch(i => i.Branches(out Label? _)))
                .ThrowIfInvalid("RenderMirror stereoEnabled branch not found")
                .Advance(1)
                .CreateLabel(out Label ifNotMultiPassLabel) // create label to keep existing logic if not multi-pass
                .Insert(
                    new CodeInstruction(OpCodes.Call, XRSettingsGetRenderMode),
                    new CodeInstruction(OpCodes.Ldc_I4_0), // StereoRenderingMode.MultiPass
                    new CodeInstruction(OpCodes.Ceq),
                    new CodeInstruction(OpCodes.Brfalse, ifNotMultiPassLabel),
                    new CodeInstruction(OpCodes.Ldarg_0), // this
                    new CodeInstruction(OpCodes.Ldloc_0), // Camera current
                    new CodeInstruction(OpCodes.Ldloc, 8), // float stereoCameraEyeOffset
                    new CodeInstruction(OpCodes.Ldloc_3), // Quaternion rotation
                    new CodeInstruction(OpCodes.Ldarg_1), // Vector3 reflectionPlanePos
                    new CodeInstruction(OpCodes.Ldarg_2), // Vector3 reflectionPlaneNormal
                    new CodeInstruction(OpCodes.Call, RenderMultiPassMethod),
                    new CodeInstruction(OpCodes.Br, afterIfStatementLabel))
                .Instructions();
        }

        [HarmonyPatch(nameof(MirrorRendererSO.PrepareForNextFrame))]
        [HarmonyPrefix]
        public static void PrepareForNextFramePrefix(MirrorRendererSO __instance)
        {
            if (renderedEyes.TryGetValue(__instance.GetInstanceID(), out var hashSet))
            {
                hashSet.Clear();
            }
        }

        private static void AddEyeRendered(MirrorRendererSO self, Camera current, MirrorRendererSO.CameraTransformData cameraTransformData)
        {
            if (!renderedEyes.TryGetValue(self.GetInstanceID(), out var hashSet))
            {
                hashSet = new HashSet<(MirrorRendererSO.CameraTransformData, Camera.MonoOrStereoscopicEye)>();
                renderedEyes.Add(self.GetInstanceID(), hashSet);
            }

            hashSet.Add((cameraTransformData, current.stereoActiveEye));
        }

        private static bool CheckEyeAlreadyRendered(MirrorRendererSO self, Camera current, MirrorRendererSO.CameraTransformData cameraTransformData)
        {
            return renderedEyes.TryGetValue(self.GetInstanceID(), out var hashSet) && hashSet.Contains((cameraTransformData, current.stereoActiveEye));
        }

        private static void RenderMultiPass(MirrorRendererSO self, Camera current, float stereoCameraEyeOffset, Quaternion camRotation, Vector3 reflectionPlanePos, Vector3 reflectionPlaneNormal)
        {
            Vector3 targetPosition = current.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0), current.stereoActiveEye);
            Matrix4x4 stereoProjectionMatrix = current.GetStereoProjectionMatrix(current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right ? Camera.StereoscopicEye.Right : Camera.StereoscopicEye.Left);
            self._bloomPrePassRenderer.SetCustomStereoCameraEyeOffset(current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right ? -stereoCameraEyeOffset : stereoCameraEyeOffset);
            self.RenderMirror(targetPosition, camRotation, stereoProjectionMatrix, self.kFullRect, reflectionPlanePos, reflectionPlaneNormal);
        }
    }
}
