#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace FigmaToUGUI.Editor
{
    public sealed class FigmaToUGUIOdinWindow : OdinEditorWindow
    {
        private FigmaToUGUIWindowController controller;

        [MenuItem("Tools/Figma/Import to UGUI (Odin)")]
        public static void Open()
        {
            GetWindow<FigmaToUGUIOdinWindow>("Figma to UGUI");
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            controller = new FigmaToUGUIWindowController(this);
            controller.OnEnable();
        }

        protected override void OnDisable()
        {
            if (controller != null)
            {
                controller.OnDisable();
            }

            base.OnDisable();
        }

        protected override void OnImGUI()
        {
            if (controller == null)
            {
                controller = new FigmaToUGUIWindowController(this);
                controller.OnEnable();
            }

            controller.OnGUI();
        }
    }
}
#endif
