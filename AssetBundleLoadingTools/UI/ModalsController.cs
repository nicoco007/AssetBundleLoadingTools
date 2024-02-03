using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Screen = HMUI.Screen;

namespace AssetBundleLoadingTools.UI
{
    internal class ModalsController : MonoBehaviour
    {
        private struct VoidResult { }

        private static TaskCompletionSource<VoidResult> instanceAvailableTaskCompletionSource = new();

        private readonly Dictionary<Screen, ModalView> dummyModals = new();

        private HierarchyManager hierarchyManager = null!;
        private MenuTransitionsHelper menuTransitionsHelper = null!;

        [UIComponent("multi-pass-modal")]
        protected ModalView? multiPassModal;

        private ModalView? modalTemplate;
        private ModalView? visibleModal;

        internal static ModalsController? Instance { get; private set; }

        internal static async Task ShowMultiPassModalAsync()
        {
            if (Plugin.Config.EnableMultiPassRendering || !Plugin.Config.ShowMultiPassModal)
            {
                return;
            }

            await instanceAvailableTaskCompletionSource.Task;

            await Instance!.ShowAsync(Instance.multiPassModal!);
        }

        [Inject]
        protected void Construct(HierarchyManager hierarchyManager, MenuTransitionsHelper menuTransitionsHelper)
        {
            this.hierarchyManager = hierarchyManager;
            this.menuTransitionsHelper = menuTransitionsHelper;
        }

        [UIAction("#post-parse")]
        protected void PostParse()
        {
            var contentSizeFitter = multiPassModal!.gameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            multiPassModal.gameObject.AddComponent<StackLayoutGroup>();

            var gameObject = new GameObject("DummyModalTemplate");
            gameObject.transform.SetParent(transform, false);
            gameObject.SetActive(false);
            modalTemplate = gameObject.AddComponent(multiPassModal)!;
        }

        private ModalView CreateDummyModal()
        {
            return new GameObject("DummyModal").AddComponent(modalTemplate)!;
        }

        internal async Task ShowAsync(ModalView mainScreenModal)
        {
            if (mainScreenModal == null)
            {
                throw new ArgumentNullException(nameof(mainScreenModal));
            }

            if (visibleModal != null)
            {
                return;
            }

            visibleModal = mainScreenModal;

            List<Screen> screens;

            // wait for view controllers to be displayed (should only take a few frames max)
            while (!(screens = hierarchyManager.GetComponentsInChildren<Screen>().Where(s => s._rootViewController != null).ToList()).Any())
            {
                await Task.Yield();
            }

            Screen mainScreen = screens.FirstOrDefault(s => s.name.Contains("Main"));

            if (mainScreen == null)
            {
                mainScreen = screens.First();
            }

            Plugin.Log.Debug($"Displaying modal on screen {mainScreen.name}");

            foreach (Screen screen in screens.Where(s => s != mainScreen))
            {
                if (!dummyModals.TryGetValue(screen, out ModalView? modal) || modal == null)
                {
                    Plugin.Log.Debug($"Creating dummy modal on screen {screen.name}");

                    modal = CreateDummyModal();
                    dummyModals.Add(screen, modal);
                }

                ConfigureAndShowModal(modal, screen);
            }

            ConfigureAndShowModal(mainScreenModal, mainScreen);
        }

        internal void Hide()
        {
            if (visibleModal != null)
            {
                visibleModal.Hide(true, () => visibleModal = null);
            }

            foreach (var modal in dummyModals.Values)
            {
                modal.Hide(true);
            }
        }

        protected void YesButtonClicked()
        {
            Plugin.Config.EnableMultiPassRendering = true;
            menuTransitionsHelper.RestartGame();
        }

        protected void NoButtonClicked()
        {
            Hide();
        }

        protected void DontAskAgainButtonClicked()
        {
            Plugin.Config.ShowMultiPassModal = false;
            Hide();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            instanceAvailableTaskCompletionSource.SetResult(default);
        }

        private void OnDisable()
        {
            instanceAvailableTaskCompletionSource = new();
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        private void ConfigureAndShowModal(ModalView modalView, Screen screen)
        {
            Transform screenTransform = screen.transform;

            modalView.transform.SetParent(screenTransform, false);
            modalView._viewIsValid = false;
            modalView._animateParentCanvas = true; // this gets set to false improperly sometimes
            modalView.SetupView(screenTransform); // force use of the screen instead of the child view controller
            modalView.Show(true, true);

            // this kinda sucks but there's no straightforward way to have the ModalView ignore the view controller
            ViewController viewController = screen.GetComponentInChildren<ViewController>();

            if (viewController != null)
            {
                viewController.didDeactivateEvent -= modalView.HandleParentViewControllerDidDeactivate;
            }
        }
    }
}
