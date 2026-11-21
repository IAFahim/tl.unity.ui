using System;
using System.IO;
using System.Linq;
using Tl.Generated;
using Tl.Unity;
using Tl.Unity.Editor;
using TlUi;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

public static class BasicsBuilder
{
    const string TimelineFile = "Basics.playable";
    const string ThemeFile = "Basics Theme.tss";
    const string PanelSettingsFile = "Basics Panel Settings.asset";
    const string ScreenFile = "Basics.uxml";
    const string BadgeFile = "Badge.uxml";
    const string ObjectsSceneFile = "Basics Objects.unity";
    const string SceneFile = "Basics.unity";

    static string SampleFolder()
    {
        foreach (var guid in AssetDatabase.FindAssets("BasicsBuilder t:MonoScript"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == nameof(BasicsBuilder))
                return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace(Path.DirectorySeparatorChar, '/');
        }
        throw new InvalidOperationException("BasicsBuilder.cs was not found under Assets.");
    }

    static void AddClip<TData>(TimelineAsset timeline, string trackName, double startSeconds, double durationSeconds, Action<TData> fill) where TData : TlClipData, new()
    {
        var track = timeline.CreateTrack<TlTrack>(null, trackName);
        track.Template = new TData();
        var clip = track.CreateClip<TlClip>();
        clip.displayName = trackName;
        clip.start = startSeconds;
        clip.duration = durationSeconds;
        fill((TData)((TlClip)clip.asset).Data);
        EditorUtility.SetDirty(clip.asset);
        EditorUtility.SetDirty(track);
    }

