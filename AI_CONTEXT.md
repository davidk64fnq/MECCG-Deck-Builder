# AI Development Context: MECCG Deck Builder

## Project Summary
- Type: Windows Forms (WinForms) Desktop Application
- Language / Framework: C# (.NET 8.0, net8.0-windows)
- Domain: Deck Builder for Middle-earth: Collectible Card Game (MECCG) (Iron Crown Enterprises / ICE & Council of Elrond Dreamcards).
- Primary Data Sources:
  - Card Data: cards-dc.json (synced from GitHub rezwits/cardnum/fdata/cards-dc.json with local fallback).
  - Set Data: sets-dc.json (synced from GitHub rezwits/cardnum/fdata/sets-dc.json with local fallback).
  - Images: https://cardnum.net/img/cards/{set}/{imageName} (with planned PlayMECCG mirror fallback).
- Help File: MECCG_Deck_Builder.chm (compiled via Microsoft HTML Help Workshop).

---

## Application Architecture & Core Files

- Form1.cs / Form1.Designer.cs: Main UI: master listbox, picture box, 5 deck tabs, 4 filter combo pairs, menus.
- Cards.cs: Card catalog management, JSON syncing, Cardnum filter lookup, deck exporters.
- CardnumCard.cs: POCO class representing raw card schema from Cardnum JSON.
- CardnumSet.cs: POCO class representing card set metadata from Cardnum JSON.
- CardImageCache.cs: Two-tier image cache (MemoryCache + local disk folders like METW/) with async I/O.
- KeyValue.cs: User-defined custom tag system (keys, values, and per-card tag mappings).
- Constants.cs: Enums (CardListField, SaveType) and string constants (URLs, filenames, suffixes).
- OpenCloseDeck.cs: DTO used for serializing/deserializing .json deck save files.
- OpenCloseFilter.cs: DTO used for serializing/deserializing .json custom filter sets.
- Messages.cs: Centralized message box text helper.
- Program.cs: WinForms entry point; registers Windows-1252 code page provider.

---

## Critical Data Structures & Conventions

### Card Representations
1. Catalog Representation (Cards.cards & Cards.cardsById):
   - Stored as `List<SortedDictionary<string, string>>` for catalog enumeration.
   - Indexed via `Dictionary<string, SortedDictionary<string, string>> cardsById` for safe O(1) lookups.
   - Keys include: id, set, fullCode, cardname, text, imageName, plus all attributes in `Cards.filterKeys` (Primary, Alignment, Race, Skill, etc.).
2. List Item Representation (List<string[]>):
   - Master list and deck tab lists use string[] with indices defined by CardListField:
     - (int)CardListField.name (0) -> Card Name
     - (int)CardListField.image (1) -> Image filename
     - (int)CardListField.set (2) -> Set code (e.g., METW, MEDM)
     - (int)CardListField.id (3) -> String representation of catalog ID

### Deck Sections (Tabs)
Five deck sections correspond to standard MECCG rules:
1. poolList (TabPagePool / ListBoxPool)
2. resourceList (TabPageResources / ListBoxResources)
3. hazardList (TabPageHazards / ListBoxHazards)
4. sideboardList (TabPageSideboard / ListBoxSideboard)
5. siteList (TabPageSites / ListBoxSites)

### Title Bar Counting Format
Title format: MECCG Deck Builder - "<Title>" (<Pool>/<Res>[<Characters>]/<Haz>/<Side>/<Site>)
- Resource characters are dynamically counted by inspecting cards in resourceList where Primary == "Character".

---

## Milestone Execution & Progress Tracking

- [x] **v0.1.0 - Foundation & Documentation** (100% complete)
- [x] **v0.2.0 - Modernization & Bug Fixes** (100% complete)
- [ ] **v0.3.0 - Domain Refactoring** (Next)
- [ ] **v0.4.0 - Curated Filter Taxonomy**
- [ ] **v0.5.0 - Procedural Deck Generator**
- [ ] **v1.0.0 - First Public Release**

### Completed Milestones

#### v0.1.0 - Foundation & Documentation
- Issue #1: Integrated CHM help system, build verification, and repository documentation standards.

