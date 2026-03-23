using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Electrical cheat sheets: Voltage Drop reference table, Voltage Drop interactive calculator,
/// Residential Service Entrance sizing calculator, Short Circuit Current calculator.
/// </summary>
internal static partial class CheatSheetElectricalDefaults
{
    private static void AddCalculatorSheets(CheatSheetDataStore store)
    {
        // ── Conductor Resistance — NEC Chapter 9, Table 8 (DC Resistance) ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "voltage-drop-table",
            Title = "Conductor Resistance",
            Subtitle = "NEC Chapter 9, Table 8",
            Description = "DC resistance of uncoated copper and aluminum conductors at 75\u00b0C. Used for voltage drop calculations. For AC circuits, these values are sufficiently accurate for sizes up to 1/0.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "voltage drop", "resistance", "ohm", "Chapter 9", "Table 8",
                "conductor", "copper", "aluminum", "DC", "impedance"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Size", Unit = "AWG/kcmil", IsInputColumn = true },
                new() { Header = "Cu Resistance", Unit = "\u03a9/1000ft", IsOutputColumn = true },
                new() { Header = "Al Resistance", Unit = "\u03a9/1000ft", IsOutputColumn = true },
                new() { Header = "Area", Unit = "cmil", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "14", "3.14", "5.17", "4,110" },
                new() { "12", "1.98", "3.25", "6,530" },
                new() { "10", "1.24", "2.04", "10,380" },
                new() { "8", "0.778", "1.28", "16,510" },
                new() { "6", "0.491", "0.808", "26,240" },
                new() { "4", "0.308", "0.508", "41,740" },
                new() { "3", "0.245", "0.403", "52,620" },
                new() { "2", "0.194", "0.319", "66,360" },
                new() { "1", "0.154", "0.253", "83,690" },
                new() { "1/0", "0.122", "0.201", "105,600" },
                new() { "2/0", "0.0967", "0.159", "133,100" },
                new() { "3/0", "0.0766", "0.126", "167,800" },
                new() { "4/0", "0.0608", "0.100", "211,600" },
                new() { "250", "0.0515", "0.0847", "250,000" },
                new() { "300", "0.0429", "0.0707", "300,000" },
                new() { "350", "0.0367", "0.0605", "350,000" },
                new() { "400", "0.0321", "0.0529", "400,000" },
                new() { "500", "0.0258", "0.0424", "500,000" },
                new() { "600", "0.0214", "0.0353", "600,000" },
                new() { "700", "0.0184", "0.0303", "700,000" },
                new() { "750", "0.0171", "0.0282", "750,000" },
                new() { "800", "0.0161", "0.0265", "800,000" },
                new() { "900", "0.0143", "0.0235", "900,000" },
                new() { "1000", "0.0129", "0.0212", "1,000,000" }
            }
        });

        // ── Voltage Drop Calculator (Interactive) ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "voltage-drop-calc",
            Title = "Voltage Drop Calculator",
            Subtitle = "NEC 210.19(A) Informational Note",
            Description = "Step-by-step voltage drop calculation for branch circuits and feeders. NEC recommends \u2264 3% for branch circuits, \u2264 5% total (feeder + branch).",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "voltage drop", "VD", "calculator", "percent", "branch circuit",
                "feeder", "conductor", "wire size", "length", "210.19"
            },
            NoteContent =
                "VOLTAGE DROP CALCULATION\n\n" +
                "SINGLE-PHASE:\n" +
                "  VD = (2 \u00d7 L \u00d7 R \u00d7 I) / 1000\n\n" +
                "THREE-PHASE:\n" +
                "  VD = (\u221a3 \u00d7 L \u00d7 R \u00d7 I) / 1000\n\n" +
                "Where:\n" +
                "  VD = Voltage drop (volts)\n" +
                "  L  = One-way length of conductor (feet)\n" +
                "  R  = Conductor resistance (\u03a9/1000 ft) from NEC Ch9 Table 8\n" +
                "  I  = Current (amperes)\n\n" +
                "  %VD = (VD / V\u209b\u2092\u1d64\u1d63\u1d9c\u1d49) \u00d7 100\n\n" +
                "NEC RECOMMENDATIONS (Informational Note):\n" +
                "\u2022 Branch circuit: \u2264 3% voltage drop\n" +
                "\u2022 Feeder: \u2264 3% voltage drop\n" +
                "\u2022 Total (feeder + branch): \u2264 5% voltage drop\n\n" +
                "ALTERNATIVE FORMULA (using cmil):\n" +
                "  VD = (2 \u00d7 K \u00d7 L \u00d7 I) / cmil\n" +
                "  K = 12.9 for copper, 21.2 for aluminum (at 75\u00b0C)",
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "System Parameters", Icon = "\u26A1",
                    Description = "Enter the system voltage. Select single-phase or three-phase using the multiplier.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "vd_voltage", Label = "System Voltage", Unit = "V", Default = "208", Hint = "120, 208, 240, 277, 480" },
                        new() { Id = "vd_phase_mult", Label = "Phase Multiplier", Default = "1.732", Hint = "2.0 for 1\u00d8, 1.732 for 3\u00d8" }
                    },
                    Tip = "Use 2.0 for single-phase circuits, 1.732 (\u221a3) for three-phase circuits."
                },
                new()
                {
                    Number = 2, Title = "Load Current", Icon = "\U0001F50C",
                    Description = "Enter the load current in amperes.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "vd_current", Label = "Current", Unit = "A", Default = "40", Hint = "Load current or circuit rating" }
                    }
                },
                new()
                {
                    Number = 3, Title = "Conductor", Icon = "\U0001F527",
                    Description = "Enter the conductor resistance from NEC Chapter 9, Table 8.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "vd_resistance", Label = "Resistance", Unit = "\u03a9/1000ft", Default = "0.308", Hint = "#4 Cu = 0.308, #2 Cu = 0.194" }
                    },
                    Tip = "See the \"Conductor Resistance\" sheet for values by wire size.",
                    Reference = "voltage-drop-table"
                },
                new()
                {
                    Number = 4, Title = "Distance", Icon = "\U0001F4CF",
                    Description = "Enter the one-way conductor length from source to load.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "vd_length", Label = "One-Way Length", Unit = "ft", Default = "150", Hint = "Source to load, one direction" }
                    }
                },
                new()
                {
                    Number = 5, Title = "Results", Icon = "\U0001F4CA",
                    Description = "Voltage drop and percentage. NEC recommends \u2264 3% for branch, \u2264 5% total.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "vd_drop", Label = "Voltage Drop", Unit = "V", IsOutput = true,
                                Formula = "{vd_phase_mult} * {vd_length} * {vd_resistance} * {vd_current} / 1000" },
                        new() { Id = "vd_pct", Label = "% Voltage Drop", Unit = "%", IsOutput = true,
                                Formula = "{vd_drop} / {vd_voltage} * 100", Highlight = "positive-negative" }
                    },
                    Tip = "\u2264 3% = branch OK. \u2264 5% = total OK. > 5% = increase wire size or reduce length."
                }
            }
        });

        // ── Residential Service Entrance Sizing (Interactive) — NEC Article 220 ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "service-entrance-calc",
            Title = "Residential Service Sizing",
            Subtitle = "NEC Article 220 \u2014 Standard Method",
            Description = "Step-by-step residential service load calculation using the NEC standard method. Determines the minimum service entrance size for a dwelling unit.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "service entrance", "residential", "load calculation", "220.12",
                "demand", "NEC Article 220", "dwelling", "panel", "service size"
            },
            NoteContent =
                "RESIDENTIAL SERVICE LOAD CALCULATION \u2014 STANDARD METHOD\n\n" +
                "NEC ARTICLE 220, Part III\n\n" +
                "1. GENERAL LIGHTING: Floor area (ft\u00b2) \u00d7 3 VA/ft\u00b2 (Table 220.12)\n" +
                "2. SMALL APPLIANCE: 2 circuits \u00d7 1,500 VA = 3,000 VA (220.52(A))\n" +
                "3. LAUNDRY: 1 circuit \u00d7 1,500 VA (220.52(B))\n" +
                "4. SUBTOTAL: Add lines 1 + 2 + 3\n" +
                "5. DEMAND: First 3,000 VA at 100% + remainder at 35% (Table 220.42)\n" +
                "6. FIXED APPLIANCES: Sum nameplate ratings;\n" +
                "   if 4 or more, apply 75% demand factor (220.53)\n" +
                "7. DRYER: 5,000 W or nameplate, whichever is larger (220.54)\n" +
                "8. RANGE: Table 220.55 \u2014 8 kW for one range \u2264 12 kW\n" +
                "9. A/C vs HEAT: Use larger of the two (220.60)\n" +
                "   A/C at 100%, heat at 100% (non-coincident loads)\n" +
                "10. TOTAL DEMAND: Sum of lines 5 through 9\n" +
                "11. SERVICE AMPS: Total demand \u00f7 240V\n" +
                "12. SERVICE SIZE: Next standard size up (100A, 125A, 150A, 200A, 400A)",
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "General Lighting", Icon = "\U0001F4A1",
                    Description = "Calculate lighting load from floor area per NEC 220.12.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "se_sqft", Label = "Floor Area", Unit = "ft\u00b2", Default = "2000", Hint = "Total living area (not garage)" },
                        new() { Id = "se_lighting", Label = "Lighting Load", Unit = "VA", IsOutput = true,
                                Formula = "{se_sqft} * 3" }
                    },
                    Tip = "Use 3 VA/ft\u00b2 for dwelling units (NEC Table 220.12). Include all habitable space."
                },
                new()
                {
                    Number = 2, Title = "Small Appliance & Laundry", Icon = "\U0001F50C",
                    Description = "Add required small appliance and laundry branch circuit loads.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "se_small_appl", Label = "Small Appliance (2 ckt)", Unit = "VA", Default = "3000", Hint = "2 circuits \u00d7 1,500 VA = 3,000 VA" },
                        new() { Id = "se_laundry", Label = "Laundry (1 ckt)", Unit = "VA", Default = "1500", Hint = "1 circuit \u00d7 1,500 VA" }
                    }
                },
                new()
                {
                    Number = 3, Title = "Apply Demand Factor", Icon = "\U0001F4CA",
                    Description = "First 3,000 VA at 100%, remainder at 35% (NEC Table 220.42).",
                    Fields = new List<StepField>
                    {
                        new() { Id = "se_subtotal", Label = "Subtotal", Unit = "VA", IsOutput = true,
                                Formula = "{se_lighting} + {se_small_appl} + {se_laundry}" },
                        new() { Id = "se_demand_lighting", Label = "After Demand Factor", Unit = "VA", IsOutput = true,
                                Formula = "3000 + ({se_subtotal} - 3000) * 0.35" }
                    },
                    Tip = "This demand factor only applies to the general lighting + small appliance + laundry subtotal."
                },
                new()
                {
                    Number = 4, Title = "Fixed Appliances", Icon = "\U0001F527",
                    Description = "Sum nameplate ratings of fixed appliances. If 4+ appliances, apply 75%.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "se_dishwasher", Label = "Dishwasher", Unit = "W", Default = "1200" },
                        new() { Id = "se_disposal", Label = "Disposal", Unit = "W", Default = "900" },
                        new() { Id = "se_water_heater", Label = "Water Heater", Unit = "W", Default = "4500" },
                        new() { Id = "se_fixed_total", Label = "Fixed Appl. Total", Unit = "W", IsOutput = true,
                                Formula = "{se_dishwasher} + {se_disposal} + {se_water_heater}" },
                        new() { Id = "se_fixed_demand", Label = "At 75% (4+ appl.)", Unit = "W", IsOutput = true,
                                Formula = "{se_fixed_total} * 0.75" }
                    },
                    Tip = "75% demand applies when there are 4 or more fixed appliances (NEC 220.53). Use 100% if fewer than 4."
                },
                new()
                {
                    Number = 5, Title = "Major Appliances", Icon = "\U0001F373",
                    Description = "Dryer and range demand per NEC 220.54 and 220.55.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "se_dryer", Label = "Dryer", Unit = "W", Default = "5000", Hint = "5,000W or nameplate, whichever is larger" },
                        new() { Id = "se_range", Label = "Range/Oven", Unit = "W", Default = "8000", Hint = "8 kW for one range \u2264 12 kW (Table 220.55)" }
                    },
                    Tip = "Dryer: use 5,000W minimum. Range: use Table 220.55 Column A (8 kW for 1 range \u2264 12 kW)."
                },
                new()
                {
                    Number = 6, Title = "A/C vs Heat", Icon = "\u2744\uFE0F",
                    Description = "Use the LARGER of air conditioning or heating (non-coincident, NEC 220.60).",
                    Fields = new List<StepField>
                    {
                        new() { Id = "se_ac", Label = "A/C Load", Unit = "W", Default = "5000", Hint = "Compressor + air handler" },
                        new() { Id = "se_heat", Label = "Heat Load", Unit = "W", Default = "10000", Hint = "Strip heat or heat pump aux" }
                    },
                    Tip = "Use whichever is LARGER \u2014 A/C and heat are non-coincident loads (NEC 220.60)."
                },
                new()
                {
                    Number = 7, Title = "Total Demand & Service Size", Icon = "\u2705",
                    Description = "Sum all demand loads and determine the minimum service size.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "se_larger_hvac", Label = "Larger HVAC Load", Unit = "W", IsOutput = true,
                                Formula = "{se_ac} + ({se_heat} - {se_ac}) * ({se_heat} - {se_ac}) / (({se_heat} - {se_ac}) * ({se_heat} - {se_ac}) + 0.0001) * ({se_heat} - {se_ac})" },
                        new() { Id = "se_total_demand", Label = "Total Demand", Unit = "VA", IsOutput = true,
                                Formula = "{se_demand_lighting} + {se_fixed_demand} + {se_dryer} + {se_range} + {se_heat}" },
                        new() { Id = "se_service_amps", Label = "Service Amps", Unit = "A", IsOutput = true,
                                Formula = "{se_total_demand} / 240", Highlight = "positive-negative" }
                    },
                    Tip = "Round up to the next standard service size: 100A, 125A, 150A, 200A, or 400A. Most modern homes require 200A."
                }
            }
        });

        // ── Short Circuit Current Calculator (Interactive) ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "short-circuit-calc",
            Title = "Short Circuit Current Calculator",
            Subtitle = "Available Fault Current at Panel",
            Description = "Estimate the available short-circuit current (fault current) at a downstream panel. Used to verify that equipment AIC/AIR ratings are adequate.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "short circuit", "fault current", "AIC", "AIR", "impedance",
                "transformer", "available", "interrupt", "SCCR", "withstand"
            },
            NoteContent =
                "SHORT CIRCUIT CURRENT CALCULATION\n\n" +
                "TRANSFORMER SECONDARY FAULT CURRENT:\n" +
                "  I\u209b\u1d9c = (KVA \u00d7 1000) / (V\u209b\u1d49\u1d9c \u00d7 \u221a3 \u00d7 Z%)\n\n" +
                "  For single-phase:\n" +
                "  I\u209b\u1d9c = (KVA \u00d7 1000) / (V\u209b\u1d49\u1d9c \u00d7 Z%)\n\n" +
                "Where:\n" +
                "  KVA  = Transformer kVA rating\n" +
                "  V    = Secondary voltage (line-to-line)\n" +
                "  Z%   = Transformer impedance (decimal, e.g., 0.0575 for 5.75%)\n" +
                "  I\u209b\u1d9c  = Short-circuit current (amperes)\n\n" +
                "CONDUCTOR IMPEDANCE ADJUSTMENT:\n" +
                "  The fault current at a downstream panel is reduced by conductor impedance.\n" +
                "  A simplified point-to-point method:\n\n" +
                "  f = (\u221a3 \u00d7 L \u00d7 I\u209b\u1d9c) / (C \u00d7 V)\n" +
                "  I\u209b\u1d9c(downstream) = I\u209b\u1d9c / (1 + f)\n\n" +
                "  C = 22,185 for 3\u00d8, Cu in steel conduit\n" +
                "  C = 14,558 for 3\u00d8, Al in steel conduit\n" +
                "  L = Length of conductor (ft)\n\n" +
                "EQUIPMENT AIC RATING:\n" +
                "\u2022 Standard residential panels: typically 10 kAIC or 22 kAIC\n" +
                "\u2022 Commercial panels: 14 kAIC, 22 kAIC, 42 kAIC, 65 kAIC\n" +
                "\u2022 Equipment must have AIC \u2265 available fault current\n" +
                "\u2022 NEC 110.9 requires equipment to interrupt available fault current\n" +
                "\u2022 NEC 110.10 requires equipment to withstand available fault current",
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "Transformer Data", Icon = "\u26A1",
                    Description = "Enter transformer nameplate data.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "sc_kva", Label = "Transformer KVA", Unit = "kVA", Default = "500", Hint = "Nameplate kVA rating" },
                        new() { Id = "sc_voltage", Label = "Secondary Voltage", Unit = "V", Default = "208", Hint = "Line-to-line secondary" },
                        new() { Id = "sc_impedance", Label = "Impedance (%Z)", Default = "5.75", Hint = "Nameplate impedance as %" }
                    },
                    Tip = "Typical impedance: 2\u20133% for \u2264 150 kVA, 5.75% for 225\u20131000 kVA."
                },
                new()
                {
                    Number = 2, Title = "Transformer Fault Current", Icon = "\U0001F4CA",
                    Description = "Calculate available short-circuit current at transformer secondary.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "sc_z_decimal", Label = "Z (decimal)", IsOutput = true,
                                Formula = "{sc_impedance} / 100" },
                        new() { Id = "sc_isc_xfmr", Label = "I\u209b\u1d9c at Transformer", Unit = "A", IsOutput = true,
                                Formula = "{sc_kva} * 1000 / ({sc_voltage} * 1.732 * {sc_z_decimal})" }
                    },
                    Tip = "This is the MAXIMUM available fault current, assuming infinite utility bus."
                },
                new()
                {
                    Number = 3, Title = "Conductor to Panel", Icon = "\U0001F527",
                    Description = "Enter conductor details from transformer to downstream panel.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "sc_length", Label = "Conductor Length", Unit = "ft", Default = "100", Hint = "One-way distance" },
                        new() { Id = "sc_c_factor", Label = "C Factor", Default = "22185", Hint = "22185 = 3\u00d8 Cu/steel; 14558 = 3\u00d8 Al/steel" }
                    },
                    Tip = "C = 22,185 for 3\u00d8 copper in steel conduit. See Bussmann or Cooper tables for other configurations."
                },
                new()
                {
                    Number = 4, Title = "Fault Current at Panel", Icon = "\U0001F4C9",
                    Description = "Calculate the available fault current at the downstream equipment.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "sc_f_factor", Label = "f Factor", IsOutput = true,
                                Formula = "1.732 * {sc_length} * {sc_isc_xfmr} / ({sc_c_factor} * {sc_voltage})" },
                        new() { Id = "sc_isc_panel", Label = "I\u209b\u1d9c at Panel", Unit = "A", IsOutput = true,
                                Formula = "{sc_isc_xfmr} / (1 + {sc_f_factor})" },
                        new() { Id = "sc_isc_ka", Label = "Available Fault Current", Unit = "kA", IsOutput = true,
                                Formula = "{sc_isc_panel} / 1000", Highlight = "positive-negative" }
                    },
                    Tip = "Equipment AIC rating must be \u2265 this value. Standard ratings: 10 kA, 14 kA, 22 kA, 42 kA, 65 kA."
                }
            }
        });
    }
}
