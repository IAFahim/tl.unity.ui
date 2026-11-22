using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tl.Ui;
using TlUi;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TlUiPackage.Tests
{
    public class TlUiPanelTests
    {
        World _world;
        Entity _host;
        GameObject _screen;
        TlUiPanel _panel;
        VisualElement _box;
        Label _firstCard;
        Button _cardButton;

        static PanelSettings PanelSettingsWithEmptyTheme()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            return panelSettings;
        }

        IEnumerator BuildScreenBoundToATestWorld()
        {
            _world = new World("Tl UI panel test");
            _host = _world.EntityManager.CreateEntity(typeof(UiSwitches), typeof(UiChannels));
            _screen = new GameObject("Screen");
            _screen.SetActive(false);
            var panelRenderer = _screen.AddComponent<PanelRenderer>();
            panelRenderer.panelSettings = PanelSettingsWithEmptyTheme();
            _panel = _screen.AddComponent<TlUiPanel>();
            _panel.Panel = panelRenderer;
            _panel.Targets.Add(new UiTarget { Id = 0, Selector = "#box" });
            _panel.Targets.Add(new UiTarget { Id = 1, Selector = ".card" });
            _panel.Targets.Add(new UiTarget { Id = 2, Selector = "Button" });
            _panel.ClassSlots.Add("lit");
            _panel.Bind(_world);
            _screen.SetActive(true);
            var rootDeadline = Time.realtimeSinceStartup + 10f;
            while (_panel.Root == null && Time.realtimeSinceStartup < rootDeadline)
                yield return null;
            Assert.IsNotNull(_panel.Root, "the Panel Renderer hands its root to the Tl UI Panel through the reload callback");
            _box = new VisualElement { name = "box" };
            _firstCard = new Label("first card");
            _firstCard.AddToClassList("card");
            var secondCard = new Label("second card");
            secondCard.AddToClassList("card");
            _cardButton = new Button { text = "card button" };
            _cardButton.AddToClassList("card");
            _box.Add(_firstCard);
            _box.Add(secondCard);
            _box.Add(_cardButton);
            _panel.Root.Add(_box);
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

        static UiChannels BoxOpacity(float opacity)
        {
            var channels = default(UiChannels);
            UiChannelSlots.Write(ref channels, UiChannelKey.Of(new UiTargetRef { Id = 0 }, UiProperty.Opacity), opacity, false);
            return channels;
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
        public IEnumerator SelectorsAddressOneElementAKindAndATypeAndTheStrongerSwitchWins()
        {
            yield return BuildScreenBoundToATestWorld();
            Assert.AreEqual(4, _panel.Elements.Count, "#box, two .card labels and the .card Button");
            yield return Show(new UiSwitches { Show = 1UL << 0, Add0 = 1UL << 0, Hide = 1UL << 2, Disable = 1UL << 1 }, BoxOpacity(0.25f));
            Assert.AreEqual(DisplayStyle.Flex, _box.resolvedStyle.display);
            Assert.IsTrue(_box.ClassListContains("lit"));
            Assert.AreEqual(0.25f, _box.resolvedStyle.opacity, 0.001f);
            Assert.IsFalse(_firstCard.enabledInHierarchy, "every .card is disabled through Id 1");
            Assert.AreEqual(DisplayStyle.None, _cardButton.resolvedStyle.display, "the Button is a .card and a Button: Id 2 hides it");
            Assert.AreEqual(DisplayStyle.Flex, _firstCard.resolvedStyle.display);

            yield return Show(default, default);
            Assert.IsFalse(_box.ClassListContains("lit"), "with no clip on, the authored classes come back");
            Assert.AreEqual(1f, _box.resolvedStyle.opacity, 0.001f);
            Assert.IsTrue(_firstCard.enabledInHierarchy);
            Assert.AreEqual(DisplayStyle.Flex, _cardButton.resolvedStyle.display);
            Debug.Log("Tl UI panel test dump:\n" + _panel.Dump());
        }

        [UnityTest]
        public IEnumerator ThePanelAllocatesNothingPerFrameOnceEveryPathRanWhileStateChangesAndWhileIdle()
        {
            yield return BuildScreenBoundToATestWorld();
            var probes = new GameObject("Allocation probes");
            var windowStart = probes.AddComponent<DocumentAllocationStart>();
            var windowEnd = probes.AddComponent<DocumentAllocationEnd>();
            windowEnd.WindowStart = windowStart;
            var lit = new UiSwitches { Show = 1UL << 0, Add0 = 1UL << 0, Hide = 1UL << 2, Disable = 1UL << 1 };
            for (var warmUp = 0; warmUp < 3; warmUp++)
            {
                yield return Show(lit, BoxOpacity(0.5f));
                yield return Show(default, default);
            }
            windowEnd.Measuring = true;
            for (var frame = 0; frame < 60; frame++)
            {
                _world.EntityManager.SetComponentData(_host, frame % 10 < 5 ? lit : default(UiSwitches));
                _world.EntityManager.SetComponentData(_host, frame % 20 < 15 ? BoxOpacity(frame / 60f) : default(UiChannels));
                yield return null;
            }
            for (var idle = 0; idle < 30; idle++)
                yield return null;
            windowEnd.Measuring = false;
            var uiCodeAllocated = windowEnd.UiCodeAllocatedBytes;
            var allocatingFrames = string.Join(" ", windowEnd.BytesPerMeasuredFrame.Take(windowEnd.MeasuredFrames).Select((bytes, frame) => (bytes, frame)).Where(entry => entry.bytes != 0).Select(entry => entry.frame + ":" + entry.bytes));
            Debug.Log("Tl UI panel allocation: " + uiCodeAllocated + " B over " + windowEnd.MeasuredFrames + " frames; allocating frames " + allocatingFrames);
            Object.Destroy(probes);
            Assert.AreEqual(0L, uiCodeAllocated, "switches flipping every 5 frames, a channel moving every frame and turning off every 20, then idle");
        }

        static long BytesAllocatedBy(System.Action uiToolkitCall)
        {
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            uiToolkitCall();
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            return allocated;
        }

        [UnityTest]
        public IEnumerator EveryUiToolkitCallThePanelMakesAllocatesNothingOnceWarm()
        {
            yield return BuildScreenBoundToATestWorld();
            var root = _panel.Root;
            var bar = new ProgressBar { lowValue = 0f, highValue = 1f };
            var first = new Button { text = "first" };
            var second = new Button { text = "second" };
            var holder = new VisualElement();
            var attached = ScriptableObject.CreateInstance<VisualTreeAsset>().Instantiate();
            var caption = new Label("caption");
            root.Add(bar);
            root.Add(first);
            root.Add(second);
            root.Add(holder);
            root.Add(caption);
            yield return null;
            var names = new[] { "progress value", "focus", "attach", "detach", "set enabled", "label text", "display flex", "display null" };
            var warmBytes = new long[names.Length];
            for (var round = 0; round < 12; round++)
            {
                var even = round % 2 == 0;
                var bytes = new long[names.Length];
                bytes[0] = BytesAllocatedBy(() => bar.SetValueWithoutNotify(even ? 0.25f : 0.75f));
                yield return null;
                bytes[1] = BytesAllocatedBy(() => (even ? first : second).Focus());
                yield return null;
                bytes[2] = BytesAllocatedBy(() => holder.Add(attached));
                yield return null;
                bytes[3] = BytesAllocatedBy(() => attached.RemoveFromHierarchy());
                yield return null;
                bytes[4] = BytesAllocatedBy(() => first.SetEnabled(!even));
                yield return null;
                bytes[5] = BytesAllocatedBy(() => caption.text = even ? "first text" : "second text");
                yield return null;
                bytes[6] = BytesAllocatedBy(() => caption.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex));
                yield return null;
                bytes[7] = BytesAllocatedBy(() => caption.style.display = new StyleEnum<DisplayStyle>(StyleKeyword.Null));
                yield return null;
                if (round >= 4)
                    for (var call = 0; call < names.Length; call++)
                        warmBytes[call] += bytes[call];
            }
            var report = string.Join(", ", names.Select((name, call) => name + " " + warmBytes[call]));
            Debug.Log("Tl UI Toolkit calls, bytes over 8 warm rounds: " + report);
            Assert.AreEqual(0L, warmBytes.Sum(), report);
        }
    }
}
