using System;
using System.IO;
using System.Linq;
using CardsSample;
using Tl.Generated;
using Tl.Ui;
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

public static class CardsBuilder
{
    const string DeckFile = "Card Set.asset";
    const string IntroTimelineFile = "Cards Intro.playable";
    const string NotEnoughEnergyTimelineFile = "Cards Not Enough Energy.playable";
    const string NewTurnTimelineFile = "Cards New Turn.playable";
    const string ThemeFile = "Cards Theme.tss";
    const string PanelSettingsFile = "Cards Panel Settings.asset";
    const string ScreenFile = "Cards.uxml";
    const string CardFile = "Card.uxml";
    const string ObjectsSceneFile = "Cards Objects.unity";
    const string SceneFile = "Cards.unity";

    static readonly string[] TargetNames = { "title", "topBar", "energy", "table", "hand", "draw", "endTurn", "banner" };
    static byte IdOf(string targetName) => (byte)Array.IndexOf(TargetNames, targetName);

    const int HandList = 0;
    const int TableList = 1;
    const int NotEnoughEnergySlot = 1;
    const int NewTurnSlot = 2;

    static string SampleFolder()
    {
        foreach (var guid in AssetDatabase.FindAssets("CardsBuilder t:MonoScript"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == nameof(CardsBuilder))
                return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace(Path.DirectorySeparatorChar, '/');
        }
        throw new InvalidOperationException("CardsBuilder.cs was not found under Assets.");
    }

    static CardSet.CardDefinition Definition(string name, int cost, int power, Color tint, int copies) => new CardSet.CardDefinition { Name = name, Cost = cost, Power = power, Tint = tint, Copies = copies };

    static CardSet LoadOrCreateDeck(string folder)
    {
        var path = folder + "/" + DeckFile;
        var existing = AssetDatabase.LoadAssetAtPath<CardSet>(path);
        if (existing != null)
            return existing;
        var deck = ScriptableObject.CreateInstance<CardSet>();
        deck.Cards.Add(Definition("Spark", 0, 1, new Color(1f, 0.85f, 0.45f), 3));
        deck.Cards.Add(Definition("Shield", 1, 2, new Color(0.6f, 0.8f, 1f), 3));
        deck.Cards.Add(Definition("Arrow", 1, 3, new Color(0.75f, 0.95f, 0.6f), 3));
        deck.Cards.Add(Definition("Fireball", 2, 5, new Color(1f, 0.6f, 0.45f), 2));
        deck.Cards.Add(Definition("Golem", 3, 8, new Color(0.8f, 0.7f, 0.95f), 2));
        deck.Cards.Add(Definition("Dragon", 5, 15, new Color(1f, 0.45f, 0.5f), 1));
        AssetDatabase.CreateAsset(deck, path);
        AssetDatabase.SaveAssets();
        return deck;
    }

    static TData AddClip<TData>(TimelineAsset timeline, string clipName, double startSeconds, double durationSeconds) where TData : TlClipData, new()
    {
        var track = timeline.CreateTrack<TlTrack>(null, TlMenu.ShortName(new TData().MenuName));
        track.Template = new TData();
        var clip = track.CreateClip<TlClip>();
        clip.displayName = clipName;
        clip.start = startSeconds;
        clip.duration = durationSeconds;
        EditorUtility.SetDirty(clip.asset);
        EditorUtility.SetDirty(track);
        return (TData)((TlClip)clip.asset).Data;
    }

    static AnimationCurve EaseOut() => new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

