using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tl.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
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
            var id = timelinesObject.TargetNames.IndexOf(name);
            if (id < 0)
            {
                if (timelinesObject.TargetNames.Count >= TlUiTimelines.MostTargetNames)
                {
                    Debug.LogWarning("Tl UI: " + timelinesObject.name + " already has " + TlUiTimelines.MostTargetNames + " target names, the most a world can drive.", timelinesObject);
                    return false;
                }
                Undo.RecordObject(timelinesObject, "Pick Tl UI Target");
                timelinesObject.TargetNames.Add(name);
                id = timelinesObject.TargetNames.Count - 1;
                EditorUtility.SetDirty(timelinesObject);
                if (timelinesObject.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(timelinesObject.gameObject.scene);
            }
            foreach (var clip in clips)
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
