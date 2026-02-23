# Smart Project Search Widget — Deep Analysis

> **Scope**: Full audit of architecture, layout, design, interaction, search semantics, performance, and improvement roadmap.
> **Files covered**: `SmartProjectSearchWidget.xaml/.cs`, `SmartProjectSearchService.cs`, `SmartProjectSearchOverlay.xaml/.cs`, `SearchOverlay.SmartProjectSearchAttached.cs`, `DocumentScanner.cs`, `DocumentItem.cs`, `ProjectFileInfo.cs`, settings integration.

---

## 1. Architecture Overview

### Component Map

```
┌─────────────────────────────────────────────────────────────┐
│  Settings (ISettingsService / SettingsService)               │
│  • SmartProjectSearchWidgetEnabled                           │
│  • SmartProjectSearchAttachToSearchOverlayMode               │
│  • SmartProjectSearchLatestMode ("list" | "single")          │
│  • SmartProjectSearchFileTypes (comma-separated)             │
│  • SmartProjectSearchWidgetPosition / Visible                │
│  • SmartProjectSearchWidgetEnabledBeforeAttachMode           │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│  SmartProjectSearchService (singleton, UI layer)             │
│  • Owns scan state (_projectInfo, _projectPath)              │
│  • Owns query state (_query, _results)                       │
│  • Delegates filesystem scan to IDocumentScanner             │
│  • Runs scoring/ranking in-process (no background thread)    │
│  • Emits StateChanged / ScanningChanged events               │
└──────────┬──────────────────────────────┬───────────────────┘
           │                              │
┌──────────▼──────────┐    ┌──────────────▼───────────────────┐
│  SmartProjectSearch  │    │  SmartProjectSearchOverlay        │
│  Widget (UserControl)│    │  (Window — standalone mode)       │
│  • SearchBox         │    │  • Hosts Widget via ContentPresenter
│  • ResultsList       │    │  • Draggable in Living Widgets    │
│  • StatusText        │    │  • Close button, transparency     │
│  • ProjectLabel      │    │  • KeyDown shortcut handling      │
└──────────────────────┘    └─────────────────────────────────┘
           │
┌──────────▼──────────────────────────────────────────────────┐
│  SearchOverlay.SmartProjectSearchAttached.cs (attached mode) │
│  • Embeds Widget inside SearchOverlay via ContentControl     │
│  • Animated expand/collapse panel below results              │
│  • Toggle button in SearchOverlay toolbar                    │
│  • Syncs overlay height dynamically                          │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Project selection** — User selects a project in `SearchOverlay.ResultsList` → `ResultsList_SelectionChanged` calls `_smartProjectSearchService.SetProjectAsync(path, name)`.
2. **Scanning** — `SetProjectAsync` → `ScanSelectedProjectAsync` → `IDocumentScanner.ScanProjectAsync` (async, on thread pool). Scans up to `maxDepth: 4`, `maxFiles: 5000`, filtered by configured file type extensions. Produces a `ProjectFileInfo` with `AllFiles`, `DisciplineFiles`, and `Revit` collections.
3. **Search pool** — `BuildSearchPool` merges `AllFiles + DisciplineFiles + RevitFiles`, deduplicates by path.
4. **Query parsing** — `ParseQuery` extracts: `latest` keyword, `::` clause separators, `|`/`or` alternatives, regex patterns (`re:` or `/.../`), file type filters.
5. **Scoring** — Each document is scored per-clause via `ScoreTerm` (field-weighted substring matching with alias expansion) + phrase bonus + freshness bonus for `latest`.
6. **Rendering** — `_results` list replaced atomically → `StateChanged` event → widget's `RenderState` sets `ItemsSource`, `StatusText`, `ProjectLabel`.

---

## 2. Layout & Design Audit

### Widget XAML (`SmartProjectSearchWidget.xaml`)

| Row | Element | Purpose |
|-----|---------|---------|
| 0 | `StackPanel` | Title "Smart Project Search" + `ProjectLabel` (accent blue, ellipsis) |
| 1 | `Grid` | `SearchBox` (TextBox) + hint text below |
| 2 | `ListBox` (ResultsList) | Scrollable results with `DataTemplate`: FileName, RelativePath, date/size/extension |
| 3 | `StatusText` | Status line at bottom |

### Overlay XAML (`SmartProjectSearchOverlay.xaml`)

- **Window**: 420×520, no resize, transparent, borderless, topmost.
- **Root border**: `#A8121212` semi-transparent dark, `CornerRadius="12"`.
- **Inner glow border**: `#33FFFFFF` 1px inset, `#14000000` fill.
- **Close button**: Top-right `✕`, hover red tint.
- **ContentPresenter**: Hosts the `SmartProjectSearchWidget` UserControl.
- **UpdateIndicator**: Green dot, bottom-right, collapsed by default.

