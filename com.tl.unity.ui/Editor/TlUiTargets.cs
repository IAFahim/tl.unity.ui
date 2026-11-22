using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        const string TargetField = "Id";

        static FieldInfo TargetFieldOf(TlClipData data)
        {
            if (data == null || !data.MenuName.StartsWith("UI/", System.StringComparison.Ordinal))
                return null;
            var field = data.GetType().GetField(TargetField, BindingFlags.Public | BindingFlags.Instance);
            return field != null && field.FieldType == typeof(byte) ? field : null;
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

        static int IdOf(TlClip clip) => (byte)TargetFieldOf(clip.Data).GetValue(clip.Data);

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
                foreach (var binding in panel.Channels)
                    used |= BitOf(binding.TargetId);
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
            var field = TargetFieldOf(tl.Data);
            if (field == null)
                return null;
            var id = (byte)field.GetValue(tl.Data);
            return id < timelinesObject.TargetNames.Count ? timelinesObject.TargetNames[id] : null;
        }

        public static bool Assign(IEnumerable<TimelineClip> clips, TlUiTimelines timelinesObject, string name)
        {
            if (timelinesObject == null || string.IsNullOrEmpty(name))
                return false;
            var clipsToName = clips.ToList();
            var names = timelinesObject.TargetNames;
            var usedByNumber = IdsUsedByNumber(timelinesObject, clipsToName);
            var id = names.IndexOf(name);
            if (id >= 0 && (usedByNumber & BitOf(id)) != 0)
                Debug.LogWarning("Tl UI: the name " + name + " is at position " + id + ", and Id " + id + " is also used by number (a Targets, Channels, Lists or Commands row, a TlTarget, or a clip with no name), so clips on it drive both. Move one of them to another Id.", timelinesObject);
            if (id < 0)
            {
                id = FreePosition(names, usedByNumber);
                if (id < 0)
                {
                    Debug.LogWarning("Tl UI: no free target Id is left in " + timelinesObject.name + "; names and numbered targets together can use " + TlUiTimelines.MostTargetNames + ".", timelinesObject);
                    return false;
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
            foreach (var clip in clipsToName)
            {
                if (!(clip?.asset is TlClip tl))
                    continue;
                var field = TargetFieldOf(tl.Data);
                if (field == null)
                    continue;
                Undo.RecordObject(tl, "Pick Tl UI Target");
                field.SetValue(tl.Data, (byte)id);
                EditorUtility.SetDirty(tl);
                var track = clip.GetParentTrack();
                if (track != null)
                {
                    Undo.RecordObject(track, "Pick Tl UI Target");
                    clip.displayName = TlMenu.ShortName(tl.Data.MenuName) + " " + name;
                    EditorUtility.SetDirty(track);
                }
            }
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
            return true;
        }
    }
}
