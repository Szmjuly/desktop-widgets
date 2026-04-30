using ClosedXML.Excel;
using LoadExtractor.Core.Models;

namespace LoadExtractor.Core.Services;

public class TraneExcelExporter
{
    private const string ShDashboard = "Dashboard";
    private const string ShChecksums = "Room Checksums";
    private const string ShDesign = "Design Cooling Detail";
    private const string ShInputs = "Inputs";
    private const string ShSource = "Source";

    public void Export(string filePath, List<TraneRoomLoad> data, double deltaT = 20.0)
    {
        var exportDeltaT = deltaT > 0 ? deltaT : 20.0;

        using var workbook = new XLWorkbook();
        // Tab order: Dashboard first, then data sheets, Inputs, hidden Source
        var dashboard = workbook.Worksheets.Add(ShDashboard);
        var checksumsWs = workbook.Worksheets.Add(ShChecksums);
        var designWs = workbook.Worksheets.Add(ShDesign);
        var inputsWs = workbook.Worksheets.Add(ShInputs);
        var sourceWs = workbook.Worksheets.Add(ShSource);

        // ── Inputs (Delta T drives calc airflow on Room Checksums) ─────────
        inputsWs.Cell(1, 1).Value = "Input";
        inputsWs.Cell(1, 2).Value = "Value";
        inputsWs.Cell(2, 1).Value = "Delta T";
        inputsWs.Cell(2, 2).Value = exportDeltaT;
        inputsWs.Cell(2, 3).Value = "Edit this value to update calculated airflow on Room Checksums (column J).";

        var inputHeaderRange = inputsWs.Range(1, 1, 1, 2);
        inputHeaderRange.Style.Font.Bold = true;
        inputHeaderRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F6228");
        inputHeaderRange.Style.Font.FontColor = XLColor.White;
        inputsWs.Cell(2, 2).Style.NumberFormat.Format = "0.0";
        inputsWs.Columns().AdjustToContents();

        // ── Hidden Source (page alignment only) ───────────────────────────
        sourceWs.Cell(1, 1).Value = "Note";
        sourceWs.Cell(1, 2).Value =
            "Rows align across Dashboard, Room Checksums, and Design Cooling Detail (data row 2 = first room).";
        sourceWs.Range(1, 1, 1, 5).Merge();
        sourceWs.Cell(3, 1).Value = "Excel row";
        sourceWs.Cell(3, 2).Value = "Room Number";
        sourceWs.Cell(3, 3).Value = "Room Name";
        sourceWs.Cell(3, 4).Value = "Checksums PDF page";
        sourceWs.Cell(3, 5).Value = "Design Cooling PDF page";
        var metaHeader = sourceWs.Range(3, 1, 3, 5);
        metaHeader.Style.Font.Bold = true;
        metaHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F6228");
        metaHeader.Style.Font.FontColor = XLColor.White;

        for (var i = 0; i < data.Count; i++)
        {
            var metaRow = 4 + i;
            var excelDataRow = i + 2;
            var item = data[i];
            sourceWs.Cell(metaRow, 1).Value = excelDataRow;
            sourceWs.Cell(metaRow, 2).Value = item.RoomNumber;
            sourceWs.Cell(metaRow, 3).Value = item.RoomName;
            sourceWs.Cell(metaRow, 4).Value = item.SourcePage;
            if (item.DesignCooling?.DesignSourcePage is { } dcp)
                sourceWs.Cell(metaRow, 5).Value = dcp;
            else
                sourceWs.Cell(metaRow, 5).Clear();
        }

        sourceWs.Columns().AdjustToContents();
        sourceWs.Hide();

        // ── Room Checksums (extracted table only) ───────────────────────────
        var csHeaders = new[]
        {
            "Room Number",
            "Room Name",
            "Total Capacity (tons)",
            "Total Capacity (MBh)",
            "Sensible Capacity (MBh)",
            "Coil Airflow (cfm)",
            "Gross Floor Area (sq ft)",
            "sq ft/Ton",
            "People",
            "Calculated Airflow (cfm)"
        };
        for (var c = 0; c < csHeaders.Length; c++)
            checksumsWs.Cell(1, c + 1).Value = csHeaders[c];

        for (var i = 0; i < data.Count; i++)
        {
            var row = i + 2;
            var item = data[i];
            checksumsWs.Cell(row, 1).Value = item.RoomNumber;
            checksumsWs.Cell(row, 2).Value = item.RoomName;
            SetNullableNumber(checksumsWs.Cell(row, 3), item.TotalCapacityTons);
            SetNullableNumber(checksumsWs.Cell(row, 4), item.TotalCapacityMbh);
            SetNullableNumber(checksumsWs.Cell(row, 5), item.SensibleCapacityMbh);
            SetNullableNumber(checksumsWs.Cell(row, 6), item.CoilAirflowCfm);
            SetNullableNumber(checksumsWs.Cell(row, 7), item.GrossFloorAreaSqFt);
            SetNullableNumber(checksumsWs.Cell(row, 8), item.SqFtPerTon);
            SetNullableNumber(checksumsWs.Cell(row, 9), item.NumberOfPeople);
            checksumsWs.Cell(row, 10).FormulaA1 =
                $"IF(OR(E{row}=\"\",'{ShInputs}'!$B$2=\"\",'{ShInputs}'!$B$2=0),\"\",(E{row}*1000)/(1.08*'{ShInputs}'!$B$2))";
        }

        // ── Design Cooling Detail (verification / background only) ─────────
        var dcHeaders = new[]
        {
            "Room Number",
            "Room Name",
            "Zone",
            "System (AHU)",
            "Coil sensible (MBh)",
            "Coil total (MBh)",
            "Total cooling airflow (cfm)",
            "Eng. total cooling load",
            "Eng. area / load",
            "Report sensible (Btu/h)",
            "Report latent (Btu/h)",
            "Report total (Btu/h)",
            "Design PDF page"
        };
        for (var c = 0; c < dcHeaders.Length; c++)
            designWs.Cell(1, c + 1).Value = dcHeaders[c];

        for (var i = 0; i < data.Count; i++)
        {
            var row = i + 2;
            var item = data[i];
            var dc = item.DesignCooling;
            designWs.Cell(row, 1).Value = item.RoomNumber;
            designWs.Cell(row, 2).Value = item.RoomName;
            if (dc != null)
            {
                designWs.Cell(row, 3).Value = dc.ZoneText ?? string.Empty;
                designWs.Cell(row, 4).Value = dc.SystemName ?? string.Empty;
                SetNullableNumber(designWs.Cell(row, 5), dc.CoilSensibleMbh);
                SetNullableNumber(designWs.Cell(row, 6), dc.CoilTotalMbh);
                SetNullableNumber(designWs.Cell(row, 7), dc.TotalCoolingAirflowCfm);
                SetNullableNumber(designWs.Cell(row, 8), dc.EngTotalCoolingLoad);
                SetNullableNumber(designWs.Cell(row, 9), dc.EngAreaPerLoad);
                SetNullableNumber(designWs.Cell(row, 10), dc.ReportSensibleBtuH);
                SetNullableNumber(designWs.Cell(row, 11), dc.ReportLatentBtuH);
                SetNullableNumber(designWs.Cell(row, 12), dc.ReportTotalBtuH);
                if (dc.DesignSourcePage is { } pg)
                    designWs.Cell(row, 13).Value = pg;
            }
        }

        // ── Dashboard (references + comparison formulas) ────────────────────
        var dashHeaders = new[]
        {
            "Room Number",
            "Room Name",
            "Total Capacity (tons)",
            "Total Capacity (MBh)",
            "Coil Airflow (cfm)",
            "Gross Floor Area (sq ft)",
            "sq ft/Ton",
            "People",
            "Calculated Airflow (cfm)",
            "System (AHU)",
            "Match — total MBh vs DC coil total",
            "Match — CFM vs DC total cooling airflow"
        };
        for (var c = 0; c < dashHeaders.Length; c++)
            dashboard.Cell(1, c + 1).Value = dashHeaders[c];

        var cs = $"'{ShChecksums}'";
        var ds = $"'{ShDesign}'";

        for (var i = 0; i < data.Count; i++)
        {
            var row = i + 2;
            dashboard.Cell(row, 1).FormulaA1 = $"={cs}!A{row}";
            dashboard.Cell(row, 2).FormulaA1 = $"={cs}!B{row}";
            dashboard.Cell(row, 3).FormulaA1 = $"={cs}!C{row}";
            dashboard.Cell(row, 4).FormulaA1 = $"={cs}!D{row}";
            dashboard.Cell(row, 5).FormulaA1 = $"={cs}!F{row}";
            dashboard.Cell(row, 6).FormulaA1 = $"={cs}!G{row}";
            dashboard.Cell(row, 7).FormulaA1 = $"={cs}!H{row}";
            dashboard.Cell(row, 8).FormulaA1 = $"={cs}!I{row}";
            dashboard.Cell(row, 9).FormulaA1 = $"={cs}!J{row}";
            dashboard.Cell(row, 10).FormulaA1 = $"={ds}!D{row}";

            // MBh vs DC coil total (col F on design)
            dashboard.Cell(row, 11).FormulaA1 =
                $"=IF(OR(ISBLANK({cs}!D{row}),ISBLANK({ds}!F{row})),\"—\",IF(ABS({cs}!D{row}-{ds}!F{row})<=MAX(0.35,0.006*MAX(ABS({cs}!D{row}),ABS({ds}!F{row}))),\"Match\",\"Mismatch\"))";

            // CFM vs DC total cooling airflow (design G)
            dashboard.Cell(row, 12).FormulaA1 =
                $"=IF(OR(ISBLANK({cs}!F{row}),ISBLANK({ds}!G{row})),\"—\",IF(ABS({cs}!F{row}-{ds}!G{row})<=MAX(25,0.02*MAX(ABS({cs}!F{row}),ABS({ds}!G{row}))),\"Match\",\"Mismatch\"))";
        }

        var lastDataRow = Math.Max(data.Count + 1, 2);

        ApplySheetChrome(dashboard, lastDataRow, 12);
        ApplySheetChrome(checksumsWs, lastDataRow, 10);
        ApplySheetChrome(designWs, lastDataRow, 13);

        checksumsWs.Columns(3, 5).Style.NumberFormat.Format = "0.0";
        checksumsWs.Column(6).Style.NumberFormat.Format = "0";
        checksumsWs.Column(7).Style.NumberFormat.Format = "0";
        checksumsWs.Column(8).Style.NumberFormat.Format = "0.00";
        checksumsWs.Column(9).Style.NumberFormat.Format = "0.0";
        checksumsWs.Column(10).Style.NumberFormat.Format = "0";

        designWs.Columns(5, 9).Style.NumberFormat.Format = "0.00";
        designWs.Column(7).Style.NumberFormat.Format = "0";
        designWs.Columns(10, 12).Style.NumberFormat.Format = "0";

        dashboard.Columns(3, 4).Style.NumberFormat.Format = "0.0";
        dashboard.Column(5).Style.NumberFormat.Format = "0";
        dashboard.Column(6).Style.NumberFormat.Format = "0";
        dashboard.Column(7).Style.NumberFormat.Format = "0.00";
        dashboard.Column(8).Style.NumberFormat.Format = "0.0";
        dashboard.Column(9).Style.NumberFormat.Format = "0";
        dashboard.Columns(11, 12).Style.NumberFormat.Format = "@";

        dashboard.SheetView.FreezeRows(1);
        checksumsWs.SheetView.FreezeRows(1);
        designWs.SheetView.FreezeRows(1);

        dashboard.Range(1, 1, lastDataRow, 12).SetAutoFilter();
        checksumsWs.Range(1, 1, lastDataRow, 10).SetAutoFilter();
        designWs.Range(1, 1, lastDataRow, 13).SetAutoFilter();

        dashboard.Columns().AdjustToContents();
        checksumsWs.Columns().AdjustToContents();
        designWs.Columns().AdjustToContents();

        checksumsWs.Column(2).Width = Math.Max(checksumsWs.Column(2).Width, 44);
        designWs.Column(2).Width = Math.Max(designWs.Column(2).Width, 44);
        dashboard.Column(2).Width = Math.Max(dashboard.Column(2).Width, 44);
        dashboard.Column(10).Width = Math.Min(Math.Max(dashboard.Column(10).Width, 10), 22);
        for (var col = 11; col <= 12; col++)
        {
            var w = dashboard.Column(col).Width;
            dashboard.Column(col).Width = Math.Min(Math.Max(w, 18), 42);
        }

        workbook.SaveAs(filePath);
    }

    private static void ApplySheetChrome(IXLWorksheet ws, int lastRow, int lastCol)
    {
        var headerRange = ws.Range(1, 1, 1, lastCol);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F6228");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var used = ws.Range(1, 1, Math.Max(lastRow, 2), lastCol);
        used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        used.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void SetNullableNumber(IXLCell cell, double? value)
    {
        if (value.HasValue)
            cell.Value = value.Value;
        else
            cell.Clear(XLClearOptions.Contents);
    }
}
