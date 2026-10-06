# AI Development Context: MECCG Deck Builder

## Project Summary
- Type: Windows Forms (WinForms) Desktop Application
- Language / Framework: C# (.NET 8.0, net8.0-windows)
- Domain: Deck Builder for Middle-earth: Collectible Card Game (MECCG) (Iron Crown Enterprises / ICE & Council of Elrond Dreamcards).
- Primary Data Sources:
  - Card Data: cards-dc.json (synced from GitHub rezwits/cardnum/fdata/cards-dc.json with local fallback).
  - Set Data: sets-dc.json (synced from GitHub rezwits/cardnum/fdata/sets-dc.json with local fallback).
  - Images: https://cardnum.net/img/cards/{set}/{imageName}
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

## Known Bugs & Pain Points for Initial Refactoring

1. Card Filter Bug (Cards.cs):
   CardMatchesFilters checks keyValuePairs[0][0] inside a loop instead of keyValuePairs[index][0], and lacks ContainsKey safety checks.
2. Expansion Filter Values Missing (Cards.cs):
   SetKeyValues is currently only invoked for METW and MEUL sets during startup. Expansion-specific attributes are not populated into the filter dropdowns.
3. Blocking Network Calls:
   Cards.cs and CardImageCache.cs use blocking .Result / .GetResult() on HttpClient. Network lag freezes the UI.
4. GDI+ Resource Leaks:
   PictureBoxCardImage.Image assignments do not dispose of previous Bitmaps. new Bitmap(path) locks image files on disk.
5. Memory Cache Disk Miss (CardImageCache.cs):
   Loading an image from disk returns a new Bitmap without caching it in MemoryCache, forcing repeated disk I/O on every card click.
6. Card ID Indexing Assumption:
   Lookup by Convert.ToInt32(cardId) assumes catalog array index == card ID. Should be migrated to a Dictionary<string, ...> or strongly-typed model.

---

## Target Modernization Goals
- Migrate raw string[] and SortedDictionary<string, string> structures to a strongly-typed Card model.
- Convert HttpClient downloads to async (async/await) with UI progress indication.
- Replace GDI+ file-locking image loads with non-locking memory stream clones and properly dispose old images.
- Fix filter indexing and populate filters from all active/released sets.
- Add unit tests for deck export formatting (TTS, Play MECCG, Cardnum, PBEM).

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