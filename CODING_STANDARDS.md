# Coding Standards & Guidelines: MECCG Deck Builder

## 1. General Principles
- Modern .NET Standard: Target C# 12 / .NET 8 idioms (collection expressions [], pattern matching, nullability annotations).
- Separation of Concerns: Keep UI presentation (Form1.cs) strictly separated from domain logic, file I/O, network access, and deck generation algorithms. Forms should act as views, delegating logic to dedicated services or models.
- Fail-Safe & User-Friendly: Catch exceptions gracefully, report actionable messages to the user, and never let unhandled background exceptions crash the UI thread.

---

## 2. Naming Conventions

### Identifiers
- Classes, Records, Enums, Structs, Methods, Properties: Use PascalCase.
  - Examples: CardCatalog, GenerateDeck(), DeckTitle
- Local Variables, Method Parameters, Private Fields: Use camelCase.
  - Examples: cardIndex, selectedSet, filePath
- Private / Internal Class Fields: Prefix with an underscore _ or use standard camelCase.
  - Examples: _cards, _httpClient (or cards, s_httpClient for static instances).
- Constants: Use PascalCase.
  - Examples: CardnumCardsUrl, PoolFileSuffix (avoid ALL_CAPS).
- Event Handlers in WinForms: Use ControlName_EventName in PascalCase.
  - Examples: ContentsToolStripMenuItem_Click, ListBoxMaster_SelectedIndexChanged
  - Note: Ensure the control name starts with an uppercase letter to satisfy Roslyn rule IDE1006.

---

## 3. Architecture & Domain Modeling

### Strongly-Typed Models over Raw Arrays
- No More string[] for Entities: Replace List<string[]> and SortedDictionary<string, string> with strongly-typed records or classes (e.g., Card, DeckCard, DeckSection).
- Enums for Domain Constants: Use strongly-typed enums for fixed concepts (e.g., DeckSection.Pool, DeckSection.Resources, CardAlignment.Hero).
- Immutability Where Appropriate: Card catalog definitions should be read-only once loaded into memory.

### Headless & Testable Domain Logic
- Deck export routines, deck import routines, card filtering, and the future procedural deck generator must never take ListBox, ComboBox, or Form controls as parameters.
- Operate on pure domain structures (e.g., Deck, IEnumerable<Card>, FilterCriteria) so they can be tested via unit tests and run headless.

---

## 4. Asynchronous Programming & Threading

### No Blocking on Async Tasks
- Never use .Result, .Wait(), or .GetAwaiter().GetResult() on the UI thread.
- All network requests (HttpClient) and disk I/O should use async / await:
  - Avoid: string json = s_httpClient.GetStringAsync(url).Result;
  - Prefer: string json = await s_httpClient.GetStringAsync(url);
- Long-running batch operations (such as Download Images or Generate Deck) must be performed asynchronously or on a background task (Task.Run) with progress updates via IProgress<T> or Progress<int>.

---

## 5. Resource Management & GDI+ Safety

### Safe Image Loading (No File Locking)
- Never instantiate new Bitmap(filePath) directly for files that might be overwritten or deleted, as GDI+ keeps the file locked indefinitely.
- Instead, read the image bytes into a MemoryStream and create an independent Bitmap clone:
  - Read bytes via File.ReadAllBytes(filePath)
  - Copy into MemoryStream
  - Construct Bitmap from stream and return an independent instance.

### Disposal of GDI+ Objects
- When assigning a new image to PictureBox.Image, always dispose of the previous image if it is an unmanaged, uncached copy to prevent GDI object leaks.
- Always dispose Graphics, Pen, Brush, and Font objects using using statements.

---

## 6. Encodings & String Formats

- Internal Strings: Standard .NET UTF-16 strings.
- JSON Serialization: Use UTF-8 without BOM (System.Text.Json or Newtonsoft.Json).
- Legacy Play MECCG Files (.play): Explicitly specify Windows-1252 ANSI:
  - Encoding.GetEncoding(1252)
- HTML Help Files (.hhp, .hhc, .hhk): Must be saved as ANSI (Windows-1252) or UTF-8 without BOM/signature. Never save help project files with a UTF-8 BOM.

---

## 7. Error Handling & Defensive Coding

- No Silent Exception Swallowing: Avoid empty catch (Exception) { } blocks. At minimum, log the error or provide user feedback via a status bar or message box.
- Specific Exception Catching: Catch specific exceptions (e.g., HttpRequestException, JsonException, IOException) before general Exception.
- Collection Key Safety: Use TryGetValue() or ContainsKey() before accessing dictionaries to prevent KeyNotFoundException.

---

