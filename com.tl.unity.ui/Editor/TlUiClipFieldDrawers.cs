using System;
using System.Collections.Generic;
using System.Linq;
using Tl.Ui;
using Tl.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace TlUi.Editor
{
    public sealed class TlUiTypeANamePopup : PopupWindowContent
    {
        readonly string _question;
        readonly Action<string> _addTheTypedName;
        string _typed;

        public TlUiTypeANamePopup(string question, Action<string> addTheTypedName, string suggested = "")
        {
            _question = question;
            _addTheTypedName = addTheTypedName;
            _typed = suggested ?? "";
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

    public static class TlUiDrawerHelp
    {
        public static float Line => EditorGUIUtility.singleLineHeight;

        static float HelpHeight(string text) => EditorStyles.helpBox.CalcHeight(new GUIContent(text), EditorGUIUtility.currentViewWidth - 40f);

        public static float HeightOfAFieldWithHelp(string text, bool withAButton) => Line + 2f + HelpHeight(text) + (withAButton ? Line + 4f : 0f) + 2f;

        public static Rect FieldLine(Rect position) => new Rect(position.x, position.y, position.width, Line);

        public static void DrawHelpUnderTheField(Rect position, string text, string buttonLabel, Action onClick)
        {
            var help = new Rect(position.x, position.y + Line + 2f, position.width, HelpHeight(text));
            EditorGUI.HelpBox(help, text, MessageType.Info);
            if (buttonLabel == null)
                return;
            var button = new Rect(position.x + EditorGUIUtility.labelWidth, help.yMax + 2f, position.width - EditorGUIUtility.labelWidth, Line);
            if (GUI.Button(button, buttonLabel))
                onClick();
        }

        public static SerializedProperty Sibling(SerializedProperty property, string name)
        {
            var path = property.propertyPath;
            var parent = path.Contains(".") ? path.Substring(0, path.LastIndexOf('.') + 1) : "";
            return property.serializedObject.FindProperty(parent + name);
        }

        public static string ParentPath(SerializedProperty property)
        {
            var path = property.propertyPath;
            return path.Contains(".") ? path.Substring(0, path.LastIndexOf('.') + 1) : "";
        }
    }

    [CustomPropertyDrawer(typeof(UiTargetRef))]
    public sealed class UiTargetRefDrawer : PropertyDrawer
    {
        const string ScreenTimelinesIsClosed = "Screen Timelines is closed, so element names can't be shown here.";
        const string NotOnAScreenYet = "No Screen Timelines plays this timeline yet, so element names can't be shown here.";
        const string NoTimelineShown = "Open this clip's timeline in the Timeline window to pick the element by name.";

        enum Names { Shown, ScreenTimelinesIsClosed, NotOnAScreenYet, NoTimelineShown }

        static Names WhatCanBeShown(SerializedProperty property)
        {
            if (property.serializedObject.isEditingMultipleObjects || TlUiTargets.TimelineClipOf(property.serializedObject.targetObject as TlClip) == null)
                return Names.NoTimelineShown;
            var timeline = TimelineEditor.inspectedAsset;
            if (TlUiScreens.TimelinesObjectFor(timeline) != null)
                return Names.Shown;
            if (TlUiScreens.ClosedSubSceneThatPlays(timeline) != null)
                return Names.ScreenTimelinesIsClosed;
            return TlUiScreens.TimelinesObjectsInOpenScenes().Count > 0 || TlUiScreens.ClosedScreenTimelines().Count > 0 ? Names.NotOnAScreenYet : Names.NoTimelineShown;
        }

        static string HelpFor(Names names) => names == Names.ScreenTimelinesIsClosed ? ScreenTimelinesIsClosed : names == Names.NotOnAScreenYet ? NotOnAScreenYet : NoTimelineShown;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var names = WhatCanBeShown(property);
            return names == Names.Shown ? EditorGUIUtility.singleLineHeight : TlUiDrawerHelp.HeightOfAFieldWithHelp(HelpFor(names), names != Names.NoTimelineShown);
        }

        static void AddTheShownTimelineToTheScreen()
        {
            var timeline = TimelineEditor.inspectedAsset;
            var timelinesObject = TlUiScreens.FindOrOpenTimelinesObject();
            if (timeline != null && timelinesObject != null)
                TlUiScreens.AddExistingTimeline(timelinesObject, timeline);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var id = property.FindPropertyRelative(nameof(UiTargetRef.Id));
            var names = WhatCanBeShown(property);
            if (names != Names.Shown)
            {
                EditorGUI.PropertyField(TlUiDrawerHelp.FieldLine(position), id, new GUIContent(label.text + " Id", HelpFor(names)));
                if (names == Names.ScreenTimelinesIsClosed)
                    TlUiDrawerHelp.DrawHelpUnderTheField(position, ScreenTimelinesIsClosed, "Open Screen Timelines", () => TlUiScreens.OpenForEditing(TlUiScreens.ClosedSubSceneThatPlays(TimelineEditor.inspectedAsset)));
                else if (names == Names.NotOnAScreenYet)
                    TlUiDrawerHelp.DrawHelpUnderTheField(position, NotOnAScreenYet, "Add this timeline to Screen Timelines", AddTheShownTimelineToTheScreen);
                else
                    TlUiDrawerHelp.DrawHelpUnderTheField(position, NoTimelineShown, null, null);
                return;
            }
            var pickByName = new GUIContent(label.text, "The element this clip changes: an element's Name in UI Builder.");
            var timelinesObject = TlUiScreens.TimelinesObjectFor(TimelineEditor.inspectedAsset);
            var clip = TlUiTargets.TimelineClipOf(property.serializedObject.targetObject as TlClip);
            var targetNames = timelinesObject.TargetNames;
            var current = id.intValue < targetNames.Count ? targetNames[id.intValue] : null;
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

    [CustomPropertyDrawer(typeof(TlUiTargetIdAttribute))]
    public sealed class TlUiTargetIdDrawer : PropertyDrawer
    {
        const string ScreenTimelinesIsClosed = "Screen Timelines is closed, so element names can't be shown here.";
        const string NoScreenTimelines = "There is no Screen Timelines in the open scenes. Right-click the Hierarchy and pick Tl UI > Screen to make one.";

        static string HelpWhenNoNames() => TlUiScreens.ClosedScreenTimelines().Count > 0 ? ScreenTimelinesIsClosed : NoScreenTimelines;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (TlUiTargets.TimelinesObjectInOpenScenes() != null)
                return EditorGUIUtility.singleLineHeight;
            var help = HelpWhenNoNames();
            return TlUiDrawerHelp.HeightOfAFieldWithHelp(help, help == ScreenTimelinesIsClosed);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var panel = property.serializedObject.targetObject as TlUiPanel;
            var timelinesObject = TlUiTargets.TimelinesObjectInOpenScenes();
            var line = TlUiDrawerHelp.FieldLine(position);
            if (timelinesObject == null)
            {
                var help = HelpWhenNoNames();
                EditorGUI.IntSlider(line, property, 0, UiIds.HighestId, new GUIContent("Target Id", help));
                if (help == ScreenTimelinesIsClosed)
                    TlUiDrawerHelp.DrawHelpUnderTheField(position, help, "Open Screen Timelines", () => TlUiScreens.OpenEveryClosedScreenTimelines());
                else
                    TlUiDrawerHelp.DrawHelpUnderTheField(position, help, null, null);
                return;
            }
            var current = TlUiTargets.LabelOfId(panel, timelinesObject, property.intValue);
            var button = EditorGUI.PrefixLabel(line, new GUIContent("Target", "The element, by its Name in UI Builder."));
            if (!EditorGUI.DropdownButton(button, new GUIContent(current ?? "Id " + property.intValue + " (no name: pick one)"), FocusType.Keyboard))
                return;
            var writeId = TlUiPanelLists.SlotWriterFor(property);
            var menu = new GenericMenu();
            var offered = TlUiTargets.NamesToOffer(timelinesObject);
            if (offered.Count == 0)
                menu.AddDisabledItem(new GUIContent("Give elements a Name in UI Builder first"));
            foreach (var name in offered)
            {
                var chosen = name;
                menu.AddItem(new GUIContent(chosen), chosen == current, () =>
                {
                    var id = TlUiTargets.IdForName(timelinesObject, chosen);
                    if (id >= 0)
                        writeId(id);
                });
            }
            menu.DropDown(button);
        }
    }

    [CustomPropertyDrawer(typeof(TlUiTimelineSlotAttribute))]
    public sealed class TlUiTimelineSlotDrawer : PropertyDrawer
    {
        const string ScreenTimelinesIsClosed = "Screen Timelines is closed, so timeline names can't be shown here.";

        public static string LabelOfSlot(IReadOnlyList<TimelineAsset> slots, int slot) =>
            slot >= 0 && slot < slots.Count && slots[slot] != null ? slots[slot].name + (slot == 0 ? " (the main timeline)" : "") : null;

        public static string SuggestedNameFor(SerializedProperty slot, TlUiTimelines timelinesObject)
        {
            var targetId = TlUiDrawerHelp.Sibling(slot, nameof(UiCommand.TargetId));
            var on = TlUiDrawerHelp.Sibling(slot, nameof(UiCommand.On));
            var code = TlUiDrawerHelp.Sibling(slot, nameof(UiSignalReaction.Code));
            if (targetId != null && on != null)
            {
                var element = TlUiTargets.LabelOfId(slot.serializedObject.targetObject as TlUiPanel, timelinesObject, targetId.intValue) ?? "Button";
                switch ((UiCommandEvent)on.enumValueIndex)
                {
                    case UiCommandEvent.Click: return element + " clicked";
                    case UiCommandEvent.PointerEnter: return element + " hovered";
                    case UiCommandEvent.PointerLeave: return element + " left";
                    case UiCommandEvent.ValueChanged: return element + " changed";
                    default: return element;
                }
            }
            return code != null ? "Signal " + code.intValue : "New Timeline";
        }

        public static bool UsesATimeline(SerializedProperty slot)
        {
            var doAction = TlUiDrawerHelp.Sibling(slot, nameof(UiCommand.Do));
            if (doAction == null)
                return true;
            var action = (UiCommandAction)doAction.enumValueIndex;
            return action != UiCommandAction.SendToGame && action != UiCommandAction.InvokeEvent;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!UsesATimeline(property) || TlUiTargets.TimelinesObjectInOpenScenes() != null || TlUiScreens.ClosedScreenTimelines().Count == 0)
                return EditorGUIUtility.singleLineHeight;
            return TlUiDrawerHelp.HeightOfAFieldWithHelp(ScreenTimelinesIsClosed, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var timelinesObject = TlUiTargets.TimelinesObjectInOpenScenes();
            var line = TlUiDrawerHelp.FieldLine(position);
            var shownLabel = new GUIContent("Timeline", "Which timeline the Play, Stop, Pause, Resume and Restart actions use. The main timeline is the one on Screen Timelines; New timeline… makes another.");
            if (!UsesATimeline(property))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(line, shownLabel, new GUIContent("not used: Do does not play a timeline"));
                return;
            }
            if (timelinesObject == null)
            {
                property.intValue = Mathf.Max(0, EditorGUI.IntField(line, new GUIContent("Timeline Slot", shownLabel.tooltip), property.intValue));
                if (TlUiScreens.ClosedScreenTimelines().Count > 0)
                    TlUiDrawerHelp.DrawHelpUnderTheField(position, ScreenTimelinesIsClosed, "Open Screen Timelines", () => TlUiScreens.OpenEveryClosedScreenTimelines());
                return;
            }
            var slots = TlUiScreens.Slots(timelinesObject);
            var current = LabelOfSlot(slots, property.intValue);
            var button = EditorGUI.PrefixLabel(line, shownLabel);
            if (!EditorGUI.DropdownButton(button, new GUIContent(current ?? "Slot " + property.intValue + " (no timeline there: pick one)"), FocusType.Keyboard))
                return;
            var writeSlot = TlUiPanelLists.SlotWriterFor(property);
            var suggested = SuggestedNameFor(property, timelinesObject);
            var shownSlot = property.intValue;
            var menu = new GenericMenu();
            for (var slot = 0; slot < slots.Count; slot++)
            {
                var slotLabel = LabelOfSlot(slots, slot);
                if (slotLabel == null)
                    continue;
                var chosen = slot;
                menu.AddItem(new GUIContent(slotLabel.Replace('/', '∕')), chosen == shownSlot, () => writeSlot(chosen));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("New timeline…"), false, () => PopupWindow.Show(button, new TlUiTypeANamePopup("Name of the new timeline", typed =>
            {
                var added = TlUiScreens.AddTimeline(timelinesObject, typed);
                if (added < 0)
                    return;
                writeSlot(added);
                TlUiScreens.ShowInTheTimelineWindow(TlUiScreens.Slots(timelinesObject)[added]);
            }, suggested)));
            if (current != null)
                menu.AddItem(new GUIContent("Open " + slots[shownSlot].name.Replace('/', '∕') + " in the Timeline window"), false, () => TlUiScreens.ShowInTheTimelineWindow(slots[shownSlot]));
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

        static string HelpFor(SerializedProperty property)
        {
            var from = TlUiDrawerHelp.Sibling(property, nameof(AnimateClip.From));
            var to = TlUiDrawerHelp.Sibling(property, nameof(AnimateClip.To));
            var ease = TlUiDrawerHelp.Sibling(property, nameof(AnimateClip.Ease));
            var help = WhatTheNumbersMean((UiProperty)property.enumValueIndex) + " The clip goes From → To, shaped by Ease. Hold keeps To after the clip ends.";
            if (ease != null && TlUiAnimatePresets.RunsBackwards(ease.animationCurveValue))
                help = "Ease goes downhill, so this clip plays To → From. To fix it, pick Preset > Make Ease go uphill.\n" + help;
            if (from != null && to != null && from.floatValue == to.floatValue)
                help = "From and To are both " + from.floatValue + ", so nothing moves. Pick a Preset, or set To to where it should end.\n" + help;
            return help;
        }

        static void StartANewClipUphill(SerializedProperty property)
        {
            var from = TlUiDrawerHelp.Sibling(property, nameof(AnimateClip.From));
            var to = TlUiDrawerHelp.Sibling(property, nameof(AnimateClip.To));
            var ease = TlUiDrawerHelp.Sibling(property, nameof(AnimateClip.Ease));
            if (from == null || to == null || ease == null || !TlUiAnimatePresets.IsUntouched(from.floatValue, to.floatValue, ease.animationCurveValue))
                return;
            ease.animationCurveValue = TlUiAnimatePresets.FastThenSlow();
            property.serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var width = EditorGUIUtility.currentViewWidth - 40f;
            return (EditorGUIUtility.singleLineHeight + 2f) * 2f + 2f + EditorStyles.helpBox.CalcHeight(new GUIContent(HelpFor(property)), width);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            StartANewClipUphill(property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var before = property.enumValueIndex;
            EditorGUI.PropertyField(line, property, new GUIContent(label.text, "What the clip moves on its target."));
            var presetLine = new Rect(position.x, line.yMax + 2f, position.width, EditorGUIUtility.singleLineHeight);
            var presetButton = EditorGUI.PrefixLabel(presetLine, new GUIContent("Preset", "Pick a ready-made animation: it sets Property, From, To, Ease, Hold and the clip's length. Change any of them after."));
            var ease = TlUiDrawerHelp.Sibling(property, nameof(AnimateClip.Ease));
            if (EditorGUI.DropdownButton(presetButton, new GUIContent("Pick a ready-made animation…"), FocusType.Keyboard))
                TlUiAnimatePresets.ShowMenu(presetButton, property.serializedObject.targetObject, TlUiDrawerHelp.ParentPath(property), ease != null && TlUiAnimatePresets.RunsBackwards(ease.animationCurveValue));
            var help = new Rect(position.x, presetLine.yMax + 2f, position.width, position.height - (EditorGUIUtility.singleLineHeight + 2f) * 2f - 2f);
            EditorGUI.HelpBox(help, HelpFor(property), MessageType.None);
            if (before == property.enumValueIndex)
                return;
            var clip = TlUiTargets.TimelineClipOf(property.serializedObject.targetObject as TlClip);
            var timelinesObject = TlUiScreens.TimelinesObjectFor(TimelineEditor.inspectedAsset);
            if (clip == null || timelinesObject == null || !(clip.asset is TlClip tl))
                return;
            var nameBefore = TlUiTargets.ClipNameFor(tl.Data, TlUiTargets.TargetNameOf(clip, timelinesObject) ?? "");
            property.serializedObject.ApplyModifiedProperties();
            TlUiTargets.RenameIfNamedByTlUi(clip, nameBefore, timelinesObject);
        }
    }
}
