using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Default cheat sheet data for the Plumbing discipline.
/// Water meter sizing table is in PlumbingDefaults.WaterMeter.cs (partial).
/// </summary>
internal static partial class CheatSheetPlumbingDefaults
{
    internal static void AddTo(CheatSheetDataStore store)
    {
        AddCoreTables(store);
        AddWaterMeterTable(store); // in PlumbingDefaults.WaterMeter.cs
        AddHydraulicCalcSheets(store); // in PlumbingDefaults.HydraulicCalc.cs
    }

    private static void AddCoreTables(CheatSheetDataStore store)
    {
        // Table E103.3(2) — Water Supply Fixture Units
        store.Sheets.Add(new CheatSheet
        {
            Id = "wsfu-fixture-loads",
            Title = "Water Supply Fixture Units",
            Subtitle = "IPC Table E103.3(2)",
            Description = "Load Values Assigned to Fixtures \u2014 Water Supply Fixture Units (wsfu)",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "wsfu", "water supply", "fixture units", "load", "plumbing", "E103.3", "hot", "cold" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Fixture", IsInputColumn = true },
                new() { Header = "Occupancy", IsInputColumn = true },
                new() { Header = "Supply Control", IsInputColumn = true },
                new() { Header = "Cold (wsfu)", Unit = "wsfu", IsOutputColumn = true },
                new() { Header = "Hot (wsfu)", Unit = "wsfu", IsOutputColumn = true },
                new() { Header = "Total (wsfu)", Unit = "wsfu", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Bathroom group", "Private", "Flush tank", "2.7", "1.5", "3.6" },
                new() { "Bathroom group", "Private", "Flushometer valve", "6.0", "3.0", "8.0" },
                new() { "Bathtub", "Private", "Faucet", "1.0", "1.0", "1.4" },
                new() { "Bathtub", "Public", "Faucet", "3.0", "3.0", "4.0" },
                new() { "Bidet", "Private", "Faucet", "1.5", "1.5", "2.0" },
                new() { "Combination fixture", "Private", "Faucet", "2.25", "2.25", "3.0" },
                new() { "Dishwashing machine", "Private", "Automatic", "\u2014", "1.4", "1.4" },
                new() { "Drinking fountain", "Offices, etc.", "3/8\" valve", "0.25", "\u2014", "0.25" },
                new() { "Kitchen sink", "Private", "Faucet", "1.0", "1.0", "1.4" },
                new() { "Kitchen sink", "Hotel, restaurant", "Faucet", "3.0", "3.0", "4.0" },
                new() { "Laundry trays (1 to 3)", "Private", "Faucet", "1.0", "1.0", "1.4" },
                new() { "Lavatory", "Private", "Faucet", "0.5", "0.5", "0.7" },
                new() { "Lavatory", "Public", "Faucet", "1.5", "1.5", "2.0" },
                new() { "Service sink", "Offices, etc.", "Faucet", "2.25", "2.25", "3.0" },
                new() { "Shower head", "Public", "Mixing valve", "3.0", "3.0", "4.0" },
                new() { "Shower head", "Private", "Mixing valve", "1.0", "1.0", "1.4" },
                new() { "Urinal", "Public", "1\" flushometer valve", "10.0", "\u2014", "10.0" },
                new() { "Urinal", "Public", "3/4\" flushometer valve", "5.0", "\u2014", "5.0" },
                new() { "Urinal", "Public", "Flush tank", "3.0", "\u2014", "3.0" },
                new() { "Washing machine (8 lb)", "Private", "Automatic", "1.0", "1.0", "1.4" },
                new() { "Washing machine (8 lb)", "Public", "Automatic", "2.25", "2.25", "3.0" },
                new() { "Washing machine (15 lb)", "Public", "Automatic", "3.0", "3.0", "4.0" },
                new() { "Water closet", "Private", "Flushometer valve", "6.0", "\u2014", "6.0" },
                new() { "Water closet", "Private", "Flush tank", "2.2", "\u2014", "2.2" },
                new() { "Water closet", "Public", "Flushometer valve", "10.0", "\u2014", "10.0" },
                new() { "Water closet", "Public", "Flush tank", "5.0", "\u2014", "5.0" },
                new() { "Water closet", "Public or private", "Flushometer tank", "2.0", "\u2014", "2.0" }
            }
        });

        // Table 710.1(1) — Building Drains and Sewers
        store.Sheets.Add(new CheatSheet
        {
            Id = "building-drains-sewers",
            Title = "Building Drains and Sewers",
            Subtitle = "IPC Table 710.1(1)",
            Description = "Maximum Number of Drainage Fixture Units Connected to Any Portion of the Building Drain or Sewer, Including Branches of the Building Drain",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "drain", "sewer", "building drain", "DFU", "drainage", "fixture units", "pipe size", "slope", "710.1" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Pipe Diameter (in)", Unit = "in", IsInputColumn = true },
                new() { Header = "1/16 in slope", Unit = "DFU", IsOutputColumn = true },
                new() { Header = "1/8 in slope", Unit = "DFU", IsOutputColumn = true },
                new() { Header = "1/4 in slope", Unit = "DFU", IsOutputColumn = true },
                new() { Header = "1/2 in slope", Unit = "DFU", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1-1/4", "\u2014", "\u2014", "1", "1" },
                new() { "1-1/2", "\u2014", "\u2014", "3", "3" },
                new() { "2", "\u2014", "\u2014", "21", "26" },
                new() { "2-1/2", "\u2014", "\u2014", "24", "31" },
                new() { "3", "\u2014", "36", "42", "50" },
                new() { "4", "\u2014", "180", "216", "250" },
                new() { "5", "\u2014", "390", "480", "575" },
                new() { "6", "\u2014", "700", "840", "1,000" },
                new() { "8", "1,400", "1,600", "1,920", "2,300" },
                new() { "10", "2,500", "2,900", "3,500", "4,200" },
                new() { "12", "3,900", "4,600", "5,600", "6,700" },
                new() { "15", "7,000", "8,300", "10,000", "12,000" }
            }
        });

        // Table P3004.1 — Drainage Fixture Unit (DFU) Values
        store.Sheets.Add(new CheatSheet
        {
            Id = "dfu-fixture-values",
            Title = "Drainage Fixture Unit Values",
            Subtitle = "IPC Table P3004.1",
            Description = "Drainage Fixture Unit (d.f.u.) Values for Various Plumbing Fixtures",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "DFU", "drainage", "fixture unit", "plumbing", "P3004.1", "fixture", "drain" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Fixture Type", IsInputColumn = true },
                new() { Header = "DFU Value", Unit = "d.f.u.", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Bar sink", "1" },
                new() { "Bathtub (with or without shower head/whirlpool)", "2" },
                new() { "Bidet", "1" },
                new() { "Clothes washer standpipe", "2" },
                new() { "Dishwasher", "2" },
                new() { "Floor drain", "0" },
                new() { "Kitchen sink", "2" },
                new() { "Lavatory", "1" },
                new() { "Laundry tub", "2" },
                new() { "Shower stall", "2" },
                new() { "Water closet (1.6 gpf)", "3" },
                new() { "Water closet (> 1.6 gpf)", "4" },
                new() { "Full-bath group (1.6 gpf WC, w/ or w/o shower)", "5" },
                new() { "Full-bath group (> 1.6 gpf WC, w/ or w/o shower)", "6" },
                new() { "Half-bath group (1.6 gpf WC + lavatory)", "4" },
                new() { "Half-bath group (> 1.6 gpf WC + lavatory)", "5" },
                new() { "Kitchen group (dishwasher + sink)", "2" },
                new() { "Laundry group (washer standpipe + laundry tub)", "3" },
                new() { "Multiple-bath groups: 1.5 baths", "7" },
                new() { "Multiple-bath groups: 2 baths", "8" },
                new() { "Multiple-bath groups: 2.5 baths", "9" },
                new() { "Multiple-bath groups: 3 baths", "10" },
                new() { "Multiple-bath groups: 3.5 baths", "11" }
            }
        });

        // Table 1106.2 — Storm Drain Pipe Sizing
        store.Sheets.Add(new CheatSheet
        {
            Id = "storm-drain-sizing",
            Title = "Storm Drain Pipe Sizing",
            Subtitle = "IPC Table 1106.2",
            Description = "Storm Drain Pipe Sizing \u2014 Capacity in GPM for Vertical Drains and Horizontal Drains at Various Slopes",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "storm", "drain", "pipe", "sizing", "GPM", "capacity", "slope", "vertical", "horizontal", "1106.2" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Pipe Size (in)", Unit = "in", IsInputColumn = true },
                new() { Header = "Vertical Drain (gpm)", Unit = "gpm", IsOutputColumn = true },
                new() { Header = "1/16 in/ft (gpm)", Unit = "gpm", IsOutputColumn = true },
                new() { Header = "1/8 in/ft (gpm)", Unit = "gpm", IsOutputColumn = true },
                new() { Header = "1/4 in/ft (gpm)", Unit = "gpm", IsOutputColumn = true },
                new() { Header = "1/2 in/ft (gpm)", Unit = "gpm", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "2", "34", "15", "22", "31", "44" },
                new() { "3", "87", "39", "55", "79", "111" },
                new() { "4", "180", "81", "115", "163", "231" },
                new() { "5", "311", "117", "165", "234", "331" },
                new() { "6", "538", "243", "344", "487", "689" },
                new() { "8", "1,117", "505", "714", "1,010", "1,429" },
                new() { "10", "2,050", "927", "1,311", "1,855", "2,623" },
                new() { "12", "3,272", "1,480", "2,093", "2,960", "4,187" },
                new() { "15", "5,543", "2,508", "3,546", "5,016", "7,093" }
            }
        });

