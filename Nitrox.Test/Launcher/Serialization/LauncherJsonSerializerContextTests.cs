using System.Text.Json;
using Nitrox.Launcher.Models.Serialization;
using Nitrox.Launcher.Models.Services;
using Nitrox.Model.Serialization;

namespace Nitrox.Launcher.Serialization;

[TestClass]
public class LauncherJsonSerializerContextTests
{
    [TestMethod]
    public void BackupMetadata()
    {
        // Arrange
        BackupMetadata metadata = new()
        {
            NitroxVersion = "1.2.3",
            BackupDate = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc),
            IncludesSaves = true,
            InstallationPath = "/games/nitrox",
            SavesPath = "/games/subnautica/saves"
        };

        const string JSON = """{"NitroxVersion":"1.2.3","BackupDate":"2026-11-14T12:00:00Z","IncludesSaves":true,"InstallationPath":"/games/nitrox","SavesPath":"/games/subnautica/saves"}""";

        // Act
        BackupMetadata? result = JsonSerializer.Deserialize(JSON, LauncherJsonSerializerContext.Default.BackupMetadata);
        string serializedJson = JsonSerializer.Serialize(result, LauncherJsonSerializerContext.Default.BackupMetadata);

        // Assert
        result.Should().BeEquivalentTo(metadata);
        serializedJson.Should().Be(JSON);
    }

    [TestMethod]
    public void SaveFileVersion()
    {
        // Arrange
        SaveFileVersion version = new(new Version(1, 2, 3, 4), 7);

        const string JSON = """{"Major":1,"Minor":2,"Build":3,"Revision":4,"SaveFormat":7}""";

        // Act
        SaveFileVersion? result = JsonSerializer.Deserialize(JSON, LauncherJsonSerializerContext.Default.SaveFileVersion);
        string serializedJson = JsonSerializer.Serialize(result, LauncherJsonSerializerContext.Default.SaveFileVersion);

        // Assert
        result.Should().BeEquivalentTo(version);
        serializedJson.Should().Be(JSON);
    }

    [TestMethod]
    public void SaveFileVersionFromBeforeTheSaveFormatHasFormatZero()
    {
        SaveFileVersion? result = JsonSerializer.Deserialize("""{"Major":1,"Minor":9,"Build":0,"Revision":0}""", LauncherJsonSerializerContext.Default.SaveFileVersion);

        result!.SaveFormat.Should().Be(NitroxSaveFormat.UNKNOWN);
    }

    [TestMethod]
    public void BlogPostArray()
    {
        // Arrange
        NitroxBlogService.BlogPost[] posts =
        [
            new()
            {
                Released = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero),
                PostUrl = "https://example.test/post",
                Title = new() { Rendered = "Release notes" },
                ThumbnailImageUrl = "https://example.test/image.png"
            }
        ];

        const string JSON = """[{"date":"2026-11-14T12:00:00+00:00","link":"https://example.test/post","title":{"rendered":"Release notes"},"jetpack_featured_media_url":"https://example.test/image.png"}]""";

        // Act
        NitroxBlogService.BlogPost[]? result = JsonSerializer.Deserialize(JSON, LauncherJsonSerializerContext.Default.BlogPostArray);
        string serializedJson = JsonSerializer.Serialize(result, LauncherJsonSerializerContext.Default.BlogPostArray);

        // Assert
        result.Should().BeEquivalentTo(posts);
        serializedJson.Should().Be(JSON);
    }
}
