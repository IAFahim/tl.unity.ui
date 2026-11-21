#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tl.Generated;
using Tl.Ui;
using Tl.Unity;
using TlUi;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace BasicsSample.Tests
{
    public class BasicsTests
    {
        sealed class TickWindow
        {
            public int FirstTick = -1;
            public int LastTick = -1;

            public void SeenAt(int tick)
            {
                if (FirstTick < 0)
                    FirstTick = tick;
                LastTick = tick;
            }
        }

        static (int StartTick, int EndTick) ClipTicks<TData>(TimelineAsset timeline, double ticksPerSecond, Func<TData, bool> isTheOne) where TData : TlClipData
        {
            foreach (var track in timeline.GetOutputTracks())
                foreach (var clip in track.GetClips())
                    if (clip.asset is TlClip { Data: TData data } && isTheOne(data))
                        return ((int)Math.Round(clip.start * ticksPerSecond), (int)Math.Round(clip.end * ticksPerSecond));
            throw new AssertionException("the Basics timeline has no " + typeof(TData).Name + " clip like that");
        }

        static void AssertWindow(string what, TickWindow seen, (int StartTick, int EndTick) clip)
        {
            Debug.Log("Basics: " + what + " clip ticks " + clip.StartTick + " to " + clip.EndTick + ", seen " + seen.FirstTick + " to " + seen.LastTick);
            Assert.That(seen.FirstTick, Is.InRange(clip.StartTick - 1, clip.StartTick + 1), what + " starts with its clip");
            Assert.That(seen.LastTick, Is.InRange(clip.EndTick - 2, clip.EndTick), what + " ends with its clip");
        }

        static bool ABadgeIsShowingIn(VisualElement holder) => holder.Children().OfType<TemplateContainer>().Any(template => template.resolvedStyle.display != DisplayStyle.None);

        static int PlayingTick(EntityManager entityManager, Entity host)
        {
            foreach (var playback in entityManager.GetBuffer<Playback>(host, true))
                if (playback.Timeline != 0 && (playback.Flags & Tl.PlaybackFlags.Ended) == 0)
                    return playback.Tick;
            return -1;
        }

        sealed class PlayMeasurement
        {
            public bool Replayed;
            public long UiCodeBytes;
            public long LargestUiCodeFrame;
            public int Frames;
            public long WholeFrameBytes;
            public readonly List<string> AllocatingTicks = new List<string>();

            public override string ToString() => "UI code allocated " + UiCodeBytes + " B over " + Frames + " frames (largest frame " + LargestUiCodeFrame
                + " B) at ticks [" + string.Join(" ", AllocatingTicks) + "]; whole main-thread frames allocated " + WholeFrameBytes + " B";
        }

        static IEnumerator ReplayAndMeasure(EntityManager entityManager, Entity host, UiAllocationWindowEnd windowEnd, PlayMeasurement measurement)
        {
            var basics = entityManager.GetBuffer<UiTimelineSlot>(host, true)[0].Timeline;
            var requests = entityManager.GetBuffer<TimelineRequest>(host);
            Tl.TlRequests.Stop(requests, basics);
            Tl.TlRequests.Start(requests, basics);
            yield return null;
            windowEnd.UiCodeAllocatedBytes = 0;
            windowEnd.LargestUiCodeFrame = 0;
            windowEnd.MeasuredFrames = 0;
            windowEnd.Measuring = true;
            var replayDeadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < replayDeadline)
            {
                var frameStart = GC.GetAllocatedBytesForCurrentThread();
                yield return null;
                measurement.WholeFrameBytes += GC.GetAllocatedBytesForCurrentThread() - frameStart;
                var tick = PlayingTick(entityManager, host);
                if (windowEnd.LastFrameBytes != 0)
                    measurement.AllocatingTicks.Add(tick + ":" + windowEnd.LastFrameBytes);
                measurement.Replayed |= tick >= 0;
                if (measurement.Replayed && tick < 0)
                    break;
            }
            for (var idle = 0; idle < 30; idle++)
                yield return null;
            windowEnd.Measuring = false;
            measurement.UiCodeBytes = windowEnd.UiCodeAllocatedBytes;
            measurement.LargestUiCodeFrame = windowEnd.LargestUiCodeFrame;
            measurement.Frames = windowEnd.MeasuredFrames;
        }

        [UnityTest]
        public IEnumerator EveryBlockFollowsItsClipAndTheUiCodeAllocatesNothingOnASecondPlay()
        {
            var scenePath = EditorBuildSettings.scenes.Select(scene => scene.path).FirstOrDefault(path => path.EndsWith("/Basics.unity"));
            Assert.IsNotNull(scenePath, "Run Tl UI > Samples > Build Basics first.");
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(scenePath.Replace("/Basics.unity", "/Basics.playable"));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var uiPanel = UnityEngine.Object.FindAnyObjectByType<TlUiPanel>();
            var signalLight = Resources.FindObjectsOfTypeAll<Light>().First(light => light.name == "Signal Light").gameObject;
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            using var hostQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiSwitches>(), ComponentType.ReadOnly<Playback>(), ComponentType.ReadOnly<UiTimelineSlot>());
            var loadDeadline = Time.realtimeSinceStartup + 120f;
            while ((hostQuery.IsEmpty || uiPanel.Elements.Count < 7) && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            Assert.IsFalse(hostQuery.IsEmpty, "the UI Sequence entity appears once the SubScene loads");
            Assert.AreEqual(7, uiPanel.Elements.Count, "the seven targets resolve to seven elements");
            var host = hostQuery.GetSingletonEntity();
            var ticksPerSecond = 1.0 / world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep;

            var root = uiPanel.Root;
            var title = root.Q("title");
            var hint = root.Q("hint");
            var go = root.Q<Button>("go");
            var badgeHolder = root.Q("badgeHolder");
            var fader = root.Q("fader");
            var caption = root.Q<Label>("caption");
            var progress = root.Q<ProgressBar>("progress");
            var authoredCaption = caption.text;
            var slotCaption = uiPanel.TextSlots[0];

            var titleShown = new TickWindow();
            var hintHidden = new TickWindow();
            var goLocked = new TickWindow();
            var badgeGlowing = new TickWindow();
            var badgeAttached = new TickWindow();
            var captionSwapped = new TickWindow();
            var signalLit = new TickWindow();
            var goFocused = new TickWindow();
            var fadeErrors = 0;
            var progressErrors = 0;
            var revealErrors = 0;
            var playDeadline = Time.realtimeSinceStartup + 30f;
            var started = false;
            while (Time.realtimeSinceStartup < playDeadline)
            {
                yield return null;
                var tick = PlayingTick(entityManager, host);
                started |= tick >= 0;
                if (started && tick < 0)
                    break;
                if (tick < 0)
                    continue;
                if (title.resolvedStyle.display != DisplayStyle.None)
                    titleShown.SeenAt(tick);
                if (hint.resolvedStyle.display == DisplayStyle.None)
                    hintHidden.SeenAt(tick);
                if (!go.enabledInHierarchy)
                    goLocked.SeenAt(tick);
                if (badgeHolder.ClassListContains("is-glowing"))
                    badgeGlowing.SeenAt(tick);
                if (ABadgeIsShowingIn(badgeHolder))
                    badgeAttached.SeenAt(tick);
                if (caption.text != authoredCaption)
                    captionSwapped.SeenAt(tick);
                if (signalLight.activeSelf)
                    signalLit.SeenAt(tick);
                if (go.panel.focusController.focusedElement == go)
                    goFocused.SeenAt(tick);
                if (tick < 60 && Mathf.Abs(fader.resolvedStyle.opacity - tick / 60f) > 0.05f || tick >= 60 && fader.resolvedStyle.opacity < 0.99f)
                    fadeErrors++;
                if (Mathf.Abs(progress.value - Mathf.Min(1f, tick / 180f)) > 0.02f)
                    progressErrors++;
                if (tick >= 120 && tick < 180)
                {
                    var expectedLength = tick < 150 ? UiRules.RevealedLength(slotCaption.Length, (tick - 120) / 30f) : slotCaption.Length;
                    if (!slotCaption.StartsWith(caption.text) || Mathf.Abs(caption.text.Length - expectedLength) > 2)
                        revealErrors++;
                }
            }
            for (var settle = 0; settle < 10; settle++)
                yield return null;
            Debug.Log("Basics: fade errors " + fadeErrors + ", progress errors " + progressErrors + ", reveal errors " + revealErrors + ", focus first tick " + goFocused.FirstTick + ", signal first tick " + signalLit.FirstTick);
            Debug.Log("Basics dump after the first play:\n" + uiPanel.Dump());

            Assert.IsTrue(started, "the Basics timeline plays");
            AssertWindow("title shown", titleShown, ClipTicks<Tl_Ui_ShowClip_TlData>(timeline, ticksPerSecond, data => data.Id == 0));
            AssertWindow("hint hidden", hintHidden, ClipTicks<Tl_Ui_HideClip_TlData>(timeline, ticksPerSecond, data => data.Id == 1));
            AssertWindow("go locked", goLocked, ClipTicks<Tl_Ui_DisableClip_TlData>(timeline, ticksPerSecond, data => data.Id == 2));
            AssertWindow("badge holder glowing", badgeGlowing, ClipTicks<Tl_Ui_AddClassClip_TlData>(timeline, ticksPerSecond, data => data.Id == 3));
            AssertWindow("badge attached", badgeAttached, ClipTicks<Tl_Ui_AttachClip_TlData>(timeline, ticksPerSecond, data => data.Id == 3));
            AssertWindow("caption swapped", captionSwapped, ClipTicks<Tl_Ui_TextClip_TlData>(timeline, ticksPerSecond, data => data.Channel == 1));
            var pingStart = ClipTicks<Tl_Ui_SignalClip_TlData>(timeline, ticksPerSecond, data => data.Code == 1).StartTick;
            Assert.That(signalLit.FirstTick, Is.InRange(pingStart - 1, pingStart + 1), "the Ping signal turns the light on when its clip starts");
            var focusStart = ClipTicks<Tl_Ui_FocusClip_TlData>(timeline, ticksPerSecond, data => data.Id == 2).StartTick;
            Assert.That(goFocused.FirstTick, Is.InRange(focusStart - 1, focusStart + 1), "the Focus clip focuses the Go button when it starts");
            Assert.AreEqual(0, fadeErrors, "the fader's opacity follows its curve, then holds 1");
            Assert.AreEqual(0, progressErrors, "the progress bar follows its curve, then holds 1");
            Assert.AreEqual(0, revealErrors, "the caption shows the text slot revealed by its curve");
            Assert.AreEqual(DisplayStyle.None, title.resolvedStyle.display, "after its clip the title is back to its authored USS: hidden");
            Assert.AreEqual(authoredCaption, caption.text, "after its clip the caption is back to its UXML text");
            Assert.IsFalse(ABadgeIsShowingIn(badgeHolder), "after its clip the badge is no longer shown");

            var probes = new GameObject("Allocation probes");
            var windowStart = probes.AddComponent<UiAllocationWindowStart>();
            var windowEnd = probes.AddComponent<UiAllocationWindowEnd>();
            windowEnd.WindowStart = windowStart;
            var secondPlay = new PlayMeasurement();
            yield return ReplayAndMeasure(entityManager, host, windowEnd, secondPlay);
            var thirdPlay = new PlayMeasurement();
            yield return ReplayAndMeasure(entityManager, host, windowEnd, thirdPlay);
            Debug.Log("Basics second play: " + secondPlay);
            Debug.Log("Basics third play: " + thirdPlay);
            Assert.IsTrue(secondPlay.Replayed && thirdPlay.Replayed, "the restarted Basics timeline plays again, twice");
            Assert.AreEqual(0L, thirdPlay.UiCodeBytes, "once every path ran, Tl UI Panel and Tl UI Data allocate nothing per frame while playing and while idle");
        }
    }
}
#endif
