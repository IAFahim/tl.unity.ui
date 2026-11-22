#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using MenuSample;
using NUnit.Framework;
using TlUi;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MenuSample.Tests
{
    public class MenuTests
    {
        InputSettings _projectSettings;
        InputSettings _testSettings;
        Keyboard _keyboard;

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

        static IEnumerator Until(System.Func<bool> condition, float seconds = 3f)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        [SetUp]
        public void UseATestKeyboardThatWorksWithoutFocus()
        {
            _projectSettings = InputSystem.settings;
            _testSettings = ScriptableObject.CreateInstance<InputSettings>();
            _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _testSettings;
            _keyboard = InputSystem.AddDevice<Keyboard>("Menu Test Keyboard");
        }

        [TearDown]
        public void PutTheProjectInputBack()
        {
            if (_keyboard != null)
                InputSystem.RemoveDevice(_keyboard);
            if (_projectSettings != null)
            {
                InputSystem.settings = _projectSettings;
                Object.Destroy(_testSettings);
            }
            else
            {
                _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
                _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
            }
        }

        void Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            InputSystem.Update();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator ScreensSwapOnOneChannelSettingsWriteEcsCancelGoesBackAndTheHotkeyPauses()
        {
            var scenePath = EditorBuildSettings.scenes.Select(scene => scene.path).FirstOrDefault(path => path.EndsWith("/Menu.unity"));
            Assert.IsNotNull(scenePath, "Run Tl UI > Samples > Build Menu first.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));

            var uiPanel = Object.FindAnyObjectByType<TlUiPanel>();
            var menuView = Object.FindAnyObjectByType<MenuView>();
            var tooltips = Object.FindAnyObjectByType<TlUiTooltips>();
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var menuQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<MenuSettings>());
            var loadDeadline = Time.realtimeSinceStartup + 120f;
            while ((menuQuery.IsEmpty || uiPanel.Root == null || menuView.Version == 0) && Time.realtimeSinceStartup < loadDeadline)
                yield return null;
            Assert.IsFalse(menuQuery.IsEmpty, "the Menu Settings object bakes the settings singleton");
            var menu = menuQuery.GetSingletonEntity();
            var root = uiPanel.Root;
            var mainScreen = root.Q("mainScreen");
            var settingsScreen = root.Q("settingsScreen");
            var volume = root.Q<Slider>("volume");
            var quality = root.Q<DropdownField>("quality");

            yield return Until(() => mainScreen.resolvedStyle.display == DisplayStyle.Flex);
            Assert.AreEqual(DisplayStyle.Flex, mainScreen.resolvedStyle.display, "the director plays the Main Screen timeline, which shows the main screen and holds");
            Assert.AreEqual(DisplayStyle.None, settingsScreen.resolvedStyle.display);
            yield return Frames(60);
            Assert.AreEqual(DisplayStyle.Flex, mainScreen.resolvedStyle.display, "End Hold keeps the screen up after the timeline's last tick");

            root.Q<Button>("play").Focus();
            Assert.AreEqual("Start the game", tooltips.Label.text, "Tl UI Tooltips shows the button's tooltip on focus");

            Click(root.Q<Button>("settings"));
            yield return Until(() => settingsScreen.resolvedStyle.display == DisplayStyle.Flex && mainScreen.resolvedStyle.display == DisplayStyle.None);
            Assert.AreEqual(DisplayStyle.Flex, settingsScreen.resolvedStyle.display, "TlPlay starts the Settings Screen timeline");
            Assert.AreEqual(DisplayStyle.None, mainScreen.resolvedStyle.display, "which takes channel 1 from the Main Screen timeline, so the main screen hides");

            Assert.AreEqual(0.8f, volume.value, 0.0001f, "the volume slider shows the ECS setting");
            Assert.AreEqual(1, quality.index, "the quality dropdown's index is bound to the ECS Quality");
            volume.value = 0.25f;
            quality.index = 2;
            yield return Until(() => Mathf.Abs(entityManager.GetComponentData<MenuSettings>(menu).Volume - 0.25f) < 0.0001f && entityManager.GetComponentData<MenuSettings>(menu).Quality == 2);
            var written = entityManager.GetComponentData<MenuSettings>(menu);
            Assert.AreEqual(0.25f, written.Volume, 0.0001f, "TwoWay writes the slider into ECS through Menu View");
            Assert.AreEqual(2, written.Quality, "and the dropdown's index");

            volume.Focus();
            using (var cancel = NavigationCancelEvent.GetPooled())
            {
                cancel.target = volume;
                volume.SendEvent(cancel);
            }
            yield return Until(() => mainScreen.resolvedStyle.display == DisplayStyle.Flex && settingsScreen.resolvedStyle.display == DisplayStyle.None);
            Assert.AreEqual(DisplayStyle.Flex, mainScreen.resolvedStyle.display, "Cancel (Escape, gamepad East) inside the settings screen plays the main screen again");
            Assert.AreEqual(DisplayStyle.None, settingsScreen.resolvedStyle.display);

            volume.Blur();
            var paused = root.Q("paused");
            Assert.AreEqual(DisplayStyle.None, paused.resolvedStyle.display);
            Tap(Key.P);
            yield return Until(() => paused.resolvedStyle.display == DisplayStyle.Flex);
            Assert.IsTrue(entityManager.GetComponentData<MenuSettings>(menu).Paused, "the P hotkey sends the pause code and the Burst system pauses");
            Assert.AreEqual(DisplayStyle.Flex, paused.resolvedStyle.display, "the Paused banner binds Paused through tl-visible");

            Click(root.Q<Button>("play"));
            var started = root.Q("started");
            yield return Until(() => started.resolvedStyle.display == DisplayStyle.Flex);
            Assert.IsTrue(entityManager.GetComponentData<MenuSettings>(menu).Started, "Play sends the start code to the Burst system");

            Assert.IsTrue(UiToolkitTickProbe.Install());
            UiToolkitTickProbe.Reset();
            UiToolkitTickProbe.Measuring = true;
            yield return Frames(120);
            UiToolkitTickProbe.Measuring = false;
            var uiToolkitBytes = UiToolkitTickProbe.AllocatedBytes;
            UiToolkitTickProbe.Uninstall();
            Debug.Log("Menu allocation: UI Toolkit panel update " + uiToolkitBytes + " B over 120 frames");
            Assert.AreEqual(0L, uiToolkitBytes, "the menu at rest allocates nothing in UI Toolkit's update");
        }
    }
}
#endif