        // Table 604.4 — Maximum Flow Rates and Consumption
        store.Sheets.Add(new CheatSheet
        {
            Id = "max-flow-rates",
            Title = "Maximum Flow Rates",
            Subtitle = "IPC Table 604.4",
            Description = "Maximum Flow Rates and Consumption for Plumbing Fixtures and Fixture Fittings",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "flow rate", "GPM", "consumption", "fixture", "faucet", "shower", "604.4", "maximum" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Fixture / Fitting", IsInputColumn = true },
                new() { Header = "Maximum Flow Rate or Quantity", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Lavatory, private", "2.2 gpm at 60 psi" },
                new() { "Lavatory, public (metering)", "0.25 gallon per metering cycle" },
                new() { "Lavatory, public (other than metering)", "0.5 gpm at 60 psi" },
                new() { "Shower head", "2.5 gpm at 80 psi" },
                new() { "Sink faucet", "2.2 gpm at 60 psi" },
                new() { "Urinal", "1.0 gallon per flushing cycle" },
                new() { "Water closet", "1.6 gallons per flushing cycle" }
            }
        });

        // Table 710.1(2) — Horizontal Fixture Branches and Stacks
        store.Sheets.Add(new CheatSheet
        {
            Id = "horizontal-branches-stacks",
            Title = "Horizontal Branches and Stacks",
            Subtitle = "IPC Table 710.1(2)",
            Description = "Maximum number of drainage fixture units for horizontal fixture branches and stacks",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "branch", "stack", "DFU", "drainage", "fixture units", "pipe size", "710.1", "horizontal" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Pipe Dia.", Unit = "in", IsInputColumn = true },
                new() { Header = "Horiz. Branch", Unit = "DFU", IsOutputColumn = true },
                new() { Header = "1 Branch Interval", Unit = "DFU", IsOutputColumn = true },
                new() { Header = "Stack \u22643 Intervals", Unit = "DFU", IsOutputColumn = true },
                new() { Header = "Stack >3 Intervals", Unit = "DFU", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1-1/2", "3", "2", "4", "8" },
                new() { "2", "6", "6", "10", "24" },
                new() { "2-1/2", "12", "9", "20", "42" },
                new() { "3", "20", "20", "48", "72" },
                new() { "4", "160", "90", "240", "500" },
                new() { "5", "360", "200", "540", "1,100" },
                new() { "6", "620", "350", "960", "1,900" },
                new() { "8", "1,400", "600", "2,200", "3,600" },
                new() { "10", "2,500", "1,000", "3,800", "5,600" },
                new() { "12", "3,900", "1,500", "6,000", "8,400" },
                new() { "15", "7,000", "Note c", "Note c", "Note c" }
            },
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "Count DFU", Icon = "\U0001F4CA",
                    Description = "Sum drainage fixture units for the pipe section.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "hb_dfu", Label = "Total DFU", Unit = "d.f.u.", Default = "20", Hint = "Sum from Table 709.1" }
                    },
                    Tip = "Use the \"Drainage Fixture Units\" sheet for per-fixture DFU values.",
                    Reference = "dfu-fixtures-groups"
                },
                new()
                {
                    Number = 2, Title = "Select Pipe Type", Icon = "\U0001F527",
                    Description = "Choose which column applies to your installation.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "hb_type", Label = "Pipe Type", Hint = "Horizontal Branch, 1 Interval, Stack \u22643, or Stack >3" }
                    },
                    Tip = "Horizontal Branch = lateral pipe from fixtures. Stack = vertical riser. Branch Interval = floor-to-floor distance on a stack."
                },
                new()
                {
                    Number = 3, Title = "Read Minimum Pipe Size", Icon = "\u2705",
                    Description = "Find the smallest pipe whose DFU capacity \u2265 your total.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "hb_result", Label = "Min. Pipe Diameter", Unit = "in", Hint = "From table above" }
                    },
                    Tip = "Example: 20 DFU on a horizontal branch \u2192 3\" pipe (capacity 20 DFU). Always round up to the next available size."
                }
            }
        });

        // Table 709.1 — Drainage Fixture Units for Fixtures and Groups
        store.Sheets.Add(new CheatSheet
        {
            Id = "dfu-fixtures-groups",
            Title = "Drainage Fixture Units - Fixtures & Groups",
            Subtitle = "IPC Table 709.1",
            Description = "Drainage fixture unit (DFU) values for various plumbing fixtures and groups, with minimum trap sizes",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "DFU", "drainage", "fixture unit", "trap", "709.1", "fixture", "group" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Fixture Type", IsInputColumn = true },
                new() { Header = "DFU", Unit = "d.f.u.", IsOutputColumn = true },
                new() { Header = "Min. Trap", Unit = "in", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Automatic clothes washer, commercial", "3", "2" },
                new() { "Automatic clothes washer, residential", "2", "2" },
                new() { "Bathroom group (1.6 gpf WC)", "5", "\u2014" },
                new() { "Bathroom group (>1.6 gpf WC)", "6", "\u2014" },
                new() { "Bathtub (w/ or w/o shower/whirlpool)", "2", "1-1/2" },
                new() { "Bidet", "1", "1-1/4" },
                new() { "Combination sink and tray", "2", "1-1/2" },
                new() { "Dental lavatory", "1", "1-1/4" },
                new() { "Dental unit or cuspidor", "1", "1-1/4" },
                new() { "Dishwashing machine, domestic", "2", "1-1/2" },
                new() { "Drinking fountain", "1/2", "1-1/4" },
                new() { "Emergency floor drain", "0", "2" },
                new() { "Floor drains", "2", "2" },
                new() { "Floor sinks", "Note h", "2" },
                new() { "Kitchen sink, domestic", "2", "1-1/2" },
                new() { "Kitchen sink w/ disposer/dishwasher", "2", "1-1/2" },
                new() { "Laundry tray (1 or 2 compartments)", "2", "1-1/2" },
                new() { "Lavatory", "1", "1-1/4" },
                new() { "Shower \u2014 \u22645.7 gpm", "2", "1-1/2" },
                new() { "Shower \u2014 >5.7 to 12.3 gpm", "3", "2" },
                new() { "Shower \u2014 >12.3 to 25.8 gpm", "5", "3" },
                new() { "Shower \u2014 >25.8 to 55.6 gpm", "6", "4" },
                new() { "Service sink", "2", "1-1/2" },
                new() { "Sink", "2", "1-1/2" },
                new() { "Urinal", "4", "Per outlet" },
                new() { "Urinal, \u22641 gallon per flush", "2", "Per outlet" },
                new() { "Urinal, nonwater supplied", "1/2", "Per outlet" },
                new() { "Wash sink (each set of faucets)", "2", "1-1/2" },
                new() { "WC, flushometer tank (public/private)", "4", "Per outlet" },
                new() { "WC, private (1.6 gpf)", "3", "Per outlet" },
                new() { "WC, private (>1.6 gpf)", "4", "Per outlet" },
                new() { "WC, public (1.6 gpf)", "4", "Per outlet" },
                new() { "WC, public (>1.6 gpf)", "6", "Per outlet" }
            },
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "List Fixtures", Icon = "\U0001F4CB",
                    Description = "Count each plumbing fixture type in your project.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "df_wc", Label = "Water Closets (1.6 gpf)", Default = "4", Hint = "Private = 3 DFU each" },
                        new() { Id = "df_lav", Label = "Lavatories", Default = "4", Hint = "1 DFU each" },
                        new() { Id = "df_shower", Label = "Showers (\u22645.7 gpm)", Default = "2", Hint = "2 DFU each" },
                        new() { Id = "df_tub", Label = "Bathtubs", Default = "2", Hint = "2 DFU each" },
                        new() { Id = "df_ksink", Label = "Kitchen Sinks", Default = "1", Hint = "2 DFU each" },
                        new() { Id = "df_dw", Label = "Dishwashers", Default = "1", Hint = "2 DFU each" },
                        new() { Id = "df_cw", Label = "Clothes Washers", Default = "1", Hint = "2 DFU each (residential)" }
                    }
                },
                new()
                {
                    Number = 2, Title = "Calculate Total DFU", Icon = "\U0001F4CA",
                    Description = "Multiply counts by DFU values and sum.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "df_wc_total", Label = "WC DFU", Unit = "d.f.u.", IsOutput = true, Formula = "{df_wc} * 3" },
                        new() { Id = "df_lav_total", Label = "Lav DFU", Unit = "d.f.u.", IsOutput = true, Formula = "{df_lav} * 1" },
                        new() { Id = "df_shower_total", Label = "Shower DFU", Unit = "d.f.u.", IsOutput = true, Formula = "{df_shower} * 2" },
                        new() { Id = "df_tub_total", Label = "Tub DFU", Unit = "d.f.u.", IsOutput = true, Formula = "{df_tub} * 2" },
                        new() { Id = "df_ksink_total", Label = "K. Sink DFU", Unit = "d.f.u.", IsOutput = true, Formula = "{df_ksink} * 2" },
                        new() { Id = "df_dw_total", Label = "DW DFU", Unit = "d.f.u.", IsOutput = true, Formula = "{df_dw} * 2" },
                        new() { Id = "df_cw_total", Label = "CW DFU", Unit = "d.f.u.", IsOutput = true, Formula = "{df_cw} * 2" }
                    }
                },
                new()
                {
                    Number = 3, Title = "Total DFU", Icon = "\u2705",
                    Description = "Sum all fixture DFU values.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "df_grand_total", Label = "TOTAL DFU", Unit = "d.f.u.", IsOutput = true,
                                Formula = "{df_wc_total} + {df_lav_total} + {df_shower_total} + {df_tub_total} + {df_ksink_total} + {df_dw_total} + {df_cw_total}" }
                    },
                    Tip = "Use this total with the Building Drains table or Horizontal Branches table to size your pipes.",
                    Reference = "horizontal-branches-stacks"
                }
            }
        });
    }
}
