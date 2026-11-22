# Tl UI Lego blocks

Every screen is built from these. **UITK** means UI Toolkit already does it on Unity 7000.0.0a16, so Tl UI adds nothing. **Tl** means a Tl UI block, needed because UI Toolkit lacks it or because it connects UI Toolkit to ECS and timelines. All Tl blocks allocate 0 B per frame once warm and are covered by PlayMode tests.

## Look and layout (UITK, no code)

| Need | Use |
|---|---|
| Layout, grids, spacing | flexbox, `gap`, `aspect-ratio`, `display: grid` (experimental) |
| States: hover, pressed, focus, checked, disabled | USS `:hover` `:active` `:focus` `:checked` `:disabled` |
| Themes and tokens | USS variables on `:root`, `.tss` theme per panel |
| Effects | USS `filter`, `drop-shadow`, `backdrop-filter`, `-unity-material` (UI Shader Graph) |
| Icons | `.svg` as `VectorImage` with tint |
| Keyframe and state animation | USS transitions, UI animation clips via `animation-name` |
| Controls | Button, RepeatButton, Toggle, ToggleButtonGroup, RadioButtonGroup, Slider, MinMaxSlider, ProgressBar, DropdownField, TextField, TabView, Foldout, ScrollView, ListView, TreeView, MultiColumnListView |
| Custom controls | `[UxmlElement]`, `BaseField<T>`, Painter2D, `[GenerateVisualContent]` |
| World-space UI | PanelRenderer with world-space Panel Settings |

## Time: a Tl timeline drives the screen (Tl)

| Block | Does |
|---|---|
| Show, Hide, Enable, Disable | per Id while the clip is on; Hide beats Show, Disable beats Enable |
| Add Class, Remove Class | toggles a USS class, so any USS transition or `animation-name` animation plays from the timeline |
| Attach | shows a UXML template inside the target |
| Animate | a curve drives opacity, translate, scale, rotate, width, height, reveal or a value, frame-exact, with hold |
| Text | shows a text slot, with typewriter reveal |
| Signal, Focus | one-shot at clip start |

## Address: which element (Tl)

| Block | Does |
|---|---|
| Targets table | Id to a selector: `#name`, `.class`, `Button`, combined |
| `<tl:TlTarget id="3"/>` | the same, authored on the element in UXML or UI Builder |

## Data: game state on screen

| Need | Use |
|---|---|
| One ECS singleton | **Tl** `TlUiData<T>`: copied once per frame, rebinds only when its bytes change |
| A list from an ECS buffer | **Tl** `TlUiBufferRows<T>` + Lists table |
| A list of ScriptableObjects | **Tl** `TlUiAssetRows` + Lists table |
| A view over every entity (nameplates, markers) | **Tl** `TlUiEntityViews<T>` |
| Formatting | **Tl** converters `tl-integer`, `tl-percent`, `tl-clock`, `tl-visible`, `tl-hidden`, `tl-invisible`, `tl-not`, `tl-fixed-text` |
| Enabled or disabled from data | **UITK** bind `enabledSelf` (with `tl-not`) |
| A USS class from data | **Tl** `<tl:TlClassBinding uss-class="danger" .../>` in Bindings |
| A UI timeline from data (low-health pulse) | **Tl** `<tl:TlTriggerBinding timeline="2" when-on="Play" when-off="Stop" .../>` |
| Bars that drain, counters that roll | **Tl** `<tl:TlSmoothBinding seconds="0.3" .../>` |
| Settings controls (two-way) | **UITK** `binding-mode="TwoWay"`: to a ScriptableObject directly, to ECS through Tl UI Data (changed fields only) |

## Input: player to game

| Need | Use |
|---|---|
| Click or Submit sends a code to ECS | **Tl** `<tl:TlSend code="1"/>` or a Commands row |
| A value change sends a code | **Tl** `<tl:TlSendValue code="4" scale="100"/>` |
| Click plays a UI timeline | **Tl** `<tl:TlPlay timeline="1" verb="Restart"/>` |
| Hotkey | **Tl** Commands row with an Input Action (performed event; ignored while typing) |
| Back | **Tl** Commands row on Cancel (Escape, gamepad East) |
| Text into ECS | **Tl** `<tl:TlSendText code="5"/>` on a TextField, into `UiTextInbox` |
| Dropdown choice | **UITK** bind `index` TwoWay, or **Tl** TlSendValue sends the index |
| Does the game own this click or key? | **Tl** `UiInputState` (pointer over UI, typing) on the timeline object |
| Choose, select, reorder in a list | **UITK** ListView selection, Submit and drag, sent by Tl as Click, Select and Drag codes |
| Drag between container rows | **Tl** `TlRowDrag` |
| Keyboard and gamepad navigation | **UITK** focus ring; Tl blocks are focusable and answer Submit |

## Moments: game to UI

| Need | Use |
|---|---|
| Something happened (level up, quest done) | **Tl** a Burst system calls `UiSignals.Raise(code)`; the Signals table plays a UI timeline, sends a code, or calls an event |

## Help

| Need | Use |
|---|---|
| Tooltips at runtime | **Tl** Tl UI Tooltips reads the standard `tooltip` field (bindable); shown on hover after a delay, at once on focus |
| Context menus at runtime | not covered (UI Toolkit shows them only in the Editor) |

## Draw and see

| Need | Use |
|---|---|
| A ring or radial cooldown | **Tl** `<tl:TlRadialFill>`: Painter2D, bindable `value`, `--tl-fill-color` / `--tl-track-color` / `--tl-thickness` |
| Other custom drawing | **UITK** `[UxmlElement]` + `INotifyValueChanged<float>` gets the Animate Value channel, bindings and smoothing for free |
| See every screen's state while playing | **Tl** Window > Tl UI > Monitor |
| Measure UI Toolkit's own allocation | **Tl** `UiToolkitTickProbe` around `UIElementsUpdatePanels` |

## Screen flow

| Need | Use |
|---|---|
| One screen at a time | **Tl** each screen is a timeline with a Show clip on one channel, End Hold; starting one replaces the other |
| Intro then menu | **Tl** End Next |
| Pause | an ECS flag. `Time.timeScale = 0` also freezes UI timelines (see [Limits](../com.tl.unity.ui/README.md#limits)) |

## How ECS and UI connect

Data flows one way and commands flow back. Game systems write plain components, buffers and signal codes; they never reference a view model, a pointer or a managed callback. The UI watches those components and sends codes back through the inbox and timeline requests. Compare BovineLabs Anchor, where a system holds a raw pointer into a pinned managed view model, found through a service locator.
