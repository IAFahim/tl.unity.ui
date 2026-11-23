using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tl.Ui;
using Tl.Unity;
using Tl.Unity.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace TlUi.Editor
{
    public static class TlUiTargets
    {
        const string TargetField = "Target";
        const string PropertyField = "Property";

        static FieldInfo TargetFieldOf(TlClipData data)
        {
            if (data == null)
                return null;
            var field = data.GetType().GetField(TargetField, BindingFlags.Public | BindingFlags.Instance);
            return field != null && field.FieldType == typeof(UiTargetRef) ? field : null;
        }

        static int IdIn(TlClipData data) => ((UiTargetRef)TargetFieldOf(data).GetValue(data)).Id;

        public static TimelineClip TimelineClipOf(TlClip clipAsset)
        {
            var timeline = TimelineEditor.inspectedAsset;
            if (clipAsset == null || timeline == null)
                return null;
            foreach (var track in timeline.GetOutputTracks())
                foreach (var clip in track.GetClips())
                    if (clip.asset == clipAsset)
                        return clip;
            return null;
        }

        public static string ClipNameFor(TlClipData data, string targetName)
        {
            var shortName = TlMenu.ShortName(data.MenuName);
            if (data.ClipType == typeof(Tl.Ui.TextClip))
                return "Text on " + targetName;
            if (data.ClipType != typeof(Tl.Ui.AnimateClip))
                return shortName + " " + targetName;
            var property = data.GetType().GetField(PropertyField, BindingFlags.Public | BindingFlags.Instance);
            switch (property != null ? (UiProperty)property.GetValue(data) : UiProperty.Opacity)
            {
                case UiProperty.Opacity: return "Fade " + targetName;
                case UiProperty.MoveX: return "Move " + targetName + " X";
                case UiProperty.MoveY: return "Move " + targetName + " Y";
                case UiProperty.Scale: return "Scale " + targetName;
                case UiProperty.Rotate: return "Rotate " + targetName;
                case UiProperty.WidthPercent: return "Width of " + targetName;
                case UiProperty.HeightPercent: return "Height of " + targetName;
                case UiProperty.Reveal: return "Type out " + targetName;
                case UiProperty.Value: return "Fill " + targetName;
                default: return shortName + " " + targetName;
            }
        }

        public static void RenameIfNamedByTlUi(TimelineClip clip, string nameBeforeTheChange, TlUiTimelines timelinesObject)
        {
            if (!(clip?.asset is TlClip tl) || TargetFieldOf(tl.Data) == null || timelinesObject == null)
                return;
            var targetName = TargetNameOf(clip, timelinesObject);
            if (string.IsNullOrEmpty(targetName))
                return;
            if (clip.displayName != nameBeforeTheChange && clip.displayName != TlMenu.ShortName(tl.Data.MenuName))
                return;
            var track = clip.GetParentTrack();
            if (track == null)
                return;
            Undo.RecordObject(track, "Rename Tl UI Clip");
            clip.displayName = ClipNameFor(tl.Data, targetName);
            EditorUtility.SetDirty(track);
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }

        public static bool TakesATarget(TimelineClip clip) => clip?.asset is TlClip tl && TargetFieldOf(tl.Data) != null;

        public static TlUiTimelines TimelinesObjectOf(UnityEngine.Playables.PlayableDirector director) => director != null ? director.GetComponent<TlUiTimelines>() : null;

        public static List<string> NamesToOffer(TlUiTimelines timelinesObject)
        {
            var names = new SortedSet<string>(System.StringComparer.Ordinal);
            if (timelinesObject != null)
                foreach (var name in timelinesObject.TargetNames)
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
            foreach (var panel in Object.FindObjectsByType<TlUiPanel>(FindObjectsInactive.Exclude))
            {
                var screen = panel.Panel != null ? panel.Panel.visualTreeAsset : null;
                if (screen != null)
                    AddElementNames(screen.Instantiate(), names);
            }
            return names.ToList();
        }

        static void AddElementNames(VisualElement parent, SortedSet<string> names)
        {
            for (var child = 0; child < parent.hierarchy.childCount; child++)
            {
                var element = parent.hierarchy[child];
                if (!string.IsNullOrEmpty(element.name) && !(element is TemplateContainer))
                    names.Add(element.name);
                AddElementNames(element, names);
            }
        }

        static ulong BitOf(int id) => id >= 0 && id < TlUiTimelines.MostTargetNames ? 1UL << id : 0UL;

        static int IdOf(TlClip clip) => IdIn(clip.Data);

        static ulong TlTargetIdsUnder(VisualElement parent)
        {
            var used = 0UL;
            for (var child = 0; child < parent.hierarchy.childCount; child++)
            {
                var element = parent.hierarchy[child];
                if (element.HasComponent<TlTarget>())
                    used |= BitOf(element.GetComponent<TlTarget>().Id);
                used |= TlTargetIdsUnder(element);
            }
            return used;
        }

        public static ulong IdsUsedByNumber(TlUiTimelines timelinesObject, IEnumerable<TimelineClip> beingNamed)
        {
            var used = 0UL;
            foreach (var panel in Object.FindObjectsByType<TlUiPanel>(FindObjectsInactive.Include))
            {
                foreach (var target in panel.Targets)
                    used |= BitOf(target.Id);
                foreach (var list in panel.Lists)
                    used |= BitOf(list.TargetId);
                foreach (var command in panel.Commands)
                    if (command.On != UiCommandEvent.HotkeyOnly)
                        used |= BitOf(command.TargetId);
                var screen = panel.Panel != null ? panel.Panel.visualTreeAsset : null;
                if (screen != null)
                    used |= TlTargetIdsUnder(screen.Instantiate());
            }
            var director = timelinesObject != null ? timelinesObject.GetComponent<PlayableDirector>() : null;
            if (director == null)
                return used;
            var names = timelinesObject.TargetNames;
            var skipped = new HashSet<TlClip>(beingNamed.Select(clip => clip?.asset as TlClip).Where(clip => clip != null));
            foreach (var timeline in TlInspectorModel.Plays(director, director.GetComponent<TlAlsoPlays>()))
                foreach (var track in timeline.GetOutputTracks())
                    foreach (var clip in track.GetClips())
                    {
                        if (!TakesATarget(clip) || skipped.Contains((TlClip)clip.asset))
                            continue;
                        var id = IdOf((TlClip)clip.asset);
                        if (id >= names.Count || string.IsNullOrEmpty(names[id]))
                            used |= BitOf(id);
                    }
            return used;
        }

        static int FreePosition(List<string> names, ulong usedByNumber)
        {
            for (var id = 0; id < TlUiTimelines.MostTargetNames; id++)
                if ((usedByNumber & BitOf(id)) == 0 && (id >= names.Count || string.IsNullOrEmpty(names[id])))
                    return id;
            return -1;
        }

        public static string TargetNameOf(TimelineClip clip, TlUiTimelines timelinesObject)
        {
            if (!(clip?.asset is TlClip tl) || timelinesObject == null)
                return null;
            if (TargetFieldOf(tl.Data) == null)
                return null;
            var id = IdIn(tl.Data);
            return id < timelinesObject.TargetNames.Count ? timelinesObject.TargetNames[id] : null;
        }

        public static int IdForName(TlUiTimelines timelinesObject, string name) => timelinesObject == null || string.IsNullOrEmpty(name) ? -1 : NameAtAnId(timelinesObject, name, IdsUsedByNumber(timelinesObject, Enumerable.Empty<TimelineClip>()));

        static int NameAtAnId(TlUiTimelines timelinesObject, string name, ulong usedByNumber)
        {
            var names = timelinesObject.TargetNames;
            var id = names.IndexOf(name);
            if (id >= 0 && (usedByNumber & BitOf(id)) != 0)
                Debug.LogWarning("Tl UI: the name " + name + " is at position " + id + ", and Id " + id + " is also used by number (a Targets, Lists or Commands row, a TlTarget, or a clip with no name), so clips on it drive both. Move one of them to another Id.", timelinesObject);
            if (id < 0)
            {
                id = FreePosition(names, usedByNumber);
                if (id < 0)
                {
                    Debug.LogWarning("Tl UI: no free target Id is left in " + timelinesObject.name + "; names and numbered targets together can use " + TlUiTimelines.MostTargetNames + ".", timelinesObject);
                    return -1;
                }
                Undo.RecordObject(timelinesObject, "Pick Tl UI Target");
                while (names.Count < id)
                    names.Add("");
                if (id < names.Count)
                    names[id] = name;
                else
                    names.Add(name);
                EditorUtility.SetDirty(timelinesObject);
                if (timelinesObject.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(timelinesObject.gameObject.scene);
            }
            return id;
        }

        public static TlUiTimelines TimelinesObjectInOpenScenes() => Object.FindObjectsByType<TlUiTimelines>(FindObjectsInactive.Include).FirstOrDefault();

        public static TlUiPanel FindAnyPanel() => Object.FindAnyObjectByType<TlUiPanel>(FindObjectsInactive.Include);

        public static string LabelOfId(TlUiPanel panel, TlUiTimelines timelinesObject, int id)
        {
            if (timelinesObject != null && id >= 0 && id < timelinesObject.TargetNames.Count && !string.IsNullOrEmpty(timelinesObject.TargetNames[id]))
                return timelinesObject.TargetNames[id];
            if (panel != null)
                foreach (var target in panel.Targets)
                    if (target.Id == id && !string.IsNullOrEmpty(target.Selector))
                        return target.Selector;
            return null;
        }

        public static bool Assign(IEnumerable<TimelineClip> clips, TlUiTimelines timelinesObject, string name)
        {
            if (timelinesObject == null || string.IsNullOrEmpty(name))
                return false;
            var clipsToName = clips.ToList();
            var id = NameAtAnId(timelinesObject, name, IdsUsedByNumber(timelinesObject, clipsToName));
            if (id < 0)
                return false;
            foreach (var clip in clipsToName)
            {
                if (!(clip?.asset is TlClip tl))
                    continue;
                var field = TargetFieldOf(tl.Data);
                if (field == null)
                    continue;
                Undo.RecordObject(tl, "Pick Tl UI Target");
                field.SetValue(tl.Data, new UiTargetRef { Id = (byte)id });
                EditorUtility.SetDirty(tl);
                var track = clip.GetParentTrack();
                if (track != null)
                {
                    Undo.RecordObject(track, "Pick Tl UI Target");
                    clip.displayName = ClipNameFor(tl.Data, name);
                    EditorUtility.SetDirty(track);
                }
            }
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
            return true;
        }
    }
}
