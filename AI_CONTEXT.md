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
   - Canonical rules properties: `Attribute` (e.g. *Race*, *Skill*) and `Value` (e.g. *Dúnadan*, *Warrior*).
   - Inspected via context menu item: `Card Attributes`.
   - Expansion sets filtered globally via top **Catalog** menu checkboxes.
2. **Custom User Taxonomy (Filter Slots 3 & 4):**
   - Housed inside `GroupBoxCustomTags` ("Custom Tags (User Taxonomy)").
   - User-defined taxonomy: `Category` (e.g. *Strategy*, *Role*) and `Tag` (e.g. *Ring Destruction*, *Scout Support*).
   - Inspected via context menu item: `Assigned Tags`.
   - Card tagging actions: `Assign Tag` (with `+ Author New Tag...` and `+ Add Tag...`), `Remove Tag`.
   - Dedicated manager: `Taxonomy > Manage Custom Tags...` (`Categories`, `Tags`, `Add...`, `Rename...`, `Delete`).
   - Tag file persistence: `Taxonomy > Load Tag Set...`, `Taxonomy > Merge Tag Set...`, and `Taxonomy > Save Tag Set...`.
   - Global filter reset: `Taxonomy > Clear All Active Filters` (<kbd>Ctrl+R</kbd>).
3. **Procedural Rule Schema Symmetry:**
   - `"Attributes"`: Dictionary of official criteria (`Attribute: Value`, e.g. `{"Race": "Dwarf"}`).
   - `"Categories"`: Dictionary of custom criteria (`Category: Tag`, e.g. `{"Faction Bonus": "Wood-elves"}`).
   - Placeholders: `{Variable.AttributeName}` for official attributes (including `{Variable.Home}` resolving both named sites and dynamic predicates), `{Variable.Category:CategoryName}` (or `{Variable.Tag:...}`) for custom tags.
   - Dynamic Predicates: Evaluates `Attribute=Value`, `Attribute!=Value`, and compound `&&` logic (e.g. `Region=Under-deeps && Site!=Dark-hold`, `Site=Haven`, `Site=Dark-hold`, `Adjacent Sites!=[Blank]`) against candidate cards dynamically, with diacritic resilience and canonical hold-type normalization.
   - Consolidated Geography Categories: Location rules unified into `Site Requirement` and `Region Requirement` for cards in scope, with `Home Region` preserved for character origin joins.
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