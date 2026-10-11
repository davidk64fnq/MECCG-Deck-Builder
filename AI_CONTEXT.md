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
- ManageCustomTagsForm.cs: Dedicated modal dialog for managing custom categories and tags.
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

## Standardized Taxonomy & Filtering Vocabulary

1. **Official Card Attributes (Filter Slots 1 & 2):**
   - Canonical rules properties from the database: `Attribute` (e.g. *Race*, *Skill*) and `Value` (e.g. *Dúnadan*, *Warrior*).
   - Inspected via context menu item: `Card Attributes`.
2. **Custom User Taxonomy (Filter Slots 3 & 4):**
   - User-defined taxonomy: `Category` (e.g. *Archetype*, *Role*) and `Tag` (e.g. *Ring Destruction*, *Scout Support*).
   - Inspected via context menu item: `Assigned Tags`.
   - Card tagging actions: `Assign Tag` (with `+ Author New Tag...` and `+ Add Tag...`), `Remove Tag`.
   - Dedicated manager: `Filter > Manage Custom Tags...` (`Categories`, `Tags`, `Add...`, `Rename...`, `Delete`).
   - Tag file persistence: `Filter > Load Tag Set...` and `Filter > Save Tag Set...`.

---

## Milestone Execution & Progress Tracking

- [x] **v0.1.0 - Foundation & Documentation** (100% complete)
- [x] **v0.2.0 - Modernization & Bug Fixes** (100% complete)
- [x] **v0.3.0 - Domain Refactoring & Tag Decoupling** (100% complete)
- [x] **v0.3.5 - UI & Documentation Vocabulary Standardization** (100% complete)
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
- Issue #16: Decoupled custom taxonomy authoring from catalog filtering.

#### v0.3.5 - UI & Documentation Vocabulary Standardization
- Standardized terminology across UI, context menus, dialogs, and CHM HTML documentation:
  - Replaced programmer jargon (`Key`, `Value`, `Key Value`) with clean domain terms: `Category`, `Tag`, `Card Attributes`, `Assigned Tags`, `Assign Tag`, `Remove Tag`.
  - Clarified file operations (`Load Tag Set...` / `Save Tag Set...`).