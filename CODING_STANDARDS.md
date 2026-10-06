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
- Legacy Play MECCG Files (.play, .playPBEM): Explicitly specify Windows-1252 ANSI:
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