### Attached Mode Panel (`SearchOverlay.xaml` Row 3)

- `Border` with `MaxHeight` animation (0 ↔ 255px), `ClipToBounds`.
- Contains title label + `ContentControl` host.
- Toggle button uses Segoe MDL2 search icon `&#xE721;`.

### Design Issues Found

| # | Issue | Severity |
|---|-------|----------|
| D1 | **No loading indicator** — scanning 5000 files shows only `StatusText = "Scanning..."`, no spinner/progress bar | Medium |
| D2 | **No empty state illustration** — "No project selected" is plain text, no visual cue | Low |
| D3 | **Result item hover state missing** — ListBox items have no hover/selection background styling | Medium |
| D4 | **Scrollbar hidden** — `ScrollViewer.VerticalScrollBarVisibility="Hidden"` gives no scroll affordance | Medium |
| D5 | **Fixed overlay size** — `ResizeMode="NoResize"`, `Width="420"`, `Height="520"` — can't resize to see more results | Medium |
| D6 | **Hint text is static** — "Try: fault current letter..." doesn't adapt to selected project context | Low |
| D7 | **No keyboard shortcut to focus search** — must click the TextBox | Low |
| D8 | **No result count badge** — user can't see total matches at a glance without reading status text | Low |
| D9 | **Attached panel title is redundant** — "Smart Project Search" label wastes vertical space in compact attached mode | Low |
| D10 | **No file type icon** — results show extension text but no visual icon differentiation | Low |

---

## 3. Interaction Audit

### Current Interactions

| Action | Trigger | Behavior |
|--------|---------|----------|
| Type query | `SearchBox.TextChanged` | 120ms debounce → `SetQueryAsync` → full re-score |
| Open file | Double-click or Enter on result | `Process.Start` with `UseShellExecute` |
| Copy path | Ctrl+C on selected result | Clipboard set, status updated |
| Close overlay | Close button or configurable shortcut | `Hide()` (not `Close()`) |
| Drag | Mouse on non-interactive area (Living Widgets) | Manual drag via `CaptureMouse` |
| Project priming | Select project in SearchOverlay results | `SetProjectAsync` triggers scan + refresh |

### Interaction Issues Found

| # | Issue | Severity |
|---|-------|----------|
| I1 | **No right-click context menu** — can't "Open containing folder", "Copy file name", "Pin" | High |
| I2 | **No keyboard navigation from search box to results** — pressing Down arrow doesn't move focus to ResultsList | High |
| I3 | **No Escape key to clear search** — must manually select-all and delete | Medium |
| I4 | **Double-click is the only open gesture** — single-click + Enter requires tabbing to list first | Medium |
| I5 | **No drag-and-drop** — can't drag a result to another app (e.g., email attachment) | Low |
| I6 | **Scan blocks UI thread indirectly** — `ScanSelectedProjectAsync` is async but `RefreshResultsAsync` scoring runs on UI thread for large pools | High |
| I7 | **No "Open in Explorer" action** — common need for project files | High |
| I8 | **No multi-select** — can't open or copy multiple files at once | Low |
| I9 | **Attached mode toggle doesn't auto-expand** — clicking the toggle button when results are collapsed forces expand, but doesn't focus the search box | Medium |

---

## 4. Search Semantics Audit

### Query Syntax (Current)

```
fault current letter              → AND search across all terms (with alias expansion)
fpl::pdf                          → clause "fpl" + file type filter "pdf"
fpl::pdf|word                     → clause "fpl" + file type filter "pdf OR word"
latest fault current letter       → "latest" keyword adds freshness boost + sort by date
re:pattern  or  /pattern/         → regex mode against full search text
"exact phrase"                    → quoted phrase treated as single term
C:\Path :: search terms           → path override: re-scan that directory instead
```

### Alias System

- **Smart token aliases**: `fault` → `{fault, short, short-circuit, sc}`, `current` → `{current, amp, amps, kaic, aic}`, etc.
- **File type aliases**: `word` → `{doc, docx}`, `excel` → `{xls, xlsx, csv}`, `jpeg` → `{jpg, jpeg}`.
- **Plural stripping**: terms ending in `s` (length > 3) also try singular form.
- **Stop words**: common English words filtered out.

