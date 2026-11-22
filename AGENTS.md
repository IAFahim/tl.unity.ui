# Tl Unity UI: rules for every contributor

## What this is

Two UPM packages that let a Unity 7 game drive UI Toolkit screens from Tl timelines and ECS data, with no per-frame allocation.

- **Who uses it.** Designers build screens in UXML, USS and UI Builder, and timelines in the Timeline window. Programmers write unmanaged ECS structs and Burst systems.
- **How data moves.**
  - Time: Tl UI clips on a timeline write two Tl columns, `UiSwitches` (bit masks per Id) and `UiChannels` (16 float channels). The panel applies only what changed.
  - Game to UI: an unmanaged singleton is shown through UI Toolkit data binding (`TlUiData<T>`), and Burst systems raise `UiSignal` codes.
  - UI to game: clicks, values, text and hotkeys become integer codes in the `UiCommandInbox` and `UiTextInbox` buffers, or timeline requests. Game code never references the UI.

| Layer | Package | Job | Input | Output |
|---|---|---|---|---|
| Rules | `com.tl.ui` | what each clip writes and how clips combine; engine-free, builds as .NET | clip data, ticks | `UiSwitches`, `UiChannels`, a look per target |
| Host | `com.tl.unity.ui` | applies looks to UI Toolkit, binds ECS data, sends input back | Tl columns, ECS singletons and buffers, UI Toolkit events | element styles and classes, inbox buffers, timeline requests |

`com.tl.unity` (Tl playback) lives in IAFahim/tl.unity and is pinned to `765657a` (tl.unity main plus IAFahim/tl.unity#51: `TlPreview.Evaluated`/`Version`/`Record` and `[assembly: TlMenuEntries]`), the commit these packages are tested against, on Unity 7000.0.0a20.

## Start here

- Before any work, pull IAFahim/unity-cli-skill and read `skills/unity-cli-field-notes/SKILL.md`. Before you finish, push what you learned back to its `main`.
- Use only the newest Unity 7 editor (`unity install 7 --yes --accept-eula`).
- Direct pushes to `main` are authorized: `git pull --rebase origin main`, then `git push origin HEAD:main`, never `--force`. Ask the owner before you create or move tags, or change visibility.
- Never hand-edit Unity-serialized files (`.asset`, `.unity`, `.prefab`, `.meta`). Create them through Unity.
- ECS code is `ISystem`, Burst and unmanaged data only.
- Designers wire references in the Inspector, never by name or path at runtime.
- No code comments. Zero compiler warnings from these packages.
- Every documented example is built and run in Unity before it is published.

## Gates

```bash
git clone --recurse-submodules https://github.com/IAFahim/tl.unity.ui.git
```

```bash
dotnet test tl.unity.ui/com.tl.ui/Tests~/Tl.Ui.Tests
```

Then, in a fresh 7000.0.0a20 project:
1. Add the packages with `file:` paths and `"testables": ["com.tl.unity.ui"]`. Unity hides warnings for Git-URL packages, so only a `file:` install shows them.
2. Compile, and check `error CS` and own warnings are 0.
3. Run EditMode tests (5: the Timeline menu, the target action, the Id guard for new names, scrub preview, and the designer flow in Play mode), then PlayMode tests (21 package tests). Run PlayMode twice with the samples imported; the allocation checks must read 0 B both times.
4. Import a sample, run its **Tl UI > Samples > Build** item, and run its test.
5. Before a tag, install the exact commit by Git URL and run the PlayMode tests again.
