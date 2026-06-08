using UnityEngine;

namespace FigmaToUGUI.Editor
{
    internal static class FigmaColorUtility
    {
        public static Color FromFigmaColor(FigmaColor color, float opacity = 1f)
        {
            return new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(color.a * opacity));
        }

        public static bool TryGetSolidColor(FigmaNode node, out Color color)
        {
            color = Color.white;

            if (node.fills == null || node.fills.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < node.fills.Count; i++)
            {
                FigmaPaint paint = node.fills[i];
                if (!paint.visible)
                {
                    continue;
                }

                if (paint.type == "SOLID")
                {
                    color = FromFigmaColor(paint.color, paint.opacity);
                    return true;
                }
            }

            return false;
        }

        public static bool HasImageFill(FigmaNode node)
        {
            if (node.fills == null)
            {
                return false;
            }

            for (int i = 0; i < node.fills.Count; i++)
            {
                FigmaPaint paint = node.fills[i];
                if (paint.visible && paint.type == "IMAGE")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
