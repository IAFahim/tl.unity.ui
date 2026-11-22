using System;
using System.IO;
using System.Linq;
using MenuSample;
using Tl.Generated;
using Tl.Unity;
using Tl.Unity.Editor;
using TlUi;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

public static class MenuBuilder
{
    const string MainTimelineFile = "Menu Main Screen.playable";
    const string SettingsTimelineFile = "Menu Settings Screen.playable";
    const string ActionsFile = "Menu Actions.asset";
    const string ThemeFile = "Menu Theme.tss";
    const string PanelSettingsFile = "Menu Panel Settings.asset";
    const string ScreenFile = "Menu.uxml";
    const string ObjectsSceneFile = "Menu Objects.unity";
    const string SceneFile = "Menu.unity";
    const int ScreenChannel = 1;

    static string SampleFolder()
    {
        foreach (var guid in AssetDatabase.FindAssets("MenuBuilder t:MonoScript"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == nameof(MenuBuilder))
                return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace(Path.DirectorySeparatorChar, '/');
        }
        throw new InvalidOperationException("MenuBuilder.cs was not found under Assets.");
    }

    static TimelineAsset LoadOrCreateScreenTimeline(string path, byte screenId)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (existing != null)
            return existing;
        var timeline = TlTimelines.CreateAsset();
        AssetDatabase.CreateAsset(timeline, path);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 0.5;
        var track = timeline.CreateTrack<TlTrack>(null, "Screen shows");
        track.Template = new Tl_Ui_ShowClip_TlData();
        var clip = track.CreateClip<TlClip>();
        clip.displayName = "Screen shows";
        clip.start = 0.0;
        clip.duration = 0.5;
        ((Tl_Ui_ShowClip_TlData)((TlClip)clip.asset).Data).Id = screenId;
        timeline.CreateMarkerTrack();
        var settings = timeline.markerTrack.CreateMarker<TlTimelineSettings>(0.0);
        settings.End = TlEnd.Hold;
        settings.Channel = ScreenChannel;
        EditorUtility.SetDirty(clip.asset);
        EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return timeline;
    }

    static InputActionReference LoadOrCreatePauseHotkey(string folder)
    {
        var path = folder + "/" + ActionsFile;
        var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>().FirstOrDefault();
        if (existing != null)
            return existing;
        var actions = ScriptableObject.CreateInstance<InputActionAsset>();
        actions.name = "Menu Actions";
        var pause = actions.AddActionMap("Menu").AddAction("Pause", binding: "<Keyboard>/p");
        pause.AddBinding("<Gamepad>/start");
        AssetDatabase.CreateAsset(actions, path);
        var reference = InputActionReference.Create(pause);
        reference.name = "Menu/Pause";
        AssetDatabase.AddObjectToAsset(reference, actions);
        AssetDatabase.SaveAssets();
        return reference;
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
        director.playableAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + MainTimelineFile);
        director.playOnAwake = true;
        sequence.AddComponent<TlAlsoPlays>().Timelines.Add(AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + SettingsTimelineFile));
        sequence.AddComponent<TlUiTimelines>();
        sequence.AddComponent<MenuAuthoring>();
        EditorSceneManager.SaveScene(objects, folder + "/" + ObjectsSceneFile);
    }

    static void ConfigurePanel(TlUiPanel uiPanel, MenuView menuView, InputActionReference pauseHotkey)
    {
        uiPanel.Targets.Clear();
        uiPanel.Commands.Clear();
        uiPanel.Commands.Add(new UiCommand { TargetId = 1, On = UiCommandEvent.Cancel, Do = UiCommandAction.PlayTimeline, TimelineSlot = 0 });
        uiPanel.Commands.Add(new UiCommand { On = UiCommandEvent.HotkeyOnly, Do = UiCommandAction.SendToGame, GameCode = 2, Hotkey = pauseHotkey });
        uiPanel.DataSource = menuView;
    }

    static void CreateScene(string folder, InputActionReference pauseHotkey)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var ui = new GameObject("UI");
        var panelRenderer = ui.AddComponent<PanelRenderer>();
        panelRenderer.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(folder + "/" + PanelSettingsFile);
        panelRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + ScreenFile);
        var menuView = ui.AddComponent<MenuView>();
        var uiPanel = ui.AddComponent<TlUiPanel>();
        uiPanel.Panel = panelRenderer;
        ConfigurePanel(uiPanel, menuView, pauseHotkey);
        ui.AddComponent<TlUiTooltips>();
        var subScene = new GameObject("Menu Objects").AddComponent<SubScene>();
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
        var problem = uiPanel == null || uiPanel.Panel == null || uiPanel.Panel.visualTreeAsset == null ? "the Tl UI Panel is not wired"
            : !(uiPanel.DataSource is MenuView) ? "the Tl UI Panel's data source is not the Menu View"
            : uiPanel.Commands.Count != 2 || uiPanel.Commands[1].Hotkey == null || uiPanel.Commands[1].Hotkey.action == null ? "the Cancel and Pause commands are missing"
            : UnityEngine.Object.FindAnyObjectByType<TlUiTooltips>() == null ? "no Tl UI Tooltips"
            : subScene == null || subScene.SceneAsset == null ? "the SubScene has no scene"
            : null;
        if (problem != null)
            throw new InvalidOperationException("Menu saved wrong: " + problem);
    }

    public static string Build()
    {
        var folder = SampleFolder();
        LoadOrCreateScreenTimeline(folder + "/" + MainTimelineFile, 0);
        LoadOrCreateScreenTimeline(folder + "/" + SettingsTimelineFile, 1);
        var pauseHotkey = LoadOrCreatePauseHotkey(folder);
        LoadOrCreatePanelSettings(folder);
        CreateObjectsScene(folder);
        CreateScene(folder, pauseHotkey);
        var scenePath = folder + "/" + SceneFile;
        AddToBuildList(scenePath);
        AssetDatabase.SaveAssets();
        CheckWhatWasSaved(scenePath);
        return scenePath;
    }

    public static void BuildHeadless() => Debug.Log("MenuBuilder: built " + Build());

    [MenuItem("Tl UI/Samples/Build Menu")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Build();
    }
}
