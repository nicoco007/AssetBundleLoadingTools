﻿using BeatSaberMarkupLanguage;
using HMUI;
using System.IO;
using System.Reflection;
using UnityEngine;
using Zenject;

namespace AssetBundleLoadingTools.UI
{
    internal class ModalsControllerCreator : IInitializable
    {
        private readonly DiContainer container;
        private readonly BSMLParser bsmlParser;
        private readonly HierarchyManager hierarchyManager;

        protected ModalsControllerCreator(DiContainer container, BSMLParser bsmlParser, HierarchyManager hierarchyManager)
        {
            this.container = container;
            this.bsmlParser = bsmlParser;
            this.hierarchyManager = hierarchyManager;
        }

        public void Initialize()
        {
            GameObject gameObject = new($"{nameof(AssetBundleLoadingTools)} {nameof(ModalsController)}");
            gameObject.transform.SetParent(hierarchyManager.transform, false);
            gameObject.SetActive(false);

            var modalsController = container.InstantiateComponent<ModalsController>(gameObject);

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AssetBundleLoadingTools.UI.Modals.bsml"))
            using (var streamReader = new StreamReader(stream))
            {
                bsmlParser.Parse(streamReader.ReadToEnd(), gameObject, modalsController);
            }

            gameObject.SetActive(true);
        }
    }
}
