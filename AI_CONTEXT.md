# AI Development Context: MECCG Deck Builder

## Project Summary
- Type: Windows Forms (WinForms) Desktop Application
- Language / Framework: C# (.NET 8.0, net8.0-windows)
- Domain: Deck Builder for Middle-earth: Collectible Card Game (MECCG) (Iron Crown Enterprises / ICE & Council of Elrond Dreamcards).
- Primary Data Sources:
  - Card Data: cards-dc.json (synced from GitHub rezwits/cardnum/fdata/cards-dc.json with local fallback).
  - Set Data: sets-dc.json (synced from GitHub rezwits/cardnum/fdata/sets-dc.json with local fallback).
  - Images: https://cardnum.net/img/cards/{set}/{imageName} (with planned PlayMECCG fallback/mirror).
- Help File: MECCG_Deck_Builder.chm (compiled via Microsoft HTML Help Workshop).

---

## Application Architecture & Core Files

- Form1.cs / Form1.Designer.cs: Main UI: master listbox, picture box, 5 deck tabs, 4 filter combo pairs, menus.
- Cards.cs: Card catalog management, JSON syncing, Cardnum filter lookup, deck exporters.
- CardnumCard.cs: POCO class representing raw card schema from Cardnum JSON.
- CardnumSet.cs: POCO class representing card set metadata from Cardnum JSON.
- CardImageCache.cs: Two-tier image cache (MemoryCache + local disk folders like METW/).
- KeyValue.cs: User-defined custom tag system (keys, values, and per-card tag mappings).
- Constants.cs: Enums (CardListField, SaveType) and string constants (URLs, filenames, suffixes).
- OpenCloseDeck.cs: DTO used for serializing/deserializing .json deck save files.
- OpenCloseFilter.cs: DTO used for serializing/deserializing .json custom filter sets.
- Messages.cs: Centralized message box text helper.
- Program.cs: WinForms entry point; registers Windows-1252 code page provider.

---

## Critical Data Structures & Conventions

### Card Representations
1. Catalog Representation (Cards.cards):
   - Stored as List<SortedDictionary<string, string>>.
   - Keys include: id, set, fullCode, cardname, text, imageName, plus all attributes in Cards.filterKeys (Primary, Alignment, Race, Skill, etc.).
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

## Status of Known Bugs & Refactoring Tasks

### Resolved
1. [RESOLVED - Issue #4] Card Filter Bug (Cards.cs):
   Fixed loop indexing bug (`keyValuePairs[index]`) and added `card.TryGetValue` safety checks with case-insensitive matching and short-circuit evaluation.
2. [RESOLVED - Issue #5] Expansion Filter Values Missing (Cards.cs):
   Removed hardcoded restriction gating `SetKeyValues` to METW and MEUL. All released cards and dreamcards now register their attributes into filter dropdowns.
3. [RESOLVED - Issue #6] Offline Fallback JSON Packaging:
   `cards-dc.json` added to project source and both JSON files set to `Copy if newer` (`PreserveNewest`) to guarantee offline fallback on fresh repository clones.

### Remaining Active Pain Points
4. Memory Cache Disk Miss (CardImageCache.cs):
   Loading an image from disk returns a new Bitmap without saving it to `MemoryCache`, causing redundant disk I/O on every card click.
5. GDI+ Resource Leaks & File Locking:
   - `PictureBoxCardImage.Image` reassignments do not dispose previous unmanaged Bitmap instances.
   - `new Bitmap(path)` locks image files on disk. Needs byte-array or non-locking memory stream cloning.
6. Blocking Network Calls:
   `Cards.cs` and `CardImageCache.cs` use blocking `.Result` / `.GetResult()` on `HttpClient`, freezing the UI thread during downloads.
7. GitHub URL Direct Endpoints:
   `Constants.cs` uses `github.com/.../blob/.../?raw=true` which requires an HTTP 302 redirect. Should be updated to direct `raw.githubusercontent.com/...` endpoints.
8. Card ID Indexing Assumption:
   Lookup by `Convert.ToInt32(cardId)` assumes catalog array index == card ID. Should be migrated to a dictionary or strongly-typed model.

---

## Image Sourcing & Asset Strategy

- Immediate Priority (Phase 1):
  - Pre-cache all card artwork for the first three sets: The Wizards (METW), The Dragons (METD), and Dark Minions (MEDM) using the in-app download tool.
  - Local disk storage (`metw/`, `metd/`, `medm/`) provides complete offline immunity from external host downtime.
- Long-Term Asset Architecture:
  - Cardnum is a single hobby endpoint that could become unavailable or outdated for newer virtual releases. PlayMECCG / GCCG hosts all assets up to modern Dreamcard sets (including *Bay of Utum*).
  - Abstract image resolution behind an `ICardImageProvider` interface.
  - Multi-tier retrieval chain: `MemoryCache -> Local Disk -> Cardnum.net -> PlayMECCG mirror`.

---

## Target Modernization Goals
- Migrate raw `string[]` and `SortedDictionary<string, string>` structures to a strongly-typed `Card` model.
- Convert `HttpClient` downloads to asynchronous (`async/await`) with UI progress indication and cancellation tokens.
- Replace GDI+ file-locking image loads with non-locking memory stream clones and properly dispose old images.
- Add unit tests for deck export formatting (TTS, Play MECCG, Cardnum).

---

## Future Directions & Roadmap

### Phase 1: Curated Filter Taxonomy (The First Three Sets)
- Establish a comprehensive custom tag library (`KeyValue.cs` / JSON presets) for the original three expansion sets:
  - The Wizards (METW)
  - The Dragons (METD)
  - Dark Minions (MEDM)
- Custom tags will classify cards by strategic archetype, hazard strategy (e.g., Dragon AH, Undead/Nazgûl, Corruption, Wilderness/Creatures), resource strategy (e.g., Faction rush, Item gathering, Ring hunting, Scout/Information), character roles, and region/movement compatibility.

### Phase 2: Rule-Based Procedural Deck Generator
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

### Architectural Implications for Current Refactoring
- Keep deck building, querying, and validation logic strictly decoupled from `Form1.cs` UI controls so the deck generation engine can build and validate decks headless.
- Ensure the card catalog model allows fast LINQ queries based on both official Cardnum attributes and custom user tags (e.g., `cards.Where(...)`).
- Design deck save/export routines to operate on generic deck data structures rather than directly reading from WinForms ListBoxes.