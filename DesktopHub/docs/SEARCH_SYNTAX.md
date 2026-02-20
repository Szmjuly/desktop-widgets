# Search Syntax Guide

> Docs index: `README.md`

## Overview
DesktopHub search supports both simple text search and advanced prefix-based filters. You can combine multiple filters using semicolons.

## Simple Search

### By Project Number
```
2024638          # Full number
638              # Short number (last 3-4 digits)
P250784          # New format
250784           # New format without P prefix
```

### By Project Name
```
Palm Beach       # Exact phrase
palm             # Partial match (fuzzy)
Miami Office     # Multiple words
```

## Prefix Filters

### Location Filter
Filter projects by location/city.

**Syntax:**
```
loc:Miami
location:Miami
```

**Multiple locations:**
```
loc:Miami,Boca,Palm Beach
```

**Combined with search:**
```
palm beach; loc:Miami
```

### Status Filter
Filter projects by status.

**Syntax:**
```
status:Active
status:Completed
status:On Hold
```

**Multiple statuses:**
```
status:Active,Completed
```

### Year Filter
Filter projects by year.

**Syntax:**
```
year:2024
year:2023
```

**Multiple years:**
```
year:2024,2023
```

### Tag Filter
Filter projects by user-defined tags.

**Syntax:**
```
tag:residential
tags:commercial,retail
```

### Team Filter
Filter projects by team or department.

**Syntax:**
```
team:Design
team:Engineering
```

### Favorites Filter
Show only favorite projects.

**Syntax:**
```
fav
favorite
favorites
```

## Combining Filters

### Multiple Filters
Use semicolons to separate filters:

```
loc:Miami; status:Active
loc:Miami; status:Active; year:2024
loc:Miami,Boca; status:Active,Completed
```

### Filters + Text Search
Combine prefix filters with text search:

```
palm beach; loc:Miami
residential; status:Active; year:2024
```

## Examples

### Find Active Projects in Miami
```
loc:Miami; status:Active
```

### Find 2024 Projects Named "Palm Beach"
```
palm beach; year:2024
```

### Find Favorite Residential Projects
```
residential; fav
```

### Find Projects in Multiple Locations
```
loc:Miami,Boca,Palm Beach; status:Active
```

### Find Recent Projects by Number
```
2024; year:2024
```

### Complex Query
```
palm; loc:Miami,Boca; status:Active,Completed; year:2024,2023
```

## Search Behavior

### Fuzzy Matching
The search engine uses fuzzy matching to find projects even with typos:

- `palm` matches "Palm Beach", "Palm Coast", "West Palm"
- `miai` matches "Miami" (typo tolerance)
- `638` matches "2024638.001"

### Relevance Scoring
Results are ranked by relevance:

1. **Exact matches** score highest (1.0)
2. **Contains matches** score high (0.8-0.9)
3. **Starts with matches** score medium (0.7)
4. **Fuzzy matches** score lower (0.5-0.6)
5. **Favorites** get a 10% boost

### Match Priority
When searching, the engine checks in order:

1. Full project number
2. Short project number
3. Project name
4. Location (if metadata exists)

### Case Insensitivity
All searches are case-insensitive:

- `MIAMI` = `miami` = `Miami`
- `PALM BEACH` = `palm beach`

## Tips & Tricks

### Quick Access
- Use short numbers for speed: `638` instead of `2024638.001`
- Mark favorites for instant access: `fav`
- Use location shortcuts: `loc:mia` for Miami

### Efficient Filtering
- Start with broadest filter first: `year:2024; loc:Miami`
- Add text search last: `year:2024; loc:Miami; palm`
- Use comma-separated values for OR logic: `loc:Miami,Boca`

### Keyboard Workflow
1. Press `Ctrl+Alt+Space`
2. Type filter: `loc:Miami; status:Active`
3. Arrow keys to navigate
4. `Enter` to open
5. `Escape` to close

### Common Patterns

**Recent active projects:**
```
status:Active; year:2024
```

**All projects in a location:**
```
loc:Miami
```

**Favorites only:**
```
fav
```

**Projects by team:**
```
team:Design; status:Active
```

**Multi-location search:**
```
loc:Miami,Boca,Palm Beach
```

## Filter Syntax Reference

| Filter | Syntax | Example | Description |
|--------|--------|---------|-------------|
| Location | `loc:` or `location:` | `loc:Miami` | Filter by city/location |
| Status | `status:` | `status:Active` | Filter by project status |
| Year | `year:` | `year:2024` | Filter by project year |
| Tag | `tag:` or `tags:` | `tag:residential` | Filter by user tags |
| Team | `team:` | `team:Design` | Filter by team/department |
| Favorites | `fav`, `favorite`, `favorites` | `fav` | Show only favorites |

## Advanced Usage

### Negation (Coming Soon)
```
loc:Miami; -status:Completed    # All Miami projects except completed
```

### Date Ranges (Coming Soon)
```
date:2024-01-01..2024-12-31
```

### Custom Fields (Coming Soon)
```
client:ABC Corp
budget:>1000000
```

## Troubleshooting

### No Results Found
- Check spelling of filters
- Verify metadata exists (location, status, etc.)
- Try broader search terms
- Remove filters one by one to isolate issue

### Too Many Results
- Add more specific filters
- Use exact project numbers
- Combine multiple filters
- Use favorites filter

### Unexpected Results
- Check for typos in filter names
- Ensure semicolons separate filters
- Verify metadata is set correctly
- Try simpler search first

---

**Pro Tip:** The search box shows live results as you type. Start with broad filters and narrow down by adding more criteria!
