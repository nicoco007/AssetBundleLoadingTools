using System.ComponentModel;

namespace AssetBundleLoadingTools.UI
{
    internal class SettingsHost
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        internal bool enableShaderReplacement
        {
            get => Plugin.Config.EnableShaderReplacement;
            set => Plugin.Config.EnableShaderReplacement = value;
        }

        internal bool enableMultiPassRendering
        {
            get => Plugin.Config.EnableMultiPassRendering;
            set => Plugin.Config.EnableMultiPassRendering = value;
        }
    }
}