    static AnimationCurve Wiggle() => new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(0.35f, 0f), new Keyframe(0.55f, 1f), new Keyframe(0.75f, 0f), new Keyframe(0.9f, 0.8f), new Keyframe(1f, 0.5f));

    static void Animate(TimelineAsset timeline, string clipName, string targetName, UiProperty property, float from, float to, double startSeconds, double durationSeconds, AnimationCurve ease, bool hold)
    {
        var data = AddClip<Tl_Ui_AnimateClip_TlData>(timeline, clipName, startSeconds, durationSeconds);
        data.Target = new UiTargetRef { Id = IdOf(targetName) };
        data.Property = property;
        data.From = from;
        data.To = to;
        data.Ease = ease;
        data.Hold = hold;
    }

    static TimelineAsset LoadOrCreateTimeline(string path, double seconds, Action<TimelineAsset> addClips)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (existing != null)
            return existing;
        var timeline = TlTimelines.CreateAsset();
        AssetDatabase.CreateAsset(timeline, path);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = seconds;
        addClips(timeline);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return timeline;
    }

    static void AddIntroClips(TimelineAsset timeline)
    {
        Animate(timeline, "Fade title", "title", UiProperty.Opacity, 0f, 1f, 0.0, 0.5, EaseOut(), true);
        Animate(timeline, "Move topBar Y", "topBar", UiProperty.MoveY, -90f, 0f, 0.0, 0.5, EaseOut(), true);
        Animate(timeline, "Fade table", "table", UiProperty.Opacity, 0f, 1f, 0.2, 0.6, EaseOut(), true);
        Animate(timeline, "Move hand Y", "hand", UiProperty.MoveY, 320f, 0f, 0.3, 0.7, EaseOut(), true);
    }

    static void AddNotEnoughEnergyClips(TimelineAsset timeline)
    {
        Animate(timeline, "Move energy X", "energy", UiProperty.MoveX, -12f, 12f, 0.0, 0.45, Wiggle(), false);
        var warn = AddClip<Tl_Ui_AddClassClip_TlData>(timeline, "Add Class energy", 0.0, 0.45);
        warn.Target = new UiTargetRef { Id = IdOf("energy") };
        warn.Class = new UiClassRef { Slot = 0 };
    }

    static void AddNewTurnClips(TimelineAsset timeline)
    {
        var show = AddClip<Tl_Ui_ShowClip_TlData>(timeline, "Show banner", 0.0, 1.0);
        show.Target = new UiTargetRef { Id = IdOf("banner") };
        Animate(timeline, "Scale banner", "banner", UiProperty.Scale, 0.4f, 1f, 0.0, 0.35, EaseOut(), false);
        Animate(timeline, "Fade banner", "banner", UiProperty.Opacity, 1f, 0f, 0.7, 0.3, AnimationCurve.Linear(0f, 0f, 1f, 1f), false);
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

    struct CodesTheGameReads
    {
        public int Draw;
        public int EndTurn;
        public int Play;
        public int Return;
        public int Reorder;
        public int NotEnoughEnergySignal;
    }

    static CodesTheGameReads CreateObjectsScene(string folder)
    {
        var objects = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sequence = new GameObject("UI Sequence");
        var director = sequence.AddComponent<PlayableDirector>();
        director.playableAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + IntroTimelineFile);
        director.playOnAwake = true;
        var alsoPlays = sequence.AddComponent<TlAlsoPlays>();
        alsoPlays.Timelines.Add(AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + NotEnoughEnergyTimelineFile));
        alsoPlays.Timelines.Add(AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + NewTurnTimelineFile));
        sequence.AddComponent<TlUiTimelines>().TargetNames.AddRange(TargetNames);
        var cards = sequence.AddComponent<CardsAuthoring>();
        cards.Deck = AssetDatabase.LoadAssetAtPath<CardSet>(folder + "/" + DeckFile);
        var codes = new CodesTheGameReads { Draw = cards.DrawCode, EndTurn = cards.EndTurnCode, Play = cards.PlayCode, Return = cards.ReturnCode, Reorder = cards.ReorderCode, NotEnoughEnergySignal = cards.NotEnoughEnergySignal };
        EditorSceneManager.SaveScene(objects, folder + "/" + ObjectsSceneFile);
        return codes;
    }

    static void ConfigurePanel(TlUiPanel uiPanel, HandRows handRows, TableRows tableRows, CardScoreView scoreView, string folder, CodesTheGameReads codes)
    {
        var card = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + CardFile);
        uiPanel.Targets.Clear();
        uiPanel.ClassSlots.Clear();
        uiPanel.ClassSlots.Add("warning");
        uiPanel.Lists.Clear();
        uiPanel.Lists.Insert(HandList, new UiListBinding { TargetId = IdOf("hand"), Rows = handRows, RowTemplate = card, DragCode = codes.Reorder, DropCode = codes.Return });
        uiPanel.Lists.Insert(TableList, new UiListBinding { TargetId = IdOf("table"), Rows = tableRows, RowTemplate = card, DropCode = codes.Play });
        uiPanel.Commands.Clear();
        uiPanel.Commands.Add(new UiCommand { TargetId = IdOf("draw"), On = UiCommandEvent.Click, Do = UiCommandAction.SendToGame, GameCode = codes.Draw });
        uiPanel.Commands.Add(new UiCommand { TargetId = IdOf("endTurn"), On = UiCommandEvent.Click, Do = UiCommandAction.SendToGame, GameCode = codes.EndTurn });
        uiPanel.Commands.Add(new UiCommand { TargetId = IdOf("endTurn"), On = UiCommandEvent.Click, Do = UiCommandAction.PlayTimeline, TimelineSlot = NewTurnSlot });
        uiPanel.Signals.Clear();
        uiPanel.Signals.Add(new UiSignalReaction { Code = codes.NotEnoughEnergySignal, Do = UiCommandAction.PlayTimeline, TimelineSlot = NotEnoughEnergySlot });
        uiPanel.DataSource = scoreView;
    }

    static void CreateScene(string folder, CodesTheGameReads codes)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var ui = new GameObject("UI");
        var panelRenderer = ui.AddComponent<PanelRenderer>();
        panelRenderer.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(folder + "/" + PanelSettingsFile);
        panelRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + ScreenFile);
        var handRows = ui.AddComponent<HandRows>();
        var tableRows = ui.AddComponent<TableRows>();
        var scoreView = ui.AddComponent<CardScoreView>();
        var uiPanel = ui.AddComponent<TlUiPanel>();
        uiPanel.Panel = panelRenderer;
        ConfigurePanel(uiPanel, handRows, tableRows, scoreView, folder, codes);
        var subScene = new GameObject("Cards Objects").AddComponent<SubScene>();
        subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(folder + "/" + ObjectsSceneFile);
        subScene.AutoLoadScene = true;
        EditorSceneManager.SaveScene(scene, folder + "/" + SceneFile);
    }

    static void AddToBuildList(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != scenePath && !(Path.GetFileName(scene.path) == Path.GetFileName(scenePath) && !File.Exists(scene.path))).ToList();
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void CheckWhatWasSaved(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var uiPanel = UnityEngine.Object.FindAnyObjectByType<TlUiPanel>();
        var subScene = UnityEngine.Object.FindAnyObjectByType<SubScene>();
        var problem = uiPanel == null || uiPanel.Panel == null || uiPanel.Panel.visualTreeAsset == null ? "the Tl UI Panel is not wired"
            : uiPanel.Lists.Count != 2 || uiPanel.Lists.Any(list => list.Rows == null || list.RowTemplate == null) ? "a list has no rows or no row template"
            : !(uiPanel.DataSource is CardScoreView) ? "the Tl UI Panel's data source is not the Card Score View"
            : subScene == null || subScene.SceneAsset == null ? "the SubScene has no scene"
            : null;
        if (problem != null)
            throw new InvalidOperationException("Cards saved wrong: " + problem);
    }

    public static string Build()
    {
        var folder = SampleFolder();
        LoadOrCreateDeck(folder);
        LoadOrCreateTimeline(folder + "/" + IntroTimelineFile, 1.0, AddIntroClips);
        LoadOrCreateTimeline(folder + "/" + NotEnoughEnergyTimelineFile, 0.45, AddNotEnoughEnergyClips);
        LoadOrCreateTimeline(folder + "/" + NewTurnTimelineFile, 1.0, AddNewTurnClips);
        LoadOrCreatePanelSettings(folder);
        var codes = CreateObjectsScene(folder);
        CreateScene(folder, codes);
        var scenePath = folder + "/" + SceneFile;
        AddToBuildList(scenePath);
        AssetDatabase.SaveAssets();
        CheckWhatWasSaved(scenePath);
        return scenePath;
    }

    public static void BuildHeadless() => Debug.Log("CardsBuilder: built " + Build());

    [MenuItem("Tl UI/Samples/Build Cards")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Build();
    }
}
