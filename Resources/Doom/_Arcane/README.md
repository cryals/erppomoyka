# Arcade DOOM runtime

The JS and WASM files were obtained from the author's public demo at
https://silentspacemarine.com/ on 2026-10-28. The corresponding source project is
https://github.com/cloudflare/doom-wasm (the local source checkout was at
`65e0d3ae2ffa604155eebd96ed40da6567bd08f4`). The demo binaries have not
been proven to correspond to that exact source revision. Rebuild and replace
them from a pinned Emscripten toolchain before treating this as a reproducible
release artifact.

`websockets-doom.js` SHA-256: `A2909044A9FBC5529F941C8DBF93CC2931927690E0341C737545CF0B9CFF23FB`

`websockets-doom.wasm` SHA-256: `6366F83A58FE8596CE742A66DBF86871D315862C89C11E65B54935BE03C7E6C4`

The engine is GPL-2.0; see `ENGINE-COPYING.md`. Keep source availability and
notices with distributions containing these binaries.

`doom1.wad` here is **Freedoom Phase 1 v0.13.0**, renamed only because the
upstream loader expects that filename. It is not id Software's DOOM shareware
artwork. It was extracted from the official
https://github.com/freedoom/freedoom/releases/download/v0.13.0/freedoom-0.13.0.zip
release, entry `freedoom-0.13.0/freedoom1.wad`.

`doom1.wad` SHA-256: `7323BCC168C5A45FF10749B339960E98314740A734C30D4B9F3337001F9E703D`

See `WAD-COPYING.txt` for the content license and attribution. The original
zip contains additional author credits; retain the source archive reference
when redistributing this WAD.

`default.cfg` comes from the same `doom-wasm` source checkout. All four
runtime assets are served from the game's local resource pack through exact
allowlisted URLs. No Cloudflare network service is required at runtime.
