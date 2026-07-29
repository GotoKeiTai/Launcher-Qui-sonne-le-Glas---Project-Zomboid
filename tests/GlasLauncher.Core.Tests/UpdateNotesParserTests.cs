using GlasLauncher.Core.Logic;
using Xunit;

namespace GlasLauncher.Core.Tests;

public class UpdateNotesParserTests
{
    [Fact]
    public void Parse_SingleBulletLine_ReturnsOneEntryWithoutMarker()
    {
        var result = UpdateNotesParser.Parse("- Correction du crash au lancement");

        Assert.Single(result);
        Assert.Equal("Correction du crash au lancement", result[0]);
    }

    [Fact]
    public void Parse_MultipleBulletLines_ReturnsOneEntryPerLine()
    {
        var result = UpdateNotesParser.Parse("- Première ligne\n- Deuxième ligne\n* Troisième ligne");

        Assert.Equal(3, result.Count);
        Assert.Equal("Première ligne", result[0]);
        Assert.Equal("Deuxième ligne", result[1]);
        Assert.Equal("Troisième ligne", result[2]);
    }

    [Fact]
    public void Parse_WindowsLineEndings_StripsCarriageReturn()
    {
        var result = UpdateNotesParser.Parse("- Première ligne\r\n- Deuxième ligne\r\n");

        Assert.Equal(2, result.Count);
        Assert.Equal("Première ligne", result[0]);
        Assert.Equal("Deuxième ligne", result[1]);
    }

    [Fact]
    public void Parse_BlankLinesBetweenEntries_SkipsEmptyLines()
    {
        var result = UpdateNotesParser.Parse("- Première ligne\n\n\n- Deuxième ligne");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_LineWithoutBulletMarker_ReturnsLineAsIs()
    {
        var result = UpdateNotesParser.Parse("Ligne sans puce");

        Assert.Single(result);
        Assert.Equal("Ligne sans puce", result[0]);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = UpdateNotesParser.Parse("");

        Assert.Empty(result);
    }
}
