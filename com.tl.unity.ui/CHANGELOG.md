# Changelog

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
- Samples Basics, Hud and Inventory, each with a builder and a PlayMode test. Hud is authored entirely in UXML, with no Targets or Commands rows.
