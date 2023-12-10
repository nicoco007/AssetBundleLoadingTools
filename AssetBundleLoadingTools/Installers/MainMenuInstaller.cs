using AssetBundleLoadingTools.UI;
using Zenject;

namespace AssetBundleLoadingTools.Installers
{
    internal class MainMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.Bind(typeof(IInitializable)).To<ModalsControllerCreator>().AsSingle();
        }
    }
}
