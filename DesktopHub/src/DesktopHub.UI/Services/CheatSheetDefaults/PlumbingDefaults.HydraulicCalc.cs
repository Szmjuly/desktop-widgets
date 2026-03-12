using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Hydraulic calculation reference data for the Plumbing discipline.
/// Includes WSFU→GPM demand table, step-by-step hydraulic calc guide,
/// water supply calculation worksheet, and friction loss worksheet.
/// </summary>
internal static partial class CheatSheetPlumbingDefaults
{
    private static void AddHydraulicCalcSheets(CheatSheetDataStore store)
    {
        // Table E103.3(3) — Estimating Demand (WSFU → GPM)
        store.Sheets.Add(new CheatSheet
        {
            Id = "wsfu-to-gpm-demand",
            Title = "WSFU to GPM \u2014 Estimating Demand",
            Subtitle = "IPC Table E103.3(3)",
            Description = "Convert total Water Supply Fixture Units (WSFU) to demand in Gallons Per Minute (GPM). Select Flush Tank or Flushometer Valve column based on your supply system type.",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "WSFU", "GPM", "demand", "estimating", "flush tank", "flushometer", "E103.3", "gallons per minute", "hydraulic" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "WSFU", Unit = "wsfu", IsInputColumn = true },
                new() { Header = "Flush Tank GPM", Unit = "gpm", IsOutputColumn = true },
                new() { Header = "Flushometer GPM", Unit = "gpm", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1", "3.0", "\u2014" },
                new() { "2", "5.0", "\u2014" },
                new() { "3", "6.5", "\u2014" },
                new() { "4", "8.0", "\u2014" },
                new() { "5", "9.4", "15.0" },
                new() { "6", "10.7", "17.4" },
                new() { "7", "11.8", "19.8" },
                new() { "8", "12.8", "22.2" },
                new() { "9", "13.7", "24.6" },
                new() { "10", "14.6", "27.0" },
                new() { "11", "15.4", "27.8" },
                new() { "12", "16.0", "28.6" },
                new() { "13", "16.5", "29.4" },
                new() { "14", "17.0", "30.2" },
                new() { "15", "17.5", "31.0" },
                new() { "16", "18.0", "31.8" },
                new() { "17", "18.4", "32.6" },
                new() { "18", "18.8", "33.4" },
                new() { "19", "19.2", "34.2" },
                new() { "20", "19.6", "35.0" },
                new() { "25", "21.5", "38.0" },
                new() { "30", "23.3", "42.0" },
                new() { "35", "24.9", "44.0" },
                new() { "40", "26.3", "46.0" },
                new() { "45", "27.7", "48.0" },
                new() { "50", "29.1", "50.0" },
                new() { "60", "32.0", "54.0" },
                new() { "70", "35.0", "58.0" },
                new() { "80", "38.0", "61.2" },
                new() { "90", "41.0", "64.3" },
                new() { "100", "43.5", "67.5" },
                new() { "120", "48.0", "73.0" },
                new() { "140", "52.5", "77.0" },
                new() { "160", "57.0", "81.0" },
                new() { "180", "61.0", "85.5" },
                new() { "200", "65.0", "90.0" },
                new() { "225", "70.0", "95.5" },
                new() { "250", "75.0", "101.0" },
                new() { "275", "80.0", "104.5" },
                new() { "300", "85.0", "108.0" },
                new() { "400", "105.0", "127.0" },
                new() { "500", "124.0", "143.0" },
                new() { "750", "170.0", "177.0" },
                new() { "1000", "208.0", "208.0" },
                new() { "1250", "239.0", "239.0" },
                new() { "1500", "269.0", "269.0" },
                new() { "1750", "297.0", "297.0" },
                new() { "2000", "325.0", "325.0" },
                new() { "2500", "380.0", "380.0" },
                new() { "3000", "433.0", "433.0" }
            }
        });

