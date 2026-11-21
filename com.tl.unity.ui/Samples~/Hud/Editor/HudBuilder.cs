using System;
using System.IO;
using System.Linq;
using HudSample;
using Tl.Generated;
using Tl.Unity;
using Tl.Unity.Editor;
using TlUi;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

public static class HudBuilder
{
    const string IntroTimelineFile = "Hud Intro.playable";
    const string FlashTimelineFile = "Hud Flash.playable";
    const string ThemeFile = "Hud Theme.tss";
    const string PanelSettingsFile = "Hud Panel Settings.asset";
    const string ScreenFile = "Hud.uxml";
    const string ObjectsSceneFile = "Hud Objects.unity";
    const string SceneFile = "Hud.unity";

    static string SampleFolder()
    {
        foreach (var guid in AssetDatabase.FindAssets("HudBuilder t:MonoScript"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == nameof(HudBuilder))
                return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace(Path.DirectorySeparatorChar, '/');
        }
        throw new InvalidOperationException("HudBuilder.cs was not found under Assets.");
    }

    static TimelineAsset LoadOrCreateShowTimeline(string path, double seconds, byte shownId)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (existing != null)
            return existing;
        var timeline = TlTimelines.CreateAsset();
        AssetDatabase.CreateAsset(timeline, path);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = seconds;
        var track = timeline.CreateTrack<TlTrack>(null, "Show");
        track.Template = new Tl_Ui_ShowClip_TlData();
        var clip = track.CreateClip<TlClip>();
        clip.displayName = "Show";
        clip.start = 0.0;
        clip.duration = seconds;
        ((Tl_Ui_ShowClip_TlData)((TlClip)clip.asset).Data).Id = shownId;
        EditorUtility.SetDirty(clip.asset);
        EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return timeline;
    }

    static PanelSettings LoadOrCreatePanelSettings(string folder)
    {
        var path = folder + "/" + PanelSettingsFile;
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
        if (existing != null)
            return existing;
        var themePath = folder + "/" + ThemeFile;
        if (!File.Exists(themePath))
        {
            File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceSynchronousImport);
        }
        var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        AssetDatabase.CreateAsset(panelSettings, path);
        AssetDatabase.SaveAssets();
        return panelSettings;
    }

    static void CreateObjectsScene(string folder)
    {
        var objects = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sequence = new GameObject("UI Sequence");
        var director = sequence.AddComponent<PlayableDirector>();
        director.playableAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + IntroTimelineFile);
        director.playOnAwake = true;
        sequence.AddComponent<TlAlsoPlays>().Timelines.Add(AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + FlashTimelineFile));
        sequence.AddComponent<TlUiTimelines>();
        sequence.AddComponent<HudDemoAuthoring>();
        EditorSceneManager.SaveScene(objects, folder + "/" + ObjectsSceneFile);
    }

    static void ConfigurePanel(TlUiPanel uiPanel, HudView hudView)
    {
        uiPanel.Targets.Clear();
        uiPanel.Targets.Add(new UiTarget { Id = 0, Selector = "#title" });
        uiPanel.Targets.Add(new UiTarget { Id = 1, Selector = "#flashBanner" });
        uiPanel.Targets.Add(new UiTarget { Id = 2, Selector = "#heal" });
        uiPanel.Targets.Add(new UiTarget { Id = 3, Selector = "#flash" });
        uiPanel.Commands.Clear();
        uiPanel.Commands.Add(new UiCommand { TargetId = 2, On = UiCommandEvent.Click, Do = UiCommandAction.SendToGame, GameCode = 1 });
        uiPanel.Commands.Add(new UiCommand { TargetId = 3, On = UiCommandEvent.Click, Do = UiCommandAction.PlayTimeline, TimelineSlot = 1 });
        uiPanel.DataSource = hudView;
    }

    static void CreateScene(string folder)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var ui = new GameObject("UI");
        var panelRenderer = ui.AddComponent<PanelRenderer>();
        panelRenderer.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(folder + "/" + PanelSettingsFile);
        panelRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + ScreenFile);
        var hudView = ui.AddComponent<HudView>();
        var uiPanel = ui.AddComponent<TlUiPanel>();
        uiPanel.Panel = panelRenderer;
        ConfigurePanel(uiPanel, hudView);
        var subScene = new GameObject("Hud Objects").AddComponent<SubScene>();
        subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(folder + "/" + ObjectsSceneFile);
        subScene.AutoLoadScene = true;
        EditorSceneManager.SaveScene(scene, folder + "/" + SceneFile);
    }

    static void AddToBuildList(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != scenePath).ToList();
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void CheckWhatWasSaved(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var uiPanel = UnityEngine.Object.FindAnyObjectByType<TlUiPanel>();
        var subScene = UnityEngine.Object.FindAnyObjectByType<SubScene>();
        var problem = uiPanel == null ? "no Tl UI Panel"
            : uiPanel.Panel == null || uiPanel.Panel.panelSettings == null || uiPanel.Panel.visualTreeAsset == null ? "the Panel Renderer is not wired"
            : !(uiPanel.DataSource is HudView) ? "the Tl UI Panel's data source is not the Hud View"
            : uiPanel.Commands.Count != 2 ? "the Heal and Flash commands are missing"
            : subScene == null || subScene.SceneAsset == null ? "the SubScene has no scene"
            : null;
        if (problem != null)
            throw new InvalidOperationException("Hud saved wrong: " + problem);
    }

    public static string Build()
    {
        var folder = SampleFolder();
        LoadOrCreateShowTimeline(folder + "/" + IntroTimelineFile, 1.5, 0);
        LoadOrCreateShowTimeline(folder + "/" + FlashTimelineFile, 0.5, 1);
        LoadOrCreatePanelSettings(folder);
        CreateObjectsScene(folder);
        CreateScene(folder);
        var scenePath = folder + "/" + SceneFile;
        AddToBuildList(scenePath);
        AssetDatabase.SaveAssets();
        CheckWhatWasSaved(scenePath);
        return scenePath;
    }

    public static void BuildHeadless() => Debug.Log("HudBuilder: built " + Build());

    [MenuItem("Tl UI/Samples/Build Hud")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Build();
    }
}
