using AssetBundleLoadingTools.Config;
using AssetBundleLoadingTools.Installers;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;

namespace AssetBundleLoadingTools
{
    // Formatted as a plugin to support adding future planned BSML UI & Harmony Patches
    // This could probably be made more generic but we're running on borrowed time currently
    // Really most of this (except the public APIs in Utilities) needs to be made non-static
    [Plugin(RuntimeOptions.SingleStartInit)]
    internal class Plugin
    {
        private static readonly Harmony harmony = new("com.legoandmars.assetbundleloadingtools");

        /// <summary>
        /// Use to send log messages through BSIPA.
        /// </summary>
        internal static IPALogger Log { get; private set; } = null!;

        internal static PluginConfig Config { get; private set; } = null!;

        [Init]
        public void Init(IPALogger logger, IPA.Config.Config config, Zenjector zenjector)
        {
            Log = logger;
            Config = config.Generated<PluginConfig>();

            zenjector.Install<MainMenuInstaller>(Location.Menu);
        }

        [OnStart]
        public void OnApplicationStart()
        {
            harmony.PatchAll();
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            harmony.UnpatchSelf();
        }
    }
}
