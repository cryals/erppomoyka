
# Rule and skill routing

Read the root `AGENTS.md` and the nearest scoped `AGENTS.md` files first. For cross-layer work, ownership/build changes, or another non-routine workflow, consult the matching entry in `.agents/SCENARIOS.md`. Use this catalog to load only the rule files and skills for surfaces actually affected.

Rules are repository policy; skills are task playbooks. A selected skill is an execution contract for its topic. Do not read every rule or skill by default. A cross-layer task can legitimately need several skills.

## Rules by situation

- Any task: root and nearest scoped `AGENTS.md`; use `CONTRIBUTING.md` for contributor-facing project conventions.
- Work in a root `Content.*` project: consult `Modules/CATALOG.md` and read the linked owner/project guide because its documentation location under `Modules/` is not in the source project's automatic `AGENTS.md` scope.
- Ownership, project references, or module placement: `.agents/rules/architecture-and-ownership.md`; read `.agents/skills/module-architecture/SKILL.md` when assembly or ownership analysis is needed.
- Inherited file or upstream sync choice: `.agents/rules/fork-trajectory-priority.md` and `.agents/rules/arcane-edit-markers.md`.
- Reverts, restorations, cherry-picks/backports, ports, or unexplained removals: `.agents/rules/change-history-analysis.md`; add domain rules and skills for affected code and resources.
- Actual merge conflict: add `.agents/rules/merge-conflict-resolution.md`.
- C# layout, comments, ECS, or YAML format: read only the matching `.agents/rules/*conventions.md` file.
- Localization or player-visible text: `.agents/rules/content-and-localization.md` and the relevant localization skill.
- API design and access boundary: `.agents/rules/coding-and-api-design.md`.
- Third-party code or assets: `.agents/rules/third-party-materials.md`.
- Verification or handoff: `.agents/rules/verification.md` and `.agents/rules/review-and-handoff.md` when applicable.

## Skills by changed surface

