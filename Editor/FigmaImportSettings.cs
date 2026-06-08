using UnityEngine;
using System;
using System.Threading;

namespace FigmaToUGUI.Editor
{
    internal sealed class FigmaImportSettings
    {
        public string fileKey;
        public string nodeId;
        public string outputFolder = "Assets/FigmaToUGUI/Generated";
        public float imageScale = 2f;
        public bool renderUnsupportedNodesAsImages = true;
        public bool reuseGeneratedSprites = true;
        public bool collapseVectorSubtrees = true;
        public int collapseVectorSubtreeThreshold = 6;
        public bool addCanvasIfNeeded = true;
        public bool syncAllTopLevelFrames;
        public bool useTextMeshPro;
        public bool applyAutoLayout = true;
        public bool applyConstraints = true;
        public bool inferInteractiveComponents = true;
        public bool stretchRootToParent = true;
        public bool createPrefabsForSyncedRoots;
        public bool replaceExistingImport = true;
        public bool preserveUserChildrenOnResync = true;
        public bool addImportMetadata = true;
        public FigmaToUGUIProfile profile;
        public Transform parent;
        public CancellationToken cancellationToken;
        public Action<FigmaImportProgress> progress;
    }
}
