# Cabinet and client WebUI integration

This reference covers the SS14 machine layer shared by HTML, WASM, and emulator games.

## Ownership and call flow

```text
Player interacts with powered cabinet
  → ActivatableUI / UserInterface opens UI key
  → client BUI creates the native DefaultWindow
  → WebView loads an exact local WebUI entry page
  → browser invokes a small allowlisted NovaBridge operation
  → client executes presentation/local behavior and responds
```

The cabinet prototype owns `ActivatableUI`, power requirements, UI key mapping, and circuit board. The shared UI key is a `Serializable, NetSerializable` enum in `Content.Arcane.Shared`. The client BUI creates its window in `Open()`. A server `EntitySystem` may close the UI on power loss; it must not execute the embedded game. Follow the existing Arcane example files linked from the main skill and consult current repository module guides before changing ownership.

## Stable identity, not browser-selected destinations

Use one compile-time game ID per cabinet/BUI. Keep a whitelist in the client window/controller. The client derives the entry URL from that ID; it does not accept arbitrary web URLs or filesystem paths from bridge payloads. Unknown IDs fail closed before creating a browser. Keep each cabinet's prototype, BUI class, localization title, frontend entry mapping, and content size in sync.

When several games share one window, a small `ClassicArcadeBoundUserInterface(owner, uiKey, gameId)` pattern can avoid duplicated window setup. The frontend receives the selected game from its native `get-state` response and maps only that known ID to a local path. A specialized game with different boot or persistence behavior can use a dedicated BUI subclass while still reusing the shell/window.

## Window and browser lifecycle

- Use the native window for title bar and close controls. Do not draw a second title bar in the game surface.
- Define logical content size per game; make resizing behavior explicit. For a fixed cabinet, disable arbitrary resize and uniformly scale/letterbox down to the viewport. Do not allow a canvas' intrinsic dimensions to enlarge the surrounding layout or crop the HUD.
- Create the `WebViewControl` only while the native window/BUI is active. Dispose bridge subscriptions and detach the browser when the window exits. Defer control-tree mutations if they occur during recursive UI updates.
- Keep load timeout, retry/error state, browser reset, and UI scale changes explicit. Never leave the user with a blank WebView on failure.
- A bridge request must not close/destroy its own control while requests are being enumerated unless the lifecycle code safely defers that operation.
- Machine power loss or range closure should follow current BUI/server semantics; it must stop timers and audio. Do not add engine modifications without explicit authorization.

## Input and focus

Embedded browser controls can consume keyboard focus and prevent game hotkeys from reaching the client. Test physical key events in the actual CEF client, not only a desktop browser.

- Define a game-specific key mapping and display it in the shell/footer.
- Forward only the intended keys to the iframe/game adapter. Prevent default browser actions for keys such as arrows/space when they are game controls.
- Preserve `keydown` and `keyup` pairs. Release all held keys on blur, iframe unload, or focus loss to avoid stuck movement/fire.
- Verify window close, Escape, global F-keys, console/backtick, chat focus, and other client shortcuts do not regress. Use an existing native-key bridge only where required and respect the native focus owner; do not globally synthesize arbitrary input.
- Restore focus predictably after closing the arcade; do not steal focus back from chat or another native window.

## Bridge design and validation

The current NovaBridge protocol sends `invoke(event, payload)` to `http://webui.local/__bridge__/<event>` with request ID and Base64 JSON. Large messages are chunked; current `WebUiBridge.TryReassemble()` sets event-specific chunk and payload limits. Confirm those current limits before sending data. Do not change a large-payload limit globally just to accommodate one game: scope it to an allowlisted event and a defensible maximum.

For each native event, document:

| Contract field | Questions |
|---|---|
| Event name | Is this event allowlisted and scoped to the expected game/window? |
| Payload | Exact fields/types, maximum lengths and sizes, empty/malformed cases? |
| Authority | Is it client-local presentation or does the server need to validate a real gameplay effect? |
| Result | What response resolves the request ID; what error code is safe to expose? |
| Lifetime | What happens if the window closes, request times out, iframe navigates, or chunks arrive late? |

Never pass arbitrary console text/commands, native filesystem paths, scripts, or URLs through the bridge. Do not build JavaScript by concatenating user-controlled strings; use JSON serialization and fixed event names. A browser-local `emit()` does not cross into C#; use `invoke()` for native actions.

## Verification checklist

- Prototype loads, machine is interactable and powered; UI key is mapped to the intended client BUI.
- Correct cabinet always opens its own fixed game and URL; invalid IDs do not open another game.
- Asset requests are local, packaged, and return correct MIME types; no unexpected external requests.
- Browser load failure, bridge error, malformed payload, timeout, close, reopen, UI scale, and power-loss paths are safe.
- Input reaches the game and global client shortcuts remain usable; blur produces key-up for all held keys.
- Canvas/art remains fully visible at preferred and reduced window dimensions.
- Build the directly affected client and any genuinely affected server/shared project. Run the game-specific browser verifier where available, then perform a manual CEF smoke test.
