using AssetBundleLoadingTools.Core;
using AssetBundleLoadingTools.Models.Shaders;
using AssetBundleLoadingTools.UI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AssetBundleLoadingTools.Utilities
{
    /// <summary>
    /// Helpers to replace legacy shaders on loaded assets.
    /// </summary>
    public static class ShaderRepair
    {
        /// <summary>
        /// Replace legacy shaders on the given <see cref="GameObject"/>.
        /// </summary>
        /// <param name="gameObject">The <see cref="GameObject"/> on which to replace shaders.</param>
        /// <returns>A <see cref="ShaderReplacementInfo"/> instance containing information on the result of the operation.</returns>
        public static ShaderReplacementInfo FixShadersOnGameObject(GameObject gameObject)
        {
            MainThreadCheck();

            var materials = GetMaterialsFromGameObjectRenderers(gameObject);
            var shaderInfos = GetShaderInfosFromMaterials(materials);

            return ReplaceShaders(materials, shaderInfos);
        }

        /// <summary>
        /// Replace legacy shaders on the given <see cref="GameObject"/> asynchronously.
        /// </summary>
        /// <param name="gameObject">The <see cref="GameObject"/> on which to replace shaders.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<ShaderReplacementInfo> FixShadersOnGameObjectAsync(GameObject gameObject)
        {
            MainThreadCheck();
            await ShaderBundleLoader.Instance.WaitForWebBundles(); // wait to catch up on "new" online bundles 

            // a bit expensive due to the GetComponentsInChildren call, but can't be awaited
            var materials = GetMaterialsFromGameObjectRenderers(gameObject);
            var shaderInfos = GetShaderInfosFromMaterials(materials);

            return await ReplaceShadersAsync(materials, shaderInfos);
        }

        /// <summary>
        /// Replace the shader on the given <see cref="Material"/> if it is a legacy shader.
        /// </summary>
        /// <param name="material">The material on which the shader will be replaced, if necessary.</param>
        /// <returns>A <see cref="ShaderReplacementInfo"/> instance containing information on the result of the operation.</returns>
        public static ShaderReplacementInfo FixShaderOnMaterial(Material material) => FixShadersOnMaterials(new List<Material>() { material });

        /// <summary>
        /// Replace legacy shaders on the given <see cref="Material"/>s.
        /// </summary>
        /// <param name="materials">The <see cref="Material"/>s on which to replace shaders.</param>
        /// <returns>A <see cref="ShaderReplacementInfo"/> instance containing information on the result of the operation.</returns>
        public static ShaderReplacementInfo FixShadersOnMaterials(List<Material> materials)
        {
            MainThreadCheck();

            var shaderInfos = GetShaderInfosFromMaterials(materials);

            return ReplaceShaders(materials, shaderInfos);
        }

        /// <summary>
        /// Replace the shader on the given <see cref="Material"/> if it is a legacy shader.
        /// </summary>
        /// <param name="material">The material on which the shader will be replaced, if necessary.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<ShaderReplacementInfo> FixShaderOnMaterialAsync(Material material) => await FixShadersOnMaterialsAsync(new List<Material>() { material });

        /// <summary>
        /// Replace legacy shaders on the given <see cref="Material"/>s.
        /// </summary>
        /// <param name="materials">The <see cref="Material"/>s on which to replace shaders.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<ShaderReplacementInfo> FixShadersOnMaterialsAsync(List<Material> materials)
        {
            MainThreadCheck();
            await ShaderBundleLoader.Instance.WaitForWebBundles(); // wait to catch up on "new" online bundles 

            var shaderInfos = GetShaderInfosFromMaterials(materials);

            return await ReplaceShadersAsync(materials, shaderInfos);
        }

        private static void MainThreadCheck()
        {
            if (!UnityGame.OnMainThread)
            {
                throw new InvalidOperationException("ShaderRepair methods must be called from the main thread.");
            }
        }

        private static async Task<ShaderReplacementInfo> ReplaceShadersAsync(List<Material> materials, List<CompiledShaderInfo> shaderInfos)
        {
            if (Plugin.Config.EnableMultiPassRendering || shaderInfos.All(x => x.IsSupported))
            {
                // TODO: explicit override for "force compiled" stuff
                // TODO: explicit override for BS uber only/standard only/etc
                return new ShaderReplacementInfo(true);
            }

            // if any shaders invalid entire model needs to be converted
            List<string> missingShaderNames = new();
            foreach (var shaderInfo in shaderInfos)
            {
                // TODO: Smarter handling of "partial replacement" here
                var (replacement, replacementMatchInfo) = await ShaderBundleLoader.Instance.GetReplacementShaderAsync(shaderInfo); // main async difference is how assetbundle.load is called

                if (replacement == null)
                {
                    missingShaderNames.Add(shaderInfo.Name); 
                }

                foreach (var material in materials)
                {
                    if (material == null || material.shader == null || material.shader != shaderInfo.Shader) continue;

                    if (replacement != null)
                    {
                        material.shader = replacement.Shader;
                    }
                    else if (!Plugin.Config.ShowUnsupportedShaders)
                    {
                        material.shader = ShaderBundleLoader.Instance.InvalidShader;
                    }
                }

                if (Plugin.Config.ShaderDebugging)
                {
                    ShaderDebugger.AddInfoToDebugging(new ShaderDebugInfo(shaderInfo, replacementMatchInfo));
                }
            }

            if (missingShaderNames.Any())
            {
                ShowMultiPassModal();
            }

            return new ShaderReplacementInfo(missingShaderNames.Count == 0, missingShaderNames);
        }

        private static ShaderReplacementInfo ReplaceShaders(List<Material> materials, List<CompiledShaderInfo> shaderInfos)
        {
            if (Plugin.Config.EnableMultiPassRendering || shaderInfos.All(x => x.IsSupported))
            {
                // TODO: explicit override for "force compiled" stuff
                // TODO: explicit override for BS uber only/standard only/etc
                return new ShaderReplacementInfo(true);
            }

            // if any shaders invalid entire model needs to be converted
            List<string> missingShaderNames = new();
            foreach (var shaderInfo in shaderInfos)
            {
                if (shaderInfo.IsSupported) continue;

                var (replacement, replacementMatchInfo) = ShaderBundleLoader.Instance.GetReplacementShader(shaderInfo); // main async difference is how assetbundle.load is called

                if (replacement == null)
                {
                    missingShaderNames.Add(shaderInfo.Name);
                }

                foreach (var material in materials)
                {
                    if (material.shader != shaderInfo.Shader) continue;

                    if (replacement != null)
                    {
                        material.shader = replacement.Shader;
                    }
                    else if (!Plugin.Config.ShowUnsupportedShaders)
                    {
                        material.shader = ShaderBundleLoader.Instance.InvalidShader;
                    }
                }

                if (Plugin.Config.ShaderDebugging)
                {
                    ShaderDebugger.AddInfoToDebugging(new ShaderDebugInfo(shaderInfo, replacementMatchInfo));
                }
            }

            if (missingShaderNames.Any())
            {
                ShowMultiPassModal();
            }

            return new ShaderReplacementInfo(missingShaderNames.Count == 0, missingShaderNames);
        }

        private static List<CompiledShaderInfo> GetShaderInfosFromMaterials(List<Material> materials) 
        {
            var shaders = materials.Where(m => m != null && m.shader != null).Select(m => m.shader).Distinct();
            List<CompiledShaderInfo> shaderInfos = new();

            foreach (var shader in shaders)
            {
                var shaderInfo = new CompiledShaderInfo(shader, GetKeywordsFromShader(shader));
                shaderInfos.Add(shaderInfo);
            }

            return shaderInfos;
        }

        /// <summary>
        /// Gets all the materials used by components on the given <see cref="GameObject"/>.
        /// </summary>
        /// <param name="gameObject">The <see cref="GameObject"/> on which to look for materials.</param>
        /// <returns>The list of <see cref="Material"/>s found.</returns>
        public static List<Material> GetMaterialsFromGameObjectRenderers(GameObject gameObject)
        {
            List<Material> sharedMaterials = new();

            // TODO: Is there anything that uses shaders that isn't a renderer?
            foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null) continue;

                    if (!sharedMaterials.Contains(material)) sharedMaterials.Add(material);
                }
            }

            return sharedMaterials;
        }

        private static List<string> GetKeywordsFromShader(Shader shader)
        {
            // Hardcoded for now, eventually swapped in for pointer magic

            return new List<string> { Constants.SinglePassKeyword };
        }

        private static void FixAndDownloadLegacyShaders(GameObject gameObject)
        {
            // unimplemented
            // hopefully this will eventually:
            // 1) fix the shaders it can
            // 2) if any shaders are missing, ask for consent to download more / upload model metadata
            // 3) if permission granted, check for shaders in API
            // 4) if shaders exist in API, download and cache locally for future use

            // I am unsure if it is 100% necessary to ask for a download prompt, as it *would* be smoother to do everything automatically
            // However, I am worried about a situation such as: somebody has a private/secret saber, a shader is broken, and they are understandably upset about some data being uploaded
            // Maybe just check the model's hash for matches, and if *that* exists don't prompt, but otherwise do

            // The actual necessary parameters you would need to send to the web api:
            // Hash, List<Shader> = (ShaderName, List<Properties>)
            // Properties list is very important because some people edit shaders with the same name to have slightly different properties based on usecase
        }

        private static void ShowMultiPassModal()
        {
            ModalsController.ShowMultiPassModalAsync().ContinueWith(task => Plugin.Log.Critical(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
