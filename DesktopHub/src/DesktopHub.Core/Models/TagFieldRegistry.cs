namespace DesktopHub.Core.Models;

/// <summary>
/// Central registry of all known tag fields, their aliases, and suggested values.
/// Used by search query parsing and tag editing UI.
/// </summary>
public static class TagFieldRegistry
{
    public static readonly IReadOnlyList<TagFieldDefinition> Fields = new List<TagFieldDefinition>
    {
        // --- Electrical ---
        new()
        {
            Key = "voltage", DisplayName = "Voltage", Category = "Electrical",
            Aliases = new[] { "v", "volt", "volts" },
            SuggestedValues = new[] { "120", "120/208", "120/240", "208", "240", "277", "277/480", "480" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "phase", DisplayName = "Phase", Category = "Electrical",
            Aliases = new[] { "ph" },
            SuggestedValues = new[] { "1", "3" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "amperage_service", DisplayName = "Amperage (Service)", Category = "Electrical",
            Aliases = new[] { "amp", "amps", "service_amp", "service_amps" },
            SuggestedValues = new[] { "100", "200", "400", "600", "800", "1000", "1200", "1600", "2000", "2500", "3000", "4000" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "amperage_generator", DisplayName = "Amperage (Generator)", Category = "Electrical",
            Aliases = new[] { "gen_amp", "gen_amps", "generator_amp" },
            SuggestedValues = new[] { "100", "200", "400", "600", "800", "1000", "1200", "1600", "2000" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "generator_brand", DisplayName = "Generator Brand", Category = "Electrical",
            Aliases = new[] { "gen", "generator", "gen_brand" },
            SuggestedValues = new[] { "Generac", "Kohler", "Cummins", "Caterpillar", "Briggs & Stratton", "Champion" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "generator_load_kw", DisplayName = "Generator Load (kW)", Category = "Electrical",
            Aliases = new[] { "gen_load", "gen_kw", "generator_load" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.FreeText
        },

        // --- HVAC / Mechanical ---
        new()
        {
            Key = "hvac_type", DisplayName = "HVAC Type", Category = "Mechanical",
            Aliases = new[] { "hvac", "ac_type", "cooling" },
            SuggestedValues = new[] { "DX", "Chilled Water", "VRF", "Split", "Package", "Mini-Split", "PTAC", "Geothermal" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "hvac_brand", DisplayName = "HVAC Brand", Category = "Mechanical",
            Aliases = new[] { "ac_brand" },
            SuggestedValues = new[] { "Carrier", "Trane", "Lennox", "Daikin", "Mitsubishi", "York", "Rheem", "Goodman", "McQuay" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "hvac_tonnage", DisplayName = "HVAC Tonnage", Category = "Mechanical",
            Aliases = new[] { "ton", "tons", "tonnage", "ac_ton" },
            SuggestedValues = new[] { "1.5", "2", "2.5", "3", "3.5", "4", "5", "7.5", "10", "15", "20", "25", "30", "40", "50" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "hvac_load_kw", DisplayName = "HVAC Load (kW)", Category = "Mechanical",
            Aliases = new[] { "hvac_load", "ac_load" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.FreeText
        },

        // --- Building ---
        new()
        {
            Key = "square_footage", DisplayName = "Square Footage", Category = "Building",
            Aliases = new[] { "sqft", "sf", "sq_ft", "area" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.FreeText
        },
        new()
        {
            Key = "build_type", DisplayName = "Build Type", Category = "Building",
            Aliases = new[] { "build", "type", "construction" },
            SuggestedValues = new[] { "New", "Renovation", "Addition", "Tenant Improvement", "Shell" },
            InputMode = TagInputMode.Dropdown
        },

        // --- Location ---
        new()
        {
            Key = "location_city", DisplayName = "City", Category = "Location",
            Aliases = new[] { "city" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "location_state", DisplayName = "State", Category = "Location",
            Aliases = new[] { "state", "st" },
            SuggestedValues = new[] { "FL", "CT", "NY", "NJ", "CA", "TX" },
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "location_municipality", DisplayName = "Municipality", Category = "Location",
            Aliases = new[] { "muni", "municipality", "county", "jurisdiction" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "location_address", DisplayName = "Address", Category = "Location",
            Aliases = new[] { "addr", "address" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.FreeText
        },

        // --- People ---
        new()
        {
            Key = "stamping_engineer", DisplayName = "Stamping Engineer", Category = "People",
            Aliases = new[] { "eng", "stamp", "pe", "stamping" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.Dropdown
        },
        new()
        {
            Key = "engineers", DisplayName = "Engineers", Category = "People",
            Aliases = new[] { "team_eng", "project_eng" },
            SuggestedValues = Array.Empty<string>(),
            InputMode = TagInputMode.MultiSelect
        },

        // --- Code ---
        new()
        {
            Key = "code_refs", DisplayName = "Code References", Category = "Code",
            Aliases = new[] { "code", "codes", "ref", "refs" },
            SuggestedValues = new[] { "NEC 2020", "NEC 2023", "FBC 7th", "FBC 8th", "IPC 2021", "FMC 7th", "FBC Energy 7th", "ASHRAE 90.1" },
            InputMode = TagInputMode.MultiSelect
        }
    };

    private static readonly Dictionary<string, TagFieldDefinition> _byKey;
    private static readonly Dictionary<string, TagFieldDefinition> _byAlias;

    static TagFieldRegistry()
    {
        _byKey = new Dictionary<string, TagFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        _byAlias = new Dictionary<string, TagFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in Fields)
        {
            _byKey[field.Key] = field;
            _byAlias[field.Key] = field;
            foreach (var alias in field.Aliases)
                _byAlias[alias] = field;
        }
    }

    /// <summary>
    /// Resolve a user-typed key (or shorthand alias) to the canonical TagFieldDefinition.
    /// Returns null if no match.
    /// </summary>
    public static TagFieldDefinition? Resolve(string keyOrAlias)
    {
        if (string.IsNullOrWhiteSpace(keyOrAlias))
            return null;
        _byAlias.TryGetValue(keyOrAlias.Trim(), out var def);
        return def;
    }

    /// <summary>
    /// Get a field definition by its canonical key.
    /// </summary>
    public static TagFieldDefinition? GetByKey(string key)
    {
        _byKey.TryGetValue(key, out var def);
        return def;
    }

    /// <summary>
    /// Get all distinct category names.
    /// </summary>
    public static IReadOnlyList<string> GetCategories()
        => Fields.Select(f => f.Category ?? "Other").Distinct().ToList();
}