#### v0.2.0 - Modernization & Bug Fixes
- Issue #4: Card Filter Bug (Cards.cs) - Fixed loop indexing (`keyValuePairs[index]`) and added `card.TryGetValue` safety with case-insensitive matching.
- Issue #5: Expansion Filter Values Missing (Cards.cs) - Removed restrictive set checks in `ImportCardnumCardInfo`; all released sets and dreamcards now populate filters.
- Issue #6: Offline Fallback Packaging - Bundled `cards-dc.json` and `sets-dc.json` into project root with `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`.
- Issue #7: MemoryCache Disk Miss & GDI+ File Locks - Added `Cache.Set` on disk hits and implemented `LoadImageNonLocking` using memory stream cloning.
- Issue #8: Downloader GDI+ Leaks & Direct URLs - Added `using` statements and `File.Exists` pre-checks to `ToolStripMenuToolsGetImages_Click`; switched to direct `raw.githubusercontent.com` endpoints.
- Issue #9: Card ID Indexing Assumption & Exporter Safety - Introduced `cardsById` dictionary and `GetCardById` helper; secured `GetCardFilterPairs`, `GetCardIndex`, and all exporters against `KeyNotFoundException`.
- Issue #10: Async Network Operations & UI Freezing - Converted `CardImageCache` and `ToolStripMenuToolsGetImages_Click` to `async/await` with live progress reporting; added `CancellationTokenSource` race-condition protection to `ListBox_SelectedIndexChanged`.

---

## Active Roadmap: v0.3.0 - Domain Refactoring

### Core Goals
1. Strongly-Typed Domain Models:
   - Replace raw `string[]` and `SortedDictionary<string, string>` with immutable/strongly-typed records or classes (e.g., `Card`, `DeckCard`, `Deck`, `DeckSection`).
   - Use strongly-typed enums for fixed domain concepts (`CardType`, `DeckSectionType`, `Alignment`, `Race`).
2. Decoupled Architecture (Headless Domain Logic):
   - Refactor `Cards.cs` and `KeyValue.cs` into decoupled domain services (`CardCatalogService`, `DeckExportService`, `CardFilterService`).
   - Eliminate direct WinForms control references (`ListBox`, `ComboBox`) in domain calculations.
3. Asynchronous Catalog Initialization:
   - Provide an async startup path (`InitializeAsync`) so catalog syncing and parsing never block application launch.

---

## Future Roadmap

### v0.4.0: Curated Filter Taxonomy (The First Three Sets)
- Establish a comprehensive custom tag library (`KeyValue.cs` / JSON presets) for the original three expansion sets:
  - The Wizards (METW)
  - The Dragons (METD)
  - Dark Minions (MEDM)
- Custom tags will classify cards by strategic archetype, hazard strategy (e.g., Dragon AH, Undead/Nazgûl, Corruption, Wilderness/Creatures), resource strategy (e.g., Faction rush, Item gathering, Ring hunting, Scout/Information), character roles, and region/movement compatibility.

### v0.5.0: Rule-Based Procedural Deck Generator
- Goal: Enable weekly generation of unique, cohesive, playable decks for online matches (Play MECCG and Tabletop Simulator) with minimal manual assembly.
- Concept:
  1. Deck Building Rule Templates: Define abstract constraints such as:
     - Starting Wizard/Company composition (mind limit, skill distribution, starting items).
     - Hazard strategy archetype (e.g., 8-10 Creatures of specific type, 6-8 environment hazards, corruption events).
     - Resource victory condition target (e.g., 20+ MPs focused across Factions + Major/Greater items).
     - Site network coherence (ensure sites match destination regions and haven travel paths).
     - Exact deck size limits (e.g., exactly 30 Resources / 30 Hazards or 25/25, plus Sideboard).
  2. Deck Generator Sets (Rule Profiles):
     - Users can configure, name, and save collections of rules in the UI as reusable profiles (e.g., "Radagast Beast Master", "Pallando Faction Diplomat", "Dragon Lore Ring Hunter").
  3. Procedural Assembly Engine:
     - The engine evaluates available cards against the Generator Set's rules and custom tags, assembling a cohesive, randomized deck that satisfies all constraints and synergy rules.
     - Automatically generates matching site/region decks to support the company's movement paths.

### v1.0.0: First Public Release
- End-to-end integration testing for Tabletop Simulator and Play MECCG export formats.
- Final UI polish, keyboard shortcut accessibility, and release packaging/installer.