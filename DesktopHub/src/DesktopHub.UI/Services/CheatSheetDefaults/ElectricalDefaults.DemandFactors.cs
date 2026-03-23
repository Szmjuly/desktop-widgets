using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Electrical cheat sheets: NEC demand factor tables (220.42, 220.54, 220.55, 220.56).
/// </summary>
internal static partial class CheatSheetElectricalDefaults
{
    private static void AddDemandFactorSheets(CheatSheetDataStore store)
    {
        // ── NEC Table 220.42 — General Lighting Demand Factors ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "demand-general-lighting",
            Title = "Demand Factors \u2014 General Lighting",
            Subtitle = "NEC Table 220.42",
            Description = "Lighting load demand factors by occupancy type. Apply to the general lighting load calculated from NEC 220.12 (unit lighting load \u00d7 area).",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "demand factor", "general lighting", "220.42", "load calculation",
                "occupancy", "dwelling", "hospital", "hotel", "warehouse"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Occupancy Type", IsInputColumn = true },
                new() { Header = "Portion of Lighting Load", IsInputColumn = true },
                new() { Header = "Demand Factor", Unit = "%", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Dwelling Units", "First 3,000 VA or less", "100" },
                new() { "Dwelling Units", "3,001 VA to 120,000 VA", "35" },
                new() { "Dwelling Units", "Remainder over 120,000 VA", "25" },
                new() { "Hospitals", "First 50,000 VA or less", "40" },
                new() { "Hospitals", "Remainder over 50,000 VA", "20" },
                new() { "Hotels/Motels (no cooking)", "First 20,000 VA or less", "50" },
                new() { "Hotels/Motels (no cooking)", "20,001 VA to 100,000 VA", "40" },
                new() { "Hotels/Motels (no cooking)", "Remainder over 100,000 VA", "30" },
                new() { "Warehouses (Storage)", "First 12,500 VA or less", "100" },
                new() { "Warehouses (Storage)", "Remainder over 12,500 VA", "50" },
                new() { "All Others", "Total VA", "100" }
            },
            NoteContent =
                "NEC 220.42 NOTES:\n\n" +
                "\u2022 The demand factors apply to the GENERAL LIGHTING load only (NEC 220.12).\n" +
                "\u2022 Do NOT apply these factors to receptacle loads calculated under 220.14.\n" +
                "\u2022 For dwelling units, the first 3,000 VA at 100% includes the small appliance\n" +
                "  and laundry branch circuit loads added per 220.52.\n" +
                "\u2022 Track lighting: use 150 VA per 2 ft of track or fraction thereof (220.43(B))."
        });

        // ── NEC Table 220.54 — Dryer Demand Factors ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "demand-dryers",
            Title = "Demand Factors \u2014 Dryers",
            Subtitle = "NEC Table 220.54",
            Description = "Demand factors for household clothes dryers in multifamily dwellings. Use 5,000 watts or the nameplate rating, whichever is larger, per dryer.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "demand factor", "dryer", "clothes dryer", "laundry", "220.54",
                "residential", "multifamily", "dwelling"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Number of Dryers", IsInputColumn = true },
                new() { Header = "Demand Factor", Unit = "%", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1", "100" },
                new() { "2", "100" },
                new() { "3", "100" },
                new() { "4", "100" },
                new() { "5", "80" },
                new() { "6", "70" },
                new() { "7", "65" },
                new() { "8", "60" },
                new() { "9", "55" },
                new() { "10", "50" },
                new() { "11", "50" },
                new() { "12\u201313", "45" },
                new() { "14\u201319", "40" },
                new() { "20\u201324", "35" },
                new() { "25\u201329", "32.5" },
                new() { "30\u201334", "30" },
                new() { "35\u201339", "27.5" },
                new() { "40 & over", "25" }
            },
            NoteContent =
                "NEC 220.54 NOTES:\n\n" +
                "\u2022 Use 5,000 watts or the nameplate rating (whichever is larger) per dryer.\n" +
                "\u2022 For 1\u20134 dryers, the demand factor is 100%.\n" +
                "\u2022 These factors apply to the FEEDER/SERVICE calculation, not the branch circuit.\n" +
                "\u2022 Each individual dryer branch circuit must be sized for the full dryer load."
        });

        // ── NEC Table 220.55 — Demand Factors for Household Cooking Appliances ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "demand-ranges",
            Title = "Demand Factors \u2014 Ranges & Ovens",
            Subtitle = "NEC Table 220.55",
            Description = "Demand loads for household electric ranges, wall-mounted ovens, counter-mounted cooking units, and other household cooking appliances over 1\u00be kW rating.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "demand factor", "range", "oven", "cooking", "220.55",
                "residential", "dwelling", "household", "appliance", "cooktop"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Number of Appliances", IsInputColumn = true },
                new() { Header = "Col A: Max Demand (kW)", Unit = "kW", IsOutputColumn = true },
                new() { Header = "Col B: Demand Factor (%)", Unit = "%", IsOutputColumn = true },
                new() { Header = "Col C: Demand Factor (%)", Unit = "%", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1", "8", "80", "80" },
                new() { "2", "11", "75", "65" },
                new() { "3", "14", "70", "55" },
                new() { "4", "17", "66", "50" },
                new() { "5", "20", "62", "45" },
                new() { "6", "21", "59", "43" },
                new() { "7", "22", "56", "40" },
                new() { "8", "23", "53", "36" },
                new() { "9", "24", "51", "35" },
                new() { "10", "25", "49", "34" },
                new() { "11", "26", "47", "32" },
                new() { "12", "27", "45", "32" },
                new() { "13\u201314", "28\u201329", "43\u201341", "32" },
                new() { "15\u201318", "30\u201333", "40\u201336", "28" },
                new() { "19\u201322", "34\u201337", "35\u201332", "28" },
                new() { "23\u201325", "38\u201340", "31\u201330", "26" }
            },
            NoteContent =
                "NEC TABLE 220.55 COLUMN GUIDE:\n\n" +
                "COLUMN A \u2014 applies to ranges rated not over 12 kW.\n" +
                "For ranges over 12 kW but not over 27 kW:\n" +
                "  Increase Col A value by 5% for each additional kW (or fraction) above 12 kW.\n\n" +
                "COLUMN B \u2014 applies to ranges rated 3\u00bd kW through 8\u00be kW.\n" +
                "  Apply the % to the NAMEPLATE RATING of each appliance.\n\n" +
                "COLUMN C \u2014 applies to ranges rated 8\u00be kW through 27 kW.\n" +
                "  Apply the % to the NAMEPLATE RATING of each appliance.\n\n" +
                "NOTES:\n" +
                "\u2022 Wall-mounted ovens and counter-mounted cooking units in the same room\n" +
                "  can be added together and treated as a single range.\n" +
                "\u2022 Branch circuits for individual ranges: 8 kW demand or nameplate, whichever is larger.\n" +
                "\u2022 See NEC 220.55 Notes 1\u20135 for complete rules."
        });

        // ── NEC Table 220.56 — Kitchen Equipment Demand Factors (Commercial) ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "demand-kitchen-equipment",
            Title = "Demand Factors \u2014 Kitchen Equipment",
            Subtitle = "NEC Table 220.56",
            Description = "Demand factors for commercial kitchen equipment. Applies to thermostatic-controlled or intermittent-use equipment. Does not apply to space heating, ventilation, or illumination equipment.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "demand factor", "kitchen", "equipment", "commercial", "220.56",
                "cooking", "restaurant", "food service"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Number of Units", IsInputColumn = true },
                new() { Header = "Demand Factor", Unit = "%", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1", "100" },
                new() { "2", "100" },
                new() { "3", "90" },
                new() { "4", "80" },
                new() { "5", "70" },
                new() { "6 & over", "65" }
            },
            NoteContent =
                "NEC 220.56 NOTES:\n\n" +
                "\u2022 These factors apply to commercial kitchen equipment that is\n" +
                "  thermostatically controlled or intermittently used.\n" +
                "\u2022 DO NOT apply to: space heating, ventilation, or illumination equipment.\n" +
                "\u2022 Minimum feeder load: not less than the sum of the two largest\n" +
                "  kitchen equipment loads.\n" +
                "\u2022 All kitchen equipment on a single feeder may use these demand factors.\n" +
                "\u2022 Separate feeders for individual equipment must be sized at 100%."
        });
    }
}
