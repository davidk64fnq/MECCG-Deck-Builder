# MECCG Deck Builder

An offline-capable Windows desktop application for building, organizing, filtering, and exporting decks for the Middle-earth Collectible Card Game (MECCG) by Iron Crown Enterprises (I.C.E.), including official expansions and Council of Elrond Dreamcard sets.

---

## Features

- Curated Offline Database: Launches instantly using a local, validated card and set database (cards-dc.json, sets-dc.json) with zero startup network dependency.
- Five-Section Deck Partitions: Manage cards across standard competitive partitions:
  - Pool: Starting company, starter items, and opening resources.
  - Resources: Resource events, allies, factions, and items.
  - Hazards: Creatures, hazard events, and corruption.
  - Sideboard: Sideboard reserves.
  - Sites: Sites and movement regions.
- Dynamic Stats Summary: Window title bar displays live card counts per section with dedicated character counts: (Pool / Res [Characters] / Haz / Side / Site).
- Card Artwork Preview & Caching: On-demand card artwork download from Cardnum with local disk caching (METW/, MEDM/, etc.) and an in-memory cache. ICE errata cards load corrected artwork automatically.
- Dual-Layer Filtering:
  - Official Card Attributes (Slots 1 & 2): Filter by Primary type, Alignment, Race, Skills, Stats, Unique status, Sites, and more.
  - Custom User Taxonomy (Slots 3 & 4): Filter by custom categories and tags (e.g., Strategy -> Ring Destruction, Role -> Scout Support). Author tags via the dedicated Manage Custom Tags dialog or right-click Assign Tag > [+ Author New Tag...] without disturbing active filter criteria.
- Multi-Platform Export Formats:
  - Tabletop Simulator (.json): Generates ready-to-use deck files compatible with TTS MECCG mods.
  - Play MECCG (.play & .playPBEM): Native format for online play.
  - Cardnum Deck (.cnum): Standard 1 <Code> listing format.
  - Archive Breakdown (.archive): Categorized breakdown (Items, Events, Characters by race, Sites by type).
  - Plain Text (.txt): Formatted deck listing.
- Procedural Deck Generator: Generate complete decks from JSON rule templates with attribute and category queries, deck copy limits, tiered variety-first duplication, and randomized thematic branches (RandomChoice). Features runtime variable capture (SaveAs), dynamic cross-card property placeholders (e.g. {Leader.Category:Faction Bonus}, {Leader.Home}), multi-card union sets, and automated shortfall fallbacks (Fallback). Re-run instant variations with F5 or load rules via Deck > Generate from Rules... (Ctrl+G).
- Automated Taxonomy Extraction: Scan card rules text for catalog entities, perform cross-card attribute lookups, or execute custom regular expressions in an expanded workspace with automatic session persistence via Taxonomy > Extract Tags with Rules....
- Two-Way CSV Spreadsheet Pipeline: Bulk audit and edit catalog taxonomy using Microsoft Excel or Google Sheets with authoritative edit-and-replace synchronization via Taxonomy > Export Taxonomy Draft (CSV)... and Taxonomy > Import Taxonomy (CSV)....
- Session Persistence: Remembers your active expansion set selection across application restarts and preserves your last-used taxonomy extraction mode, regular expressions, and tag categories for iterative tagging workflows.

---

## Keyboard & Mouse Shortcuts

| Action | Location | Behavior |
|---|---|---|
| Left Click | Master List | Select card and preview image |
| Double Click | Master List | Add card to the active deck tab |
| Double Click | Any Deck Tab | Duplicate the clicked card in that tab |
| Drag & Drop | Master List -> Tab | Add card to the target tab |
| Right Click | Master List | Context menu: Copy to tab, view Card Attributes, view Assigned Tags, assign custom tags |
| Right Click | Deck Tab | Context menu: Copy, Move to another tab, Delete, view Card Attributes / Tags |
| Right Click | Filter ComboBox | Context menu: Clear this filter slot or clear all filters |
| Escape | Filter ComboBox | Reset focused filter ComboBox to blank |
| Ctrl + N | Global | Start a new deck (Deck > New Deck) |
| Ctrl + O | Global | Open saved deck JSON (Deck > Open Deck...) |
| Ctrl + S | Global | Save current deck JSON (Deck > Save Deck) |
| Ctrl + E | Global | Export deck dialog (Deck > Export Deck...) |
| Ctrl + G | Global | Generate deck from rules (Deck > Generate from Rules...) |
| F5 | Global | Re-run last used deck generation rules file (Deck > Re-run Deck Rules) |
| Ctrl + R | Global | Clear all active filter slots (Taxonomy > Clear All Active Filters) |
| F1 | Global | Open Help documentation (Help > Help Contents) |
| Enter | Quick Assign Tag Box | Assign tag(s) to the active card (+ Assign) |

---

## Getting Started

### Prerequisites
- Windows 10 / 11
- .NET 8.0 Desktop Runtime (or Visual Studio 2022 v17.8+)

### Building from Source
1. Clone the repository:
   git clone https://github.com/davidk64fnq/MECCG-Deck-Builder.git
   cd MECCG-Deck-Builder

2. Restore packages and build:
   dotnet restore
   dotnet build -c Release

3. Run the executable in bin/Release/net8.0-windows/MECCG Deck Builder.exe.

---

## Offline Use & Image Pre-downloading
To pre-cache card artwork for tournament or offline use:
1. Open the application.
2. Under the Catalog menu, check the expansion sets you wish to cache (or choose Select All Expansions).
3. Select Catalog > Download Card Artwork.... Card artwork will download to local set folders asynchronously.

---

## Documentation
Full compiled HTML Help documentation is available in-app via Help > Help Contents (F1) or by opening MECCG_Deck_Builder.chm.

---

## Acknowledgments
- Card data and artwork courtesy of the Cardnum Project (https://cardnum.net).
- Middle-earth Collectible Card Game is originally (C) Iron Crown Enterprises and Tolkien Enterprises.