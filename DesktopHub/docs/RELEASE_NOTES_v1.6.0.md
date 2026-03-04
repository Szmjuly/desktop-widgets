# Version 1.6.0 - Intelligent Project Search and Sub-Project Scanning

## 🎉 New Features

### Multi-Token Project Search
- Search queries like **"Boca West"** now split into individual tokens with AND semantics — both words must match somewhere across project name, number, or display fields.
- Single-word queries retain original fuzzy scoring for backward compatibility.

### Project Family Expansion
- When multiple projects share the same base project number (e.g., `2024337.00`, `2024337.01`, `2024337.02-B2`), searching for any family member now automatically surfaces all siblings.
- **Related Project** (gold badge, ≥2 direct matches): Confirmed project family — siblings ranked right after direct matches.
- **Duplicate Number?** (red badge, 1 direct match): Possible folder naming error — helps catch mismatched project numbers.

### Token-Order Awareness
- Results where all tokens match but in reversed order (e.g., "West Boca" for query "Boca West") are penalized and flagged as **Similar Match** (orange badge).
- Direct phrase matches always rank above reversed/scrambled token matches.

### Match Type Badges in UI
- Three new visual indicators on search results:
  - 🟡 **Related Project** — same project family, confirmed by multiple siblings
  - 🟠 **Similar Match** — all tokens found but in different order
  - 🔴 **Duplicate Number?** — same base number with only one direct match

### Sub-Project Scanning
- Projects with nested sub-project folders (e.g., `_2170 Guest House`, `_2200 Main House` inside a parent project) are now fully detected.
- Discipline folders (Electrical, Mechanical, Plumbing) and Revit File folders inside sub-projects are scanned and merged into the parent project.
- **Doc Quick Open**, **Smart Project Search**, and the **Search Overlay** all benefit from this deeper scanning.
- New `SubProjectInfo` model captures sub-project metadata for future UI grouping.

## 🐛 Fixes

### Explorer.exe Comma Bug
- Fixed critical bug where project folders containing commas in their name (e.g., `"2340 Gordon Drive, Naples FL"`) would open the user's OneDrive Documents folder instead of the correct project path.
- Root cause: `explorer.exe` treats commas as argument separators; paths are now quoted.

### Directory Scanning Skip Rule
- Removed overly aggressive `_` prefix directory skip in the document scanner. Sub-project folders like `_2170 Guest House` were previously invisible to all scans (discipline files, Revit files, and broad file search).

## 🎨 Improvements

### Search Result Limit
- Increased maximum search results from 10 to 50 to accommodate project family expansion (e.g., Boca West has 15+ sub-projects under base number 2024337).

### Search Scoring Refinements
- Added `project.Display` (full number + name) as an additional scoring field for broader matching.
- Multi-token scoring uses per-token average with AND semantics for balanced relevance.
- Family match scores (0.9×) rank above loose token matches (0.55×) but below direct matches.

### Internal Architecture
- Extracted `ScanDisciplinesAt` helper in DocumentScanner to avoid duplicating discipline scanning logic between root and sub-project levels.
- Added `ExtractBaseProjectNumber` for consistent project number family grouping across old/new number formats.

## 📌 Notes
- This release is a **minor version bump** from `1.5.0` to `1.6.0` because it introduces significant new search capabilities (multi-token matching, family expansion, match badges) and sub-project scanning while preserving backward compatibility.
- The `SubProjectInfo` metadata is available for future UI work such as showing sub-project grouping in Doc Quick Open's discipline dropdown.
