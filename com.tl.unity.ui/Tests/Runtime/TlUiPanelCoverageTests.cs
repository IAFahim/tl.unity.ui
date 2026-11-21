using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tl;
using Tl.Ui;
using TlUi;
using Unity.Entities;
using Unity.Properties;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TlUiPackage.Tests
{
    public class TlUiPanelCoverageTests
    {
        World _world;
        Entity _host;
        GameObject _screen;
        TlUiPanel _panel;

        static PanelSettings PanelSettingsWithEmptyTheme()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            return panelSettings;
        }

        IEnumerator BuildScreen(System.Action<TlUiPanel> configure, System.Action<VisualElement> addElements)
        {
            _world = new World("Tl UI coverage test");
            var entityManager = _world.EntityManager;
            _host = entityManager.CreateEntity(typeof(UiSwitches), typeof(UiChannels));
            entityManager.AddBuffer<UiTimelineSlot>(_host).Add(new UiTimelineSlot { Timeline = new TlTimeline { Asset = new TlAssetId(1, 2, 3, 4) } });
            entityManager.AddBuffer<Tl.Unity.TimelineRequest>(_host);
            entityManager.AddBuffer<UiCommandInbox>(_host);
            _screen = new GameObject("Screen");
            _screen.SetActive(false);
            var panelRenderer = _screen.AddComponent<PanelRenderer>();
            panelRenderer.panelSettings = PanelSettingsWithEmptyTheme();
            _panel = _screen.AddComponent<TlUiPanel>();
            _panel.Panel = panelRenderer;
            configure(_panel);
            _panel.Bind(_world);
            _screen.SetActive(true);
            var rootDeadline = Time.realtimeSinceStartup + 10f;
            while (_panel.Root == null && Time.realtimeSinceStartup < rootDeadline)
                yield return null;
            Assert.IsNotNull(_panel.Root);
            addElements(_panel.Root);
            _panel.ResolveTargetsAgain();
            yield return null;
            yield return null;
        }

        IEnumerator Show(UiSwitches switches, UiChannels channels)
        {
            _world.EntityManager.SetComponentData(_host, switches);
            _world.EntityManager.SetComponentData(_host, channels);
            yield return null;
            yield return null;
        }

        List<(int Code, int Payload)> Inbox() => _world.EntityManager.GetBuffer<UiCommandInbox>(_host).AsNativeArray().ToArray().Select(command => (command.Code, command.Payload)).ToList();

        static void Send<TEvent>(VisualElement target) where TEvent : EventBase<TEvent>, new()
        {
            using var sent = EventBase<TEvent>.GetPooled();
            sent.target = target;
            target.SendEvent(sent);
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
        public IEnumerator KeepSpaceEnableRemoveClassAndTransformChannelsReachTheElementsAndLetGo()
        {
            var locked = new Button { name = "locked", text = "locked" };
            var tagged = new Label("tagged") { name = "tagged" };
            var mover = new VisualElement { name = "mover" };
            yield return BuildScreen(panel =>
            {
                panel.Targets.Add(new UiTarget { Id = 3, Selector = "#locked" });
                panel.Targets.Add(new UiTarget { Id = 4, Selector = "#tagged" });
                panel.Targets.Add(new UiTarget { Id = 5, Selector = "#mover" });
                panel.ClassSlots.Add("lit");
                panel.ClassSlots.Add("tag");
                panel.Channels.Add(new UiChannelBinding { Channel = 1, TargetId = 5, Property = UiChannelProperty.TranslateX });
                panel.Channels.Add(new UiChannelBinding { Channel = 2, TargetId = 5, Property = UiChannelProperty.TranslateY });
                panel.Channels.Add(new UiChannelBinding { Channel = 3, TargetId = 5, Property = UiChannelProperty.Scale });
                panel.Channels.Add(new UiChannelBinding { Channel = 4, TargetId = 5, Property = UiChannelProperty.Rotate });
                panel.Channels.Add(new UiChannelBinding { Channel = 5, TargetId = 5, Property = UiChannelProperty.WidthPercent });
            }, root =>
            {
                locked.SetEnabled(false);
                tagged.AddToClassList("tag");
                root.Add(locked);
                root.Add(tagged);
                root.Add(mover);
            });
            var moved = new UiChannels { Live = 0b111110 };
            moved.Values.Set(1, 10f);
            moved.Values.Set(2, 20f);
            moved.Values.Set(3, 1.5f);
            moved.Values.Set(4, 45f);
            moved.Values.Set(5, 50f);
            yield return Show(new UiSwitches { Invisible = 1UL << 5, Enable = 1UL << 3, Remove1 = 1UL << 4 }, moved);
            Assert.AreEqual(Visibility.Hidden, mover.resolvedStyle.visibility, "Hide with Keep Space hides in place");
            Assert.AreEqual(DisplayStyle.Flex, mover.resolvedStyle.display);
            Assert.IsTrue(locked.enabledInHierarchy, "Enable makes an authored-disabled button usable");
            Assert.IsFalse(tagged.ClassListContains("tag"), "Remove Class removes an authored class");
            Assert.AreEqual(new Vector3(10f, 20f, 0f), mover.resolvedStyle.translate);
            Assert.AreEqual(1.5f, mover.resolvedStyle.scale.value.x, 0.0001f);
            Assert.AreEqual(45f, mover.resolvedStyle.rotate.angle.ToDegrees(), 0.01f);
            Assert.AreEqual(Length.Percent(50f), mover.style.width.value);

            yield return Show(default, default);
            Assert.AreEqual(Visibility.Visible, mover.resolvedStyle.visibility);
            Assert.IsFalse(locked.enabledInHierarchy, "with no clip on, the button is disabled again as authored");
            Assert.IsTrue(tagged.ClassListContains("tag"), "with no clip on, the authored class is back");
            Assert.AreEqual(Vector3.zero, mover.resolvedStyle.translate);
            Assert.AreEqual(1f, mover.resolvedStyle.scale.value.x, 0.0001f);
            Assert.AreEqual(StyleKeyword.Null, mover.style.width.keyword);
        }

        [UnityTest]
        public IEnumerator PointerAndValueEventsSendCodesAndClicksSendEveryTimelineVerb()
        {
            var zone = new VisualElement { name = "zone" };
            var volume = new Slider(0f, 100f) { name = "volume" };
            var buttons = new[] { "play", "stop", "pause", "resume" }.Select(name => new Button { name = name, text = name }).ToArray();
            yield return BuildScreen(panel =>
            {
                panel.Targets.Add(new UiTarget { Id = 0, Selector = "#zone" });
                panel.Targets.Add(new UiTarget { Id = 1, Selector = "#volume" });
                panel.Targets.Add(new UiTarget { Id = 2, Selector = "#play" });
                panel.Targets.Add(new UiTarget { Id = 3, Selector = "#stop" });
                panel.Targets.Add(new UiTarget { Id = 4, Selector = "#pause" });
                panel.Targets.Add(new UiTarget { Id = 5, Selector = "#resume" });
                panel.Commands.Add(new UiCommand { TargetId = 0, On = UiCommandEvent.PointerEnter, Do = UiCommandAction.SendToGame, GameCode = 10, GamePayload = 1 });
                panel.Commands.Add(new UiCommand { TargetId = 0, On = UiCommandEvent.PointerLeave, Do = UiCommandAction.SendToGame, GameCode = 11, GamePayload = 2 });
                panel.Commands.Add(new UiCommand { TargetId = 1, On = UiCommandEvent.ValueChanged, Do = UiCommandAction.SendToGame, GameCode = 12 });
                panel.Commands.Add(new UiCommand { TargetId = 2, On = UiCommandEvent.Click, Do = UiCommandAction.PlayTimeline });
                panel.Commands.Add(new UiCommand { TargetId = 3, On = UiCommandEvent.Click, Do = UiCommandAction.StopTimeline });
                panel.Commands.Add(new UiCommand { TargetId = 4, On = UiCommandEvent.Click, Do = UiCommandAction.PauseTimeline });
                panel.Commands.Add(new UiCommand { TargetId = 5, On = UiCommandEvent.Click, Do = UiCommandAction.ResumeTimeline });
            }, root =>
            {
                root.Add(zone);
                root.Add(volume);
                foreach (var button in buttons)
                    root.Add(button);
            });
            Send<PointerEnterEvent>(zone);
            Send<PointerLeaveEvent>(zone);
            volume.value = 42f;
            foreach (var button in buttons)
                Send<ClickEvent>(button);
            yield return null;
            CollectionAssert.AreEqual(new[] { (10, 1), (11, 2), (12, 42) }, Inbox(), "pointer enter, pointer leave and value changed send their codes; value changed sends the new value");
            var ops = _world.EntityManager.GetBuffer<Tl.Unity.TimelineRequest>(_host).AsNativeArray().ToArray().Select(request => request.Op).ToArray();
            CollectionAssert.AreEqual(new[] { RequestOp.Start, RequestOp.Stop, RequestOp.Pause, RequestOp.Resume }, ops, "each button sends its verb for timeline slot 0");
        }

        [UnityTest]
        public IEnumerator AScriptableObjectDataSourceBindsThroughThePercentAndHiddenConverters()
        {
            var settings = ScriptableObject.CreateInstance<UiTestSettings>();
            var volumeText = new Label { name = "volumeText" };
            var mutedBanner = new Label("muted") { name = "mutedBanner" };
            yield return BuildScreen(panel => panel.DataSource = settings, root =>
            {
                TlUiConverters.Register();
                var percentText = new DataBinding { dataSourcePath = new PropertyPath(nameof(UiTestSettings.Volume)), bindingMode = BindingMode.ToTarget };
                Assert.IsTrue(ConverterGroups.TryGetConverterGroup(TlUiConverters.Percent, out var percent));
                percentText.ApplyConverterGroupToUI(percent);
                volumeText.SetBinding("text", percentText);
                var hiddenWhenMuted = new DataBinding { dataSourcePath = new PropertyPath(nameof(UiTestSettings.Muted)), bindingMode = BindingMode.ToTarget };
                Assert.IsTrue(ConverterGroups.TryGetConverterGroup(TlUiConverters.Hidden, out var hidden));
                hiddenWhenMuted.ApplyConverterGroupToUI(hidden);
                mutedBanner.SetBinding("style.display", hiddenWhenMuted);
                root.Add(volumeText);
                root.Add(mutedBanner);
            });
            yield return null;
            yield return null;
            Assert.AreEqual("45%", volumeText.text, "Volume 0.45 through tl-percent");
            Assert.AreEqual(DisplayStyle.None, mutedBanner.resolvedStyle.display, "Muted true through tl-hidden");
            Object.Destroy(settings);
        }
    }
}
