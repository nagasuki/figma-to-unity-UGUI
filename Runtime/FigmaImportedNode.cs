using UnityEngine;

namespace FigmaToUGUI
{
    [DisallowMultipleComponent]
    public sealed class FigmaImportedNode : MonoBehaviour
    {
        public string fileKey;
        public string nodeId;
        public string nodeName;
        public string nodeType;
        public string importedAtUtc;
        public string prefabAssetPath;
        public string prefabSavedAtUtc;
    }
}
