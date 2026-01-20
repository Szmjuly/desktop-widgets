using ProjectSearcher.Core.Models;
using ProjectSearcher.Infrastructure.Search;
using Xunit;

namespace ProjectSearcher.Tests;

public class SearchServiceTests
{
    private readonly SearchService _searchService;

    public SearchServiceTests()
    {
        _searchService = new SearchService();
    }

    [Fact]
    public void ParseQuery_SimpleText_ShouldReturnSearchText()
    {
        // Act
        var filter = _searchService.ParseQuery("palm beach");

        // Assert
        Assert.Equal("palm beach", filter.SearchText);
        Assert.Empty(filter.Locations);
        Assert.Empty(filter.Statuses);
    }

    [Fact]
    public void ParseQuery_LocationFilter_ShouldParseLocation()
    {
        // Act
        var filter = _searchService.ParseQuery("loc:Miami");

        // Assert
        Assert.Single(filter.Locations);
        Assert.Equal("Miami", filter.Locations[0]);
    }

    [Fact]
    public void ParseQuery_MultipleFilters_ShouldParseAll()
    {
        // Act
        var filter = _searchService.ParseQuery("loc:Miami; status:Active; year:2024");

        // Assert
        Assert.Single(filter.Locations);
        Assert.Equal("Miami", filter.Locations[0]);
        Assert.Single(filter.Statuses);
        Assert.Equal("Active", filter.Statuses[0]);
        Assert.Single(filter.Years);
        Assert.Equal("2024", filter.Years[0]);
    }

    [Fact]
    public void ParseQuery_MixedFiltersAndText_ShouldParseBoth()
    {
        // Act
        var filter = _searchService.ParseQuery("palm beach; loc:Miami; status:Active");

        // Assert
        Assert.Equal("palm beach", filter.SearchText);
        Assert.Single(filter.Locations);
        Assert.Single(filter.Statuses);
    }

    [Theory]
    [InlineData("test", "test", 1.0)]
    [InlineData("test", "testing", 0.8)]
    [InlineData("palm", "palm beach", 0.8)]
    public void CalculateFuzzyScore_ShouldReturnExpectedScore(string source, string target, double minExpectedScore)
    {
        // Act
        var score = _searchService.CalculateFuzzyScore(source, target);

        // Assert
        Assert.True(score >= minExpectedScore, $"Expected score >= {minExpectedScore}, got {score}");
    }

    [Fact]
    public async Task SearchAsync_ShouldRankByRelevance()
    {
        // Arrange
        var projects = new List<Project>
        {
            new() { FullNumber = "2024638", Name = "Palm Beach Project", Path = "Q:\\test1", Year = "2024" },
            new() { FullNumber = "2024639", Name = "Miami Office", Path = "Q:\\test2", Year = "2024" },
            new() { FullNumber = "2024640", Name = "Palm Coast Building", Path = "Q:\\test3", Year = "2024" }
        };

        // Act
        var results = await _searchService.SearchAsync("palm", projects);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results[0].Score >= results[1].Score, "Results should be ranked by score");
    }
}
