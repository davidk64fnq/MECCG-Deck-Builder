# AI Development Context: MECCG Deck Builder

## Project Summary
- Type: Windows Forms (WinForms) Desktop Application
- Language / Framework: C# (.NET 8.0, net8.0-windows)
- Domain: Deck Builder for Middle-earth: Collectible Card Game (MECCG) (Iron Crown Enterprises / ICE & Council of Elrond Dreamcards).
- Primary Data Sources:
  - Card Data: cards-dc.json (curated local master dataset with offline-first startup; optional user-initiated sync from GitHub rezwits/cardnum/fdata/cards-dc.json).
  - Set Data: sets-dc.json (curated local master dataset with offline-first startup; optional user-initiated sync from GitHub rezwits/cardnum/fdata/sets-dc.json).
  - Images: https://cardnum.net/img/cards/{set}/{imageName} (on-demand download with local set disk caching).
- Help File: `MECCG_Deck_Builder.chm` (compiled via Microsoft HTML Help Workshop across 14 modular topic files in a 4-book `.hhc` hierarchy: Welcome & Getting Started, Deck Construction & Formats, Card Catalog & Artwork, and Custom Taxonomy & Automation).

---

## Application Architecture & Core Files

- Form1.cs / Form1.Designer.cs: Main UI: 4-column workspace (`Master List`, `Card Image`, `Card Taxonomy Inspector` with monospace metadata view and pinned Quick Assign bar, and `Deck Partitions`), 3+3 two-tier filter GroupBoxes, four-header workflow menu bar (`Deck`, `Catalog`, `Taxonomy`, `Help`). Flush height alignment across all four upper panes.
- Card.cs: Strongly-typed card domain model with dynamic attribute map.
- CardSet.cs: Strongly-typed set domain model.
- CardCatalogService.cs: Card catalog management, local JSON loading, filter option discovery; normalizes Tolkien lore sub-races (*Elf -> Elf, *Dwarf -> Dwarf) with raw values retained in `Subrace`.
- CardFilterService.cs: Custom taxonomy management, cascading faceted search, authoritative edit-and-replace tagging, diacritic-resilient matching, site hold-type normalization, and dynamic attribute predicate evaluation (`Attribute=Value`, `&&`, `!=[Blank]`).
- Deck.cs: Strongly-typed deck domain model.
- DeckSectionType.cs: Enum representing deck partitions (Pool, Resources, Hazards, Sideboard, Sites).
- DeckExportService.cs: Decoupled deck export service (TTS, Play MECCG, Cardnum, Archive, Text).
- CardnumCard.cs: POCO class representing raw card schema from Cardnum JSON.
- CardnumSet.cs: POCO class representing card set metadata from Cardnum JSON.
- CardImageCache.cs: Two-tier image cache (MemoryCache + local disk folders like METW/) with async I/O.
- Constants.cs: Enums and string constants.
- OpenCloseDeck.cs: DTO used for serializing/deserializing .json deck save files.
- OpenCloseFilter.cs: DTO used for serializing/deserializing .json custom filter sets.
- Messages.cs: Centralized message box text helper.
- Program.cs: WinForms entry point; registers Windows-1252 code page provider.
- `DeckGenerationRule.cs`: Domain DTOs for deck generation schemas, choice branches, variable captures (`SaveAs`), automated shortfall fallbacks (`Fallback`), and symmetrical rule categories (`Categories`).
- `DeckGenerationService.cs`: Headless procedural deck generation engine with variety-first sampling, copy limits, branching choice groups, dynamic variable interpolation (`{Var.Attr}`, `{Var.Category:Cat}`), multi-card union sets, and fallback routing.
- `TaxonomyExtractionService.cs`: Text scanning, regex batch rule execution, dual-source cross-card attribute/category lookups (supporting official attributes and custom taxonomy), and two-way CSV spreadsheet pipeline.
- UserSettings.cs: User settings model and UserSettingsService persisting expansion set selections and taxonomy extraction dialog state to user_settings.json.
- `RunTaxonomyRulesDialog.cs`: Modal extraction dialog with four player-friendly tool modes: Scan Card Text for Catalog Entities, Cross-Card Attribute Lookup, Custom Pattern Search (Regex), and Batch Rules from File (.json).
- `DeckAssemblyMatrixDialog.cs` + partial files (`DeckAssemblyMatrixDialog.TheaterAndCharacters.cs`, `DeckAssemblyMatrixDialog.ResourcesAndQuests.cs`, `DeckAssemblyMatrixDialog.EventsAndHazards.cs`, `DeckAssemblyMatrixDialog.SiteTrackerAndModels.cs`): Modeless independent-peer drafting workbench (`MinimizeBox = true`, `1px` top hairline border) with a fixed left-hand navigation sidebar (`General Settings` + numbered `PIPELINE STEPS` `1. Regions & Sites` through `12. Hazard Events` with live `⚠` badges), global workbench `F5` pipeline shortcut (`ProcessCmdKey` with `Run Pipeline (F5)` button, `UseWaitCursor`, and live title-bar `attempt X/25` progress), scrollable `RichTextBox` status/summary panels inside consistent `GroupBox` containers (`CreateStatusGroupBox`, `SetRichBulletedStatus`, `SetRichPlainStatus`, RTF retry snapshots, natural-sorted lists), `.workbench.json` profile persistence, `Decks Before Council` (`1–4`) capacitated bipartite site-tap matching (`DeckCycleSiteTracker`, 1 `Resources` tap per cycle per non-haven site with unlimited havens across Steps 4–9, emitting one grouped warning per distinct bottleneck with a `Shared site pool` breakdown when earlier steps share sites), `TailorHazardsToCurrentDeck` (`General Settings` toggle switching Hazards between Unknown Opponent and Target Current Deck), `X(Y)` non-Hazard MP summary, streamlined centralized pipeline execution (`RunnableTabInfo` registry with 25-attempt retry and whole-deck `DeckSnapshot` save/recall), hybrid `Seed Region` terrain filtering with `⚠` mismatch validation, 50-trial unbiased region group search (`Max Sites` cap), optional `"Region group must contain a haven"` (`RequireHavenInRegionGroup`) with terrain-restricted BFS reachability validation and guided anchoring, 2-tier character prioritization (`Local Region/Sites` -> `Other Eligible`, 100 unbiased trials with uniform random tie-breaking, default `DI <= 4`, blank/`-` `Direct` treated as `0`, field-exit `0`-match & `> 20` Mind validation, recruit `Site Requirement` enforcement, and non-warning Wizard home-site `Note:`), 4-tier faction prioritization, 2-tier ally prioritization, roll-table-scored `Rings` drafting (with `Special Ring Role`, `Special Ability` unique-title innate + event test counting, `Aid Ring Test` drafting *Scroll of Isildur* through `DeckCycleSiteTracker`, and GoldRing-site checks), `Palantíri` drafting ahead of `Items` (with `Role`, `GreaterItem`/`Palantiri`/`Site Requirement` playability, and `Special Ability` unique-title innate *Saruman* + event counting), flat hard-guard `Items` drafting after `Rings` and `Palantíri` (steering Pass 1 around claimed Gold Ring / Palantír sites, with dynamic `Role` filter and Ring/Palantír/Quest exclusion), `Quests` drafting (with `Quest (Only)` + secondary `Role`, canonical hold-type and `&&` `Site Requirement` support, satisfied-prerequisite Tier 1 priority, and optional One Ring package auto-drafting), `Resource Events` drafting (with `Gates of Morning`/`Twilight` environment spinners and proportionate `Role` auto-fill to `Play Deck Size` + Sideboard events), `Creatures` drafting (`Nazgûl` + `Nazgûl Support` [`Target Species = Nazgûl`], `Standard Creatures` [1.0], and non-Nazgûl `Dual-Mode Half-Creatures` [0.5] with live `12`-creature-equivalent readout, proportionate `Creature Race` bucketing with variety-first sampling, and either manual `Region Terrains`/`Site Holds` keying filters or automatic `_deck.Sites` keying), `Hazard Events` drafting (`Doors of Night` + `Twilight` sharing a 3-copy limit across `Resources + Hazards + Sideboard`, proportionate `Role` auto-fill to `Play Deck Size` + Sideboard events with `Duration` gated on `Long-event` presence, manual `Region Terrains`/`Site Holds` keying in Unknown Opponent mode vs. `_deck.Sites`/`Resources`/`Pool` keying, `Target Item`, and 3-way `Target Species` checks in Target Current Deck mode, plus single combined Sideboard event counting), `;` = OR / `&&` = AND `Prerequisite` evaluation, multiline tooltips, and live deck synchronization.
- `ExportTaxonomyDialog.cs`: Partition-scoped CSV taxonomy draft exporter with preset column templates and custom category registration.

