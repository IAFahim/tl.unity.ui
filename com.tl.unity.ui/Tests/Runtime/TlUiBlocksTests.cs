using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tl;
using Tl.Ui;
using TlUi;
using Unity.Collections;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TlUiPackage.Tests
{
    public class TlUiBlocksTests
    {
        World _world;
        Entity _host;
        Entity _settings;
        GameObject _screen;
        TlUiPanel _panel;
        TestSettingsView _view;

        static PanelSettings PanelSettingsWithEmptyTheme()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            return panelSettings;
        }

        IEnumerator BuildScreen(System.Action<TlUiPanel> configure, System.Action<VisualElement> addElements, PanelSettings panelSettings = null)
        {
            _world = new World("Tl UI blocks test");
            var entityManager = _world.EntityManager;
            _host = entityManager.CreateEntity(typeof(UiSwitches), typeof(UiChannels), typeof(UiInputState));
            entityManager.AddBuffer<UiTimelineSlot>(_host).Add(new UiTimelineSlot { Timeline = new TlTimeline { Asset = new TlAssetId(1, 2, 3, 4) } });
            entityManager.AddBuffer<Tl.Unity.TimelineRequest>(_host);
            entityManager.AddBuffer<UiCommandInbox>(_host);
            entityManager.AddBuffer<UiTextInbox>(_host);
            _settings = entityManager.CreateEntity(typeof(TestSettingsData));
            entityManager.SetComponentData(_settings, new TestSettingsData { Volume = 0.45f, Muted = true, Coins = 100 });
            _screen = new GameObject("Blocks screen");
            _screen.SetActive(false);
            var panelRenderer = _screen.AddComponent<PanelRenderer>();
            panelRenderer.panelSettings = panelSettings ?? PanelSettingsWithEmptyTheme();
            _panel = _screen.AddComponent<TlUiPanel>();
            _panel.Panel = panelRenderer;
            _view = _screen.AddComponent<TestSettingsView>();
            _view.Bind(_world);
            _panel.DataSource = _view;
            configure(_panel);
            _panel.Bind(_world);
            _screen.SetActive(true);
            var rootDeadline = Time.realtimeSinceStartup + 10f;
            while (_panel.Root == null && Time.realtimeSinceStartup < rootDeadline)
                yield return null;
            Assert.IsNotNull(_panel.Root, "the Panel Renderer hands its root to the Tl UI Panel");
            addElements(_panel.Root);
            _panel.ResolveTargetsAgain();
            yield return null;
            yield return null;
        }

        void SetSettings(TestSettingsData data) => _world.EntityManager.SetComponentData(_settings, data);

        TestSettingsData Settings => _world.EntityManager.GetComponentData<TestSettingsData>(_settings);

        List<(int Code, int Payload)> Inbox() => _world.EntityManager.GetBuffer<UiCommandInbox>(_host).AsNativeArray().ToArray().Select(command => (command.Code, command.Payload)).ToList();

        RequestOp[] Requests() => _world.EntityManager.GetBuffer<Tl.Unity.TimelineRequest>(_host).AsNativeArray().ToArray().Select(request => request.Op).ToArray();

        static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
                yield return null;
        }

        static DataBinding Bound<TBinding>(TBinding binding, string path) where TBinding : DataBinding
        {
            binding.dataSourcePath = new PropertyPath(path);
            return binding;
        }

        [TearDown]
        public void DestroyScreenAndWorld()
        {
            if (_screen != null)
                Object.Destroy(_screen);
            if (_world != null && _world.IsCreated)
                _world.Dispose();
        }

        [UnityTest]
        public IEnumerator ClassTriggerAndSmoothBindingsFollowEcsDataAndTheRadialFillTakesBoundValues()
        {
            var banner = new Label("muted") { name = "banner" };
            var buy = new Button { name = "buy", text = "Buy" };
            var bar = new ProgressBar { name = "bar", lowValue = 0f, highValue = 1f };
            var coins = new Label { name = "coins" };
            var radial = new TlRadialFill { name = "radial" };
            radial.style.width = 64;
            radial.style.height = 64;
            var smoothBar = new TlSmoothBinding { Seconds = 0.3f };
            var smoothCoins = new TlSmoothBinding { Seconds = 0.3f };
            yield return BuildScreen(panel => { }, root =>
            {
                TlUiConverters.Register();
                banner.SetBinding("muted-class", Bound(new TlClassBinding { ClassName = "muted" }, nameof(TestSettingsData.Muted)));
                banner.SetBinding("muted-trigger", Bound(new TlTriggerBinding { Timeline = 0, WhenOn = UiTimelineVerb.Play, WhenOff = UiTimelineVerb.Stop }, nameof(TestSettingsData.Muted)));
                var enabledWhenSound = Bound(new DataBinding { bindingMode = BindingMode.ToTarget }, nameof(TestSettingsData.Muted));
                Assert.IsTrue(ConverterGroups.TryGetConverterGroup(TlUiConverters.Not, out var not));
                enabledWhenSound.ApplyConverterGroupToUI(not);
                buy.SetBinding("enabledSelf", enabledWhenSound);
                bar.SetBinding("value", Bound(smoothBar, nameof(TestSettingsData.Volume)));
                coins.SetBinding("text", Bound(smoothCoins, nameof(TestSettingsData.Coins)));
                radial.SetBinding("value", Bound(new DataBinding { bindingMode = BindingMode.ToTarget }, nameof(TestSettingsData.Volume)));
                root.Add(banner);
                root.Add(buy);
                root.Add(bar);
                root.Add(coins);
                root.Add(radial);
            });
            yield return Frames(3);
            Assert.IsTrue(banner.ClassListContains("muted"), "Tl Class Binding turns the class on while Muted is true");
            Assert.IsFalse(buy.enabledSelf, "a plain binding on enabledSelf through tl-not disables Buy while Muted");
            CollectionAssert.AreEqual(new[] { RequestOp.Start }, Requests(), "Tl Trigger Binding plays timeline slot 0 because Muted starts true");
            Assert.AreEqual(0.45f, bar.value, 0.0001f, "Tl Smooth Binding shows the first value at once");
            Assert.AreEqual("100", coins.text, "on a Label it shows the number as cached integer text");
            Assert.AreEqual(0.45f, radial.value, 0.0001f, "Tl Radial Fill binds through its value property");

            SetSettings(new TestSettingsData { Volume = 0.9f, Muted = false, Coins = 200 });
            yield return Frames(3);
            var movingDeadline = Time.realtimeSinceStartup + 1f;
            while (bar.value <= 0.45f && Time.realtimeSinceStartup < movingDeadline)
                yield return null;
            Assert.IsFalse(banner.ClassListContains("muted"), "the class goes off with the data");
            Assert.IsTrue(buy.enabledSelf);
            CollectionAssert.AreEqual(new[] { RequestOp.Start, RequestOp.Stop }, Requests(), "the trigger stops the timeline on the falling edge, once");
            Assert.Greater(bar.value, 0.45f, "the bar is on its way");
            Assert.Less(bar.value, 0.9f, "but not there yet: it eases");
            Assert.AreEqual(0.9f, radial.value, 0.0001f);
            var easeDeadline = Time.realtimeSinceStartup + 3f;
            while ((Mathf.Abs(bar.value - 0.9f) > 0.0001f || coins.text != "200") && Time.realtimeSinceStartup < easeDeadline)
                yield return null;
            Assert.AreEqual(0.9f, bar.value, 0.0001f, "the bar arrives at the new value");
            Assert.AreEqual("200", coins.text, "the counter rolls to the new value");

            SetSettings(new TestSettingsData { Volume = 0.9f, Muted = false, Coins = 201 });
            yield return Frames(3);
            CollectionAssert.AreEqual(new[] { RequestOp.Start, RequestOp.Stop }, Requests(), "other fields changing do not re-fire the trigger");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator BindingsAuthoredInUxmlKeepEveryAttribute()
        {
            var screen = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.tl.unity.ui/Tests/Runtime/AuthoredBindings.uxml");
            Assert.IsNotNull(screen);
            yield return BuildScreen(panel => panel.Panel.visualTreeAsset = screen, root => { });
            var flagged = _panel.Root.Q("flagged");
            Assert.IsTrue(flagged.TryGetBinding("flag", out var flagBinding), "the class binding loads from UXML");
            var classBinding = (TlClassBinding)flagBinding;
            Assert.AreEqual("quiet", classBinding.ClassName, "uss-class parses");
            Assert.IsTrue(classBinding.Invert, "invert parses");
            Assert.IsTrue(flagged.TryGetBinding("trigger", out var triggerBinding));
            var trigger = (TlTriggerBinding)triggerBinding;
            Assert.AreEqual(UiTimelineVerb.Restart, trigger.WhenOn, "when-on parses");
            Assert.AreEqual(UiTimelineVerb.Pause, trigger.WhenOff, "when-off parses");
            Assert.IsTrue(_panel.Root.Q("eased").TryGetBinding("value", out var smoothBinding));
            Assert.AreEqual(0.2f, ((TlSmoothBinding)smoothBinding).Seconds, 0.0001f, "seconds parses");

            yield return Frames(3);
            Assert.IsFalse(flagged.ClassListContains("quiet"), "invert: Muted true turns the class off");
            CollectionAssert.AreEqual(new[] { RequestOp.Stop, RequestOp.Start }, Requests(), "Muted starts true, so the trigger restarts slot 0");
            SetSettings(new TestSettingsData { Volume = 0.45f, Muted = false, Coins = 100 });
            yield return Frames(3);
            Assert.IsTrue(flagged.ClassListContains("quiet"));
            CollectionAssert.AreEqual(new[] { RequestOp.Stop, RequestOp.Start, RequestOp.Pause }, Requests(), "the falling edge pauses it");
        }
#endif

        [UnityTest]
        public IEnumerator ABurstSystemRaisesASignalThatPlaysAUiTimeline()
        {
            yield return BuildScreen(panel => panel.Signals.Add(new UiSignalReaction { Code = 9, Do = UiCommandAction.PlayTimeline, TimelineSlot = 0 }), root => { });
            var gameEvents = _world.EntityManager.CreateEntity(typeof(LevelUpEvents));
            _world.EntityManager.SetComponentData(gameEvents, new LevelUpEvents { Code = 9 });
            _world.EntityManager.AddBuffer<Tl.Unity.TimelineEvent<UiSignal>>(gameEvents);
            var raise = _world.CreateSystem<RaiseLevelUpSystem>();
            raise.Update(_world.Unmanaged);
            yield return Frames(2);
            CollectionAssert.AreEqual(new[] { RequestOp.Start }, Requests(), "the Burst system's UiSignals.Raise reaches the Signals table, which plays UI timeline slot 0");
        }

        [UnityTest]
        public IEnumerator PointerOverUiCountsPickableElementsNotIgnoredLayoutContainers()
        {
            var settings = InputSystem.settings;
            var testSettings = ScriptableObject.CreateInstance<InputSettings>();
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            var mouse = InputSystem.AddDevice<Mouse>("Tl UI Test Mouse");
            mouse.MakeCurrent();
            try
            {
                var layout = new VisualElement { name = "layout", pickingMode = PickingMode.Ignore };
                layout.style.position = Position.Absolute;
                layout.style.left = 0;
                layout.style.top = 0;
                layout.style.width = 600;
                layout.style.height = 400;
                var corner = new Button { name = "corner", text = "corner" };
                corner.style.position = Position.Absolute;
                corner.style.left = 10;
                corner.style.top = 10;
                corner.style.width = 120;
                corner.style.height = 40;
                layout.Add(corner);
                yield return BuildScreen(panel => { }, root => root.Add(layout));
                yield return Frames(2);

                void PointAt(Vector2 panelPosition)
                {
                    var origin = RuntimePanelUtils.ScreenToPanel(_panel.Root.panel, Vector2.zero);
                    var scale = (RuntimePanelUtils.ScreenToPanel(_panel.Root.panel, new Vector2(100f, 100f)) - origin) / 100f;
                    var topLeftScreen = new Vector2((panelPosition.x - origin.x) / scale.x, (panelPosition.y - origin.y) / scale.y);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(topLeftScreen.x, Screen.height - topLeftScreen.y) });
                    InputSystem.Update();
                }

                PointAt(corner.worldBound.center);
                yield return Frames(2);
                Debug.Log("PointerOverUi: screen " + Screen.width + "x" + Screen.height + ", corner " + corner.worldBound + ", mouse " + mouse.position.ReadValue());
                Assert.IsTrue(_world.EntityManager.GetComponentData<UiInputState>(_host).PointerOverUi, "the pointer over a button near the top-left corner is over UI (screen y is flipped into panel space)");

                var emptySpot = new Vector2(corner.worldBound.xMax + 200f, corner.worldBound.yMax + 200f);
                Assert.IsTrue(layout.worldBound.Contains(emptySpot), "the empty spot lies inside the layout container (" + layout.worldBound + ")");
                PointAt(emptySpot);
                yield return Frames(2);
                Assert.IsFalse(_world.EntityManager.GetComponentData<UiInputState>(_host).PointerOverUi, "empty space inside a picking-mode Ignore layout container is not over UI");

                layout.pickingMode = PickingMode.Position;
                yield return Frames(2);
                Assert.IsTrue(_world.EntityManager.GetComponentData<UiInputState>(_host).PointerOverUi, "a pickable full-screen container counts, so set picking-mode Ignore on layout containers");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                if (settings != null)
                    InputSystem.settings = settings;
            }
        }

        [UnityTest]
        public IEnumerator TwoWayKeepsAGameWriteMadeInTheSameFrame()
        {
            var mute = new Toggle { name = "mute" };
            yield return BuildScreen(panel => { }, root =>
            {
                mute.SetBinding("value", Bound(new DataBinding { bindingMode = BindingMode.TwoWay }, nameof(TestSettingsData.Muted)));
                root.Add(mute);
            });
            yield return Frames(2);
            Assert.IsTrue(mute.value);
            mute.value = false;
            SetSettings(new TestSettingsData { Volume = 0.6f, Muted = true, Coins = 100 });
            yield return Frames(3);
            Assert.IsFalse(Settings.Muted, "the toggle's write reaches ECS");
            Assert.AreEqual(0.6f, Settings.Volume, 0.0001f, "and the game's write to another field in the same frame survives: only changed fields are written");
        }

        [UnityTest]
        public IEnumerator ChannelsLeaveBoundTextAloneAndCancelTypingTextAndDropdownReachTheGame()
        {
            var price = new Label { name = "price" };
            var screenRoot = new VisualElement { name = "settingsScreen" };
            var nameField = new TextField { name = "playerName", isDelayed = false };
            nameField.AddComponent(new TlSendText { Code = 50 });
            var quality = new DropdownField(new List<string> { "Low", "Medium", "High" }, 0) { name = "quality" };
            quality.AddComponent(new TlSendValue { Code = 51, Scale = 1f });
            yield return BuildScreen(panel =>
            {
                panel.Targets.Add(new UiTarget { Id = 0, Selector = "#price" });
                panel.Targets.Add(new UiTarget { Id = 1, Selector = "#settingsScreen" });
                panel.Channels.Add(new UiChannelBinding { Channel = 0, TargetId = 0, Property = UiChannelProperty.Opacity });
                panel.Commands.Add(new UiCommand { TargetId = 1, On = UiCommandEvent.Cancel, Do = UiCommandAction.SendToGame, GameCode = 52 });
            }, root =>
            {
                TlUiConverters.Register();
                var priceText = Bound(new DataBinding { bindingMode = BindingMode.ToTarget }, nameof(TestSettingsData.Coins));
                Assert.IsTrue(ConverterGroups.TryGetConverterGroup(TlUiConverters.Integer, out var integer));
                priceText.ApplyConverterGroupToUI(integer);
                price.SetBinding("text", priceText);
                screenRoot.Add(nameField);
                screenRoot.Add(quality);
                root.Add(price);
                root.Add(screenRoot);
            });
            yield return Frames(2);
            Assert.AreEqual("100", price.text);
            var channels = new UiChannels { Live = 1 };
            channels.Values.Set(0, 0.5f);
            _world.EntityManager.SetComponentData(_host, channels);
            yield return Frames(2);
            Assert.AreEqual(0.5f, price.resolvedStyle.opacity, 0.001f, "the opacity channel reaches the bound label");
            Assert.AreEqual("100", price.text, "and leaves its bound text alone");

            nameField.Focus();
            yield return Frames(2);
            Assert.IsTrue(_panel.SomeoneIsTyping(), "focus in a TextField counts as typing, so hotkeys are ignored");
            Assert.IsTrue(_world.EntityManager.GetComponentData<UiInputState>(_host).Typing, "UiInputState tells Burst gameplay the player is typing");
            nameField.value = "Ada";
            using (var cancel = NavigationCancelEvent.GetPooled())
            {
                cancel.target = nameField;
                nameField.SendEvent(cancel);
            }
            quality.index = 2;
            nameField.Blur();
            yield return Frames(2);
            Assert.IsFalse(_world.EntityManager.GetComponentData<UiInputState>(_host).Typing);
            var texts = _world.EntityManager.GetBuffer<UiTextInbox>(_host).AsNativeArray().ToArray();
            Assert.AreEqual(1, texts.Length, "TlSendText sends the new text once");
            Assert.AreEqual(50, texts[0].Code);
            Assert.AreEqual("Ada", texts[0].Text.ToString());
            CollectionAssert.AreEqual(new[] { (52, 0), (51, 2) }, Inbox(), "Cancel (Escape, gamepad East) bubbles to the screen's Cancel command; a dropdown sends its index");
        }

        [UnityTest]
        public IEnumerator AWorldSpacePanelResolvesAndTakesClips()
        {
            var worldSettings = PanelSettingsWithEmptyTheme();
            worldSettings.renderMode = PanelRenderMode.WorldSpace;
            var sign = new Label("Shop") { name = "sign" };
            yield return BuildScreen(panel => panel.Targets.Add(new UiTarget { Id = 0, Selector = "#sign" }), root => root.Add(sign), worldSettings);
            Assert.AreEqual(PanelRenderMode.WorldSpace, _panel.Panel.panelSettings.renderMode);
            Assert.AreSame(sign, _panel.FirstElementOf(0), "a world-space Panel Renderer hands its root to the Tl UI Panel like a screen one");
            _world.EntityManager.SetComponentData(_host, new UiSwitches { Hide = UiIds.BitOf(0) });
            yield return Frames(2);
            Assert.AreEqual(DisplayStyle.None, sign.style.display.value, "a Hide clip reaches the world-space element");
            StringAssert.Contains("#sign", _panel.Dump());
        }
    }
}
