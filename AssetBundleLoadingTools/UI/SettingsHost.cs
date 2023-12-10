namespace AssetBundleLoadingTools.UI
{
    internal class SettingsHost
    {
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
