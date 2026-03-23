using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Electrical cheat sheets: Motor Overload Sizing, Motor Branch Circuit OCPD.
/// </summary>
internal static partial class CheatSheetElectricalDefaults
{
    private static void AddMotorProtectionSheets(CheatSheetDataStore store)
    {
        // ── NEC 430.32 — Motor Overload Protection ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "motor-overload",
            Title = "Motor Overload Protection",
            Subtitle = "NEC 430.32",
            Description = "Maximum overload device sizing for motors based on service factor and temperature rise. Enter motor FLA in Interactive mode to calculate max overload amps.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "motor", "overload", "protection", "430.32", "service factor",
                "OL", "FLA", "temperature rise", "heater", "starter"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Motor Condition", IsInputColumn = true },
                new() { Header = "Max OL", Unit = "% of FLA", IsOutputColumn = true },
                new() { Header = "Notes", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Service Factor \u2265 1.15", "125%", "Most common for general-purpose motors" },
                new() { "Temperature Rise \u2264 40\u00b0C", "125%", "Marked on nameplate" },
                new() { "All Other Motors", "115%", "When SF < 1.15 and no temp rise marking" }
            },
            Steps = new List<GuideStep>
            {
                new()
                {
                    Number = 1, Title = "Motor Nameplate Data", Icon = "\U0001F50C",
                    Description = "Enter the motor's nameplate full-load amperage and service factor.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ol_fla", Label = "Motor FLA", Unit = "A", Default = "28", Hint = "Nameplate full-load amps" },
                        new() { Id = "ol_sf", Label = "Service Factor", Default = "1.15", Hint = "Nameplate SF (typically 1.0 or 1.15)" }
                    },
                    Tip = "Most general-purpose motors have SF = 1.15. If no SF is marked, use 1.0."
                },
                new()
                {
                    Number = 2, Title = "Determine OL Percentage", Icon = "\U0001F4CA",
                    Description = "SF \u2265 1.15 \u2192 125%. SF < 1.15 \u2192 115%.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ol_pct_125", Label = "At 125%", Unit = "A", IsOutput = true,
                                Formula = "{ol_fla} * 1.25" },
                        new() { Id = "ol_pct_115", Label = "At 115%", Unit = "A", IsOutput = true,
                                Formula = "{ol_fla} * 1.15" }
                    },
                    Tip = "Use 125% if SF \u2265 1.15 or temp rise \u2264 40\u00b0C. Use 115% for all other motors."
                },
                new()
                {
                    Number = 3, Title = "Result", Icon = "\u2705",
                    Description = "Select the correct overload value based on your motor's service factor.",
                    Fields = new List<StepField>
                    {
                        new() { Id = "ol_result_125", Label = "Max OL (SF \u2265 1.15)", Unit = "A", IsOutput = true,
                                Formula = "{ol_fla} * 1.25" },
                        new() { Id = "ol_result_115", Label = "Max OL (SF < 1.15)", Unit = "A", IsOutput = true,
                                Formula = "{ol_fla} * 1.15" }
                    },
                    Tip = "If the calculated value does not correspond to a standard OL rating, the next higher standard rating is permitted per 430.32(A)(1)."
                }
            }
        });

        // ── NEC Table 430.52 — Motor Branch-Circuit Short-Circuit & Ground-Fault Protective Device ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "motor-branch-ocpd",
            Title = "Motor Branch Circuit OCPD",
            Subtitle = "NEC Table 430.52",
            Description = "Maximum rating or setting of motor branch-circuit short-circuit and ground-fault protective device, expressed as percentage of full-load current.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "motor", "branch circuit", "OCPD", "430.52", "fuse", "circuit breaker",
                "short circuit", "ground fault", "protective device", "FLA"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Motor Type", IsInputColumn = true },
                new() { Header = "Non-Time Delay Fuse", Unit = "% FLA", IsOutputColumn = true },
                new() { Header = "Dual Element Fuse", Unit = "% FLA", IsOutputColumn = true },
                new() { Header = "Instantaneous Trip CB", Unit = "% FLA", IsOutputColumn = true },
                new() { Header = "Inverse Time CB", Unit = "% FLA", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Single-Phase Motors", "300", "175", "800", "250" },
                new() { "AC Polyphase \u2014 Squirrel Cage (Design B)", "300", "175", "800", "250" },
                new() { "AC Polyphase \u2014 Squirrel Cage (Other)", "300", "175", "800", "250" },
                new() { "AC Polyphase \u2014 Design B Energy Efficient", "300", "175", "1100", "250" },
                new() { "Wound Rotor", "150", "150", "800", "150" },
                new() { "DC (Constant Voltage)", "150", "150", "250", "150" }
            },
            NoteContent =
                "NEC TABLE 430.52 NOTES:\n\n" +
                "\u2022 For certain exceptions, the next higher standard OCPD rating is permitted\n" +
                "  if the calculated value does not correspond to a standard size (NEC 430.52(C)(1) Ex. 1).\n\n" +
                "\u2022 Instantaneous-trip CBs are permitted ONLY as part of a listed combination\n" +
                "  motor controller (NEC 430.52(C)(3)).\n\n" +
                "\u2022 For Design B energy-efficient motors, the instantaneous trip CB percentage\n" +
                "  is increased to 1100% due to higher locked-rotor current.\n\n" +
                "\u2022 Torque motors: protective device shall not exceed 170% of motor\n" +
                "  nameplate current rating."
        });
    }
}
