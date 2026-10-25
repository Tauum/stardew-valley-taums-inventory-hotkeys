# Menu Supreme: code review and CurseForge release plan

## Current status (25 October 2026)

The project is now named Taum's Inventory Hotkeys and its manifest is version 1.0.0. This document's original line references and packaging evidence describe the 24 October snapshot; they are not validation of the renamed build.

Progress is marked beside each finding below. A source-level change is not an in-game validation.

Reviewed: 24 October 2026. Scope: all 1,988 lines of `Taums-menu-supreme/ModEntry.cs`, the project and manifest, build configuration, existing design notes, and generated package contents.

**Recommendation: finish a stabilization pass before submitting a public release.** The main blockers are input handling, modal/controller behavior, menu lifecycle, and the declared SMAPI minimum. Splitting the file will make these easier to maintain, but moving methods alone will not fix them.

This review changes no runtime source or configuration. Findings below distinguish source evidence from behavior still requiring an in-game test. Source line numbers refer to this review's snapshot.

## Validation completed

- Release build passed: `dotnet build Taums-menu-supreme/Taums-menu-supreme.csproj --configuration Release --no-restore`.
- Two warnings remain: CS9057 (the SMAPI analyzer needs a newer compiler) and CS8603 (`ModEntry.cs:1601`, a tooltip delegate returning null).
- Installed SDK: 6.0.428. The analyzer references compiler 4.9; the current compiler is 4.3. A successful compilation therefore does not mean the SMAPI analyzer ran successfully.
- Generated an audit ZIP with deployment disabled and ZIP output directed to `artifacts/review`.
- ZIP inspection passed: exactly `Taums-menu-supreme/manifest.json` and `Taums-menu-supreme/Taums-menu-supreme.dll`. No game assemblies, player config, reference mods, or design files are bundled. Inventory: `artifacts/review/package-contents.json`.
- No automated test project currently exists. No in-game, controller, multiplayer, or performance profiling was performed during this review.
- The audit ZIP is evidence of packaging behavior, not a release candidate that has passed gameplay testing.

## Findings that should block the first stable release

### [ ] R1. Keyboard name entry has functional defects — high priority

- [x] Remove keyboard B from the cancel path and route name editing through Stardew's native naming controls. The custom naming wrapper now delegates controller input to the game's menu without inspecting its private fields.
- [ ] Verify text input, prefill replacement, and cancellation in game.

Evidence: `ModEntry.cs:185`, `ModEntry.cs:942`.

The editor treats `SButton.B` as Cancel before name input is handled. Typing B therefore closes the editor or dismisses deletion. `HandleNameTyping` appends the enum name of a key, so letters are uppercase, normal number-row keys such as D1 are not text, and punctuation, keyboard layouts, paste, and IME input are unsupported. This is a source-confirmed routing defect.

Use the game's normal text-input subscriber/TextBox path for keyboard entry, keeping the native naming menu for controller entry. Separate text focus from navigation focus. Reserve keyboard Escape for cancellation; controller B must remain distinct from keyboard B. Preserve the requested replace-prefill-on-first-type behavior.

### [ ] R2. Delete confirmation is not a controller modal — high priority

- [x] Source check: the confirmation menu has separate Delete and Cancel focus components, starts on Cancel, routes controller A/B and directions, and restores Delete focus on cancel. Mouse-wheel icon changes are blocked while it is open.
- [ ] Verify those controls in game before marking R2 complete.

Evidence: `ModEntry.cs:116`, `ModEntry.cs:127`, `ModEntry.cs:775`, `ModEntry.cs:1798`, `ModEntry.cs:1864`.

The confirmation has mouse rectangles but no controller focus components. Direction inputs still navigate the underlying editor. A still activates its currently focused editor control, whose center generally does not fall inside either confirmation button. If the name control is focused, A can open naming while deletion is pending.

Give confirmation its own two-button focus graph, defaulting to Cancel. Route A, B, arrows, and pointer input exclusively to that graph until dismissed; restore focus to Delete afterward. Likewise, prevent mouse-wheel icon cycling beneath confirmation/keybind overlays (`ModEntry.cs:1244`).

### [ ] R3. Controller components can be left behind when inventory instances change — high priority

- [x] Remove old snap components before populating a replacement inventory page; restore the native neighbor only while this mod still owns it.
- [ ] Reopen inventory repeatedly and inspect component IDs and counts in game.

