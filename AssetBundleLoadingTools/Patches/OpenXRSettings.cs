using HarmonyLib;
using UnityEngine.XR.OpenXR;

namespace AssetBundleLoadingTools.Patches
{
    [HarmonyPatch(typeof(OpenXRSettings), nameof(OpenXRSettings.ApplyRenderSettings))]
    internal class OpenXRSettings_ApplyRenderSettings
    {
        public static void Prefix(OpenXRSettings __instance)
        {
            __instance.m_renderMode = Plugin.Config.EnableMultiPassRendering ? OpenXRSettings.RenderMode.MultiPass : OpenXRSettings.RenderMode.SinglePassInstanced;
        }
    }
}
