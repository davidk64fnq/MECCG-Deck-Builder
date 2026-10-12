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

- Form1.cs / Form1.Designer.cs: Main UI: master listbox, picture box, 5 deck tabs, two-tier filter GroupBoxes, menus.
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
   - Housed inside `GroupBoxCardAttributes` ("Card Attributes (Official Database)").
   - Canonical rules properties: `Attribute` (e.g. *Race*, *Skill*) and `Value` (e.g. *Dúnadan*, *Warrior*).
   - Inspected via context menu item: `Card Attributes`.
2. **Custom User Taxonomy (Filter Slots 3 & 4):**
   - Housed inside `GroupBoxCustomTags` ("Custom Tags (User Taxonomy)").
   - User-defined taxonomy: `Category` (e.g. *Archetype*, *Role*) and `Tag` (e.g. *Ring Destruction*, *Scout Support*).
   - Inspected via context menu item: `Assigned Tags`.
   - Card tagging actions: `Assign Tag` (with `+ Author New Tag...` and `+ Add Tag...`), `Remove Tag`.
   - Dedicated manager: `Filter > Manage Custom Tags...` (`Categories`, `Tags`, `Add...`, `Rename...`, `Delete`).
   - Tag file persistence: `Filter > Load Tag Set...` and `Filter > Save Tag Set...`.

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
- [ ] **v0.5.0 - Procedural Deck Generator**
- [ ] **v1.0.0 - First Public Release**