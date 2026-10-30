# Local HTML/CSS/JavaScript games

Use this mode for a self-contained browser game or a supplied classic-game pack that already runs in a modern browser.

## Recommended layout

```text
WebUI/public/arcade/<game-id>/
  index.html
  game.css
  game.js
  assets/...
  README.md
  LICENSE (or bundled source notices)
```

`public/` files are copied by Vite into generated `Resources/dist`; never edit that output by hand or commit generated output unless repository policy explicitly requires it. Keep original notices and provenance next to the game. Pin dependencies locally; do not add CDN script tags or remote fonts/audio.

The shared interface is `WebUI/src/interfaces/arcade/App.svelte`. It owns the shell, fixed-game selection received from C#, start/loading/failure/close states, footer and game-specific theming. It loads `/dist/arcade/<known-id>/index.html` in an iframe so legacy globals do not collide with Svelte. This iframe is not a security boundary: the game's scripts are trusted same-origin content.

## Adaptation rules

- Preserve the original game loop and controls when possible. Add a small `adapter.js` or wrapper for keyboard normalization, lifecycle, load/error messaging, and canvas fitting instead of rewriting the game.
- Handle browser key differences (`key`, `code`, legacy `keyCode`) only as needed by the game. Preserve both down/up transitions; release held keys on blur.
- Provide an explicit intrinsic game size/aspect ratio and use CSS `contain`/letterboxing. Ensure the game does not clip on the selected logical size or at a reduced viewport.
- Stop intervals, animation frames, audio, and event listeners on unload/close. Avoid duplicate boot when the player retries.
- Do not expose the game URL as caller input; the Svelte shell must derive it from the whitelisted native game ID.
- Keep UI labels and controls localized through the existing native state/FTL pattern when player-visible text is new. Do not hardcode player-facing strings in C# when a locale key is appropriate.

## Native communication

Purely local games should not need NovaBridge for every frame or input. Keep gameplay in JS. Use bridge requests only for a native operation that genuinely belongs to the client, such as close, sound preference, or client-local persistence. If an operation affects server-authoritative gameplay or rewards, define an explicit server message and validate it server-side; do not trust a browser claim.

For an iframe `postMessage` adapter, validate `event.origin`, `event.source`, the expected message shape, action allowlist, and payload bounds. Use request IDs and acknowledge asynchronous operations. Never treat a page unload request as durable confirmation: the browser may be destroyed before the host handles it.

## Current examples

- `WebUI/public/arcade/flappy_bird/`: standalone local HTML/CSS/JS game.
- `WebUI/public/arcade/asteroids/`: legacy game with bundled local dependencies/assets.
- `WebUI/public/arcade/adapter.js`: shared key forwarding and iframe lifecycle conventions.
- `WebUI/public/arcade/nu_pogodi/` (inspect actual shipped folder files): full-surface, immediate-start special case with 936×607 content, no start bezel/footer during play.
- `WebUI/scripts/verify-classic-arcades.js`: preview checks for local pages, game surfaces, input, reduced sizes, errors, and remote/missing resources.

Run `npm ci` only when installing dependencies is needed, then `npm run build`. Run the classic arcade browser verifier if available. Finally open in embedded CEF and check audio, keyboard, native close, reopen, and power behavior; desktop preview alone is insufficient.
