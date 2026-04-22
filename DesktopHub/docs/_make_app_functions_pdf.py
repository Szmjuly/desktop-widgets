"""
Render docs/APP_FUNCTIONS.pdf -- a clean product catalog for the 5 apps.
One-shot script; keeps the PDF content in sync with APP_FUNCTIONS.txt.
Run with: python docs/_make_app_functions_pdf.py
"""
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.units import inch
from reportlab.lib.colors import HexColor
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, PageBreak,
    HRFlowable, ListFlowable, ListItem,
)

OUTPUT = "docs/APP_FUNCTIONS.pdf"

# --- Styles ---------------------------------------------------------------

title_style = ParagraphStyle(
    "Title",
    fontName="Helvetica-Bold",
    fontSize=22,
    leading=26,
    textColor=HexColor("#111111"),
    spaceAfter=4,
)
subtitle_style = ParagraphStyle(
    "Subtitle",
    fontName="Helvetica",
    fontSize=11,
    leading=14,
    textColor=HexColor("#555555"),
    spaceAfter=18,
)
app_name_style = ParagraphStyle(
    "AppName",
    fontName="Helvetica-Bold",
    fontSize=16,
    leading=20,
    textColor=HexColor("#111111"),
    spaceBefore=14,
    spaceAfter=4,
)
section_style = ParagraphStyle(
    "Section",
    fontName="Helvetica-Bold",
    fontSize=10,
    leading=13,
    textColor=HexColor("#666666"),
    spaceBefore=10,
    spaceAfter=4,
)
body_style = ParagraphStyle(
    "Body",
    fontName="Helvetica",
    fontSize=10.5,
    leading=15,
    textColor=HexColor("#222222"),
    alignment=TA_LEFT,
    spaceAfter=6,
)
bullet_style = ParagraphStyle(
    "Bullet",
    parent=body_style,
    leftIndent=12,
    bulletIndent=0,
    spaceAfter=2,
)
sub_bullet_style = ParagraphStyle(
    "SubBullet",
    parent=body_style,
    leftIndent=28,
    bulletIndent=16,
    fontSize=10,
    leading=14,
    spaceAfter=1,
)

# --- Content --------------------------------------------------------------

APPS = [
    {
        "name": "1. DesktopHub",
        "description": (
            "DesktopHub is a Windows desktop productivity launcher that lives in the system "
            "tray and opens on a global keyboard shortcut. Its primary job is project search: "
            "pressing the hotkey brings up a blurred overlay where the user types a project "
            "name or keyword, and the app surfaces matching projects from the configured network "
            "drives, ranked by recency and frequency. Around that core it hosts a set of floating "
            "widgets &mdash; quick tasks, pinned documents, a customizable launcher, a "
            "cheat-sheet library, a timer, project-tag editor, usage metrics, and a developer "
            "panel for authorized users &mdash; each of which the user can enable, disable, "
            "arrange, and toggle visibility for independently. The app follows the user across "
            "Windows virtual desktops, is fully themeable, and supports configurable global "
            "hotkeys for every widget."
        ),
        "functions": [
            "Opens a search overlay on a global hotkey (default Ctrl+Alt+Space)",
            "Searches across configured network drives (P:, Q:, L:, Archive, etc.) to find projects by name, tag, or path",
            "Tracks most-recently and most-frequently opened projects for one-click access",
            ("Hosts a set of pinned widgets the user can arrange and toggle individually:", [
                "Quick Tasks &mdash; local todo list with categories, priorities, and daily carry-over",
                "Document Quick Open &mdash; pinned documents that reopen with a single click",
                "Frequent Projects &mdash; shortcut tiles for the projects you launch most",
                "Quick Launch &mdash; customizable file/folder/URL shortcuts",
                "Smart Project Search &mdash; semantic / regex / extension-aware file search inside a project",
                "Cheat Sheets &mdash; browsable technical reference library (engineering codes, AV/IT wiring, etc.)",
                "Timer &mdash; countdown and stopwatch",
                "Project Info &mdash; view and edit project metadata tags (voltage, HVAC, location, etc.)",
                "Metrics Viewer &mdash; personal usage analytics with an optional admin multi-user view",
                "Developer Panel &mdash; internal tooling for authorized developers (database browser, user role management, release pushes)",
            ]),
            "Follows the user across Windows virtual desktops &mdash; overlay windows appear on whichever desktop the user is currently on",
            "Themeable (light, dark, coffee) with per-widget transparency",
            "Configurable global and per-group hotkeys",
        ],
    },
    {
        "name": "2. Renamer / Spec Header Updater",
        "description": (
            "Spec Header Updater (internal name: <i>Renamer</i>) is a one-shot Windows GUI for "
            "engineers who maintain MEP specification binders. It opens a folder, lists every "
            "Word specification inside, and lets the user update the date and project-info "
            "headers across many files at once instead of one at a time. It reads and writes "
            ".docx natively, and for organizations that still have legacy .doc files it can "
            "convert them up on the fly; it can also export the whole batch to PDF in a single "
            "step. A tiered subscription model controls batch-size limits and the PDF-export "
            "feature."
        ),
        "functions": [
            "Opens a folder of Word specs and shows every document's current header fields in one grid",
            "Updates the date and project-info header fields across many .docx files at once",
            "Handles legacy .doc files by converting them to .docx",
            "Exports a batch to .pdf in one pass (via Word automation)",
            "Tiered subscription model (Free / Premium / Business) that unlocks larger batch sizes and the PDF export",
        ],
    },
    {
        "name": "3. HAP Extractor",
        "description": (
            "HAP Extractor is a narrow-purpose Windows utility for HVAC designers. Carrier's HAP "
            "software produces PDF reports that are hard to work with in spreadsheet form, and "
            "the relevant load numbers are split across two separate summaries. The tool ingests "
            "those PDFs &mdash; either as two files or as the combined report &mdash; parses "
            "them structurally, and presents the merged result as a single filterable grid "
            "grouped by room, system, and air-handler unit. From there the user filters down to "
            "what they need and exports to Excel for their load schedules and equipment "
            "selections. It is strictly a viewer-and-exporter; it doesn't recalculate anything "
            "and doesn't touch the underlying HAP model."
        ),
        "functions": [
            "Opens HAP (Heat &amp; Air-load Program) output PDFs &mdash; either the Zone Sizing Summary and Space Design Load Summary as two files, or the combined report",
            "Parses each report structurally and merges them into a single filterable grid",
            "Lets the user filter by room, system, or air-handler unit",
            "Exports the combined grid to .xlsx for downstream design work",
            "Display and export only &mdash; it does not recalculate loads or change the underlying HAP model",
        ],
    },
    {
        "name": "4. Narrative Generator",
        "description": (
            "The Narrative Generator is an internal tool for MEP authoring teams that automates "
            "the production of revision-memorandum documents &mdash; the formal memos that "
            "accompany each drawing-set revision. It scans the project folder on the shared "
            "Q:\\ drive, identifies the current revision set, filters by discipline, pulls "
            "sheet numbers and titles out of the revised PDFs, and renders a Word document from "
            "the team's standard narrative template with everything pre-populated. If a previous "
            "revision memo exists, it is moved to a dated archive folder before the new one is "
            "written, so history is preserved. The final narrative opens directly in Word, "
            "ready for the author to review and ship."
        ),
        "functions": [
            "Scans the project folder on the Q:\\ share to pull project number, name, and metadata",
            "Finds the current revision set and filters drawing sheets by discipline (Electrical, Mechanical, Plumbing, Fire Protection)",
            "Extracts sheet names and numbers out of the revised drawings",
            "Renders a Word narrative from the team template, with all metadata and sheet lists filled in",
            "Archives the previous revision before overwriting so nothing is ever lost",
            "Writes the final .docx back into the project's Narratives\\ subfolder and opens it in Word",
        ],
    },
    {
        "name": "5. HEIC Converter",
        "description": (
            "HEIC Converter is a small Windows utility that exists because Apple devices save "
            "photos in the HEIC/HEIF format and most Windows workflows still want JPEG or PNG. "
            "It accepts files by drag-and-drop or by pointing it at a folder (optionally "
            "recursively), converts everything in one pass to JPEG or PNG, and gives the user "
            "control over output quality and whether to overwrite existing files. It has a GUI "
            "for interactive use and a CLI for scripting or batch automation."
        ),
        "functions": [
            "Drag-and-drop or folder-select of .heic / .heif images",
            "Batch conversion to .jpg or .png in one pass",
            "Optional recursive folder traversal for nested image trees",
            "Configurable JPEG quality (1&ndash;100) and an overwrite-existing-files toggle",
            "Also runs from the command line for scripting",
        ],
    },
]

