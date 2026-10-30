---
name: ss14-arcade-integration
description: Integrate local HTML/JavaScript games, WASM games such as DOOM, or console emulators such as Game Boy Advance into Arcane SS14 arcade cabinets through the client WebUI.
metadata:
  short-description: Integrate games into SS14 arcade machines
---

# SS14 Arcade Game Integration

Use this skill when adding or repairing a playable game in an Arcane SS14 machine using the embedded WebUI. It covers local HTML/CSS/JS games, native WebAssembly games, and console emulators with persistent cartridge saves. It does not apply to ordinary WebUI screens or server-authoritative gameplay.

## Select the integration mode

- **HTML/CSS/JS game:** keep the game as static local assets under `WebUI/public/arcade/<slug>/`; host it in the shared arcade shell's isolated iframe. Read [references/html-games.md](references/html-games.md).
- **WASM game (DOOM-like):** keep the loader, WASM, data files, and configuration local; use an allowlisted asset mapping in the native window. Validate CEF/WASM, input, canvas sizing, and local loading. Read [references/wasm-games.md](references/wasm-games.md).
- **Console emulator (GBA-like):** vendor/pin the emulator and core, load ROM/runtime locally, and persist cartridge SRAM through an explicit client bridge. Read [references/console-emulators.md](references/console-emulators.md).
- For any new physical machine or circuit board, also read [references/cabinet-integration.md](references/cabinet-integration.md).

## Non-negotiable architecture

- The arcade machine's BUI owns the native window and its lifetime. The client hosts/renderers the game; the server only authorizes opening/closing the BUI and enforces actual machine rules such as power. Never move emulator execution, game simulation, or arbitrary browser requests onto the server.
- Select a fixed, code-owned game ID from a whitelist. Never accept a URL, asset path, script, command, or filesystem path from a client/web page as a game selector.
- Ship all runtime files locally. No CDN, remote script/font, external emulator core, or runtime ROM download. The CEF host serves approved packaged resources through `http://webui.local/`; `Resources/dist` is generated from WebUI source and is not hand-edited.
- A same-origin iframe is a code-isolation convenience, not a security sandbox against shipped same-origin scripts. Treat every bundled game as trusted third-party code: review provenance, license, included scripts, network behavior, and write down required notices before distributing.
- Native bridge requests are untrusted input. Keep event names and payloads allowlisted, bound lengths and chunk counts, validate again in C#, and expose only the narrow operation needed. `novaBridge.invoke()` makes native requests; `novaBridge.emit()` dispatches a browser-local event and is not a native call.
- Preserve normal machine behavior: power loss closes the BUI, closing the window stops game timers/audio, keyboard focus is returned safely, and progress is not reported as saved until a durable local write succeeds.
- Games are client-local and independent unless the task explicitly defines a server feature. Do not invent score rewards, networking, persistence roaming, or shared sessions.

## Workflow

1. Inspect current Arcane prototypes, UI key, server system, client BUI/window, the WebUI build pipeline, local asset host, and the relevant existing game example before designing new abstractions.
2. Establish the artifact format and its rights/provenance. For supplied files, record the source and license/permission evidence; do not infer redistribution rights from public availability.
3. Choose one of the modes above and keep content under the owner already used by the arcade system. Reuse the cabinet, BUI key, window, bridge, and WebUI shell where compatible.
4. Define a fixed ID, title, content aspect/size, keyboard mapping, lifecycle, offline behavior, error state, and persistence contract before wiring it in.
5. Implement the narrowest adapter possible. Keep the original game's mechanics and assets intact unless conversion is needed for compatibility, licensing, input, or layout.
6. Verify static build, local/offline requests, game start and focus, gameplay keys and key-up/blur behavior, resize/letterboxing, close/reopen, power loss, and error paths. For a saved game, test save → client restart → load and corrupted/missing-save recovery.
7. Build `WebUI` and the directly affected `Content.Arcane.Client` project. A browser preview does not prove embedded CEF, native keyboard routing, packaged resources, or client storage works; state any remaining manual gates explicitly.

## Arcane implementation references

Use these as examples to inspect and adapt, not as a requirement to duplicate every detail:

- Native shared game window and game whitelist: `Content.Arcane.Client/Arcade/DoomArcadeWindow.cs`.
- BUI per cabinet: `Content.Arcane.Client/Arcade/DoomArcadeBoundUserInterface.cs` and `ClassicArcadeBoundUserInterface.cs`.
- Server power/lifetime rule: `Content.Arcane.Server/Arcade/DoomArcadeSystem.cs`.
- Shared UI key: `Content.Arcane.Shared/Arcade/DoomArcadeUiKey.cs`.
- Cabinet prototypes: `Resources/Prototypes/_Arcane/Entities/Structures/Machines/doom_arcade.yml` and `classic_arcades.yml`.
- Svelte shell and iframe/bridge lifecycle: `WebUI/src/interfaces/arcade/App.svelte`.
- HTML game adapter and local examples: `WebUI/public/arcade/adapter.js`, plus folders such as `flappy_bird/`, `asteroids/`, and `nu_pogodi/`.
- FireRed emulator configuration, local SRAM and store: `WebUI/public/arcade/firered/`, `Content.Arcane.Client/Arcade/FireRedSaveStore.cs`.
- DOOM files are served from `Resources/Doom/_Arcane/` through the explicit allowlist in `DoomArcadeWindow.cs`; the shell is `WebUI/src/interfaces/doom-arcade/`.
- Design and operational constraints: `WebUI/docs/design.md` and per-game README/license files.

## Related repository skills

Add `web-ui` for WebView/NovaBridge/build changes; `bound-user-interface` and `construction-and-machines` for machine/BUI/prototype work; `save-data-and-configuration` for persistent saves; `security-and-validation` for bridge trust boundaries; `resources-and-assets` and `third-party-materials` for assets/licensing; `testing` when selecting checks; and `build-and-packaging` when the build or resource packaging path changes. Select only those triggered by the task.
