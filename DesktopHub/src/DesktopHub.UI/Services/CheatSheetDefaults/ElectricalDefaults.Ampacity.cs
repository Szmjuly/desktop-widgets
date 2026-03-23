using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Electrical cheat sheets: Conductor Ampacity, Conduit Fill, Box Fill.
/// </summary>
internal static partial class CheatSheetElectricalDefaults
{
    private static void AddAmpacitySheets(CheatSheetDataStore store)
    {
        // ── NEC Table 310.16 — Conductor Ampacity ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "conductor-ampacity",
            Title = "Conductor Ampacity",
            Subtitle = "NEC Table 310.16",
            Description = "Allowable ampacities of insulated conductors rated up to 2000V, 60\u00b0C through 90\u00b0C. Not more than 3 current-carrying conductors in raceway, cable, or earth.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "ampacity", "conductor", "wire", "310.16", "THHN", "THWN", "TW", "THW",
                "copper", "aluminum", "temperature", "60C", "75C", "90C", "current", "rating"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Material", IsInputColumn = true },
                new() { Header = "Size", Unit = "AWG/kcmil", IsInputColumn = true },
                new() { Header = "60\u00b0C", Unit = "A", IsOutputColumn = true },
                new() { Header = "75\u00b0C", Unit = "A", IsOutputColumn = true },
                new() { Header = "90\u00b0C", Unit = "A", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                // Copper conductors
                new() { "Cu", "14", "15", "20", "25" },
                new() { "Cu", "12", "20", "25", "30" },
                new() { "Cu", "10", "30", "35", "40" },
                new() { "Cu", "8", "40", "50", "55" },
                new() { "Cu", "6", "55", "65", "75" },
                new() { "Cu", "4", "70", "85", "95" },
                new() { "Cu", "3", "85", "100", "115" },
                new() { "Cu", "2", "95", "115", "130" },
                new() { "Cu", "1", "110", "130", "145" },
                new() { "Cu", "1/0", "125", "150", "170" },
                new() { "Cu", "2/0", "145", "175", "195" },
                new() { "Cu", "3/0", "165", "200", "225" },
                new() { "Cu", "4/0", "195", "230", "260" },
                new() { "Cu", "250", "215", "255", "290" },
                new() { "Cu", "300", "240", "285", "320" },
                new() { "Cu", "350", "260", "310", "350" },
                new() { "Cu", "400", "280", "335", "380" },
                new() { "Cu", "500", "320", "380", "430" },
                new() { "Cu", "600", "350", "420", "475" },
                new() { "Cu", "700", "385", "460", "520" },
                new() { "Cu", "750", "400", "475", "535" },
                new() { "Cu", "800", "410", "490", "555" },
                new() { "Cu", "900", "435", "520", "585" },
                new() { "Cu", "1000", "455", "545", "615" },
                new() { "Cu", "1250", "495", "590", "665" },
                new() { "Cu", "1500", "520", "625", "705" },
                new() { "Cu", "1750", "545", "650", "735" },
                new() { "Cu", "2000", "560", "665", "750" },

                // Aluminum conductors
                new() { "Al", "12", "15", "20", "25" },
                new() { "Al", "10", "25", "30", "35" },
                new() { "Al", "8", "30", "40", "45" },
                new() { "Al", "6", "40", "50", "60" },
                new() { "Al", "4", "55", "65", "75" },
                new() { "Al", "3", "65", "75", "85" },
                new() { "Al", "2", "75", "90", "100" },
                new() { "Al", "1", "85", "100", "115" },
                new() { "Al", "1/0", "100", "120", "135" },
                new() { "Al", "2/0", "115", "135", "150" },
                new() { "Al", "3/0", "130", "155", "175" },
                new() { "Al", "4/0", "150", "180", "205" },
                new() { "Al", "250", "170", "205", "230" },
                new() { "Al", "300", "190", "230", "255" },
                new() { "Al", "350", "210", "250", "280" },
                new() { "Al", "400", "225", "270", "305" },
                new() { "Al", "500", "260", "310", "350" },
                new() { "Al", "600", "285", "340", "385" },
                new() { "Al", "700", "310", "375", "420" },
                new() { "Al", "750", "320", "385", "435" },
                new() { "Al", "800", "330", "395", "450" },
                new() { "Al", "900", "355", "425", "480" },
                new() { "Al", "1000", "375", "445", "500" },
                new() { "Al", "1250", "405", "485", "545" },
                new() { "Al", "1500", "435", "520", "585" },
                new() { "Al", "1750", "455", "545", "615" },
                new() { "Al", "2000", "470", "560", "630" }
            }
        });

        // ── Conduit Fill — NEC Chapter 9, Tables 1 + 4 (EMT, THHN/THWN) ──
        // Tall format: Wire Size + Conduit Size → Max Conductors (cascading dropdowns)
        store.Sheets.Add(new CheatSheet
        {
            Id = "conduit-fill",
            Title = "Conduit Fill \u2014 EMT",
            Subtitle = "NEC Chapter 9, Tables 1 & 4",
            Description = "Maximum number of THHN/THWN conductors in EMT conduit (40% fill for 3+ conductors). Select wire size and conduit size to find the maximum conductor count.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "conduit", "fill", "raceway", "EMT", "Chapter 9", "wire count",
                "THHN", "THWN", "conductor", "40%", "trade size"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Wire Size", Unit = "AWG/kcmil", IsInputColumn = true },
                new() { Header = "Conduit Size", IsInputColumn = true },
                new() { Header = "Max Conductors", IsOutputColumn = true }
            },
            Rows = BuildConduitFillRows()
        });

        // ── Box Fill — NEC 314.16(B) ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "box-fill",
            Title = "Box Fill \u2014 Conductor Volume",
            Subtitle = "NEC Table 314.16(B)",
            Description = "Volume allowance per conductor for outlet/junction box fill calculations. Each conductor entering the box counts as one volume. Clamps, devices, and grounds have additional allowances (see notes).",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "box fill", "junction box", "outlet box", "volume", "314.16",
                "cubic inches", "conductor", "clamp", "device"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Conductor Size", Unit = "AWG", IsInputColumn = true },
                new() { Header = "Volume per Conductor", Unit = "in\u00b3", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "18", "1.50" },
                new() { "16", "1.75" },
                new() { "14", "2.00" },
                new() { "12", "2.25" },
                new() { "10", "2.50" },
                new() { "8", "3.00" },
                new() { "6", "5.00" },
                new() { "4", "7.50" }
            },
            NoteContent =
                "BOX FILL ALLOWANCES \u2014 NEC 314.16(B)\n\n" +
                "Each conductor that enters the box = 1\u00d7 volume from table above.\n\n" +
                "ADDITIONAL ALLOWANCES:\n" +
                "\u2022 Internal clamps (1 or more): add volume of LARGEST conductor \u00d7 1\n" +
                "\u2022 Support fittings: add volume of LARGEST conductor \u00d7 1\n" +
                "\u2022 Each yoke/strap (device): add volume of LARGEST conductor connected \u00d7 2\n" +
                "\u2022 Equipment grounding conductors (1 or more): add volume of LARGEST EGC \u00d7 1\n" +
                "\u2022 Conductors that pass through without splice or termination: count as 1 conductor\n" +
                "\u2022 Pigtails that do not leave the box: not counted\n" +
                "\u2022 Wire connectors: not counted separately\n\n" +
                "EXAMPLE:\n" +
                "4\u00d7 #12 conductors + 2\u00d7 #12 grounds + 1 device + 1 clamp set\n" +
                "= 4(2.25) + 1(2.25) + 2(2.25) + 1(2.25) = 18.0 in\u00b3 minimum box volume"
        });
    }

    /// <summary>
    /// Generates conduit fill rows in tall format: Wire Size, Conduit Size, Max Conductors.
    /// Pivots the original wide NEC table into a format suitable for cascading dropdown lookups.
    /// Rows with 0 conductors are omitted (wire doesn't fit in that conduit size).
    /// </summary>
    private static List<List<string>> BuildConduitFillRows()
    {
        var conduitSizes = new[] { "1/2\"", "3/4\"", "1\"", "1-1/4\"", "1-1/2\"", "2\"", "2-1/2\"", "3\"", "3-1/2\"", "4\"" };

        // Original NEC Ch9 data: wire size → max conductors per conduit size
        var data = new (string Wire, int[] Counts)[]
        {
            ("14",  new[] { 12, 22, 35, 61, 84, 138, 241, 364, 476, 608 }),
            ("12",  new[] { 9, 16, 26, 46, 63, 103, 181, 273, 357, 456 }),
            ("10",  new[] { 5, 10, 16, 28, 38, 63, 110, 166, 217, 277 }),
            ("8",   new[] { 3, 5, 9, 16, 21, 36, 62, 94, 123, 157 }),
            ("6",   new[] { 2, 4, 6, 11, 15, 26, 45, 68, 89, 114 }),
            ("4",   new[] { 1, 2, 4, 7, 9, 16, 27, 41, 54, 69 }),
            ("3",   new[] { 1, 1, 3, 6, 8, 13, 23, 35, 46, 59 }),
            ("2",   new[] { 1, 1, 3, 5, 7, 11, 20, 30, 39, 50 }),
            ("1",   new[] { 1, 1, 1, 4, 5, 8, 14, 22, 28, 36 }),
            ("1/0", new[] { 0, 1, 1, 3, 4, 7, 12, 18, 24, 31 }),
            ("2/0", new[] { 0, 1, 1, 2, 3, 6, 10, 16, 20, 26 }),
            ("3/0", new[] { 0, 1, 1, 2, 3, 5, 8, 13, 17, 22 }),
            ("4/0", new[] { 0, 1, 1, 1, 2, 4, 7, 11, 14, 18 }),
            ("250", new[] { 0, 0, 1, 1, 1, 3, 6, 9, 11, 15 }),
            ("300", new[] { 0, 0, 1, 1, 1, 3, 5, 7, 10, 13 }),
            ("350", new[] { 0, 0, 1, 1, 1, 2, 4, 7, 9, 11 }),
            ("400", new[] { 0, 0, 0, 1, 1, 2, 4, 6, 8, 10 }),
            ("500", new[] { 0, 0, 0, 1, 1, 1, 3, 5, 6, 8 }),
            ("600", new[] { 0, 0, 0, 1, 1, 1, 2, 4, 5, 7 }),
            ("700", new[] { 0, 0, 0, 1, 1, 1, 2, 3, 4, 6 }),
            ("750", new[] { 0, 0, 0, 0, 1, 1, 1, 3, 4, 5 }),
        };

        var rows = new List<List<string>>();
        foreach (var (wire, counts) in data)
        {
            for (var i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                    rows.Add(new List<string> { wire, conduitSizes[i], counts[i].ToString() });
            }
        }
        return rows;
    }
}
