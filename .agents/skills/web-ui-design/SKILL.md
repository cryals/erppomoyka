---
name: web-ui-design
description: Design or review Arcane WebUI visual systems and Svelte interface layouts, including GlassUI states, accessibility, and icon policy.
---

# Arcane WebUI design

Use this skill for a visual WebUI interface, component styling, or an approved change to its design system. Do not use it for a bridge-only or build-only change.

## Source of truth

- Read `WebUI/docs/design.md` before choosing tokens, layout, components, or interaction states. Add a focused section to it when an approved decision changes the system.
- Work from the interface's native C# state contract. The frontend renders state; it must not recreate permission, visibility, unread, or enabled logic locally.
- Treat `D:\ss14gays\BandaStation\tgui` as a reference for hierarchy, spacing, responsive composition, state-driven pages, and focus handling only. Do not copy its source, Sass, fonts, sprite sheets, or assets.

## Visual and interaction invariants

- Use Font Awesome Free icons through the project's installed frontend dependency. Nerd Font glyphs are permitted only when the font is bundled and the control also has a text or accessible label. Never use PNG, raster sprites, or emoji for interactive controls.
- The native game window provides title bar and close control. Browser content starts below it; do not add a duplicate in-page title bar.
- Build a dark, restrained GlassUI: translucent layered surfaces, subtle borders, readable contrast, and deliberate group dividers. Avoid terminal scanlines, global uppercase, pixel fonts, or a pixel-art aesthetic unless the interface's approved design explicitly asks for it.
- Define default, hover, pressed, keyboard-focus, disabled, danger, unread, pending, and overflow states before treating a component as finished. Respect `prefers-reduced-motion`; transition state, not layout.
- Keep pointer targets at least 40 px high, preserve logical Tab order, expose disabled reasons as text/tooltips, and leave labels localizable.

## Escape Menu

- Preserve every native menu action, separator, visibility condition, disabled reason, keyboard path, and changelog unread behavior. `ChangelogManager` is authoritative; opening the menu alone must not consume unread state.
- Render actions from host-provided ordered groups. Keep destructive actions in a separate bottom group and do not infer an action from its displayed label.
- Reuse child windows and native actions rather than rebuilding them as browser pages unless the task explicitly migrates that child interface.

## Review

Check the actual CEF surface at its smallest supported height, at normal scale, with keyboard-only navigation, an unavailable optional action, a disabled action with a reason, and a live state update. A browser-only Vite preview is not sufficient proof of native usability.
