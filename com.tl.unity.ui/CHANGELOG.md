# Changelog

## 0.1.0-preview.3

### Added
- Every UI block is in the Timeline window's **+** menu under **UI**: Show, Hide, Enable, Disable, Add Class, Remove Class, Attach, Animate, Text, Signal and Focus. Picking one adds a Tl track whose clips are that block. Before, `com.tl.unity` made menu entries only for clips compiled from `Assets/`, so the UI blocks never showed and only the sample builders could add them.
- **GameObject > Tl UI > Screen** builds a working screen in one click:
  - a Panel Renderer with Tl UI Panel;
  - a SubScene holding a Playable Director and Tl UI Timelines;
  - a starter `Screen.uxml` whose Hello label is Id 0;
  - an empty Tl timeline, which opens in the Timeline window.
- EditMode tests for both, including a designer's whole flow: Screen, then **+ > UI > Hide**, then a clip, then Play, and the label hides while the clip is on.

### Fixed
- The 0.1.0-preview.2 lookup of the Tl UI Timelines object made the Basics sample allocate 192 B at the start of some replays when the full test suite ran. The panel again finds that object as 0.1.0-preview.1 did, and falls back to the inbox only when the Playable Director has no timeline.

## 0.1.0-preview.2

### Changed
- Moved, with `com.tl.ui`, from the tl.unity branch `feat/uitk-blocks` to its own repository, IAFahim/tl.unity.ui. Install by Git URL from there.

### Fixed
- Send To Game, Tl Send, text and row codes reach the game when the Playable Director next to Tl UI Timelines has no timeline. Before, the panel found that object only through Tl's `TimelineRequest` buffer, which Tl adds only for a director with a timeline, so every code was dropped with a misleading warning. Playing a timeline slot there now logs that the director needs a timeline.
- No Unity 7 statics warnings (UAL0010, UAL0013): Tl UI Panel, Tl Row Drag and UI Toolkit Tick Probe use `[AutoStaticsCleanup]` instead of hand-written `SubsystemRegistration` resets.

## 0.1.0-preview.1

### Added
- Tl UI Panel next to Unity 7's Panel Renderer: Targets (Id to selector), Class, Template and Text Slots, Channels, Signals, Commands and a Data Source. Targets resolve in the Panel Renderer's versioned UI reload callback; each frame only the Ids and channels whose columns changed are applied.
- Tl UI Timelines and its baker: the director's Playable and Also Plays become timeline slots for commands, and the object gets a `UiCommandInbox` buffer for game systems.
- `TlUiData<T>`: shows an unmanaged ECS singleton through UI Toolkit data binding, with a typed property bag and a version that changes only when the bytes change.
- Converters `tl-integer`, `tl-percent`, `tl-clock`, `tl-visible`, `tl-hidden`, registered at runtime and in the Editor for UI Builder.
- Lists: the Lists table shows rows from `TlUiAssetRows` (a list of ScriptableObjects) or a `TlUiBufferRows<T>` (an ECS buffer) in a ListView or a container, with row clicks and drags sent to the inbox.
- `TlUiEntityViews<T>`: one view per entity with an unmanaged component, placed over the entity on screen; views are reused, not destroyed.
- Converter `tl-fixed-text` for `FixedString32Bytes` and `FixedString64Bytes`.
- UI Toolkit components (experimental in Unity 7000.0.0a16) you can author in UXML or UI Builder: `TlTarget`, `TlSend`, `TlSendValue` and `TlPlay`. List rows carry `TlRow` and `TlRowDrag`. Handlers are static, state is unmanaged, and nothing is allocated per element or per click.
- Keyboard and gamepad: Submit acts like a click for TlSend, TlPlay, Commands, and container rows (which are focusable). A ListView keeps UI Toolkit's own selection (Select Code), Submit, and reordering drag, which sends the Drag Code instead of reordering.
- Game systems raise UI signals with `UiSignals.Raise` on any `TimelineEvent<UiSignal>` buffer. Signal reactions can play, stop or restart a UI timeline or send a code, as well as call a UnityEvent.
- Tl UI Tooltips: runtime tooltips from the standard `tooltip` field, shown after a hover delay or at once on focus.
- Bindings TlClassBinding, TlTriggerBinding and TlSmoothBinding (DataBinding subclasses for UXML and UI Builder); converters tl-not and tl-invisible; TlRadialFill custom element.
- Two-way binding into ECS through Tl UI Data, writing only the fields the UI changed.
- Commands: Cancel event; hotkeys through the action's performed event, ignored while typing. UiInputState, TlSendText with UiTextInbox, dropdown index in TlSendValue.
- Window > Tl UI > Monitor; UiToolkitTickProbe for measuring UI Toolkit's panel update.
- Samples Basics, Hud, Inventory and Menu, each with a builder and a PlayMode test. Hud is authored entirely in UXML, with no Targets or Commands rows.
