# Tl UI

Engine-free UI clips and rules for Tl timelines. This package holds the part of a timeline-driven UI that does not depend on any engine: what each clip writes, how clips combine, and what a target should look like. Any engine host applies the result to its own UI; `com.tl.unity.ui` is the Unity one.

## What the clips write

| Clip | Fields | Writes |
|---|---|---|
| Show, Hide (Keep Space), Enable, Disable | `Id` | a bit for the Id in the matching mask of the `UiSwitches` column while the clip is on |
| Add Class, Remove Class | `Id`, `ClassSlot` | a bit in the slot's add or remove mask |
| Attach | `Id`, `TemplateSlot` | a bit in the slot's attach mask |
| Animate | `Channel`, `Value` (curve), `Hold` | the curve's value into a `UiChannels` channel while on; with Hold, the last key at the clip's end, kept until another clip feeds the channel |
| Text | `Channel`, `TextSlot` | the text slot number into a channel while on |
| Signal | `Code` | a `UiSignal` event at the clip's start (forward play) |
| Focus | `Id` | a `UiFocus` event at the clip's start (forward play) |

`UiSwitches` is a `While<T>` column: every clip that is on is OR-ed in, and the column returns to 0 when none is. `UiChannels` uses `UiChannelKind`: the last fed value wins per channel, held channels stay live after their clip.

## Rules for a host

`UiRules.LookOf(switches, targetIds)` gives a target's look from the Ids that address it: Hide beats Show, Disable beats Enable, Remove beats Add, and anything no clip touches stays as authored. `UiRules.TouchedIds` and `UiRules.TouchedChannels` compare two frames so a host applies only what changed. `UiSelector.Parse` reads selectors (`#name`, `.class`, `Type`, words as descendants) for hosts with a retained element tree.

## Build and test on .NET

The folder is also a .NET project that compiles as C# 9 against Tl.Core alone, with the plain Tl host for tests:

```bash
dotnet test com.tl.ui/Tests~/Tl.Ui.Tests -c Release
```

The tests play one timeline with every clip and check each column bit and channel value per tick against a scalar reference, the conflict rules, that Signal and Focus fire once, and that a second play allocates 0 B. The tl submodule (`external/tl-kinds`) must be initialised.