### Scoring Model

| Signal | Weight | Notes |
|--------|--------|-------|
| FileName starts with term | 4.0 | Highest — exact prefix match |
| FileName contains term | 3.2 | Strong — substring in name |
| RelativePath contains term | 2.4 | Medium — path context |
| Subfolder contains term | 1.5 | Weak — folder name |
| Category contains term | 1.2 | Weakest field |
| Subsequence match (≥3 chars) | 0.6 | Fuzzy fallback |
| Full phrase match in search text | +4.0 | Bonus on top of term scores |
| Freshness (latest mode, 30-day window) | 0–5.0 | Linear decay over 30 days |

### Minimum hit threshold

- ≤2 terms: all must match
- 3+ terms: `count - 1` must match (allows 1 miss)
- OR: phrase match overrides minimum

### Search Issues Found

| # | Issue | Severity |
|---|-------|----------|
| S1 | **No typo tolerance / fuzzy matching** — "fautl" returns nothing; subsequence is too weak for real typos | High |
| S2 | **Alias system is hardcoded** — adding new domain terms requires code changes | Medium |
| S3 | **No stemming** — "drawings" won't match "drawing" (only plural-s stripping exists) | Medium |
| S4 | **No TF-IDF or rarity weighting** — common terms like "plan" score the same as rare terms like "kaic" | Medium |
| S5 | **Scoring doesn't consider term proximity** — "fault current" in adjacent words should score higher than scattered matches | Medium |
| S6 | **No search history / recent queries** — user re-types the same queries repeatedly | Medium |
| S7 | **No "did you mean?" suggestions** — no feedback when zero results | Low |
| S8 | **Regex mode has no syntax validation feedback** — invalid regex silently returns empty | Low |
| S9 | **`latest` freshness window is fixed at 30 days** — not configurable | Low |
| S10 | **No negative/exclusion terms** — can't do `-backup` or `NOT backup` | Medium |
| S11 | **No wildcard support** — `fault*.pdf` doesn't work outside regex mode | Low |
| S12 | **Path override re-scans every query change** — typing `C:\Folder :: f` then `C:\Folder :: fa` triggers two full scans | High |

---

## 5. Performance Audit

### Current Bottlenecks

| # | Bottleneck | Impact | Details |
|---|-----------|--------|---------|
| P1 | **Full re-score on every keystroke** (after 120ms debounce) | High | `RefreshResultsAsync` iterates entire pool (up to 5000 docs), builds search text strings, runs regex/substring matching. All on UI thread after `await`. |
| P2 | **`BuildSearchText` allocates a new string per document per query** | Medium | `$"{doc.FileName} {doc.RelativePath} {doc.Subfolder} {doc.Category}"` — 5000 allocations per keystroke. |
| P3 | **`ToLowerInvariant()` called per-field per-term per-document** in `ScoreTerm` | Medium | 4 `ToLowerInvariant()` calls × terms × aliases × 5000 docs = massive allocation pressure. |
| P4 | **`ExpandTerm` creates new arrays on every call** | Low | `new[] { term, term[..^1] }` allocations in hot loop. |
| P5 | **`BuildSearchPool` deduplicates via `GroupBy` on every refresh** | Medium | LINQ `GroupBy` + `Select` + `ToList` on potentially 10K+ items. |
| P6 | **No scan result caching** — switching back to a previously scanned project re-scans from disk | High | `ScanSelectedProjectAsync` always calls `_scanner.ScanProjectAsync` for new projects. |
| P7 | **Path override re-scans directory on every query change** | High | `RefreshResultsAsync` calls `ScanProjectAsync` again if path override detected. |
| P8 | **No incremental/prefix search** — can't short-circuit when user adds characters | Medium | Adding a character to query could filter the previous result set instead of re-scoring everything. |
| P9 | **`Regex.Matches` in `ExtractSearchTerms` compiles regex on every call** | Low | Should use pre-compiled or `[GeneratedRegex]`. |
| P10 | **`Regex.IsMatch` for `latest` detection runs on every query** | Low | Simple `string.Contains` with word boundary check would suffice. |

### Recommended Performance Fixes (Priority Order)

