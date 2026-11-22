using System;
using System.Collections.Generic;
using System.Linq;
using Tl.Ui;
using Tl.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;

namespace TlUi.Editor
{
    public sealed class TlUiTypeANamePopup : PopupWindowContent
    {
        readonly string _question;
        readonly Action<string> _addTheTypedName;
        string _typed = "";

        public TlUiTypeANamePopup(string question, Action<string> addTheTypedName)
        {
            _question = question;
            _addTheTypedName = addTheTypedName;
        }

        public override Vector2 GetWindowSize() => new Vector2(260f, EditorGUIUtility.singleLineHeight * 3f + 12f);

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField(_question);
            GUI.SetNextControlName("TlUiTypedName");
            _typed = EditorGUILayout.TextField(_typed);
            EditorGUI.FocusTextInControl("TlUiTypedName");
            var pressedEnter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_typed)))
                if ((GUILayout.Button("Add") || pressedEnter) && !string.IsNullOrWhiteSpace(_typed))
                {
                    _addTheTypedName(_typed.Trim());
                    editorWindow.Close();
                }
        }
    }

    public static class TlUiPanelLists
    {
        public static List<TlUiPanel> PanelsInOpenScenes() => UnityEngine.Object.FindObjectsByType<TlUiPanel>(FindObjectsInactive.Exclude).ToList();

        public static string NameAtSlot(IEnumerable<IList<string>> listsOfEveryPanel, int slot)
        {
            var names = listsOfEveryPanel.Where(list => slot < list.Count && !string.IsNullOrEmpty(list[slot])).Select(list => list[slot]).Distinct().ToList();
            return names.Count == 0 ? null : string.Join(" / ", names);
        }

        public static int AddToEveryPanel(List<TlUiPanel> panels, Func<TlUiPanel, List<string>> listOf, string name, int mostSlots)
        {
            for (var slot = 0; slot < mostSlots; slot++)
                foreach (var panel in panels)
                    if (slot < listOf(panel).Count && listOf(panel)[slot] == name)
                        return slot;
            var free = -1;
            for (var slot = 0; slot < mostSlots && free < 0; slot++)
                if (panels.All(panel => slot >= listOf(panel).Count || string.IsNullOrEmpty(listOf(panel)[slot])))
                    free = slot;
            if (free < 0)
                return -1;
            Undo.RecordObjects(panels.Cast<UnityEngine.Object>().ToArray(), "Add Tl UI Name");
            foreach (var panel in panels)
            {
                var list = listOf(panel);
                while (list.Count <= free)
                    list.Add("");
                list[free] = name;
                EditorUtility.SetDirty(panel);
                EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            }
            return free;
        }

        public static Action<int> SlotWriterFor(SerializedProperty slot)
        {
            var clipAsset = slot.serializedObject.targetObject;
            var path = slot.propertyPath;
            return value =>
            {
                if (clipAsset == null)
                    return;
                var fresh = new SerializedObject(clipAsset);
                var property = fresh.FindProperty(path);
                if (property == null)
                    return;
                property.intValue = value;
                fresh.ApplyModifiedProperties();
            };
        }
    }

    [CustomPropertyDrawer(typeof(UiTargetRef))]
    public sealed class UiTargetRefDrawer : PropertyDrawer
    {
        const string HowToGetNames = "To pick the target by name, open the timeline from its object: in the Hierarchy, tick the box next to Screen Timelines (the SubScene), expand it, and click the Screen Timelines object inside. The Timeline window then shows names here.";

        static bool NamesCanBeOffered(SerializedProperty property) =>
            TlUiTargets.TimelinesObjectOf(TimelineEditor.inspectedDirector) != null
            && TlUiTargets.TimelineClipOf(property.serializedObject.targetObject as TlClip) != null
            && !property.serializedObject.isEditingMultipleObjects;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (NamesCanBeOffered(property))
                return EditorGUIUtility.singleLineHeight;
            return EditorGUIUtility.singleLineHeight + 4f + EditorStyles.helpBox.CalcHeight(new GUIContent(HowToGetNames), EditorGUIUtility.currentViewWidth - 40f);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var id = property.FindPropertyRelative(nameof(UiTargetRef.Id));
            var pickByName = new GUIContent(label.text, "The element this clip changes: an element's Name in UI Builder.");
            if (!NamesCanBeOffered(property))
            {
                var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(line, id, new GUIContent(label.text + " Id", HowToGetNames));
                EditorGUI.HelpBox(new Rect(position.x, line.yMax + 2f, position.width, position.height - EditorGUIUtility.singleLineHeight - 4f), HowToGetNames, MessageType.Info);
                return;
            }
            var timelinesObject = TlUiTargets.TimelinesObjectOf(TimelineEditor.inspectedDirector);
            var clip = TlUiTargets.TimelineClipOf(property.serializedObject.targetObject as TlClip);
            var names = timelinesObject.TargetNames;
            var current = id.intValue < names.Count ? names[id.intValue] : null;
            var shown = string.IsNullOrEmpty(current) ? "Id " + id.intValue + " (no name: pick one)" : current;
            var button = EditorGUI.PrefixLabel(position, pickByName);
            if (!EditorGUI.DropdownButton(button, new GUIContent(shown), FocusType.Keyboard))
                return;
            var menu = new GenericMenu();
            var offered = TlUiTargets.NamesToOffer(timelinesObject);
            if (offered.Count == 0)
                menu.AddDisabledItem(new GUIContent("Give elements a Name in UI Builder first"));
            foreach (var name in offered)
            {
                var chosen = name;
                menu.AddItem(new GUIContent(chosen), chosen == current, () => TlUiTargets.Assign(new[] { clip }, timelinesObject, chosen));
            }
            menu.DropDown(button);
        }
    }

    [CustomPropertyDrawer(typeof(UiClassRef))]
    public sealed class UiClassRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var slot = property.FindPropertyRelative(nameof(UiClassRef.Slot));
            var writeSlot = TlUiPanelLists.SlotWriterFor(slot);
            var panels = TlUiPanelLists.PanelsInOpenScenes();
            var current = TlUiPanelLists.NameAtSlot(panels.Select(panel => (IList<string>)panel.ClassSlots), slot.intValue);
            var button = EditorGUI.PrefixLabel(position, new GUIContent(label.text, "The USS class this clip adds or removes. Its style lives in your USS file."));
            if (!EditorGUI.DropdownButton(button, new GUIContent(current ?? "Slot " + slot.intValue + " (no class name: pick or add one)"), FocusType.Keyboard))
                return;
            var menu = new GenericMenu();
            for (var index = 0; index < UiIds.SlotCount; index++)
            {
                var name = TlUiPanelLists.NameAtSlot(panels.Select(panel => (IList<string>)panel.ClassSlots), index);
                if (name == null)
                    continue;
                var chosen = index;
                menu.AddItem(new GUIContent(name), chosen == slot.intValue, () => writeSlot(chosen));
            }
            if (panels.Count == 0)
                menu.AddDisabledItem(new GUIContent("Open the scene with the Tl UI Panel to name classes"));
            else
                menu.AddItem(new GUIContent("New class name…"), false, () => PopupWindow.Show(button, new TlUiTypeANamePopup("USS class name, without the dot", typed =>
                {
                    var added = TlUiPanelLists.AddToEveryPanel(panels, panel => panel.ClassSlots, typed, UiIds.SlotCount);
                    if (added >= 0)
                        writeSlot(added);
                    else
                        Debug.LogWarning("Tl UI: all " + UiIds.SlotCount + " class slots are used. Free one in the Tl UI Panel's Class Slots.");
                })));
            menu.DropDown(button);
        }
    }

    [CustomPropertyDrawer(typeof(UiTextRef))]
    public sealed class UiTextRefDrawer : PropertyDrawer
    {
        const int MostTexts = 256;

        static string Shortened(string text) => text.Length > 40 ? text.Substring(0, 40) + "…" : text;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var slot = property.FindPropertyRelative(nameof(UiTextRef.Slot));
            var writeSlot = TlUiPanelLists.SlotWriterFor(slot);
            var panels = TlUiPanelLists.PanelsInOpenScenes();
            var current = TlUiPanelLists.NameAtSlot(panels.Select(panel => (IList<string>)panel.TextSlots), slot.intValue);
            var button = EditorGUI.PrefixLabel(position, new GUIContent(label.text, "The text the target shows while this clip is on."));
            if (!EditorGUI.DropdownButton(button, new GUIContent(current != null ? Shortened(current) : "Slot " + slot.intValue + " (no text: pick or add one)"), FocusType.Keyboard))
                return;
            var menu = new GenericMenu();
            var longest = panels.Count == 0 ? 0 : panels.Max(panel => panel.TextSlots.Count);
            for (var index = 0; index < longest; index++)
            {
                var text = TlUiPanelLists.NameAtSlot(panels.Select(panel => (IList<string>)panel.TextSlots), index);
                if (text == null)
                    continue;
                var chosen = index;
                menu.AddItem(new GUIContent(Shortened(text).Replace('/', '∕')), chosen == slot.intValue, () => writeSlot(chosen));
            }
            if (panels.Count == 0)
                menu.AddDisabledItem(new GUIContent("Open the scene with the Tl UI Panel to add texts"));
            else
                menu.AddItem(new GUIContent("New text…"), false, () => PopupWindow.Show(button, new TlUiTypeANamePopup("Text to show", typed =>
                {
                    var added = TlUiPanelLists.AddToEveryPanel(panels, panel => panel.TextSlots, typed, MostTexts);
                    if (added >= 0)
                        writeSlot(added);
                })));
            menu.DropDown(button);
        }
    }

    [CustomPropertyDrawer(typeof(UiTemplateRef))]
    public sealed class UiTemplateRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var slot = property.FindPropertyRelative(nameof(UiTemplateRef.Slot));
            var writeSlot = TlUiPanelLists.SlotWriterFor(slot);
            var panels = TlUiPanelLists.PanelsInOpenScenes();
            string NameOfTemplateAt(int index) => TlUiPanelLists.NameAtSlot(panels.Select(panel => (IList<string>)panel.TemplateSlots.Select(template => template != null ? template.name : null).ToList()), index);
            var current = NameOfTemplateAt(slot.intValue);
            var button = EditorGUI.PrefixLabel(position, new GUIContent(label.text, "The UXML template put inside the target while this clip is on."));
            if (!EditorGUI.DropdownButton(button, new GUIContent(current ?? "Slot " + slot.intValue + " (no template: add one to the Tl UI Panel)"), FocusType.Keyboard))
                return;
            var menu = new GenericMenu();
            for (var index = 0; index < UiIds.SlotCount; index++)
            {
                var name = NameOfTemplateAt(index);
                if (name == null)
                    continue;
                var chosen = index;
                menu.AddItem(new GUIContent(name), chosen == slot.intValue, () => writeSlot(chosen));
            }
            if (panels.Count > 0)
                menu.AddItem(new GUIContent("Add templates on the Tl UI Panel…"), false, () => Selection.activeObject = panels[0]);
            menu.DropDown(button);
        }
    }

    [CustomPropertyDrawer(typeof(UiProperty))]
    public sealed class UiPropertyDrawer : PropertyDrawer
    {
        static string WhatTheNumbersMean(UiProperty property)
        {
            switch (property)
            {
                case UiProperty.Opacity: return "0 is invisible, 1 is as designed.";
                case UiProperty.MoveX: return "Pixels from where the layout puts it. + moves right, - moves left.";
                case UiProperty.MoveY: return "Pixels from where the layout puts it. + moves down, - moves up.";
                case UiProperty.Scale: return "1 is the designed size, 0 is nothing, 2 is double.";
                case UiProperty.Rotate: return "Degrees, clockwise.";
                case UiProperty.WidthPercent: return "Percent of the parent's width.";
                case UiProperty.HeightPercent: return "Percent of the parent's height.";
                case UiProperty.Reveal: return "How much of the text shows, 0 to 1, like typing.";
                case UiProperty.Value: return "The value of a slider, progress bar or Tl Radial Fill.";
                default: return "";
            }
        }

        static SerializedProperty Sibling(SerializedProperty property, string name)
        {
            var path = property.propertyPath;
            var parent = path.Contains(".") ? path.Substring(0, path.LastIndexOf('.') + 1) : "";
            return property.serializedObject.FindProperty(parent + name);
        }

        string HelpFor(SerializedProperty property)
        {
            var from = Sibling(property, nameof(AnimateClip.From));
            var to = Sibling(property, nameof(AnimateClip.To));
            var help = WhatTheNumbersMean((UiProperty)property.enumValueIndex) + " The clip goes From → To, shaped by Ease. Hold keeps To after the clip ends.";
            if (from != null && to != null && from.floatValue == to.floatValue)
                help = "From and To are both " + from.floatValue + ", so nothing moves. Set To to where it should end.\n" + help;
            return help;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var width = EditorGUIUtility.currentViewWidth - 40f;
            return EditorGUIUtility.singleLineHeight + 4f + EditorStyles.helpBox.CalcHeight(new GUIContent(HelpFor(property)), width);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var before = property.enumValueIndex;
            EditorGUI.PropertyField(line, property, new GUIContent(label.text, "What the clip moves on its target."));
            var help = new Rect(position.x, line.yMax + 2f, position.width, position.height - EditorGUIUtility.singleLineHeight - 4f);
            EditorGUI.HelpBox(help, HelpFor(property), MessageType.None);
            if (before == property.enumValueIndex)
                return;
            var clip = TlUiTargets.TimelineClipOf(property.serializedObject.targetObject as TlClip);
            var timelinesObject = TlUiTargets.TimelinesObjectOf(TimelineEditor.inspectedDirector);
            if (clip == null || timelinesObject == null || !(clip.asset is TlClip tl))
                return;
            var nameBefore = TlUiTargets.ClipNameFor(tl.Data, TlUiTargets.TargetNameOf(clip, timelinesObject) ?? "");
            property.serializedObject.ApplyModifiedProperties();
            TlUiTargets.RenameIfNamedByTlUi(clip, nameBefore, timelinesObject);
        }
    }
}
