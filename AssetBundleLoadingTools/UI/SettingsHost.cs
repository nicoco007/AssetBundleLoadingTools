using BeatSaberMarkupLanguage.Settings;
using System;
using Zenject;

namespace AssetBundleLoadingTools.UI
{
    internal class SettingsHost : IInitializable, IDisposable
    {
        private readonly BSMLSettings bsmlSettings;

        private SettingsHost(BSMLSettings bsmlSettings)
        {
            this.bsmlSettings = bsmlSettings;
        }

        public void Initialize()
        {
            bsmlSettings.AddSettingsMenu("Asset Bundles", "AssetBundleLoadingTools.UI.Settings.bsml", this);
        }

        public void Dispose()
        {
            bsmlSettings.RemoveSettingsMenu(this);
        }

        internal bool EnableMultiPassRendering
        {
            get => Plugin.Config.EnableMultiPassRendering;
            set => Plugin.Config.EnableMultiPassRendering = value;
        }

        internal bool ShowUnsupportedShaders
        {
            get => Plugin.Config.ShowUnsupportedShaders;
            set => Plugin.Config.ShowUnsupportedShaders = value;
        }
    }
}