1. **Pre-compute lowercase fields** — cache `fileNameLower`, `relativePathLower`, etc. on `DocumentItem` or in a search index wrapper at scan time.
2. **Move scoring to background thread** — `Task.Run` the scoring loop, check `_refreshVersion` before dispatching results.
3. **Cache search pool** — `BuildSearchPool` result should be cached and invalidated only on project change.
4. **Incremental filtering** — if new query is a prefix extension of previous query, filter previous results instead of full re-score.
5. **Cache path override scans** — store last scanned path + result, skip re-scan if path unchanged.
6. **Pre-compile regexes** — use `[GeneratedRegex]` or static compiled instances for `ExtractSearchTerms`, `latest` detection.
7. **Object pooling** — reuse `List<(DocumentItem, double)>` scored lists to reduce GC pressure.

---

## 6. UI/UX Recommendations

### High Priority

| # | Recommendation | Effort |
|---|---------------|--------|
| U1 | **Add right-click context menu**: Open, Open Containing Folder, Copy Path, Copy File Name, Pin | Medium |
| U2 | **Keyboard navigation**: Down arrow from SearchBox → focus ResultsList; Escape → clear search or close panel | Small |
| U3 | **Loading spinner** during scan — replace or augment StatusText with a small animated indicator | Small |
| U4 | **Result hover/selection styling** — add background highlight on hover and distinct selection color | Small |
| U5 | **Show scrollbar** — change to `Auto` visibility so users know there are more results | Trivial |
| U6 | **File type icons** — prepend emoji/icon based on extension (📄 PDF, 📐 DWG, 📊 Excel, etc.) | Small |

### Medium Priority

| # | Recommendation | Effort |
|---|---------------|--------|
| U7 | **Search result count badge** — show "(23)" next to title or in toggle button | Trivial |
| U8 | **Resizable overlay** — allow vertical resize to show more/fewer results | Medium |
| U9 | **Auto-focus search box** when attached panel expands or overlay opens | Trivial |
| U10 | **Highlight matching terms** in result file names (bold or color the matched substring) | Medium |
| U11 | **Empty state visual** — show a folder icon + "Select a project from the search results above" | Small |
| U12 | **Recent searches dropdown** — show last 5-10 queries as clickable pills below search box | Medium |
| U13 | **Contextual hint text** — adapt hint to show project name: "Search within 12345 Project Name..." | Trivial |

### Low Priority

| # | Recommendation | Effort |
|---|---------------|--------|
| U14 | **Drag-and-drop** results to other apps | Large |
| U15 | **Multi-select** with Ctrl+Click for batch operations | Medium |
| U16 | **Compact mode toggle** — reduce result item height for power users | Small |
| U17 | **Dark/light theme support** — currently hardcoded dark colors | Large |
| U18 | **Animated result transitions** — fade in new results for polish | Small |

---

## 7. AI / Neural Network Improvement Roadmap

### Phase 1: Local Lightweight ML (No External Dependencies)

| # | Feature | Approach | Effort |
|---|---------|----------|--------|
| A1 | **Typo-tolerant fuzzy matching** | Implement Levenshtein distance (edit distance ≤ 2) as a fallback when exact/substring match fails. Score: `0.3 * (1 - editDistance/termLength)`. No ML needed. | Small |
| A2 | **Learned term weights (TF-IDF)** | At scan time, compute IDF (inverse document frequency) for each token across the file pool. Rare terms get higher weight. Store as `Dictionary<string, double>`. | Medium |
| A3 | **User behavior learning** | Track which results users actually open per query. Store `(query, openedFilePath, timestamp)` in SQLite. Use click-through rate to boost frequently-opened files for similar queries. | Medium |
| A4 | **Personalized alias expansion** | When user searches "sc" and opens a "short circuit" file, auto-learn that alias. Store in settings as user-defined aliases alongside hardcoded ones. | Medium |

### Phase 2: Embedding-Based Semantic Search

| # | Feature | Approach | Effort |
|---|---------|----------|--------|
| A5 | **Local embedding model** | Ship a small ONNX model (e.g., MiniLM-L6, ~23MB) via `Microsoft.ML.OnnxRuntime`. At scan time, compute 384-dim embeddings for each file's metadata (name + path + category). At query time, embed the query and rank by cosine similarity. | Large |
| A6 | **Hybrid scoring** | Combine keyword score (current) with semantic similarity: `finalScore = 0.6 * keywordScore + 0.4 * cosineSimilarity`. Tunable weights. | Medium |
| A7 | **Semantic file clustering** | Group files by embedding similarity. Show "Related files" section when user selects a result. | Large |
| A8 | **Query intent classification** | Classify query intent: `search`, `latest`, `browse`, `regex`. Use a tiny decision tree or rule-based classifier to auto-select mode instead of requiring `latest` keyword. | Medium |

