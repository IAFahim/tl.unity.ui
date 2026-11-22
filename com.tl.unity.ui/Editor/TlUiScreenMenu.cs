using System.IO;
using Tl.Unity.Editor;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace TlUi.Editor
{
    public static class TlUiScreenMenu
    {
        public const string DefaultFolder = "Assets/Tl UI";

        const string StarterScreen =
@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <ui:VisualElement name=""screen"" picking-mode=""Ignore"" style=""flex-grow: 1; align-items: center; justify-content: center;"">
        <ui:Label name=""hello"" text=""Hello"" style=""font-size: 96px; color: rgb(255, 255, 255); background-color: rgb(40, 90, 200); padding: 24px 48px; border-radius: 16px;"" />
    </ui:VisualElement>
</ui:UXML>
";

        public struct Created
        {
            public GameObject Screen;
            public SubScene Timelines;
            public TimelineAsset Timeline;
        }

        [MenuItem("GameObject/Tl UI/Screen", false, 10)]
        static void CreateFromMenu()
        {
            var created = Create(DefaultFolder);
            if (created.Screen == null)
                return;
            var director = OpenTimelinesObject(created.Timelines);
            Selection.activeGameObject = director != null ? director.gameObject : created.Screen;
            if (director != null && !Application.isBatchMode)
                TimelineEditor.GetOrCreateWindow().SetTimeline(director);
        }

        public static PlayableDirector OpenTimelinesObject(SubScene timelines)
        {
            if (timelines == null)
                return null;
            if (!timelines.EditingScene.isLoaded)
                SubSceneUtility.EditScene(timelines);
            var scene = timelines.EditingScene;
            if (!scene.isLoaded)
                return null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<TlUiTimelines>(out _))
                    return root.GetComponent<PlayableDirector>();
            return null;
        }

        public static Created Create(string folder)
        {
            var screenScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(screenScene.path))
            {
                EditorUtility.DisplayDialog("Tl UI Screen", "Save this scene first, then add the screen again.", "OK");
                return default;
            }
            CreateFolder(folder);
            var panelSettings = CreatePanelSettings(folder);
            var screenFile = CreateText(folder + "/Screen.uxml", StarterScreen);
            var timeline = CreateTimeline(folder + "/Screen Timeline.playable");
            var timelinesScenePath = CreateTimelinesScene(folder + "/Screen Timelines.unity", timeline, screenScene);

            var subSceneObject = new GameObject("Screen Timelines");
            var subScene = subSceneObject.AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(timelinesScenePath);
            subScene.AutoLoadScene = true;
            Undo.RegisterCreatedObjectUndo(subSceneObject, "Create Tl UI Screen");

            var screen = new GameObject("Screen");
            var panelRenderer = screen.AddComponent<PanelRenderer>();
            panelRenderer.panelSettings = panelSettings;
            panelRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(screenFile);
            var panel = screen.AddComponent<TlUiPanel>();
            panel.Panel = panelRenderer;
            Undo.RegisterCreatedObjectUndo(screen, "Create Tl UI Screen");
            EditorSceneManager.MarkSceneDirty(screenScene);
            return new Created { Screen = screen, Timelines = subScene, Timeline = timeline };
        }

        static void CreateFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            CreateFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static string CreateText(string wantedPath, string text)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath(wantedPath);
            File.WriteAllText(path, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        static PanelSettings CreatePanelSettings(string folder)
        {
            var themePath = CreateText(folder + "/Screen Theme.tss", "@import url(\"unity-theme://default\");\n");
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            AssetDatabase.CreateAsset(panelSettings, AssetDatabase.GenerateUniqueAssetPath(folder + "/Screen Panel Settings.asset"));
            return panelSettings;
        }

        static TimelineAsset CreateTimeline(string wantedPath)
        {
            var timeline = TlTimelines.CreateAsset();
            AssetDatabase.CreateAsset(timeline, AssetDatabase.GenerateUniqueAssetPath(wantedPath));
            AssetDatabase.SaveAssets();
            return timeline;
        }

        static string CreateTimelinesScene(string wantedPath, TimelineAsset timeline, UnityEngine.SceneManagement.Scene screenScene)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath(wantedPath);
            var timelinesScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var timelines = new GameObject("Screen Timelines");
            var director = timelines.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = true;
            timelines.AddComponent<TlUiTimelines>().TargetNames.Add("hello");
            EditorSceneManager.SaveScene(timelinesScene, path);
            EditorSceneManager.CloseScene(timelinesScene, true);
            EditorSceneManager.SetActiveScene(screenScene);
            return path;
        }
    }
}
