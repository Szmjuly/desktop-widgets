using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Default cheat sheet data for the Electrical discipline.
/// </summary>
internal static class CheatSheetElectricalDefaults
{
    internal static void AddTo(CheatSheetDataStore store)
    {
        // 1) Motor FLA (NEC Table 430.248 single-phase)
        store.Sheets.Add(new CheatSheet
        {
            Id = "motor-fla-1ph",
            Title = "Motor FLA - Single Phase",
            Subtitle = "NEC Table 430.248",
            Description = "Full-Load Current in Amperes for Single-Phase AC Motors",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string> { "motor", "FLA", "full load", "ampere", "single phase", "430.248", "HP" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "HP", Unit = "HP", IsInputColumn = true },
                new() { Header = "115V", Unit = "A", IsOutputColumn = true },
                new() { Header = "200V", Unit = "A", IsOutputColumn = true },
                new() { Header = "208V", Unit = "A", IsOutputColumn = true },
                new() { Header = "230V", Unit = "A", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1/6", "4.4", "2.5", "2.4", "2.2" },
                new() { "1/4", "5.8", "3.3", "3.2", "2.9" },
                new() { "1/3", "7.2", "4.1", "4.0", "3.6" },
                new() { "1/2", "9.8", "5.6", "5.4", "4.9" },
                new() { "3/4", "13.8", "7.9", "7.6", "6.9" },
                new() { "1", "16", "9.2", "8.8", "8" },
                new() { "1-1/2", "20", "11.5", "11", "10" },
                new() { "2", "24", "13.8", "13.2", "12" },
                new() { "3", "34", "19.6", "18.7", "17" },
                new() { "5", "56", "32.2", "30.8", "28" },
                new() { "7-1/2", "80", "46", "44", "40" },
                new() { "10", "100", "57.5", "55", "50" }
            }
        });

        // Motor FLA (NEC Table 430.250 three-phase)
        store.Sheets.Add(new CheatSheet
        {
            Id = "motor-fla-3ph",
            Title = "Motor FLA - Three Phase",
            Subtitle = "NEC Table 430.250",
            Description = "Full-Load Current in Amperes for Three-Phase AC Motors",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string> { "motor", "FLA", "full load", "ampere", "three phase", "3-phase", "430.250", "HP" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "HP", Unit = "HP", IsInputColumn = true },
                new() { Header = "115V", Unit = "A", IsOutputColumn = true },
                new() { Header = "200V", Unit = "A", IsOutputColumn = true },
                new() { Header = "208V", Unit = "A", IsOutputColumn = true },
                new() { Header = "230V", Unit = "A", IsOutputColumn = true },
                new() { Header = "460V", Unit = "A", IsOutputColumn = true },
                new() { Header = "575V", Unit = "A", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1/2", "4.4", "2.5", "2.4", "2.2", "1.1", "0.9" },
                new() { "3/4", "6.4", "3.7", "3.5", "3.2", "1.6", "1.3" },
                new() { "1", "8.4", "4.8", "4.6", "4.2", "2.1", "1.7" },
                new() { "1-1/2", "12", "6.9", "6.6", "6", "3", "2.4" },
                new() { "2", "13.6", "7.8", "7.5", "6.8", "3.4", "2.7" },
                new() { "3", "\u2014", "11", "10.6", "9.6", "4.8", "3.9" },
                new() { "5", "\u2014", "17.5", "16.7", "15.2", "7.6", "6.1" },
                new() { "7-1/2", "\u2014", "25.3", "24.2", "22", "11", "9" },
                new() { "10", "\u2014", "32.2", "30.8", "28", "14", "11" },
                new() { "15", "\u2014", "48.3", "46.2", "42", "21", "17" },
                new() { "20", "\u2014", "62.1", "59.4", "54", "27", "22" },
                new() { "25", "\u2014", "78.2", "74.8", "68", "34", "27" },
                new() { "30", "\u2014", "92", "88", "80", "40", "32" },
                new() { "40", "\u2014", "120", "114", "104", "52", "41" },
                new() { "50", "\u2014", "150", "143", "130", "65", "52" },
                new() { "60", "\u2014", "177", "169", "154", "77", "62" },
                new() { "75", "\u2014", "221", "211", "192", "96", "77" },
                new() { "100", "\u2014", "285", "273", "248", "124", "99" },
                new() { "125", "\u2014", "359", "343", "312", "156", "125" },
                new() { "150", "\u2014", "414", "396", "360", "180", "144" },
                new() { "200", "\u2014", "552", "528", "480", "240", "192" }
            }
        });

        // 2) Transformer KVA Rating - Single Phase
        store.Sheets.Add(new CheatSheet
        {
            Id = "xfmr-kva-1ph",
            Title = "Transformer FLA - Single Phase",
            Subtitle = "Single-Phase Transformer Full-Load Amperes by KVA",
            Description = "Full-load current for single-phase transformers based on KVA rating and voltage",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Calculator,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string> { "transformer", "KVA", "FLA", "single phase", "ampere", "voltage" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "KVA", Unit = "kVA", IsInputColumn = true },
                new() { Header = "120V", Unit = "A", IsOutputColumn = true },
                new() { Header = "208V", Unit = "A", IsOutputColumn = true },
                new() { Header = "240V", Unit = "A", IsOutputColumn = true },
                new() { Header = "277V", Unit = "A", IsOutputColumn = true },
                new() { Header = "480V", Unit = "A", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1", "8.3", "4.8", "4.2", "3.6", "2.1" },
                new() { "1.5", "12.5", "7.2", "6.3", "5.4", "3.1" },
                new() { "2", "16.7", "9.6", "8.3", "7.2", "4.2" },
                new() { "3", "25.0", "14.4", "12.5", "10.8", "6.3" },
                new() { "5", "41.7", "24.0", "20.8", "18.1", "10.4" },
                new() { "7.5", "62.5", "36.1", "31.3", "27.1", "15.6" },
                new() { "10", "83.3", "48.1", "41.7", "36.1", "20.8" },
                new() { "15", "125.0", "72.1", "62.5", "54.2", "31.3" },
                new() { "25", "208.3", "120.2", "104.2", "90.3", "52.1" },
                new() { "37.5", "312.5", "180.3", "156.3", "135.4", "78.1" },
                new() { "50", "416.7", "240.4", "208.3", "180.5", "104.2" },
                new() { "75", "625.0", "360.6", "312.5", "270.8", "156.3" },
                new() { "100", "833.3", "480.8", "416.7", "361.0", "208.3" },
                new() { "150", "1250.0", "721.2", "625.0", "541.5", "312.5" },
                new() { "167", "1391.7", "802.9", "695.8", "602.9", "347.9" },
                new() { "200", "1666.7", "961.5", "833.3", "722.0", "416.7" },
                new() { "250", "2083.3", "1201.9", "1041.7", "902.5", "520.8" },
                new() { "333", "2775.0", "1601.4", "1387.5", "1202.2", "693.8" },
                new() { "500", "4166.7", "2403.8", "2083.3", "1805.1", "1041.7" }
            }
        });

        // Transformer KVA Rating - Three Phase
        store.Sheets.Add(new CheatSheet
        {
            Id = "xfmr-kva-3ph",
            Title = "Transformer FLA - Three Phase",
            Subtitle = "Three-Phase Transformer Full-Load Amperes by KVA",
            Description = "Full-load current for three-phase transformers based on KVA rating and voltage",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Calculator,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string> { "transformer", "KVA", "FLA", "three phase", "3-phase", "ampere", "voltage" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "KVA", Unit = "kVA", IsInputColumn = true },
                new() { Header = "208V", Unit = "A", IsOutputColumn = true },
                new() { Header = "240V", Unit = "A", IsOutputColumn = true },
                new() { Header = "480V", Unit = "A", IsOutputColumn = true },
                new() { Header = "600V", Unit = "A", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "15", "41.7", "36.1", "18.0", "14.4" },
                new() { "30", "83.3", "72.2", "36.1", "28.9" },
                new() { "45", "125.0", "108.3", "54.1", "43.3" },
                new() { "75", "208.2", "180.4", "90.2", "72.2" },
                new() { "112.5", "312.3", "270.6", "135.3", "108.3" },
                new() { "150", "416.4", "360.8", "180.4", "144.3" },
                new() { "225", "624.6", "541.3", "270.6", "216.5" },
                new() { "300", "832.8", "721.7", "360.8", "288.7" },
                new() { "500", "1388.0", "1202.8", "601.4", "481.1" },
                new() { "750", "2081.9", "1804.2", "902.1", "721.7" },
                new() { "1000", "2775.9", "2405.6", "1202.8", "962.3" },
                new() { "1500", "4163.8", "3608.4", "1804.2", "1443.4" },
                new() { "2000", "5551.8", "4811.3", "2405.6", "1924.5" },
                new() { "2500", "6939.7", "6014.1", "3007.0", "2405.6" }
            }
        });

