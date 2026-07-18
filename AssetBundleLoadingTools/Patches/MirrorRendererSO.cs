using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace AssetBundleLoadingTools.Patches
{
    /// <summary>
    /// This patch temporarily fixes the mirror renderer not rendering properly in non-stereo cameras. I don't know why this works.
    /// </summary>
    [HarmonyPatch(typeof(UniversalRendererData), "Create")]
    internal static class FixMonoCameraBloomInMirrors
    {
        public static void Prefix(UniversalRendererData __instance)
        {
            if (__instance.name == "RenderPipelineAsset_MirrorRenderer")
            {
                __instance.rendererFeatures.Insert(__instance.rendererFeatures.FindIndex(f => f is BloomPrePassRendererFeature) + 1, ScriptableObject.CreateInstance<MainEffectRendererFeature>());
            }
        }
    }

    /// <summary>
    /// This patch fixes the wrong math used to calculate the position of the mirrored camera (mirrored across the reflection plane).
    /// </summary>
    [HarmonyPatch]
    internal class FixMirrorCameraPose
    {
        [HarmonyPatch(typeof(MirrorRendererSO), nameof(MirrorRendererSO.RenderMirror))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RenderMirrorTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                // _mirrorCamera.worldToCameraMatrix.GetPosition()
                .MatchForward(false, new CodeMatch(OpCodes.Ldarg_0), new CodeMatch(OpCodes.Ldfld), new CodeMatch(OpCodes.Callvirt), new CodeMatch(OpCodes.Stloc_S), new CodeMatch(OpCodes.Ldloca_S), new CodeMatch(OpCodes.Call))
                .SetAndAdvance(OpCodes.Ldarg_1, null)
                .SetAndAdvance(OpCodes.Ldarg_S, 5)
                .SetAndAdvance(OpCodes.Ldarg_S, 6)
                .SetAndAdvance(OpCodes.Call, AccessTools.DeclaredMethod(typeof(FixMirrorCameraPose), nameof(GetPosition)))
                .RemoveInstructions(2)
                // _mirrorCamera.worldToCameraMatrix.rotation
                .MatchForward(false, new CodeMatch(OpCodes.Ldarg_0), new CodeMatch(OpCodes.Ldfld), new CodeMatch(OpCodes.Callvirt), new CodeMatch(OpCodes.Stloc_S), new CodeMatch(OpCodes.Ldloca_S), new CodeMatch(OpCodes.Call))
                .SetAndAdvance(OpCodes.Ldarg_2, null)
                .SetAndAdvance(OpCodes.Ldarg_S, 6)
                .SetAndAdvance(OpCodes.Call, AccessTools.DeclaredMethod(typeof(FixMirrorCameraPose), nameof(GetRotation)))
                .RemoveInstructions(3)
                .Instructions();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 GetPosition(Vector3 camPosition, Vector3 reflectionPlanePos, Vector3 reflectionPlaneNormal)
        {
            Vector3 v = camPosition - reflectionPlanePos;
            float dist = Vector3.Dot(v, reflectionPlaneNormal);
            return camPosition - 2f * dist * reflectionPlaneNormal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Quaternion GetRotation(Quaternion camRotation, Vector3 reflectionPlaneNormal) => Quaternion.LookRotation(Vector3.Reflect(camRotation * Vector3.forward, reflectionPlaneNormal), Vector3.Reflect(camRotation * Vector3.up, reflectionPlaneNormal));
    }

    /// <summary>
    /// This patch makes base game mirrors work properly when the stereo rendering mode is multi-pass.
    /// </summary>
    [HarmonyPatch]
    internal class MirrorRendererSOMultiPass
    {
        private static readonly MethodInfo XRSettingsGetStereoRenderingMode = AccessTools.DeclaredPropertyGetter(typeof(XRSettings), nameof(XRSettings.stereoRenderingMode));
        private static readonly MethodInfo CameraGetStereoEnabled = AccessTools.DeclaredPropertyGetter(typeof(Camera), nameof(Camera.stereoEnabled));
        private static readonly MethodInfo CameraGetProjectionMatrix = AccessTools.DeclaredPropertyGetter(typeof(Camera), nameof(Camera.projectionMatrix));
        private static readonly MethodInfo TransformGetPosition = AccessTools.DeclaredPropertyGetter(typeof(Transform), nameof(Transform.position));
        private static readonly MethodInfo GetEyePosition = AccessTools.DeclaredPropertyGetter(typeof(MirrorRendererSOMultiPass), nameof(EyePosition));
        private static readonly MethodInfo GetProjectionMatrix = AccessTools.DeclaredPropertyGetter(typeof(MirrorRendererSOMultiPass), nameof(ProjectionMatrix));
        private static readonly FieldInfo StereoTextureWidthField = AccessTools.DeclaredField(typeof(MirrorRendererSO), nameof(MirrorRendererSO._stereoTextureWidth));

        private static readonly Stack<(Vector3 eyePosition, Matrix4x4 projectionMatrix)> cameraParameters = [];

        private static Vector3 EyePosition => cameraParameters.Peek().eyePosition;

        private static Matrix4x4 ProjectionMatrix => cameraParameters.Peek().projectionMatrix;

        private static event Action<ScriptableRenderContext, Camera>? RenderSingleCamera;

        [HarmonyPatch(typeof(UniversalRenderPipeline), nameof(UniversalRenderPipeline.RenderSingleCamera), [typeof(ScriptableRenderContext), typeof(UniversalCameraData)])]
        [HarmonyPrefix]
        public static void UniversalRenderPipeline_RenderSingleCamera(ScriptableRenderContext context, UniversalCameraData cameraData)
        {
            // Contrary to Camera.transform.position & Camera.projectionMatrix, these properly take into account the current XR eye in multi-pass.
            // Since calls to RenderSingleCamera can be nested (e.g. with the mirror renderer) we use a stack to store values.
            cameraParameters.Push((Matrix4x4.Inverse(cameraData.GetViewMatrix()).GetPosition(), cameraData.GetProjectionMatrix()));

            // RenderSingleCamera happens a bit after RenderPipelineManager.beginCameraRendering so we use our own event
            RenderSingleCamera?.Invoke(context, cameraData.camera);
        }

        [HarmonyPatch(typeof(UniversalRenderPipeline), nameof(UniversalRenderPipeline.RenderSingleCamera), [typeof(ScriptableRenderContext), typeof(UniversalCameraData)])]
        [HarmonyPostfix]
        public static void UniversalRenderPipeline_RenderSingleCamera_Post()
        {
            cameraParameters.Pop();
        }

        [HarmonyPatch(typeof(Mirror), nameof(Mirror.OnEnable))]
        [HarmonyPostfix]
        public static void OnEnable(Mirror __instance)
        {
            RenderPipelineManager.beginCameraRendering -= __instance.OnBeginCameraRendering;
            RenderSingleCamera += __instance.OnBeginCameraRendering;
        }

        [HarmonyPatch(typeof(Mirror), nameof(Mirror.OnDisable))]
        [HarmonyPostfix]
        public static void OnDisable(Mirror __instance)
        {
            RenderSingleCamera -= __instance.OnBeginCameraRendering;
        }

        [HarmonyPatch(typeof(MirrorRendererSO), nameof(MirrorRendererSO.RenderMirrorTexture))]
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

                // replace `camera.position` with our `eyePosition` (this allows the render texture caching to work properly)
                .RemoveInstruction()
                .SetAndAdvance(OpCodes.Call, GetEyePosition)

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

                // replace `camera.projectionMatrix` with our `projectionMatrix`
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(i => i.Calls(CameraGetProjectionMatrix)))
                .SetAndAdvance(OpCodes.Call, GetProjectionMatrix)
                .RemoveInstruction()
                .Instructions();
        }
    }
}
