# MECCG Deck Builder

An offline-capable Windows desktop application for building, organizing, filtering, and exporting decks for the **Middle-earth Collectible Card Game (MECCG)** by Iron Crown Enterprises (I.C.E.), including official expansions and Council of Elrond Dreamcard sets.

---

## Features

- **Complete Card Database:** Automatically synchronizes card and set definitions from the open Cardnum database, with offline local JSON fallback.
- **Five-Section Deck Partitions:** Manage cards across standard competitive partitions:
  - **Pool:** Starting company, starter items, and opening resources.
  - **Resources:** Resource events, allies, factions, and items.
  - **Hazards:** Creatures, hazard events, and corruption.
  - **Sideboard:** Sideboard reserves.
  - **Sites:** Sites and movement regions.
- **Dynamic Stats Summary:** Window title bar displays live card counts per section with dedicated character counts: `(Pool / Res [Characters] / Haz / Side / Site)`.
- **Card Artwork Preview & Caching:** On-demand card artwork download from Cardnum with local disk caching (`METW/`, `MEDM/`, etc.) and an in-memory cache. ICE errata cards load corrected artwork automatically.
- **Dual-Layer Filtering:**
  - **Cardnum Built-in:** Filter by Primary type, Alignment, Race, Skills, Stats, Unique status, Sites, and more.
  - **Custom User Taxonomy:** Create custom tags (e.g., `Archetype` -> `Ring Destruction`, `Role` -> `Scout Support`) and tag individual cards.
- **Multi-Platform Export Formats:**
  - **Tabletop Simulator (`.json`):** Generates ready-to-use deck files compatible with TTS MECCG mods.
  - **Play MECCG (`.play` & `.playPBEM`):** Native format for online play.
  - **Cardnum Deck (`.cnum`):** Standard `1 <Code>` listing format.
  - **Archive Breakdown (`.archive`):** Categorized breakdown (Items, Events, Characters by race, Sites by type).
  - **Plain Text (`.txt`):** Formatted deck listing.

---

## Keyboard & Mouse Shortcuts

| Action | Location | Behavior |
|---|---|---|
| Left Click | Master List | Select card and preview image |
| Double Click | Master List | Add card to the active deck tab |
| Double Click | Any Deck Tab | Duplicate the clicked card in that tab |
| Drag & Drop | Master List -> Tab | Add card to the target tab |
| Right Click | Master List | Context menu: Copy to tab, view Cardnum attributes, assign custom tags |
| Right Click | Deck Tab | Context menu: Copy, Move to another tab, Delete, view metadata |
| Ctrl + N | Global | Start a new deck |
| Ctrl + O | Global | Open saved deck (`.json`) |
| Ctrl + S | Global | Export deck dialog |

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

3. Run the executable in `bin/Release/net8.0-windows/MECCG Deck Builder.exe`.

---

## Offline Use & Image Pre-downloading
To pre-cache card artwork for tournament or offline use:
1. Open the application.
2. Under the **Set** menu, check the sets you wish to cache.
3. Select **Tools > Download Images**. Card artwork will download to local set folders.

---

## Documentation
Full compiled HTML Help documentation is available in-app via **Help > Contents** (or by opening `MECCG_Deck_Builder.chm`).

---

## Acknowledgments
- Card data and artwork courtesy of the Cardnum Project (https://cardnum.net).
- Middle-earth Collectible Card Game is originally (C) Iron Crown Enterprises and Tolkien Enterprises.