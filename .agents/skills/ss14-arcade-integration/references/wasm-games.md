# WASM games such as DOOM

Use this mode when the game engine itself is WebAssembly and needs a loader, game data, configuration, and an interactive canvas.

## Package every runtime dependency locally

Keep browser code under the WebUI's local `public` tree when Vite should package it. Large data or explicit native-served assets can live in an Arcane-owned resource directory (the current DOOM example uses `Resources/Doom/_Arcane/`). Enumerate each native-served resource in a fixed allowlist in the client window; never derive resource paths from a page query parameter or user payload. Confirm MIME types (`application/wasm` for `.wasm`, correct JS type, and explicit binary type for game data) in the resource handler.

The page should load from the local WebUI origin and request only its own packaged files. Disable/unavailable network multiplayer, WebSockets, or remote update mechanisms unless explicitly required and safely supported. Do not assume CEF supports every browser feature: test the exact pinned client/WebView runtime, WASM compilation, memory mode, threads, audio, fullscreen, pointer lock, and keyboard behavior. Prefer single-threaded WASM when shared memory/thread support is unavailable.

## Boot and failure contract

1. Native window creates the WebView and bridge.
2. Svelte obtains native state; the page presents a start/ready overlay where autoplay or focus restrictions require user interaction.
3. Loader fetches only local, approved JS/WASM/data/config assets.
4. A bounded boot timer reports a visible failure and offers a safe retry.
5. Once the canvas is live, contain its intrinsic pixel buffer inside a fixed aspect-ratio surface; letterbox rather than crop HUD or gameplay.
6. On close/failure, stop the loop and audio, remove handlers, and dispose the iframe/browser cleanly.

Map keyboard keys explicitly and test their physical delivery in CEF. Pointer-lock is not automatically available in a game window and can conflict with SS14 input. If the task requires keyboard-only, provide the required controls without relying on pointer lock or browser fullscreen.

## DOOM example in Arcane

- C# host/window: `Content.Arcane.Client/Arcade/DoomArcadeWindow.cs`.
- WebUI interface: `WebUI/src/interfaces/doom-arcade/`.
- Local runtime/data mapping: `Resources/Doom/_Arcane/` mapped to fixed `/assets/doom/...` paths.
- Layout verifier: `WebUI/scripts/verify-doom-layout.js`; it uses the real local WASM and checks canvas containment at multiple viewport sizes.
- The native window owns chrome and size; WebUI does not draw a duplicate native title bar.

Treat the game data file (for example, WAD) as a separate licensed asset from the loader and WASM. Preserve third-party source and asset notices. Verify distribution rights before adding a file, regardless of whether it can be downloaded freely.

## Tests beyond browser preview

- production build includes the entry page, loader, WASM, and all data/config dependencies;
- offline launch produces no request to an external host;
- WASM starts repeatedly without white frames, freezes, or unbounded memory growth;
- canvas/HUD fits at target and reduced logical sizes;
- keyboard controls and SS14 global shortcuts both work;
- audio ends when the game window closes;
- client packaged-resource requests return correct status and MIME types;
- CEF on the supported client build is tested separately from Playwright/local preview.