## 8. Formatting & Tooling
- Indentation: 4 spaces (no tabs in C# source files; tabs permitted only in generated export templates if required by format specification like Tabletop Simulator JSON).
- Line Endings: CRLF (\r\n) for Windows compatibility.
- Warnings as Errors: Aim for zero compiler warnings and zero Roslyn analyzer warnings on Release builds.

---

## 9. Help System & HTML Documentation Guidelines (CHM & Markdown)

### 9.1 CHM Viewer Constraints & Layout Architecture
- **Narrow Viewport Awareness:** Microsoft HTML Help (`.chm`) viewer panes default to 600–800px in width. Designs must flow vertically and remain fully readable without horizontal scrolling.
- **Avoid Wide Monolithic Tables:** Never use 3- or 4-column tables to hold long descriptions, multi-bullet lists, or code queries. Tables must be reserved for compact summary indexes.
- **The "Matrix + Reference Cards" Pattern:** For detailed topic references (e.g. attributes, categories, recipes):
  1. **Top:** A lightweight, 3-column **At-a-Glance Matrix** or **Recipe Directory** table (Name, Scope, 1-line Purpose) with deep-anchor links (`<a href="#id">`).
  2. **Body:** Individual full-width **Reference Cards** headed by `<h3 id="..." class="entry-heading">` containing metadata badges, descriptive text, notes, and bulleted examples.

---

### 9.2 Visual Hierarchy & CSS Class Discipline
- **Never Wrap Headings in `<code>`:** Do not wrap `<h3>` text in `<code>...</code>`. Doing so reduces the heading font size to 9.5pt and applies an inline grey background that clashes with headings and badges. Use `h3.entry-heading` instead.
- **Structural Metadata vs. Game Tags:** Maintain strict visual separation between metadata and actual card values:
  - **Card Scopes / Structural Metadata:** Use `<span class="scope-pill">` (clean blue Segoe UI pill) inside `<div class="scope-badge">`.
  - **Literal Game Tags / Card Attributes / Code:** Use `<code>...</code>` (Consolas monospace grey box).
  - *Rule of Thumb:* If it is a string a player types into a query or tag box, it belongs in `<code>`. If it is category scope or UI instruction, use a span/badge.

---

### 9.3 List Consistency: The "Tag-First" Dictionary Pattern
- **Strict Left-to-Right Directionality:** In all reference lists, the tag or query value must anchor the left side:
  - **Correct:** `<code>[Tag Value]</code> &rarr; Description / Example Cards (<em>Card Title</em>)`
  - **Incorrect:** `<em>Card Title</em> &rarr; <code>[Tag Value]</code>`
- **Predictable Scanning Rhythm:** Never invert directionality between categories. Readers scan down the page looking for the tag to use; having the tag consistently on the left creates an effortless dictionary format.

---

### 9.4 Typography, Tolkien Entities & Expansion Conventions
- **Card Titles:** Always italicize card names using `<em>Title</em>` (e.g. *The Lonely Mountain*, *D&aacute;in II*).
- **Expansion Set Annotations:** The baseline default is *The Wizards* (`METW`). When citing cards introduced in later expansions, explicitly suffix the set code in parentheses:
  - *Example:* *The Drowning-deeps* (MEBA), *Framsburg* (METD).
- **Tolkien Diacritics & Accents:** Preserve all canon Tolkien accents using HTML entities:
  - `Barad-d&ucirc;r`, `D&aacute;in II`, `Ud&ucirc;n`, `Nazg&ucirc;l`, `Th&eacute;oden`, `Akh&ocirc;rahil`.
- **Special HTML Entities:** Use standard named entities for symbols:
  - `&rarr;` for arrows ($\rightarrow$)
  - `&bull;` for bullet dividers (&bull;)
  - `&mdash;` for em-dashes (&mdash;)

---

### 9.5 Language, Terminology & Tone
- **Player-First over Engine-First:** Explain mechanics in clear, functional MECCG gameplay terms rather than internal code jargon:
  - *Avoid:* "Dynamic predicates specify legal creature keys."
  - *Prefer:* "Site predicates (e.g. `Site=Ruins & Lairs`) define which hazard creatures can attack there."
- **Avoid Banned Buzzwords:**
  - Do not use corporate or game-design jargon like *"synergistic"*, *"environmental anchors"*, or *"light/shadow weather axis"*.
  - Do not label user-extensible taxonomies as *"Closed vs. Open Vocabulary"* (which falsely implies the app locks users out of authoring new tags). Use intuitive headings like `Baseline Roles (Pre-packaged Dataset):` versus `Representative Examples:`.

---

### 9.6 Markdown (`.md`) Angle-Bracket Safety
- **No Raw Angle-Bracket Placeholders:** In GitHub Markdown files (`README.md`, `CODING_STANDARDS.md`), never write unescaped angle brackets around words (such as `<Code>` or `<Tag>`). 
- **The HTML Collision Bug:** GitHub's markdown parser evaluates angle brackets as raw HTML. Case-insensitive tags like `<Code>` are parsed as unclosed `<code>` elements, corrupting the layout of all subsequent text.
- **Escape Pattern:** Always wrap placeholders in backticks (e.g. `` `1 <Code>` `` or `` `<Tag>` ``) or escape them as `&lt;Code&gt;`.