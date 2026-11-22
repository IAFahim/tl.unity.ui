# Tl UI

Engine-free UI clips and rules for Tl timelines. This package holds the part of a timeline-driven UI that does not depend on any engine: what each clip writes, how clips combine, and what a target should look like. Any engine host applies the result to its own UI; `com.tl.unity.ui` is the Unity one.

## What the clips write

Target, Class, Template and Text are one-byte structs, so a host's editor can draw them as names; Unity's Inspector does.

| Clip | Fields | Writes |
|---|---|---|
| Show, Hide (Keep Space), Enable, Disable | `Target` (`UiTargetRef`, an Id 0 to 63) | a bit for the Id in the matching mask of the `UiSwitches` column while the clip is on |
| Add Class, Remove Class | `Target`, `Class` (`UiClassRef`) | a bit in the slot's add or remove mask |
| Attach | `Target`, `Template` (`UiTemplateRef`) | a bit in the slot's attach mask |
| Animate | `Target`, `Property`, `From`, `To`, `Ease` (curve 0 to 1), `Hold` | `From + (To - From) * Ease(progress)` into `UiChannels` under the key (target, property) while on; with Hold, To at the clip's end, kept until another clip feeds that key |
| Text | `Target`, `Text` | the text slot number under the key (target, Text) while on |
| Signal | `Code` | a `UiSignal` event at the clip's start (forward play) |
| Focus | `Target` | a `UiFocus` event at the clip's start (forward play) |

`UiSwitches` is a `While<T>` column: every clip that is on is OR-ed in, and the column returns to 0 when none is. `UiChannels` uses `UiChannelKind`: 16 slots keyed by (target Id, property) (`UiChannelKey`); the last fed value wins per key, held keys stay live after their clip. Game systems write the same struct with `UiChannelSlots.Write`.

## Rules for a host

`UiRules.LookOf(switches, targetIds)` gives a target's look from the Ids that address it: Hide beats Show, Disable beats Enable, Remove beats Add, and anything no clip touches stays as authored. `UiRules.TouchedIds` and `UiRules.TouchedTargetsOfChannels` compare two frames so a host applies only what changed. `UiSelector.Parse` reads selectors (`#name`, `.class`, `Type`, words as descendants) for hosts with a retained element tree.

## Build and test on .NET

The folder is also a .NET project that compiles as C# 9 against Tl.Core alone, with the plain Tl host for tests:

```bash
dotnet test com.tl.ui/Tests~/Tl.Ui.Tests -c Release
```

The tests play one timeline with every clip and check each column bit and channel value per tick against a scalar reference, the conflict rules, that Signal and Focus fire once, and that a second play allocates 0 B. The tl submodule (`external/tl-kinds`) must be initialised.
