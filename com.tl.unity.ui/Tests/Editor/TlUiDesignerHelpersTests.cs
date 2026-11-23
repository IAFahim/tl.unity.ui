using System.Linq;
using NUnit.Framework;
using Tl.Generated;
using Tl.Ui;
using Tl.Unity;
using TlUi;
using TlUi.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TlUiPackage.EditorTests
{
    public class TlUiDesignerHelpersTests
    {
        const string HelpersFolder = "Assets/Tl UI Designer Helpers Test";

        static TlUiScreenMenu.Created ANewScreen()
        {
            var screenScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(HelpersFolder))
                AssetDatabase.CreateFolder("Assets", "Tl UI Designer Helpers Test");
            EditorSceneManager.SaveScene(screenScene, HelpersFolder + "/Helpers.unity");
            var created = TlUiScreenMenu.Create(HelpersFolder);
            EditorSceneManager.SaveScene(screenScene);
            TlUiScreens.ForgetWhatWasFound();
            return created;
        }

        [TearDown]
        public void ForgetTheScreen()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(HelpersFolder);
            TlUiScreens.ForgetWhatWasFound();
        }

        [Test]
        public void TheTimelineButtonOpensTheClosedScreenTimelinesAndANewTimelineBecomesTheNextSlot()
        {
            var created = ANewScreen();
            Assert.IsFalse(created.Timelines.EditingScene.isLoaded, "a new screen's Screen Timelines starts closed");
            CollectionAssert.AreEqual(new[] { created.Timelines }, TlUiScreens.ClosedScreenTimelines(), "the closed SubScene is known to hold a Tl UI Timelines");
            Assert.AreEqual(created.Timelines, TlUiScreens.ClosedSubSceneThatPlays(created.Timeline), "and to play the screen's timeline");
            Assert.IsNull(TlUiTargets.TimelinesObjectInOpenScenes());

            var director = TlUiScreens.ShowInTheTimelineWindow(created.Timeline);
            Assert.IsNotNull(director, "the Timeline button finds the director");
            Assert.IsTrue(created.Timelines.EditingScene.isLoaded, "after opening Screen Timelines for editing by itself");
            var timelinesObject = director.GetComponent<TlUiTimelines>();
            Assert.AreEqual(timelinesObject, TlUiScreens.TimelinesObjectFor(created.Timeline), "element names are found for the screen's timeline with nothing selected");
            CollectionAssert.AreEqual(new[] { created.Timeline }, TlUiScreens.Slots(timelinesObject));

            var slot = TlUiScreens.AddTimeline(timelinesObject, "play clicked");
            Assert.AreEqual(1, slot, "New timeline… makes slot 1");
            var playClicked = TlUiScreens.Slots(timelinesObject)[1];
            Assert.AreEqual(HelpersFolder + "/play clicked.playable", AssetDatabase.GetAssetPath(playClicked), "next to the screen's timeline, named as typed");
            var alsoPlays = director.GetComponent<TlAlsoPlays>();
            CollectionAssert.AreEqual(new[] { playClicked }, alsoPlays.Timelines, "listed in Also Plays, which the baker numbers from slot 1");
            CollectionAssert.IsEmpty(alsoPlays.PlayOnAwake, "and not started by itself");
            Assert.IsFalse(created.Timelines.EditingScene.isDirty, "Screen Timelines is saved, so the new slot is kept");
            Assert.AreEqual(timelinesObject, TlUiScreens.TimelinesObjectFor(playClicked), "a clip on the new timeline gets element names too");
            Assert.AreEqual(1, TlUiScreens.AddExistingTimeline(timelinesObject, playClicked), "adding it again keeps its slot");
            Assert.AreEqual("Screen Timeline (the main timeline)", TlUiTimelineSlotDrawer.LabelOfSlot(TlUiScreens.Slots(timelinesObject), 0));
            Assert.AreEqual("play clicked", TlUiTimelineSlotDrawer.LabelOfSlot(TlUiScreens.Slots(timelinesObject), 1));
            Assert.IsNull(TlUiTimelineSlotDrawer.LabelOfSlot(TlUiScreens.Slots(timelinesObject), 2));

            var mainTimeline = director.playableAsset;
            EditorSceneManager.CloseScene(created.Timelines.EditingScene, true);
            TlUiScreens.ForgetWhatWasFound();
            Assert.AreEqual(created.Timelines, TlUiScreens.ClosedSubSceneThatPlays(playClicked), "double-clicking the new timeline later finds its closed Screen Timelines");
            Assert.AreEqual(mainTimeline, TlUiScreens.ShowInTheTimelineWindow(playClicked).playableAsset, "and opens it again");
        }

        [Test]
        public void ATimelineSlotForACommandSuggestsANameFromTheElementAndTheEvent()
        {
            var created = ANewScreen();
            var timelinesObject = TlUiScreens.FindOrOpenTimelinesObject();
            Assert.IsNotNull(timelinesObject, "the slot picker opens Screen Timelines when it is closed");
            var panel = created.Screen.GetComponent<TlUiPanel>();
            panel.Commands.Add(new UiCommand { TargetId = TlUiTargets.IdForName(timelinesObject, "play"), On = UiCommandEvent.Click, Do = UiCommandAction.RestartTimeline });
            panel.Signals.Add(new UiSignalReaction { Code = 3 });
            var serializedPanel = new SerializedObject(panel);
            Assert.AreEqual("play clicked", TlUiTimelineSlotDrawer.SuggestedNameFor(serializedPanel.FindProperty("Commands.Array.data[0].TimelineSlot"), timelinesObject));
            Assert.AreEqual("Signal 3", TlUiTimelineSlotDrawer.SuggestedNameFor(serializedPanel.FindProperty("Signals.Array.data[0].TimelineSlot"), timelinesObject));
            Assert.IsTrue(TlUiTimelineSlotDrawer.UsesATimeline(serializedPanel.FindProperty("Commands.Array.data[0].TimelineSlot")), "a Restart Timeline row picks a timeline");
            Assert.IsFalse(TlUiTimelineSlotDrawer.UsesATimeline(serializedPanel.FindProperty("Signals.Array.data[0].TimelineSlot")), "an Invoke Event row shows its Timeline field as not used");
            Assert.IsNotNull(typeof(UiCommand).GetField(nameof(UiCommand.TimelineSlot)).GetCustomAttributes(typeof(TlUiTimelineSlotAttribute), false).FirstOrDefault(), "Commands rows pick their timeline by name");
            Assert.IsNotNull(typeof(UiSignalReaction).GetField(nameof(UiSignalReaction.TimelineSlot)).GetCustomAttributes(typeof(TlUiTimelineSlotAttribute), false).FirstOrDefault(), "and so do Signals rows");
        }

        [Test]
        public void APresetSetsWhatAnAnimateClipMovesHowAndForHowLong()
        {
            var created = ANewScreen();
            var timelinesObject = TlUiScreens.ShowInTheTimelineWindow(created.Timeline).GetComponent<TlUiTimelines>();
            var track = created.Timeline.CreateTrack<TlTrack>(null, "Animate");
            track.Template = new Tl_Ui_AnimateClip_TlData();
            var clip = track.CreateClip<TlClip>();
            var clipAsset = (TlClip)clip.asset;
            var data = (Tl_Ui_AnimateClip_TlData)clipAsset.Data;
            Assert.IsTrue(TlUiAnimatePresets.IsUntouched(data.From, data.To, data.Ease), "a new Animate clip starts at From 0, To 0, with Tl's starting Ease");
            Assert.IsTrue(TlUiAnimatePresets.RunsBackwards(data.Ease), "whose curve goes downhill, so From 0 To 1 would play 1 → 0: the Inspector turns it uphill");
            Assert.IsFalse(TlUiAnimatePresets.RunsBackwards(TlUiAnimatePresets.FastThenSlow()));
            Assert.IsTrue(TlUiTargets.Assign(new[] { clip }, timelinesObject, "hello"));
            Assert.AreEqual("Fade hello", clip.displayName);

            TlUiAnimatePresets.Apply(clipAsset, "Data.", TlUiAnimatePresets.All.First(preset => preset.Menu == "Appear/Slide in from the left"), clip, timelinesObject);
            data = (Tl_Ui_AnimateClip_TlData)clipAsset.Data;
            Assert.AreEqual(UiProperty.MoveX, data.Property, "Slide in from the left moves X");
            Assert.AreEqual(-400f, data.From);
            Assert.AreEqual(0f, data.To, "and ends where the layout put it");
            Assert.IsTrue(data.Hold, "and stays there");
            Assert.AreEqual(0f, data.Ease.Evaluate(0f), 0.001f);
            Assert.AreEqual(1f, data.Ease.Evaluate(1f), 0.001f, "Ease goes uphill, From → To");
            Assert.AreEqual(0.5, clip.duration, 1e-6, "the preset sets the clip's length");
            Assert.AreEqual("Move hello X", clip.displayName, "and the clip renames itself");

            TlUiAnimatePresets.Apply(clipAsset, "Data.", TlUiAnimatePresets.All.First(preset => preset.Menu == "Attention/Pop (grow and back)"), clip, timelinesObject);
            data = (Tl_Ui_AnimateClip_TlData)clipAsset.Data;
            Assert.AreEqual(UiProperty.Scale, data.Property);
            Assert.IsFalse(data.Hold, "a pop goes back to the designed size");
            Assert.AreEqual(1f, data.Ease.Evaluate(0.5f), 0.05f, "it peaks at To halfway");
            Assert.AreEqual(0f, data.Ease.Evaluate(1f), 0.001f, "and ends back at From");
            Assert.AreEqual("Scale hello", clip.displayName);

            TlUiAnimatePresets.SwapFromAndTo(clipAsset, "Data.");
            data = (Tl_Ui_AnimateClip_TlData)clipAsset.Data;
            Assert.AreEqual(1.2f, data.From, 1e-5f, "Swap From and To swaps them");
            Assert.AreEqual(1f, data.To, 1e-5f);

            foreach (var preset in TlUiAnimatePresets.All)
            {
                Assert.AreNotEqual(preset.From, preset.To, preset.Menu + " moves something");
                Assert.Greater(preset.Seconds, 0.0, preset.Menu + " has a length");
            }
        }
    }
}