Evidence: `ModEntry.cs:356`, `ModEntry.cs:636`, `ModEntry.cs:1309`, `ModEntry.cs:1537`.

`RepositionTabs` adds custom components to the new page before `AddHotkeyToSnapGraph` notices that the menu instance changed. Cleanup then removes those tracked components from the old page and clears the dictionary. The new page can retain the previously added instances, after which fresh components with the same IDs are added. This is a source-derived duplicate-component path; verify by reopening inventory repeatedly and inspecting IDs/counts.

Attach/detach the menu session before building layout or adding components. Cleanup must target the page that owns each component. Also avoid repeatedly adding the native tab when `addedInventoryTabToClickableSet` is true (`ModEntry.cs:1337`). Verify that cleanup restores a native neighbor only when it is still the value owned by this mod, to avoid overwriting another mod's later change.

### [ ] R4. Layout updates do not update editor controller targets — high priority

- [x] Refresh editor controller bounds after layout updates.
- [ ] Handle small screens and long text, then test resize and UI scale in game.

Evidence: `ModEntry.cs:637`, `ModEntry.cs:1792`, `ModEntry.cs:1935`.

Draw recalculates rectangles, but editor focus components copy their bounds only in the constructor. A resize or UI-scale change can move the visible control while controller activation still uses the old rectangle. Fixed offsets such as the keybind row at Y+456 do not shrink when the panel height does. Four upward hotkey rows also have no viewport containment policy.

Calculate one layout snapshot used by drawing, hit testing, and controller targets. Rebuild/update it on resize, scale, menu changes, and hotkey changes. Define a small-screen policy that keeps controls and all supported rows reachable while preserving the accepted layout at the current resolution. Wrap or truncate long names, confirmation prompts, and displayed keybinds.

### [ ] R5. The manifest promises an unsupported SMAPI baseline — high priority

- [x] Raise `MinimumApiVersion` from 4.0.0 to 4.5.2.
- [ ] Verify the declared game and SMAPI compatibility in game.

Evidence: `manifest.json:8`, `ModEntry.cs:1174`.

The manifest declares SMAPI 4.0.0, but the implementation calls `IInputHelper.Press`, which is absent from the published 4.0.0 interface. Set the floor to a version actually tested with this API. Using the current tested installation, 4.5.2, is the conservative first-release choice; lower it only after verification. Declare/test game compatibility alongside it.

