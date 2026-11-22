# Tl Unity UI

UI Toolkit for Tl timelines and Unity ECS. Designers make a UI Toolkit screen as usual (UXML, USS, UI Builder) and drive it from three places:

- **Time**: UI clips on a Tl timeline (Show, Hide, Enable, Disable, Add Class, Remove Class, Attach, Animate, Text, Signal, Focus).
- **Game data**: an unmanaged ECS singleton, written from Burst, shown through UI Toolkit data binding.
- **Player input**: clicks, value changes and hotkeys that play UI timelines or send codes to game systems.

The clips and their rules are engine-free and live in `com.tl.ui`; this package is the Unity side. Once warm, its per-frame code allocates nothing.

## Install

The packages live on the tl.unity branch `feat/uitk-blocks` and are tested as local packages. Add all three to `Packages/manifest.json` with `file:` paths to a checkout of that branch:

```json
"com.tl.unity": "file:/path/to/tl.unity/com.tl.unity",
"com.tl.ui": "file:/path/to/tl.unity/com.tl.ui",
"com.tl.unity.ui": "file:/path/to/tl.unity/com.tl.unity.ui"
```

Once the branch is merged, each also installs by Git URL (`https://github.com/IAFahim/tl.unity.git?path=/com.tl.ui#main` and so on); list all three, because a Git package cannot pull another Git package. The Git install is not tested yet.

## Set up a screen

1. **The timeline object.** In a SubScene, make a GameObject with a **Playable Director** and add **Tl UI Timelines**. Its Playable is timeline slot 0; its **Also Plays** list holds slots 1 and up, which buttons can play.
2. **The screen.** In the main scene, make a GameObject with a **Panel Renderer** (Unity 7's UI Toolkit component; UI Document is Legacy) holding your Panel Settings and UXML, and add **Tl UI Panel**.
3. **Targets.** Give each thing a timeline will touch an Id (0 to 63): put a **Target** component on the element in UXML or UI Builder (see below), or add a row to Tl UI Panel's **Targets** table with a selector: `#name` is one element, `.class` every element with that class, `Button` every button, and words combine (`.shop-card Button`). An element can get Ids from both.
4. **Clips.** In the Timeline window, add Tl tracks with UI clips (menu **UI**) and set each clip's Id.

## Blocks

| Clip | While it is on |
|---|---|
| Show | the targets are visible (`display: flex`) |
| Hide | the targets are hidden; **Keep Space** makes them invisible in place instead |
| Enable / Disable | the targets can or cannot be used |
| Add Class / Remove Class | a USS class from the **Class Slots** list is added or removed; USS transitions animate the change |
| Attach | a UXML template from **Template Slots** is shown inside the targets (created once, then shown and hidden) |
| Animate | a curve drives a **channel** (0 to 15); the **Channels** table says which target and property: opacity, translate, scale, rotate, width, height, reveal, or the value of a slider, progress bar or custom element. **Hold** keeps the last key after the clip |
| Text | a channel shows a text from **Text Slots** |
| Signal | once, at its start: the **Signals** table's UnityEvent for its code (sound, particles, any method) |
| Focus | once, at its start: keyboard and gamepad focus moves to the target |

Rules when clips disagree: Hide beats Show, Disable beats Enable, Remove Class beats Add Class. With no clip on, a target is exactly what the UXML and USS made it. Clips with different Ids overlap freely; two clips that overlap on one track count only the first, so give overlapping work its own track.

## Commands

The **Commands** table turns a target's Click, Pointer Enter, Pointer Leave or Value Changed event, or an Input Action **Hotkey** (built, not yet covered by a test), into one action: play, stop, pause, resume or restart a timeline slot, **Send To Game** (a code and payload appended to the timeline object's `UiCommandInbox` buffer, read and cleared by your Burst systems), or a UnityEvent.

```csharp
foreach (var inbox in SystemAPI.Query<DynamicBuffer<UiCommandInbox>>())
{
    for (var command = 0; command < inbox.Length; command++)
        if (inbox[command].Code == rules.HealCode)
            hud.Health = math.min(hud.MaxHealth, hud.Health + rules.HealAmount);
    inbox.Clear();
}
```

## Author in UXML with UI Toolkit components

Unity 7 lets a struct ride on any element as a UI Toolkit component. Tl UI ships four you can author in UXML, so a screen needs no table rows for them. In UXML a component is a child node named by its struct:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:tl="TlUi">
    <ui:Label name="title" text="Shop">
        <tl:TlTarget id="0" />
    </ui:Label>
    <ui:Button text="Heal">
        <tl:TlSend code="1" />
    </ui:Button>
    <ui:Button text="Flash">
        <tl:TlPlay timeline="1" verb="Restart" />
    </ui:Button>
    <ui:Slider low-value="0" high-value="1">
        <tl:TlSendValue code="4" scale="100" />
    </ui:Slider>
