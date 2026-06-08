using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUGUI.Editor
{
    internal sealed class FigmaToUGUIImporter
    {
        private const int MaxConcurrentSpriteDownloads = 4;

        private readonly FigmaApiClient client;
        private readonly FigmaImportSettings settings;
        private readonly Dictionary<string, Sprite> spritesByNodeId = new Dictionary<string, Sprite>();

        public FigmaToUGUIImporter(FigmaApiClient client, FigmaImportSettings settings)
        {
            this.client = client;
            this.settings = settings;
        }

        public async Task<GameObject> ImportAsync(FigmaFile file)
        {
            ThrowIfCancelled();
            Report("Preparing import", "Resolving Figma root node...", 0.16f);

            if (file == null || file.document == null)
            {
                throw new InvalidOperationException("No Figma document was returned.");
            }

            FigmaNode root = ResolveImportRoot(file.document);
            if (root == null)
            {
                throw new InvalidOperationException("Could not find a visible frame or node to import.");
            }

            EnsureOutputFolder();

            if (settings.renderUnsupportedNodesAsImages)
            {
                await PrepareRenderedSpritesAsync(root);
            }

            ThrowIfCancelled();
            Report("Building hierarchy", "Creating Unity UI objects...", 0.75f);

            Transform parent = ResolveParent();
            if (settings.replaceExistingImport)
            {
                parent = ReplaceExistingImport(parent, root);
            }

            int totalNodes = CountImportableNodes(root);
            int createdNodes = 0;
            GameObject imported = CreateNode(root, parent, null, true, totalNodes, ref createdNodes);
            imported.name = SafeName(root.name, "Figma Import");

            Selection.activeGameObject = imported;
            EditorGUIUtility.PingObject(imported);
            Report("Import complete", "Imported " + createdNodes + " UI object(s).", 1f);
            return imported;
        }

        private FigmaNode ResolveImportRoot(FigmaNode document)
        {
            if (document.type != "DOCUMENT" && document.type != "CANVAS")
            {
                return document;
            }

            if (document.children == null)
            {
                return document;
            }

            for (int i = 0; i < document.children.Count; i++)
            {
                FigmaNode child = document.children[i];
                if (child.visible && child.HasBounds)
                {
                    return child;
                }

                FigmaNode nested = ResolveImportRoot(child);
                if (nested != null && nested.HasBounds)
                {
                    return nested;
                }
            }

            return document;
        }

        private async Task PrepareRenderedSpritesAsync(FigmaNode root)
        {
            ThrowIfCancelled();
            Report("Preparing sprites", "Finding layers that need generated sprites...", 0.18f);

            List<FigmaNode> renderNodes = new List<FigmaNode>();
            CollectNodesForRendering(root, renderNodes, true);

            List<string> nodeIds = new List<string>();
            for (int i = 0; i < renderNodes.Count; i++)
            {
                FigmaNode node = renderNodes[i];
                Sprite cachedSprite;
                if (settings.reuseGeneratedSprites && TryLoadCachedSprite(node, out cachedSprite))
                {
                    spritesByNodeId[node.id] = cachedSprite;
                    continue;
                }

                nodeIds.Add(node.id);
            }

            Dictionary<string, string> urls = await client.GetNodeImageUrlsAsync(settings.fileKey, nodeIds, settings.imageScale, settings.cancellationToken, settings.progress);
            int skippedSprites = 0;
            List<SpriteImportRecord> records = new List<SpriteImportRecord>();

            for (int start = 0; start < renderNodes.Count; start += MaxConcurrentSpriteDownloads)
            {
                ThrowIfCancelled();
                List<Task<SpriteImportRecord>> downloads = new List<Task<SpriteImportRecord>>();
                for (int i = start; i < Mathf.Min(start + MaxConcurrentSpriteDownloads, renderNodes.Count); i++)
                {
                    FigmaNode node = renderNodes[i];
                    if (spritesByNodeId.ContainsKey(node.id))
                    {
                        continue;
                    }

                    if (!urls.ContainsKey(node.id) || string.IsNullOrEmpty(urls[node.id]))
                    {
                        skippedSprites++;
                        continue;
                    }

                    downloads.Add(DownloadSpriteRecordAsync(node, urls[node.id], i, renderNodes.Count));
                }

                SpriteImportRecord[] downloaded = await Task.WhenAll(downloads.ToArray());
                for (int i = 0; i < downloaded.Length; i++)
                {
                    if (downloaded[i] != null)
                    {
                        records.Add(downloaded[i]);
                    }
                }
            }

            ImportSprites(records);

            if (skippedSprites > 0)
            {
                Debug.LogWarning("Figma to UGUI skipped " + skippedSprites + " generated sprite(s). Those nodes still import as RectTransforms, but their baked image could not be rendered by Figma.");
            }
        }

        private async Task<SpriteImportRecord> DownloadSpriteRecordAsync(FigmaNode node, string url, int index, int total)
        {
            ThrowIfCancelled();
            Report("Downloading sprites", "Downloading sprite " + (index + 1) + "/" + total + "...", 0.45f + 0.25f * ((float)index / Mathf.Max(1, total)));
            byte[] bytes = await client.DownloadBytesAsync(url, settings.cancellationToken);
            ThrowIfCancelled();

            return new SpriteImportRecord
            {
                node = node,
                bytes = bytes,
                assetPath = BuildSpriteAssetPath(node)
            };
        }

        private void CollectNodesForRendering(FigmaNode node, List<FigmaNode> renderNodes, bool isRoot)
        {
            if (node == null || !node.visible || !node.HasBounds)
            {
                return;
            }

            if (!isRoot && ShouldCollapseSubtreeAsSprite(node))
            {
                renderNodes.Add(node);
                return;
            }

            if (ShouldRenderNodeAsSprite(node))
            {
                renderNodes.Add(node);
                return;
            }

            if (node.children == null)
            {
                return;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                CollectNodesForRendering(node.children[i], renderNodes, false);
            }
        }

        private bool ShouldCollapseSubtreeAsSprite(FigmaNode node)
        {
            if (!settings.collapseVectorSubtrees || node.children == null || node.children.Count == 0)
            {
                return false;
            }

            if (node.type != "GROUP" && node.type != "COMPONENT" && node.type != "INSTANCE")
            {
                return false;
            }

            if (ContainsText(node))
            {
                return false;
            }

            return CountRenderableDescendants(node) >= Mathf.Max(2, settings.collapseVectorSubtreeThreshold);
        }

        private bool ContainsText(FigmaNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (node.type == "TEXT")
            {
                return true;
            }

            if (node.children == null)
            {
                return false;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                if (ContainsText(node.children[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private int CountRenderableDescendants(FigmaNode node)
        {
            if (node == null || node.children == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < node.children.Count; i++)
            {
                FigmaNode child = node.children[i];
                if (child == null || !child.visible || !child.HasBounds)
                {
                    continue;
                }

                if (ShouldRenderNodeAsSprite(child))
                {
                    count++;
                }

                count += CountRenderableDescendants(child);
            }

            return count;
        }

        private bool ShouldRenderNodeAsSprite(FigmaNode node)
        {
            if (node.type == "TEXT" || node.type == "FRAME" || node.type == "GROUP" ||
                node.type == "COMPONENT" || node.type == "INSTANCE" || node.type == "DOCUMENT" ||
                node.type == "CANVAS")
            {
                return false;
            }

            if (FigmaColorUtility.HasImageFill(node))
            {
                return true;
            }

            if (node.type == "VECTOR" || node.type == "BOOLEAN_OPERATION" || node.type == "STAR" ||
                node.type == "POLYGON" || node.type == "LINE")
            {
                return true;
            }

            if (node.fills != null && node.fills.Count > 1)
            {
                return true;
            }

            return false;
        }

        private GameObject CreateNode(FigmaNode node, Transform parent, FigmaRectangle parentBounds, bool isRoot, int totalNodes, ref int createdNodes)
        {
            ThrowIfCancelled();
            if (node == null || !node.visible || !node.HasBounds)
            {
                return null;
            }

            createdNodes++;
            if (createdNodes == 1 || createdNodes % 10 == 0 || createdNodes == totalNodes)
            {
                Report("Building hierarchy", "Creating UI object " + createdNodes + "/" + Mathf.Max(1, totalNodes) + "...", 0.75f + 0.24f * ((float)createdNodes / Mathf.Max(1, totalNodes)));
            }

            bool importChildren;
            bool mappedPrefab;
            GameObject go = CreateGameObjectForNode(node, out importChildren, out mappedPrefab);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyRect(rect, node, parentBounds, isRoot);
            ApplyMetadata(go, node);
            ApplyLayoutElement(go, node);

            if (!mappedPrefab && node.type == "TEXT")
            {
                ApplyText(go, node);
            }
            else if (!mappedPrefab && spritesByNodeId.ContainsKey(node.id))
            {
                ApplyImage(go, spritesByNodeId[node.id], Color.white, node);
            }
            else if (!mappedPrefab && ShouldHaveColorImage(node))
            {
                Color color;
                FigmaColorUtility.TryGetSolidColor(node, out color);
                ApplyImage(go, null, color, node);
            }

            if (importChildren && !spritesByNodeId.ContainsKey(node.id) && node.children != null)
            {
                for (int i = 0; i < node.children.Count; i++)
                {
                    CreateNode(node.children[i], rect, node.Bounds, false, totalNodes, ref createdNodes);
                }
            }

            ApplyAutoLayout(go, node);

            return go;
        }

        private int CountImportableNodes(FigmaNode node)
        {
            if (node == null || !node.visible || !node.HasBounds)
            {
                return 0;
            }

            int count = 1;
            if (node.children == null || spritesByNodeId.ContainsKey(node.id))
            {
                return count;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                count += CountImportableNodes(node.children[i]);
            }

            return count;
        }

        private GameObject CreateGameObjectForNode(FigmaNode node, out bool importChildren, out bool mappedPrefab)
        {
            importChildren = true;
            mappedPrefab = false;

            FigmaPrefabMapping mapping = settings.profile != null ? settings.profile.FindPrefabMapping(node.name) : null;
            if (mapping == null)
            {
                GameObject plain = new GameObject(SafeName(node.name, node.type), typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(plain, "Import Figma UI");
                return plain;
            }

            mappedPrefab = true;
            importChildren = mapping.importChildren;

            GameObject instance = PrefabUtility.InstantiatePrefab(mapping.prefab) as GameObject;
            if (instance == null)
            {
                GameObject fallback = new GameObject(SafeName(node.name, node.type), typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(fallback, "Import Figma UI");
                return fallback;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Import Figma UI");
            instance.name = SafeName(node.name, mapping.prefab.name);

            if (instance.GetComponent<RectTransform>() != null)
            {
                return instance;
            }

            GameObject wrapper = new GameObject(SafeName(node.name, node.type), typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(wrapper, "Import Figma UI");
            instance.transform.SetParent(wrapper.transform, false);
            return wrapper;
        }

        private void ApplyRect(RectTransform rect, FigmaNode node, FigmaRectangle parentBounds, bool isRoot)
        {
            FigmaRectangle bounds = node.Bounds;
            if (isRoot && settings.stretchRootToParent && rect.parent is RectTransform)
            {
                ApplyStretchToParent(rect);
                return;
            }

            if (!settings.applyConstraints || parentBounds == null || node.constraints == null)
            {
                ApplyFixedRect(rect, bounds, parentBounds);
                return;
            }

            ApplyConstrainedRect(rect, bounds, parentBounds, node.constraints);
        }

        private void ApplyStretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private void ApplyFixedRect(RectTransform rect, FigmaRectangle bounds, FigmaRectangle parentBounds)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(bounds.width, bounds.height);

            if (parentBounds == null)
            {
                rect.anchoredPosition = Vector2.zero;
                return;
            }

            rect.anchoredPosition = new Vector2(bounds.x - parentBounds.x, -(bounds.y - parentBounds.y));
        }

        private void ApplyConstrainedRect(RectTransform rect, FigmaRectangle bounds, FigmaRectangle parentBounds, FigmaConstraints constraints)
        {
            float left = bounds.x - parentBounds.x;
            float right = (parentBounds.x + parentBounds.width) - (bounds.x + bounds.width);
            float top = bounds.y - parentBounds.y;
            float bottom = (parentBounds.y + parentBounds.height) - (bounds.y + bounds.height);

            bool stretchX = constraints.horizontal == "STRETCH";
            bool stretchY = constraints.vertical == "STRETCH";
            bool scaleX = constraints.horizontal == "SCALE" && parentBounds.width > 0f;
            bool scaleY = constraints.vertical == "SCALE" && parentBounds.height > 0f;

            Vector2 anchorMin = Vector2.zero;
            Vector2 anchorMax = Vector2.zero;
            Vector2 pivot = new Vector2(0f, 1f);
            Vector2 size = new Vector2(bounds.width, bounds.height);
            Vector2 position = Vector2.zero;

            if (scaleX)
            {
                anchorMin.x = Mathf.Clamp01(left / parentBounds.width);
                anchorMax.x = Mathf.Clamp01((left + bounds.width) / parentBounds.width);
                pivot.x = 0.5f;
                size.x = 0f;
            }
            else if (stretchX)
            {
                anchorMin.x = 0f;
                anchorMax.x = 1f;
                pivot.x = 0.5f;
            }
            else if (constraints.horizontal == "MAX")
            {
                anchorMin.x = 1f;
                anchorMax.x = 1f;
                pivot.x = 1f;
                position.x = -right;
            }
            else if (constraints.horizontal == "CENTER")
            {
                anchorMin.x = 0.5f;
                anchorMax.x = 0.5f;
                pivot.x = 0.5f;
                position.x = (bounds.x + bounds.width * 0.5f) - (parentBounds.x + parentBounds.width * 0.5f);
            }
            else
            {
                anchorMin.x = 0f;
                anchorMax.x = 0f;
                pivot.x = 0f;
                position.x = left;
            }

            if (scaleY)
            {
                anchorMin.y = Mathf.Clamp01(bottom / parentBounds.height);
                anchorMax.y = Mathf.Clamp01((bottom + bounds.height) / parentBounds.height);
                pivot.y = 0.5f;
                size.y = 0f;
            }
            else if (stretchY)
            {
                anchorMin.y = 0f;
                anchorMax.y = 1f;
                pivot.y = 0.5f;
            }
            else if (constraints.vertical == "MAX")
            {
                anchorMin.y = 0f;
                anchorMax.y = 0f;
                pivot.y = 0f;
                position.y = bottom;
            }
            else if (constraints.vertical == "CENTER")
            {
                anchorMin.y = 0.5f;
                anchorMax.y = 0.5f;
                pivot.y = 0.5f;
                position.y = (parentBounds.y + parentBounds.height * 0.5f) - (bounds.y + bounds.height * 0.5f);
            }
            else
            {
                anchorMin.y = 1f;
                anchorMax.y = 1f;
                pivot.y = 1f;
                position.y = -top;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            if (stretchX)
            {
                Vector2 offsetMin = rect.offsetMin;
                Vector2 offsetMax = rect.offsetMax;
                offsetMin.x = left;
                offsetMax.x = -right;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }

            if (scaleX)
            {
                Vector2 offsetMin = rect.offsetMin;
                Vector2 offsetMax = rect.offsetMax;
                offsetMin.x = 0f;
                offsetMax.x = 0f;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }

            if (stretchY)
            {
                Vector2 offsetMin = rect.offsetMin;
                Vector2 offsetMax = rect.offsetMax;
                offsetMin.y = bottom;
                offsetMax.y = -top;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }

            if (scaleY)
            {
                Vector2 offsetMin = rect.offsetMin;
                Vector2 offsetMax = rect.offsetMax;
                offsetMin.y = 0f;
                offsetMax.y = 0f;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
        }

        private void ApplyText(GameObject go, FigmaNode node)
        {
            if (ShouldUseTextMeshPro() && TryApplyTextMeshPro(go, node))
            {
                return;
            }

            Text text = go.AddComponent<Text>();
            text.text = node.characters;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            if (node.style != null)
            {
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(node.style.fontSize));
                text.fontStyle = node.style.fontWeight >= 700f ? FontStyle.Bold : FontStyle.Normal;
                text.alignment = ResolveTextAnchor(node.style);
                if (node.style.lineHeightPx > 0f && node.style.fontSize > 0f)
                {
                    text.lineSpacing = node.style.lineHeightPx / node.style.fontSize;
                }

                if (settings.profile != null)
                {
                    Font mappedFont = settings.profile.FindLegacyFont(node.style.fontFamily);
                    if (mappedFont != null)
                    {
                        text.font = mappedFont;
                    }
                }
            }

            Color color;
            if (FigmaColorUtility.TryGetSolidColor(node, out color))
            {
                text.color = color;
            }
        }

        private bool TryApplyTextMeshPro(GameObject go, FigmaNode node)
        {
            Type textMeshProType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (textMeshProType == null)
            {
                return false;
            }

            Component component = go.AddComponent(textMeshProType);
            SetProperty(component, "text", node.characters);
            SetProperty(component, "enableWordWrapping", true);

            if (node.style != null)
            {
                SetProperty(component, "fontSize", node.style.fontSize);
                SetTmpAlignment(component, node.style);

                if (settings.profile != null)
                {
                    UnityEngine.Object mappedFont = settings.profile.FindTextMeshProFontAsset(node.style.fontFamily);
                    if (mappedFont != null)
                    {
                        SetProperty(component, "font", mappedFont);
                    }
                }
            }

            Graphic graphic = component as Graphic;
            if (graphic != null)
            {
                graphic.raycastTarget = false;

                Color color;
                if (FigmaColorUtility.TryGetSolidColor(node, out color))
                {
                    graphic.color = color;
                }
            }

            return true;
        }

        private void SetTmpAlignment(Component component, FigmaTypeStyle style)
        {
            Type alignmentType = Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
            if (alignmentType == null)
            {
                return;
            }

            string value = ResolveTmpAlignmentName(style);
            try
            {
                object parsed = Enum.Parse(alignmentType, value);
                SetProperty(component, "alignment", parsed);
            }
            catch (ArgumentException)
            {
            }
        }

        private string ResolveTmpAlignmentName(FigmaTypeStyle style)
        {
            bool center = style.horizontalAlign == "CENTER";
            bool right = style.horizontalAlign == "RIGHT";
            bool middle = style.verticalAlign == "CENTER";
            bool bottom = style.verticalAlign == "BOTTOM";

            if (bottom && center) return "Bottom";
            if (bottom && right) return "BottomRight";
            if (bottom) return "BottomLeft";
            if (middle && center) return "Center";
            if (middle && right) return "Right";
            if (middle) return "Left";
            if (center) return "Top";
            if (right) return "TopRight";
            return "TopLeft";
        }

        private static void SetProperty(Component component, string propertyName, object value)
        {
            PropertyInfo property = component.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(component, value, null);
                }
                catch (ArgumentException)
                {
                }
            }
        }

        private void ApplyImage(GameObject go, Sprite sprite, Color color, FigmaNode node)
        {
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;

            FigmaNineSliceRule rule = settings.profile != null ? settings.profile.FindNineSliceRule(node.name) : null;
            if (sprite != null && rule != null && rule.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
            }
        }

        private bool ShouldHaveColorImage(FigmaNode node)
        {
            if (node.type == "FRAME" || node.type == "COMPONENT" || node.type == "INSTANCE" ||
                node.type == "RECTANGLE" || node.type == "ELLIPSE")
            {
                Color unused;
                return FigmaColorUtility.TryGetSolidColor(node, out unused);
            }

            return false;
        }

        private TextAnchor ResolveTextAnchor(FigmaTypeStyle style)
        {
            bool left = style.horizontalAlign == "LEFT" || string.IsNullOrEmpty(style.horizontalAlign);
            bool center = style.horizontalAlign == "CENTER";
            bool right = style.horizontalAlign == "RIGHT";
            bool top = style.verticalAlign == "TOP" || string.IsNullOrEmpty(style.verticalAlign);
            bool middle = style.verticalAlign == "CENTER";

            if (top && left) return TextAnchor.UpperLeft;
            if (top && center) return TextAnchor.UpperCenter;
            if (top && right) return TextAnchor.UpperRight;
            if (middle && left) return TextAnchor.MiddleLeft;
            if (middle && center) return TextAnchor.MiddleCenter;
            if (middle && right) return TextAnchor.MiddleRight;
            if (left) return TextAnchor.LowerLeft;
            if (center) return TextAnchor.LowerCenter;
            return TextAnchor.LowerRight;
        }

        private void ApplyAutoLayout(GameObject go, FigmaNode node)
        {
            if (!settings.applyAutoLayout || string.IsNullOrEmpty(node.layoutMode))
            {
                return;
            }

            if (node.layoutMode != "HORIZONTAL" && node.layoutMode != "VERTICAL")
            {
                return;
            }

            HorizontalOrVerticalLayoutGroup group;
            if (node.layoutMode == "HORIZONTAL")
            {
                group = go.GetComponent<HorizontalLayoutGroup>();
                if (group == null)
                {
                    group = go.AddComponent<HorizontalLayoutGroup>();
                }
            }
            else
            {
                group = go.GetComponent<VerticalLayoutGroup>();
                if (group == null)
                {
                    group = go.AddComponent<VerticalLayoutGroup>();
                }
            }

            group.spacing = node.itemSpacing;
            group.padding = new RectOffset(
                Mathf.RoundToInt(node.padding.left),
                Mathf.RoundToInt(node.padding.right),
                Mathf.RoundToInt(node.padding.top),
                Mathf.RoundToInt(node.padding.bottom));
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = ResolveLayoutAlignment(node);

            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            }
        }

        private void ApplyLayoutElement(GameObject go, FigmaNode node)
        {
            if (!settings.applyAutoLayout)
            {
                return;
            }

            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }

            element.preferredWidth = node.Bounds.width;
            element.preferredHeight = node.Bounds.height;
            element.flexibleWidth = node.layoutGrow > 0f || node.layoutSizingHorizontal == "FILL" || node.layoutAlign == "STRETCH" ? 1f : 0f;
            element.flexibleHeight = node.layoutGrow > 0f || node.layoutSizingVertical == "FILL" || node.layoutAlign == "STRETCH" ? 1f : 0f;
        }

        private TextAnchor ResolveLayoutAlignment(FigmaNode node)
        {
            string primary = node.primaryAxisAlignItems;
            string counter = node.counterAxisAlignItems;

            if (node.layoutMode == "VERTICAL")
            {
                return ResolveTextAnchorLike(counter, primary);
            }

            return ResolveTextAnchorLike(primary, counter);
        }

        private TextAnchor ResolveTextAnchorLike(string horizontal, string vertical)
        {
            bool centerX = horizontal == "CENTER";
            bool right = horizontal == "MAX";
            bool centerY = vertical == "CENTER";
            bool bottom = vertical == "MAX";

            if (bottom && centerX) return TextAnchor.LowerCenter;
            if (bottom && right) return TextAnchor.LowerRight;
            if (bottom) return TextAnchor.LowerLeft;
            if (centerY && centerX) return TextAnchor.MiddleCenter;
            if (centerY && right) return TextAnchor.MiddleRight;
            if (centerY) return TextAnchor.MiddleLeft;
            if (centerX) return TextAnchor.UpperCenter;
            if (right) return TextAnchor.UpperRight;
            return TextAnchor.UpperLeft;
        }

        private Transform ResolveParent()
        {
            if (settings.parent != null)
            {
                return settings.parent;
            }

            if (!settings.addCanvasIfNeeded)
            {
                return null;
            }

            Canvas existingCanvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (existingCanvas != null)
            {
                return existingCanvas.transform;
            }

            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasGo.transform;
        }

        private Transform ReplaceExistingImport(Transform parent, FigmaNode root)
        {
            FigmaImportedNode selectedMetadata = settings.parent != null
                ? settings.parent.GetComponent<FigmaImportedNode>()
                : null;

            if (selectedMetadata != null && selectedMetadata.fileKey == settings.fileKey && selectedMetadata.nodeId == root.id)
            {
                parent = settings.parent.parent;
            }

            FigmaImportedNode[] importedNodes = parent != null
                ? parent.GetComponentsInChildren<FigmaImportedNode>(true)
                : UnityEngine.Object.FindObjectsOfType<FigmaImportedNode>(true);

            for (int i = 0; i < importedNodes.Length; i++)
            {
                FigmaImportedNode metadata = importedNodes[i];
                if (metadata.fileKey == settings.fileKey && metadata.nodeId == root.id)
                {
                    Undo.DestroyObjectImmediate(metadata.gameObject);
                    break;
                }
            }

            return parent;
        }

        private void ApplyMetadata(GameObject go, FigmaNode node)
        {
            if (!settings.addImportMetadata)
            {
                return;
            }

            FigmaImportedNode metadata = go.GetComponent<FigmaImportedNode>();
            if (metadata == null)
            {
                metadata = go.AddComponent<FigmaImportedNode>();
            }

            metadata.fileKey = settings.fileKey;
            metadata.nodeId = node.id;
            metadata.nodeName = node.name;
            metadata.nodeType = node.type;
            metadata.importedAtUtc = DateTime.UtcNow.ToString("o");
        }

        private void ImportSprites(List<SpriteImportRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                return;
            }

            ThrowIfCancelled();
            Report("Importing sprites", "Writing sprite files...", 0.7f);

            for (int i = 0; i < records.Count; i++)
            {
                SpriteImportRecord record = records[i];
                string fullPath = Path.GetFullPath(record.assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, record.bytes);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ThrowIfCancelled();
            Report("Importing sprites", "Applying sprite importer settings...", 0.72f);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < records.Count; i++)
                {
                    ApplySpriteImporterSettings(records[i]);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            for (int i = 0; i < records.Count; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(records[i].assetPath);
                if (sprite != null)
                {
                    spritesByNodeId[records[i].node.id] = sprite;
                }
            }
        }

        private void ApplySpriteImporterSettings(SpriteImportRecord record)
        {
            TextureImporter importer = AssetImporter.GetAtPath(record.assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                FigmaNineSliceRule rule = settings.profile != null ? settings.profile.FindNineSliceRule(record.node.name) : null;
                if (rule != null && rule.border != Vector4.zero)
                {
                    importer.spriteBorder = rule.border;
                }

                AssetDatabase.WriteImportSettingsIfDirty(record.assetPath);
                AssetDatabase.ImportAsset(record.assetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private string BuildSpriteAssetPath(FigmaNode node)
        {
            string scaleKey = settings.imageScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
            string safeName = MakeAssetSafeName(node.name + "_" + node.id + "_s" + scaleKey);
            return NormalizeAssetFolder(settings.outputFolder).TrimEnd('/') + "/" + safeName + ".png";
        }

        private bool TryLoadCachedSprite(FigmaNode node, out Sprite sprite)
        {
            sprite = null;
            string assetPath = BuildSpriteAssetPath(node);
            if (!File.Exists(Path.GetFullPath(assetPath)))
            {
                return false;
            }

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            return sprite != null;
        }

        private void EnsureOutputFolder()
        {
            settings.outputFolder = NormalizeAssetFolder(settings.outputFolder);
            Directory.CreateDirectory(Path.GetFullPath(settings.outputFolder));
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return "Assets/FigmaToUGUI/Generated";
            }

            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (folder != "Assets" && !folder.StartsWith("Assets/"))
            {
                return "Assets/FigmaToUGUI/Generated";
            }

            return folder;
        }

        private static string SafeName(string name, string fallback)
        {
            return string.IsNullOrEmpty(name) ? fallback : name;
        }

        private static string MakeAssetSafeName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                value = value.Replace(invalid[i], '_');
            }

            return value.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        }

        private bool ShouldUseTextMeshPro()
        {
            return settings.useTextMeshPro || (settings.profile != null && settings.profile.useTextMeshPro);
        }

        private void ThrowIfCancelled()
        {
            settings.cancellationToken.ThrowIfCancellationRequested();
        }

        private void Report(string title, string detail, float value)
        {
            if (settings.progress != null)
            {
                settings.progress(new FigmaImportProgress(title, detail, value));
            }
        }

        private sealed class SpriteImportRecord
        {
            public FigmaNode node;
            public byte[] bytes;
            public string assetPath;
        }

    }
}
