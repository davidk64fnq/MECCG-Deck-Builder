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
- ManageCustomTagsForm.cs: Dedicated modal dialog for creating, renaming, and deleting custom keys and values.
- AddTagDialog.cs / PromptDialog.cs: Quick modal dialogs for assigning and authoring tags from context menus.
- Card.cs: Strongly-typed card domain model with dynamic attribute map.
- CardSet.cs: Strongly-typed set domain model.
- CardCatalogService.cs: Card catalog management, JSON syncing, filter option discovery.
- CardFilterService.cs: Custom taxonomy management and faceted filtering engine.
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

---

## Critical Data Structures & Conventions

### Card Representations
1. Domain Representation (`Card`):
   - Strongly-typed class containing all standard card properties and dynamic attribute lookup.
2. Catalog Management (`CardCatalogService`):
   - Enumerable `IReadOnlyList<Card>` and O(1) indexed `IReadOnlyDictionary<string, Card> CardsById`.

### Deck Sections (Tabs)
Five deck sections correspond to standard MECCG rules:
1. Pool (`DeckSectionType.Pool`)
2. Resources (`DeckSectionType.Resources`)
3. Hazards (`DeckSectionType.Hazards`)
4. Sideboard (`DeckSectionType.Sideboard`)
5. Sites (`DeckSectionType.Sites`)

### Title Bar Counting Format
Title format: MECCG Deck Builder - "<Title>" (<Pool>/<Res>[<Characters>]/<Haz>/<Side>/<Site>)
- Resource characters are dynamically counted by inspecting cards in Resources where Primary == "Character".

---

## Milestone Execution & Progress Tracking

- [x] **v0.1.0 - Foundation & Documentation** (100% complete)
- [x] **v0.2.0 - Modernization & Bug Fixes** (100% complete)
- [x] **v0.3.0 - Domain Refactoring & Tag Decoupling** (100% complete)
- [ ] **v0.4.0 - Curated Filter Taxonomy** (Next)
- [ ] **v0.5.0 - Procedural Deck Generator**
- [ ] **v1.0.0 - First Public Release**

### Completed Milestones

#### v0.1.0 - Foundation & Documentation
- Issue #1: Integrated CHM help system, build verification, and repository documentation standards.

#### v0.2.0 - Modernization & Bug Fixes
- Issue #4: Card Filter Bug - Fixed loop indexing and case-insensitive dictionary safety.
- Issue #5: Expansion Filter Values Missing - Populated filter values across all expansions and dreamcards.
- Issue #6: Offline Fallback Packaging - Bundled offline fallback JSON datasets.
- Issue #7: MemoryCache Disk Miss & GDI+ File Locks - Non-locking memory stream image loading.
- Issue #8: Downloader GDI+ Leaks & Direct URLs - Fixed batch downloader resource handling.
- Issue #9: Card ID Indexing Assumption & Exporter Safety - Secured dictionary lookups and exporter routines.
- Issue #10: Async Network Operations & UI Freezing - Implemented non-blocking async operations with CancellationTokenSource race protection.

#### v0.3.0 - Domain Refactoring & Tag Decoupling
- Strongly-typed domain models (`Card`, `CardSet`, `Deck`, `DeckSectionType`).
- Decoupled headless services (`CardCatalogService`, `CardFilterService`, `DeckExportService`).
- Issue #16: Decoupled custom taxonomy authoring from catalog filtering:
  - Converted filter ComboBoxes 3 & 4 to read-only `DropDownList`.
  - Added dedicated `ManageCustomTagsForm` (Filter > Manage Custom Tags...) for taxonomy authoring.
  - Added `AddTagDialog` and `PromptDialog` to card context menus for inline tag creation and assignment without wiping active filter selections.
  - Seamless tag remapping: context menus always show categories, display checkmarks for active tags, and allow direct reassignment without manual deletion.
  - Removed redundant runtime `Filter > Delete Key` menu.