</ui:UXML>
```

| Component | Does |
|---|---|
| `TlTarget id` | puts the element in that Id, like a Targets row |
| `TlSend code payload row-as-payload` | a click appends the code to the `UiCommandInbox`; with **Row As Payload** the payload is the index of the list row the button sits in |
| `TlSendValue code scale` | a float, int or bool value change sends the code with the new value (floats times **Scale**, rounded) |
| `TlPlay timeline verb` | a click plays, stops, pauses, resumes or restarts a timeline slot (UXML reserves `slot`, so the attribute is `timeline`) |

From code it is the same struct: `button.AddComponent(new TlSend { Code = 1 })`. TlSend and TlPlay stop the click there, so a button inside a list row does not also click the row. List rows carry two code-only components the panel adds itself: `TlRow` (list and row index, row clicks) and `TlRowDrag` (drag onto another row).

The handlers are static and registered once per type, the state is an unmanaged struct UI Toolkit keeps in native memory, and nothing is allocated per element or per click. UI Toolkit components are **experimental** in Unity 7000.0.0a16. They need no setting at runtime. **Project Settings > UI Toolkit > Enable UI Components** only adds the Components section to the UI Builder inspector, so a designer can add them there. The Targets and Commands tables work without components. Use the tables for anything that needs an asset or an event: Input Action hotkeys, UnityEvents.

## Game data

Write an unmanaged singleton from any system, add a one-line view, and drag the view into Tl UI Panel's **Data Source**:

```csharp
[GeneratePropertyBag]
public struct HudData : IComponentData
{
    public float Health;
    public int Coins;
    public bool LowHealth;
}

public sealed class HudView : TlUiData<HudData>
{
}
```

Bind in UI Builder or UXML with paths relative to the struct (`Health`, `Coins`). The view copies the singleton once per frame and changes its version only when the bytes change, so UI Toolkit re-evaluates bindings only then. Converters for bindings:

| Converter | From | To |
|---|---|---|
| `tl-integer` | int or float | text, rounded; 0 to 9,999 are cached, so they cost nothing after the first time |
| `tl-percent` | float 0 to 1 | `"45%"`, cached |
| `tl-clock` | seconds | `"m:ss"`, cached to 99:59 |
| `tl-visible` / `tl-hidden` | bool | `display` |

A ScriptableObject works as the Data Source too, for settings and catalogs.

## Lists and entity views

The **Lists** table shows rows in a target: a ListView (virtualized) or any container (rows pooled and hidden when unused). Rows come from a **Tl UI Asset Rows** asset (a list of ScriptableObjects, for shops and catalogs) or a **Tl UI Buffer Rows** component that copies an ECS buffer, and each row binds its UXML template with paths relative to the row (`Name`, `Count`). A row click sends **Click Code** with the row index; dragging a row onto another sends **Drag Code** with both indexes, and the rows get `tl-dragging` and `tl-drop-target` while it happens.

```csharp
public sealed class BagRows : TlUiBufferRows<InventorySlot>
{
}
```

**Tl UI Entity Views** gives every entity with an unmanaged component a view from a UXML template inside a container target, placed over the entity on screen each frame and bound to that entity's component: nameplates, health bars, markers. Views of destroyed entities are hidden and reused.

```csharp
public sealed class NameplateViews : TlUiEntityViews<Nameplate>
{
}
```

## Measured

Unity 7000.0.0a16, `-batchmode -nographics`, PlayMode tests in a fresh project whose only extra packages are these three:

- Basics: every block matches its clip to the tick (a 0 to 1 s clip at 60 Hz is on for ticks 0 to 59). On the third play the Tl UI Panel's code allocates 0 B (over 19,025 to 34,369 frames, depending on the run).
- Hud: Tl UI Data and Tl UI Panel allocate 0 B over 240 frames in which the singleton changes every frame; the whole main thread allocated 158,776 B in those frames against 157,440 B with the data paused, so the binding updates add about 6 B per frame.
- Inventory: rows show the ECS buffer through `tl-fixed-text` and `tl-integer`; a row click uses an item, a synthesized drag swaps two slots, a shop row click buys. Nameplates follow their entities within 3 px, and a destroyed entity's view is hidden for reuse. Bag Rows, Nameplate Views and the panel allocate 0 B over 240 frames while two nameplates move and change every frame.
- Package tests: selectors for one element, a class and a type; Hide with Keep Space, Enable on an authored-disabled button, Remove Class; translate, scale, rotate and width channels; Pointer Enter, Pointer Leave and Value Changed commands into the inbox; Play, Stop, Pause and Resume clicks into the request buffer; a ScriptableObject data source through `tl-percent` and `tl-hidden`; every component added from code and authored in UXML, with every attribute set to a non-default value.
- UI Toolkit components: 1,000 rounds of `GetComponent`, `HasComponent`, a click through a static handler and `MarkComponentDirty` allocate 0 B once warm.
- Every UI Toolkit call the panel makes (style display, set enabled, label text, progress value, focus, attach, detach) allocates 0 B once warm.

The first play of a timeline pays one-time costs: UI Toolkit focus and blur paths, Attach creating its template, Tl filling its per-stage event cache. Rendering was not part of these runs.

## Limits

- 64 Ids, 16 channels and 8 slots of each kind per screen.
- Ids are shared by every timeline in the World: any timeline with an Id 3 clip drives every target with Id 3.
- Animate writes inline styles; a USS transition on the same property eases the timeline's values.
- One timeline object (Tl UI Timelines) per World receives commands.

## Samples

- **Basics**: every time block on one screen, with a test that checks each against its clip.
- **Hud**: ECS data binding, converters, the command inbox and a button that plays a timeline, all authored in UXML with Target, Send and Play components.
- **Inventory**: a bag from an ECS buffer (click to use, drag to swap), a shop ListView from ScriptableObjects (click to buy), and nameplates that follow three moving enemies.

Import a sample, run **Tl UI > Samples > Build Basics** (or **Build Hud**, **Build Inventory**), open the built scene and press Play.