        // Hydraulic Calculation — Step-by-Step Guide
        store.Sheets.Add(new CheatSheet
        {
            Id = "hydraulic-calc-guide",
            Title = "Hydraulic Calc \u2014 Step by Step",
            Subtitle = "Water Supply Calculation Procedure",
            Description = "Step-by-step guide for performing a hydraulic calculation to determine if the building water supply has adequate pressure or if a booster pump is needed.",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Note,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "hydraulic", "calculation", "water supply", "pressure", "booster pump", "friction loss", "step by step", "procedure" },
            NoteContent =
                "WATER SUPPLY HYDRAULIC CALCULATION \u2014 STEP BY STEP\n\n" +

                "STEP 1: GATHER PROJECT INFO\n" +
                "\u2022 Building height (ft)\n" +
                "\u2022 Supply pressure from building/utility (PSI)\n" +
                "\u2022 Pipe sizes: Service pipe, Distribution Hot, Distribution Cold\n" +
                "\u2022 Max pipe length from meter to farthest fixture (ft)\n\n" +

                "STEP 2: CALCULATE TOTAL WSFU\n" +
                "\u2022 Count all fixtures using Table E103.3(2) \u2014 see \"Water Supply Fixture Units\" sheet\n" +
                "\u2022 Sum total Water Supply Fixture Units for the system\n\n" +

                "STEP 3: CONVERT WSFU \u2192 GPM\n" +
                "\u2022 Use Table E103.3(3) \u2014 see \"WSFU to GPM\" sheet\n" +
                "\u2022 Select Flush Tank or Flushometer Valve column based on your system\n\n" +

                "STEP 4: DETERMINE PIPE LENGTHS\n" +
                "\u2022 Max Pipe Length = measured length from meter to farthest fixture\n" +
                "\u2022 Adjusted Length = Max Pipe Length \u00d7 1.2 (rule of thumb for fittings)\n" +
                "  OR calculate actual equivalent fittings from pipe bends and valves\n\n" +

                "STEP 5: CALCULATE PRESSURE LOSSES\n" +
                "a) Pressure Loss Per Height = Building Height (ft) \u00f7 2.31 = ___ PSI\n" +
                "b) Highest Fixture Pressure = typically 40 PSI\n" +
                "   (can adjust lower to avoid booster pump if client agrees)\n" +
                "c) Meter Loss = typically 13 PSI (0 for condo buildings)\n" +
                "d) RPZ / Backflow Preventer = typically 13 PSI\n" +
                "   (0 for condo with 2\" Watts RPZ due to building type)\n" +
                "e) Special Losses (filters, RO, etc.) = varies by filter type\n\n" +

                "STEP 6: TOTAL LOSSES\n" +
                "Total Losses = (a) + (b) + (c) + (d) + (e)\n\n" +

                "STEP 7: PRESSURE AVAILABLE\n" +
                "Pressure Available = Supply Pressure \u2212 Total Losses\n" +
                "\u2022 POSITIVE \u2192 system has adequate static pressure\n" +
                "\u2022 NEGATIVE \u2192 booster pump is likely required\n\n" +

                "STEP 8: FRICTION LOSS CALCULATION (per section)\n" +
                "For each pipe section (e.g., A\u2013B, B\u2013C, etc.):\n" +
                "1. WSFU for the section\n" +
                "2. Convert WSFU \u2192 GPM using Table E103.3(3)\n" +
                "3. Pipe length (ft) of the section\n" +
                "4. Trial pipe size (from sanitary calculation)\n" +
                "5. Count equivalent fittings from bends in the pipe\n" +
                "6. Total Equiv. Length = Pipe Length + Equiv. Fittings\n" +
                "7. Friction Loss per 100 ft \u2192 from Pressure Drop chart\n" +
                "   (use GPM and pipe size to read the chart)\n" +
                "8. Friction Loss = (Total Equiv. Length \u00f7 100) \u00d7 Loss per 100 ft\n\n" +

                "STEP 9: FINAL CHECK\n" +
                "Difference = Pressure Available \u2212 Total Friction Loss\n" +
                "\u2022 POSITIVE \u2192 adequate pressure, design is acceptable\n" +
                "\u2022 NEGATIVE \u2192 increase pipe size or add booster pump\n\n" +

                "NOTES:\n" +
                "\u2022 Trial pipe size comes from your sanitary calculation\n" +
                "\u2022 Equivalent fittings come from bends in the pipe\n" +
                "\u2022 Pressure drop chart: use GPM (Y-axis) and pipe diameter\n" +
                "  to find pressure drop per 100 ft of tube (X-axis)\n" +
                "\u2022 Fluid velocities in excess of 5\u20138 ft/sec are not usually recommended",
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "Gather Project Info", Icon = "\U0001F4CB",
                    Description = "Collect building parameters and pipe sizes from project documents.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "height", Label = "Building Height", Unit = "ft", Default = "32" },
                        new() { Id = "supply_pressure", Label = "Supply Pressure", Unit = "PSI", Default = "55", Hint = "Regulated by building/utility" },
                        new() { Id = "service_pipe", Label = "Service Pipe", Unit = "in", Default = "1.5" },
                        new() { Id = "dist_hot", Label = "Distribution Hot", Unit = "in", Default = "1.25" },
                        new() { Id = "dist_cold", Label = "Distribution Cold", Unit = "in", Default = "1.25" },
                        new() { Id = "max_pipe_length", Label = "Max Pipe Length", Unit = "ft", Default = "145", Hint = "Meter to farthest fixture" }
                    }
                },
                new()
                {
                    Number = 2, Title = "Calculate Total WSFU", Icon = "\U0001F4CA",
                    Description = "Count all fixtures and sum their Water Supply Fixture Units.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "total_wsfu", Label = "Total WSFU", Unit = "wsfu", Default = "44.2", Hint = "Sum from Table E103.3(2)" }
                    },
                    Tip = "Use the \"Water Supply Fixture Units\" sheet for per-fixture values.",
                    Reference = "wsfu-fixture-loads"
                },
                new()
                {
                    Number = 3, Title = "Convert WSFU \u2192 GPM", Icon = "\U0001F4A7",
                    Description = "Look up the demand in gallons per minute from your total WSFU.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "gpm", Label = "Demand (GPM)", Unit = "gpm", Default = "27.7", Hint = "From Table E103.3(3) \u2014 flush tank or flushometer" }
                    },
                    Tip = "Select the Flush Tank or Flushometer column based on your system type.",
                    Reference = "wsfu-to-gpm-demand"
                },
                new()
                {
                    Number = 4, Title = "Determine Pipe Lengths", Icon = "\U0001F4CF",
                    Description = "Calculate adjusted length accounting for fittings.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "adjusted_length", Label = "Adjusted Length", Unit = "ft", IsOutput = true, Formula = "{max_pipe_length} * 1.2" }
                    },
                    Tip = "Rule of thumb: multiply max pipe length by 1.2. Or calculate actual equivalent fittings."
                },
                new()
                {
                    Number = 5, Title = "Calculate Pressure Losses", Icon = "\u26A1",
                    Description = "Determine each source of pressure loss in the system.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "loss_height", Label = "a) Loss per Height", Unit = "PSI", IsOutput = true, Formula = "{height} / 2.31" },
                        new() { Id = "fixture_pressure", Label = "b) Fixture Pressure", Unit = "PSI", Default = "40", Hint = "Can reduce to avoid booster pump" },
                        new() { Id = "meter_loss", Label = "c) Meter Loss", Unit = "PSI", Default = "0", Hint = "Typically 13, or 0 for condo" },
                        new() { Id = "rpz_loss", Label = "d) RPZ / Backflow", Unit = "PSI", Default = "0", Hint = "Typically 13, or 0 for condo w/ 2\" Watts" },
                        new() { Id = "special_losses", Label = "e) Special Losses", Unit = "PSI", Default = "2", Hint = "Filters, RO, etc. \u2014 varies by type" }
                    },
                    Tip = "Meter & RPZ losses are usually 13 PSI each, but 0 for condo buildings with 2\" Watts RPZ."
                },
                new()
                {
                    Number = 6, Title = "Total Losses", Icon = "\u2211",
                    Description = "Sum all pressure losses.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "total_losses", Label = "Total Losses", Unit = "PSI", IsOutput = true,
                                Formula = "{loss_height} + {fixture_pressure} + {meter_loss} + {rpz_loss} + {special_losses}" }
                    }
                },
                new()
                {
                    Number = 7, Title = "Pressure Available", Icon = "\U0001F50D",
                    Description = "Determine if the system has adequate static pressure.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "pressure_available", Label = "Pressure Available", Unit = "PSI", IsOutput = true,
                                Formula = "{supply_pressure} - {total_losses}", Highlight = "positive-negative" }
                    },
                    Tip = "Positive = OK. Negative = booster pump is likely required."
                },
                new()
                {
                    Number = 8, Title = "Friction Loss (per section)", Icon = "\U0001F527",
                    Description = "Calculate friction losses for a pipe section using the pressure drop chart.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "section_length", Label = "Section Length", Unit = "ft", Default = "145" },
                        new() { Id = "trial_pipe", Label = "Trial Pipe Size", Unit = "in", Default = "1.25", Hint = "From sanitary calculation" },
                        new() { Id = "equiv_fittings", Label = "Equiv. Fittings", Unit = "ft", Default = "29", Hint = "From bends, valves, tees" },
                        new() { Id = "total_equiv", Label = "Total Equiv. Length", Unit = "ft", IsOutput = true, Formula = "{section_length} + {equiv_fittings}" },
                        new() { Id = "friction_per_100", Label = "Friction Loss / 100 ft", Unit = "PSI", Default = "0.052", Hint = "From pressure drop chart (GPM + pipe size)" },
                        new() { Id = "friction_loss", Label = "Section Friction Loss", Unit = "PSI", IsOutput = true, Formula = "{total_equiv} * {friction_per_100} / 100" }
                    },
                    Tip = "Read the pressure drop chart: GPM on Y-axis, pipe diameter on diagonal lines, pressure drop on X-axis."
                },
                new()
                {
                    Number = 9, Title = "Final Check", Icon = "\u2705",
                    Description = "Verify that available pressure exceeds friction losses.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "final_diff", Label = "Pressure \u2212 Friction", Unit = "PSI", IsOutput = true,
                                Formula = "{pressure_available} - {friction_loss}", Highlight = "positive-negative" }
                    },
                    Tip = "Positive = design is acceptable. Negative = increase pipe size or add booster pump."
                }
            }
        });

        // Water Supply Calculation Worksheet Reference
        store.Sheets.Add(new CheatSheet
        {
            Id = "water-supply-calc-worksheet",
            Title = "Water Supply Calculation Worksheet",
            Subtitle = "Pressure Analysis Summary",
            Description = "Reference layout for the water supply calculation worksheet. Fill in values to determine if pressure is adequate or a booster pump is needed.",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "water supply", "calculation", "worksheet", "pressure", "booster pump", "meter loss", "RPZ", "friction" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Parameter", IsInputColumn = true },
                new() { Header = "Typical Value", IsOutputColumn = true },
                new() { Header = "Notes", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Service Pipe Size (in)", "1.5", "From design" },
                new() { "Distribution Hot Pipe (in)", "1.25", "From design" },
                new() { "Distribution Cold Pipe (in)", "1.25", "From design" },
                new() { "Max Pipe Length (ft)", "Measured", "Meter to farthest fixture" },
                new() { "Adjusted Length (ft)", "Length \u00d7 1.2", "Accounts for fittings" },
                new() { "Supply Pressure (PSI)", "Per building", "Regulated by building/utility" },
                new() { "Height of Building (ft)", "Measured", "Total building height" },
                new() { "Pressure Loss per Height (PSI)", "Height \u00f7 2.31", "0.433 PSI per ft of head" },
                new() { "Highest Fixture Pressure (PSI)", "40", "Can reduce to avoid booster pump" },
                new() { "Meter Loss (PSI)", "0 or 13", "0 for condo buildings" },
                new() { "RPZ / Backflow Preventer (PSI)", "0 or 13", "0 for condo w/ 2\" Watts RPZ" },
                new() { "Special Losses \u2014 Filters, RO (PSI)", "Varies", "Depends on filter type used" },
                new() { "TOTAL LOSSES (PSI)", "Sum of above", "All losses added together" },
                new() { "PRESSURE AVAILABLE (PSI)", "Supply \u2212 Losses", "Positive = OK, Negative = pump" }
            },
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "Pipe Sizes", Icon = "\U0001F527",
                    Description = "Enter pipe diameters from your design.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ws_service", Label = "Service Pipe", Unit = "in", Default = "1.5" },
                        new() { Id = "ws_hot", Label = "Distribution Hot", Unit = "in", Default = "1.25" },
                        new() { Id = "ws_cold", Label = "Distribution Cold", Unit = "in", Default = "1.25" }
                    }
                },
                new()
                {
                    Number = 2, Title = "Pipe Length", Icon = "\U0001F4CF",
                    Description = "Measure from meter to farthest fixture.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ws_max_length", Label = "Max Pipe Length", Unit = "ft", Default = "145" },
                        new() { Id = "ws_adj_length", Label = "Adjusted Length", Unit = "ft", IsOutput = true, Formula = "{ws_max_length} * 1.2" }
                    },
                    Tip = "Adjusted length = Max \u00d7 1.2 to account for fittings."
                },
                new()
                {
                    Number = 3, Title = "Supply & Building", Icon = "\U0001F3E2",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ws_supply", Label = "Supply Pressure", Unit = "PSI", Default = "55", Hint = "From building/utility" },
                        new() { Id = "ws_height", Label = "Building Height", Unit = "ft", Default = "32" },
                        new() { Id = "ws_loss_height", Label = "Pressure Loss / Height", Unit = "PSI", IsOutput = true, Formula = "{ws_height} / 2.31" }
                    },
                    Tip = "0.433 PSI per foot of head (height \u00f7 2.31)."
                },
                new()
                {
                    Number = 4, Title = "Other Losses", Icon = "\u26A1",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ws_fixture", Label = "Fixture Pressure", Unit = "PSI", Default = "40", Hint = "Can reduce to avoid pump" },
                        new() { Id = "ws_meter", Label = "Meter Loss", Unit = "PSI", Default = "0", Hint = "0 for condo, 13 typical" },
                        new() { Id = "ws_rpz", Label = "RPZ / Backflow", Unit = "PSI", Default = "0", Hint = "0 for condo w/ 2\" Watts" },
                        new() { Id = "ws_special", Label = "Special Losses", Unit = "PSI", Default = "2", Hint = "Filters, RO, etc." }
                    }
                },
                new()
                {
                    Number = 5, Title = "Results", Icon = "\U0001F4CA",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ws_total", Label = "TOTAL LOSSES", Unit = "PSI", IsOutput = true,
                                Formula = "{ws_loss_height} + {ws_fixture} + {ws_meter} + {ws_rpz} + {ws_special}" },
                        new() { Id = "ws_available", Label = "PRESSURE AVAILABLE", Unit = "PSI", IsOutput = true,
                                Formula = "{ws_supply} - {ws_total}", Highlight = "positive-negative" }
                    },
                    Tip = "Positive = adequate. Negative = booster pump needed."
                }
            }
        });

        // Friction Loss Worksheet Reference
        store.Sheets.Add(new CheatSheet
        {
            Id = "friction-loss-worksheet",
            Title = "Friction Loss Worksheet",
            Subtitle = "Per-Section Pipe Friction Analysis",
            Description = "Column reference for the friction loss calculation worksheet. Complete one row per pipe section to determine total friction losses and whether pressure is adequate.",
            Discipline = Discipline.Plumbing,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "ipc2021",
            Tags = new List<string> { "friction loss", "worksheet", "pipe", "pressure drop", "equivalent length", "fittings", "hydraulic" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Column", IsInputColumn = true },
                new() { Header = "Description", IsOutputColumn = true },
                new() { Header = "How to Find", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Section", "Pipe section label (e.g., A\u2013B)", "From riser diagram" },
                new() { "WSFU", "Fixture units for this section", "Sum fixtures on this section" },
                new() { "GPM", "Flow rate in gallons/min", "Convert WSFU via Table E103.3(3)" },
                new() { "Length (ft)", "Physical pipe length", "Measured from drawings" },
                new() { "Trial Pipe Size", "Pipe diameter (in)", "From sanitary calculation" },
                new() { "Equiv. Fittings", "Equivalent length of fittings", "Count bends, valves, tees" },
                new() { "Total Equiv. Length", "Length + Equiv. Fittings", "Physical + fitting lengths" },
                new() { "Friction Loss / 100 ft (PSI)", "Pressure drop per 100 ft", "From pressure drop chart (GPM + pipe size)" },
                new() { "Friction Loss in Equiv. Length", "Total Equiv. \u00d7 Col 8 \u00f7 100", "Actual friction loss for this section" },
                new() { "Excess Pressure (PSI)", "Available \u2212 Friction", "Final: positive = OK, negative = resize" }
            },
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "Section Flow", Icon = "\U0001F4A7",
                    Description = "Determine the flow for this pipe section.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "fl_wsfu", Label = "Section WSFU", Unit = "wsfu", Default = "44.2", Hint = "Sum fixtures on this section" },
                        new() { Id = "fl_gpm", Label = "Flow (GPM)", Unit = "gpm", Default = "27.7", Hint = "Convert WSFU via Table E103.3(3)" }
                    },
                    Tip = "Look up GPM from the \"WSFU to GPM\" sheet.",
                    Reference = "wsfu-to-gpm-demand"
                },
                new()
                {
                    Number = 2, Title = "Pipe Section", Icon = "\U0001F4CF",
                    Description = "Enter pipe length and size for this section.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "fl_length", Label = "Pipe Length", Unit = "ft", Default = "145" },
                        new() { Id = "fl_pipe", Label = "Trial Pipe Size", Unit = "in", Default = "1.25", Hint = "From sanitary calculation" }
                    }
                },
                new()
                {
                    Number = 3, Title = "Equivalent Fittings", Icon = "\U0001F527",
                    Description = "Account for bends, valves, and tees.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "fl_fittings", Label = "Equiv. Fittings", Unit = "ft", Default = "29", Hint = "From bends, valves, tees" },
                        new() { Id = "fl_total_equiv", Label = "Total Equiv. Length", Unit = "ft", IsOutput = true, Formula = "{fl_length} + {fl_fittings}" }
                    }
                },
                new()
                {
                    Number = 4, Title = "Friction Loss", Icon = "\U0001F4C9",
                    Description = "Read the pressure drop chart using GPM and pipe size.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "fl_per100", Label = "Loss per 100 ft", Unit = "PSI", Default = "0.052", Hint = "From pressure drop chart" },
                        new() { Id = "fl_loss", Label = "Section Friction Loss", Unit = "PSI", IsOutput = true, Formula = "{fl_total_equiv} * {fl_per100} / 100" }
                    },
                    Tip = "Pressure drop chart: GPM on Y-axis, pipe size on diagonal lines, drop per 100 ft on X-axis."
                },
                new()
                {
                    Number = 5, Title = "Final Check", Icon = "\u2705",
                    Description = "Compare available pressure against friction loss.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "fl_available", Label = "Pressure Available", Unit = "PSI", Default = "-0.85", Hint = "From Water Supply Calc" },
                        new() { Id = "fl_excess", Label = "Excess Pressure", Unit = "PSI", IsOutput = true,
                                Formula = "{fl_available} - {fl_loss}", Highlight = "positive-negative" }
                    },
                    Tip = "Positive = OK. Negative = increase pipe size or add booster pump.",
                    Reference = "water-supply-calc-worksheet"
                }
            }
        });
    }
}
