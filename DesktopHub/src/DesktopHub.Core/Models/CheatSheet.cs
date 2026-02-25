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

    /// <summary>Column definitions for Table/Calculator types.</summary>
    public List<CheatSheetColumn> Columns { get; set; } = new();

    /// <summary>Row data — each row is a list of cell values matching column order.</summary>
    public List<List<string>> Rows { get; set; } = new();

    /// <summary>Free-form content for Note type sheets.</summary>
    public string? NoteContent { get; set; }
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
