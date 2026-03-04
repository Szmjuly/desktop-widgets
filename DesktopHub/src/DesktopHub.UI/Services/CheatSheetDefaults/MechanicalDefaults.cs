using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Default cheat sheet data for the Mechanical discipline.
/// </summary>
internal static class CheatSheetMechanicalDefaults
{
    internal static void AddTo(CheatSheetDataStore store)
    {
        // HVAC Rules of Thumb
        store.Sheets.Add(new CheatSheet
        {
            Id = "hvac-rules-of-thumb",
            Title = "HVAC Rules of Thumb",
            Subtitle = "Common HVAC CFM per Ton Ratios",
            Description = "General rules of thumb for outside air and supply air calculations",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Note,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "HVAC", "CFM", "ton", "outside air", "supply air", "rule of thumb", "OA", "SA" },
            NoteContent = "General HVAC Rules of Thumb:\n\n" +
                          "\u2022 Outside Air (OA): 150 CFM per ton\n" +
                          "\u2022 Supply Air (SA): 400 CFM per ton\n\n" +
                          "These are approximate starting points for preliminary calculations. " +
                          "Final values must be determined by load calculations per ASHRAE 62.1 and project-specific requirements."
        });

        // Condensate Drain Sizing (Table 307.2.2)
        store.Sheets.Add(new CheatSheet
        {
            Id = "condensate-drain-sizing",
            Title = "Condensate Drain Sizing",
            Subtitle = "FMC Table 307.2.2",
            Description = "Minimum condensate drain pipe diameter based on equipment capacity",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "condensate", "drain", "pipe", "sizing", "refrigeration", "307.2.2" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Equipment Capacity", IsInputColumn = true },
                new() { Header = "Min. Pipe Diameter", Unit = "in", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Up to 20 tons", "3/4" },
                new() { "Over 20 to 40 tons", "1" },
                new() { "Over 40 to 90 tons", "1-1/4" },
                new() { "Over 90 to 125 tons", "1-1/2" },
                new() { "Over 125 to 250 tons", "2" }
            }
        });

        // Kitchen Hood Exhaust CFM (combined table)
        store.Sheets.Add(new CheatSheet
        {
            Id = "hood-exhaust-cfm",
            Title = "Kitchen Hood Exhaust CFM",
            Subtitle = "CFM per Linear Foot by Hood Type and Duty",
            Description = "Minimum exhaust flow rates for kitchen hoods by duty classification and hood type",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "hood", "exhaust", "CFM", "kitchen", "cooking", "light duty", "medium duty", "heavy duty", "extra heavy duty" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Duty Level", IsInputColumn = true },
                new() { Header = "Hood Type", IsInputColumn = true },
                new() { Header = "CFM/Linear Ft", Unit = "cfm/ft", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Light", "Backshelf/pass-over", "250" },
                new() { "Light", "Double island canopy (per side)", "250" },
                new() { "Light", "Eyebrow", "250" },
                new() { "Light", "Single island canopy", "400" },
                new() { "Light", "Wall-mounted canopy", "200" },
                new() { "Medium", "Backshelf/pass-over", "300" },
                new() { "Medium", "Double island canopy (per side)", "300" },
                new() { "Medium", "Eyebrow", "250" },
                new() { "Medium", "Single island canopy", "500" },
                new() { "Medium", "Wall-mounted canopy", "300" },
                new() { "Heavy", "Backshelf/pass-over", "400" },
                new() { "Heavy", "Double island canopy (per side)", "400" },
                new() { "Heavy", "Eyebrow", "N/A" },
                new() { "Heavy", "Single island canopy", "600" },
                new() { "Heavy", "Wall-mounted canopy", "400" },
                new() { "Extra Heavy", "Backshelf/pass-over", "N/A" },
                new() { "Extra Heavy", "Double island canopy (per side)", "550" },
                new() { "Extra Heavy", "Eyebrow", "N/A" },
                new() { "Extra Heavy", "Single island canopy", "700" },
                new() { "Extra Heavy", "Wall-mounted canopy", "550" }
            }
        });

        // Grease Filter Clearance (Table 507.2.8)
        store.Sheets.Add(new CheatSheet
        {
            Id = "grease-filter-clearance",
            Title = "Grease Filter Clearance",
            Subtitle = "FMC Table 507.2.8",
            Description = "Minimum distance between the lowest edge of a grease filter and the cooking/heating surface",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "grease", "filter", "clearance", "cooking", "hood", "507.2.8" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Cooking Appliance Type", IsInputColumn = true },
                new() { Header = "Height Above Surface", Unit = "ft", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Without exposed flame", "0.5" },
                new() { "Exposed flame and burners", "2" },
                new() { "Exposed charcoal and charbroil type", "3.5" }
            }
        });

