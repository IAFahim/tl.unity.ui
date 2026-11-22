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
using UnityEngine;
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

        static TrackAsset AddFromThePlusMenu(TimelineAsset timeline, System.Type menuTrack)
        {
            var createTrack = TimelineEditorInternal("TimelineHelpers").GetMethod("CreateTrack", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(TimelineAsset), typeof(System.Type), typeof(TrackAsset), typeof(string) }, null);
            createTrack.Invoke(null, new object[] { timeline, menuTrack, null, null });
            return timeline.GetRootTracks().Last();
        }

        static string MenuPathOf(System.Type track) => track.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

        [Test]
        public void EveryUiBlockIsInTheTimelinePlusMenuAndAddsATlTrackOfThatBlock()
        {
            var plusMenu = TracksInThePlusMenu();
            foreach (var block in Blocks)
            {
                var menuTrack = plusMenu.FirstOrDefault(track => MenuPathOf(track) == block.Menu);
                Assert.IsNotNull(menuTrack, block.Menu + " is in the Timeline + menu");
                var menuTrackInstance = ScriptableObject.CreateInstance(menuTrack);
                Assert.AreEqual(menuTrack.Name, MonoScript.FromScriptableObject(menuTrackInstance)?.name, block.Menu + " lives in a file named after its class, so Timeline accepts it");
                Object.DestroyImmediate(menuTrackInstance);

                var timeline = TlTimelines.CreateAsset();
                var track = AddFromThePlusMenu(timeline, menuTrack) as TlTrack;
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

        [UnityTest]
        public IEnumerator ADesignerAddsAScreenAndAHideClipAndTheLabelHidesWhileTheClipIsOn()
        {
            var flowScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(FlowFolder))
                AssetDatabase.CreateFolder("Assets", "Tl UI Designer Flow Test");
            EditorSceneManager.SaveScene(flowScene, FlowFolder + "/Flow.unity");
            var screen = TlUiScreenMenu.Create(FlowFolder, out var timeline);
            Assert.IsNotNull(screen, "GameObject > Tl UI > Screen makes the screen");
            var hideTrack = (TlTrack)AddFromThePlusMenu(timeline, typeof(HideUiMenuTrack));
            var hideClip = hideTrack.CreateClip<TlClip>();
            hideClip.start = 0.5;
            hideClip.duration = 1.0;
            EditorUtility.SetDirty(hideClip.asset);
            EditorUtility.SetDirty(hideTrack);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(flowScene);

            yield return new EnterPlayMode();

            var panel = Object.FindAnyObjectByType<TlUiPanel>();
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var timelinesQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiTimelineSlot>());
            var loadDeadline = Time.realtimeSinceStartup + 60f;
            while ((panel.Root == null || panel.Root.Q<Label>("hello") == null || timelinesQuery.IsEmpty) && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            var hello = panel.Root.Q<Label>("hello");
            Assert.IsNotNull(hello, "the starter screen has the Hello label");
            Assert.IsFalse(timelinesQuery.IsEmpty, "the Screen Timelines SubScene bakes Tl UI Timelines");

            var hiddenDeadline = Time.realtimeSinceStartup + 10f;
            while (hello.resolvedStyle.display != DisplayStyle.None && Time.realtimeSinceStartup < hiddenDeadline)
                yield return null;
            Assert.AreEqual(DisplayStyle.None, hello.resolvedStyle.display, "the Hide clip hides the label, which the starter screen marks as Id 0, the clip's default Id");
            var shownDeadline = Time.realtimeSinceStartup + 5f;
            while (hello.resolvedStyle.display == DisplayStyle.None && Time.realtimeSinceStartup < shownDeadline)
                yield return null;
            Assert.AreEqual(DisplayStyle.Flex, hello.resolvedStyle.display, "the label comes back when the clip ends");
            timelinesQuery.Dispose();

            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(FlowFolder);
        }
    }
}
