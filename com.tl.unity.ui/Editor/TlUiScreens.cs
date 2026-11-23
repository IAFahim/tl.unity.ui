using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tl.Unity;
using Tl.Unity.Editor;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TlUi.Editor
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class TlUiScreens
    {
        public static List<TlUiTimelines> TimelinesObjectsInOpenScenes() => Object.FindObjectsByType<TlUiTimelines>(FindObjectsInactive.Include).ToList();

        public static List<TimelineAsset> Slots(TlUiTimelines timelinesObject)
        {
            var slots = new List<TimelineAsset>();
            var director = timelinesObject != null ? timelinesObject.GetComponent<PlayableDirector>() : null;
            if (director == null)
                return slots;
            slots.Add(director.playableAsset as TimelineAsset);
            var alsoPlays = director.GetComponent<TlAlsoPlays>();
            if (alsoPlays != null)
                slots.AddRange(alsoPlays.Timelines);
            return slots;
        }

        public static bool Plays(TlUiTimelines timelinesObject, TimelineAsset timeline)
        {
            var director = timelinesObject != null ? timelinesObject.GetComponent<PlayableDirector>() : null;
            return director != null && timeline != null && TlInspectorModel.Plays(director, director.GetComponent<TlAlsoPlays>()).Contains(timeline);
        }

        public static TlUiTimelines TimelinesObjectFor(TimelineAsset timeline)
        {
            var shownWithItsDirector = TlUiTargets.TimelinesObjectOf(TimelineEditor.inspectedDirector);
            if (shownWithItsDirector != null)
                return shownWithItsDirector;
            return TimelinesObjectsInOpenScenes().FirstOrDefault(timelinesObject => Plays(timelinesObject, timeline));
        }

        static string s_timelinesScriptPath;

        static string TimelinesScriptPath()
        {
            if (s_timelinesScriptPath != null)
                return s_timelinesScriptPath;
            var script = MonoImporter.GetAllRuntimeMonoScripts().FirstOrDefault(candidate => candidate != null && candidate.GetClass() == typeof(TlUiTimelines));
            s_timelinesScriptPath = script != null ? AssetDatabase.GetAssetPath(script) : "";
            return s_timelinesScriptPath;
        }

        static bool IsOpenForEditing(SubScene subScene) => subScene.EditingScene.IsValid() && subScene.EditingScene.isLoaded;

        static string[] FilesTheSubSceneUses(SubScene subScene) => string.IsNullOrEmpty(subScene.EditableScenePath) ? new string[0] : AssetDatabase.GetDependencies(subScene.EditableScenePath, true);

        const double SecondsAFoundListStaysTrue = 0.5;
        static double s_closedFoundAt = double.MinValue;
        static List<SubScene> s_closedFound = new List<SubScene>();

        public static List<SubScene> ClosedScreenTimelines()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - s_closedFoundAt < SecondsAFoundListStaysTrue && s_closedFound.All(subScene => subScene != null && !IsOpenForEditing(subScene)))
                return s_closedFound;
            var timelinesScript = TimelinesScriptPath();
            s_closedFound = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include)
                .Where(subScene => subScene.SceneAsset != null && !IsOpenForEditing(subScene) && FilesTheSubSceneUses(subScene).Contains(timelinesScript))
                .ToList();
            s_closedFoundAt = now;
            return s_closedFound;
        }

        public static void ForgetWhatWasFound() => s_closedFoundAt = double.MinValue;

        public static SubScene ClosedSubSceneThatPlays(TimelineAsset timeline)
        {
            var timelinePath = AssetDatabase.GetAssetPath(timeline);
            if (string.IsNullOrEmpty(timelinePath))
                return null;
            return ClosedScreenTimelines().FirstOrDefault(subScene => FilesTheSubSceneUses(subScene).Contains(timelinePath));
        }

        public static TlUiTimelines OpenForEditing(SubScene subScene)
        {
            if (subScene == null)
                return null;
            if (!IsOpenForEditing(subScene))
            {
                SubSceneUtility.EditScene(subScene);
                ForgetWhatWasFound();
            }
            if (!IsOpenForEditing(subScene))
                return null;
            foreach (var root in subScene.EditingScene.GetRootGameObjects())
            {
                var timelinesObject = root.GetComponentInChildren<TlUiTimelines>(true);
                if (timelinesObject != null)
                    return timelinesObject;
            }
            return null;
        }

        public static TlUiTimelines OpenEveryClosedScreenTimelines()
        {
            TlUiTimelines first = null;
            foreach (var subScene in ClosedScreenTimelines())
            {
                var opened = OpenForEditing(subScene);
                if (first == null)
                    first = opened;
            }
            return first;
        }

        public static TlUiTimelines FindOrOpenTimelinesObject()
        {
            var open = TimelinesObjectsInOpenScenes().FirstOrDefault();
            return open != null ? open : OpenEveryClosedScreenTimelines();
        }

        public static TlUiTimelines FindOrOpenTimelinesObjectFor(TimelineAsset timeline)
        {
            var open = TimelinesObjectsInOpenScenes().FirstOrDefault(timelinesObject => Plays(timelinesObject, timeline));
            return open != null ? open : OpenForEditing(ClosedSubSceneThatPlays(timeline));
        }

        public static PlayableDirector ShowInTheTimelineWindow(TimelineAsset timeline)
        {
            var timelinesObject = FindOrOpenTimelinesObjectFor(timeline);
            var director = timelinesObject != null ? timelinesObject.GetComponent<PlayableDirector>() : null;
            if (director == null)
                return null;
            if (Application.isBatchMode)
                return director;
            var window = TimelineEditor.GetOrCreateWindow();
            window.Show();
            window.locked = false;
            if (director.playableAsset == timeline)
                window.SetTimeline(director);
            else
                window.SetTimeline(timeline);
            window.locked = true;
            return director;
        }

        [OnOpenAsset(0)]
        static bool OpenATimelineWithItsScreen(EntityId entityId, int line)
        {
            var timeline = EditorUtility.EntityIdToObject(entityId) as TimelineAsset;
            if (timeline == null || FindOrOpenTimelinesObjectFor(timeline) == null)
                return false;
            return ShowInTheTimelineWindow(timeline) != null;
        }

        public static void EditTheMainTimeline(TlUiTimelines timelinesObject)
        {
            var main = Slots(timelinesObject).FirstOrDefault();
            if (main != null)
                ShowInTheTimelineWindow(main);
        }

        public static void EditTheMainTimelineOfAnyScreen()
        {
            var timelinesObject = FindOrOpenTimelinesObject();
            if (timelinesObject != null)
                EditTheMainTimeline(timelinesObject);
            else if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Tl UI", "There is no Screen Timelines in the open scenes. Right-click the Hierarchy and pick Tl UI > Screen to make one.", "OK");
        }

        public static void EditTheLayout(TlUiPanel panel)
        {
            var layout = panel != null && panel.Panel != null ? panel.Panel.visualTreeAsset : null;
            if (layout != null)
                AssetDatabase.OpenAsset(layout);
        }

        static string FileNameFrom(string typedName)
        {
            var name = new string(typedName.Trim().Select(letter => Path.GetInvalidFileNameChars().Contains(letter) || letter == '/' || letter == '\\' ? ' ' : letter).ToArray()).Trim();
            return name.Length == 0 ? "New Timeline" : name;
        }

        public static int AddTimeline(TlUiTimelines timelinesObject, string typedName)
        {
            var director = timelinesObject != null ? timelinesObject.GetComponent<PlayableDirector>() : null;
            if (director == null)
                return -1;
            var main = director.playableAsset as TimelineAsset;
            var folder = main != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(main)).Replace('\\', '/') : TlUiScreenMenu.DefaultFolder;
            var timeline = TlTimelines.CreateAsset();
            AssetDatabase.CreateAsset(timeline, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + FileNameFrom(typedName) + ".playable"));
            AssetDatabase.SaveAssets();
            return AddExistingTimeline(timelinesObject, timeline);
        }

        public static int AddExistingTimeline(TlUiTimelines timelinesObject, TimelineAsset timeline)
        {
            var director = timelinesObject != null ? timelinesObject.GetComponent<PlayableDirector>() : null;
            if (director == null || timeline == null)
                return -1;
            var slots = Slots(timelinesObject);
            if (slots.Contains(timeline))
                return slots.IndexOf(timeline);
            var alsoPlays = TlInspectorModel.AlsoPlays(director, true);
            Undo.RecordObject(alsoPlays, "Add Tl UI Timeline");
            alsoPlays.Timelines.Add(timeline);
            EditorUtility.SetDirty(alsoPlays);
            var scene = director.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
            return alsoPlays.Timelines.Count;
        }

        static TlUiPanel PanelOnTheSelection() => Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<TlUiPanel>() : null;

        static bool SelectionIsAScreenPart() =>
            Selection.activeGameObject != null
            && (Selection.activeGameObject.GetComponent<TlUiPanel>() != null
                || Selection.activeGameObject.GetComponent<TlUiTimelines>() != null
                || Selection.activeGameObject.GetComponent<SubScene>() != null);

        [MenuItem("GameObject/Tl UI/Edit Layout", false, 11)]
        static void EditTheLayoutFromTheMenu() => EditTheLayout(PanelOnTheSelection());

        [MenuItem("GameObject/Tl UI/Edit Layout", true)]
        static bool CanEditTheLayout() => PanelOnTheSelection() != null && PanelOnTheSelection().Panel != null && PanelOnTheSelection().Panel.visualTreeAsset != null;

        [MenuItem("GameObject/Tl UI/Edit Timeline", false, 12)]
        static void EditTheTimelineFromTheMenu()
        {
            var subScene = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<SubScene>() : null;
            var timelinesObject = subScene != null ? OpenForEditing(subScene) : Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<TlUiTimelines>() : null;
            if (timelinesObject != null)
                EditTheMainTimeline(timelinesObject);
            else
                EditTheMainTimelineOfAnyScreen();
        }

        [MenuItem("GameObject/Tl UI/Edit Timeline", true)]
        static bool CanEditTheTimeline() => SelectionIsAScreenPart();
    }
}