---

## Workbench Architectural Conventions (Future Tabs)
- **Centralized Pipeline & Deck Reset:** Tabs 2+ are pure configuration views driven from `General Settings` (`Run Pipeline`, `Clear Deck`, `Save/Recall Snapshot`). `RunPipelineDraft()` clears all deck partitions before each run/attempt, so individual tab runners do not perform per-slice `RemoveAll` cleanup.
- **Local Group vs. Outside Haven Scope:** Downstream tabs (`Starting Company`, `Wizards & Recruits`, `Factions`, and future `Items`, `Allies`, `Hazards` tabs) separate cards tied to the drafted region group—including Havens located inside those drafted regions—(Tier 1) from Havens outside the drafted region group (Tier 2).
- **Generic Pipeline Registration:** Register every new runnable tab in `_tabInfo` (`RunnableTabInfo(Runner, StatusLabel)`) and `_pipelineOrder` with a `bool silent = false` runner signature, and prefix soft warnings in the tab's status label with `⚠` so `RunPipelineDraft()` handles retries, snapshots, and warning surfacing automatically.
- **`partial class` Organization:** `DeckAssemblyMatrixDialog` is split across 5 `sealed partial class` files grouped by domain (`DeckAssemblyMatrixDialog.cs` for core layout/pipeline/settings/validation, `DeckAssemblyMatrixDialog.TheaterAndCharacters.cs` for Steps 1–3, `DeckAssemblyMatrixDialog.ResourcesAndQuests.cs` for Steps 4–9, `DeckAssemblyMatrixDialog.EventsAndHazards.cs` for Steps 10–12, and `DeckAssemblyMatrixDialog.SiteTrackerAndModels.cs` for `DeckCycleSiteTracker`, shared guards, and settings DTOs).

---

## AI Studio -> Antigravity Edit Delivery Workflow
- **Discuss Before Editing:** Review designs, trade-offs, and options first without emitting edit instructions until the changes are agreed (unless edits are explicitly requested immediately).
- **Single Copy-Paste Block:** Present all agreed edits across all target files inside **one single copyable code block** (using a 4-backtick outer markdown fence so inner 3-backtick code fences stay nested) so the user only needs one copy-paste into Antigravity.
- **File-Edit-Only Guardrail:** Always include this exact instruction at the top of every Antigravity edit block: `Use file-editing tools only. Do not run any terminal, shell, build, or verification commands.` This prevents Antigravity's auto-run safety check from stalling on terminal or `dotnet build` prompts; the user handles building and testing directly in Visual Studio.
- **Verbatim Find & Replace Blocks:** Structure every change with explicit `FILE: path/to/file`, `FIND:`, and `REPLACE WITH:` blocks containing exact, unique surrounding context lines verified against the current codebase.
- **No Shorthand Placeholders:** Never use omissions or placeholders (such as `// ... existing code ...`) inside `FIND` or `REPLACE WITH` blocks; every block must be literally copy-pasteable by a fast model (e.g., Gemini Flash in Antigravity) without guessing or running build checks.

---

## Standardized Taxonomy & Filtering Vocabulary

1. **Official Card Attributes (Filter Slots 1–3):**
   - Housed inside `GroupBoxCardAttributes` ("Card Attributes (Official Database)").
   - Canonical rules properties: `Attribute` (e.g. *Race*, *Skill*, *Specific*) and `Value` (e.g. *Dúnadan*, *Warrior*).
   - Inspected via context menu item: `Card Attributes`.
   - Expansion sets filtered globally via top **Catalog** menu checkboxes.

2. **Custom User Taxonomy (Filter Slots 4–6):**
   - Housed inside `GroupBoxCustomTags` ("Custom Tags (User Taxonomy)").
   - User-defined taxonomy: `Category` (e.g. *Role*, *Region Requirement*, *Prerequisite*) and `Tag` (e.g. *Card Flow*, *Gorgoroth*, *Paths of the Dead*).
   - Inspected via context menu item: `Assigned Tags` and the live **Card Taxonomy Inspector** pane.
   - Card tagging actions: `Assign Tag` (with `+ Author New Tag...` and `+ Add Tag...`), `Remove Tag`.
   - Dedicated manager: `Taxonomy > Manage Custom Tags...` (`Categories`, `Tags`, `Add...`, `Rename...`, `Delete`).
   - Tag file persistence: `Taxonomy > Load Tag Set...`, `Taxonomy > Merge Tag Set...`, and `Taxonomy > Save Tag Set...` (defaults to auto-loaded baseline `Rules_Support_Tags.json`).
   - Global filter reset: `Taxonomy > Clear All Active Filters` (<kbd>Ctrl+R</kbd>).

