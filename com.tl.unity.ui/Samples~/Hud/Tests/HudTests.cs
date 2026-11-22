#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using HudSample;
using NUnit.Framework;
using TlUi;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HudSample.Tests
{
    public class HudTests
    {
        static string ClockOf(float seconds)
        {
            var wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (wholeSeconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (wholeSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
                yield return null;
        }

        static void Click(VisualElement button)
        {
            using var click = ClickEvent.GetPooled();
            click.target = button;
            button.SendEvent(click);
        }

        [UnityTest]
        public IEnumerator BindingsShowTheBurstWrittenSingletonAndButtonsReachGameSystemsAndTimelines()
        {
            var scenePath = EditorBuildSettings.scenes.Select(scene => scene.path).FirstOrDefault(path => path.EndsWith("/Hud.unity"));
            Assert.IsNotNull(scenePath, "Run Tl UI > Samples > Build Hud first.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var uiPanel = UnityEngine.Object.FindAnyObjectByType<TlUiPanel>();
            var hudView = UnityEngine.Object.FindAnyObjectByType<HudView>();
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var hudQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<HudData>(), ComponentType.ReadWrite<HudRules>());
            var loadDeadline = Time.realtimeSinceStartup + 120f;
            while ((hudQuery.IsEmpty || uiPanel.Root == null || hudView.Version == 0) && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            Assert.IsFalse(hudQuery.IsEmpty, "the Hud Demo object bakes the HudData singleton");
            var hud = hudQuery.GetSingletonEntity();
            var root = uiPanel.Root;
            var health = root.Q<Label>("health");
            var coins = root.Q<Label>("coins");
            var clock = root.Q<Label>("clock");
            var healthBar = root.Q<ProgressBar>("healthBar");
            var lowHealth = root.Q("lowHealth");
            var flashBanner = root.Q("flashBanner");

            yield return Frames(30);
            var rules = entityManager.GetComponentData<HudRules>(hud);
            rules.Paused = true;
            entityManager.SetComponentData(hud, rules);
            yield return Frames(5);
            var shown = entityManager.GetComponentData<HudData>(hud);
            var settleDeadline = Time.realtimeSinceStartup + 3f;
            while ((coins.text != shown.Coins.ToString(CultureInfo.InvariantCulture) || Mathf.Abs(healthBar.value - shown.HealthFraction) > 0.0001f) && Time.realtimeSinceStartup < settleDeadline)
                yield return null;
            Debug.Log("Hud paused at health " + shown.Health + ", coins " + shown.Coins + ", seconds " + shown.Seconds + "; labels " + health.text + " " + coins.text + " " + clock.text + ", bar " + healthBar.value);
            Assert.AreEqual(Mathf.RoundToInt(shown.Health).ToString(CultureInfo.InvariantCulture), health.text, "the health label binds Health through tl-integer");
            Assert.AreEqual(shown.Coins.ToString(CultureInfo.InvariantCulture), coins.text, "the coins label rolls to Coins through Tl Smooth Binding");
            Assert.AreEqual(ClockOf(shown.Seconds), clock.text, "the clock binds Seconds through tl-clock");
            Assert.AreEqual(shown.HealthFraction, healthBar.value, 0.0001f, "the bar eases to HealthFraction through Tl Smooth Binding");
            Assert.AreEqual(shown.HealthFraction, root.Q<TlRadialFill>("healthRing").value, 0.0001f, "the Tl Radial Fill ring binds HealthFraction through its value property");
            Assert.AreEqual(DisplayStyle.None, lowHealth.resolvedStyle.display, "the low-health banner is hidden while health is high");

            shown.Health = 10f;
            entityManager.SetComponentData(hud, shown);
            yield return Frames(5);
            Assert.AreEqual("10", health.text);
            Assert.AreEqual(DisplayStyle.Flex, lowHealth.resolvedStyle.display, "the low-health banner binds LowHealth through tl-visible");
            var pulseSeen = false;
            var pulseDeadline = Time.realtimeSinceStartup + 3f;
            while (!pulseSeen && Time.realtimeSinceStartup < pulseDeadline)
            {
                yield return null;
                pulseSeen = lowHealth.ClassListContains("pulse");
            }
            Assert.IsTrue(pulseSeen, "Tl Trigger Binding on LowHealth plays the looping Low Health Pulse timeline, whose Add Class clip pulses the banner");

            Assert.IsTrue(root.Q<Button>("heal").HasComponent<TlSend>(), "Hud.uxml authors a Send component on the Heal button");
            Assert.AreEqual(1, root.Q<Button>("heal").GetComponent<TlSend>().Code, "the UXML attribute code=1 reaches the Send component");
            Assert.AreEqual(1, root.Q<Button>("flash").GetComponent<TlPlay>().Timeline, "Hud.uxml authors Play timeline 1 on the Flash button");
            Assert.AreEqual(0, uiPanel.Targets.Count + uiPanel.Commands.Count, "the Hud screen needs no selector or command table: Target, Send and Play are authored in UXML");
            Click(root.Q<Button>("heal"));
            yield return Frames(5);
            var healed = entityManager.GetComponentData<HudData>(hud);
            Assert.AreEqual(10f + rules.HealAmount, healed.Health, 0.001f, "the Heal button sends code 1 to the inbox and the Burst system heals");
            Assert.AreEqual(Mathf.RoundToInt(healed.Health).ToString(CultureInfo.InvariantCulture), health.text);
            var pulseStopDeadline = Time.realtimeSinceStartup + 2f;
            while (lowHealth.ClassListContains("pulse") && Time.realtimeSinceStartup < pulseStopDeadline)
                yield return null;
            yield return Frames(60);
            Assert.IsFalse(lowHealth.ClassListContains("pulse"), "healing out of low health stops the pulse timeline on the falling edge");

            Assert.AreEqual(DisplayStyle.None, flashBanner.resolvedStyle.display);
            Click(root.Q<Button>("flash"));
            var flashSeen = false;
            var flashDeadline = Time.realtimeSinceStartup + 5f;
            while (!flashSeen && Time.realtimeSinceStartup < flashDeadline)
            {
                yield return null;
                flashSeen = flashBanner.resolvedStyle.display != DisplayStyle.None;
            }
            Assert.IsTrue(flashSeen, "the Flash button plays the Hud Flash timeline from Also Plays, which shows the flash banner");
            var hideDeadline = Time.realtimeSinceStartup + 5f;
            while (flashBanner.resolvedStyle.display != DisplayStyle.None && Time.realtimeSinceStartup < hideDeadline)
                yield return null;
            Assert.AreEqual(DisplayStyle.None, flashBanner.resolvedStyle.display, "the banner hides when the Flash timeline ends");

            var probes = new GameObject("Allocation probes");
            var windowStart = probes.AddComponent<HudAllocationWindowStart>();
            var windowEnd = probes.AddComponent<HudAllocationWindowEnd>();
            windowEnd.WindowStart = windowStart;
            rules.Paused = false;
            entityManager.SetComponentData(hud, rules);
            yield return Frames(60);
            var versionBefore = hudView.Version;
            Assert.IsTrue(UiToolkitTickProbe.Install(), "the probe finds UI Toolkit's panel update in the player loop");
            UiToolkitTickProbe.Reset();
            UiToolkitTickProbe.Measuring = true;
            windowEnd.Measuring = true;
            long wholeFramesWhileChanging = 0;
            for (var frame = 0; frame < 240; frame++)
            {
                var frameStart = GC.GetAllocatedBytesForCurrentThread();
                yield return null;
                wholeFramesWhileChanging += GC.GetAllocatedBytesForCurrentThread() - frameStart;
            }
            windowEnd.Measuring = false;
            UiToolkitTickProbe.Measuring = false;
            var uiToolkitBytes = UiToolkitTickProbe.AllocatedBytes;
            var uiToolkitTicks = UiToolkitTickProbe.Ticks;
            var uiToolkitLargest = UiToolkitTickProbe.LargestTick;
            UiToolkitTickProbe.Uninstall();
            var changes = hudView.Version - versionBefore;
            rules.Paused = true;
            entityManager.SetComponentData(hud, rules);
            yield return Frames(10);
            long wholeFramesWhilePaused = 0;
            for (var frame = 0; frame < 240; frame++)
            {
                var frameStart = GC.GetAllocatedBytesForCurrentThread();
                yield return null;
                wholeFramesWhilePaused += GC.GetAllocatedBytesForCurrentThread() - frameStart;
            }
            var uiCodeAllocated = windowEnd.UiCodeAllocatedBytes;
            Debug.Log("Hud allocation: UI code " + uiCodeAllocated + " B over " + windowEnd.MeasuredFrames + " frames with " + changes + " data changes; UI Toolkit panel update " + uiToolkitBytes + " B over " + uiToolkitTicks + " ticks (largest " + uiToolkitLargest + " B); whole main-thread frames " + wholeFramesWhileChanging + " B while the data changes every frame, " + wholeFramesWhilePaused + " B while paused");
            Assert.Greater(changes, 200, "the singleton changes almost every frame while unpaused");
            Assert.AreEqual(0L, uiCodeAllocated, "Tl UI Data and Tl UI Panel allocate nothing while the singleton changes every frame");
            Assert.AreEqual(0L, uiToolkitBytes, "UI Toolkit's panel update, with its bindings, Tl Smooth and Trigger Bindings and the radial fill, allocates nothing either");
        }
    }
}
#endif
