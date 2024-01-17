using System.Collections.Generic;

namespace AssetBundleLoadingTools.Models.Shaders
{
    /// <summary>
    /// Information regarding a shader replacement operation.
    /// </summary>
    public class ShaderReplacementInfo
    {
        /// <summary>
        /// Gets whether or not all shaders used by the object were replaced.
        /// </summary>
        public bool AllShadersReplaced { get; }

        /// <summary>
        /// Gets the names of the shaders that could not be replaced.
        /// </summary>
        public List<string> MissingShaderNames { get; }

        internal ShaderReplacementInfo(bool allShadersReplaced, List<string>? missingShaderNames = null) 
        {
            if (missingShaderNames == null) missingShaderNames = new List<string>();

            AllShadersReplaced = allShadersReplaced;
            MissingShaderNames = missingShaderNames;
        }
    }
}
