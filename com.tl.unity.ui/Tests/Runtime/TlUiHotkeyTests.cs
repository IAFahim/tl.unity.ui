using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tl;
using Tl.Ui;
using TlUi;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TlUiPackage.Tests
{
    public class TlUiHotkeyTests
    {
        World _world;
        Entity _host;
        GameObject _screen;
        InputSettings _projectSettings;
        InputSettings _testSettings;
        Keyboard _keyboard;
        InputActionAsset _actions;

        [SetUp]
        public void UseATestKeyboardThatWorksWithoutFocus()
        {
            _projectSettings = InputSystem.settings;
            _testSettings = ScriptableObject.CreateInstance<InputSettings>();
            _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _testSettings;
            _keyboard = InputSystem.AddDevice<Keyboard>("Tl UI Test Keyboard");
        }

        [TearDown]
        public void PutTheProjectInputBack()
        {
            if (_screen != null)
                Object.Destroy(_screen);
            if (_world != null && _world.IsCreated)
                _world.Dispose();
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
            if (_actions != null)
                Object.Destroy(_actions);
        }

        void Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            InputSystem.Update();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator AHotkeyRunsItsCommandThroughThePerformedEventButNotWhileTyping()
        {
            _actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var pause = _actions.AddActionMap("Screen").AddAction("Pause", binding: "<Keyboard>/escape");
            var hotkey = InputActionReference.Create(pause);
            _world = new World("Tl UI hotkey test");
            _host = _world.EntityManager.CreateEntity(typeof(UiSwitches), typeof(UiChannels));
            _world.EntityManager.AddBuffer<UiTimelineSlot>(_host).Add(new UiTimelineSlot { Timeline = new TlTimeline { Asset = new TlAssetId(1, 2, 3, 4) } });
            _world.EntityManager.AddBuffer<Tl.Unity.TimelineRequest>(_host);
            _world.EntityManager.AddBuffer<UiCommandInbox>(_host);
            _screen = new GameObject("Hotkey screen");
            _screen.SetActive(false);
            var panelRenderer = _screen.AddComponent<PanelRenderer>();
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            panelRenderer.panelSettings = panelSettings;
            var panel = _screen.AddComponent<TlUiPanel>();
            panel.Panel = panelRenderer;
            panel.Commands.Add(new UiCommand { On = UiCommandEvent.HotkeyOnly, Do = UiCommandAction.SendToGame, GameCode = 60, GamePayload = 1, Hotkey = hotkey });
            panel.Bind(_world);
            _screen.SetActive(true);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (panel.Root == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(panel.Root);

            Tap(Key.Escape);
            yield return null;
            var inbox = _world.EntityManager.GetBuffer<UiCommandInbox>(_host).AsNativeArray().ToArray();
            Assert.AreEqual(1, inbox.Length, "pressing the hotkey's key runs its command once");
            Assert.AreEqual(60, inbox[0].Code);
            Assert.AreEqual(1, inbox[0].Payload);

            var nameField = new TextField();
            panel.Root.Add(nameField);
            yield return null;
            nameField.Focus();
            yield return null;
            Tap(Key.Escape);
            yield return null;
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<UiCommandInbox>(_host).Length, "a hotkey does nothing while a TextField has focus");

            nameField.Blur();
            yield return null;
            Tap(Key.Escape);
            yield return null;
            Assert.AreEqual(2, _world.EntityManager.GetBuffer<UiCommandInbox>(_host).Length, "and works again once typing ends");
        }
    }
}
