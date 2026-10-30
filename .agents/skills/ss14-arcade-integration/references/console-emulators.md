# Console emulators and Game Boy Advance

Use this mode when an arcade runs a console emulator (for example EmulatorJS with the mGBA core) and must retain cartridge saves.

## Emulator and ROM packaging

- Pin the emulator frontend/core version and keep the required JS, CSS, WASM/data files, and runtime configuration local. Include upstream license/source notices and any applicable offer/compliance material for bundled emulator/core code.
- Record ROM provenance and explicit redistribution permission. A user's assertion/permission is evidence to record, not an independently verified legal conclusion. If rights are ambiguous, stop before committing or distributing the ROM and ask for lawful replacement/confirmation.
- Disable unneeded remote core lookup, CDN, downloads, IndexedDB, localStorage, netplay, save-state UI, and other browser persistence if the intended design uses native client-local SRAM. Verify that disabling these does not prevent the selected core from running offline.
- Define a fixed game/core and ROM path in code. Never accept arbitrary ROM paths or web URLs from the game iframe.

## Save model: cartridge SRAM is not an emulator save-state

The game writes cartridge SRAM/flash through the emulated console. Emulator save-states are a separate feature and must not be confused with in-game progress. Persist only the cartridge save bytes unless the feature explicitly requires save-state support.

Recommended Arcane flow:

```text
client reads local UserData generation
 → NovaBridge `firered-save-load`
 → Svelte passes bounded Base64 payload to the emulator iframe
 → emulator applies SRAM after core/filesystem are ready
 → iframe snapshots SRAM on checkpoint / requested close
 → same-origin postMessage to Svelte shell
 → NovaBridge `firered-save-write` (chunked when needed)
 → client validates, writes unique temp, flushes, atomically renames
 → native success response → iframe receives save-result/ack
```

The native `UserData` save belongs to this client installation. It is not automatically server-side, account-synced, or shared across devices. Communicate that limitation clearly.

## Persistence requirements

- Establish a conservative maximum SRAM size and reject empty, malformed, oversized, or unexpected payloads both in JS and C#.
- Persist to client `UserData`, never an absolute developer-machine path, browser profile, game asset directory, or server filesystem.
- Write a unique temporary generation, flush/close it, then atomically rename/commit it. Never replace the last known-good generation with partial data.
- Retain a small number of complete generations and load newest valid first; fall back to an older valid generation. If candidates exist but none are readable, surface a load failure rather than silently starting fresh and later overwriting the only progress.
- Autosave periodically (Arcane's current FireRed interval is 60 seconds) and request an immediate snapshot on the in-page close action. Do not claim that `pagehide` alone is durable: closing/destroying CEF can cancel pending JavaScript/bridge requests.
- Do not close after a requested final save until an explicit success acknowledgement arrives; if it fails/times out, keep the game open and tell the player the save was not confirmed.
- Serialize concurrent snapshots or queue them. Track each bridge request/flush ID so a stale result cannot acknowledge a newer write. Always clear pending state on success, failure, timeout, and iframe teardown.
- Log safe operational milestones on the native side: loaded byte count, applied byte count if the page can confirm it, successfully committed byte count, and full exception context on failures. Never log the entire save payload.

## Emulator timing and API specifics

Do not assume `getSaveFile()` returns a detached snapshot or that it is synchronous; inspect the pinned emulator version's implementation. In the current vendored EmulatorJS build, `gameManager.getSaveFile()` defaults to invoking `saveSaveFiles()` before reading the virtual filesystem path; `getSaveFile(false)` skips that synchronization. Preserve the needed flush behavior unless the selected version's source proves another safe sequence.

Apply an existing SRAM only after the emulator manager, filesystem, and save path exist. Write bytes to the exact save path and invoke the emulator's supported load/import hook; verify the method names against the pinned local version. Report success to the host only after application succeeds. Missing save is a normal new-game state; malformed existing save or native read failure is not the same thing.

## Large payload bridge

SRAM can be too large for one bridge URL. Use the bridge's chunked transport with request IDs, event allowlisting, per-game maximum chunk count/part size, stale-buffer cleanup, a bounded total payload, and a 10-second-or-suitable request timeout. Match frontend chunking to native reassembly limits. Avoid raising global limits for every WebUI page. Test partial, duplicate, out-of-order, malformed, oversized, timed-out, and complete transfers.

## Expected verification

1. Start with no save: logs say no save/new game; game starts normally.
2. Create an in-game save; verify an autosave success log with byte count.
3. Use in-page close; verify it waits for confirmed write; reopen and verify save found and applied.
4. Restart the client and repeat; confirm data remains under that same installation's UserData.
5. Simulate missing, truncated, corrupt-newest plus valid-older, all-corrupt, oversized, and write-failure cases. Confirm fallback/rejection and no silent destructive reset.
6. Check client logs for save/load success and error text without exposing the SRAM bytes.

Arcane examples: `WebUI/public/arcade/firered/game.js`, `WebUI/src/interfaces/arcade/App.svelte`, and `Content.Arcane.Client/Arcade/FireRedSaveStore.cs`.
