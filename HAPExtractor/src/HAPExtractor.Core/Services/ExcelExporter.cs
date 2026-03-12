using ClosedXML.Excel;
using HAPExtractor.Core.Models;

namespace HAPExtractor.Core.Services;

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
