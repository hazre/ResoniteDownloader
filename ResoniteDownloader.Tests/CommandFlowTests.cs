using Xunit;

namespace ResoniteDownloader.Tests;

public class CommandFlowTests
{
  [Fact]
  public async Task DownloadCommand_WhenAlreadyInstalled_ReturnsSuccessWithGitHubOutput()
  {
    var gameDir = CreateTempDir();
    var outputFile = Path.Combine(gameDir, "github_output.txt");
    var previousOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");

    try
    {
      File.WriteAllText(Path.Combine(gameDir, "Resonite.dll"), "x");
      File.WriteAllText(Path.Combine(gameDir, "Build.version"), "2026.2.1.5");
      Environment.SetEnvironmentVariable("GITHUB_OUTPUT", outputFile);

      var method = ReflectionTestHelpers.GetDownloaderMethod("DownloadCommand");
      var task = method.Invoke(null, [gameDir, "user", "pass", "", "2026.2.1.1", "123456", "public"]) as Task<int>;
      Assert.NotNull(task);

      var exitCode = await task!;
      Assert.Equal(0, exitCode);

      var output = File.ReadAllText(outputFile);
      Assert.Contains("version=2026.2.1.5", output);
      Assert.Contains("build-id=2026215", output);
      Assert.Contains("manifest-id=123456", output);
    }
    finally
    {
      Environment.SetEnvironmentVariable("GITHUB_OUTPUT", previousOutput);
      Directory.Delete(gameDir, recursive: true);
    }
  }

  [Fact]
  public async Task DownloadCommand_HeadlessBranchUsesHeadlessSubdirectoryForVersionCheck()
  {
    var gameDir = CreateTempDir();
    var headlessDir = Path.Combine(gameDir, "Headless");
    Directory.CreateDirectory(headlessDir);

    try
    {
      File.WriteAllText(Path.Combine(headlessDir, "Resonite.dll"), "x");
      File.WriteAllText(Path.Combine(gameDir, "Build.version"), "2026.2.1.5");

      var method = ReflectionTestHelpers.GetDownloaderMethod("DownloadCommand");
      var task = method.Invoke(null, [gameDir, "user", "pass", "beta", "2026.2.1.1", "123456", "headless"]) as Task<int>;
      Assert.NotNull(task);

      var exitCode = await task!;
      Assert.Equal(0, exitCode);
    }
    finally
    {
      Directory.Delete(gameDir, recursive: true);
    }
  }

  [Fact]
  public async Task ResolveVersionCommand_WithBothInputs_WritesGitHubOutput()
  {
    var outputFile = Path.Combine(Path.GetTempPath(), "resonite-tests-output-" + Guid.NewGuid() + ".txt");
    var previousOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");

    try
    {
      Environment.SetEnvironmentVariable("GITHUB_OUTPUT", outputFile);

      var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersionCommand");
      var task = method.Invoke(null, ["2026.2.1.7", "987654", null, null, null, "public"]) as Task<int>;
      Assert.NotNull(task);

      var exitCode = await task!;
      Assert.Equal(0, exitCode);

      var output = File.ReadAllText(outputFile);
      Assert.Contains("version=2026.2.1.7", output);
      Assert.Contains("build-id=2026217", output);
      Assert.Contains("manifest-id=987654", output);
    }
    finally
    {
      Environment.SetEnvironmentVariable("GITHUB_OUTPUT", previousOutput);
      if (File.Exists(outputFile))
        File.Delete(outputFile);
    }
  }

  [Fact]
  public async Task ResolveVersionCommand_WithManifestOnlyWithoutCredentials_ReturnsError()
  {
    var method = ReflectionTestHelpers.GetDownloaderMethod("ResolveVersionCommand");
    var task = method.Invoke(null, [null, "987654", null, null, null, "public"]) as Task<int>;
    Assert.NotNull(task);

    var exitCode = await task!;
    Assert.Equal(1, exitCode);
  }

  private static string CreateTempDir()
  {
    var dir = Path.Combine(Path.GetTempPath(), "resonite-tests-" + Guid.NewGuid());
    Directory.CreateDirectory(dir);
    return dir;
  }
}
