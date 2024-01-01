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

        private static TaskCompletionSource<VoidResult> controllerAvailableTaskCompletionSource = new();

        private HierarchyManager hierarchyManager = null!;
        private MenuTransitionsHelper menuTransitionsHelper = null!;

        [UIComponent("multi-pass-modal")]
        private ModalView? multiPassModal;

        private ModalView? modalTemplate;

        private readonly Stack<ModalView> modals = new();
        private readonly List<ModalView> visibleModals = new();

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
            var gameObject = new GameObject("DummyModal");
            gameObject.SetActive(false);
            return gameObject.AddComponent(modalTemplate)!;
        }

        internal void Show(ModalView mainScreenModal)
        {
            if (mainScreenModal == null)
            {
                throw new ArgumentNullException(nameof(mainScreenModal));
            }

            List<Screen> screens = hierarchyManager.GetComponentsInChildren<Screen>().Where(s => s._rootViewController != null).ToList();
            Screen mainScreen = screens.FirstOrDefault(s => s.name.Contains("Main"));

            if (mainScreen == null)
            {
                mainScreen = screens.First();
            }

            Plugin.Log.Notice($"Displaying modal on screen {mainScreen.name}");

            foreach (Screen screen in screens.Where(s => s != mainScreen))
            {
                ModalView modal = modals.Count > 0 ? modals.Pop() : CreateDummyModal();
                modal.transform.SetParent(screen.transform, false);
                modal._viewIsValid = false;
                modal._animateParentCanvas = true;
                modal.Show(true, true);

                visibleModals.Add(modal);
            }

            mainScreenModal.transform.SetParent(mainScreen.transform, false);
            mainScreenModal._viewIsValid = false;
            mainScreenModal._animateParentCanvas = true; // this gets set to false improperly sometimes
            mainScreenModal.Show(true, true);
        }

        internal void Hide(ModalView mainScreenModal)
        {
            mainScreenModal.Hide(true);

            foreach (var modal in visibleModals)
            {
                modal.Hide(true, () => modals.Push(modal));
            }

            visibleModals.Clear();
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

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            controllerAvailableTaskCompletionSource.SetResult(default);
        }

        private void OnDisable()
        {
            controllerAvailableTaskCompletionSource = new();
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}