### Phase 3: Advanced AI Features

| # | Feature | Approach | Effort |
|---|---------|----------|--------|
| A9 | **Natural language queries** | "Show me the most recent fault current letter for FPL" → parse intent + entities. Could use a local small LLM (Phi-3-mini via ONNX) or rule-based NLU. | Very Large |
| A10 | **Content-aware search** | Index PDF/DOCX text content (first 500 chars) at scan time using lightweight extractors. Enable searching inside file contents, not just names/paths. | Large |
| A11 | **Smart suggestions / autocomplete** | As user types, suggest completions based on: file name tokens in pool, recent queries, learned aliases. Show as dropdown below search box. | Medium |
| A12 | **Anomaly detection** | Flag files that are unusually old, large, or misplaced (e.g., a DWG in a "Letters" folder). Surface as warnings. | Medium |

### Implementation Priority for AI

```
Immediate (no ML):  A1 (fuzzy matching), A2 (TF-IDF)
Short-term:         A3 (behavior learning), A4 (learned aliases), A11 (autocomplete)
Medium-term:        A5 (embeddings), A6 (hybrid scoring)
Long-term:          A9 (NLU), A10 (content search)
```

---

## 8. Code Quality & Cleanup

### Issues

| # | Issue | Location | Fix |
|---|-------|----------|-----|
| C1 | **`SmartProjectSearchResult` is a mutable POCO duplicate of `DocumentItem`** | `SmartProjectSearchService.cs:662-672` | Eliminate — bind directly to `DocumentItem` or create a lightweight read-only wrapper |
| C2 | **`BuildSearchText` concatenates 4 fields into a throwaway string** | `SmartProjectSearchService.cs:370-371` | Use `ReadOnlySpan<char>` checks or pre-cache at index time |
| C3 | **`ScoreTerm` calls `ToLowerInvariant()` 4 times per doc per term** | `SmartProjectSearchService.cs:375-378` | Pre-compute lowercase at scan/index time |
| C4 | **Static alias dictionaries are not extensible** | `SmartProjectSearchService.cs:15-43` | Move to settings or a JSON config file |
| C5 | **`ParseQuery` uses `Regex.IsMatch` and `Regex.Replace` without caching** | `SmartProjectSearchService.cs:450-451` | Use `[GeneratedRegex]` or pre-compiled static `Regex` |
| C6 | **`RefreshResultsAsync` mixes I/O (path override scan) with scoring** | `SmartProjectSearchService.cs:152-206` | Separate scan and score into distinct methods |
| C7 | **No unit tests for search scoring** | — | Add tests for `ScoreTerm`, `ParseQuery`, `ApplySearch` edge cases |
| C8 | **`SmartProjectSearchWidget` directly references service** | `SmartProjectSearchWidget.xaml.cs` | Consider MVVM pattern with a ViewModel for testability |
| C9 | **Overlay `ResizeMode="NoResize"` prevents user customization** | `SmartProjectSearchOverlay.xaml:12` | Allow vertical resize with `MinHeight` constraint |
| C10 | **`IsSubsequence` is case-sensitive** | `SmartProjectSearchService.cs:413-427` | Should compare case-insensitively (both inputs are already lowered, but fragile) |

---

## 9. Implementation Checklist

### 🔴 Critical (Do First)

- [x] **P1/P3**: Pre-compute lowercase fields on DocumentItem at scan time; cache in a search index wrapper ✅
- [x] **P1**: Move scoring loop to `Task.Run` background thread with `_refreshVersion` guard ✅
- [x] **P5**: Cache `BuildSearchPool` result; invalidate only on project change ✅
- [x] **I1**: Add right-click context menu (Open, Open Folder, Copy Path, Copy Name) ✅
- [x] **I2**: Keyboard navigation — Down arrow from SearchBox to ResultsList, Escape to clear ✅
- [x] **S1/A1**: Implement Levenshtein fuzzy matching as scoring fallback (edit distance ≤ 2) ✅
- [x] **C1**: Eliminate `SmartProjectSearchResult` duplication — use `DocumentItem` directly or thin wrapper ✅

### 🟡 Important (Do Next)

