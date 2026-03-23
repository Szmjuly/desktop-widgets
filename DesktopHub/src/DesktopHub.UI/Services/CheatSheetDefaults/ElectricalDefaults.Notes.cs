using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Electrical cheat sheets: AFCI/GFCI Requirements, Electrical Room Clearances.
/// </summary>
internal static partial class CheatSheetElectricalDefaults
{
    private static void AddNoteSheets(CheatSheetDataStore store)
    {
        // ── AFCI / GFCI Requirements — NEC 210.12 & 210.8 ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "afci-gfci-requirements",
            Title = "AFCI / GFCI Requirements",
            Subtitle = "NEC 210.12 & 210.8",
            Description = "Summary of arc-fault and ground-fault circuit-interrupter protection requirements by location and occupancy type.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "AFCI", "GFCI", "arc fault", "ground fault", "210.12", "210.8",
                "protection", "dwelling", "receptacle", "bathroom", "kitchen",
                "bedroom", "garage", "outdoor", "laundry"
            },
            NoteContent =
                "AFCI PROTECTION \u2014 NEC 210.12\n\n" +

                "Required in DWELLING UNITS for 120V, 15A and 20A branch circuits supplying:\n" +
                "\u2022 Kitchens\n" +
                "\u2022 Family rooms / Living rooms / Parlors\n" +
                "\u2022 Dining rooms\n" +
                "\u2022 Bedrooms / Dens / Libraries / Sunrooms\n" +
                "\u2022 Recreation rooms / Closets\n" +
                "\u2022 Hallways / Foyers / Similar rooms or areas\n" +
                "\u2022 Laundry areas\n\n" +

                "AFCI Types:\n" +
                "\u2022 AFCI breaker (most common \u2014 combination type)\n" +
                "\u2022 Outlet branch-circuit type AFCI at first outlet\n" +
                "\u2022 Dual-function AFCI/GFCI breaker (where both are required)\n\n" +

                "EXCEPTIONS:\n" +
                "\u2022 Fire alarm circuits\n" +
                "\u2022 Circuits in spaces not readily accessible (attics, crawl spaces in certain conditions)\n\n" +

                "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n\n" +

                "GFCI PROTECTION \u2014 NEC 210.8\n\n" +

                "DWELLING UNITS \u2014 210.8(A) \u2014 125V, 15A and 20A receptacles:\n" +
                "\u2022 Bathrooms\n" +
                "\u2022 Garages and accessory buildings with floors at or below grade\n" +
                "\u2022 Outdoors (all)\n" +
                "\u2022 Crawl spaces \u2014 at or below grade\n" +
                "\u2022 Unfinished basements\n" +
                "\u2022 Kitchens \u2014 all receptacles serving countertop surfaces\n" +
                "\u2022 Within 6 ft of sinks (laundry, utility, wet bar)\n" +
                "\u2022 Boathouses\n" +
                "\u2022 Bathtub/shower stall areas (within 6 ft)\n" +
                "\u2022 Laundry areas\n" +
                "\u2022 Indoor damp/wet locations\n\n" +

                "NON-DWELLING \u2014 210.8(B) \u2014 125V through 250V receptacles:\n" +
                "\u2022 Bathrooms\n" +
                "\u2022 Kitchens\n" +
                "\u2022 Rooftops\n" +
                "\u2022 Outdoors (all)\n" +
                "\u2022 Within 6 ft of sinks\n" +
                "\u2022 Indoor wet/damp locations\n" +
                "\u2022 Locker rooms with shower facilities\n" +
                "\u2022 Garages, service bays, similar areas\n" +
                "\u2022 Crawl spaces \u2014 at or below grade\n" +
                "\u2022 Unfinished basements\n" +
                "\u2022 Boat hoists\n" +
                "\u2022 Dishwashers (dwelling unit, 2020 NEC)\n\n" +

                "GFCI FOR SPECIFIC EQUIPMENT (any occupancy):\n" +
                "\u2022 Electric vehicle charging (EVSE) per manufacturer\n" +
                "\u2022 Pool equipment \u2014 NEC 680\n" +
                "\u2022 Hot tubs / Spas \u2014 NEC 680\n" +
                "\u2022 Fountains \u2014 NEC 680\n" +
                "\u2022 Vending machines \u2014 422.51\n\n" +