        // Clearance to Combustibles (Table 510.9.2)
        store.Sheets.Add(new CheatSheet
        {
            Id = "clearance-combustibles",
            Title = "Clearance to Combustibles",
            Subtitle = "FMC Table 510.9.2",
            Description = "Required clearance to combustible materials based on exhaust type or temperature",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "clearance", "combustibles", "exhaust", "temperature", "510.9.2" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Exhaust Type / Temp", IsInputColumn = true },
                new() { Header = "Clearance", Unit = "in", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Less than 100\u00b0F", "1" },
                new() { "100-600\u00b0F", "12" },
                new() { "Flammable vapors", "6" }
            }
        });

        // Type I/II Hood Notes
        store.Sheets.Add(new CheatSheet
        {
            Id = "type-i-ii-hoods",
            Title = "Type I & Type II Hoods",
            Subtitle = "FMC Sections 507.2 & 507.3",
            Description = "When Type I and Type II hoods are required",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Note,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "hood", "Type I", "Type II", "grease", "smoke", "cooking", "507.2", "507.3" },
            NoteContent = "TYPE I HOODS (Section 507.2):\n" +
                          "Required where cooking appliances produce grease or smoke. " +
                          "Install over medium-duty, heavy-duty and extra-heavy-duty cooking appliances.\n\n" +
                          "Exceptions:\n" +
                          "1. Not required for electric appliances with effluent \u22645 mg/m\u00b3 grease (tested per UL 710B at 500 cfm)\n" +
                          "2. Not required for solid fuel/combo pizza ovens tested and listed using direct venting per NFPA 96\n\n" +
                          "TYPE II HOODS (Section 507.3):\n" +
                          "Required above dishwashers and appliances that produce heat or moisture but NOT grease/smoke, " +
                          "except where loads are incorporated into HVAC design.\n\n" +
                          "Also required above all appliances producing products of combustion that don't produce grease/smoke.\n\n" +
                          "Spaces with appliances not requiring Type II hoods: exhaust at 0.70 cfm/ft\u00b2. " +
                          "Each appliance not under a hood = minimum 100 ft\u00b2 at 0.70 cfm/ft\u00b2."
        });

        // Local Exhaust R-2/R-3/R-4 (Table 403.3.2.3)
        store.Sheets.Add(new CheatSheet
        {
            Id = "local-exhaust-residential",
            Title = "Local Exhaust - Residential",
            Subtitle = "FMC Table 403.3.2.3",
            Description = "Minimum required local exhaust rates for Group R-2, R-3, and R-4 occupancies",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "exhaust", "residential", "kitchen", "bathroom", "R-2", "R-3", "R-4", "403.3.2.3" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Area", IsInputColumn = true },
                new() { Header = "Exhaust Rate", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Kitchens", "100 cfm intermittent or 25 cfm continuous" },
                new() { "Bathrooms and toilet rooms", "50 cfm intermittent or 20 cfm continuous" }
            }
        });

        // Minimum Ventilation Rates (Table 403.3.1.1)
        store.Sheets.Add(new CheatSheet
        {
            Id = "min-ventilation-rates",
            Title = "Minimum Ventilation Rates",
            Subtitle = "FMC Table 403.3.1.1",
            Description = "Minimum ventilation rates by occupancy classification",
            Discipline = Discipline.Mechanical,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "fmc2023",
            Tags = new List<string> { "ventilation", "outdoor air", "Rp", "Ra", "exhaust", "occupancy", "ASHRAE", "403.3.1.1", "CFM" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Occupancy", IsInputColumn = true },
                new() { Header = "Density", Unit = "#/1000ft\u00b2" },
                new() { Header = "Rp", Unit = "cfm/person" },
                new() { Header = "Ra", Unit = "cfm/ft\u00b2" },
                new() { Header = "Exhaust", Unit = "cfm/ft\u00b2" }
            },
            Rows = new List<List<string>>
            {
                new() { "Correctional \u2014 Booking/waiting", "50", "7.5", "0.06", "\u2014" },
                new() { "Correctional \u2014 Cells without plumbing", "25", "5", "0.12", "\u2014" },
                new() { "Correctional \u2014 Cells with plumbing", "25", "5", "0.12", "1.0" },
                new() { "Correctional \u2014 Day room", "30", "5", "0.06", "\u2014" },
                new() { "Correctional \u2014 Guard stations", "15", "5", "0.06", "\u2014" },
                new() { "Dry cleaners \u2014 Coin-operated dry cleaner", "20", "15", "\u2014", "\u2014" },
                new() { "Dry cleaners \u2014 Coin-operated laundries", "20", "7.5", "0.12", "\u2014" },
                new() { "Dry cleaners \u2014 Commercial dry cleaner", "30", "30", "\u2014", "\u2014" },
                new() { "Dry cleaners \u2014 Commercial laundry", "10", "5", "0.12", "\u2014" },
                new() { "Dry cleaners \u2014 Storage, pick up", "30", "7.5", "0.12", "\u2014" },
                new() { "Education \u2014 Art classroom", "20", "10", "0.18", "0.7" },
                new() { "Education \u2014 Auditoriums", "150", "5", "0.06", "\u2014" },
                new() { "Education \u2014 Classrooms (ages 5-8)", "25", "10", "0.12", "\u2014" },
                new() { "Education \u2014 Classrooms (age 9+)", "35", "10", "0.12", "\u2014" },
                new() { "Education \u2014 Computer lab", "25", "10", "0.12", "\u2014" },
                new() { "Education \u2014 Day care (through age 4)", "25", "10", "0.18", "\u2014" },
                new() { "Education \u2014 Lecture classroom", "65", "7.5", "0.06", "\u2014" },
                new() { "Education \u2014 Lecture hall (fixed seats)", "150", "7.5", "0.06", "\u2014" },
                new() { "Education \u2014 Locker/dressing rooms", "\u2014", "\u2014", "\u2014", "0.25" },
                new() { "Education \u2014 Media center", "25", "10", "0.12", "\u2014" },
                new() { "Education \u2014 Multiuse assembly", "100", "7.5", "0.06", "\u2014" },
                new() { "Education \u2014 Music/theater/dance", "35", "10", "0.06", "\u2014" },
                new() { "Education \u2014 Science laboratories", "25", "10", "0.18", "1.0" },
                new() { "Education \u2014 Sports locker rooms", "\u2014", "\u2014", "\u2014", "0.5" },
                new() { "Education \u2014 Wood/metal shops", "20", "10", "0.18", "0.5" },
                new() { "Food/Bev \u2014 Bars, cocktail lounges", "100", "7.5", "0.18", "\u2014" },
                new() { "Food/Bev \u2014 Cafeteria, fast food", "100", "7.5", "0.18", "\u2014" },
                new() { "Food/Bev \u2014 Dining rooms", "70", "7.5", "0.18", "\u2014" },
                new() { "Food/Bev \u2014 Kitchens (cooking)", "20", "7.5", "0.12", "0.7" },
                new() { "Hotels \u2014 Bathrooms/toilet (private)", "\u2014", "\u2014", "\u2014", "25/50" },
                new() { "Hotels \u2014 Bedroom/living room", "10", "5", "0.06", "\u2014" },
                new() { "Hotels \u2014 Conference/meeting", "50", "5", "0.06", "\u2014" },
                new() { "Hotels \u2014 Dormitory sleeping areas", "20", "5", "0.06", "\u2014" },
                new() { "Hotels \u2014 Gambling casinos", "120", "7.5", "0.18", "\u2014" },
                new() { "Hotels \u2014 Lobbies/prefunction", "30", "7.5", "0.06", "\u2014" },
                new() { "Hotels \u2014 Multipurpose assembly", "120", "5", "0.06", "\u2014" },
                new() { "Offices \u2014 Conference rooms", "50", "5", "0.06", "\u2014" },
                new() { "Offices \u2014 Main entry lobbies", "10", "5", "0.06", "\u2014" },
                new() { "Offices \u2014 Office spaces", "5", "5", "0.06", "\u2014" },
                new() { "Offices \u2014 Reception areas", "30", "5", "0.06", "\u2014" },
                new() { "Offices \u2014 Telephone/data entry", "60", "5", "0.06", "\u2014" },
                new() { "Public \u2014 Corridors", "\u2014", "\u2014", "0.06", "\u2014" },
                new() { "Public \u2014 Courtrooms", "70", "5", "0.06", "\u2014" },
                new() { "Public \u2014 Elevator car", "\u2014", "\u2014", "\u2014", "1.0" },
                new() { "Public \u2014 Legislative chambers", "50", "5", "0.06", "\u2014" },
                new() { "Public \u2014 Libraries", "10", "5", "0.12", "\u2014" },
                new() { "Public \u2014 Museums (children's)", "40", "7.5", "0.12", "\u2014" },
                new() { "Public \u2014 Museums/galleries", "40", "7.5", "0.06", "\u2014" },
                new() { "Public \u2014 Places of religious worship", "120", "5", "0.06", "\u2014" },
                new() { "Public \u2014 Shower room (per head)", "\u2014", "\u2014", "\u2014", "50/20" },
                new() { "Public \u2014 Toilet rooms (public)", "\u2014", "\u2014", "\u2014", "50/70" },
                new() { "Retail \u2014 Dressing rooms", "\u2014", "\u2014", "\u2014", "0.25" },
                new() { "Retail \u2014 Mall common areas", "40", "7.5", "0.06", "\u2014" },
                new() { "Retail \u2014 Sales", "15", "7.5", "0.12", "\u2014" },
                new() { "Retail \u2014 Shipping and receiving", "2", "10", "0.12", "\u2014" },
                new() { "Retail \u2014 Storage rooms", "\u2014", "\u2014", "0.12", "\u2014" },
                new() { "Retail \u2014 Warehouses", "\u2014", "10", "0.06", "\u2014" },
                new() { "Specialty \u2014 Barber", "25", "7.5", "0.06", "0.5" },
                new() { "Specialty \u2014 Beauty salons", "25", "20", "0.12", "0.6" },
                new() { "Specialty \u2014 Nail salons", "25", "20", "0.12", "0.6" },
                new() { "Specialty \u2014 Pet shops (animal areas)", "10", "7.5", "0.18", "0.9" },
                new() { "Specialty \u2014 Supermarkets", "8", "7.5", "0.06", "\u2014" },
                new() { "Sports \u2014 Bowling alleys (seating)", "40", "10", "0.12", "\u2014" },
                new() { "Sports \u2014 Disco/dance floors", "100", "20", "0.06", "\u2014" },
                new() { "Sports \u2014 Game arcades", "20", "7.5", "0.18", "\u2014" },
                new() { "Sports \u2014 Gym/stadium/arena (play)", "7", "20", "0.18", "\u2014" },
                new() { "Sports \u2014 Health club/aerobics", "40", "20", "0.06", "\u2014" },
                new() { "Sports \u2014 Health club/weight room", "10", "20", "0.06", "\u2014" },
                new() { "Sports \u2014 Ice arenas (no combustion)", "\u2014", "\u2014", "0.30", "0.5" },
                new() { "Sports \u2014 Spectator areas", "150", "7.5", "0.06", "\u2014" },
                new() { "Sports \u2014 Swimming pools (pool/deck)", "\u2014", "\u2014", "0.48", "\u2014" },
                new() { "Storage \u2014 Parking garages (enclosed)", "\u2014", "\u2014", "\u2014", "0.75" },
                new() { "Storage \u2014 Refrigerated warehouses", "\u2014", "10", "\u2014", "\u2014" },
                new() { "Storage \u2014 Warehouses", "\u2014", "10", "0.06", "\u2014" },
                new() { "Theaters \u2014 Lobbies", "150", "5", "0.06", "\u2014" },
                new() { "Theaters \u2014 Stages, studios", "70", "10", "0.06", "\u2014" },
                new() { "Theaters \u2014 Ticket booths", "60", "5", "0.06", "\u2014" },
                new() { "Transportation \u2014 Platforms", "100", "7.5", "0.06", "\u2014" },
                new() { "Transportation \u2014 Waiting areas", "100", "7.5", "0.06", "\u2014" },
                new() { "Workrooms \u2014 Bank vaults/safe deposit", "5", "5", "0.06", "\u2014" },
                new() { "Workrooms \u2014 Computer (no printing)", "4", "5", "0.06", "\u2014" },
                new() { "Workrooms \u2014 Copy, printing rooms", "4", "5", "0.06", "0.5" },
                new() { "Workrooms \u2014 Darkrooms", "\u2014", "\u2014", "\u2014", "1.0" },
                new() { "Workrooms \u2014 Pharmacy (prep. area)", "10", "5", "0.18", "\u2014" },
                new() { "Workrooms \u2014 Photo studios", "10", "5", "0.12", "\u2014" }
            }
        });
    }
}