- Local C# implementation: `csharp-style`; add `naming-conventions` for symbol/API naming, `performance` for hot paths, `logging-and-errors` for diagnostics, and `security-and-validation` for trust boundaries. Use `module-architecture` for project or assembly boundaries.
- ECS: use `ecs-basics` to understand an unfamiliar subsystem; use `ecs-components`, `ecs-events`, and/or `ecs-systems` for the corresponding implementation. Add `entity-api-patterns` for shared helpers/prototypes, `serialization-and-datafields` for serialized contracts, and `entity-lifecycle-and-spawning` or `entity-relations-and-links` for those lifecycle concerns.
- Client/server/shared, networking, prediction, or PVS: `client-server-shared` plus only the applicable `networking`, `prediction`, `pvs`, `security-and-validation`, and gameplay-domain skills.
- Interactions and entity behavior: `interaction-flow` for verbs/in-hand/reusable interactions; `actions-and-doafter` for actions/cooldowns/DoAfter; `containers-and-inventory` for item movement; `transform-and-physics` for coordinates/collisions; `timers-and-async` for delayed work.
- Gameplay domains: `damage-status-and-effects`, `round-and-game-rules`, `minds-roles-and-objectives`, `npc-ai`, `commands-and-cvars`, `admin-and-permissions`, `database-migrations`, `save-data-and-configuration`, `randomness-and-determinism`, or `external-services` when that domain changes.
- Content systems: `construction-and-machines`, `chemistry-and-reagents`, `atmos`, or `collections-and-datasets` for those respective systems.
- UI: `xaml-ui` for XAML controls/code-behind; `bound-user-interface` for entity-owned UI contracts and message/state flow (use both when both surfaces change); `eui` for session-oriented interfaces. Add `forms-and-input-validation` and localization skills only when those surfaces change.
- Embedded browser UI: `web-ui` for the `WebUI` submodule, generated `Resources/dist`, CEF/WebView hosting, and NovaBridge contracts. Use `web-ui-design` for visual systems, GlassUI components, icon policy, accessibility, and Svelte layout. Combine `web-ui` with `build-and-packaging` when changing the submodule, MSBuild, generated-output, or packaging path.
- Embedded arcade games: `ss14-arcade-integration` for adding local HTML/JS games, WASM titles such as DOOM, or console emulators with client-local saves to SS14 machines; combine with `web-ui`, machine/BUI, persistence, security, asset/licensing, and test skills only for the layers actually changed.
- Localization: `localization`; add `localization-in-code` when code resolves text, and `prototype-localization` when visible prototype text changes.
- Prototypes and structured resources: `prototypes` for prototype authoring, `yaml-and-schema` for other schema/config YAML, `maps-and-mapping` for maps, and `resources-and-assets` for resource ownership/validation. Add `appearance-and-visualizers`, `sprite-overlays-and-shaders`, or `audio` for those presentation assets and behaviors.
- Build, project, CI, or packaging: `build-and-packaging`; add `module-architecture` when project ownership or references change. For root `Content.*` module ownership, references, and build impact, consult `Modules/CATALOG.md` and read only the linked owner/layer guide for the affected project.
- Tests: `tests-authoring` when writing tests; `testing` when choosing or running checks. Debugging a reproduced failure uses `debugging`.
- Supplementary vendored .NET guidance: Arcane rules and skills remain authoritative. Use [C# refactoring](vendor/dotnet-skills/plugins/dotnet/skills/csharp-refactoring/SKILL.md) only for an explicit behavior-preserving refactor; [MSBuild](vendor/dotnet-skills/plugins/dotnet/skills/msbuild/SKILL.md) or [binlog diagnostics](vendor/dotnet-skills/plugins/dotnet-msbuild/skills/binlog-failure-analysis/SKILL.md) only for an MSBuild failure, build-performance investigation, or existing `.binlog`; [EF Core query review](vendor/dotnet-skills/plugins/dotnet-data/skills/optimizing-ef-core-queries/SKILL.md) for EF Core query correctness or performance; and [performance analysis](vendor/dotnet-skills/plugins/dotnet-diag/skills/analyzing-dotnet-performance/SKILL.md) only for measured .NET performance work.
- Vendored Git cleanup examples are not a preparation step or permission to delete work. For build/binlog diagnosis, follow [Git safety](rules/git-safety.md) and preserve unrelated untracked/ignored files; keep the upstream snapshot unchanged.
- Documentation and task process: `documentation` for technical docs/reports, `git-workflow` for history/staging/commit operations, and `ai-workflow` for multi-step planning, preflight, and delivery gates.
- Explicit concise-response request only: `caveman-mode`. It changes only the current response style; Arcane rules and the selected domain skills remain mandatory, and the vendored Caveman snapshot is not a canonical Arcane skill.
- Complete feature spanning layers: `gameplay-feature` plus the technical skills for the affected layers.
- Feature port or inherited upstream work: `porting` and `upstream-maintenance`, plus `git-workflow` when history/staging is part of the task and domain skills for ported code/resources.
- Review: `code-review` plus the technical skills for the changed surfaces.

## Always-on invariants

- Arcane runtime projects are root-level `Content.Arcane.Common`, `Content.Arcane.Shared`, `Content.Arcane.Server`, and `Content.Arcane.Client`. Arcane resources use existing `Resources` owner directories such as `Resources/Prototypes/_Arcane`, `Resources/Textures/_Arcane`, and `Resources/Locale/{culture}/_Arcane`. `Modules/Arcane` currently contains guidance, not runtime projects.
- Arcane-owned projects and `_Arcane` owner directories do not need Arcane edit markers. Inherited paths on the TraumaStation sync surface do; consult the marker rule for language-specific syntax and line-count details.
- Never author our change under another fork's marker. Preserve upstream markers on lines we did not change.
- `en-US` is the structural source of truth and the only locale updated by default. Change `ru-RU` only when the user explicitly requests Russian localization; then preserve the existing key contract, attributes, variables, selectors, paths, and ordering.
- Do not cross engine boundaries, invent APIs, bypass access modifiers, or expand repository searches beyond the task's evidence needs.
