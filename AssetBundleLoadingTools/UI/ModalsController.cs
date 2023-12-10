using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace AssetBundleLoadingTools.UI
{
    internal class ModalsController : MonoBehaviour
    {
        private struct VoidResult { }

        private static TaskCompletionSource<VoidResult> controllerAvailableTaskCompletionSource = new();

        private MenuTransitionsHelper menuTransitionsHelper = null!;

        [UIComponent("multi-pass-modal")]
        private ModalView? multiPassModal;

        private ModalView? leftScreenDummyModal;
        private ModalView? rightScreenDummyModal;

        internal static ModalsController? Instance { get; private set; }

        internal static async Task ShowMultiPassModalAsync()
        {
            if (Plugin.Config.EnableMultiPassRendering || !Plugin.Config.ShowMultiPassModal)
            {
                return;
            }

            await controllerAvailableTaskCompletionSource.Task;

            Instance!.Show(Instance.multiPassModal!);
        }

        [Inject]
        protected void Construct(MenuTransitionsHelper menuTransitionsHelper)
        {
            this.menuTransitionsHelper = menuTransitionsHelper;
        }

        [UIAction("#post-parse")]
        protected void PostParse()
        {
            // create empty modal views so we can block the other screens
            var screenSystem = gameObject.GetComponentInParent<ScreenSystem>();
            leftScreenDummyModal = CreateDummyModal(screenSystem.leftScreen);
            rightScreenDummyModal = CreateDummyModal(screenSystem.rightScreen);

            var contentSizeFitter = multiPassModal!.gameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            multiPassModal.gameObject.AddComponent<StackLayoutGroup>();
        }

        private ModalView CreateDummyModal(HMUI.Screen screen)
        {
            var gameObject = new GameObject("DummyModal");
            gameObject.SetActive(false);
            gameObject.transform.SetParent(screen.transform, false);
            return gameObject.AddComponent(multiPassModal)!;
        }

        internal void Show(ModalView mainScreenModal)
        {
            if (mainScreenModal != null)
            {
                mainScreenModal._viewIsValid = false;
                mainScreenModal.Show(true, true);
            }

            if (leftScreenDummyModal != null)
            {
                leftScreenDummyModal._viewIsValid = false;
                leftScreenDummyModal.Show(true, true);
            }

            if (rightScreenDummyModal != null)
            {
                rightScreenDummyModal._viewIsValid = false;
                rightScreenDummyModal.Show(true, true);
            }
        }

        internal void Hide(ModalView mainScreenModal)
        {
            if (mainScreenModal != null)
            {
                mainScreenModal.Hide(true);
            }

            if (leftScreenDummyModal != null)
            {
                leftScreenDummyModal.Hide(true);
            }

            if (rightScreenDummyModal != null)
            {
                rightScreenDummyModal.Hide(true);
            }
        }

        protected void YesButtonClicked()
        {
            Plugin.Config.EnableMultiPassRendering = true;
            menuTransitionsHelper.RestartGame();
        }

        protected void NoButtonClicked()
        {
            HideMultiPassModal();
        }

        protected void DontAskAgainButtonClicked()
        {
            Plugin.Config.ShowMultiPassModal = false;
            HideMultiPassModal();
        }

        private void HideMultiPassModal()
        {
            Hide(multiPassModal!);
        }

        private void Start()
        {
            Instance = this;
            controllerAvailableTaskCompletionSource.SetResult(default);
        }

        private void OnDestroy()
        {
            Instance = null;
            controllerAvailableTaskCompletionSource = new();
        }
    }
}