- [x] **P6**: Cache scanned projects — `Dictionary<string, ProjectFileInfo>` with TTL or size limit ✅ (bounded LRU-like queue)
- [x] **P7**: Cache path override scan results — skip re-scan if path unchanged ✅
- [x] **S10**: Add exclusion terms — prefix `-` to negate a term ✅
- [x] **U1**: Right-click context menu on results ✅ (merged with I1)
- [x] **U2**: Full keyboard navigation flow ✅ (merged with I2)
- [x] **U3**: Loading spinner during scan ✅
- [x] **U4**: Result hover/selection styling ✅
- [x] **U5**: Show scrollbar (`Auto` visibility) ✅
- [x] **U6**: File type icons in results ✅
- [x] **U9**: Auto-focus search box on panel open ✅
- [ ] **U10**: Highlight matching terms in result names
- [x] **U13**: Contextual placeholder text with project name ✅
- [ ] **S2/C4**: Move alias dictionaries to settings/config file
- [ ] **S6/U12**: Search history — store recent queries, show as pills
- [ ] **A2**: Implement TF-IDF term weighting at scan time

### 🟢 Nice to Have (Polish)

- [ ] **U7**: Result count badge
- [ ] **U8**: Resizable overlay (vertical)
- [ ] **U11**: Empty state visual
- [ ] **U16**: Compact mode toggle
- [ ] **U18**: Animated result transitions
- [ ] **S3**: Basic stemming (beyond plural-s)
- [ ] **S5**: Term proximity scoring bonus
- [ ] **S9**: Configurable freshness window
- [ ] **S11**: Wildcard support outside regex
- [ ] **C5**: Pre-compile all regex patterns
- [ ] **C7**: Unit tests for scoring and parsing
- [ ] **C8**: MVVM refactor for testability
- [ ] **C9**: Allow overlay resize

### 🔵 Future / AI Track

- [ ] **A3**: User behavior learning (click-through tracking)
- [ ] **A4**: Learned alias expansion from user behavior
- [ ] **A5**: Local ONNX embedding model for semantic search
- [ ] **A6**: Hybrid keyword + semantic scoring
- [ ] **A8**: Query intent auto-classification
- [ ] **A11**: Autocomplete / smart suggestions dropdown
- [ ] **A10**: Content-aware search (PDF/DOCX text indexing)
- [ ] **A9**: Natural language query understanding

---

## 10. Quick Wins (< 30 min each)

1. **Show scrollbar**: Change `ScrollViewer.VerticalScrollBarVisibility` from `"Hidden"` to `"Auto"`.
2. **Auto-focus search**: Call `SearchBox.Focus()` on widget Loaded and when attached panel expands.
3. **Escape to clear**: Handle `KeyDown` on SearchBox — Escape clears text.
4. **Down arrow to results**: Handle `PreviewKeyDown` on SearchBox — Down moves focus to ResultsList.
5. **Contextual placeholder**: Set SearchBox watermark to `"Search in {projectName}..."`.
6. **Result count in status**: Always prefix status with count: `"23 results — ..."`.
7. **Hover styling**: Add `IsMouseOver` trigger on result `Border` for background highlight.

---

## 11. File Reference Map

| File | Role | Lines |
|------|------|-------|
| `Widgets/SmartProjectSearchWidget.xaml` | Widget UI layout | 95 |
| `Widgets/SmartProjectSearchWidget.xaml.cs` | Widget code-behind (events, rendering) | 98 |
| `Services/SmartProjectSearchService.cs` | Core search engine (scan, parse, score, rank) | 673 |
| `Overlays/SmartProjectSearchOverlay.xaml` | Standalone overlay window UI | 78 |
| `Overlays/SmartProjectSearchOverlay.xaml.cs` | Overlay code-behind (drag, close, transparency) | 151 |
| `Overlays/SearchOverlay.SmartProjectSearchAttached.cs` | Attached mode panel logic | 150 |
| `Core/Models/DocumentItem.cs` | File metadata model | 67 |
| `Core/Models/ProjectFileInfo.cs` | Project scan result model | 105 |
| `Infrastructure/Scanning/DocumentScanner.cs` | Filesystem scanner | 389 |
| `Core/Abstractions/ISettingsService.cs` | Settings contract (smart search section) | ~20 lines |
| `Infrastructure/Settings/SettingsService.cs` | Settings persistence | ~20 lines |
| `Settings/SettingsWindow.xaml` | Settings UI (smart search section) | ~40 lines |
| `Settings/SettingsWindow.xaml.cs` | Settings event handlers | ~60 lines |