                "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n\n" +

                "DUAL-FUNCTION AFCI/GFCI:\n" +
                "Where BOTH AFCI and GFCI protection are required (e.g., dwelling unit kitchen,\n" +
                "laundry), use a dual-function AFCI/GFCI breaker to satisfy both requirements\n" +
                "with a single device."
        });

        // ── Electrical Room Clearances — NEC 110.26 ──
        store.Sheets.Add(new CheatSheet
        {
            Id = "electrical-clearances",
            Title = "Electrical Room Clearances",
            Subtitle = "NEC 110.26",
            Description = "Working space requirements around electrical equipment. Minimum depth, width, and headroom for safe access to panels, switchboards, and similar equipment.",
            Discipline = Discipline.Electrical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nec2020",
            Tags = new List<string>
            {
                "clearance", "working space", "110.26", "electrical room", "panel",
                "switchboard", "depth", "width", "headroom", "access", "dedicated space"
            },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Voltage to Ground", IsInputColumn = true },
                new() { Header = "Condition 1", Unit = "ft", IsOutputColumn = true },
                new() { Header = "Condition 2", Unit = "ft", IsOutputColumn = true },
                new() { Header = "Condition 3", Unit = "ft", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "0\u2013150V", "3", "3", "3" },
                new() { "151\u2013600V", "3", "3.5", "4" },
                new() { "601V\u20132,500V", "3", "4", "5" },
                new() { "2,501V\u20139,000V", "4", "5", "6" },
                new() { "9,001V\u201325,000V", "5", "6", "9" },
                new() { "25,001V\u201375kV", "6", "8", "12" }
            },
            NoteContent =
                "NEC 110.26 WORKING SPACE REQUIREMENTS\n\n" +

                "CONDITIONS:\n" +
                "\u2022 Condition 1: Exposed live parts on ONE side only, and no grounded parts\n" +
                "  on the other side (or grounded parts with suitable insulation).\n" +
                "\u2022 Condition 2: Exposed live parts on ONE side, grounded parts on the OTHER side.\n" +
                "  (Concrete, brick, or tile walls are considered grounded.)\n" +
                "\u2022 Condition 3: Exposed live parts on BOTH sides of the working space.\n\n" +

                "WIDTH \u2014 110.26(A)(2):\n" +
                "\u2022 Minimum 30 inches wide, or the width of the equipment, whichever is greater.\n" +
                "\u2022 Equipment rated 1200A or more and over 6 ft wide: one entrance at each end\n" +
                "  of the working space, minimum 24 inches wide \u00d7 6.5 ft high.\n\n" +

                "HEADROOM \u2014 110.26(A)(3):\n" +
                "\u2022 Minimum 6.5 feet (6 ft 6 in) or the height of the equipment, whichever is greater.\n" +
                "\u2022 Service equipment, switchboards, panelboards, or MCC installed in\n" +
                "  existing dwelling units: existing headroom is permitted.\n\n" +

                "DEDICATED EQUIPMENT SPACE \u2014 110.26(E):\n" +
                "\u2022 Indoor: the footprint of the equipment extends from floor to a height of 6 ft\n" +
                "  (or the structural ceiling) above the equipment. No piping, ducts, leak protection,\n" +
                "  or other equipment foreign to the electrical installation is permitted in this zone.\n" +
                "\u2022 Sprinkler protection is permitted if piping passes through the zone.\n" +
                "\u2022 Suspended ceilings with removable panels are NOT considered structural ceilings.\n\n" +

                "ILLUMINATION \u2014 110.26(D):\n" +
                "\u2022 Illumination SHALL be provided for all working spaces around service equipment,\n" +
                "  switchboards, panelboards, or motor control centers installed indoors.\n" +
                "\u2022 Shall not be controlled by automatic means only.\n\n" +

                "ENTRANCE / EGRESS \u2014 110.26(C):\n" +
                "\u2022 At least one entrance of sufficient area to give access to the working space.\n" +
                "\u2022 For equipment rated 1200A or more: minimum two entrances, each at least\n" +
                "  24 in wide \u00d7 6.5 ft high, at opposite ends of the working space.\n" +
                "\u2022 Exception: a single entrance is permitted if the working space is double\n" +
                "  the required depth."
        });
    }
}
