# Changelog

## 0.3.0-preview.6

### Changed
- Updated to latest `tl` (`d87d1998`) and `tl.unity` `main` (PR #51 merged; unpinned `#main` install docs).
- Pre-warmed common `tl-integer` (`0..999`), `tl-percent` (`0..100%`), and `tl-clock` (`0:00..9:59`) string caches during `TlUiConverters.Register()` for deterministic 0 B cold CoreCLR runs.
- Verified on Windows 11 with Unity `7000.0.0a21`.

## 0.3.0-preview.5

Checked in a real windowed Unity 7000.0.0a20 Editor for the first time (on a virtual X display, driven by mouse clicks), which found two Inspector problems.

### Fixed
- **The Edit bar's buttons share the Inspector's width.** The "Edit" label is gone. In a normal-width Inspector, **Other timelines** was cut off.
- **Timeline is greyed out where it does nothing.** On Commands and Signals rows whose **Do** is Send To Game or Invoke Event, the field says "not used: Do does not play a timeline" instead of naming a timeline.

### Verified on screen
- **Tl UI > Screen** selects Screen and shows its timeline locked in the Timeline window. Selecting Screen no longer empties the Timeline window.
- Clicking the Timeline ruler previews the Game view in Edit mode: Hello is faint and lower part-way through Fade hello.
- The Preset list and its submenus. **Slide in from the left** sets Move X from -400 to 0, makes the clip 0.5 s and renames it **Move hello X**.
- The Target list shows the layout's element names.
- On a Commands row, the Timeline list shows timeline names, **New timeline…**, and **Open … in the Timeline window**, which opens that timeline.

### Docs
- Screenshots of the real Editor in the designer guide and README: the new screen, scrubbing, the Preset and Target lists, Commands with the Timeline list, a framed button timeline, and UI Builder with an element selected.
- New tips found while taking them: press **A** over the Timeline to fit all clips; fix a thin UI Builder canvas by setting its Size.

## 0.3.0-preview.4

Made so a designer can set up a screen with clicks only.

### Added
- **Edit bar.** The Inspector of a Screen (Tl UI Panel) and of Screen Timelines starts with one row: **Layout** opens the UXML in UI Builder, **Timeline** opens the screen's timeline, **Other timelines** opens the others or makes a **New timeline…**. Right-clicking the Screen in the Hierarchy offers **Tl UI > Edit Layout** and **Tl UI > Edit Timeline**.
- **No more "tick the box, expand, click the object".** Timeline opens the Screen Timelines SubScene for editing by itself and locks the Timeline window on the screen's timeline, so clicking the Screen or anything else no longer empties it. Double-clicking a `.playable` that a Screen Timelines plays does the same.
- **Element names on every timeline of a screen.** A clip on a timeline from Also Plays (a button's, a signal's) now gets the Target name list too, however the timeline was opened. Before, only the director's own timeline did.
- **Presets on Animate clips.** A **Preset** dropdown under Property fills in Property, From, To, Ease, Hold and the clip's length: Fade in, Slide in from each side, Pop in, Type out the text, Fade out, Slide out, Shrink away, Pop (grow and back), Shake, Blink, Spin once, and fills for progress bars and radial fills. **Swap From and To** is in the same menu.
- **Timelines by name on Commands and Signals.** The Timeline Slot number is a dropdown of the screen's timeline names. **New timeline…** creates one next to the main timeline, named after the row (for example `play clicked`), adds it to Also Plays, saves Screen Timelines, picks it and opens it.
- **Fix buttons instead of instructions.** Where the Inspector can't show names, the note under the field has a button: **Open Screen Timelines**, or **Add this timeline to Screen Timelines**.

### Fixed
- **New Animate clips played backwards.** Tl gives a new curve field a downhill line (1 to 0), so an Animate clip set From 0 To 1 went 1 → 0. The Inspector turns the Ease of an untouched Animate clip uphill the first time it is shown, and warns when an Ease goes downhill, with **Preset > Make Ease go uphill**.

### Not yet verified on screen
- The Edit bar, the dropdowns and the locked Timeline window are covered by EditMode tests of what they do (open, find, create, write), not by a picture of a windowed Editor.

`com.tl.ui` has no changes; its version moves with this package.

## 0.3.0-preview.3

### Added
- **Commands and Lists pick their element by name too.** The Target field on Tl UI Panel's Commands and Lists rows is a dropdown of element names. A name not used yet is added to Tl UI Timelines > Target Names at a free Id. With the Screen Timelines SubScene closed, a blue note says to tick its box.
- A visual designer guide, `docs/DESIGNER-GUIDE.md`, with diagrams of each Editor step, day-to-day recipes, and Unity renders of the Hello, Cards and Inventory screens.

### Fixed
- Inventory sample: the Equipment column is wide enough for its heading, which ran into the Shop heading.
- Cards sample: the "Drag a card here to play it" hint sits at the bottom of the table, no longer behind a played card.

Both were found in the first renders of the samples (`docs/images`).

## 0.3.0-preview.2

### Fixed
- **No more silent numbers.** When a clip's Target can't offer names, the Inspector says why in a blue note under it: the timeline was opened without its Screen Timelines object. Before, only a tooltip explained the "Target Id" number field.
- Picking a class, text or template slot from its dropdown writes through a fresh `SerializedObject`. A menu answers after the Inspector has drawn, so the old property could be stale.

### Not yet verified on screen
- The scrub preview asks Unity for a player-loop update after it changes the screen and after it stops (0.3.0-preview.1), so the Game view redraws in Edit mode. The tests check the UI state, not what a windowed Game view draws.

## 0.3.0-preview.1

Made for designers who never read code. Breaking: timelines made with 0.2 lose their UI clip values (the fields changed) and the panel's Channels table is gone; rebuild the samples.

### Added
- **Animate says what it moves.** It has **Target**, **Property** (Opacity, Move X, Move Y, Scale, Rotate, Width Percent, Height Percent, Reveal, Value), **From**, **To**, **Ease** (a 0 to 1 curve) and **Hold**. No Channels table and no channel numbers. Text has Target and Text.
- **The Inspector shows names.** Target, Class, Text and Template fields are dropdowns of element names, USS class names, texts and templates. **New class name…** and **New text…** add one in place. The Property field explains its numbers and warns when From equals To, so nothing moves.
- **Clips name themselves by what they do,** for example **Fade hello**, **Move hand Y** or **Hide title**. They rename when the Property changes, unless you typed a name.
- **The starter screen teaches by example.** **Tl UI > Screen** comes with **Fade hello** and **Move hello Y** clips that play on Play and preview while you scrub.
- **Drag between lists, with a ghost.** In a container list, a row lifts after 6 px and a ghost copy follows the pointer on top of the screen (`tl-drag-ghost`; the source gets `tl-dragging`). A list's new **Drop Code** receives rows dragged in from another list: payload is the row, target is the row landed on (-1 for empty space), and the new `UiCommandInbox.FromList` says which list it came from.
- **Cards sample:** drag a card onto the table to play it, back to take it back, onto another card to reorder; energy and score in Burst; a Card Set asset is the deck; signal and banner timelines.
- **Inventory sample:** Equipment slots you drag bag items onto, and back.
- `UiChannelSlots.Write(ref UiChannels, UiChannelKey.Of(target, property), value, hold)` lets game systems animate any property from code.

### Fixed
- **A Hide clip that ends where the timeline ends no longer leaves its target hidden while you scrub or play the Timeline window to the end.** 0.2's preview records the state after the last tick, and a test now scrubs to exactly that end. 0.1.0-preview.5 showed the last tick there, which matches the "hello stays hidden until it loops" report.
- A disabled Tl UI Panel stops its preview.
- A list targeted by a name no longer warns before the names arrive from ECS; it resolves once they do.
- Sample builders drop older-version sample scenes that no longer exist from the build list, and sample tests open the scene that exists.
- Every sample MonoBehaviour is in a file named after it, so scenes keep their script references on a20.

## 0.2.0-preview.1

### Changed
- **Needs Unity 7000.0.0a20** and `com.tl.unity` at `765657a`: tl.unity main plus IAFahim/tl.unity#51. Stay on 0.1.0-preview.5 for 7000.0.0a16.
- **Timeline + menu entries now come from Tl itself.**
  - `com.tl.ui` opts in with `[assembly: TlMenuEntries]`, so on first load Tl writes `Tl_Ui_<Clip>_TlMenu.cs` into `Assets/Tl Generated/Editor` and recompiles once.
  - The 11 hand-written menu tracks are gone; a new clip needs no new file (IAFahim/tl.unity#50).
- **Scrub preview uses Tl's public preview API:** `TlPreview.Evaluated`, `Version` and `Record`. It no longer chains onto the preview hook or copies Tl's bake, system list and stepping (IAFahim/tl.unity#49).

### Fixed
- Tl Smooth Binding keeps its scheduled item running and returns at once when nothing eases, instead of pausing and resuming it. A pause and resume unregisters and re-registers two element callbacks in UI Toolkit.

## 0.1.0-preview.5

### Fixed
- **Root cause of the Basics sample's flaky 192 B.**
  - Tl UI Panel worked out "pointer over UI" with `panel.Pick` after applying the frame's clips, and `Pick` begins with `ValidateLayout()`.
  - So when a clip changed the layout (an element shown at tick 0), the panel forced a full UI Toolkit layout pass inside its own update. That pass sometimes allocated, and it doubled layout work on frames that changed.
  - The panel now reads the pointer state first, from the layout UI Toolkit finished last frame, then applies clips.
  - A preallocated per-step recording across whole test runs shows those picks no longer allocate. The 0.1.0-preview.4 change, remembering the timeline object, made the failure rarer but was not the cause.
- **Tl UI Target** no longer gives a new name an Id that the world already uses by number. In a screen that also uses numbers, such as the samples, a new name used to take Id 0 and drive that element too. A new name now takes the first Id that the open scenes don't use: no Targets, Channels, Lists or Commands row, no UXML `TlTarget`, and no clip without a name. The skipped positions stay empty, and a warning names an existing clash.

## 0.1.0-preview.4

### Added
- **Named targets.** Right-click a UI clip and pick **Tl UI Target…** to choose an element name from your UXML; the clip renames itself (**Hide title**).
  - Names live on Tl UI Timelines (**Target Names**) and are baked into ECS as `FixedString64Bytes` (`UiTargetName`).
  - Every Tl UI Panel finds elements by those names, so a new screen needs no Targets table and no `TlTarget`.
  - Clips still store a byte, so Play mode still allocates nothing.
- **Preview while scrubbing.** Dragging the Timeline playhead shows Show, Hide, Enable, Disable, classes, attach, animate and text on every Tl UI Panel in Edit mode.
  - Tl bakes the director and runs its real systems once per edit, recording every tick, so scrubbing is a lookup and matches Play mode.
  - Signals, focus and commands do not fire in preview, and leaving preview restores the UXML.
- **GameObject > Tl UI > Screen** starts with `hello` as a target name, opens the timelines SubScene, and binds its director in the Timeline window.

### Fixed
- Tl UI Panel remembers the Tl UI Timelines object instead of running an entity query each time the pointer-over-UI or typing state changes. That query sometimes allocated 192 B, which made the Basics sample's allocation check flaky.

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
