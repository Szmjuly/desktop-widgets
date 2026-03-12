using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Code books and jurisdictions shared across all disciplines.
/// </summary>
internal static class CheatSheetSharedDefaults
{
    internal static void AddTo(CheatSheetDataStore store)
    {
        // --- Code Books ---
        store.CodeBooks.Add(new CodeBook { Id = "nec2020", Name = "NEC", Edition = "2020", Year = 2020, Discipline = Discipline.Electrical });
        store.CodeBooks.Add(new CodeBook { Id = "nec2023", Name = "NEC", Edition = "2023", Year = 2023, Discipline = Discipline.Electrical });
        store.CodeBooks.Add(new CodeBook { Id = "ipc2021", Name = "IPC", Edition = "2021", Year = 2021, Discipline = Discipline.Plumbing });
        store.CodeBooks.Add(new CodeBook { Id = "fmc2023", Name = "FMC", Edition = "2023", Year = 2023, Discipline = Discipline.Mechanical });
        store.CodeBooks.Add(new CodeBook { Id = "fbc-energy-2023", Name = "FBC Energy", Edition = "2023", Year = 2023, Discipline = Discipline.Electrical });
        store.CodeBooks.Add(new CodeBook { Id = "nfpa13-2022", Name = "NFPA 13", Edition = "2022", Year = 2022, Discipline = Discipline.FireProtection });
        store.CodeBooks.Add(new CodeBook { Id = "nfpa14-2019", Name = "NFPA 14", Edition = "2019", Year = 2019, Discipline = Discipline.FireProtection });
        store.CodeBooks.Add(new CodeBook { Id = "nfpa20-2022", Name = "NFPA 20", Edition = "2022", Year = 2022, Discipline = Discipline.FireProtection });

        // --- Jurisdictions ---
        store.Jurisdictions.Add(new JurisdictionCodeAdoption
        {
            JurisdictionId = "fl-state",
            Name = "Florida (Statewide)",
            State = "FL",
            AdoptedCodeBookId = "nec2020",
            AdoptionName = "FBC 2023 (8th Edition)",
            AdoptionYear = 2023,
            Notes = "FBC 2023 adopts NEC 2020. Will move to FBC 2026 / NEC 2023."
        });
        store.Jurisdictions.Add(new JurisdictionCodeAdoption
        {
            JurisdictionId = "fl-miami-dade",
            Name = "Miami-Dade County",
            State = "FL",
            AdoptedCodeBookId = "nec2020",
            AdoptionName = "FBC 2023 + Miami-Dade Amendments",
            AdoptionYear = 2023,
            Notes = "LV wiring requires specific keynotes per local amendments."
        });
    }
}
