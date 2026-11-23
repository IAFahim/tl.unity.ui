# Changelog

## 0.3.0-preview.5

### Changed
- Version only, to stay in step with `com.tl.unity.ui`.

## 0.3.0-preview.4

### Changed
- Version only, to stay in step with `com.tl.unity.ui`.

## 0.3.0-preview.3

### Changed
- Version only, to stay in step with `com.tl.unity.ui`.

## 0.3.0-preview.2

### Changed
- Version only, to stay in step with `com.tl.unity.ui`.

## 0.3.0-preview.1

### Changed
- Breaking: every UI clip names its target with `UiTargetRef Target` instead of `byte Id`. Class, Template and Text slots are `UiClassRef`, `UiTemplateRef` and `UiTextRef`. These are one-byte structs, so editors can draw them as names.
- Breaking: Animate is `Target`, `Property` (`UiProperty`), `From`, `To`, `Ease`, `Hold`. It writes `From + (To - From) * Ease(progress)`.
- Breaking: `UiChannels` holds 16 slots keyed by (target, property) (`UiChannelKey`, `UiChannelKeys`), not 16 numbered channels. `UiRules.TouchedTargetsOfChannels` replaces `TouchedChannels`. `UiChannelSlots.Write` sets one from code.

## 0.2.0-preview.1

### Changed
- Opts into the Timeline **+** menu with `[assembly: Tl.Unity.TlMenuEntries]`, in Unity builds only; the .NET build ignores it. Needs Unity 7000.0.0a20 and `com.tl.unity` at `765657a`.

## 0.1.0-preview.5

### Changed
- Version only, to stay in step with `com.tl.unity.ui`.

## 0.1.0-preview.4

### Changed
- Version only, to stay in step with `com.tl.unity.ui`.

## 0.1.0-preview.3

### Changed
- Version only, to stay in step with `com.tl.unity.ui`.

## 0.1.0-preview.2

### Changed
- Moved, with `com.tl.unity.ui`, to its own repository, IAFahim/tl.unity.ui. No code change.

## 0.1.0-preview.1

### Added
- Show, Hide, Enable, Disable, Add Class, Remove Class and Attach clips feeding one `While<UiSwitches>` column.
- Animate and Text clips feeding `UiChannelKind` (16 channels, Hold lands on the curve's last key at the clip's end).
- Signal and Focus clips raising `UiSignal` and `UiFocus` events.
- `UiRules` (look per target, touched Ids and channels, reveal length) and `UiSelector`.
- .NET tests on the plain Tl host.
