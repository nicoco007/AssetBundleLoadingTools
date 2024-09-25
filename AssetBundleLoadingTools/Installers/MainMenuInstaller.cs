using AssetBundleLoadingTools.UI;
using System;
using Zenject;

namespace AssetBundleLoadingTools.Installers
{
    internal class MainMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind(typeof(IInitializable)).To<ModalsControllerCreator>().AsSingle();
            Container.Bind(typeof(IInitializable), typeof(IDisposable)).To<SettingsHost>().AsSingle();
        }
    }
}