3. **Procedural Rule Schema Symmetry & Storyline Engine:**
   - `"Attributes"`: Dictionary of official criteria (`Attribute: Value`, e.g. `{"Race": "Dwarf"}`).
   - `"Categories"`: Dictionary of custom criteria (`Category: Tag`, e.g. `{"Faction Bonus": "Wood-elves"}`).
   - **Placeholders:** `{Variable.AttributeName}` for official attributes (including `{Variable.Home}` resolving both named sites and dynamic predicates) and `{Variable.Category:CategoryName}` (or `{Variable.Tag:...}`) for custom tags.
   - **Dynamic Predicates:** Evaluates `Attribute=Value`, `Attribute!=Value`, and compound `&&` logic (e.g. `Region=Under-deeps && Site!=Dark-hold`, `Site=Haven`, `Site=Dark-hold`, `Adjacent Sites!=[Blank]`) dynamically, with diacritic resilience and canonical hold-type normalization.
   - **Strict Geography Separation:**
     - `Site Requirement`: Reserved exclusively for **Site Names** (*Vale of Erech*, *Minas Tirith*) and **Site Hold Types** (*Dark-hold*, *Ruins & Lairs*). Strictly purged of `Region=...` predicates (Nazgûl use `Region Requirement`) and redundant `Haven` tags (havens are foundational to all decks; omitting them prevents false expedition draws).
     - `Region Requirement`: Reserved exclusively for **Geographic Region Names** (*Gorgoroth*, *Rohan*) and **Terrain Types** (*Wilderness*, *Shadow-land*).
     - `Home Region`: Preserved specifically on characters to link starting sites to territories for regional company cohesion.
   - **Role Taxonomy (14 Baseline Roles):**
     - Symmetrical across resources and hazards: `Automatic Attack`, `Card Flow`, `Combat`, `Corruption`, `Duration`, `Influence`, `Item`, `Movement`, `Protection`, `Quest`, `Readiness`, `Recruitment`, `Site`, `Terrain`.
     - `Card Flow`: Unified tag covering the hand-deck-discard pipeline (drawing extra cards, searching play decks, discard retrieval, hand size modifiers, cycling, and discard attacks).
     - `Combat` vs. `Protection`: `Combat` is strictly engaging in the fight (prowess, strikes, combat dice, extra attacks). `Protection` is strictly avoiding or surviving the fight (attack cancellation, stealth/evasion, personal strike immunity, wound prevention, healing).
     - *Creature Policy:* Standard hazard creatures omit `Role: Combat` (their combat nature is fully captured by official attributes `Secondary: Creature` and `Prowess`/`Strikes`; tagging them pollutes the combat trick filter). Dual-mode cards only receive a `Role` if their event mode has a distinct tactical function (e.g., `Readiness` on *Adûnaphel*, `Combat` on *Akhôrahil* for his -1 Body debuff).
     - *Avatars Policy:* Avatars (Wizards, Ringwraiths) omit `Role` tags; they are deck keystones drafted by Name or `Race: Wizard`, not commodity utility draws.
   - **Universal Quest & Storyline Chaining:**
     - `Prerequisite`: Assigned to dependent cards to declare required enabler cards or relics (`;` = OR, `&&` = AND, e.g. `Paths of the Dead` on *Army of the Dead*; `Narsil && Reforging` on *Andúril, the Flame of the West*; `The One Ring` on *Cracks of Doom* and *Gollum's Fate*).
     - *Location Shielding:* Generic open draws rely on `Site Requirement` (e.g., `Site Requirement: "[Blank]"`) to keep location-locked enablers out of unrelated decks without maintaining inverse tags.
   - **Primary Usability Rule (Soft Synergy vs. Hard Dependency):**
     - Hard dependencies (cards non-functional without each other, like *Army of the Dead* and *Paths of the Dead*) use `Specific` and `Prerequisite`.
     - Standalone cards with optional partner perks (e.g., *Arwen* +7 DI on Aragorn, or *Athelas* healing with any Dúnadan vs. corruption removal with Aragorn) must **never** be tagged with hard prerequisites (`Specific`) or restrictive secondary roles that would lock them out of generic company draws.

4. **Core Workflow Menu Hierarchy:**
   - **Deck:** `New Deck` (<kbd>Ctrl+N</kbd>), `Open Deck...` (<kbd>Ctrl+O</kbd>), `Save Deck` (<kbd>Ctrl+S</kbd>), `Save Deck As...`, `Generate from Rules...` (<kbd>Ctrl+G</kbd>), `Re-run Deck Rules` (<kbd>F5</kbd>), `Export Deck...` (<kbd>Ctrl+E</kbd>), `Exit`.
   - **Catalog:** `Select All Expansions`, `Clear All Expansions`, expansion checkboxes, `Download Card Artwork...`.
   - **Taxonomy:** `Manage Custom Tags...`, `Extract Tags with Rules...`, `Export Taxonomy Draft (CSV)...`, `Import Taxonomy (CSV)...`, `Load Tag Set...`, `Merge Tag Set...`, `Save Tag Set...`, `Clear All Active Filters` (<kbd>Ctrl+R</kbd>).
   - **Help:** `Help Contents` (<kbd>F1</kbd>), `About MECCG Deck Builder...`.

---

## Milestone Execution & Progress Tracking

- [x] **v0.1.0 - Foundation & Documentation** (100% complete)
- [x] **v0.2.0 - Modernization & Bug Fixes** (100% complete)
- [x] **v0.3.0 - Domain Refactoring & Architecture** (100% complete)
- [x] **v0.4.0 - Curated Filter Taxonomy** (100% complete)
  - Issue #12: Custom Filter Synchronization Across Slots, Deletion Cleanup, and Empty Key UX
  - Issue #13: Implement Cascading Faceted Search Across All Filter Slots to Eliminate Zero-Result Queries
  - Issue #14: Add Per-Slot and Global Filter Reset Capabilities with Keyboard Shortcuts and Context Menus
  - Issue #15: Fix Empty Set Selection Bypass, Startup ComboBox Crash, and Preview Cleanup
  - Issue #16: Decouple Custom Tag Authoring from Filter Bar (Dedicated Taxonomy Manager)
  - Issue #17: Group filter panel into titled containers with slot labels and layer demarcation
  - Issue #18: Standardize taxonomy and filtering terminology across UI, dialogs, and help documentation
- [x] **v0.5.0 - Procedural Deck Generator & Taxonomy Realignment** (100% complete)
  - [x] Issue #20: Procedural Deck Generation Engine, Relational Taxonomy Extraction, and Filter Normalization
  - [x] Issue #21: Rule Variable Capture, Shortfall Fallback Mechanics, and Modular Help System Overhaul
  - [x] Issue #23: Symmetric Filter Invalidation Fix, Attribute Reference Overhaul, and Concurrent Tag Set Merging
  - [x] Issue #24: Persist expansion set selection and extraction regex parameters across sessions
  - [x] Issue #25: Harmonize taxonomy extraction UI layout, data flow sequencing, and documentation
  - [x] Issue #27: Dynamic CSV Taxonomy Pipeline with Symmetrical Attribute Scoping and Placeholder Protection
  - [x] Issue #28: Menu Bar Modernization: Unified Four-Header Hierarchy and Standardized Shortcuts
  - [x] Issue #29: Realine Help System and Documentation with Modernized Menu Bar Architecture, Symmetrical Rule Categories, and Clean Terminology
  - [x] Issue #30: Configurable Card Distribution Strategies in Procedural Deck Rules (`Distribution: Variety | Focused | Uniform`)
  - [x] Issue #31: Dual-Mode Right Pane with Card Taxonomy Inspector, Monospace Metadata Readout, and Pinned Quick-Assign Bar
  - [x] Issue #32: Procedural Rule Wildcards (* / All), Faction Influence Alignment, and Lossadan Cairn Attribute Correction
  - [x] Issue #33: Add Numeric Comparison Operators (<, <=, >, >=, !=) to Procedural Deck Generation Rules
  - [x] Issue #34: Support Filtering for Blank / Unset Attributes and Custom Tags ([Blank] / [None])
  - [x] Issue #35: Normalize Creature Race Attributes to Singular in CardCatalogService and Align Automatic Attack Tags
  - [x] Issue #36: Support Dynamic Attribute Predicate Selectors (Attribute=Value) in Custom Taxonomy Tags and Deck Generation
  - [x] Issue #37: Broaden Cross-Card Lookup to Support Custom Categories and Region Type Taxonomy
  - [x] Issue #38: Consolidate Site/Region Requirement Taxonomy and Model Dynamic Character Homes as Native Predicates
  - [x] Issue #39: Baseline Taxonomy Synchronization, Role Vocabulary Alignment, and Dialog Directory Persistence
  - [x] Issue #40: Unified 'Card Flow' Role, Case-Sensitive Tag Renaming Fix, and Universal Quest Engine Architecture
    - Consolidated 'Deck' and 'Hand' roles into plain-language 'Card Flow' tag.
    - Fixed tag/category rename bug by changing guard comparison to StringComparison.Ordinal.
    - Implemented 'Prerequisite' and 'Prerequisite For' categories with Recipe 10 documentation.
    - Purged 'Region=...' predicates and redundant 'Haven' tags from Site Requirement.
  - [x] Issue #41: Geographic Theater Deck Rules: Contiguous Region Clusters, Haven Starter Hubs, and Overland Pathfinding
    - Graph layer built from 'Adjacent Regions' taxonomy on Region cards.
    - Added 'GeographicTheater' rule schema with 'Starter Only' and 'Starter + Region' execution modes.
    - Automated Hero starting haven (Rivendell), stepping-stone havens (Lórien/Grey Havens for Edhellond), and BFS overland transit routes.
    - Unified downstream contract exporting {Theater.Haven} and {Theater.Region}.
  - [x] Issue #42: Modular Deck Assembly Workbench: Two-Slot Component Matrix (Starting Pool & Geographic Theater)
    - Created `DeckAssemblyMatrixDialog` (Ctrl+M) providing a side-by-side modular assembly workbench.
    - Added Company slot controls: component discovery, multi-select species checklist, leader skill filter, and pool size spinner.
    - Added Theater slot controls: component discovery, haven anchor, movement mode, dynamic region/site numeric spinners, canonical terrain and hold checklists, and transit toggles.
    - Supported bi-directional assembly pipelines: Leader-First (Company -> Theater) and Region-First (Theater -> Company) via `"Haven": "Auto"`.
    - Fixed fallback property inheritance: fallbacks now inherit parent target `Section` and `SaveAs` variable captures.
    - Added `GenerateModularDeck` to `DeckGenerationService` with dynamic parameter injection and blackboard context sharing.
    - Context-aware `F5` re-run routing: re-reads component JSONs from disk and regenerates using exact workbench parameter choices.
    - In-session dialog memory (`DeckAssemblyDialogState`): restores all controls upon reopening the workbench.
    - Authored initial component library: `Company_Regional_Retinue.json`, `Company_Noble_Bodyguards.json`, `Company_Sage_Lore_Expedition.json`, and `Theater_Contiguous_Cluster.json`.
  - [x] Issue #43: Avatar Tournament Copy Limits, Universal Ally Engine, and Directory Taxonomy Refactoring
    - Implemented official tournament Avatar allowance in `DeckGenerationService.cs` (up to 3 of one, or 2+1 mix; max 3 total in Resources; max 2 distinct titles; 3 different prohibited).
    - Fixed Avatar playset distribution in `Focused` sampling mode.
    - Tagged *Roäc the Raven* with dynamic predicate `Site Requirement: Region=Northern Rhovanion` and `Region Requirement: Northern Rhovanion`.
    - Authored Chapter 2 (Allies) and Chapter 3 (Avatars) in `tagging_and_drafting_playbook.html`.
    - Documented the Keystone Principle and Gandalf's `Home: Site=Haven` predicate.
    - Refactored Catalog Partition Directory table into an alphabetical index mapping desktop filter criteria (Slots 1 & 2) to custom categories and specialized attributes.
    - Updated `deck_generation_engine.html` and `deck_rules_reference.html` with Avatar copy limit rules.
  - [x] Issue #44: Composite Secondary Filter Fix, Hazard Partition Tri-Split, and Chapters 4–7 Playbook Implementation
    - Fixed CS0161 and composite slash-filtering bug in `CardFilterService.MatchesCardnumCriterion` by checking exact matches before delimiter splitting.
    - Enhanced `sets_and_faceting.html` explaining faceted peer-slot dropdown pruning vs. Master List intersection scope.
    - Refactored Catalog Partition Directory into distinct Standard Creature, Dual-Mode Creature, and Hazard Event rows with clean category scopes.
    - Authored Chapter 4: Factions (29 METW factions, 1-to-1 strongholds, and *Army of the Dead* quest engine).
    - Authored Chapter 5: Hazard Creatures (Standard - 44 METW cards), establishing the Regional Hold Boundary Rule and Option 1 universal hold keys for *Pûkel-men*.
    - Authored Chapter 6: Hazard Creatures (Dual-Mode - 11 METW cards), balancing creature territorial boundaries with event roles.
    - Authored Chapter 7: Hazard Events (60 METW cards across Permanent, Long, and Short events), standardizing `Role: Influence` on *Muster Disperses*, `Target Item: Ring` on *The Precious*, and removing *The One Ring* prerequisite on *The Nazgûl are Abroad*.
  - [x] Issue #45: Generalized Taxonomy CSV Exporter, Playbook Reference Cards & Navigation, and Chapter 8 (Items & Gear)
    - Replaced chapter 3-column tables with full-width Reference Cards and scope badges.
    - Added in-topic chapter navigation bar (`.chapter-nav`) and per-chapter return links (`.back-to-top`) to eliminate long-scroll fatigue.
    - Created `ExportTaxonomyDialog.cs` providing a generalized partition exporter with 12 presets and smart column checklists.
    - Refined `Readiness` (wound healing & untapping) vs. `Protection` (combat evasion & survival) and expanded `Role: Item` to include ring testing.
    - Audited all 54 METW items and authored Chapter 8 (Items & Gear) with five procedural drafting patterns.
    - Synchronized Column 3 of the Catalog Partition Directory table for Items & Gear.
  - [x] Issue #46: Resource Events, Sites, and Regions Playbook Completion, Adjacency Graph Repair, and Companion Tags Alignment
    - Audited and aligned 85 METW Resource Events; refined `Role: Influence` on *Tempering Friendship*, established `Target Item: Gold Ring` for the testing suite (*Ringlore*, *Test of Form*, *Test of Lore*, *Wizard's Test*, *Scroll of Isildur*), and populated `Site Requirement: Haven` on Haven-entry events.
    - Verified 69 METW Sites; confirmed discrete flags for `Information` (5 sites), `Palantiri` (6 sites), and `Scroll` (*Tolfalas*).
    - Repaired 4 Middle-earth region adjacency graph defects (*Andrast*, *Bay of Belfalas*, *Eriadoran Coast*, *Old Pûkel-land*) restoring bidirectional BFS pathfinding.
    - Authored Chapters 9 (Resource Events), 10 (Sites & Havens), and 11 (Regions) in `tagging_and_drafting_playbook.html`.
    - Overhauled `rules_support_tags.html` with full 15-category `.chapter-nav` navigation, `.back-to-top` links, `Adjacent Regions` documentation, and a clean cross-reference bridge in Section 3.
    - Transferred the `{Leader.Home}` machine predicate execution walkthrough into Chapter 3 of the Playbook.
  - [x] Issue #47: Interactive Modeless Workbench: 4-Tab Drafting Studio, Character Composition Grids, Avatar Rules & Atomic Theater Chaining
    - Converted `DeckAssemblyMatrixDialog` from modal (`ShowDialog`) to modeless (`Show(owner)`) with single-instance focus handling and bi-directional live UI sync (`RefreshMainDeckUI`).
    - Tab 1 (General Settings): Added unified Play Deck Size (30/30) and Sideboard Size spinners with live composition status strip.
    - Tab 2 (Starting Company): Built 5-row tactical roster (`Starter 1–5`) with Skill, Species, Mind, DI, per-row `[ ✕ ]` clear buttons, and optional Theater region constraint.
    - Enforced the official $\le 20$ Mind starting pool cap via mathematical feasibility pre-checking and 100-attempt backtracking search.
    - Tab 3 (Non-Starting Characters): Added dedicated Avatar / Wizard controls enforcing official tournament allowances (3× of one, or 2+1 split; max 3 total; 3 different blocked).
    - Built symmetrical 10-row recruit grid with live budget validation guard disabling Run/Save when Wizards + Companions > 10.
    - Tab 4 (Geographic Theater): Streamlined controls with automated Rivendell BFS overland routing and stepping-stone havens.
    - Implemented atomic budget pre-checking in Geographic Theater to guarantee non-starter home sites and regions fit without orphan characters.
    - Fixed ListBox index inversion bug on sorted deck tabs by sorting underlying `Deck` sections before refreshing listboxes.
    - Repaired Home site matching for hyphens/spaces and resolved `Home Region` lookups through `_filterService`.
  - [x] Issue #48: Decoupled Avatar / Non-Avatar Resource Character Allowances, 3-Row Sideboard Reserve Grid, and Secondary Wizard Destination Routing
    - Decoupled Avatar allowance (up to 3 cards; 3x of one or 2+1 split) from the 10 companion rows on Tab 3 in `DeckAssemblyMatrixDialog.cs`. Selecting Wizards no longer penalizes the player's companion allowance.
    - Added dedicated Secondary Wizard destination control (`_cmbWizard2Dest`: `Sideboard` [default] vs. `Resources`), enabling the classic competitive tournament mirror-shield configuration (2 copies of Primary in Resources + 1 copy of Secondary in Sideboard).
    - Added a dedicated 3-row "Sideboard Contingency Characters (Sideboard - Up to 3)" grid (`Reserve 1–3`) with identical columns (Skill, Species, Mind, DI, Clear button).
    - Compacted vertical row spacing to 26px and expanded workbench client size to 860 x 650, fitting 10 Recruits + 3 Reserves + Wizard selection without scrollbars.
    - Enforced the Sideboard Size limit as a dynamic validation guard for reserve rows and any Sideboard Wizard.
    - Extended Run, Clear, Save, and Recall actions to handle both Resources companions and Sideboard reserve characters, enforcing global 1-copy uniqueness across the whole deck.
    - Updated Tab 1 live composition summary: distinguishes non-Avatar companions, Avatars, and Sideboard characters against official tournament limits.
    - Enhanced `RunGeographicTheaterDraft`: audits required home sites for Resource companions as the primary baseline, then opportunistically incorporates Sideboard character home sites if site and region budgets permit.
  - [x] Issue #49: Configurable Character Home Site Inclusion Policy, Sideboard Reserve Home Sites, and Wizard Land Home Site Routing
    - Added dedicated Wizard Land Home Site toggle (`_chkIncludeWizardHome`) in Avatar box: `Include Wizard's land home site in Site deck (Isengard for Saruman / Rhosgobel for Radagast)`.
    - Added Home Site Policy ComboBox (`_cmbHomeSitePolicy`) on Tab 3 with three operational modes:
      1. `Best Effort (Fit in budget, else Havens)` [Default]: Prioritizes and drafts all character/Wizard home sites that fit within Site Count and Region Count; reports any budget omissions with the reminder that characters deploy at Rivendell.
      2. `Strict (Require all; abort on shortfall)`: Pre-checks mathematical feasibility against both site and region budgets; cleanly aborts if any required home site cannot fit.
      3. `Havens Only (Do not force home sites)`: Omits all non-starter home sites, preserving site deck capacity purely for theater adventure sites and standard Havens.
    - Added Sideboard Home Site toggle (`_chkIncludeSideboardHomeSites`): `Also attempt to include Sideboard character home sites (if budget permits)` (default: unchecked).
    - Bi-directional coordination: Tab 3 Non-Starting Character draft respects the active policy when theater sites already exist, and Tab 4 Geographic Theater draft applies the policy when constructing the site deck and region highway.
  - [x] Issue #49: Modular Workbench Studio: Profile Storage, Option A Layout, Natural Ranges, Dynamic Haven Selection, and Clean Theater Roaming
    - Adopted Option A flush-left grid layout across Tabs 2 and 3, eliminating row labels and resolving header collision coordinates.
    - Implemented `NormalizeNumericQuery` supporting natural range expressions (`>= 3 and <= 5`, `2-4`, `3 to 5`) with Roslyn `SYSLIB1045` suppression.
    - Added `Home Region Policy` dropdown on Tab 2 (`Best Effort`, `Strict`, `Open / Anywhere`) to control regional company constraints.
    - Restructured Wizard controls into two clean rows and replaced checkbox with `Wizard Home Site Policy` dropdown (`None` vs `Include Wizard Home Site(s)`).
    - Rolled Sideboard home site inclusion directly into `Best Effort` as Tier 2 opportunistic priority, eliminating `_chkIncludeSideboardHomeSites`.
    - Enhanced Haven Anchor with dynamic `Starting Company (Majority Vote)` (plurality winner with Rivendell tie-break) and `Nearest Haven (to Theater)` (BFS hop calculation from cluster to Haven home capitals).
    - Decoupled theater roaming clusters from havens, dropped the overland BFS highway to ensure `Region Count` strictly equals region cards drafted, and removed the ghost wizard fallback.
    - Added `Save Settings...` and `Load Settings...` profile persistence on Tab 1 via `*.workbench.json`.
    - Standardized status text color to neutral slate gray across all tabs and expanded Tab 3 status label height to prevent clipping.
  - [x] Issue #50: Decoupled Geographic Theater Architecture: Dedicated Seed Region, Terrain-Enforced Clusters, Multi-Attempt Search & UI Preview Synchronization
    - Added dedicated `Seed Region` dropdown (`_cmbSeedRegion`) with `Auto (From Haven Anchor)`, `Random (Across Middle-earth)`, and alphabetical region discovery.
    - Decoupled Haven Anchor from seed region: travel to/from Havens uses Starter Movement (0 region cards); `Region Count` strictly equals contiguous theater regions without drafting disconnected haven territory cards into the site deck.
    - Permanent `Company Haven (Majority Vote)` in `_cmbHaven` with fair random tie-breaking and smart haven candidate evaluation when the pool is empty.
    - Prevented circular dependency ("mutual reflection") between `Nearest Haven (to Theater)` and `Auto (From Haven Anchor)` via dynamic auto-switching.
    - In `Starter Only` mode, dynamically stripped `Nearest Haven (to Theater)` from the Haven list and replaced `Seed Region` with `(N/A - Haven Hub)`, saving and restoring previous selections on mode toggle.
    - Enforced `allowedRegions` constraint in `DeckGenerationService.ExpandContiguousRegionCluster` so all cluster neighbors strictly obey checked terrain filters (e.g. Wilderness).
    - Implemented a 100-attempt Monte Carlo retry search in `RunGeographicTheaterDraft` for random seeds and whenever `Site Count > 0` to hunt for site-dense branches before graceful fallback.
    - Refactored `RefreshMainDeckUI` to accept `DeckSectionType?`, automatically switching tabs, resetting `SelectedIndex = 0`, and updating artwork/taxonomy preview on Run, with fallback to `ListBoxMaster[0]` on Clear.
    - Updated all workbench Run, Clear, and Recall actions across Starting Company (`Pool`), Recruits (`Resources`), and Theater (`Sites`) to pass section types.
    - Added inline amber warning notices (`Color.FromArgb(180, 83, 9)`) for region, site, and budget shortfalls.
    - Fixed WinForms ampersand suppression by setting `UseMnemonic = false` on status labels (`Wold & Foothills`).
    - Resolved `IDE1006` naming violations (`NotifyDeckChanged`).
  - [x] Issue #51: Side-by-Side Card Taxonomy & Deck Workspace, 3+3 Faceted Filter Bar, and Selection Sync Fixes
    - Widened `Form1` (`1242 x 682`) to display `PanelTaxonomy` and `TabControlDeck` simultaneously in a 4-column layout (`[Master] | [Image] | [Taxonomy] | [Deck]`) with flush `Y=37, H=505` alignment.
    - Removed `BtnModeDeck` / `BtnModeTaxonomy` mode-toggle buttons, expanding `RichTextBoxTaxonomy` and deck partition listboxes vertically by 29px.
    - Expanded the bottom filter bar from 2+2 (4 slots) to 3+3 (6 slots: Slots 1–3 for Official Card Attributes, Slots 4–6 for Custom User Tags) with `AdjustWidthComboBox_DropDown` across all 12 dropdowns.
    - Updated Quick Assign category synchronization and `ExportTaxonomyDialog` to support all 6 filter slots.
    - Fixed re-entrant `ListBox_SelectedIndexChanged` wipe (`_isSyncingListSelection`) when switching focus from `ListBoxMaster` to a deck tab.
    - Fixed `RemoveCardFromSection` to advance selection or fall back to `ListBoxMaster[0]` / clear preview when deleting or moving an inspected card.
    - Updated all 9 affected CHM help HTML topics (`interface.html`, `sets_and_faceting.html`, `attributes_reference.html`, `custom_taxonomy.html`, `deck_operations.html`, `index.html`, `spreadsheet_pipeline.html`, `tagging_and_drafting_playbook.html`, `taxonomy_extraction_tools.html`).
  - [x] Issue #52: Multi-Tab Execution Pipeline, Tab Responsibility Decoupling, Theater Scope Isolation & Contiguous BFS Spanning
    - Added dynamic pipeline step-order dropdowns (`Step 1`..`Step N`) on `General Settings` inside `_pnlPipelineSteps` with `(None)` support, duplicate auto-swapping, `Reset Order`, and `ExecutionOrder` persistence in `.workbench.json` (`DeckAssemblySettings`).
    - Introduced `RunnableTabInfo` (`Runner`, `StatusLabel`) registry, pre-run clear confirmation, `bool silent = false` runner signatures, and a 25-attempt pipeline retry loop with Step-1 early-stop, best-valid-snapshot retention, and diagnostic fallback.
    - Decoupled Tab 3 and Tab 4 responsibilities: replaced `Wizard Home Site Policy` with a non-blocking amber `⚠` notice in `_lblRecruitStatus` (`Saruman`/`Isengard`, `Radagast`/`Rhosgobel`), moved `Non-Starter Sites/Regs` (`_cmbTheaterHomeSitePolicy`: `Ignore`, `Best Effort`, `Strict`) onto Tab 4, unified `Resources` and `Sideboard` character handling, and added Sideboard size validation with 25-attempt tightest-row-first retry in `RunNonStartingCharactersDraft`.
    - Isolated non-Haven Theater scope (Tier 1) from Haven scope (Tier 2) in `RunStartingCompanyDraft` (`primaryTheaterRegions` vs. `havenRegions`) and `RunNonStartingCharactersDraft` (`theaterAdventureSites` vs. `havenSitesInDeck`), excluded `Haven` sites from `requiredHomeRegions`/`acceptedHomeRegions`, added `FindContiguousSpanningBackbone` and `ExpandClusterToTarget` for `Strict` multi-region BFS spanning, and set `Edhellond` home region capital in `FindNearestHavenToCluster` to `"Anfalas"`.
    - Standardized `"Match Haven Anchor"` and `"Random"` seed options, added dynamic `ToolTip` descriptions (disposed in `Dispose(bool)`), and escaped `&&` mnemonics in Tab 1 and Tab 4 `GroupBox` titles.
  - [x] Issue #53: Workbench Simplification, Tab Reordering, Min/Max Adventure Sites & Strict Tier 1->2 Character Prioritization
    - Reordered tabs to match execution sequence (`General Settings`, `Regions & Sites`, `Starting Company`, `Wizards & Recruits`) and replaced military/math jargon with plain MECCG terminology.
    - Removed per-tab `Run`/`Clear`/`Save`/`Recall` buttons and Step 1–3 order dropdowns; centralized `Run Pipeline`, `Clear Deck`, `Save Snapshot`, and `Recall Snapshot` (`DeckSnapshot`) on `General Settings` with `ValidateWorkbenchReadiness()` guards.
    - Simplified `Regions & Sites` by retiring `Movement Mode`, `Base Haven`, `Site Count`, and `Non-Starter` policy; always drafts all 4 Hero Havens (preferring Hero printings), `Region Count` (`4–12`) connected regions, and matching adventure sites bounded by linked `Min`/`Max Adventure Sites` (`0–50`, default `4–12`).
    - Removed `Haven` from `Site Holds`, defaulted all `Region Terrains` and `Site Holds` items to checked (`Uncheck to exclude`), and refreshed `Seed Region` on dialog visibility changes.
    - Removed character policy dropdowns and implemented strict Tier 1 -> Tier 2 character prioritization (drafted regions/sites + local Havens first, outside Havens as fallback) across `Starting Company` and `Wizards & Recruits`, fixing un-drafted Haven-region leaks and `CA1859`.
  - [x] Issue #54: Modular Workbench Tab 5 (Factions) with Priority Ranking & Prerequisite Auto-Drafting
    - Added Tab 5 (`Factions`) with `Factions in Play Deck (Resources)` (`0–15`, default `1`) and `Factions in Sideboard` (`0–10`, default `1`), `.workbench.json` profile persistence, live summary counts, and `Play Deck Size` / `Sideboard Size` budget validation.
    - Registered `RunFactionsDraft` as Step 4 of `_pipelineOrder`, scoping slice cleanup to `Secondary == "Faction"` and cards tagged with `Prerequisite For` a Faction card.
    - Enforced Hero-playable alignment, `Site Requirement` in `_deck.Sites`, `Specific` character presence, and recursive `TryResolvePrerequisiteChain` validation (checking each prerequisite's own `Site Requirement` and `Specific` character).
    - Implemented 4-tier prioritization (`Faction Bonus` [`Pool > Resources > Sideboard`, ignoring `*`/`All`] -> Character `Race` [`Pool > Resources > Sideboard`] -> Local region-group sites/Havens before outside Havens -> Remaining eligible), combined `Resources + Sideboard` copy limits, and same-section `Prerequisite` auto-drafting.
  - [x] Issue #55: Workbench Tier 3 Character Fallback, Specific Faction Tier 1 Priority, Pipeline Streamlining & Help Examples
    - Added Tier 3 out-of-region character fallback across `RunStartingCompanyDraft` and `RunNonStartingCharactersDraft` (`Tier 1 > Tier 2 > Tier 3` scoring) and updated Tab 2/3/4 status hints.
    - Added `SatisfiesRecruitSiteRequirement` in `RunNonStartingCharactersDraft` so `Wizards & Recruits` enforces character `Site Requirement` tags (e.g., Hobbits -> `Bag End`, *Fram Framson* -> `Framsburg`) against `_deck.Sites`, while `Starting Company` ignores `Site Requirement`.
    - Added `GetBestSpecificCharacterSourceRank` in `RunFactionsDraft` to promote `Specific` character matches into Tier 1 alongside `Faction Bonus` (`Pool > Resources > Sideboard`), enabling *Army of the Dead* (+ *Paths of the Dead*) to outrank generic racial matches.
    - Removed the pre-run confirmation `MessageBox` in `RunPipelineDraft()` for faster iteration (`Save Snapshot` / `Recall Snapshot` remain for rollback).
    - Tagged home-site-restricted characters with `Site Requirement` in `Rules_Support_Tags.json` and added `Resources/Help/workbench_examples.html` (linked from `deck_generation_engine.html` and compiled into `MECCG_Deck_Builder.chm`).
  - [x] Issue #56: Modular Workbench Tab 6 (Allies) with Bipartite Site-Collision Prevention & Character Site Requirement Docs
    - Added Tab 6 (`Allies`) with Resources/Sideboard ally spinners, `.workbench.json` persistence, live composition summary integration, and unified `Play Deck Size` / `Sideboard Size` budget validation.
    - Registered `RunAlliesDraft` as Step 5 of `_pipelineOrder`, scoping slice cleanup to `Secondary == "Ally"` and ally `Prerequisite For` enablers while enforcing Hero alignment, uniqueness limits, and recursive prerequisite chains.
    - Implemented bipartite maximum matching (`MaxSiteAssignments` / `TryMatchSite`) between Resources Factions and Allies to ensure both assets have distinct playable sites in 1-deck games, with a Pass 2 fallback and `⚠` notice enabling pipeline retry.
    - Updated `rules_support_tags.html` and `tagging_and_drafting_playbook.html` to document `Site Requirement` on Character cards for home-site-only non-starter recruits (Hobbits at *Bag End*, *Fram Framson* at *Framsburg*) alongside drafting Pattern D.
  - [x] Issue #57: Modular Workbench Tab 7 (Items), Multi-Tag Prerequisite OR/AND Logic & Pipeline Cleanup
    - Added Tab 7 (`Items`) with `Pool (0–2)` Minor Items, `Resources`/`Sideboard` counts across Minor/Major/Greater/Special tiers, dynamic `Role Focus` `CheckedListBox` (with `IsHandleCreated` guard on `ItemCheck`), `.workbench.json` persistence (`ExcludedItemRoles`), live summary counts, and `Play Deck Size` / `Sideboard Size` budget validation.
    - Registered `RunItemsDraft` as Step 6 of `_pipelineOrder` (`Pool Minor` -> `Special` -> `Greater` -> `Major` -> `Minor`, `Resources` before `Sideboard`), excluding `Gold Ring Item`, `Palantír`, and blank-`Site Requirement` Ring `Special Item` cards for dedicated future tabs.
    - Enforced flat hard eligibility guards: Hero alignment, `Role Focus`, site playability (`Site Requirement` or discrete flags `MinorItem`/`MajorItem`/`GreaterItem`/`Scroll` + same-site `Hoard`), `Region Requirement`, `Specific`, existing `Prerequisite`, and Hero character-race `Target Species` (ignoring creature races, `Target Item`, and `Prerequisite For`).
    - Standardized multi-tag `Prerequisite` evaluation (`;` = OR, `&&` = AND) across `TryResolvePrerequisiteChain` and `SatisfiesExistingPrerequisites`, removed redundant per-tab `RemoveAll` slice cleanup, added `Scroll` to *Scroll of Isildur*'s `Race` in `cards-dc.json`, and tagged *Wizard's Ring* with `Role: Influence` in `Rules_Support_Tags.json`.
 - [x] Issue #58: Modular Workbench Tab 8 (Rings), Decks Before Council & Special Ability Taxonomy
    - Added `Decks Before Council` (`1–4`, default `1`) to `General Settings` (with `.workbench.json` persistence) and relaxed `RunAlliesDraft`'s 1-deck disjoint-site check when `Decks Before Council >= 2`.
    - Added Tab 8 (`Rings`) and registered `RunRingsDraft` as Step 7 of `_pipelineOrder` with `Resources`/`Sideboard` counts for `Gold Rings`, `Allow Ring Test`, `Aid Ring Test`, `The One Ring` (`<= 1` combined guard), `Dwarven Rings`, `Magic Rings`, `Lesser Rings`, and `Wizard's Ring`.
    - Enforced `GoldRing = yes` site presence on Gold Ring drafts, added regex-based 2d6 roll-table probability scoring (`ScoreGoldRingForSpecials`) for non-hardcoded Gold Ring selection, counted earlier-tab `Special Ability` testers (*Gandalf*, *Scroll of Isildur*) toward test totals, and excluded Ring `Special Item` cards (`IsSpecialRingItem`) from `Items` (`IsEligibleBaseItem`).
    - Added `"Special Ability"` (`Gold Ring: Allow Test`, `Gold Ring: Aid Test`, `Palantír: Allow Use`) to `ExportTaxonomyDialog`, `Rules_Support_Tags.json`, `rules_support_tags.html`, and `tagging_and_drafting_playbook.html`, and removed those cards from `Target Item`.
  - [x] Issue #59: Modular Workbench Tab 9 (Palantíri), Special Ring Role Focus & Unique Innate Ability Counting Fix
    - Added Tab 9 (`Palantíri`) and registered `RunPalantiriDraft` as Step 8 of `_pipelineOrder` with `Resources`/`Sideboard` counts for `Palantíri (Items)` (default `1`/`0`) and `Allow Palantír Use` (default `1`/`0`), plus a per-tab `Role Focus` `CheckedListBox` (`_clbPalantirRoles`).
    - Enforced `Site Requirement` or `GreaterItem`/`Palantiri` site playability (covering *Lossadan Cairn* and `Hoard`), `Region Requirement`, `Specific`, `Prerequisite`, `Skill`/`Race` locks (*Sage*), and `Target Species` (*Elf; Wizard*), and added `IsPalantirItem`/`IsPalantirPackageCard` accounting to `UpdateDeckSummary` and `ValidateWorkbenchReadiness`.
    - Fixed unique innate `Special Ability` overcounting (`CountEffectiveSpecialAbility` / `FormatInnateAbilitySummary`) so `Unique` characters (*Gandalf*, *Saruman*) count at most once per distinct title per section.
    - Added `Special Ring Role Focus` (`_clbRingRoles`) on `Rings` for Special Rings, persisted new fields in `DeckAssemblySettings` (`.workbench.json`), refactored `IsRingPackageCard` to share `HasSpecialAbility`, and removed a duplicate blocked-tabs check in `ValidateWorkbenchReadiness`.
  - [x] Issue #60: Modular Workbench Tab 10 (Quests), Prerequisite For Retirement & Site/Ring Drafting Fixes
    - Added Tab 10 (`Quests`) and registered `RunQuestsDraft` as Step 9 of `_pipelineOrder` with `Resources`/`Sideboard` counts (default `1`/`0`), `Quest (Only)` + secondary `Role Focus` (`_clbQuestRoles`), `Auto-draft One Ring package` toggle (default checked), `.workbench.json` persistence, and `IsQuestPackageCard` summary/budget accounting.
    - Excluded `Role = Quest` items from `Items` (`IsEligibleBaseItem`), enforced `Skill`/`Target Species`/`Site Requirement`/`Region Requirement`/`Specific` and recursive `Prerequisite` drafting, prioritized already-satisfied prerequisite quests (Tier 1) over standalone/self-contained quests (Tier 2), and added `TryResolveOneRingPackage` (*Precious Gold Ring* + `Allow Ring Test` + *The One Ring*).
    - Fixed `SiteMatchesSingleRequirement` / `MatchesAnySiteRequirement` and `GetPlayableSiteIndices` to evaluate canonical hold types (`Dark-hold`, `Shadow-hold`, etc.) and `&&` multi-site clauses (`Bag End && Lórien`), restricted `IsOneRingCard` (and Special Ring predicates) to `Secondary == "Special Item"`, weighted `ScoreGoldRingForSpecials` toward *The One Ring*/*Dwarven Rings*, converted missing `GoldRing` site in `RunRingsDraft` to a soft `⚠` warning, and fixed `General Settings` label clipping (`lblOrder`, `lblProfileHint`) and stale pipeline status clearing.
    - Retired `Prerequisite For` across `Rules_Support_Tags.json`, `ExportTaxonomyDialog.cs`, and Help HTML docs (`rules_support_tags.html`, `tagging_and_drafting_playbook.html`, `deck_building_recipes.html`).
  - [x] Issue #61: Workbench Ring-Test Balancing, Unbiased Pipeline Fallback, and Simplified Max Adventure Sites
    - Added additive `Special Ability` tag `Gold Ring: Exclude One Ring` on *Ringlore* (`Rules_Support_Tags.json`) and documented it in `rules_support_tags.html` and `tagging_and_drafting_playbook.html`.
    - Updated `RunRingsDraft` and `TryResolveOneRingPackage` (`DeckAssemblyMatrixDialog.cs`) to exclude `Gold Ring: Exclude One Ring` testers for *The One Ring*, distinguish reusable (`Secondary != "Short-event"`) vs. single-use (`Short-event`) testers without name hardcoding, draft an extra tester when prior Special Rings consume existing short-events, apply variety-first copy-count ordering to `chosenAllow`, and display `oneRingDiag` skip diagnostics on the `Quests` tab.
    - Replaced `Sites.Count * 10 + totalCards` scoring in `RunPipelineDraft()` with `warningTabs.Count <= fewestWarnings` to keep the last minimum-warning attempt without biasing toward site-dense regions or multi-card quest packages.
    - Removed `Min Adventure Sites` (`_numMinSiteCount` / `MinSiteCount`) from `DeckAssemblyMatrixDialog.cs` and `workbench_examples.html`; updated `RunRegionsAndSitesDraft` to prefer the first random connected cluster with `> 0` and `<= Max Adventure Sites` (capping the smallest-excess cluster if all attempts exceed `Max`).
  - [x] Issue #62: Modular Workbench Tab 11 (Resource Events), X(Y) MP Summary, Workbench Default Tuning & Resource Target Species Cleanup
    - Added Tab 11 (`Resource Events`) and registered `RunResourceEventsDraft` as Step 10 of `_pipelineOrder`, featuring `Environment` counts (*Gates of Morning*, *Twilight*), `Additional Events in Sideboard` (default `4`), proportionate `Role Focus` auto-fill to `Play Deck Size` (min 1 per eligible role when slots permit), variety-first sampling, hard eligibility guards, `.workbench.json` persistence, and `ValidateWorkbenchReadiness()` checks.
    - Increased workbench `ClientSize` height (`690`) and `grpSummary` height (`256`), cleaned up `Decks Before Council` hint text, and added `ParseCardMPs` / `MP Total` (excluding Hazards, with `Resources` formatted as `X(Y)`) to `UpdateDeckSummary()`.
    - Tuned workbench & `DeckAssemblySettings` defaults: `Region Count = 5`, `Mind >=1` on all 5 `Starting Company` rows, `Primary Wizard = Random (2)`, `Secondary Wizard = Random (1, Sideboard)`, `Factions` & `Allies` `1`/`0`, `Pool Minor Items = 2` & `Sideboard Minor Items = 0`, and `0` defaults for `Rings`, `Palantíri`, and `Quests` Resources.
    - Narrowed `Target Species` on Resource cards strictly to company-bearer/caster requirements by removing enemy-attacker tags from *Dark Quarrels*, *The Cock Crows*, *Praise to Elbereth*, and `Nazgûl` on *Wizard's River-horses* in `Rules_Support_Tags.json`, updating `rules_support_tags.html` and `tagging_and_drafting_playbook.html`.
  - [x] Issue #63: Modular Workbench Tab 12 (Creatures), Global Hazard Targeting Mode & Nazgûl Support Package
    - Added `TailorHazardsToCurrentDeck` (`General Settings`) switching Hazards between Unknown Opponent (manual `Region Terrains` / `Site Holds` keying) and Target Current Deck (`_deck.Sites` keying).
    - Added Tab 12 (`Creatures`) and registered `RunCreaturesDraft` as Step 11 of `_pipelineOrder`, featuring `Nazgûl` (`<= 9` combined), `Nazgûl Support` (`Primary = Hazard` + event + `Target Species = Nazgûl`), `Standard Creatures (1.0)`, `Dual-Mode Half-Creatures (0.5)`, live Creature Equivalents readout, and proportionate `Species / Race` bucketing with variety-first sampling.
    - Updated `Rules_Support_Tags.json` (*Fury of the Iron Crown* -> `Target Species: Nazgûl` only), `rules_support_tags.html`, `tagging_and_drafting_playbook.html`, `README.md`, and `AI_CONTEXT.md`.
  - [x] Issue #64: Modular Workbench Tab 13 (Hazard Events), Multiline Tab Bar, Deck-Tab Tagging Focus Fix & Balrog of Moria Cleanup
    - Added Tab 13 (`Hazard Events`) and registered `RunHazardEventsDraft` as Step 12 of `_pipelineOrder` with `Doors of Night`/`Twilight` environment spinners (enforcing a combined 3-copy `Twilight` limit across `Resources + Hazards + Sideboard`), proportionate `Role Focus` auto-fill to `Play Deck Size` + Sideboard events, `Duration` (*The Will of Sauron*) held last and gated on `Long-event` presence, manual `Keying Filters` in Unknown Opponent mode vs. `_deck.Sites`/`Resources`/`Pool` `Target Item` and 3-way `Target Species` checks in Target Current Deck mode, and single combined Sideboard event counting in `UpdateDeckSummary()`.
    - Enabled `Multiline = true` on `_tabControl` (`DeckAssemblyMatrixDialog`) and increased `ClientSize` height to `714` so all 13 tabs display cleanly across two rows.
    - Fixed `Form1.UpdateMasterList()` to preserve the active Deck tab selection and `_inspectedCard` when mutating custom tags on a card selected in `Pool`/`Res`/`Haz`/`Side`/`Site`.
    - Removed secondary perk tags (`Lórien`, `Hollin; Redhorn Gate; Wold & Foothills`, `Role: Site`) from *Balrog of Moria* in `Rules_Support_Tags.json` and synchronized `rules_support_tags.html`, `tagging_and_drafting_playbook.html`, `README.md`, and `AI_CONTEXT.md`.
  - [x] Issue #65: Per-Cycle Site-Capacity Matching (Decks Before Council) Across Workbench Steps 4–9 & Workbench Help Reference Overhaul
    - Replaced Allies-only 1-deck site-collision checking (`GetPlayableSiteIndices` / `MaxSiteAssignments`) in `DeckAssemblyMatrixDialog.cs` with `DeckCycleSiteTracker` and capacitated bipartite matching (`MaxNonHavenSiteAssignments`, `TryMatchSite`), giving each non-Haven site `1` `Resources` tap slot per `Decks Before Council` cycle (`1–4`) while treating Havens as unlimited.
    - Enforced Pass 1 within-capacity drafting and Pass 2 `⚠` fallback warnings across `RunFactionsDraft`, `RunAlliesDraft`, `RunItemsDraft` (`Resources` tiers `Special -> Greater -> Major -> Minor`, leaving `Pool` Minor Items and `Sideboard` unrestricted), `RunRingsDraft` (`Resources` Gold Rings), `RunPalantiriDraft` (`Resources` Palantíri), and `RunQuestsDraft` (Quest Items and site-tapping `Prerequisite` cards).
    - Overhauled `Resources\Help\workbench_reference.html` with consistent `.scope-pill` / `<code>` / `<strong>` / `<em>` styling, fixed **Checklist Guards** examples, and documented **Per-Cycle Site-Capacity Matching** across Chapters `1`, `5`, `6`, `7`, `8`, `9`, and `10`.
  - [x] Issue #66: Workbench Help 5-Rule Style Contract, Ring Site-Overlap Diagnostics & General Settings Layout Fixes
    - Added `.lead-in` to `Resources\Help\style.css`, documented the **5-Rule Inline Style Contract** in `CODING_STANDARDS.md` §9.2 (for future rollout across remaining help topics), and overhauled `Resources\Help\workbench_reference.html` (adding the main-tab bridge in the intro, unbolding prose deck sections, unboxing prose numbers/`⚠`, separating `.lead-in` from UI controls, and documenting the Avatar `<= 3` cap).
    - Wired missing `warnings.AddRange(ringOverlaps)` in `RunRingsDraft`, refined `DeckCycleSiteTracker.BuildOverlapMessage` to isolate exact bipartite Hall bottleneck cards (`{N}-slot capacity filled by ...`), expanded `General Settings` status/summary bounds to prevent clipping (`_lblPipelineStatus`, `_lblDeckSummary`), and trimmed the `Allow Ring Test:` note on `Rings`.
  - [x] Issue #67: Pipeline Reordering (Rings -> Palantíri -> Items) & Grouped Shared Site-Pool Warnings
    - Reordered workbench execution pipeline (`_pipelineOrder`) and tab pages from `... -> Items -> Rings -> Palantíri -> ...` to `... -> Rings -> Palantíri -> Items -> ...`, allowing `Items` Pass 1 to steer around sites claimed by Gold Rings and Palantíri.
    - Routed `Aid Ring Test` in `RunRingsDraft` through `DeckCycleSiteTracker` so *Scroll of Isildur* is drafted within per-cycle site capacity before `Items` runs, skipping duplicate item drafting later while leaving it eligible under Greater Items if untaken.
    - Updated `DeckCycleSiteTracker.BuildOverlapMessage` to aggregate overflow cards sharing the same bottleneck into a single grouped `⚠` warning with a `Shared site pool` breakdown and slot-count diagnosis.
    - Synchronized documentation across `Resources\Help\workbench_reference.html`, `AI_CONTEXT.md`, and `README.md`.
  - [x] Issue #68: Workbench Left-Hand Sidebar Navigation, Numbered Steps 1–12 & Antigravity Edit Workflow
    - Replaced the multiline `TabControl` in `DeckAssemblyMatrixDialog` with a fixed left-hand vertical navigation sidebar (`General Settings` + Steps `1.`–`12.`), active accent bar, reusable disposed nav fonts, and live `⚠` step badges.
    - Fixed `8. Items` scope note clipping (`lblItemScopeNote`, `grpItemCounts`/`grpItemRoles` height `226`, `_lblItemStatus` at `Y=256`).
    - Aligned `Resources\Help\workbench_reference.html` to `General Settings` + Steps `1.`–`12.` (fixing stale `Rings`/`Palantíri`/`Items` chapter headings).
    - Documented the `AI Studio -> Antigravity Edit Delivery Workflow` in `AI_CONTEXT.md` and updated `README.md`.
  - [x] Issue #69: Refactor Modular Workbench into Domain-Scoped Partial Classes
    - Decomposed the monolithic `DeckAssemblyMatrixDialog.cs` (~3,400 lines) into five domain-focused `sealed partial class` files:
      1. `DeckAssemblyMatrixDialog.cs`: Core shell, layout host, sidebar navigation, centralized pipeline orchestrator (`RunPipelineDraft`), session snapshots, and `.workbench.json` profile persistence.
      2. `DeckAssemblyMatrixDialog.TheaterAndCharacters.cs`: Steps 1–3 (`Regions & Sites`, `Starting Company`, `Wizards & Recruits`), page builders (`BuildRegionsAndSitesPage`, `BuildStartingCompanyPage`, `BuildWizardsAndRecruitsPage`), drafting runners, and character/cluster helpers.
      3. `DeckAssemblyMatrixDialog.ResourcesAndQuests.cs`: Steps 4–9 (`Factions`, `Allies`, `Rings`, `Palantíri`, `Items`, `Quests`), page builders, checklist populators, and drafting runners.
      4. `DeckAssemblyMatrixDialog.EventsAndHazards.cs`: Steps 10–12 (`Resource Events`, `Creatures`, `Hazard Events`), page builders, checklist populators, proportionate allocators, and hazard keying logic.
      5. `DeckAssemblyMatrixDialog.SiteTrackerAndModels.cs`: `DeckCycleSiteTracker` bipartite matching engine, cross-tab card eligibility guards, and settings DTOs (`CharacterRowControls`, `CharacterRowSettings`, `DeckAssemblySettings`).
    - Extracted UI page construction from the constructor into dedicated `Build*Page()` methods for each step, encapsulating both UI and domain logic within each partial file with zero behavioral regression.
  - [x] Issue #70: Workbench RichTextBox Status Formatting, Checklist Label Standardization & Help Directory Polish
    - Replaced all 13 workbench status/summary `Label` controls with scrollable `RichTextBox` (`_rtb*`) panels (`CreateStatusRichTextBox`, `SetRichPlainStatus`, `SetRichBulletedStatus`) featuring hanging indents, styled label/value/warning typography, and `Rtf` retry-snapshot restoration in `RunPipelineDraft`.
    - Removed `lblOrder` on `General Settings` (streamlining pipeline button layout and expanding `_rtbDeckSummary`), applied natural sorting (`NaturalSort.Extension`) to status/summary card and site lists, split `Creatures` status bullets by tier, added `Adventure Sites:` bullets on `Regions & Sites`, and tightened `Creatures` column spacing.
    - Standardized checklist labels (`Role`, `Special Ring Role`, `Creature Race`) across `DeckAssemblyMatrixDialog*.cs`, `README.md`, `CODING_STANDARDS.md`, and `Resources/Help/workbench_reference.html`.
    - Simplified the `Workbench Step Directory` table in `workbench_reference.html` (recompiled into `MECCG_Deck_Builder.chm`) and updated Rule 2 & Rule 4 of the 5-Rule Inline Style Contract in `CODING_STANDARDS.md`.
  - [x] Issue #71: Workbench UX & Peer Window Polish, Hybrid Seed Filtering, Haven Reachability Guard & Terminology Alignment
  - [x] Issue #72: Workbench Character Grids Overhaul (2-Tier Priority, Unbiased 100-Trial Search, Field-Exit Validation, 0-DI Support) & F5 Pipeline Busy Feedback
- [ ] **v1.0.0 - First Public Release**

## Clean-Slate Schema Enrichment Pipeline

To eliminate the inconsistencies, historical quirks, and unstructured text burdens of the legacy Cardnum dataset, we adopt a phased, clean-slate schema redesign:

1. **Card Type Slicing:** Process cards partition-by-partition based on `Primary` card type (e.g., `Site`, `Character`, `Resource`, `Hazard`).
2. **Catalog & Text Ingestion:** 
   - Export minimal reference data from the app (`Card Name`, `Set`, `Card Text`).
   - Pair with the raw `cards-dc.json` entries to cross-reference existing attributes.
3. **Deck-Builder-First Schema Engineering:** 
   - Extract gameplay rules into structured, unambiguous attributes (e.g., parsing composite automatic attacks, movement path structures, recruitment hooks, and conditional item restrictions).
   - Normalize polarity (e.g., CP burden vs check resistance) and eliminate legacy shorthand codes.
4. **Intermediate JSON Output:** Deliver an enriched, strongly-typed JSON dataset for the slice (e.g., `sites_enriched.json`).
5. **CSV Pipeline Review:**
   - Convert the enriched JSON into CSV.
   - Import into MECCG Deck Builder via `Taxonomy > Import Taxonomy (CSV)...` to inspect and review the taxonomy live in the UI and filter slots.
6. **Unified Master Schema Consolidation:** Once all partitions are finalized, consolidate the schemas into a unified local master catalog (`cards-dc.json` / `Card.cs`).

