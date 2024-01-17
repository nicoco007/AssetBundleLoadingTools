﻿using Newtonsoft.Json;
using UnityEngine.Rendering;

namespace AssetBundleLoadingTools.Models.Shaders
{
    internal class ShaderProperty
    {
        public string Name { get; set; }

        public string DisplayName { get; set; } // Unsure if this is actually needed for comparisons. It did catch a different shader ONCE. Probably better to have too much data than not enough

        public ShaderPropertyType PropertyType { get; set; } // might be a good idea to avoid directly referencing Unity assemblies for models, depending on what these end up being used in

        // Default property value might be needed?
        [JsonConstructor]
        public ShaderProperty(string name, string displayName, ShaderPropertyType propertyType)
        {
            Name = name;
            DisplayName = displayName;
            PropertyType = propertyType;
        }
    }
}