    static TimelineAsset LoadOrCreateTimeline(string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (existing != null)
            return existing;
        var timeline = TlTimelines.CreateAsset();
        AssetDatabase.CreateAsset(timeline, path);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 3.5;
        AddClip<Tl_Ui_ShowClip_TlData>(timeline, "Title shows", 0.0, 2.5, data => data.Id = 0);
        AddClip<Tl_Ui_HideClip_TlData>(timeline, "Hint hides", 0.5, 1.0, data => data.Id = 1);
        AddClip<Tl_Ui_DisableClip_TlData>(timeline, "Go locked", 0.0, 1.0, data => data.Id = 2);
        AddClip<Tl_Ui_FocusClip_TlData>(timeline, "Go focused", 1.0, 0.1, data => data.Id = 2);
        AddClip<Tl_Ui_AddClassClip_TlData>(timeline, "Badge holder glows", 1.0, 1.0, data => { data.Id = 3; data.ClassSlot = 0; });
        AddClip<Tl_Ui_AttachClip_TlData>(timeline, "Badge attached", 1.5, 1.5, data => { data.Id = 3; data.TemplateSlot = 0; });
        AddClip<Tl_Ui_AnimateClip_TlData>(timeline, "Fader fades in", 0.0, 1.0, data => { data.Channel = 0; data.Value = AnimationCurve.Linear(0f, 0f, 1f, 1f); data.Hold = true; });
        AddClip<Tl_Ui_TextClip_TlData>(timeline, "Caption text", 2.0, 1.0, data => { data.Channel = 1; data.TextSlot = 0; });
        AddClip<Tl_Ui_AnimateClip_TlData>(timeline, "Caption reveal", 2.0, 0.5, data => { data.Channel = 2; data.Value = AnimationCurve.Linear(0f, 0f, 1f, 1f); data.Hold = false; });
        AddClip<Tl_Ui_AnimateClip_TlData>(timeline, "Progress fills", 0.0, 3.0, data => { data.Channel = 3; data.Value = AnimationCurve.Linear(0f, 0f, 1f, 1f); data.Hold = true; });
        AddClip<Tl_Ui_SignalClip_TlData>(timeline, "Ping", 1.0, 0.1, data => data.Code = 1);
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
        director.playableAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + TimelineFile);
        director.playOnAwake = true;
        sequence.AddComponent<TlUiTimelines>();
        EditorSceneManager.SaveScene(objects, folder + "/" + ObjectsSceneFile);
    }

    static void ConfigurePanel(TlUiPanel panel, VisualTreeAsset badge, GameObject signalLight)
    {
        panel.Targets.Clear();
        panel.Targets.Add(new UiTarget { Id = 0, Selector = "#title" });
        panel.Targets.Add(new UiTarget { Id = 1, Selector = "#hint" });
        panel.Targets.Add(new UiTarget { Id = 2, Selector = "#go" });
        panel.Targets.Add(new UiTarget { Id = 3, Selector = "#badgeHolder" });
        panel.Targets.Add(new UiTarget { Id = 4, Selector = "#fader" });
        panel.Targets.Add(new UiTarget { Id = 5, Selector = "#caption" });
        panel.Targets.Add(new UiTarget { Id = 6, Selector = "#progress" });
        panel.ClassSlots.Clear();
        panel.ClassSlots.Add("is-glowing");
        panel.TemplateSlots.Clear();
        panel.TemplateSlots.Add(badge);
        panel.TextSlots.Clear();
        panel.TextSlots.Add("Text clips swap this caption while they are on.");
        panel.Channels.Clear();
        panel.Channels.Add(new UiChannelBinding { Channel = 0, TargetId = 4, Property = UiChannelProperty.Opacity });
        panel.Channels.Add(new UiChannelBinding { Channel = 1, TargetId = 5, Property = UiChannelProperty.Text });
        panel.Channels.Add(new UiChannelBinding { Channel = 2, TargetId = 5, Property = UiChannelProperty.Reveal });
        panel.Channels.Add(new UiChannelBinding { Channel = 3, TargetId = 6, Property = UiChannelProperty.Value });
        panel.Signals.Clear();
        var ping = new UiSignalReaction { Code = 1 };
        UnityEventTools.AddBoolPersistentListener(ping.OnSignal, new UnityAction<bool>(signalLight.SetActive), true);
        panel.Signals.Add(ping);
    }

    static void CreateScene(string folder)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var signalLight = new GameObject("Signal Light");
        signalLight.AddComponent<Light>().type = LightType.Point;
        signalLight.SetActive(false);
        var ui = new GameObject("UI");
        var panelRenderer = ui.AddComponent<PanelRenderer>();
        panelRenderer.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(folder + "/" + PanelSettingsFile);
        panelRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + ScreenFile);
        var uiPanel = ui.AddComponent<TlUiPanel>();
        uiPanel.Panel = panelRenderer;
        ConfigurePanel(uiPanel, AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + BadgeFile), signalLight);
        var subScene = new GameObject("Basics Objects").AddComponent<SubScene>();
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
            : uiPanel.Panel == null ? "Tl UI Panel has no Panel Renderer"
            : uiPanel.Panel.panelSettings == null ? "Panel Renderer has no Panel Settings"
            : uiPanel.Panel.panelSettings.themeStyleSheet == null ? "Panel Settings has no theme"
            : uiPanel.Panel.visualTreeAsset == null ? "Panel Renderer has no UXML"
            : uiPanel.TemplateSlots.Count == 0 || uiPanel.TemplateSlots[0] == null ? "the Badge template slot is empty"
            : uiPanel.Signals.Count == 0 || uiPanel.Signals[0].OnSignal.GetPersistentEventCount() == 0 ? "the Ping signal has no listener"
            : subScene == null || subScene.SceneAsset == null ? "the SubScene has no scene"
            : null;
        if (problem != null)
            throw new InvalidOperationException("Basics saved wrong: " + problem);
    }

    public static string Build()
    {
        var folder = SampleFolder();
        LoadOrCreateTimeline(folder + "/" + TimelineFile);
        LoadOrCreatePanelSettings(folder);
        CreateObjectsScene(folder);
        CreateScene(folder);
        var scenePath = folder + "/" + SceneFile;
        AddToBuildList(scenePath);
        AssetDatabase.SaveAssets();
        CheckWhatWasSaved(scenePath);
        return scenePath;
    }

    public static void BuildHeadless() => Debug.Log("BasicsBuilder: built " + Build());

    [MenuItem("Tl UI/Samples/Build Basics")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Build();
    }
}