# --- Build ---------------------------------------------------------------


def make_bullets(items):
    """
    Convert a flat list (or list with sub-bullet tuples) into a ListFlowable.
    Entries that are plain strings become top-level bullets; entries that are
    (header, [sublist]) tuples become a bullet with a nested indented list.
    """
    rendered = []
    for entry in items:
        if isinstance(entry, tuple):
            header, sub = entry
            rendered.append(ListItem(
                Paragraph(header, body_style),
                leftIndent=12,
            ))
            sub_items = [
                ListItem(Paragraph(s, sub_bullet_style), leftIndent=28, bulletColor=HexColor("#888888"))
                for s in sub
            ]
            rendered.append(ListFlowable(
                sub_items,
                bulletType="bullet",
                start="circle",
                leftIndent=28,
                bulletFontSize=7,
            ))
        else:
            rendered.append(ListItem(
                Paragraph(entry, body_style),
                leftIndent=12,
            ))
    return ListFlowable(
        rendered,
        bulletType="bullet",
        start="square",
        leftIndent=12,
        bulletFontSize=7,
    )


def build():
    doc = SimpleDocTemplate(
        OUTPUT,
        pagesize=letter,
        leftMargin=0.75 * inch,
        rightMargin=0.75 * inch,
        topMargin=0.75 * inch,
        bottomMargin=0.75 * inch,
        title="Application Functions",
        author="Solomon Markowitz",
    )

    story = []
    story.append(Paragraph("Application Functions", title_style))
    story.append(Paragraph(
        "Plain-English description of what each application does for the end user. "
        "<font color='#888888'>Owner: Solomon Markowitz &nbsp;&middot;&nbsp; Last updated: 2026-04-22</font>",
        subtitle_style))
    story.append(HRFlowable(width="100%", thickness=1, color=HexColor("#DDDDDD"), spaceBefore=2, spaceAfter=14))

    for i, app in enumerate(APPS):
        story.append(Paragraph(app["name"], app_name_style))
        story.append(Paragraph("DESCRIPTION", section_style))
        story.append(Paragraph(app["description"], body_style))
        story.append(Paragraph("FUNCTION", section_style))
        story.append(make_bullets(app["functions"]))
        if i != len(APPS) - 1:
            story.append(HRFlowable(
                width="100%", thickness=0.5, color=HexColor("#EEEEEE"),
                spaceBefore=16, spaceAfter=6,
            ))

    doc.build(story)
    print(f"Wrote {OUTPUT}")


if __name__ == "__main__":
    build()
