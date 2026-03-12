using System.Text.Json.Serialization;

namespace DesktopHub.Core.Models;

/// <summary>
/// Describes which code book edition a cheat sheet belongs to (e.g. NEC2020, NEC2023).
/// </summary>
public class CodeBook
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public int Year { get; set; }
    public Discipline Discipline { get; set; }
}

/// <summary>
/// Maps a municipality/state to the code book edition it currently adopts.
/// </summary>
public class JurisdictionCodeAdoption
{
    public string JurisdictionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AdoptedCodeBookId { get; set; } = string.Empty;
    public string AdoptionName { get; set; } = string.Empty;
    public int AdoptionYear { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Hints how the widget should render input/output for this sheet.
/// Auto = infer from column structure; others override the default.
/// </summary>
public enum CheatSheetLayout
{
    /// <summary>Infer layout from column count and input/output flags.</summary>
    Auto,
    /// <summary>Prominent input panel + card-style output; table is secondary reference.</summary>
    CompactLookup,
    /// <summary>Full scrollable table is primary; input filters are secondary.</summary>
    FullTable,
    /// <summary>Simple two-column list with optional search/filter.</summary>
    SimpleList
}

/// <summary>
/// Defines the type/shape of a cheat sheet.
/// </summary>
public enum CheatSheetType
{
    /// <summary>Standard lookup table with rows/columns.</summary>
    Table,
    /// <summary>Input → output calculator-style reference.</summary>
    Calculator,
    /// <summary>Free-form notes / key notes for a jurisdiction or topic.</summary>
    Note
}

/// <summary>
/// Defines how a column in a cheat-sheet table should behave.
/// </summary>
public class CheatSheetColumn
{
    public string Header { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public bool IsInputColumn { get; set; }
    public bool IsOutputColumn { get; set; }
}

/// <summary>
/// A single cheat sheet reference (table, calculator, or note).
/// </summary>
public class CheatSheet
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public Discipline Discipline { get; set; }
    public CheatSheetType SheetType { get; set; }
    public string? CodeBookId { get; set; }
    public string? JurisdictionId { get; set; }
    public List<string> Tags { get; set; } = new();

    /// <summary>Layout hint for how the widget should render this sheet's input/output UI.</summary>
    public CheatSheetLayout Layout { get; set; } = CheatSheetLayout.Auto;

    /// <summary>Column definitions for Table/Calculator types.</summary>
    public List<CheatSheetColumn> Columns { get; set; } = new();

    /// <summary>Row data — each row is a list of cell values matching column order.</summary>
    public List<List<string>> Rows { get; set; } = new();

    /// <summary>Free-form content for Note type sheets.</summary>
    public string? NoteContent { get; set; }

    /// <summary>Structured step-by-step guide data. When populated, enables Interactive and Visual rendering modes.</summary>
    public List<GuideStep> Steps { get; set; } = new();
}

/// <summary>
/// A single field (input or computed output) within a guide step.
/// </summary>
public class StepField
{
    /// <summary>Unique identifier used in formula references, e.g. "height".</summary>
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Display unit, e.g. "PSI", "ft", "gpm".</summary>
    public string? Unit { get; set; }
    /// <summary>Default value pre-filled in Interactive mode.</summary>
    public string? Default { get; set; }
    /// <summary>Placeholder / helper text shown in the input.</summary>
    public string? Hint { get; set; }
    /// <summary>True = computed output; false = user input.</summary>
    public bool IsOutput { get; set; }
    /// <summary>Math formula referencing other field IDs, e.g. "{height} / 2.31". Only for outputs.</summary>
    public string? Formula { get; set; }
    /// <summary>"positive-negative" to color green when ≥ 0, red when &lt; 0.</summary>
    public string? Highlight { get; set; }
}

/// <summary>
/// One step in a structured step-by-step guide (used by Interactive and Visual rendering modes).
/// </summary>
public class GuideStep
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<StepField> Fields { get; set; } = new();
    /// <summary>Practical tip or rule-of-thumb shown in a callout.</summary>
    public string? Tip { get; set; }
    /// <summary>Sheet Id to link to (e.g. "wsfu-to-gpm-demand").</summary>
    public string? Reference { get; set; }
    /// <summary>Emoji or icon key for Visual mode.</summary>
    public string? Icon { get; set; }
}

/// <summary>
/// Root container for all cheat-sheet data, serialized to/from JSON.
/// </summary>
public class CheatSheetDataStore
{
    public List<CodeBook> CodeBooks { get; set; } = new();
    public List<JurisdictionCodeAdoption> Jurisdictions { get; set; } = new();
    public List<CheatSheet> Sheets { get; set; } = new();
}
