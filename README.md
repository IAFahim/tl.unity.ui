# Tl Unity UI

UI Toolkit screens driven by [Tl](https://github.com/IAFahim/tl) timelines and Unity ECS. Built for Unity 7000.0.0a16.

You make the screen the usual UI Toolkit way, with UXML, USS and UI Builder. Then three things drive it:

- **Timelines move the screen.** Show, hide, add a class, animate a value or type out text, all from clips in Unity's Timeline window.
- **Game data shows on screen.** A Burst system writes a plain ECS struct, and UI Toolkit bindings display it. Nothing is allocated per frame.
- **Input goes back to the game.** Clicks, slider changes and hotkeys become small integer codes that your Burst systems read.

Game code never calls the UI and never holds a reference to it. Game code writes data and codes, and the UI reads them.

## Designers: start here (no code)

Make something hide, and see it while you scrub:

1. Save your scene. In the **Hierarchy**, right-click and pick **Tl UI > Screen**. A big **Hello** label appears, and the Timeline window opens on it, ready to scrub.
2. In the Timeline window, click **+** and pick **UI > Hide**.
3. Right-click the new **Hide** track and pick **Add Tl Clip**. Drag the clip to where the hide should start, and drag its end to how long it lasts.
4. Drag the playhead across the clip. Hello disappears in the Game view while the playhead is on the clip, with no Play needed. Press **Play** to see it run for real.

To hide something else:
1. Double-click `Screen.uxml` to open it in UI Builder, add an element, and give it a **Name**.
2. Right-click the clip and pick **Tl UI Target…**, then pick that name. The clip renames itself, for example **Hide title**.

You never type a number. Every block is under **+ > UI**: Show, Hide, Enable, Disable, Add Class, Remove Class, Attach, Animate, Text, Signal and Focus.

Names live on the **Screen Timelines** object, in **Tl UI Timelines > Target Names**, and are baked into ECS as fixed strings. A clip stores its name's position in that list, a byte, so Play mode still allocates nothing. A new name never takes a number that an older screen already uses by hand.

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
"com.tl.ui": "https://github.com/IAFahim/tl.unity.ui.git?path=/com.tl.ui#v0.1.0-preview.5",
"com.tl.unity.ui": "https://github.com/IAFahim/tl.unity.ui.git?path=/com.tl.unity.ui#v0.1.0-preview.5"
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
- The bar eases to the new health, and the ring follows it.
- Below 30% health, a banner pulses.

The other samples work the same way:

| Sample | Shows |
|---|---|
| **Basics** | every timeline clip on one screen |
| **Hud** | ECS data on screen, buttons that reach game systems |
| **Inventory** | a bag from an ECS buffer (click to use, drag to swap), a shop list, tooltips, and nameplates that follow enemies |
| **Menu** | two screens that swap with no screen code, settings that write ECS, Escape to go back, and a pause hotkey |

## Make your own screen

Five files and two GameObjects. This exact example is built and tested in a fresh project.

**1. The data.** An unmanaged struct the screen shows, and the rules the game reads. `PlayerHud.cs`:

```csharp
using Unity.Entities;
using Unity.Properties;

[GeneratePropertyBag]
public struct PlayerHud : IComponentData
{
    public float Health;
    public int Coins;
}

public struct PlayerHudRules : IComponentData
{
    public int HealCode;
    public float HealAmount;
}
```

**2. The view.** One line connects the struct to UI Toolkit data binding. `PlayerHudView.cs`:

```csharp
using TlUi;

public sealed class PlayerHudView : TlUiData<PlayerHud>
{
}
```

**3. The authoring.** It bakes both structs, and a designer sets the numbers in the Inspector. `PlayerHudAuthoring.cs`:

```csharp
using Unity.Entities;
using UnityEngine;

public sealed class PlayerHudAuthoring : MonoBehaviour
{
    public float StartHealth = 100f;
    public int HealCode = 1;
    public float HealAmount = 30f;

    sealed class Baker : Baker<PlayerHudAuthoring>
    {
        public override void Bake(PlayerHudAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PlayerHud { Health = authoring.StartHealth });
            AddComponent(entity, new PlayerHudRules { HealCode = authoring.HealCode, HealAmount = authoring.HealAmount });
        }
    }
}
```

**4. The game.** A Burst system reads the codes the UI sent and writes the struct back. `PlayerHudSystem.cs`:

```csharp
using TlUi;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct PlayerHudSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerHud>();
        state.RequireForUpdate<PlayerHudRules>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var rules = SystemAPI.GetSingleton<PlayerHudRules>();
        var hud = SystemAPI.GetSingleton<PlayerHud>();
        foreach (var inbox in SystemAPI.Query<DynamicBuffer<UiCommandInbox>>())
        {
            for (var command = 0; command < inbox.Length; command++)
                if (inbox[command].Code == rules.HealCode)
                    hud.Health += rules.HealAmount;
            inbox.Clear();
        }
        SystemAPI.SetSingleton(hud);
    }
}
```

**5. The screen.** Bind the label to a field, and give the button the heal code. `PlayerHud.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:tl="TlUi">
    <ui:Label name="health">
        <Bindings>
            <ui:DataBinding property="text" data-source-path="Health" binding-mode="ToTarget" source-to-ui-converters="tl-integer" />
        </Bindings>
    </ui:Label>
    <ui:Button name="heal" text="Heal">
        <tl:TlSend code="1" />
    </ui:Button>
</ui:UXML>
```

**6. The scene.**
- **In a SubScene**, add a GameObject with **Tl UI Timelines** and **Player Hud Authoring**. Tl UI Timelines adds a Playable Director, and its Playable can stay empty until you have a timeline. This object gets the `UiCommandInbox`.
- **In the main scene**, add a GameObject with a **Panel Renderer** (your Panel Settings, and `PlayerHud.uxml`), **Player Hud View** and **Tl UI Panel**. On Tl UI Panel, set **Panel** to the Panel Renderer and **Data Source** to the view.

Press Play: the label shows 100, and each click on **Heal** adds 30.

To animate the screen, give elements an Id with `<tl:TlTarget id="0"/>` and put UI clips with that Id on a Tl track in the Timeline window.

## Where to read next

- [`com.tl.unity.ui/README.md`](com.tl.unity.ui/README.md): every clip, component, binding, converter, list and limit.
- [`docs/LEGO-BLOCKS.md`](docs/LEGO-BLOCKS.md): every need, and which block covers it. Where UI Toolkit already covers a need, Tl UI adds nothing.

## Status

Preview, version `0.1.0-preview.5`. Checked on Unity 7000.0.0a16 on Linux.

On `0.1.0-preview.4`:
- 21 package PlayMode tests, plus the Basics, Hud, Inventory and Menu sample tests (three full runs, 26/26 each);
- 4 EditMode tests: every block in the **+** menu, **Tl UI Target** naming a clip, scrubbing hiding and restoring Hello, and the element named hello hiding in Play mode;
- 13 .NET tests from a fresh clone.

On `0.1.0-preview.2`: "Make your own screen" above, built as the steps say.

On `0.1.0-preview.1`: CoreCLR and IL2CPP players running all four samples headless.

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
