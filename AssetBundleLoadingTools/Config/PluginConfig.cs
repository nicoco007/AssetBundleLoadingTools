﻿using UnityEngine.XR.OpenXR;

namespace AssetBundleLoadingTools.Config
{
    public class PluginConfig
    {
        public virtual bool ShaderDebugging { get; set; } = true;

        public virtual bool DownloadNewBundles { get; set; } = true;

        public virtual bool ShowUnsupportedShaders { get; set; } = false;

        public virtual bool EnableMultiPassRendering { get; set; } = false;

        public virtual bool ShowMultiPassModal { get; set; } = true;

        public virtual void Changed()
        {
            if (OpenXRSettings.Instance != null && OpenXRLoaderBase.Instance != null)
            {
                OpenXRSettings.Instance.renderMode = EnableMultiPassRendering ? OpenXRSettings.RenderMode.MultiPass : OpenXRSettings.RenderMode.SinglePassInstanced;
            }
        }
    }
}
