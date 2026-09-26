using System.Text.Encodings.Web;
using System.Text.Json;
using YouLoader.Core.Services;

namespace YouLoader.Tests;

/// <summary>
/// The website's “Common problems” page is built from website/data/problems.json, which is exported from
/// the app's own explanations. This keeps the two in step: change an explanation, and this test fails
/// until the file is regenerated with YOULOADER_UPDATE_PROBLEMS=1.
/// </summary>
public class ProblemCatalogueTests
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static string ExportPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "YouLoader.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "website", "data", "problems.json");
    }

    static string Export() => JsonSerializer.Serialize(
        ErrorMessages.Catalogue.Select(p => new
        {
            p.Id,
            p.Category,
            p.Explanation.Summary,
            p.Explanation.Cause,
            p.Explanation.Suggestions,
        }),
        Json).ReplaceLineEndings("\n") + "\n";

    [Fact]
    public void WebsiteCopyMatchesTheApp()
    {
        var path = ExportPath();
        var expected = Export();

        if (Environment.GetEnvironmentVariable("YOULOADER_UPDATE_PROBLEMS") == "1")
            File.WriteAllText(path, expected);

        Assert.True(File.Exists(path), "Run the tests once with YOULOADER_UPDATE_PROBLEMS=1 to create website/data/problems.json.");
        Assert.True(
            File.ReadAllText(path).ReplaceLineEndings("\n") == expected,
            "website/data/problems.json is out of date. Run the tests with YOULOADER_UPDATE_PROBLEMS=1 to regenerate it.");
    }

    [Fact]
    public void EveryProblemHasAUniqueAnchorAndAKnownSection()
    {
        string[] sections = ["video", "youtube", "saving", "converting", "connection", "app"];
        var ids = ErrorMessages.Catalogue.Select(p => p.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", id));
        Assert.All(ErrorMessages.Catalogue, p => Assert.Contains(p.Category, sections));
        Assert.All(ErrorMessages.Catalogue, p => Assert.NotEmpty(p.Explanation.Suggestions));
    }
}
