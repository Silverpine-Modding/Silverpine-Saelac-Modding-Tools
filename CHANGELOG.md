# Changelog

Created by Saelac and ChatGPT.

## 1.12.1

- Fixes tile refresh becoming unavailable after Silverpine destroys the
  framework's initial runtime object during bootstrap. Scene loading and API
  calls now recreate the coroutine host without reinstalling persistent hooks.
- Clears abandoned coroutine handles when a host is destroyed, so subsequent
  refresh requests from any mod can run. Native water-effect ownership survives
  host replacement; consumer APIs and save data are unchanged.
- Adds simulated host-lifecycle and multi-consumer queue regression tests.

## 1.12.0

- Calls the tile-refresh API after native floor construction and deconstruction.
- Refreshes native abyss/wall faces as well as grass and water edges after edits.
- Runs the refresh queue on the persistent framework runtime so plugin-host
  destruction during bootstrap does not stop the service or remove its hooks.
- Adds ModTileVisuals for refreshing grass overgrowth and water-edge shading
  at one turf, a deduplicated batch, or a rectangle. Includes all eight
  neighboring cells by default so borders and diagonal water corners update.
- Adds coalesced deferred requests that wait two frames after the last edit
  for native tile registration, Start, and deferred destruction to finish.
  Pending work is canceled on save load, world unload, and active-scene change.
- Tracks native water-edge instances and replaces only those owned effects
  when refreshing, preventing duplicate shading without deleting other children.
- Adds bounded-batch and game-API checks, standalone integration examples,
  and the independent visuals.tiles feature status. No continuous world scan,
  terrain mutation, save-format change, or consumer migration is required.

## 1.11.0

- Adds offline ModInventory.Capture() for loaded plugin identities and installed
  patcher assembly versions, with partial-inventory warnings and metadata-only
  reading that never loads patcher code.
- Framework Status now includes patcher assemblies.
- Documents the separate, optional Mod Update Checker addon. The framework
  contains no update requests and does not bundle or require that addon.
- Retains both plugin GUIDs and all existing public APIs.

## 1.10.4

- Applies opaque dark backgrounds to Framework Status and Audio Settings.
- Restores the main-menu launcher and inventory Mods page's original colors.
  Keeps the background scroll lock from 1.10.3 and all existing audio controls.

## 1.10.3

- Freezes the inventory Mods list's mouse-wheel scrolling, dragging, and
  inertia while a mod GUI owns a session. Restores its previous enabled state
  on close, including failed opens and emergency Escape, without disabling
  consumer GUI scroll views or changing existing registration APIs.
- Gives the main-menu launcher and inventory Mods page an opaque dark
  background. Keeps native tabs, borders, and consumer GUI styling unchanged.

## 1.10.2

- Removes construction-search blanket focus resets introduced with the B
  shortcut. Only releases the search field's keyboard focus on an outside
  click, before drawing buttons, without consuming clicks or resetting mouse
  capture. The B toggle and emergency Escape behavior remain available.

## 1.10.1

- Adds an optional, configurable construction-menu toggle shortcut, default B.
  Uses the existing Construct ability, including base-game and mod entries.
- Guards the shortcut during conversations, other tool sessions, text input,
  turn processing, interacting status effects, and exclusive pause menus.
  Construction search accepts the bound key as text; Escape still closes it.

## 1.10.0

- Preserves all 1.9.3 public/protected APIs and both framework plugin identities.
- Handles emergency Escape during world input, isolates session close failures,
  and automatically closes attached framework windows and overlay roots.
- Makes music cue handles refer to exact registrations; adds cancellable,
  shared audio loading without changing the original overload.
- Adds inventory mod search, optional owner grouping, and layout recalculation
  after screen/canvas changes while retaining three columns and scrolling.
- Adds optional ModContext ownership and exact registration cleanup across
  menus, dialogue, construction, audio, and save extensions.
- Adds same-owner prefab aliases and versioned per-save text payloads in atomic
  companion files. Preserves missing-owner/failed-migration data and backs up
  existing saves before overwriting after missing-content warnings.
- Initializes features independently and adds Framework Status to both menus
  with availability, plugin/menu listings, session/music state, and recent errors.
- Adds automated compatibility and failure-path checks; native UI/audio behavior
  still requires validation in Silverpine.

## 1.9.3

- Adds the `DialoguePromptTransforms` registry for conditional, non-mutating
  dialogue-history, world-lore, and environment prompt transformations.
- Contains transform activation and callback failures so consumer prompt
  customization cannot break Silverpine's native prompt pipeline.

## 1.9.2

- Adds an in-assembly compatibility plugin for the legacy
  `renegadex.silverpine.moddingtools` BepInEx GUID. Consumer plugins built with
  that hard dependency now load through `Saelac.Silverpine.ModdingTools`
  without requiring a separate redirect DLL.

## 1.9.1

- Provides shared main-menu and in-game menu registration, guarded GUI
  sessions, player-input locks, world-input suppression, and an emergency
  Escape close path.
- Provides scaled IMGUI helpers and reusable native-style UI behavior.
- Adds serialized-prefab registration, injectable construction categories,
  construction previews, native placement support, and free-build support.
- Adds custom audio loading and ownership, priority music, map-area and
  dialogue hooks, wildcard matching, late mixer binding, independent mixer
  bypass, and expandable per-mod volume controls.
- Adds a scrollable conversation-action menu with a pinned, native-sized Leave
  button and isolated scroll-wheel input.
- Adds the `DialogueInputActors` registry for dialogue-aware input decisions.
- Documents a standalone AI/developer integration contract and the Silverpine
  plugin-lifetime rules required by consumer mods.

Public Release builds are optimized and do not contain debug symbols.