Primary-source comparison: [SMAPI 4.0.0 input interface](https://github.com/Pathoschild/SMAPI/blob/4.0.0/src/SMAPI/IInputHelper.cs), [current input interface](https://github.com/Pathoschild/SMAPI/blob/develop/src/SMAPI/IInputHelper.cs).

### [ ] R6. GMCM integration depends on private implementation details — high priority

- [x] Mark GMCM as required and forward keyboard capture events to its overlay.
- [ ] Harden the private overlay calls and test the installed GMCM version.

Evidence: `ModEntry.cs:871`, `ModEntry.cs:933`, `ModEntry.cs:1222`, `manifest.json:9`.

The exact keybind sub-modal is constructed through GMCM's internal type name and constructor. Draw, click, completion, and button methods/properties are found through reflection. Depending on GMCM does not make this a public API contract. Missing members can silently do nothing, while invocation exceptions can escape the event handler.

Keep the exact requested GMCM UI, but put this integration behind one adapter with a tested version range, cached members, explicit capability checks, guarded invocation, and a clear failure path. Verify controller acceptance/cancellation of the captured binding: the current routing sends A/X/Y to capture, but does not provide its own confirmation focus controls.

For the first release, recommend a tested GMCM dependency if exact parity is required. Keep it optional only if the fallback is deliberately supported and documented: the current fallback captures one button immediately and does not provide the same chord, alternative-binding, or confirmation workflow. Do not copy the whole third-party UI implementation as a shortcut.

### [ ] R7. Input ownership and editor cleanup need one lifecycle — high priority

- [x] Clear capture state when the editor closes or the game returns to title.
- [ ] Consolidate input ownership and test menu changes and cancellation in game.

Evidence: `ModEntry.cs:106`, `ModEntry.cs:310`, `ModEntry.cs:742`, `ModEntry.cs:766`, `ModEntry.cs:1620`, `ModEntry.cs:1956`.

The global SMAPI handler and `receiveLeftClick` both contain click-entry paths; suppression is relied upon to prevent duplicate processing. The pointer handler accepts saved rectangles for any GameMenu, although rendering only exposes them on Inventory. Rectangles and snap state are mostly cleared during rendering rather than when the menu/page changes, leaving a stale-input window.

Name input replaces the active menu and restores the editor only on acceptance. Its cancel/close path needs an explicit test and restoration policy. Editor close/title cleanup does not reset every capture/overlay field. Draw manually redraws a saved parent GameMenu, so menu ownership must remain explicit.

Have one owner dispatch each input once to the topmost active mode. Validate inventory page and owning menu identity at input time. Handle menu changes, editor close, naming cancellation, return to title, and save transitions centrally; clear pending capture and dispatch state as appropriate. Test close readiness while an inventory item is held.

### [ ] R8. GMCM config changes are saved before the user presses Save — medium priority

- [x] Hold keybind changes in drafts until Save; Reset restores defaults.
- [ ] Verify cancel behavior, migration, icon validation, and failed writes.

Evidence: `ModEntry.cs:1591`, `ModEntry.cs:1614`, `ModEntry.cs:1635`.

The keybind setter writes config immediately; canceling GMCM can therefore leave changes on disk. The registered Reset callback reloads the file rather than restoring defaults. `Normalize` silently truncates lists over 40 and accepts arbitrary icon indices up to 100000; drawing then clamps these to the last available emoji.

Separate committed config, GMCM working values, and editor drafts. Persist on explicit Save; cancel must discard drafts. Define Reset behavior and preserve/migrate existing configurations before removing legacy fields. Validate icon IDs against the catalog. Handle malformed config and write failures without silently overwriting the user's last working configuration.

## Compatibility and behavior to verify before claiming support

1. **Shortcut delivery:** the existing close-inventory, wait, then `Helper.Input.Press` approach should stay. Select a usable bound alternative rather than blindly taking the first; test chords, physical keys already held, controller bindings, and cancellation if another menu opens during the delay. A log showing a sent key does not prove the target mod opened. SMAPI documents one-tick simulated input, and there is an upstream report involving simulated input and menu navigation; that report is a test lead, not proof this mod has the same bug. See [SMAPI input contract](https://github.com/Pathoschild/SMAPI/blob/develop/src/SMAPI/IInputHelper.cs) and [upstream issue 1016](https://github.com/Pathoschild/SMAPI/issues/1016).
2. **Split screen:** all UI, focus, parent-menu, tooltip-mode, and pending-dispatch fields currently belong to one mod instance. They are not isolated per screen. Either isolate session state with SMAPI's per-screen utilities or explicitly exclude split screen from the first release. Remote multiplayer clients still need separate testing.
3. **Input hints:** `OnCursorMoved` always switches to mouse hints, even though controller snapping moves the cursor. Verify whether snapping raises this event on the supported build and distinguish deliberate pointer movement if necessary. Keyboard Y/controller Y are hardcoded; make the toggle configurable if it conflicts with another mod's inventory controls.
4. **Emoji assets:** the catalog is a hardcoded 196-entry contiguous range and a cached texture. Verify against the actual chat picker for the supported game build and texture-mod combinations. Invalidate cached assets when the game's content changes; preserve saved emoji identity if the catalog evolves.
5. **Other menu mods:** menu discovery guesses from reflected collections and geometry. Centralize that in a tested GameMenu adapter; do not claim support for arbitrary menu replacements. Repeated polling of every collection is not a substitute for a compatibility contract.

## Dead code and redundant paths

These are source-review candidates for removal/consolidation, with semantic reference checks before editing:

| Candidate | Evidence | Action |
| --- | --- | --- |
| `RegisterGenericModConfigMenu(includeDraft)` draft branch | Line 1572; every caller omits the argument | Remove the unused parameter and branch at line 1604. |
| `OpenModMenuAsChildMenu` API declaration | Line 1984; no callers | Remove from the local API interface unless a planned public-API integration needs it. |
| `HubConfig.Columns` and `Rows` | Lines 1629–1638; layout/navigation use fixed 10/4 | Treat as legacy serialized fields; migrate or retire them, rather than exposing ineffective settings. |
| Late Left/Right editor branch | Line 208; direction/capture paths return earlier | Remove the unreachable fallback after choosing the authoritative navigation path. |
| `StardewValley.BellsAndWhistles` import | Line 7; no referenced symbols from it | Remove unused import. |
| Identity emoji index list | Lines 74, 1123, 1240 | Compute indices directly while the catalog is contiguous, or introduce a real catalog mapping when needed. |
| Duplicate keybind hit rectangle | Lines 55 and 657 | Use one bounds value unless expanded hit padding is intentional. |
| Two tooltip layout implementations | Lines 1395 and 1451 | Share measuring, wrapping, viewport fitting, and text/glyph row drawing. The visibility tooltip's true branch currently has no caller. |
| Repeated tab discovery/focus helpers | Lines 443, 479, 492, 557, 1517 | Share cached menu references and one focus operation. |
| Temporary simulation-tracking collections | Lines 75–76, 1182, 1213 | Remove after compatibility verification or keep only behind a diagnostics setting with bounded lifetime. |
| Repeated snap-neighbor and manual navigation rules | Lines 230, 1380, 1850, 1864 | Generate both from one navigation model to prevent direction mismatches. |
| Stale terminology/comments | `InventoryTabEditorMenu`, `HubTab`, dice comment at 1143 | Rename internal concepts to Hotkey; preserve existing JSON fields until migrated. |

The GMCM fallback, parent redraw, emoji range, and reflection fallbacks are live behavior, not automatically dead code. Removing them requires a deliberate replacement or compatibility decision.

## Performance work worth doing

The primary performance problem is work repeated while drawing, not the maximum list of 40 hotkeys.

- `OnRenderedActiveMenu` calls `RepositionTabs`, which recursively scans menu fields, enumerates collections, sorts anchors, allocates lists/hash sets, rebuilds bounds, and updates the snap graph. `AddHotkeyToSnapGraph` then updates the graph again.
- `AddHotkeyToSnapGraph` logs an interpolated Info message on every visible inventory draw. At 60 inventory draws per second, that is approximately 60 messages per second before other diagnostics.
- The editor draw path also calls `RepositionTabs` every frame, although most geometry is unchanged.
- GMCM member lookup occurs during draw/tick/click paths, and opening its editor scans loaded assemblies each time.

Cache a menu layout/session and invalidate it only on menu/page changes, resize, UI scale, definitions changing, or visibility changing. Cache verified reflection members once per compatible type. Update components once per layout revision. Draw should mostly consume an existing layout snapshot.

Log state changes once. Move detailed input/config traces behind diagnostics; keep actionable warnings. Avoid dumping every configured name/keybind on ordinary saves. Measure inventory frame time and allocations at 0 and 40 hotkeys before/after; do not promise a numerical speedup without a profile. Replacing every LINQ call or introducing object pools would be lower value than removing repeated discovery and logging.

## Recommended class boundaries

Keep the current behavior and visuals while moving ownership into a small set of focused types:

```text
ModEntry.cs                         Event wiring and startup only
Models/ModConfig.cs                 Persisted settings and migration version
Models/HotkeyDefinition.cs          Name, stable identity, emoji ID, binding
Services/HotkeyStore.cs             Draft/commit/save/reset and migration
Input/ShortcutDispatcher.cs         Delayed key simulation and cancellation
UI/InventoryHotkeyOverlay.cs        Row layout, rendering, hit testing, visibility
UI/ControllerNavigation.cs          Shared row/control focus rules
UI/EmojiCatalog.cs                  Chat icon IDs, texture, invalidation
Menus/HotkeyEditorMenu.cs           Editor-owned draft and native text controls
Menus/DeleteHotkeyDialog.cs         Confirmation input/focus and result
Integrations/GameMenuAdapter.cs     Verified native menu access/reflection
Integrations/GmcmIntegration.cs     Public config API and isolated overlay adapter
```

#### hotkey buttons
add a page to the modlist editor which allows you to edit the current buttons (a/b/y and pc keybinds)
- 1
  Controller B is hardcoded as cancel during keybind capture

In OnButtonPressed, B cancels the editor's keybind capture, meaning users cannot reliably assign Controller B as a shortcut through that flow. The same problem applies to Escape on keyboard.

I'd distinguish capture mode from modal navigation, or explicitly document reserved buttons.
###

#### dependency problem
GMCM integration relies on private implementation details

OpenDraftKeybindEditor() locates GenericModConfigMenu.Framework.Overlays.KeybindOverlay through reflection. Other methods read private fields such as KeybindEdit and Buttons.

A GMCM update could break this without changing its public API. I'd isolate this behind an adapter and make the native fallback capture more capable, particularly for multi-button chords.

- workaround clearly state it is dependant on GMCM
###

### inconsistent fields
Configuration and layout are slightly inconsistent

HubConfig exposes Columns and Rows, but RepositionTabs() hardcodes 10 columns and MaxCustomTabs hardcodes 40 entries.

Either implement configurable grid dimensions or remove these settings to avoid misleading future maintainers.
- leave as current but rename it from tabs to common script convention throughout
###

####
One additional concern is the reflection-heavy inventory discovery. GetInventoryPage() searches collections and types by name, and GetInventoryColumnAnchors() recursively inspects UI components. This is clever for compatibility, but potentially expensive and brittle. I'd profile it, cache results where safe, and invalidate that cache when the menu or layout changes.

- come up with an idea and reupdate this if it takes too much effort note that, otherwise make a plan to resolve this seperately
####

These names describe proposed classes, not existing game APIs. Keep sprite/tooltip helpers near the UI until reuse justifies a separate file. Avoid a service locator, large inheritance hierarchy, provider system, or one class per button.

The editor should own its draft, layout, focus, and active sub-modal mode. The overlay should own the attached inventory session and restore only the navigation links it modified. Persisted definitions can be shared; active menu state should be per screen if split-screen support is included.

Do not rename the serialized `Tabs` field to `Hotkeys` without migration. Existing user configurations must survive the refactor.

## Implementation order and acceptance gates

| Stage | Work | Gate before moving on |
| --- | --- | --- |
| 1. Record baseline | Preserve accepted screenshots, representative existing config, and an inventory of current behavior. Add regression cases for the defects above. | Existing hotkeys can be restored and visual changes can be compared. |
| 2. Fix input and modal correctness | Native keyboard text input; distinguish B key/controller B; controller deletion confirmation; exclusive top-modal routing; explicit naming/capture cancellation. | Mouse and controller can create, edit, cancel, bind, delete, and return without trapped focus or duplicate activation. |
| 3. Separate ownership | Extract store, editor, overlay, dispatcher, and the two integration boundaries in small compilable increments. Preserve serialization. Fix menu attach/detach ordering. | Reopen inventory repeatedly; counts stay bounded, component IDs are unique, and saved data is unchanged. |
| 4. Stabilize layout and performance | Shared layout/focus bounds, resize/scale invalidation, small-screen policy, remove per-frame logs and discovery. | Accepted alignment remains; 40 hotkeys fit or remain reachable at supported viewports; profile shows discovery runs only when invalidated. |
| 5. Prepare release | Accurate minimums/dependencies, clean analyzer-capable build, i18n, current documentation, version consistency, license choice, reproducible ZIP and contents check. | Clean install from the actual ZIP works and generates a fresh config. |
| 6. Compatibility testing | Complete the matrix below with actual game runs, especially target-mod opening and gamepad acceptance. Fix regressions before adding more scope. | No unresolved high-priority defects; support claims match evidence. |
| 7. Submit | Prepare CurseForge listing and beta file, collect feedback, then promote a tested stable version. | Screenshots show the shipped UI, controls/dependencies are documented, and the tested artifact is the uploaded artifact. |

## Focused test matrix

Automate pure config migration, capacity, grid-direction, and layout calculations where they catch behavior regressions. Use in-game tests for rendering, native menus, and third-party input; tests that only duplicate drawing code would add little value.

- Create/edit/save/cancel/reload/delete on mouse and controller, including both delete-confirmation choices and B/Escape.
- Names containing B, lowercase, digits, punctuation, spaces, accented characters; backspace, held-key repeat, paste, native naming cancel, and replacement of initial text.
- Counts 0, 1, 9, 10, 11, 39, 40; block the 41st without dropping existing entries. Navigate all partial/full rows both ways.
- Hide/show with focus on Add, a saved hotkey, a native tab, and an inventory slot; no hidden component receives clicks/focus.
- Reopen/close inventory at least 20 times; component counts and IDs must not accumulate. Switch to every native page and back.
- Resize while the editor is open; test 1280x800, 1920x1080, 2560x1440 and representative UI scales. Include four full rows.
- Test software/hardware cursor settings and switching input devices; tooltips remain appropriate and cursor visible.
- Test GMCM installed, missing, and an incompatible version according to the final dependency policy. Verify save/cancel/reset, modifier chords, alternate bindings, and controller confirmation.
- Test actual menu opening for Chirp/Chirper, World Navigator, and the exact progression/perfection mod being targeted. Record mod versions and the bound action, rather than assuming a display name identifies the mod.
- Test a physical assigned key immediately after a simulated one; no stuck key or broken target-menu navigation. Try an unrelated modal opening during the delay.
- Return to title, load another save, cancel native naming, and close while holding an inventory item; no stale editor/capture/dispatch state.
- Fresh config, previous config, invalid icon, malformed config, and a failed write. Confirm no silent loss of working hotkeys.
- Test remote multiplayer client behavior; test split screen only if claiming support, after state isolation.

## Release metadata, documentation, and packaging

- Keep `Taum.MenuSupreme` stable. Update the description to describe inventory hotkey shortcuts accurately.
- The manifest is 0.1.0 while the project/assembly currently defaults to 1.0.0. Set one project version and use the build package's supported manifest version mechanism.
- Set the actual tested SMAPI/game minimum and GMCM dependency policy. Add the CurseForge update key once the project ID exists.
- Retain the game's `net6.0` runtime target while using a compiler/SDK compatible with the analyzer. Correct CS8603 and rerun the analyzer rather than suppressing its load warning.
- Move the developer-specific E-drive GamePath out of shared configuration into a local override; let normal game-path discovery work for other contributors.
- Add an intentional Release ZIP command/script that leaves automatic deployment disabled and verifies an allowlist of package files. The current two-file ZIP passes; include i18n/license/runtime assets if those are added later. Avoid bundling player config, design screenshots, game DLLs, or reference projects.
- Keep the reference checkout separate from the distributable source where practical. Its `StardewMods-main/LICENSE` says all rights reserved unless otherwise noted; do not accidentally publish it as part of this mod's source or copy code without checking the relevant permission. It is not included in the verified binary ZIP.
- Add a project-level README, changelog, chosen license, support/issue link, and source-control ignore rules for bin/obj, IDE user files, audit artifacts, and local paths. Existing spec documents describe an older provider/Chests Anywhere concept; archive or label them as historical.
- Move player-facing strings into `i18n/default.json`; make layout accommodate longer translations and names.
- Document install/uninstall, controls, maximum capacity, global-vs-save scope of config, keyboard/controller toggle, GMCM requirements, tested versions, and shortcut limitations. Avoid claiming universal compatibility with all mod input implementations.

The official build-package documentation covers release ZIPs, exclusions, and project-version tokens: [SMAPI mod build package](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md).

## CurseForge submission checklist

Checked against official guidance on the review date. These are publishing tasks after code stabilization:

- Create a Stardew Valley mod project with an accurate English name, one-sentence summary, functional description, relevant category, and selected project license.
- Prepare a distinctive 400x400 project avatar and actual in-game screenshots showing the overlay/editor/controller hints. Current moderation policy specifies the avatar dimensions and requires accurate sample images for visual mods; actual screenshots are appropriate for this UI mod.
- Upload the verified ZIP, specify supported game versions, choose the release type deliberately, and provide a changelog.
- Declare required/recommended CurseForge project relationships consistently with the manifest where those projects are available. Check the current policy for any dependency not hosted there before promising automatic installation.
- Link a support channel for reports and describe tested compatibility. Do not treat successful ZIP generation or compilation as gameplay validation.
- A beta is suitable for initial feedback; official submission guidance says a project needs at least one Release file before it syncs to the app, so plan the subsequent stable release deliberately.

Sources: [Creating and submitting a project](https://support.curseforge.com/support/solutions/articles/9000197241-creating-and-submitting-a-project), [CurseForge moderation policies](https://support.curseforge.com/support/solutions/articles/9000197279-moderation-policies).

## Proposed first release scope

Preserve the approved inventory layout, chat-emoji carousel, native controller naming, exact GMCM keybind experience, mouse/controller create-edit-delete, four rows of ten, and Y visibility toggle. Stabilize those features before adding other menu integrations or redesigning the UI again. Treat untested platforms, split screen, and arbitrary mod-menu compatibility as explicit limits until verified.
