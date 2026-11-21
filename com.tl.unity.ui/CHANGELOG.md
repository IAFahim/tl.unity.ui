# Changelog

## 0.1.0-preview.1

### Added
- Tl UI Panel next to Unity 7's Panel Renderer: Targets (Id to selector), Class, Template and Text Slots, Channels, Signals, Commands and a Data Source. Targets resolve in the Panel Renderer's versioned UI reload callback; each frame only the Ids and channels whose columns changed are applied.
- Tl UI Timelines and its baker: the director's Playable and Also Plays become timeline slots for commands, and the object gets a `UiCommandInbox` buffer for game systems.
- `TlUiData<T>`: shows an unmanaged ECS singleton through UI Toolkit data binding, with a typed property bag and a version that changes only when the bytes change.
- Converters `tl-integer`, `tl-percent`, `tl-clock`, `tl-visible`, `tl-hidden`, registered at runtime and in the Editor for UI Builder.
- Samples Basics and Hud, each with a builder and a PlayMode test.
