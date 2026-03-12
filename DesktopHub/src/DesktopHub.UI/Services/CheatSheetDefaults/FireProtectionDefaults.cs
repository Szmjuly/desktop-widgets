using System.Collections.Generic;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Default cheat sheet data for the Fire Protection discipline.
/// </summary>
internal static class CheatSheetFireProtectionDefaults
{
    internal static void AddTo(CheatSheetDataStore store)
    {
        // NFPA 13 Sprinkler Design Criteria
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-design-criteria",
            Title = "Sprinkler Design Criteria",
            Subtitle = "NFPA 13 \u2014 Hazard Classification",
            Description = "Minimum sprinkler design density and area of operation by occupancy hazard classification per NFPA 13",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "sprinkler", "design", "density", "area", "hazard", "NFPA 13", "light", "ordinary", "extra" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Hazard", IsInputColumn = true },
                new() { Header = "Density", Unit = "gpm/ft\u00b2", IsOutputColumn = true },
                new() { Header = "Design Area", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "Typical Occupancies", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Light Hazard", "0.10", "1,500", "Churches, hospitals, offices, residential, restaurants, schools" },
                new() { "Ordinary Hazard Group 1", "0.15", "1,500", "Parking garages, laundries, electronics, bakeries" },
                new() { "Ordinary Hazard Group 2", "0.20", "1,500", "Chemical plants, dry cleaners, machine shops, stages, warehouses (Class I\u2013IV <12 ft)" },
                new() { "Extra Hazard Group 1", "0.30", "2,500", "Aircraft hangars, die casting, plywood mfg, upholstering" },
                new() { "Extra Hazard Group 2", "0.40", "2,500", "Flammable liquid spraying, open oil quenching, solvent cleaning, varnish/paint dipping" }
            }
        });

        // NFPA 13 Pipe Schedule — Light Hazard
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-pipe-schedule-light",
            Title = "Pipe Schedule \u2014 Light Hazard",
            Subtitle = "NFPA 13 Table 23.6.3.1.1 (Steel Pipe)",
            Description = "Maximum number of sprinklers on a single pipe for Light Hazard pipe schedule systems",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "pipe schedule", "sprinkler", "light hazard", "steel pipe", "NFPA 13", "23.6.3.1" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Pipe Size", Unit = "in", IsInputColumn = true },
                new() { Header = "Max Sprinklers", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1", "2" },
                new() { "1-1/4", "3" },
                new() { "1-1/2", "5" },
                new() { "2", "10" },
                new() { "2-1/2", "30" },
                new() { "3", "60" },
                new() { "3-1/2", "100" }
            }
        });

        // NFPA 13 Pipe Schedule — Ordinary Hazard
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-pipe-schedule-ordinary",
            Title = "Pipe Schedule \u2014 Ordinary Hazard",
            Subtitle = "NFPA 13 Table 23.6.3.1.2 (Steel Pipe)",
            Description = "Maximum number of sprinklers on a single pipe for Ordinary Hazard pipe schedule systems",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "pipe schedule", "sprinkler", "ordinary hazard", "steel pipe", "NFPA 13", "23.6.3.1" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Pipe Size", Unit = "in", IsInputColumn = true },
                new() { Header = "Max Sprinklers", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "1", "2" },
                new() { "1-1/4", "3" },
                new() { "1-1/2", "5" },
                new() { "2", "10" },
                new() { "2-1/2", "20" },
                new() { "3", "40" },
                new() { "3-1/2", "65" }
            }
        });

        // NFPA 14 Standpipe Requirements
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa14-standpipe-classes",
            Title = "Standpipe System Classes",
            Subtitle = "NFPA 14 \u2014 Standpipe Classifications",
            Description = "Standpipe system classifications, flow requirements, and typical applications",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nfpa14-2019",
            Tags = new List<string> { "standpipe", "NFPA 14", "class I", "class II", "class III", "hose", "flow", "pressure" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Class", IsInputColumn = true },
                new() { Header = "Hose Connection", IsOutputColumn = true },
                new() { Header = "Min Flow", Unit = "gpm", IsOutputColumn = true },
                new() { Header = "Min Pressure", Unit = "psi", IsOutputColumn = true },
                new() { Header = "Use", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Class I", "2-1/2\" outlet", "500", "100", "Fire department use" },
                new() { "Class II", "1-1/2\" hose station", "100", "65", "Building occupant use" },
                new() { "Class III", "Both 2-1/2\" + 1-1/2\"", "500", "100", "Combined \u2014 FD and occupant" }
            }
        });

        // Hose Stream Allowances
        store.Sheets.Add(new CheatSheet
        {
            Id = "hose-stream-allowances",
            Title = "Hose Stream Allowances",
            Subtitle = "NFPA 13 \u2014 Inside Hose Stream Demand",
            Description = "Hose stream allowances to add to sprinkler system demand based on hazard classification",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "hose stream", "allowance", "demand", "NFPA 13", "sprinkler", "fire pump", "hazard" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Hazard", IsInputColumn = true },
                new() { Header = "Inside Hose", Unit = "gpm", IsOutputColumn = true },
                new() { Header = "Duration", Unit = "min", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Light Hazard", "0, 50, or 100", "30" },
                new() { "Ordinary Hazard Group 1", "0, 50, or 100", "60\u201390" },
                new() { "Ordinary Hazard Group 2", "0, 50, or 100", "60\u201390" },
                new() { "Extra Hazard Group 1", "0, 50, or 100", "90\u2013120" },
                new() { "Extra Hazard Group 2", "0, 50, or 100", "90\u2013120" }
            }
        });

        // Common Fire Pump Sizes
        store.Sheets.Add(new CheatSheet
        {
            Id = "fire-pump-sizes",
            Title = "Common Fire Pump Sizes",
            Subtitle = "NFPA 20 \u2014 Standard Fire Pump Ratings",
            Description = "Standard rated capacities for listed fire pumps per NFPA 20",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nfpa20-2022",
            Tags = new List<string> { "fire pump", "NFPA 20", "capacity", "rated", "GPM", "PSI", "HP" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Rated GPM", Unit = "gpm", IsInputColumn = true },
                new() { Header = "Typical Pressure", Unit = "psi", IsOutputColumn = true },
                new() { Header = "Approx. Electric HP", Unit = "hp", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "25", "40\u2013100", "3\u20135" },
                new() { "50", "40\u2013140", "5\u201315" },
                new() { "100", "40\u2013140", "10\u201325" },
                new() { "150", "40\u2013140", "15\u201330" },
                new() { "200", "40\u2013175", "20\u201350" },
                new() { "250", "40\u2013175", "25\u201360" },
                new() { "300", "40\u2013175", "30\u201375" },
                new() { "400", "40\u2013175", "40\u2013100" },
                new() { "500", "40\u2013250", "50\u2013125" },
                new() { "750", "40\u2013271", "75\u2013200" },
                new() { "1000", "40\u2013290", "100\u2013250" },
                new() { "1250", "40\u2013300", "125\u2013300" },
                new() { "1500", "40\u2013325", "150\u2013400" },
                new() { "2000", "40\u2013332", "200\u2013500" },
                new() { "2500", "80\u2013224", "250\u2013600" }
            }
        });

        // Sprinkler Head Spacing
        store.Sheets.Add(new CheatSheet
        {
            Id = "sprinkler-spacing",
            Title = "Sprinkler Spacing Requirements",
            Subtitle = "NFPA 13 \u2014 Standard Spray Upright/Pendent",
            Description = "Maximum protection area, spacing, and distance from walls for standard spray sprinklers",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "sprinkler", "spacing", "coverage", "area", "NFPA 13", "distance", "wall", "pendent", "upright" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Hazard / Type", IsInputColumn = true },
                new() { Header = "Max Area/Head", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "Max Spacing", Unit = "ft", IsOutputColumn = true },
                new() { Header = "Min Spacing", Unit = "ft", IsOutputColumn = true },
                new() { Header = "Max from Wall", Unit = "ft", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Light Hazard \u2014 Noncombustible ceiling", "225", "15", "6", "7.5" },
                new() { "Light Hazard \u2014 Combustible ceiling", "168", "15", "6", "7.5" },
                new() { "Ordinary Hazard", "130", "15", "6", "7.5" },
                new() { "Extra Hazard", "100", "12", "6", "6" },
                new() { "High-Piled Storage", "100", "12", "6", "6" }
            }
        });

        // Water Supply Duration Requirements
        store.Sheets.Add(new CheatSheet
        {
            Id = "fp-water-supply-duration",
            Title = "Water Supply Duration",
            Subtitle = "NFPA 13 \u2014 Minimum Water Supply Duration",
            Description = "Minimum duration of water supply for sprinkler systems by hazard classification",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "water supply", "duration", "NFPA 13", "sprinkler", "hazard", "minutes", "tank" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Hazard", IsInputColumn = true },
                new() { Header = "Min Duration", Unit = "min", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Light Hazard", "30" },
                new() { "Ordinary Hazard Group 1", "60" },
                new() { "Ordinary Hazard Group 2", "60" },
                new() { "Extra Hazard Group 1", "90" },
                new() { "Extra Hazard Group 2", "120" }
            }
        });

        // Sprinkler Temperature Ratings
        store.Sheets.Add(new CheatSheet
        {
            Id = "sprinkler-temp-ratings",
            Title = "Sprinkler Temperature Ratings",
            Subtitle = "NFPA 13 Table 7.2.4.1",
            Description = "Sprinkler head temperature ratings, classifications, color codes, and maximum ceiling temperatures",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "sprinkler", "temperature", "rating", "color code", "NFPA 13", "glass bulb", "fusible link", "7.2.4.1" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Classification", IsInputColumn = true },
                new() { Header = "Temp Rating (\u00b0F)", IsOutputColumn = true },
                new() { Header = "Max Ceiling (\u00b0F)", IsOutputColumn = true },
                new() { Header = "Glass Bulb Color", IsOutputColumn = true },
                new() { Header = "Link Color", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Ordinary", "135\u2013170", "100", "Orange or Red", "Uncolored" },
                new() { "Intermediate", "175\u2013225", "150", "Yellow or Green", "White" },
                new() { "High", "250\u2013300", "225", "Blue", "Blue" },
                new() { "Extra High", "325\u2013375", "300", "Purple", "Red" },
                new() { "Very Extra High", "400\u2013475", "375", "Black", "Green" },
                new() { "Ultra High", "500\u2013575", "475", "Black", "Orange" }
            }
        });

        // Sprinkler K-Factor Reference
        store.Sheets.Add(new CheatSheet
        {
            Id = "sprinkler-k-factors",
            Title = "Sprinkler K-Factor Reference",
            Subtitle = "Common Sprinkler Discharge Coefficients",
            Description = "Standard K-factors for sprinkler heads and their typical applications. Flow = K \u00d7 \u221aP where P is pressure in PSI.",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "K-factor", "sprinkler", "discharge", "coefficient", "flow", "orifice", "NFPA 13" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "K-Factor", IsInputColumn = true },
                new() { Header = "Orifice Size", Unit = "in", IsOutputColumn = true },
                new() { Header = "Typical Application", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "K 2.8", "3/8", "Residential, small orifice" },
                new() { "K 4.0", "7/16", "Residential" },
                new() { "K 4.2", "7/16", "Residential (NFPA 13R/13D)" },
                new() { "K 5.6", "1/2", "Standard spray \u2014 most common" },
                new() { "K 8.0", "17/32", "Large orifice" },
                new() { "K 11.2", "5/8", "Extended coverage, storage" },
                new() { "K 14.0", "3/4", "ESFR, storage" },
                new() { "K 16.8", "3/4+", "ESFR" },
                new() { "K 22.4", "1", "ESFR high-challenge storage" },
                new() { "K 25.2", "1+", "ESFR high-challenge storage" }
            }
        });

        // ================================================================
        // Occupancy Hazard Classifications (NFPA 13 Annex A.4.3)
        // ================================================================

        // A.4.3.2 — Light Hazard Occupancies
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-light-hazard-occupancies",
            Title = "Light Hazard Occupancies",
            Subtitle = "NFPA 13 A.4.3.2",
            Description = "Light hazard occupancies include occupancies having uses and conditions similar to the following",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "light hazard", "occupancy", "classification", "NFPA 13", "A.4.3.2", "hazard group" },
            NoteContent =
                "A.4.3.2 \u2014 Light Hazard Occupancies:\n\n" +
                "(1)  Animal shelters\n" +
                "(2)  Churches\n" +
                "(3)  Clubs\n" +
                "(4)  Eaves and overhangs, if of combustible construction with no combustibles beneath\n" +
                "(5)  Educational\n" +
                "(6)  Hospitals, including animal hospitals and veterinary facilities\n" +
                "(7)  Institutional\n" +
                "(8)  Kennels\n" +
                "(9)  Libraries, except large stack rooms\n" +
                "(10) Museums\n" +
                "(11) Nursing or convalescent homes\n" +
                "(12) Offices, including data processing\n" +
                "(13) Residential\n" +
                "(14) Restaurant seating areas\n" +
                "(15) Theaters and auditoriums, excluding stages and prosceniums\n" +
                "(16) Unused attics"
        });

        // A.4.3.3 — Ordinary Hazard (Group 1) Occupancies
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-oh1-occupancies",
            Title = "Ordinary Hazard Group 1 Occupancies",
            Subtitle = "NFPA 13 A.4.3.3",
            Description = "Ordinary Hazard (Group 1) occupancies include occupancies having uses and conditions similar to the following",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "ordinary hazard", "group 1", "OH1", "occupancy", "classification", "NFPA 13", "A.4.3.3", "hazard group" },
            NoteContent =
                "A.4.3.3 \u2014 Ordinary Hazard (Group 1) Occupancies:\n\n" +
                "(1)  Automobile parking and showrooms\n" +
                "(2)  Bakeries\n" +
                "(3)  Beverage manufacturing\n" +
                "(4)  Canneries\n" +
                "(5)  Dairy products manufacturing and processing\n" +
                "(6)  Electronic plants\n" +
                "(7)  Glass and glass products manufacturing\n" +
                "(8)  Laundries\n" +
                "(9)  Restaurant service areas\n" +
                "(10) Porte cocheres\n" +
                "(11) Mechanical rooms"
        });

        // A.4.3.4 — Ordinary Hazard (Group 2) Occupancies
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-oh2-occupancies",
            Title = "Ordinary Hazard Group 2 Occupancies",
            Subtitle = "NFPA 13 A.4.3.4",
            Description = "Ordinary Hazard (Group 2) occupancies include occupancies having uses and conditions similar to the following",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "ordinary hazard", "group 2", "OH2", "occupancy", "classification", "NFPA 13", "A.4.3.4", "hazard group" },
            NoteContent =
                "A.4.3.4 \u2014 Ordinary Hazard (Group 2) Occupancies:\n\n" +
                "(1)  Agricultural facilities\n" +
                "(2)  Barns and stables\n" +
                "(3)  Cereal mills\n" +
                "(4)  Chemical plants \u2014 ordinary\n" +
                "(5)  Confectionery products\n" +
                "(6)  Distilleries\n" +
                "(7)  Dry cleaners\n" +
                "(8)  Exterior loading docks (Note: exterior loading docks only used for loading and unloading of ordinary combustibles should be classified as OH2. For the handling of flammable and combustible liquids, hazardous materials, or where utilized for storage, exterior loading docks and all interior loading docks should be protected based upon the actual occupancy and the materials handled on the dock, as if the materials were actually stored in that configuration.)\n" +
                "(9)  Feed mills\n" +
                "(10) Horse stables\n" +
                "(11) Leather goods manufacturing\n" +
                "(12) Libraries \u2014 large stack room areas\n" +
                "(13) Machine shops\n" +
                "(14) Metal working\n" +
                "(15) Mercantile\n" +
                "(16) Paper and pulp mills\n" +
                "(17) Paper process plants\n" +
                "(18) Piers and wharves\n" +
                "(19) Plastics fabrication, including blow molding, extruding, and machining; excluding operations using combustible hydraulic fluids\n" +
                "(20) Post offices\n" +
                "(21) Printing and publishing\n" +
                "(22) Racetrack stable/kennel areas, including those stable/kennel areas, barns, and associated buildings at state, county, and local fairgrounds\n" +
                "(23) Repair garages\n" +
                "(24) Resin application area\n" +
                "(25) Stages\n" +
                "(26) Textile manufacturing\n" +
                "(27) Tire manufacturing\n" +
                "(28) Tobacco products manufacturing\n" +
                "(29) Wood machining\n" +
                "(30) Wood product assembly"
        });

        // A.4.3.5 — Extra Hazard (Group 1) Occupancies
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-eh1-occupancies",
            Title = "Extra Hazard Group 1 Occupancies",
            Subtitle = "NFPA 13 A.4.3.5",
            Description = "Extra Hazard (Group 1) occupancies include occupancies having uses and conditions similar to the following",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "extra hazard", "group 1", "EH1", "occupancy", "classification", "NFPA 13", "A.4.3.5", "hazard group" },
            NoteContent =
                "A.4.3.5 \u2014 Extra Hazard (Group 1) Occupancies:\n\n" +
                "(1)  Aircraft hangars (except as governed by NFPA 409)\n" +
                "(2)  Combustible hydraulic fluid use areas\n" +
                "(3)  Die casting\n" +
                "(4)  Metal extruding\n" +
                "(5)  Plywood and particleboard manufacturing\n" +
                "(6)  Printing [using inks having flash points below 100\u00b0F (38\u00b0C)]\n" +
                "(7)  Rubber reclaiming, compounding, drying, milling, vulcanizing\n" +
                "(8)  Saw mills\n" +
                "(9)  Textile picking, opening, blending, garnetting, or carding, combining of cotton, synthetics, wool shoddy, or burlap\n" +
                "(10) Upholstering with plastic foams"
        });

        // A.4.3.6 — Extra Hazard (Group 2) Occupancies
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-eh2-occupancies",
            Title = "Extra Hazard Group 2 Occupancies",
            Subtitle = "NFPA 13 A.4.3.6",
            Description = "Extra Hazard (Group 2) occupancies include occupancies having uses and conditions similar to the following",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Note,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "extra hazard", "group 2", "EH2", "occupancy", "classification", "NFPA 13", "A.4.3.6", "hazard group" },
            NoteContent =
                "A.4.3.6 \u2014 Extra Hazard (Group 2) Occupancies:\n\n" +
                "(1)  Asphalt saturating\n" +
                "(2)  Flammable liquids spraying\n" +
                "(3)  Flow coating\n" +
                "(4)  Manufactured home or modular building assemblies (where finished enclosure is present and has combustible interiors)\n" +
                "(5)  Open oil quenching\n" +
                "(6)  Plastics manufacturing\n" +
                "(7)  Solvent cleaning\n" +
                "(8)  Varnish and paint dipping\n" +
                "(9)  Car stackers and car lift systems with 2 cars stacked vertically"
        });

        // ================================================================
        // Detailed Sprinkler Spacing Tables (from NFPA 13)
        // ================================================================

        // Table 10.2.4.2.1(a) — Light Hazard Standard Pendent/Upright Spray
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-table-10-2-4-2-1a",
            Title = "Sprinkler Spacing \u2014 Light Hazard (Detailed)",
            Subtitle = "NFPA 13 Table 10.2.4.2.1(a)",
            Description = "Protection Areas and Maximum Spacing of Standard Pendent and Upright Spray Sprinklers for Light Hazard",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "sprinkler", "spacing", "light hazard", "protection area", "10.2.4.2.1", "standard spray", "pendent", "upright", "construction type" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Construction Type", IsInputColumn = true },
                new() { Header = "System Type", IsInputColumn = true },
                new() { Header = "Area (ft\u00b2)", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "Area (m\u00b2)", Unit = "m\u00b2", IsOutputColumn = true },
                new() { Header = "Spacing (ft)", Unit = "ft", IsOutputColumn = true },
                new() { Header = "Spacing (m)", Unit = "m", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Noncombustible unobstructed", "Hydraulically calculated", "225", "20", "15", "4.6" },
                new() { "Noncombustible unobstructed", "Pipe schedule", "200", "18", "15", "4.6" },
                new() { "Noncombustible obstructed", "Hydraulically calculated", "225", "20", "15", "4.6" },
                new() { "Noncombustible obstructed", "Pipe schedule", "200", "18", "15", "4.6" },
                new() { "Combustible unobstructed with no exposed members", "Hydraulically calculated", "225", "20", "15", "4.6" },
                new() { "Combustible unobstructed with no exposed members", "Pipe schedule", "200", "18", "15", "4.6" },
                new() { "Combustible unobstructed with exposed members 3 ft (910 mm) or more on center", "Hydraulically calculated", "225", "20", "15", "4.6" },
                new() { "Combustible unobstructed with exposed members 3 ft (910 mm) or more on center", "Pipe schedule", "200", "18", "15", "4.6" },
                new() { "Combustible unobstructed with members less than 3 ft (910 mm) on center", "All", "130", "12", "15", "4.6" },
                new() { "Combustible obstructed with exposed members 3 ft (910 mm) or more on center", "All", "168", "16", "15", "4.6" },
                new() { "Combustible obstructed with members less than 3 ft (910 mm) on center", "All", "130", "12", "15", "4.6" },
                new() { "Combustible concealed spaces in accordance with 10.2.6.1.4", "All", "120", "11", "15 parallel / 10 perpendicular to slope", "4.6 parallel / 3.0 perpendicular to slope" }
            }
        });

        // Table 10.2.4.2.1(b) — Ordinary Hazard Standard Pendent/Upright Spray
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-table-10-2-4-2-1b",
            Title = "Sprinkler Spacing \u2014 Ordinary Hazard (Detailed)",
            Subtitle = "NFPA 13 Table 10.2.4.2.1(b)",
            Description = "Protection Areas and Maximum Spacing of Standard Pendent and Upright Spray Sprinklers for Ordinary Hazard",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.SimpleList,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "sprinkler", "spacing", "ordinary hazard", "protection area", "10.2.4.2.1", "standard spray", "pendent", "upright" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Construction Type", IsInputColumn = true },
                new() { Header = "System Type", IsInputColumn = true },
                new() { Header = "Area (ft\u00b2)", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "Area (m\u00b2)", Unit = "m\u00b2", IsOutputColumn = true },
                new() { Header = "Spacing (ft)", Unit = "ft", IsOutputColumn = true },
                new() { Header = "Spacing (m)", Unit = "m", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "All", "All", "130", "12", "15", "4.6" }
            }
        });

        // Table 11.2.2.1.2 — Extended Coverage Upright/Pendent Spray
        store.Sheets.Add(new CheatSheet
        {
            Id = "nfpa13-table-11-2-2-1-2",
            Title = "Extended Coverage Sprinkler Spacing",
            Subtitle = "NFPA 13 Table 11.2.2.1.2",
            Description = "Protection Areas and Maximum Spacing for Extended Coverage Upright and Pendent Spray Sprinklers by hazard and construction type",
            Discipline = Discipline.FireProtection,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.FullTable,
            CodeBookId = "nfpa13-2022",
            Tags = new List<string> { "sprinkler", "extended coverage", "spacing", "protection area", "11.2.2.1.2", "light hazard", "ordinary hazard", "extra hazard", "high-piled storage" },
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Construction Type", IsInputColumn = true },
                new() { Header = "LH Area (ft\u00b2)", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "LH Spacing (ft)", Unit = "ft", IsOutputColumn = true },
                new() { Header = "OH Area (ft\u00b2)", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "OH Spacing (ft)", Unit = "ft", IsOutputColumn = true },
                new() { Header = "EH Area (ft\u00b2)", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "EH Spacing (ft)", Unit = "ft", IsOutputColumn = true },
                new() { Header = "HPS Area (ft\u00b2)", Unit = "ft\u00b2", IsOutputColumn = true },
                new() { Header = "HPS Spacing (ft)", Unit = "ft", IsOutputColumn = true }
            },
            Rows = new List<List<string>>
            {
                new() { "Unobstructed", "400 (37)", "20 (6.1)", "400 (37)", "20 (6.1)", "\u2014", "\u2014", "\u2014", "\u2014" },
                new() { "Unobstructed", "324 (30)", "18 (5.5)", "324 (30)", "18 (5.5)", "\u2014", "\u2014", "\u2014", "\u2014" },
                new() { "Unobstructed", "256 (24)", "16 (4.9)", "256 (24)", "16 (4.9)", "\u2014", "\u2014", "\u2014", "\u2014" },
                new() { "Unobstructed", "\u2014", "\u2014", "196 (18)", "14 (4.3)", "196 (18)", "14 (4.3)", "196 (18)", "14 (4.3)" },
                new() { "Unobstructed", "\u2014", "\u2014", "144 (13)", "12 (3.7)", "144 (13)", "15 (4.6)", "144 (13)", "15 (4.6)" },
                new() { "Obstructed noncombustible (when specifically listed for such use)", "400 (37)", "20 (6.1)", "400 (37)", "20 (6.1)", "\u2014", "\u2014", "\u2014", "\u2014" },
                new() { "Obstructed noncombustible", "324 (30)", "18 (5.5)", "324 (30)", "18 (5.5)", "\u2014", "\u2014", "\u2014", "\u2014" },
                new() { "Obstructed noncombustible", "256 (24)", "16 (4.9)", "256 (24)", "16 (4.9)", "\u2014", "\u2014", "\u2014", "\u2014" },
                new() { "Obstructed noncombustible", "\u2014", "\u2014", "196 (18)", "14 (4.3)", "196 (18)", "14 (4.3)", "196 (18)", "14 (4.3)" },
                new() { "Obstructed noncombustible", "\u2014", "\u2014", "144 (13)", "12 (3.7)", "144 (13)", "15 (4.6)", "144 (13)", "15 (4.6)" },
                new() { "Obstructed combustible", "N/A", "N/A", "N/A", "N/A", "N/A", "N/A", "N/A", "N/A" }
            }
        });
    }
}
