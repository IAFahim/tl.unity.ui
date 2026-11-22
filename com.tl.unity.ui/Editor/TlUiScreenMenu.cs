using System.IO;
using Tl.Unity.Editor;
using Unity.Scenes;
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
@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:tl=""TlUi"">
    <ui:VisualElement name=""screen"" picking-mode=""Ignore"" style=""flex-grow: 1; align-items: center; justify-content: center;"">
        <ui:Label name=""hello"" text=""Hello"" style=""font-size: 96px; color: rgb(255, 255, 255); background-color: rgb(40, 90, 200); padding: 24px 48px; border-radius: 16px;"">
            <tl:TlTarget id=""0"" />
        </ui:Label>
    </ui:VisualElement>
</ui:UXML>
";

        [MenuItem("GameObject/Tl UI/Screen", false, 10)]
        static void CreateFromMenu()
        {
            var screen = Create(DefaultFolder, out var timeline);
            if (screen == null)
                return;
            Selection.activeGameObject = screen;
            if (!Application.isBatchMode)
                TimelineEditor.GetOrCreateWindow().SetTimeline(timeline);
        }

        public static GameObject Create(string folder, out TimelineAsset timeline)
        {
            timeline = null;
            var screenScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(screenScene.path))
            {
                EditorUtility.DisplayDialog("Tl UI Screen", "Save this scene first, then add the screen again.", "OK");
                return null;
            }
            CreateFolder(folder);
            var panelSettings = CreatePanelSettings(folder);
            var screenFile = CreateText(folder + "/Screen.uxml", StarterScreen);
            timeline = CreateTimeline(folder + "/Screen Timeline.playable");
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
            return screen;
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
            timelines.AddComponent<TlUiTimelines>();
            EditorSceneManager.SaveScene(timelinesScene, path);
            EditorSceneManager.CloseScene(timelinesScene, true);
            EditorSceneManager.SetActiveScene(screenScene);
            return path;
        }
    }
}
