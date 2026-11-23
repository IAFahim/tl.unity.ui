using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TlUi.Editor
{
    public static class TlUiEditBar
    {
        static readonly GUIContent LayoutButton = new GUIContent("Layout", "Opens this screen's UXML in UI Builder: add elements, name them, style them.");
        static readonly GUIContent TimelineButton = new GUIContent("Timeline", "Opens the main timeline in the Timeline window, ready to scrub with element names.");
        static readonly GUIContent OtherTimelinesButton = new GUIContent("Other timelines", "Opens another timeline of this screen, or makes a new one.");

        public static void Draw(TlUiPanel panel, TlUiTimelines timelinesObject)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var layout = panel != null && panel.Panel != null ? panel.Panel.visualTreeAsset : null;
                using (new EditorGUI.DisabledScope(layout == null))
                    if (GUILayout.Button(LayoutButton, EditorStyles.miniButtonLeft, GUILayout.MinWidth(50f), GUILayout.ExpandWidth(true)))
                        TlUiScreens.EditTheLayout(panel);
                if (GUILayout.Button(TimelineButton, EditorStyles.miniButtonMid, GUILayout.MinWidth(50f), GUILayout.ExpandWidth(true)))
                {
                    if (timelinesObject != null)
                        TlUiScreens.EditTheMainTimeline(timelinesObject);
                    else
                        TlUiScreens.EditTheMainTimelineOfAnyScreen();
                }
                var otherTimelines = GUILayoutUtility.GetRect(OtherTimelinesButton, EditorStyles.miniButtonRight, GUILayout.MinWidth(50f), GUILayout.ExpandWidth(true));
                if (EditorGUI.DropdownButton(otherTimelines, OtherTimelinesButton, FocusType.Passive, EditorStyles.miniButtonRight))
                    ShowTheOtherTimelines(otherTimelines, timelinesObject);
            }
            EditorGUILayout.Space(4f);
        }

        static void ShowTheOtherTimelines(Rect under, TlUiTimelines timelinesObject)
        {
            timelinesObject = timelinesObject != null ? timelinesObject : TlUiScreens.FindOrOpenTimelinesObject();
            var menu = new GenericMenu();
            if (timelinesObject == null)
            {
                menu.AddDisabledItem(new GUIContent("No Screen Timelines: right-click the Hierarchy, Tl UI > Screen"));
                menu.DropDown(under);
                return;
            }
            var slots = TlUiScreens.Slots(timelinesObject);
            for (var slot = 1; slot < slots.Count; slot++)
            {
                var timeline = slots[slot];
                if (timeline != null)
                    menu.AddItem(new GUIContent(timeline.name.Replace('/', '∕')), false, () => TlUiScreens.ShowInTheTimelineWindow(timeline));
            }
            if (slots.Skip(1).Any(timeline => timeline != null))
                menu.AddSeparator("");
            menu.AddItem(new GUIContent("New timeline…"), false, () => PopupWindow.Show(under, new TlUiTypeANamePopup("Name of the new timeline", typed =>
            {
                var added = TlUiScreens.AddTimeline(timelinesObject, typed);
                if (added >= 0)
                    TlUiScreens.ShowInTheTimelineWindow(TlUiScreens.Slots(timelinesObject)[added]);
            }, "New Timeline")));
            menu.DropDown(under);
        }
    }

    [CustomEditor(typeof(TlUiPanel))]
    public sealed class TlUiPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TlUiEditBar.Draw((TlUiPanel)target, TlUiTargets.TimelinesObjectInOpenScenes());
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(TlUiTimelines))]
    public sealed class TlUiTimelinesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            TlUiEditBar.Draw(TlUiTargets.FindAnyPanel(), (TlUiTimelines)target);
            DrawDefaultInspector();
        }
    }
}
