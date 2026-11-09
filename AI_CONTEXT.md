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

- Form1.cs / Form1.Designer.cs: Main UI: master listbox, picture box, dual-mode right pane (`Deck Builder` vs `Card Taxonomy Inspector` with monospace metadata view and pinned Quick Assign bar), two-tier filter GroupBoxes, four-header workflow menu bar (`Deck`, `Catalog`, `Taxonomy`, `Help`). Flush height alignment across master list, card artwork, and right pane.- AddTagDialog.cs / PromptDialog.cs: Quick modal dialogs for assigning and authoring tags from context menus.
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
- RunTaxonomyRulesDialog.cs: Modal extraction dialog with four player-friendly tool modes: Scan Card Text for Catalog Entities, Cross-Card Attribute Lookup, Custom Pattern Search (Regex), and Batch Rules from File (.json).

---

## Standardized Taxonomy & Filtering Vocabulary

1. **Official Card Attributes (Filter Slots 1 & 2):**
   - Housed inside `GroupBoxCardAttributes` ("Card Attributes (Official Database)").
   - Canonical rules properties: `Attribute` (e.g. *Race*, *Skill*, *Specific*) and `Value` (e.g. *Dúnadan*, *Warrior*).
   - Inspected via context menu item: `Card Attributes`.
   - Expansion sets filtered globally via top **Catalog** menu checkboxes.

2. **Custom User Taxonomy (Filter Slots 3 & 4):**
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
     - `Prerequisite`: Assigned to quest cards to declare required enabler cards or relics (e.g. `Paths of the Dead` on *Army of the Dead*; `Narsil; Reforging` on *Andúril*).
     - `Prerequisite For`: Assigned to enabler cards (e.g. `Army of the Dead` on *Paths of the Dead*).
     - *Enabler Shield Pattern:* Generic open resource rules specify `"Prerequisite For": "[Blank]"` so specialized enablers are never drafted into decks lacking their parent quest.
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

## Modular Slot Matrix Architecture & Parametric Deck Assembly

### 1. Vision & Core Objectives
To evolve procedural generation beyond monolithic `.json` templates into a modular, workbench-style deck constructor:
- **Decoupled Components:** Split full deck rule sets into reusable, modular `.json` components by theme and section (e.g., Company packages, Hazard suites, Regional site routes, and Strategy/Quest engines).
- **Interactive Assembly Matrix (Alternative B):** A unified configuration dialog where players select modules via dropdown slots (e.g., *Company*, *Theater/Region*, *Strategy*, *Hazard Suite*) rather than stepping through a rigid multi-page wizard or editing raw JSON files.
- **In-App Visual Designer (Long-Term):** Future capability to visually author, parameterize, and export component files directly inside the application using existing faceted filter controls.

---

### 2. Parametric Budgeting & Variable Deck Sizing
To prevent components from hardcoding static integer counts (`"Count": 7`) that break when deck sizes change (e.g., 25/25 starter, 30/30 tournament, 36/36, 40/40 drafts):
- **Runtime Budget Injection:** The Matrix UI lets the user select target deck sizes (e.g., 30 Resources / 30 Hazards / 20 Sideboard) and passes these targets into the orchestrator.
- **Proportional Allocation (`Percent`):** Component rules can declare fractional budgets (e.g., `"Percent": 20` for combat gear, `"Percent": 10` for factions, `"Percent": 70` for open utility events).
- **Integer Resolution & Surplus Absorption:** The orchestrator converts percentages to integer counts (`Budget * Percent`) and allocates any rounding remainders to the largest open utility rule.

---

### 3. Multi-Directional / Order-Independent Assembly (Inverted Drivers)
Because deck construction does not always originate from the leader character outward, the execution pipeline supports flexible dependency chaining:
- **Leader-First Construction (Outward):**
  1. `Company Module` selects a leader (e.g., *Dáin II*) $\rightarrow$ `SaveAs: "Leader"`.
  2. `Site Module` queries `{Leader.Category:Home Region}` and `{Leader.Home}` to draft matching strongholds and contiguous regions.
  3. `Resource Module` queries `{Leader.Category:Faction Bonus}` to draft matching faction cards.
- **Region-First Construction (Inward / Geographic Anchor):**
  1. `Theater/Region Module` establishes a geographic territory (e.g., *Gondor / Anórien*) $\rightarrow$ `SaveAs: "AnchorRegion"`.
  2. `Company Module` queries `"Home Region": "{AnchorRegion.Region}"`, constraining candidate leaders strictly to characters native to that theater (*Boromir*, *Faramir*, *Beregond*).
  3. `Site Module` drafts contiguous site paths and strongholds within that regional cluster.
  4. `Hazard Module` keys regional hazards and creatures directly to the theater's terrain and region requirements.
- **Strategy/Quest-First Construction:**
  1. `Quest Module` selects an objective (e.g., *Army of the Dead*) $\rightarrow$ `SaveAs: "Quest"`.
  2. Chained modules resolve required champions (`{Quest.Specific}` $\rightarrow$ *Aragorn II*), prerequisites (`{Quest.Category:Prerequisite}` $\rightarrow$ *Paths of the Dead*), and staging sites (`{Quest.Category:Site Requirement}`).

---

### 4. Component Contracts & Context Continuity
- **Shared In-Memory Context:** Leverages the existing `contextVariables` dictionary (`SaveAs` / `{...}`) across sequential component executions without modifying core generation evaluation logic.
- **Contract Declarations:** Component headers declare their context dependencies:
  - `"RequiresContext": [ "Leader" ]` (Prerequisites that must be present in memory).
  - `"ProvidesContext": [ "Leader", "ExpeditionSites" ]` (Variables populated for downstream modules).
- **Global Constraints Maintained:** Deck uniqueness limits, 3-copy rules, and alignment compatibility remain globally enforced across all evaluated components.