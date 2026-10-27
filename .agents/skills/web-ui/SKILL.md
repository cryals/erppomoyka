---
name: web-ui
description: Integrate, design, or modify Arcane's embedded WebUI frontend and its CEF bridge, including Svelte interfaces, WebView hosting, and WebUI build output.
---

# Arcane WebUI

Use this skill for work on the `WebUI` submodule, its generated `Resources/dist` output, the C# WebView host, or an interface rendered through the embedded browser. Do not use it for regular XAML/BUI work that does not involve WebUI.

## Ownership and boundaries

- `WebUI` is a separate frontend submodule. Its source, lockfile, and frontend dependencies stay in that checkout; its production output is generated into root `Resources/dist` and is not hand-edited.
- The client owns browser lifetime, input routing, native action dispatch, resource serving, and JavaScript responses. Server authority does not move into WebUI.
- Before adding a project reference, MSBuild target, resource handler, or packaging behavior, verify the existing `Content.Client` owner, the WebView API pinned in `RobustToolbox`, and the current build workflow.
- Handle `http://webui.local/` requests only from the embedded control, serve only approved generated assets, and validate every bridge event and payload before calling a native action.
- `NovaBridge` is the frontend contract: requests travel to the native host via `http://webui.local/__bridge__/...`; responses and push updates return through `ExecuteJavaScript` as `nova:*` CustomEvents. Keep request IDs, JSON/base64 encoding, timeouts, and lifecycle behavior compatible unless the contract changes deliberately on both sides.

## Interface design rules

- Use Font Awesome Free icons by default. Nerd Font glyphs are allowed only when their required font is bundled and the glyph has an accessible text label. Do not use PNG, raster sprites, or emoji as icons for interactive UI controls.
- Buttons use an icon plus localized text; an icon alone is not the sole affordance unless an accessible name is provided.
- The game owns the outer window chrome. WebUI pages must not draw a duplicate title bar unless a task explicitly asks for a standalone page header.
- Escape Menu uses the established dark minimal GlassUI language: translucent dark surface, restrained border and shadow, clear group dividers, generous hit targets, and explicit hover, focus, disabled, danger, and unread states.
- Preserve existing behavior when migrating native UI: every action, visibility/permission condition, disabled reason, localized label, separator, keyboard path, and notification state must be represented in the bridge contract and tested.
- Changelog unread state is live state, not cosmetic text. Subscribe to the native `ChangelogManager` state, mark it read only after the changelog has actually opened, and push the updated state to an already-open WebUI page.

## Design references and licensing

- `D:\ss14gays\BandaStation\tgui\packages\tgui-escape-menu` is a behavioral and visual reference: study hierarchy, grouping, scaling, focus, and state transitions.
- Do not copy BandaStation/TGUI source, Sass, fonts, sprites, icons, or compiled assets into Arcane/WebUI. Reimplement the intended appearance and interactions using Arcane-owned code and dependencies.
- Read `WebUI/docs/design.md` before changing WebUI visuals or contracts; update it when an approved design decision changes. Use `web-ui-design` for a new visual surface or a deliberate design-system change.

## Verification

- Frontend-only changes: use the submodule's lockfile-based install and its production build; confirm the expected generated entry page is present in `Resources/dist`.
- Bridge/client changes: build the affected client project and manually exercise page load, Escape open/close, each action, keyboard focus, failed bridge response, and live changelog-state update.
- Do not treat a Vite build as proof that CEF rendering, input routing, or native actions work.
