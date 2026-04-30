using ClosedXML.Excel;
using LoadExtractor.Core.Models;

namespace LoadExtractor.Core.Services;

public class ExcelExporter
{
    // Envelope rows: Window & Skylight -> Ceiling (9 rows, 3 cols each)
    private static readonly string[] EnvelopeRowNames =
    {
        "Window & Skylight Solar", "Wall Transmission", "Roof Transmission",
        "Window Transmission", "Skylight Transmission", "Door Loads",
        "Floor Transmission", "Partitions", "Ceiling"
    };

    // Internal gain rows: Overhead Lighting -> Electrical Equipment (3 rows, 2 cols each)
    private static readonly string[] InternalGainRowNames =
    {
        "Overhead Lighting", "Task Lighting", "Electric Equipment"
    };

    public void Export(string filePath, List<CombinedSpaceData> data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Component Loads");

        int col = 1;

        // Layout:
        //   Row 1: Section headers (yellow merged) — "TOTALS", "Window & Skylight -> Ceiling", etc.
        //   Row 2: Item names (merged per group) — "Window & Skylight Solar", "Wall Transmission", etc.
        //   Row 3: Sub-column headers — "Area", "Sensible", "Heating", etc. + fixed col headers
        //   Row 4+: Data

        // Fixed columns A-F (headers in row 3)
        ws.Cell(3, 1).Value = "ROOM NAME";
        ws.Cell(3, 2).Value = "System";
        ws.Cell(3, 3).Value = "SQFT";

        // TOTALS merged header over D-F
        int totalsStart = 4;
        ws.Range(1, totalsStart, 1, totalsStart + 2).Merge().Value = "TOTALS";
        ws.Cell(3, totalsStart).Value = "People";
        ws.Cell(3, totalsStart + 1).Value = "Sensible";
        ws.Cell(3, totalsStart + 2).Value = "Latent";

        col = totalsStart + 3; // 7

        // Envelope section: Window & Skylight -> Ceiling
        int envStart = col;
        foreach (var rowName in EnvelopeRowNames)
        {
            int groupStart = col;
            // Row 3: sub-column headers
            ws.Cell(3, col).Value = "Area";
            ws.Cell(3, col + 1).Value = "Sensible (Cooling)";
            ws.Cell(3, col + 2).Value = "Sensible (Heating)";

            // Row 2: item name spanning its 3 columns
            ws.Range(2, groupStart, 2, groupStart + 2).Merge().Value = rowName;

            col += 3;
        }
        ws.Range(1, envStart, 1, col - 1).Merge().Value = "Window & Skylight -> Ceiling";

        // Internal gains section: Overhead Lighting -> Electrical Equipment
        int intStart = col;
        foreach (var rowName in InternalGainRowNames)
        {
            int groupStart = col;
            ws.Cell(3, col).Value = "Details";
            ws.Cell(3, col + 1).Value = "Sensible (Cooling)";

            ws.Range(2, groupStart, 2, groupStart + 1).Merge().Value = rowName;

            col += 2;
        }
        ws.Range(1, intStart, 1, col - 1).Merge().Value = "Overhead Lighting -> Electrical Equipment";

        // People section (2 cols)
        int peopleStart = col;
        ws.Range(1, peopleStart, 1, peopleStart + 1).Merge().Value = "People";
        ws.Cell(3, col).Value = "Sensible";
        ws.Cell(3, col + 1).Value = "Latent";
        col += 2;

        // Infiltration -> Misc section (2 cols)
        int infStart = col;
        ws.Range(1, infStart, 1, infStart + 1).Merge().Value = "Infiltration -> Misc.";
        ws.Cell(2, infStart).Value = "Infiltration";
        ws.Cell(2, infStart + 1).Value = "Miscellaneous";
        ws.Cell(3, infStart).Value = "Sensible";
        ws.Cell(3, infStart + 1).Value = "Sensible";
        col += 2;

        // Safety Factor section (3 cols)
        int sfStart = col;
        ws.Range(1, sfStart, 1, sfStart + 2).Merge().Value = "Safety Factor";
        ws.Cell(3, col).Value = "Details";
        ws.Cell(3, col + 1).Value = "Sensible";
        ws.Cell(3, col + 2).Value = "Latent";
        col += 3;

        int totalCols = col - 1;

        // === Format headers ===
        var headerRange1 = ws.Range(1, 1, 1, totalCols);
        headerRange1.Style.Font.Bold = true;
        headerRange1.Style.Fill.BackgroundColor = XLColor.Yellow;
        headerRange1.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var headerRange2 = ws.Range(2, 1, 2, totalCols);
        headerRange2.Style.Font.Italic = true;
        headerRange2.Style.Font.FontSize = 9;
        headerRange2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var headerRange3 = ws.Range(3, 1, 3, totalCols);
        headerRange3.Style.Font.Bold = true;
        headerRange3.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // === Data rows (starting at row 4) ===
        int dataRow = 4;
        foreach (var item in data)
        {
            col = 1;
            var cl = item.ComponentLoads;

            // Fixed columns
            ws.Cell(dataRow, col++).Value = item.RoomName;
            ws.Cell(dataRow, col++).Value = item.SystemName;
            ws.Cell(dataRow, col++).Value = item.FloorAreaSqFt;

            // Totals
            ws.Cell(dataRow, col++).Value = item.TotalPeopleDetails;
            ws.Cell(dataRow, col++).Value = item.TotalCoolingSensible;
            ws.Cell(dataRow, col++).Value = item.TotalCoolingLatent;

            if (cl != null)
            {
                // Envelope rows (9 × 3)
                foreach (var envRow in cl.EnvelopeRows)
                {
                    WriteDetailsValue(ws, dataRow, col++, envRow.CoolingDetails);
                    ws.Cell(dataRow, col++).Value = envRow.CoolingSensible;
                    ws.Cell(dataRow, col++).Value = envRow.HeatingSensible;
                }

                // Internal gain rows (3 × 2)
                foreach (var igRow in cl.InternalGainRows)
                {
                    WriteDetailsValue(ws, dataRow, col++, igRow.CoolingDetails);
                    ws.Cell(dataRow, col++).Value = igRow.CoolingSensible;
                }

                // People (Sensible, Latent)
                ws.Cell(dataRow, col++).Value = cl.People.CoolingSensible;
                ws.Cell(dataRow, col++).Value = cl.People.CoolingLatent;

                // Infiltration + Misc (Sensible each)
                ws.Cell(dataRow, col++).Value = cl.Infiltration.CoolingSensible;
                ws.Cell(dataRow, col++).Value = cl.Miscellaneous.CoolingSensible;

                // Safety Factor
                WriteDetailsValue(ws, dataRow, col++, cl.SafetyFactor.CoolingDetails);
                ws.Cell(dataRow, col++).Value = cl.SafetyFactor.CoolingSensible;
                ws.Cell(dataRow, col++).Value = cl.SafetyFactor.CoolingLatent;
            }

            dataRow++;
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents();

        // Add borders
        var dataRange = ws.Range(1, 1, dataRow - 1, totalCols);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Add auto-filter only on columns A-F (Room Name, System, SQFT, People, Sensible, Latent)
        ws.Range(3, 1, dataRow - 1, 2).SetAutoFilter();

        workbook.SaveAs(filePath);
    }

    /// <summary>
    /// Export "Matt's Way" — a simplified CFM-focused spreadsheet grouped by AHU,
    /// with Excel formulas for Hap CFM and Calculated CFM, and blank user-input columns.
    /// New layout: Room Name, Room Sqft, Sqft/Ton, AHU#, then remaining CFM columns.
    /// Creates a DATA sheet with per-system ft²/Ton from Air System Sizing Summary.
    /// </summary>
    public void ExportMattsWay(string filePath, HapProject project,
        List<AirSystemSizingData>? airSystemSizing = null)
    {
        using var workbook = new XLWorkbook();

        // ── Matt's Way sheet (worksheet #1) ──
        var ws = workbook.Worksheets.Add("Matt's Way");

        // ── DATA sheet (worksheet #2 — reference table for ft²/Ton per system) ──
        var dataSheet = workbook.Worksheets.Add("DATA");
        dataSheet.Cell(1, 1).Value = "System Name";
        dataSheet.Cell(1, 2).Value = "ft²/Ton";
        dataSheet.Cell(1, 3).Value = "Floor Area";
        dataSheet.Cell(1, 4).Value = "Tons";
        dataSheet.Cell(1, 5).Value = "CFM/Ton";

        var dataHeaderRange = dataSheet.Range(1, 1, 1, 5);
        dataHeaderRange.Style.Font.Bold = true;
        dataHeaderRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F6228");
        dataHeaderRange.Style.Font.FontColor = XLColor.White;

        for (int i = 0; i < project.AirSystems.Count; i++)
        {
            int dRow = i + 2;
            var sys = project.AirSystems[i];
            dataSheet.Cell(dRow, 1).Value = sys.Name;            // System Name (e.g. ACCU-01)

            // Look up sizing data by system name (only if PDF3 was provided)
            var sizing = airSystemSizing?.FirstOrDefault(s =>
                s.SystemName.Equals(sys.Name, StringComparison.OrdinalIgnoreCase));

            if (sizing != null)
            {
                dataSheet.Cell(dRow, 2).Value = sizing.SqftPerTon;
                dataSheet.Cell(dRow, 3).Value = sizing.FloorArea;
                dataSheet.Cell(dRow, 4).Value = sizing.TotalCoilLoadTons;
                dataSheet.Cell(dRow, 5).Value = sizing.CfmPerTon;
            }
        }

        dataSheet.Column(2).Style.NumberFormat.Format = "0.0";
        dataSheet.Column(3).Style.NumberFormat.Format = "0.0";
        dataSheet.Column(4).Style.NumberFormat.Format = "0.0";
        dataSheet.Column(5).Style.NumberFormat.Format = "0.0";
        dataSheet.Columns().AdjustToContents();

        // Column mapping (updated layout per user spec):
        //   A=1 (TOTAL'S label)
        //   B=2 (Room Name)       — swapped with AHU#
        //   C=3 (Room Sqft)       — new
        //   D=4 (Sqft/Ton)        — new, totals only via VLOOKUP
        //   E=5 (AHU #)           — swapped, was col B
        //   F=6 (Unit Sensible)
        //   G=7 (Cooling Sensible MBH)
        //   H=8 (System CFM)
        //   I=9 (Hap CFM)
        //   J=10 (Calculated CFM)
        //   K=11 (USER INPUT)
        const int colA = 1, colB = 2, colC = 3, colD = 4, colE = 5;
        const int colF = 6, colG = 7, colH = 8, colI = 9, colJ = 10, colK = 11;

        // Row 1: Headers
        ws.Cell(1, colB).Value = "Room Name";
        ws.Cell(1, colC).Value = "Room Sqft";
        ws.Cell(1, colD).Value = "SQF1/Ton";
        ws.Cell(1, colE).Value = "AHU #";
        ws.Cell(1, colF).Value = "Unit Sensible";
        ws.Cell(1, colG).Value = "Cooling Sensible (MBH)";
        ws.Cell(1, colH).Value = "System CFM";
        ws.Cell(1, colI).Value = "Hap CFM";
        ws.Cell(1, colJ).Value = "Calculated CFM";
        ws.Cell(1, colK).Value = "USER INPUT";

        // Header formatting — green background, bold, white text
        var headerRange = ws.Range(1, colA, 1, colK);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F6228");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int currentRow = 3; // row 2 = blank separator

        for (int ahuIdx = 0; ahuIdx < project.AirSystems.Count; ahuIdx++)
        {
            var system = project.AirSystems[ahuIdx];
            int spaceCount = system.Spaces.Count;
            if (spaceCount == 0) continue;

            int firstDataRow = currentRow;
            int lastDataRow = currentRow + spaceCount - 1;
            int totalsRow = lastDataRow + 2; // 1 blank row between data and TOTAL'S

            // Write data rows
            for (int si = 0; si < spaceCount; si++)
            {
                var space = system.Spaces[si];
                int row = firstDataRow + si;

                ws.Cell(row, colB).Value = space.SpaceName;
                ws.Cell(row, colC).Value = space.FloorArea;   // Room Sqft from zone data
                // D (Sqft/Ton) — blank for data rows, only populated in TOTALS
                ws.Cell(row, colE).Value = system.Name;
                ws.Cell(row, colG).Value = space.CoolingSensible;

                // Hap CFM formula: =G{row}/1.08*20  (Delta T = 20)
                ws.Cell(row, colI).FormulaA1 = $"G{row}/1.08*20";

                // Calculated CFM formula: =(G{row}/$G${totalsRow})*$H${totalsRow}
                ws.Cell(row, colJ).FormulaA1 = $"(G{row}/$G${totalsRow})*$H${totalsRow}";
            }

            // TOTAL'S row
            ws.Cell(totalsRow, colA).Value = "TOTAL'S";
            ws.Cell(totalsRow, colA).Style.Font.Bold = true;

            // C: SUM of Room Sqft
            ws.Cell(totalsRow, colC).FormulaA1 = $"SUM(C{firstDataRow}:C{lastDataRow})";

            // D: Sqft/Ton — VLOOKUP from DATA sheet by system name
            // =VLOOKUP(E{totalsRow},DATA!A:B,2,FALSE)
            ws.Cell(totalsRow, colD).FormulaA1 = $"IFERROR(VLOOKUP(E{totalsRow},DATA!A:B,2,FALSE),\"\")";

            ws.Cell(totalsRow, colE).Value = system.Name;
            // F (Unit Sensible) — blank for user input
            // G: SUM of Cooling Sensible
            ws.Cell(totalsRow, colG).FormulaA1 = $"SUM(G{firstDataRow}:G{lastDataRow})";
            // H (System CFM) — blank for user input
            // I: SUM of Hap CFM
            ws.Cell(totalsRow, colI).FormulaA1 = $"SUM(I{firstDataRow}:I{lastDataRow})";
            // J: SUM of Calculated CFM
            ws.Cell(totalsRow, colJ).FormulaA1 = $"SUM(J{firstDataRow}:J{lastDataRow})";
            // K: SUM of USER INPUT
            ws.Cell(totalsRow, colK).FormulaA1 = $"SUM(K{firstDataRow}:K{lastDataRow})";

            // Yellow highlight on TOTAL'S row
            var totalsRange = ws.Range(totalsRow, colA, totalsRow, colK);
            totalsRange.Style.Fill.BackgroundColor = XLColor.Yellow;
            totalsRange.Style.Font.Bold = true;

            // Next AHU group starts after 2 blank rows
            currentRow = totalsRow + 3;
        }

        // Number formatting
        ws.Column(colC).Style.NumberFormat.Format = "0.0";   // Room Sqft — 1 decimal
        ws.Column(colD).Style.NumberFormat.Format = "0.0";   // Sqft/Ton — 1 decimal
        ws.Column(colG).Style.NumberFormat.Format = "0.0";   // Cooling Sensible — 1 decimal
        ws.Column(colI).Style.NumberFormat.Format = "0";     // Hap CFM — integer
        ws.Column(colJ).Style.NumberFormat.Format = "0.0";   // Calculated CFM — 1 decimal
        ws.Column(colK).Style.NumberFormat.Format = "0";     // USER INPUT — integer

        // Auto-fit columns
        ws.Columns().AdjustToContents();

        // Borders on data area (row 1 through last written row)
        int lastRow = currentRow - 3; // back up past the trailing blank rows
        if (lastRow >= 1)
        {
            var dataRange = ws.Range(1, colA, lastRow, colK);
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Auto-filter on header row across all columns
        ws.Range(1, colA, 1, colK).SetAutoFilter();

        workbook.SaveAs(filePath);
    }

    private void WriteDetailsValue(IXLWorksheet ws, int row, int col, string details)
    {
        // Details may be "75 ft²", "1770 W", "5% / 5%", or just a number
        var clean = details.Replace("ft²", "").Replace("ft2", "")
                           .Replace("W", "").Replace("w", "").Trim();
        if (double.TryParse(clean.Replace(",", ""), out double val))
        {
            ws.Cell(row, col).Value = val;
        }
        else
        {
            ws.Cell(row, col).Value = details;
        }
    }
}
