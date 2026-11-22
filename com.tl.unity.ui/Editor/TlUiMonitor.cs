using UnityEditor;
using UnityEngine.UIElements;

namespace TlUi.Editor
{
    public sealed class TlUiMonitor : EditorWindow
    {
        Label _dump;

        [MenuItem("Window/Tl UI/Monitor")]
        public static void Open() => GetWindow<TlUiMonitor>("Tl UI Monitor");

        void CreateGUI()
        {
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _dump = new Label { enableRichText = false };
            _dump.selection.isSelectable = true;
            _dump.style.whiteSpace = WhiteSpace.Pre;
            scroll.Add(_dump);
            rootVisualElement.Add(scroll);
            rootVisualElement.schedule.Execute(Refresh).Every(250);
            Refresh();
        }

        void Refresh() => _dump.text = TlUiPanel.DumpEveryPanel();
    }
}