        // 3) Feeder Schedule (OCPD -> Wire/Conduit)
        store.Sheets.Add(new CheatSheet
        {
            Id = "feeder-schedule",
            Title = "Branch Circuit / Feeder Schedule",
            Subtitle = "Copper conductors, THHN/THWN, 75\u00b0C terminations",
            Description = "Standard branch circuit and feeder conductor sizing based on OCPD rating",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nec2020",
            Tags = new List<string> { "feeder", "branch circuit", "OCPD", "wire", "conduit", "conductor", "schedule" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "OCPD (A)", Unit = "A", IsInputColumn = true },
                new() { Header = "Phase Conductors", IsOutputColumn = true },
                new() { Header = "Ground", IsOutputColumn = true },
                new() { Header = "Conduit", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "15", "2#14", "1#14G", "1/2\"C" },
                new() { "20", "2#12", "1#12G", "1/2\"C" },
                new() { "30", "2#10", "1#10G", "3/4\"C" },
                new() { "40", "2#8", "1#10G", "3/4\"C" },
                new() { "50", "2#6", "1#10G", "1\"C" },
                new() { "60", "2#6", "1#10G", "1\"C" },
                new() { "70", "2#4", "1#8G", "1\"C" },
                new() { "80", "2#4", "1#8G", "1-1/4\"C" },
                new() { "90", "2#3", "1#8G", "1-1/4\"C" },
                new() { "100", "2#3", "1#8G", "1-1/4\"C" },
                new() { "110", "2#1", "1#6G", "1-1/2\"C" },
                new() { "125", "2#1", "1#6G", "1-1/2\"C" },
                new() { "150", "2#1/0", "1#6G", "2\"C" },
                new() { "175", "2#2/0", "1#4G", "2\"C" },
                new() { "200", "2#3/0", "1#4G", "2\"C" },
                new() { "225", "2#4/0", "1#4G", "2-1/2\"C" },
                new() { "250", "2#250", "1#4G", "2-1/2\"C" },
                new() { "300", "2#350", "1#2G", "3\"C" },
                new() { "350", "2#350", "1#2G", "3\"C" },
                new() { "400", "2#500", "1#1G", "3\"C" }
            }
        });

        // Three-phase feeder variant
        store.Sheets.Add(new CheatSheet
        {
            Id = "feeder-schedule-3ph",
            Title = "Three-Phase Feeder Schedule",
            Subtitle = "Copper conductors, THHN/THWN, 75\u00b0C terminations",
            Description = "Standard three-phase feeder conductor sizing based on OCPD rating",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nec2020",
            Tags = new List<string> { "feeder", "three phase", "3-phase", "OCPD", "wire", "conduit", "conductor", "schedule" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "OCPD (A)", Unit = "A", IsInputColumn = true },
                new() { Header = "Phase Conductors", IsOutputColumn = true },
                new() { Header = "Ground", IsOutputColumn = true },
                new() { Header = "Conduit", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "15", "3#14", "1#14G", "1/2\"C" },
                new() { "20", "3#12", "1#12G", "3/4\"C" },
                new() { "30", "3#10", "1#10G", "3/4\"C" },
                new() { "40", "3#8", "1#10G", "1\"C" },
                new() { "50", "3#6", "1#10G", "1\"C" },
                new() { "60", "3#6", "1#10G", "1-1/4\"C" },
                new() { "70", "3#4", "1#8G", "1-1/4\"C" },
                new() { "80", "3#4", "1#8G", "1-1/4\"C" },
                new() { "90", "3#3", "1#8G", "1-1/4\"C" },
                new() { "100", "3#3", "1#8G", "1-1/2\"C" },
                new() { "110", "3#1", "1#6G", "1-1/2\"C" },
                new() { "125", "3#1", "1#6G", "2\"C" },
                new() { "150", "3#1/0", "1#6G", "2\"C" },
                new() { "175", "3#2/0", "1#4G", "2\"C" },
                new() { "200", "3#3/0", "1#4G", "2-1/2\"C" },
                new() { "225", "3#4/0", "1#4G", "2-1/2\"C" },
                new() { "250", "3#250", "1#4G", "3\"C" },
                new() { "300", "3#350", "1#2G", "3\"C" },
                new() { "350", "3#350", "1#2G", "3\"C" },
                new() { "400", "3#500", "1#1G", "3-1/2\"C" }
            }
        });

        // NEC Table 250.66 - GEC Sizing
        store.Sheets.Add(new CheatSheet
        {
            Id = "nec-250-66",
            Title = "GEC Sizing",
            Subtitle = "NEC Table 250.66",
            Description = "Grounding Electrode Conductor (GEC) sizing based on largest service-entrance conductor",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nec2020",
            Tags = new List<string> { "GEC", "grounding", "electrode", "conductor", "250.66", "service entrance" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Service Cu", Unit = "AWG/kcmil", IsInputColumn = true },
                new() { Header = "Service Al", Unit = "AWG/kcmil", IsInputColumn = true },
                new() { Header = "GEC Cu", Unit = "AWG/kcmil", IsOutputColumn = true },
                new() { Header = "GEC Al", Unit = "AWG/kcmil", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "2 or smaller", "1/0 or smaller", "8", "6" },
                new() { "1 or 1/0", "2/0 or 3/0", "6", "4" },
                new() { "2/0 or 3/0", "4/0 or 250", "4", "2" },
                new() { "Over 3/0 thru 350", "Over 250 thru 500", "2", "1/0" },
                new() { "Over 350 thru 600", "Over 500 thru 900", "1/0", "3/0" },
                new() { "Over 600 thru 1100", "Over 900 thru 1750", "2/0", "4/0" },
                new() { "Over 1100", "Over 1750", "3/0", "250" }
            }
        });

        // Service Conduit Schedule (15A-3000A)
        store.Sheets.Add(new CheatSheet
        {
            Id = "service-conduit-schedule",
            Title = "Service Conduit Schedule",
            Subtitle = "Service entrance conductors & conduit by ampacity",
            Description = "Service entrance conductor and conduit sizing from 15A to 3000A (NEC Article 230)",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nec2020",
            Tags = new List<string> { "service", "conduit", "schedule", "ampacity", "entrance", "230", "feeder" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Service (A)", Unit = "A", IsInputColumn = true },
                new() { Header = "Conductors", IsOutputColumn = true },
                new() { Header = "Ground", IsOutputColumn = true },
                new() { Header = "Conduit", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "15",   "4#12",                    "1#12G",        "1/2\"C" },
                new() { "20",   "4#10",                    "1#12G",        "3/4\"C" },
                new() { "30",   "4#10",                    "1#10G",        "3/4\"C" },
                new() { "60",   "4#6",                     "1#10G",        "1\"C" },
                new() { "100",  "4#3",                     "1#8G",         "1-1/4\"C" },
                new() { "125",  "4#1",                     "1#6G",         "1-1/2\"C" },
                new() { "150",  "4#1/0",                   "1#6G",         "2\"C" },
                new() { "200",  "4#3/0",                   "1#4G",         "2\"C" },
                new() { "225",  "4#4/0",                   "1#4G",         "2-1/2\"C" },
                new() { "250",  "4#250 KCMIL",             "1#4G",         "2-1/2\"C" },
                new() { "300",  "4#350 KCMIL",             "1#2G",         "3\"C" },
                new() { "350",  "4#350 KCMIL",             "1#2G",         "3\"C" },
                new() { "400",  "4#500 KCMIL",             "1#1G",         "3-1/2\"C" },
                new() { "600",  "2 SETS 4#350 KCMIL",      "1#500 KCMIL",  "(2)3\"C" },
                new() { "800",  "2 SETS 4#500 KCMIL",      "1#500 KCMIL",  "(2)3-1/2\"C" },
                new() { "1000", "3 SETS 4#500 KCMIL",      "1#500 KCMIL",  "(3)3-1/2\"C" },
                new() { "1200", "4 SETS 4#400 KCMIL",      "1#500 KCMIL",  "(4)3\"C" },
                new() { "1600", "4 SETS 4#500 KCMIL",      "1#500 KCMIL",  "(4)3-1/2\"C" },
                new() { "2000", "5 SETS 4#500 KCMIL",      "1#500 KCMIL",  "(5)4\"C" },
                new() { "2500", "6 SETS 4#500 KCMIL",      "1#500 KCMIL",  "(6)4\"C" },
                new() { "3000", "(8) SETS 4#500 KCMIL",    "1#500 KCMIL",  "(8)4\"C" }
            }
        });

        // NEC Table 250.122 - EGC Sizing
        store.Sheets.Add(new CheatSheet
        {
            Id = "nec-250-122",
            Title = "EGC Sizing",
            Subtitle = "NEC Table 250.122",
            Description = "Equipment Grounding Conductor (EGC) sizing based on OCPD rating",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "nec2020",
            Tags = new List<string> { "EGC", "equipment", "grounding", "conductor", "250.122", "OCPD" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "OCPD (A)", Unit = "A", IsInputColumn = true },
                new() { Header = "EGC Copper", Unit = "AWG/kcmil", IsOutputColumn = true },
                new() { Header = "EGC Aluminum", Unit = "AWG/kcmil", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "15", "14", "12" },
                new() { "20", "12", "10" },
                new() { "30", "10", "8" },
                new() { "40", "10", "8" },
                new() { "60", "10", "8" },
                new() { "100", "8", "6" },
                new() { "200", "6", "4" },
                new() { "300", "4", "2" },
                new() { "400", "3", "1" },
                new() { "500", "2", "1/0" },
                new() { "600", "1", "2/0" },
                new() { "800", "1/0", "3/0" },
                new() { "1000", "2/0", "4/0" },
                new() { "1200", "3/0", "250" },
                new() { "1600", "4/0", "350" },
                new() { "2000", "250", "400" },
                new() { "2500", "350", "600" },
                new() { "3000", "400", "600" },
                new() { "4000", "500", "750" },
                new() { "5000", "700", "1200" },
                new() { "6000", "800", "1200" }
            }
        });

        // Interior Lighting Power Allowances (FBC Energy Table C405.3.2(2))
        store.Sheets.Add(new CheatSheet
        {
            Id = "lighting-power-allowances",
            Title = "Interior Lighting Power Allowances",
            Subtitle = "FBC Energy Table C405.3.2(2) \u2014 Space-by-Space",
            Description = "Interior Lighting Power Density (LPD) allowances by space type",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "fbc-energy-2023",
            Tags = new List<string> { "lighting", "LPD", "watts", "power density", "C405.3.2", "space-by-space", "energy" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Space Type", IsInputColumn = true },
                new() { Header = "LPD", Unit = "W/ft\u00b2", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Atrium \u2014 Less than 40 ft height", "0.48" },
                new() { "Atrium \u2014 Greater than 40 ft height", "0.60" },
                new() { "Audience seating \u2014 Auditorium", "0.61" },
                new() { "Audience seating \u2014 Gymnasium", "0.23" },
                new() { "Audience seating \u2014 Motion picture theater", "0.27" },
                new() { "Audience seating \u2014 Penitentiary", "0.67" },
                new() { "Audience seating \u2014 Performing arts theater", "1.16" },
                new() { "Audience seating \u2014 Religious building", "0.72" },
                new() { "Audience seating \u2014 Sports arena", "0.33" },
                new() { "Audience seating \u2014 Otherwise", "0.23" },
                new() { "Banking activity area", "0.61" },
                new() { "Classroom/lecture/training \u2014 Penitentiary", "0.89" },
                new() { "Classroom/lecture/training \u2014 Otherwise", "0.71" },
                new() { "Computer room", "0.94" },
                new() { "Conference/meeting/multipurpose room", "0.97" },
                new() { "Copy/print room", "0.31" },
                new() { "Corridor \u2014 Visually impaired facility", "0.71" },
                new() { "Corridor \u2014 Hospital", "0.71" },
                new() { "Corridor \u2014 Otherwise", "0.41" },
                new() { "Courtroom", "1.20" },
                new() { "Dining \u2014 Bar/lounge or leisure", "0.86" },
                new() { "Dining \u2014 Cafeteria or fast food", "0.40" },
                new() { "Dining \u2014 Visually impaired facility", "1.27" },
                new() { "Dining \u2014 Family dining", "0.60" },
                new() { "Dining \u2014 Penitentiary", "0.42" },
                new() { "Dining \u2014 Otherwise", "0.43" },
                new() { "Electrical/mechanical room", "0.43" },
                new() { "Emergency vehicle garage", "0.52" },
                new() { "Food preparation area", "1.09" },
                new() { "Guest room", "0.41" },
                new() { "Laboratory \u2014 In/as classroom", "1.11" },
                new() { "Laboratory \u2014 Otherwise", "1.33" },
                new() { "Laundry/washing area", "0.53" },
                new() { "Loading dock, interior", "0.88" },
                new() { "Lobby \u2014 Elevator", "0.65" },
                new() { "Lobby \u2014 Visually impaired facility", "1.69" },
                new() { "Lobby \u2014 Hotel", "0.51" },
                new() { "Lobby \u2014 Motion picture theater", "0.23" },
                new() { "Lobby \u2014 Performing arts theater", "1.25" },
                new() { "Lobby \u2014 Otherwise", "0.84" },
                new() { "Locker room", "0.52" },
                new() { "Lounge/breakroom \u2014 Healthcare facility", "0.42" },
                new() { "Lounge/breakroom \u2014 Otherwise", "0.59" },
                new() { "Office \u2014 Enclosed", "0.74" },
                new() { "Office \u2014 Open plan", "0.61" },
                new() { "Parking area, interior", "0.15" },
                new() { "Pharmacy area", "1.66" },
                new() { "Restroom \u2014 Visually impaired facility", "1.26" },
                new() { "Restroom \u2014 Otherwise", "0.63" },
                new() { "Sales area", "1.05" },
                new() { "Seating area, general", "0.23" },
                new() { "Stairwell", "0.49" },
                new() { "Storage room", "0.38" },
                new() { "Vehicular maintenance area", "0.60" },
                new() { "Workshop", "1.26" },
                new() { "Convention center \u2014 Exhibit space", "0.61" },
                new() { "Dormitory \u2014 Living quarters", "0.50" },
                new() { "Visually impaired \u2014 Chapel", "0.70" },
                new() { "Visually impaired \u2014 Recreation room", "1.77" },
                new() { "Fire station \u2014 Sleeping quarters", "0.23" },
                new() { "Gym/fitness \u2014 Exercise area", "0.90" },
                new() { "Gym/fitness \u2014 Playing area", "0.85" },
                new() { "Healthcare \u2014 Exam/treatment room", "1.40" },
                new() { "Healthcare \u2014 Imaging room", "0.94" },
                new() { "Healthcare \u2014 Medical supply room", "0.62" },
                new() { "Healthcare \u2014 Nursery", "0.92" },
                new() { "Healthcare \u2014 Nurse's station", "1.17" },
                new() { "Healthcare \u2014 Operating room", "2.26" },
                new() { "Healthcare \u2014 Patient room", "0.68" },
                new() { "Healthcare \u2014 Physical therapy room", "0.91" },
                new() { "Healthcare \u2014 Recovery room", "1.25" },
                new() { "Library \u2014 Reading area", "0.96" },
                new() { "Library \u2014 Stacks", "1.18" },
                new() { "Manufacturing \u2014 Detailed manufacturing", "0.80" },
                new() { "Manufacturing \u2014 Equipment room", "0.76" },
                new() { "Manufacturing \u2014 Extra high bay (>50')", "1.42" },
                new() { "Manufacturing \u2014 High bay (25-50')", "1.24" },
                new() { "Manufacturing \u2014 Low bay (<25')", "0.86" },
                new() { "Museum \u2014 General exhibition", "0.31" },
                new() { "Museum \u2014 Restoration room", "1.10" },
                new() { "Performing arts \u2014 Dressing room", "0.41" },
                new() { "Post office \u2014 Sorting area", "0.76" },
                new() { "Religious \u2014 Fellowship hall", "0.54" },
                new() { "Religious \u2014 Worship/pulpit/choir", "0.85" },
                new() { "Retail \u2014 Dressing/fitting room", "0.51" },
                new() { "Retail \u2014 Mall concourse", "0.82" },
                new() { "Sports arena \u2014 Class I facility", "2.94" },
                new() { "Sports arena \u2014 Class II facility", "2.01" },
                new() { "Sports arena \u2014 Class III facility", "1.30" },
                new() { "Sports arena \u2014 Class IV facility", "0.86" },
                new() { "Transportation \u2014 Baggage/carousel", "0.39" },
                new() { "Transportation \u2014 Airport concourse", "0.25" },
                new() { "Transportation \u2014 Terminal ticket counter", "0.51" },
                new() { "Warehouse \u2014 Medium/bulky palletized", "0.33" },
                new() { "Warehouse \u2014 Smaller hand-carried", "0.69" }
            }
        });

        // Miami-Dade specific keynote
        store.Sheets.Add(new CheatSheet
        {
            Id = "miami-dade-lv-keynote",
            Title = "Miami-Dade LV Wiring Keynote",
            Subtitle = "Local Amendment - Low Voltage Wiring",
            Description = "Special keynote for low-voltage wiring installations in Miami-Dade County",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nec2020",
            JurisdictionId = "fl-miami-dade",
            Tags = new List<string> { "Miami", "Miami-Dade", "LV", "low voltage", "keynote", "local", "amendment" },
            NoteContent = "Per Miami-Dade County local amendments:\n\n" +
                          "\u2022 All low-voltage wiring shall be installed in accordance with local building department requirements.\n" +
                          "\u2022 LV wiring keynote shall be included on electrical drawings.\n" +
                          "\u2022 Contractor to verify specific requirements with the Authority Having Jurisdiction (AHJ).\n\n" +
                          "Reference: Miami-Dade County amendments to FBC 2023."
        });
    }
}
