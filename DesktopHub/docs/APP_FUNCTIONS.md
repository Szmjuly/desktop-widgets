# Application Functions — What Each Program Does

**Purpose:** Plain-English description of what each application does for the end user. No infrastructure, no security posture — just features.
**Owner:** Solomon Markowitz (szmjuly@gmail.com).
**Last updated:** 2026-04-22.

---

## 1. DesktopHub

**In one sentence:** A keyboard-driven desktop launcher and widget host that makes it faster to find projects, open documents, and run the team's everyday tools.

**What it does:**
- Opens a search overlay on a global hotkey (default `Ctrl+Alt+Space`)
- Searches across configured network drives (P:, Q:, L:, Archive, etc.) to find projects by name, tag, or path
- Tracks most-recently and most-frequently opened projects for one-click access
- Hosts a set of pinned widgets the user can arrange and toggle individually:
  - **Quick Tasks** — local todo list with categories, priorities, and daily carry-over
  - **Document Quick Open** — pinned documents that reopen with a single click
  - **Frequent Projects** — shortcut tiles for the projects you launch most
  - **Quick Launch** — customizable file/folder/URL shortcuts
  - **Smart Project Search** — semantic / regex / extension-aware file search inside a project
  - **Cheat Sheets** — browsable technical reference library (engineering codes, AV/IT wiring, etc.)
  - **Timer** — countdown and stopwatch
  - **Project Info** — view and edit project metadata tags (voltage, HVAC, location, etc.)
  - **Metrics Viewer** — personal usage analytics with an optional admin multi-user view
  - **Developer Panel** — internal tooling for authorized developers (database browser, user role management, release pushes)
- Follows the user across Windows virtual desktops — overlay windows appear on whichever desktop the user is currently on
- Themeable (light, dark, coffee) with per-widget transparency
- Configurable global and per-group hotkeys

**Description:**
DesktopHub is a Windows desktop productivity launcher that lives in the system tray and opens on a global keyboard shortcut. Its primary job is project search: pressing the hotkey brings up a blurred overlay where the user types a project name or keyword, and the app surfaces matching projects from the configured network drives, ranked by recency and frequency. Around that core it hosts a set of floating widgets — quick tasks, pinned documents, a customizable launcher, a cheat-sheet library, a timer, project-tag editor, usage metrics, and a developer panel for authorized users — each of which the user can enable, disable, arrange, and toggle visibility for independently. The app follows the user across Windows virtual desktops, is fully themeable, and supports configurable global hotkeys for every widget.

---

## 2. Renamer / Spec Header Updater

**In one sentence:** Batch-updates the dates and project-information fields inside the headers of Microsoft Word specification documents.

**What it does:**
- Opens a folder of Word specs and shows every document's current header fields in one grid
- Updates the date and project-info header fields across many `.docx` files at once
- Handles legacy `.doc` files by converting them to `.docx`
- Exports a batch to `.pdf` in one pass (via Word automation)
- Tiered subscription model (Free / Premium / Business) that unlocks larger batch sizes and the PDF export

**Description:**
Spec Header Updater (internal name: *Renamer*) is a one-shot Windows GUI for engineers who maintain MEP specification binders. It opens a folder, lists every Word specification inside, and lets the user update the date and project-info headers across many files at once instead of one at a time. It reads and writes `.docx` natively, and for organizations that still have legacy `.doc` files it can convert them up on the fly; it can also export the whole batch to PDF in a single step. A tiered subscription model controls batch-size limits and the PDF-export feature.

---

## 3. HAP Extractor

**In one sentence:** Extracts and combines HVAC-load information out of Carrier HAP thermal-analysis PDF reports into a filterable spreadsheet.

**What it does:**
- Opens HAP (Heat & Air-load Program) output PDFs — either the Zone Sizing Summary and Space Design Load Summary as two files, or the combined report
- Parses each report structurally and merges them into a single filterable grid
- Lets the user filter by room, system, or air-handler unit
- Exports the combined grid to `.xlsx` for downstream design work
- Display and export only — it does not recalculate loads or change the underlying HAP model

**Description:**
HAP Extractor is a narrow-purpose Windows utility for HVAC designers. Carrier's HAP software produces PDF reports that are hard to work with in spreadsheet form, and the relevant load numbers are split across two separate summaries. The tool ingests those PDFs — either as two files or as the combined report — parses them structurally, and presents the merged result as a single filterable grid grouped by room, system, and air-handler unit. From there the user filters down to what they need and exports to Excel for their load schedules and equipment selections. It is strictly a viewer-and-exporter; it doesn't recalculate anything and doesn't touch the underlying HAP model.

---

## 4. Narrative Generator

**In one sentence:** Auto-generates Word revision-memorandum narratives for MEP projects by pulling metadata straight off the project's network-drive folder.

**What it does:**
- Scans the project folder on the `Q:\` share to pull project number, name, and metadata
- Finds the current revision set and filters drawing sheets by discipline (Electrical, Mechanical, Plumbing, Fire Protection)
- Extracts sheet names and numbers out of the revised drawings
- Renders a Word narrative from the team template, with all metadata and sheet lists filled in
- Archives the previous revision before overwriting so nothing is ever lost
- Writes the final `.docx` back into the project's `Narratives\` subfolder and opens it in Word

**Description:**
The Narrative Generator is an internal tool for MEP authoring teams that automates the production of revision-memorandum documents — the formal memos that accompany each drawing-set revision. It scans the project folder on the shared `Q:\` drive, identifies the current revision set, filters by discipline, pulls sheet numbers and titles out of the revised PDFs, and renders a Word document from the team's standard narrative template with everything pre-populated. If a previous revision memo exists, it is moved to a dated archive folder before the new one is written, so history is preserved. The final narrative opens directly in Word, ready for the author to review and ship.

---

## 5. HEIC Converter

**In one sentence:** Converts Apple HEIC / HEIF image files to JPEG or PNG.

**What it does:**
- Drag-and-drop or folder-select of `.heic` / `.heif` images
- Batch conversion to `.jpg` or `.png` in one pass
- Optional recursive folder traversal for nested image trees
- Configurable JPEG quality (1–100) and an overwrite-existing-files toggle
- Also runs from the command line for scripting

**Description:**
HEIC Converter is a small Windows utility that exists because Apple devices save photos in the HEIC/HEIF format and most Windows workflows still want JPEG or PNG. It accepts files by drag-and-drop or by pointing it at a folder (optionally recursively), converts everything in one pass to JPEG or PNG, and gives the user control over output quality and whether to overwrite existing files. It has a GUI for interactive use and a CLI for scripting or batch automation.
