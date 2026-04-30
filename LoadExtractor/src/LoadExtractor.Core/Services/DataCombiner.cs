using LoadExtractor.Core.Models;

namespace LoadExtractor.Core.Services;

public class DataCombiner
{
    /// <summary>
    /// Combine data from PDF1 (Zone Sizing Summary) and PDF2 (Space Design Load Summary).
    /// Links spaces by system name + space name.
    /// </summary>
    public List<CombinedSpaceData> Combine(HapProject pdf1, List<SpaceComponentLoads> pdf2)
    {
        var results = new List<CombinedSpaceData>();

        // Build a lookup from PDF1: (systemName, spaceName) -> FloorArea
        var pdf1Lookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in pdf1.AirSystems)
        {
            foreach (var space in system.Spaces)
            {
                var key = $"{system.Name}|{space.SpaceName}";
                pdf1Lookup[key] = space.FloorArea;
            }
        }

        // For each space in PDF2, find its floor area from PDF1
        foreach (var loads in pdf2)
        {
            var key = $"{loads.SystemName}|{loads.SpaceName}";
            double floorArea = 0;

            // Try exact match first
            if (pdf1Lookup.TryGetValue(key, out double exactArea))
            {
                floorArea = exactArea;
            }
            else
            {
                // Try fuzzy match — space names may have slight differences
                // (e.g., "123 3 Car Garage" vs "123 3 Car Garage")
                foreach (var kvp in pdf1Lookup)
                {
                    var parts = kvp.Key.Split('|');
                    if (parts.Length == 2 &&
                        parts[0].Equals(loads.SystemName, StringComparison.OrdinalIgnoreCase) &&
                        NormalizeSpaceName(parts[1]) == NormalizeSpaceName(loads.SpaceName))
                    {
                        floorArea = kvp.Value;
                        break;
                    }
                }
            }

            // Extract people count from the People row details
            double peopleCount = 0;
            var peopleDetails = loads.People.CoolingDetails;
            if (!string.IsNullOrEmpty(peopleDetails))
            {
                double.TryParse(peopleDetails.Replace(",", ""), out peopleCount);
            }

            results.Add(new CombinedSpaceData
            {
                RoomName = loads.SpaceName,
                SystemName = loads.SystemName,
                FloorAreaSqFt = floorArea,
                TotalPeopleDetails = peopleCount,
                TotalCoolingSensible = loads.TotalZoneLoads.CoolingSensible,
                TotalCoolingLatent = loads.TotalZoneLoads.CoolingLatent,
                ComponentLoads = loads,
            });
        }

        return results;
    }

    private static string NormalizeSpaceName(string name)
    {
        return name.Trim().ToLowerInvariant().Replace("  ", " ");
    }
}
