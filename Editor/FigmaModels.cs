using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigmaToUGUI.Editor
{
    [Serializable]
    internal sealed class FigmaFile
    {
        public string name;
        public FigmaNode document;
        public bool hasMultipleSelectedNodes;
    }

    [Serializable]
    internal sealed class FigmaNode
    {
        public string id;
        public string name;
        public string type;
        public bool visible = true;
        public string characters;
        public FigmaRectangle absoluteBoundingBox;
        public FigmaRectangle absoluteRenderBounds;
        public List<FigmaNode> children = new List<FigmaNode>();
        public List<FigmaPaint> fills = new List<FigmaPaint>();
        public List<FigmaPaint> strokes = new List<FigmaPaint>();
        public FigmaTypeStyle style;
        public bool clipsContent;
        public string layoutMode;
        public string primaryAxisSizingMode;
        public string counterAxisSizingMode;
        public string primaryAxisAlignItems;
        public string counterAxisAlignItems;
        public string layoutAlign;
        public string layoutSizingHorizontal;
        public string layoutSizingVertical;
        public float layoutGrow;
        public float itemSpacing;
        public FigmaPadding padding;
        public FigmaConstraints constraints;

        public bool HasBounds
        {
            get
            {
                return HasValidBounds(absoluteBoundingBox) || HasValidBounds(absoluteRenderBounds);
            }
        }

        public FigmaRectangle Bounds
        {
            get
            {
                if (HasValidBounds(absoluteBoundingBox))
                {
                    return absoluteBoundingBox;
                }

                return absoluteRenderBounds;
            }
        }

        private static bool HasValidBounds(FigmaRectangle rectangle)
        {
            return rectangle != null && rectangle.width > 0f && rectangle.height > 0f;
        }
    }

    [Serializable]
    internal sealed class FigmaPaint
    {
        public string type;
        public bool visible = true;
        public float opacity = 1f;
        public FigmaColor color = new FigmaColor();
        public string imageRef;
        public string scaleMode;
    }

    [Serializable]
    internal sealed class FigmaColor
    {
        public float r;
        public float g;
        public float b;
        public float a = 1f;
    }

    [Serializable]
    internal sealed class FigmaRectangle
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    internal sealed class FigmaTypeStyle
    {
        public float fontSize = 14f;
        public float fontWeight = 400f;
        public float lineHeightPx;
        public string horizontalAlign;
        public string verticalAlign;
        public string fontFamily;
    }

    [Serializable]
    internal sealed class FigmaPadding
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }

    [Serializable]
    internal sealed class FigmaConstraints
    {
        public string horizontal = "MIN";
        public string vertical = "MIN";
    }

    internal static class FigmaModelParser
    {
        public static FigmaFile ParseFile(string json, string selectedNodeId)
        {
            Dictionary<string, object> root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new InvalidOperationException("Figma returned invalid JSON.");
            }

            FigmaFile file = new FigmaFile();
            file.name = ReadString(root, "name");

            if (!string.IsNullOrEmpty(selectedNodeId) && root.ContainsKey("nodes"))
            {
                Dictionary<string, object> nodes = root["nodes"] as Dictionary<string, object>;
                List<string> selectedNodeIds = SplitNodeIds(selectedNodeId);
                if (nodes == null || selectedNodeIds.Count == 0)
                {
                    throw new InvalidOperationException("The selected node id was not found in the Figma response.");
                }

                if (selectedNodeIds.Count > 1)
                {
                    FigmaNode selectedNodesRoot = new FigmaNode
                    {
                        id = "selected-nodes",
                        name = string.IsNullOrEmpty(file.name) ? "Selected Figma Nodes" : file.name,
                        type = "DOCUMENT",
                        visible = true
                    };

                    for (int i = 0; i < selectedNodeIds.Count; i++)
                    {
                        string nodeId = selectedNodeIds[i];
                        if (!nodes.ContainsKey(nodeId))
                        {
                            throw new InvalidOperationException("The selected node id was not found in the Figma response: " + nodeId);
                        }

                        FigmaNode node = ParseSelectedNode(nodes[nodeId] as Dictionary<string, object>);
                        if (node != null)
                        {
                            selectedNodesRoot.children.Add(node);
                        }
                    }

                    file.document = selectedNodesRoot;
                    file.hasMultipleSelectedNodes = true;
                }
                else
                {
                    string nodeId = selectedNodeIds[0];
                    if (!nodes.ContainsKey(nodeId))
                    {
                        throw new InvalidOperationException("The selected node id was not found in the Figma response.");
                    }

                    file.document = ParseSelectedNode(nodes[nodeId] as Dictionary<string, object>);
                }
            }
            else
            {
                file.document = ParseNode(root.ContainsKey("document") ? root["document"] as Dictionary<string, object> : null);
            }

            return file;
        }

        private static FigmaNode ParseSelectedNode(Dictionary<string, object> nodePayload)
        {
            Dictionary<string, object> document = nodePayload != null && nodePayload.ContainsKey("document")
                ? nodePayload["document"] as Dictionary<string, object>
                : null;

            return ParseNode(document);
        }

        private static List<string> SplitNodeIds(string value)
        {
            List<string> nodeIds = new List<string>();
            if (string.IsNullOrEmpty(value))
            {
                return nodeIds;
            }

            string[] parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string nodeId = parts[i].Trim();
                if (!string.IsNullOrEmpty(nodeId))
                {
                    nodeIds.Add(nodeId);
                }
            }

            return nodeIds;
        }

        private static FigmaNode ParseNode(Dictionary<string, object> data)
        {
            if (data == null)
            {
                return null;
            }

            FigmaNode node = new FigmaNode();
            node.id = ReadString(data, "id");
            node.name = ReadString(data, "name");
            node.type = ReadString(data, "type");
            node.visible = ReadBool(data, "visible", true);
            node.characters = ReadString(data, "characters");
            node.absoluteBoundingBox = ReadRectangle(data, "absoluteBoundingBox");
            node.absoluteRenderBounds = ReadRectangle(data, "absoluteRenderBounds");
            node.fills = ReadPaints(data, "fills");
            node.strokes = ReadPaints(data, "strokes");
            node.style = ReadTypeStyle(data, "style");
            node.clipsContent = ReadBool(data, "clipsContent", false);
            node.layoutMode = ReadString(data, "layoutMode");
            node.primaryAxisSizingMode = ReadString(data, "primaryAxisSizingMode");
            node.counterAxisSizingMode = ReadString(data, "counterAxisSizingMode");
            node.primaryAxisAlignItems = ReadString(data, "primaryAxisAlignItems");
            node.counterAxisAlignItems = ReadString(data, "counterAxisAlignItems");
            node.layoutAlign = ReadString(data, "layoutAlign");
            node.layoutSizingHorizontal = ReadString(data, "layoutSizingHorizontal");
            node.layoutSizingVertical = ReadString(data, "layoutSizingVertical");
            node.layoutGrow = ReadFloat(data, "layoutGrow");
            node.itemSpacing = ReadFloat(data, "itemSpacing");
            node.padding = new FigmaPadding
            {
                left = ReadFloat(data, "paddingLeft"),
                right = ReadFloat(data, "paddingRight"),
                top = ReadFloat(data, "paddingTop"),
                bottom = ReadFloat(data, "paddingBottom")
            };
            node.constraints = ReadConstraints(data, "constraints");

            List<object> children = ReadList(data, "children");
            if (children != null)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    FigmaNode child = ParseNode(children[i] as Dictionary<string, object>);
                    if (child != null)
                    {
                        node.children.Add(child);
                    }
                }
            }

            return node;
        }

        private static FigmaRectangle ReadRectangle(Dictionary<string, object> data, string key)
        {
            Dictionary<string, object> rect = ReadDictionary(data, key);
            if (rect == null)
            {
                return null;
            }

            return new FigmaRectangle
            {
                x = ReadFloat(rect, "x"),
                y = ReadFloat(rect, "y"),
                width = ReadFloat(rect, "width"),
                height = ReadFloat(rect, "height")
            };
        }

        private static List<FigmaPaint> ReadPaints(Dictionary<string, object> data, string key)
        {
            List<FigmaPaint> paints = new List<FigmaPaint>();
            List<object> rawPaints = ReadList(data, key);
            if (rawPaints == null)
            {
                return paints;
            }

            for (int i = 0; i < rawPaints.Count; i++)
            {
                Dictionary<string, object> rawPaint = rawPaints[i] as Dictionary<string, object>;
                if (rawPaint == null)
                {
                    continue;
                }

                FigmaPaint paint = new FigmaPaint
                {
                    type = ReadString(rawPaint, "type"),
                    visible = ReadBool(rawPaint, "visible", true),
                    opacity = ReadFloat(rawPaint, "opacity", 1f),
                    imageRef = ReadString(rawPaint, "imageRef"),
                    scaleMode = ReadString(rawPaint, "scaleMode")
                };

                Dictionary<string, object> color = ReadDictionary(rawPaint, "color");
                if (color != null)
                {
                    paint.color = new FigmaColor
                    {
                        r = ReadFloat(color, "r"),
                        g = ReadFloat(color, "g"),
                        b = ReadFloat(color, "b"),
                        a = ReadFloat(color, "a", 1f)
                    };
                }

                paints.Add(paint);
            }

            return paints;
        }

        private static FigmaTypeStyle ReadTypeStyle(Dictionary<string, object> data, string key)
        {
            Dictionary<string, object> style = ReadDictionary(data, key);
            if (style == null)
            {
                return null;
            }

            return new FigmaTypeStyle
            {
                fontSize = ReadFloat(style, "fontSize", 14f),
                fontWeight = ReadFloat(style, "fontWeight", 400f),
                lineHeightPx = ReadFloat(style, "lineHeightPx"),
                horizontalAlign = ReadString(style, "textAlignHorizontal"),
                verticalAlign = ReadString(style, "textAlignVertical"),
                fontFamily = ReadString(style, "fontFamily")
            };
        }

        private static FigmaConstraints ReadConstraints(Dictionary<string, object> data, string key)
        {
            Dictionary<string, object> constraints = ReadDictionary(data, key);
            if (constraints == null)
            {
                return new FigmaConstraints();
            }

            string horizontal = ReadString(constraints, "horizontal");
            string vertical = ReadString(constraints, "vertical");

            return new FigmaConstraints
            {
                horizontal = string.IsNullOrEmpty(horizontal) ? "MIN" : horizontal,
                vertical = string.IsNullOrEmpty(vertical) ? "MIN" : vertical
            };
        }

        private static Dictionary<string, object> ReadDictionary(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key))
            {
                return null;
            }

            return data[key] as Dictionary<string, object>;
        }

        private static List<object> ReadList(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key))
            {
                return null;
            }

            return data[key] as List<object>;
        }

        private static string ReadString(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null)
            {
                return string.Empty;
            }

            return data[key].ToString();
        }

        private static bool ReadBool(Dictionary<string, object> data, string key, bool defaultValue)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null)
            {
                return defaultValue;
            }

            if (data[key] is bool)
            {
                return (bool)data[key];
            }

            bool parsed;
            return bool.TryParse(data[key].ToString(), out parsed) ? parsed : defaultValue;
        }

        private static float ReadFloat(Dictionary<string, object> data, string key, float defaultValue = 0f)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null)
            {
                return defaultValue;
            }

            object value = data[key];
            if (value is double)
            {
                return (float)(double)value;
            }

            if (value is float)
            {
                return (float)value;
            }

            if (value is long)
            {
                return (long)value;
            }

            float parsed;
            return float.TryParse(value.ToString(), out parsed) ? parsed : defaultValue;
        }
    }
}
