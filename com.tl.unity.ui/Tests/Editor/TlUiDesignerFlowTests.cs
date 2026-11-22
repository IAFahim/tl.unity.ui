using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tl.Generated;
using Tl.Unity;
using Tl.Unity.Editor;
using TlUi;
using TlUi.Editor;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace TlUiPackage.EditorTests
{
    public class TlUiDesignerFlowTests
    {
        const string FlowFolder = "Assets/Tl UI Designer Flow Test";

        static (string Menu, System.Type Data)[] Blocks => new[]
        {
            ("UI/Show", typeof(Tl_Ui_ShowClip_TlData)),
            ("UI/Hide", typeof(Tl_Ui_HideClip_TlData)),
            ("UI/Enable", typeof(Tl_Ui_EnableClip_TlData)),
            ("UI/Disable", typeof(Tl_Ui_DisableClip_TlData)),
            ("UI/Add Class", typeof(Tl_Ui_AddClassClip_TlData)),
            ("UI/Remove Class", typeof(Tl_Ui_RemoveClassClip_TlData)),
            ("UI/Attach", typeof(Tl_Ui_AttachClip_TlData)),
            ("UI/Animate", typeof(Tl_Ui_AnimateClip_TlData)),
            ("UI/Text", typeof(Tl_Ui_TextClip_TlData)),
            ("UI/Signal", typeof(Tl_Ui_SignalClip_TlData)),
            ("UI/Focus", typeof(Tl_Ui_FocusClip_TlData)),
        };

        static System.Type TimelineEditorInternal(string name) => typeof(TimelineEditor).Assembly.GetType("UnityEditor.Timeline." + name, true);

        static List<System.Type> TracksInThePlusMenu()
        {
            var typeUtility = TimelineEditorInternal("TypeUtility");
            var allTracks = (IEnumerable<System.Type>)typeUtility.GetMethod("AllTrackTypes", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            var isHidden = typeUtility.GetMethod("IsHiddenInMenu", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(System.Type) }, null);
            return allTracks.Where(track => !(bool)isHidden.Invoke(null, new object[] { track })).ToList();
        }

        static TlTrack AddFromThePlusMenu(TimelineAsset timeline, System.Type menuTrack)
        {
            var createTrack = TimelineEditorInternal("TimelineHelpers").GetMethod("CreateTrack", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(TimelineAsset), typeof(System.Type), typeof(TrackAsset), typeof(string) }, null);
            createTrack.Invoke(null, new object[] { timeline, menuTrack, null, null });
            return timeline.GetRootTracks().Last() as TlTrack;
        }

        static string MenuPathOf(System.Type track) => track.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

        static System.Type MenuTrack(string menuPath)
        {
            var menuTrack = TracksInThePlusMenu().FirstOrDefault(track => MenuPathOf(track) == menuPath);
            Assert.IsNotNull(menuTrack, menuPath + " is in the Timeline + menu");
            return menuTrack;
        }

        static TlUiScreenMenu.Created ScreenWithAHideClip(double start, double duration)
        {
            var flowScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(FlowFolder))
                AssetDatabase.CreateFolder("Assets", "Tl UI Designer Flow Test");
            EditorSceneManager.SaveScene(flowScene, FlowFolder + "/Flow.unity");
            var created = TlUiScreenMenu.Create(FlowFolder);
            Assert.IsNotNull(created.Screen, "GameObject > Tl UI > Screen makes the screen");
            var hideTrack = AddFromThePlusMenu(created.Timeline, MenuTrack("UI/Hide"));
            var hideClip = hideTrack.CreateClip<TlClip>();
            hideClip.start = start;
            hideClip.duration = duration;
            EditorUtility.SetDirty(hideClip.asset);
            EditorUtility.SetDirty(hideTrack);
            EditorUtility.SetDirty(created.Timeline);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(flowScene);
            return created;
        }

        static void ForgetTheFlow()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(FlowFolder);
        }

        [Test]
        public void EveryUiBlockIsInTheTimelinePlusMenuAndAddsATlTrackOfThatBlock()
        {
            var wanted = TlMenuStubFiles.Wanted();
            var plusMenu = TracksInThePlusMenu();
            foreach (var block in Blocks)
            {
                var stub = block.Data.Name.Replace("_TlData", "_TlMenu") + ".cs";
                Assert.IsTrue(wanted.ContainsKey(stub), "com.tl.ui opts in with [assembly: TlMenuEntries], so Tl writes " + stub);
                var menuTrack = plusMenu.FirstOrDefault(track => MenuPathOf(track) == block.Menu);
                Assert.IsNotNull(menuTrack, block.Menu + " is in the Timeline + menu");
                var menuTrackInstance = ScriptableObject.CreateInstance(menuTrack);
                Assert.AreEqual(menuTrack.Name, MonoScript.FromScriptableObject(menuTrackInstance)?.name, block.Menu + " lives in a file named after its class, so Timeline accepts it");
                Object.DestroyImmediate(menuTrackInstance);

                var timeline = TlTimelines.CreateAsset();
                var track = AddFromThePlusMenu(timeline, menuTrack);
                var shortName = block.Menu.Substring("UI/".Length);
                Assert.IsNotNull(track, block.Menu + " turns into a Tl track when picked");
                Assert.AreEqual(1, timeline.GetRootTracks().Count(), "the menu entry itself is not left in the timeline");
                Assert.AreEqual(shortName, track.name);
                Assert.AreEqual(block.Data, track.Template.GetType());
                var clip = track.CreateClip<TlClip>();
                Assert.AreEqual(block.Data, ((TlClip)clip.asset).Data.GetType(), "a clip added on the track is a " + shortName + " clip");
                Assert.AreEqual(shortName, clip.displayName);
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void TheTargetActionNamesAClipsTargetAndAddsNewNamesToTheTimelinesObject()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var timelinesObject = new GameObject("Screen Timelines").AddComponent<TlUiTimelines>();
            timelinesObject.TargetNames.Add("hello");
            var timeline = TlTimelines.CreateAsset();
            var hide = AddFromThePlusMenu(timeline, MenuTrack("UI/Hide")).CreateClip<TlClip>();
            var animate = AddFromThePlusMenu(timeline, MenuTrack("UI/Animate")).CreateClip<TlClip>();
            var action = new TlUiTargetAction();

            Assert.AreEqual(ActionValidity.Valid, action.Validate(new[] { hide }), "Tl UI Target is offered on a Hide clip");
            Assert.AreEqual(ActionValidity.NotApplicable, action.Validate(new[] { animate }), "and not on an Animate clip, which drives a channel, not a target");
            CollectionAssert.Contains(TlUiTargets.NamesToOffer(timelinesObject), "hello");

            Assert.IsTrue(TlUiTargets.Assign(new[] { hide }, timelinesObject, "title"));
            CollectionAssert.AreEqual(new[] { "hello", "title" }, timelinesObject.TargetNames, "a new name is added to the timelines object");
            Assert.AreEqual(1, ((Tl_Ui_HideClip_TlData)((TlClip)hide.asset).Data).Id, "the clip stores the name's position");
            Assert.AreEqual("Hide title", hide.displayName, "the clip shows its target on the timeline");
            Assert.AreEqual("title", TlUiTargets.TargetNameOf(hide, timelinesObject));

            Assert.IsTrue(TlUiTargets.Assign(new[] { hide }, timelinesObject, "hello"));
            Assert.AreEqual(0, ((Tl_Ui_HideClip_TlData)((TlClip)hide.asset).Data).Id, "an existing name keeps its position");
            Assert.AreEqual("Hide hello", hide.displayName);
            Assert.AreEqual(2, timelinesObject.TargetNames.Count);
            Object.DestroyImmediate(timeline);
        }

        [Test]
        public void ANewNameSkipsEveryIdTheWorldAlreadyUsesByNumber()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(FlowFolder))
                AssetDatabase.CreateFolder("Assets", "Tl UI Designer Flow Test");
            var screenPath = FlowFolder + "/Numbered.uxml";
            System.IO.File.WriteAllText(screenPath, "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:tl=\"TlUi\"><ui:Label name=\"title\"><tl:TlTarget id=\"1\" /></ui:Label><ui:Label name=\"coins\" /><ui:Label name=\"lives\" /></ui:UXML>");
            AssetDatabase.ImportAsset(screenPath, ImportAssetOptions.ForceSynchronousImport);
            try
            {
                var screen = new GameObject("Screen");
                var panelRenderer = screen.AddComponent<PanelRenderer>();
                panelRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(screenPath);
                var panel = screen.AddComponent<TlUiPanel>();
                panel.Panel = panelRenderer;
                panel.Targets.Add(new UiTarget { Id = 0, Selector = "#title" });
                panel.Commands.Add(new UiCommand { TargetId = 2, On = UiCommandEvent.Click });
                panel.Channels.Add(new UiChannelBinding { Channel = 0, TargetId = 4 });
                panel.Lists.Add(new UiListBinding { TargetId = 5 });
                panel.Commands.Add(new UiCommand { TargetId = 6, On = UiCommandEvent.HotkeyOnly });

                var timeline = TlTimelines.CreateAsset();
                var numbered = AddFromThePlusMenu(timeline, MenuTrack("UI/Show")).CreateClip<TlClip>();
                ((Tl_Ui_ShowClip_TlData)((TlClip)numbered.asset).Data).Id = 3;
                var hideCoins = AddFromThePlusMenu(timeline, MenuTrack("UI/Hide")).CreateClip<TlClip>();
                var hideLives = AddFromThePlusMenu(timeline, MenuTrack("UI/Hide")).CreateClip<TlClip>();
                var timelinesObject = new GameObject("Screen Timelines").AddComponent<TlUiTimelines>();
                timelinesObject.GetComponent<PlayableDirector>().playableAsset = timeline;

                Assert.AreEqual(0b111111UL, TlUiTargets.IdsUsedByNumber(timelinesObject, new[] { hideCoins }), "Ids 0 to 5 are used: a Targets row, a TlTarget in the UXML, a Commands row, a numbered clip, a Channels row and a Lists row; a hotkey-only command targets nothing");
                Assert.IsTrue(TlUiTargets.Assign(new[] { hideCoins }, timelinesObject, "coins"));
                Assert.AreEqual(6, ((Tl_Ui_HideClip_TlData)((TlClip)hideCoins.asset).Data).Id, "the first new name takes the first Id nothing uses");
                CollectionAssert.AreEqual(new[] { "", "", "", "", "", "", "coins" }, timelinesObject.TargetNames, "the positions in use stay empty, so no name drives them");
                Assert.IsTrue(TlUiTargets.Assign(new[] { hideLives }, timelinesObject, "lives"));
                Assert.AreEqual(7, ((Tl_Ui_HideClip_TlData)((TlClip)hideLives.asset).Data).Id, "the next new name takes the next free Id");
                Assert.IsTrue(TlUiTargets.Assign(new[] { hideLives }, timelinesObject, "coins"));
                Assert.AreEqual(6, ((Tl_Ui_HideClip_TlData)((TlClip)hideLives.asset).Data).Id, "an existing name keeps its Id");
                Assert.AreEqual(3, ((Tl_Ui_ShowClip_TlData)((TlClip)numbered.asset).Data).Id, "naming other clips leaves a numbered clip alone");
                Object.DestroyImmediate(timeline);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(FlowFolder);
            }
        }

        [UnityTest]
        public IEnumerator ScrubbingTheTimelineHidesTheLabelWhileThePlayheadIsOnTheClipAndRestoresItAfter()
        {
            var created = ScreenWithAHideClip(0.5, 1.0);
            var director = TlUiScreenMenu.OpenTimelinesObject(created.Timelines);
            Assert.IsNotNull(director, "the Screen Timelines SubScene opens for editing and holds the director");
            var panel = created.Screen.GetComponent<TlUiPanel>();
            AnimationMode.StartAnimationMode();
            try
            {
                director.time = 1.0;
                director.Evaluate();
                var previewDeadline = Time.realtimeSinceStartup + 20f;
                while ((panel.Root == null || panel.Root.Q<Label>("hello") == null || panel.Root.Q<Label>("hello").resolvedStyle.display != DisplayStyle.None) && Time.realtimeSinceStartup < previewDeadline)
                    yield return null;
                Assert.IsTrue(panel.IsPreviewing, "scrubbing a Tl UI timeline previews every Tl UI Panel in the open scenes");
                var hello = panel.Root.Q<Label>("hello");
                Assert.AreEqual(DisplayStyle.None, hello.resolvedStyle.display, "at 1.0 s the playhead is on the Hide clip, so Hello is hidden in Edit mode");

                director.time = 0.2;
                director.Evaluate();
                yield return null;
                yield return null;
                Assert.AreEqual(DisplayStyle.Flex, hello.resolvedStyle.display, "at 0.2 s, before the clip, Hello shows");

                director.time = 1.4;
                director.Evaluate();
                yield return null;
                yield return null;
                Assert.AreEqual(DisplayStyle.None, hello.resolvedStyle.display, "scrubbing back onto the clip hides it again");

                director.time = 1.8;
                director.Evaluate();
                yield return null;
                yield return null;
                Assert.AreEqual(DisplayStyle.Flex, hello.resolvedStyle.display, "after the clip, Hello shows");

                director.time = 1.0;
                director.Evaluate();
                yield return null;
                yield return null;
                AnimationMode.StopAnimationMode();
                var stopDeadline = Time.realtimeSinceStartup + 5f;
                while (panel.IsPreviewing && Time.realtimeSinceStartup < stopDeadline)
                    yield return null;
                yield return null;
                Assert.IsFalse(panel.IsPreviewing, "leaving Timeline preview stops the UI preview");
                Assert.AreEqual(0, TlUiScrubPreview.PreviewingPanels);
                Assert.AreEqual(DisplayStyle.Flex, hello.resolvedStyle.display, "and Hello is back to what the UXML says");
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                TlUiScrubPreview.StopAll();
            }
            ForgetTheFlow();
        }

        [UnityTest]
        public IEnumerator ADesignerAddsAScreenAndAHideClipAndTheLabelNamedHelloHidesWhilePlaying()
        {
            var created = ScreenWithAHideClip(0.5, 1.0);
            StringAssert.DoesNotContain("TlTarget", System.IO.File.ReadAllText(FlowFolder + "/Screen.uxml"), "the starter screen marks nothing with an Id");
            Assert.IsNotNull(created.Timelines, "the Screen Timelines SubScene exists");

            yield return new EnterPlayMode();

            var panel = Object.FindAnyObjectByType<TlUiPanel>();
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var timelinesQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiTimelineSlot>());
            var loadDeadline = Time.realtimeSinceStartup + 60f;
            while ((panel.Root == null || panel.Root.Q<Label>("hello") == null || timelinesQuery.IsEmpty || panel.TargetNames.Count == 0) && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            var hello = panel.Root.Q<Label>("hello");
            Assert.IsNotNull(hello, "the starter screen has the Hello label");
            CollectionAssert.AreEqual(new[] { "hello" }, panel.TargetNames, "the panel reads the target names Tl UI Timelines baked into ECS");

            var hiddenDeadline = Time.realtimeSinceStartup + 10f;
            while (hello.resolvedStyle.display != DisplayStyle.None && Time.realtimeSinceStartup < hiddenDeadline)
                yield return null;
            Assert.AreEqual(DisplayStyle.None, hello.resolvedStyle.display, "the Hide clip hides the element named hello, the name at Id 0, the clip's default");
            var shownDeadline = Time.realtimeSinceStartup + 5f;
            while (hello.resolvedStyle.display == DisplayStyle.None && Time.realtimeSinceStartup < shownDeadline)
                yield return null;
            Assert.AreEqual(DisplayStyle.Flex, hello.resolvedStyle.display, "the label comes back when the clip ends");
            timelinesQuery.Dispose();

            yield return new ExitPlayMode();
            ForgetTheFlow();
        }
    }
}
