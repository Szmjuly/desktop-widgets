using DesktopHub.Infrastructure.Scanning;
using Xunit;

namespace DesktopHub.Tests;

public class ScannerTests
{
    private readonly ProjectScanner _scanner;

    public ScannerTests()
    {
        _scanner = new ProjectScanner();
    }

    [Theory]
    [InlineData("2024638.001 Palm Beach Project", "2024", "2024638.001", "638.001", "Palm Beach Project")]
    [InlineData("2023456 Miami Office", "2023", "2023456", "3456", "Miami Office")]
    [InlineData("P250784.00 - Boca Raton", "2025", "P250784.00", "250784", "Boca Raton")]
    [InlineData("P240123 - Test Project", "2024", "P240123", "240123", "Test Project")]
    public void TryParseProjectFolder_ValidFormats_ShouldParse(
        string folderName, 
        string expectedYear, 
        string expectedFullNumber, 
        string expectedShortNumber, 
        string expectedName)
    {
        // Act
        var result = _scanner.TryParseProjectFolder(folderName, $"Q:\\test\\{folderName}", expectedYear);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedFullNumber, result.FullNumber);
        Assert.Equal(expectedShortNumber, result.ShortNumber);
        Assert.Equal(expectedName, result.Name);
        Assert.Equal(expectedYear, result.Year);
    }

    [Theory]
    [InlineData("InvalidFolder")]
    [InlineData("Random Text")]
    [InlineData("123")]
    public void TryParseProjectFolder_InvalidFormats_ShouldReturnNull(string folderName)
    {
        // Act
        var result = _scanner.TryParseProjectFolder(folderName, $"Q:\\test\\{folderName}", "2024");

        // Assert
        Assert.Null(result);
    }
}
