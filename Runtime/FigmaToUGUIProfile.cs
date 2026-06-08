using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigmaToUGUI
{
    [CreateAssetMenu(menuName = "Figma To UGUI/Import Profile", fileName = "FigmaToUGUIProfile")]
    public sealed class FigmaToUGUIProfile : ScriptableObject
    {
        public bool useTextMeshPro;
        public bool applyAutoLayout;
        public bool applyConstraints = true;
        public bool inferInteractiveComponents = true;
        public bool stretchRootToParent;
        public bool createPrefabsForSyncedRoots;
        public bool replaceExistingImport = true;
        public bool preserveUserChildrenOnResync = true;
        public bool addImportMetadata = true;
        public Font defaultLegacyFont;
        public UnityEngine.Object defaultTextMeshProFontAsset;
        public List<FigmaFontMapping> fontMappings = new List<FigmaFontMapping>();
        public List<FigmaPrefabMapping> prefabMappings = new List<FigmaPrefabMapping>();
        public List<FigmaNineSliceRule> nineSliceRules = new List<FigmaNineSliceRule>();

        public Font FindLegacyFont(string figmaFontFamily)
        {
            for (int i = 0; i < fontMappings.Count; i++)
            {
                FigmaFontMapping mapping = fontMappings[i];
                if (mapping != null && mapping.Matches(figmaFontFamily) && mapping.legacyFont != null)
                {
                    return mapping.legacyFont;
                }
            }

            return defaultLegacyFont;
        }

        public UnityEngine.Object FindTextMeshProFontAsset(string figmaFontFamily)
        {
            for (int i = 0; i < fontMappings.Count; i++)
            {
                FigmaFontMapping mapping = fontMappings[i];
                if (mapping != null && mapping.Matches(figmaFontFamily) && mapping.textMeshProFontAsset != null)
                {
                    return mapping.textMeshProFontAsset;
                }
            }

            return defaultTextMeshProFontAsset;
        }

        public FigmaPrefabMapping FindPrefabMapping(string nodeName)
        {
            for (int i = 0; i < prefabMappings.Count; i++)
            {
                FigmaPrefabMapping mapping = prefabMappings[i];
                if (mapping != null && mapping.prefab != null && mapping.Matches(nodeName))
                {
                    return mapping;
                }
            }

            return null;
        }

        public FigmaNineSliceRule FindNineSliceRule(string nodeName)
        {
            for (int i = 0; i < nineSliceRules.Count; i++)
            {
                FigmaNineSliceRule rule = nineSliceRules[i];
                if (rule != null && rule.Matches(nodeName))
                {
                    return rule;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class FigmaFontMapping
    {
        public string figmaFontFamily;
        public Font legacyFont;
        public UnityEngine.Object textMeshProFontAsset;

        public bool Matches(string value)
        {
            return !string.IsNullOrEmpty(figmaFontFamily) &&
                   string.Equals(figmaFontFamily, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public sealed class FigmaPrefabMapping
    {
        public string layerNameContains;
        public GameObject prefab;
        public bool importChildren;

        public bool Matches(string nodeName)
        {
            return !string.IsNullOrEmpty(layerNameContains) &&
                   !string.IsNullOrEmpty(nodeName) &&
                   nodeName.IndexOf(layerNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    [Serializable]
    public sealed class FigmaNineSliceRule
    {
        public string layerNameContains;
        public Vector4 border;

        public bool Matches(string nodeName)
        {
            return !string.IsNullOrEmpty(layerNameContains) &&
                   !string.IsNullOrEmpty(nodeName) &&
                   nodeName.IndexOf(layerNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
