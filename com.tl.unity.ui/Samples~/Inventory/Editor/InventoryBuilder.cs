using System;
using System.IO;
using System.Linq;
using InventorySample;
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

public static class InventoryBuilder
{
    const string IntroTimelineFile = "Inventory Intro.playable";
    const string ShopRowsFile = "Shop Rows.asset";
    const string ThemeFile = "Inventory Theme.tss";
    const string PanelSettingsFile = "Inventory Panel Settings.asset";
    const string ScreenFile = "Inventory.uxml";
    const string BagSlotFile = "BagSlot.uxml";
    const string ShopRowFile = "ShopRow.uxml";
    const string EquipSlotFile = "EquipSlot.uxml";
    const string NameplateFile = "Nameplate.uxml";
    const string ObjectsSceneFile = "Inventory Objects.unity";
    const string SceneFile = "Inventory.unity";

    static string SampleFolder()
    {
        foreach (var guid in AssetDatabase.FindAssets("InventoryBuilder t:MonoScript"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == nameof(InventoryBuilder))
                return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace(Path.DirectorySeparatorChar, '/');
        }
        throw new InvalidOperationException("InventoryBuilder.cs was not found under Assets.");
    }

    static ShopItem LoadOrCreateShopItem(string folder, string displayName, int price, string description)
    {
        var path = folder + "/Shop " + displayName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<ShopItem>(path);
        if (existing != null)
            return existing;
        var item = ScriptableObject.CreateInstance<ShopItem>();
        item.DisplayName = displayName;
        item.Price = price;
        item.Description = description;
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    static TlUiAssetRows LoadOrCreateShopRows(string folder)
    {
        var path = folder + "/" + ShopRowsFile;
        var existing = AssetDatabase.LoadAssetAtPath<TlUiAssetRows>(path);
        if (existing != null)
            return existing;
        var rows = ScriptableObject.CreateInstance<TlUiAssetRows>();
        rows.Items.Add(LoadOrCreateShopItem(folder, "Potion", 15, "Restores 25 health"));
        rows.Items.Add(LoadOrCreateShopItem(folder, "Shield", 60, "Blocks the next hit"));
        rows.Items.Add(LoadOrCreateShopItem(folder, "Arrow", 2, "One arrow for the bow"));
        AssetDatabase.CreateAsset(rows, path);
        AssetDatabase.SaveAssets();
        return rows;
    }

    static TimelineAsset LoadOrCreateIntro(string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (existing != null)
            return existing;
        var timeline = TlTimelines.CreateAsset();
        AssetDatabase.CreateAsset(timeline, path);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 2.0;
        var track = timeline.CreateTrack<TlTrack>(null, "Title shows");
        track.Template = new Tl_Ui_ShowClip_TlData();
        var clip = track.CreateClip<TlClip>();
        clip.displayName = "Title shows";
        clip.start = 0.0;
        clip.duration = 2.0;
        ((Tl_Ui_ShowClip_TlData)((TlClip)clip.asset).Data).Target = new UiTargetRef { Id = 0 };
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

    static void AddEnemy(string displayName, Vector3 position, float radiansPerSecond)
    {
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.name = displayName;
        UnityEngine.Object.DestroyImmediate(enemy.GetComponent<Collider>());
        enemy.transform.position = position;
        var authoring = enemy.AddComponent<EnemyAuthoring>();
        authoring.DisplayName = displayName;
        authoring.RadiansPerSecond = radiansPerSecond;
    }

    static void CreateObjectsScene(string folder)
    {
        var objects = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sequence = new GameObject("UI Sequence");
        var director = sequence.AddComponent<PlayableDirector>();
        director.playableAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(folder + "/" + IntroTimelineFile);
        director.playOnAwake = true;
        sequence.AddComponent<TlUiTimelines>();
        var bag = sequence.AddComponent<BagAuthoring>();
        bag.Shop = AssetDatabase.LoadAssetAtPath<TlUiAssetRows>(folder + "/" + ShopRowsFile);
        bag.StartingItems.Add(new BagAuthoring.StartingItem { Name = "Potion", Count = 3 });
        bag.StartingItems.Add(new BagAuthoring.StartingItem { Name = "Sword", Count = 1 });
        bag.StartingItems.Add(new BagAuthoring.StartingItem { Name = "Arrow", Count = 12 });
        AddEnemy("Goblin", new Vector3(-4f, 0.5f, 0f), 1.0f);
        AddEnemy("Orc", new Vector3(0f, 0.5f, 2f), -0.7f);
        AddEnemy("Troll", new Vector3(4f, 0.5f, 0f), 0.5f);
        EditorSceneManager.SaveScene(objects, folder + "/" + ObjectsSceneFile);
    }

    static void ConfigurePanel(TlUiPanel uiPanel, BagRows bagRows, EquipmentRows equipmentRows, TlUiAssetRows shopRows, string folder)
    {
        uiPanel.Targets.Clear();
        uiPanel.Targets.Add(new UiTarget { Id = 0, Selector = "#title" });
        uiPanel.Targets.Add(new UiTarget { Id = 1, Selector = "#bag" });
        uiPanel.Targets.Add(new UiTarget { Id = 2, Selector = "#shop" });
        uiPanel.Targets.Add(new UiTarget { Id = 4, Selector = "#equipment" });
        uiPanel.Lists.Clear();
        uiPanel.Lists.Add(new UiListBinding { TargetId = 1, Rows = bagRows, RowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + BagSlotFile), ClickCode = 1, DragCode = 2, DropCode = 5 });
        uiPanel.Lists.Add(new UiListBinding { TargetId = 2, Rows = shopRows, RowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + ShopRowFile), ClickCode = 3 });
        uiPanel.Lists.Add(new UiListBinding { TargetId = 4, Rows = equipmentRows, RowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + EquipSlotFile), DropCode = 4 });
    }

    static void CreateScene(string folder)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var camera = Camera.main;
        camera.transform.position = new Vector3(0f, 9f, -11f);
        camera.transform.LookAt(new Vector3(0f, 0f, 1f));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(2f, 1f, 1.2f);
        var ui = new GameObject("UI");
        var panelRenderer = ui.AddComponent<PanelRenderer>();
        panelRenderer.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(folder + "/" + PanelSettingsFile);
        panelRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + ScreenFile);
        var bagRows = ui.AddComponent<BagRows>();
        var equipmentRows = ui.AddComponent<EquipmentRows>();
        var uiPanel = ui.AddComponent<TlUiPanel>();
        uiPanel.Panel = panelRenderer;
        ConfigurePanel(uiPanel, bagRows, equipmentRows, AssetDatabase.LoadAssetAtPath<TlUiAssetRows>(folder + "/" + ShopRowsFile), folder);
        ui.AddComponent<TlUiTooltips>();
        var nameplates = ui.AddComponent<NameplateViews>();
        nameplates.Panel = uiPanel;
        nameplates.ContainerId = 3;
        nameplates.ViewTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/" + NameplateFile);
        nameplates.Camera = camera;
        nameplates.WorldOffset = new Vector3(0f, 1.2f, 0f);
        var subScene = new GameObject("Inventory Objects").AddComponent<SubScene>();
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
        var nameplates = UnityEngine.Object.FindAnyObjectByType<NameplateViews>();
        var subScene = UnityEngine.Object.FindAnyObjectByType<SubScene>();
        var problem = uiPanel == null || uiPanel.Panel == null || uiPanel.Panel.visualTreeAsset == null ? "the Tl UI Panel is not wired"
            : uiPanel.Lists.Count != 3 || uiPanel.Lists.Any(list => list.Rows == null || list.RowTemplate == null) ? "a list has no rows or no row template"
            : nameplates == null || nameplates.Panel == null || nameplates.ViewTemplate == null || nameplates.Camera == null ? "the Nameplate Views are not wired"
            : subScene == null || subScene.SceneAsset == null ? "the SubScene has no scene"
            : null;
        if (problem != null)
            throw new InvalidOperationException("Inventory saved wrong: " + problem);
    }

    public static string Build()
    {
        var folder = SampleFolder();
        LoadOrCreateShopRows(folder);
        LoadOrCreateIntro(folder + "/" + IntroTimelineFile);
        LoadOrCreatePanelSettings(folder);
        CreateObjectsScene(folder);
        CreateScene(folder);
        var scenePath = folder + "/" + SceneFile;
        AddToBuildList(scenePath);
        AssetDatabase.SaveAssets();
        CheckWhatWasSaved(scenePath);
        return scenePath;
    }

    public static void BuildHeadless() => Debug.Log("InventoryBuilder: built " + Build());

    [MenuItem("Tl UI/Samples/Build Inventory")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Build();
    }
}
