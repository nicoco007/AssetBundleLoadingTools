﻿using AssetBundleLoadingTools.Config;
using AssetBundleLoadingTools.Core;
using AssetBundleLoadingTools.UI;
using BeatSaberMarkupLanguage.Settings;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using System.Threading;
using UnityEngine.XR;
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
        private static Timer? _debuggerWriteTimer { get; set; }

        private SettingsHost settingsHost = new();

        [Init]
        public void Init(IPALogger logger, IPA.Config.Config config)
        {
            Log = logger;
            Config = config.Generated<PluginConfig>();

            if (Config.ShaderDebugging)
            {
                _debuggerWriteTimer = new Timer(ShaderDebugger.SerializeDebuggingInfo, null, 30000, 30000);
            }

            // Not ideal for the synchronous load time addition, but unfortunately necessary if we want to support synchronous ShaderRepair support
            // Might eventually become more of a problem load-time wise; for now there's relatively few bundles so i'm not too worried
            // Because of how Unity AssetBundle loading works, say the following situation comes up:
            // LoadAllBundlesAsync()[starting] -> LoadAllBundlesSync()[starting] -> LoadAllBundlesSync()[ending] ->LoadAllBundlesAsync()[ending]
            // If the async method is halfway through loading an assetbundle, then suddenly a LoadAllBundlesSync call blocks the main thread, Unity throws this exception:
            // "The AssetBundle 'IO.Stream' can't be loaded because another AssetBundle with the same files is already loaded."
            // Completely unavoidable even if you JUST started loading - unity REALLY does not like you trying to load assetbundles with the same name (even if the files are completely different)
            ShaderBundleLoader.Instance.LoadAllBundles();

            if (Config.DownloadNewBundles)
            {
                ShaderBundleLoader.Instance.LoadExtraWebBundlesAsync();
            }
            else
            {
                ShaderBundleLoader.Instance.WebBundlesLoaded = true;
            }
        }

        [OnStart]
        public void OnApplicationStart()
        {
            harmony.PatchAll();

            BSMLSettings.instance.AddSettingsMenu("Asset Bundles", "AssetBundleLoadingTools.UI.Settings.bsml", settingsHost);
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            _debuggerWriteTimer?.Dispose();
            harmony.UnpatchSelf();

            BSMLSettings.instance.RemoveSettingsMenu(settingsHost);
        }
    }
}
