using Xunit;

namespace ResoniteDownloader.Tests;

public class ResolveVersionTests
{
  [Fact]
  public async Task ResolveVersion_WhenNoVersionOrManifestProvided_ReturnsLatestMarker()
  {
    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersion");
    var task = method!.Invoke(null, [null, null, "public"]) as Task<(string? version, string? manifestId)>;
    Assert.NotNull(task);

    var (version, manifestId) = await task!;

    Assert.Equal("latest", version);
    Assert.Null(manifestId);
  }

  [Fact]
  public async Task ResolveVersion_WhenBothInputsProvided_ReturnsInputsDirectly()
  {
    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersion");
    var task = method.Invoke(null, ["2026.2.1.10", "123456", "public"]) as Task<(string? version, string? manifestId)>;
    Assert.NotNull(task);

    var (version, manifestId) = await task!;

    Assert.Equal("2026.2.1.10", version);
    Assert.Equal("123456", manifestId);
  }

  [Fact]
  public async Task ResolveVersion_WhenOnlyManifestProvided_ReturnsManifestPlaceholderVersion()
  {
    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersion");
    var task = method.Invoke(null, [null, "999999", "public"]) as Task<(string? version, string? manifestId)>;
    Assert.NotNull(task);

    var (version, manifestId) = await task!;

    Assert.Equal("manifest-999999", version);
    Assert.Equal("999999", manifestId);
  }

  [Fact]
  public void ResolveVersionMonitorJson_WhenVersionExists_ReturnsManifestId()
  {
    const string monitorJson = """
      {
        "public": [
          { "gameVersion": "2026.2.1.1", "manifestId": "1111111111111111111" },
          { "gameVersion": "2026.2.1.2", "manifestId": "2222222222222222222" }
        ],
        "headless": [
          { "gameVersion": "2026.2.1.2", "manifestId": "3333333333333333333" }
        ]
      }
      """;

    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersionFromMonitorJson");

    var result = ((string? version, string? manifestId))method!.Invoke(null, [monitorJson, "public", "2026.2.1.2"])!;

    Assert.Equal("2026.2.1.2", result.version);
    Assert.Equal("2222222222222222222", result.manifestId);
  }

  [Fact]
  public void ResolveVersionMonitorJson_WhenVersionMissing_ReturnsNullPair()
  {
    const string monitorJson = """
      { "public": [ { "gameVersion": "2026.2.1.1", "manifestId": "111" } ] }
      """;

    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersionFromMonitorJson");
    var result = ((string? version, string? manifestId))method.Invoke(null, [monitorJson, "public", "2026.2.1.9"])!;

    Assert.Null(result.version);
    Assert.Null(result.manifestId);
  }

  [Fact]
  public void ResolveVersionMonitorJson_WhenBranchMissing_ReturnsNullPair()
  {
    const string monitorJson = """
      { "headless": [ { "gameVersion": "2026.2.1.1", "manifestId": "111" } ] }
      """;

    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersionFromMonitorJson");
    var result = ((string? version, string? manifestId))method.Invoke(null, [monitorJson, "public", "2026.2.1.1"])!;

    Assert.Null(result.version);
    Assert.Null(result.manifestId);
  }

  [Fact]
  public void ResolveLatestFromMonitorJson_WhenBranchHasVersions_ReturnsHighestEntry()
  {
    const string monitorJson = """
      {
        "public": [
          { "gameVersion": "2026.2.1.1", "manifestId": "1111111111111111111" },
          { "gameVersion": "2026.2.1.4", "manifestId": "4444444444444444444" },
          { "gameVersion": "2026.2.1.3", "manifestId": "3333333333333333333" }
        ]
      }
      """;

    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveLatestFromMonitorJson");

    var result = ((string? version, string? manifestId))method!.Invoke(null, [monitorJson, "public"])!;

    Assert.Equal("2026.2.1.4", result.version);
    Assert.Equal("4444444444444444444", result.manifestId);
  }

  [Fact]
  public void ResolveLatestFromMonitorJson_WhenBranchMissing_ReturnsNullPair()
  {
    const string monitorJson = """
      { "headless": [ { "gameVersion": "2026.2.1.4", "manifestId": "444" } ] }
      """;

    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveLatestFromMonitorJson");
    var result = ((string? version, string? manifestId))method.Invoke(null, [monitorJson, "public"])!;

    Assert.Null(result.version);
    Assert.Null(result.manifestId);
  }

  [Fact]
  public void ResolveLatestFromMonitorJson_IgnoresInvalidVersions()
  {
    const string monitorJson = """
      {
        "public": [
          { "gameVersion": "not-a-version", "manifestId": "111" },
          { "gameVersion": "2026.2.1.2", "manifestId": "222" }
        ]
      }
      """;

    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveLatestFromMonitorJson");
    var result = ((string? version, string? manifestId))method.Invoke(null, [monitorJson, "public"])!;

    Assert.Equal("2026.2.1.2", result.version);
    Assert.Equal("222", result.manifestId);
  }
}
