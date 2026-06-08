using System;

namespace FigmaToUGUI.Editor
{
    internal sealed class FigmaImportProgress
    {
        public string title;
        public string detail;
        public float value;

        public FigmaImportProgress(string title, string detail, float value)
        {
            this.title = title;
            this.detail = detail;
            this.value = Math.Max(0f, Math.Min(1f, value));
        }
    }
}
