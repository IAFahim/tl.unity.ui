# Tl Unity UI

UI Toolkit screens driven by [Tl](https://github.com/IAFahim/tl) timelines and Unity ECS. Built for Unity 7000.0.0a16.

You make the screen the usual UI Toolkit way, with UXML, USS and UI Builder. Then three things drive it:

- **Timelines move the screen.** Show, hide, add a class, animate a value or type out text, all from clips in Unity's Timeline window.
- **Game data shows on screen.** A Burst system writes a plain ECS struct, and UI Toolkit bindings display it. Nothing is allocated per frame.
- **Input goes back to the game.** Clicks, slider changes and hotkeys become small integer codes that your Burst systems read.

Game code never calls the UI and never holds a reference to it. Game code writes data and codes, and the UI reads them.

## What is in this repo

| Folder | Package | What it is |
|---|---|---|
| `com.tl.ui` | Tl UI | Engine-free UI clips and rules. It also builds as a plain .NET project. |
| `com.tl.unity.ui` | Tl Unity UI | The Unity side: UI Toolkit, ECS, input, samples and tests. |

Both packages need `com.tl.unity`, the Tl playback package from [IAFahim/tl.unity](https://github.com/IAFahim/tl.unity).

## Install

Add these three lines to `dependencies` in your project's `Packages/manifest.json`:

```json
"com.tl.unity": "https://github.com/IAFahim/tl.unity.git?path=/com.tl.unity#92c8c6c343bdbfc179e4d49f88dd9e77414e7346",
"com.tl.ui": "https://github.com/IAFahim/tl.unity.ui.git?path=/com.tl.ui#v0.1.0-preview.1",
"com.tl.unity.ui": "https://github.com/IAFahim/tl.unity.ui.git?path=/com.tl.unity.ui#v0.1.0-preview.1"
```

You need all three lines because a Git package cannot pull in another Git package. `com.tl.unity` is pinned to the commit these packages were tested against. Unity resolves Entities, Input System and Timeline for you.

## Try it in five minutes

1. Add the three lines above to an empty 7000.0.0a16 project.
2. Open **Window > Package Manager**, select **Tl Unity UI**, open **Samples** and import **Hud**.
3. Run **Tl UI > Samples > Build Hud** from the menu bar. This creates `Hud.unity`, its timelines and its Panel Settings in the imported sample folder.
4. Open `Hud.unity` and press Play.

You should see the following:
- Health drains, and **Heal** heals through an ECS system.
- **Flash** plays a timeline.
- The ring and bar ease to the new value.
- Below 30% health, a banner pulses.

The other samples work the same way:

| Sample | Shows |
|---|---|
| **Basics** | every timeline clip on one screen |
| **Hud** | ECS data on screen, buttons that reach game systems |
| **Inventory** | a bag from an ECS buffer (click to use, drag to swap), a shop list, tooltips, and nameplates that follow enemies |
| **Menu** | two screens that swap with no screen code, settings that write ECS, Escape to go back, and a pause hotkey |

## Make your own screen

**1. The data.** Write an unmanaged struct and a one-line view.

```csharp
using TlUi;
using Unity.Entities;
using Unity.Properties;

[GeneratePropertyBag]
public struct PlayerHud : IComponentData
{
    public float Health;
    public int Coins;
}

public sealed class PlayerHudView : TlUiData<PlayerHud> { }
```

**2. The game.** A Burst system writes the struct and reads the codes the UI sent.

```csharp
[BurstCompile]
public partial struct PlayerHudSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var hud = SystemAPI.GetSingleton<PlayerHud>();
        foreach (var inbox in SystemAPI.Query<DynamicBuffer<UiCommandInbox>>())
        {
            for (var i = 0; i < inbox.Length; i++)
                if (inbox[i].Code == 1)
                    hud.Health += 30f;
            inbox.Clear();
        }
        SystemAPI.SetSingleton(hud);
    }
}
```

Bake the `PlayerHud` singleton from any authoring component, the way the Hud sample's `HudDemoAuthoring` does.

**3. The screen.** Bind to the struct's fields and give the button a code.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:tl="TlUi">
    <ui:Label name="health">
        <Bindings>
            <ui:DataBinding property="text" data-source-path="Health" binding-mode="ToTarget" source-to-ui-converters="tl-integer" />
        </Bindings>
    </ui:Label>
    <ui:Button text="Heal">
        <tl:TlSend code="1" />
    </ui:Button>
</ui:UXML>
```

**4. The scene.**
- **In a SubScene**, add a GameObject with these:
  - a **Playable Director**, whose timeline can be empty for now;
  - **Tl UI Timelines**, which gives the game the `UiCommandInbox`;
  - your authoring component that bakes `PlayerHud`.
- **In the main scene**, add a GameObject with a **Panel Renderer** (your Panel Settings and UXML), your `PlayerHudView`, and **Tl UI Panel**.
- **Wire it up.** On Tl UI Panel, set **Panel** to the Panel Renderer and **Data Source** to the view.

Press Play: the label shows `Health`, and the button heals.

To animate the screen, give elements an Id with `<tl:TlTarget id="0"/>` and put UI clips with that Id on a Tl track in the Timeline window.

## Where to read next

- [`com.tl.unity.ui/README.md`](com.tl.unity.ui/README.md): every clip, component, binding, converter, list and limit.
- [`docs/LEGO-BLOCKS.md`](docs/LEGO-BLOCKS.md): every need, and which block covers it. Where UI Toolkit already covers a need, Tl UI adds nothing.

## Status

Preview, version `0.1.0-preview.1`. Checked on Unity 7000.0.0a16 on Linux:
- Editor PlayMode tests: 20 package tests and 4 sample tests;
- 13 .NET tests;
- CoreCLR and IL2CPP players running all four samples headless.

Not checked yet: other platforms, how it looks on screen, and newer Unity alphas.

## Develop

```bash
git clone --recurse-submodules https://github.com/IAFahim/tl.unity.ui.git
```

```bash
dotnet test tl.unity.ui/com.tl.ui/Tests~/Tl.Ui.Tests
```

The .NET build of `com.tl.ui` finds Tl in the `external/tl-kinds` submodule. To work on the packages in a Unity project, point the manifest at the folders with `file:` paths.

## License

MIT
