using System.Diagnostics;
using System.Text.Json;
using System.CommandLine;

static class Program
{
  public static async Task<int> Main(string[] args)
  {
    RootCommand rootCommand = CreateRootCommand();
    return await rootCommand.Parse(args).InvokeAsync();
  }
  public static RootCommand CreateRootCommand()
  {
    RootCommand rootCommand = new("Resonite Downloader - Download and manage Resonite game files");

    rootCommand.Subcommands.Add(CreateDownloadCommand());
    rootCommand.Subcommands.Add(CreateResolveVersionCommand());

    return rootCommand;
  }

  private static Command CreateDownloadCommand()
  {
    Option<string> gameDirOption = new("--game-dir", "-d")
    {
      Description = "Game installation directory",
      Required = true
    };

    Option<string> steamUserOption = new("--steam-user", "-u")
    {
      Description = "Steam username",
      Required = true
    };

    Option<string> steamPassOption = new("--steam-pass", "-p")
    {
      Description = "Steam password",
      Required = true
    };

    Option<string> betaPassOption = new("--beta-pass", "-b")
    {
      Description = "Beta password",
      Required = false
    };

    Option<string?> versionOption = new("--version", "-v")
    {
      Description = "Specific version to download (defaults to latest)",
      DefaultValueFactory = _ => null
    };

    Option<string?> manifestIdOption = new("--manifest-id", "-m")
    {
      Description = "Specific manifest ID to use (bypasses version resolution)",
      DefaultValueFactory = _ => null
    };

    Option<string> branchOption = new("--branch")
    {
      Description = "Branch to download from",
      DefaultValueFactory = _ => "public"
    };

    Command downloadCommand = new("download", "Download Resonite game files")
        {
            gameDirOption,
            steamUserOption,
            steamPassOption,
            betaPassOption,
            versionOption,
            manifestIdOption,
            branchOption
        };

    downloadCommand.SetAction(async parseResult =>
    {
      string gameDir = parseResult.GetValue(gameDirOption)!;
      string steamUser = parseResult.GetValue(steamUserOption)!;
      string steamPass = parseResult.GetValue(steamPassOption)!;
      string betaPass = parseResult.GetValue(betaPassOption)!;
      string? version = parseResult.GetValue(versionOption);
      string? manifestId = parseResult.GetValue(manifestIdOption);
      string branch = parseResult.GetValue(branchOption)!;

      return await ResoniteDownloader.DownloadCommand(gameDir, steamUser, steamPass, betaPass, version, manifestId, branch);
    });

    return downloadCommand;
  }

  private static Command CreateResolveVersionCommand()
  {
    Option<string?> versionOption = new("--version", "-v")
    {
      Description = "Specific version to check (if not provided, fetches latest)",
      DefaultValueFactory = _ => null
    };

    Option<string?> manifestIdOption = new("--manifest-id", "-m")
    {
      Description = "Specific manifest ID to use (downloads Build.version to resolve)",
      DefaultValueFactory = _ => null
    };

    Option<string?> steamUserOption = new("--steam-user", "-u")
    {
      Description = "Steam username (required when using --manifest-id)",
      DefaultValueFactory = _ => null
    };

    Option<string?> steamPassOption = new("--steam-pass", "-p")
    {
      Description = "Steam password (required when using --manifest-id)",
      DefaultValueFactory = _ => null
    };

    Option<string?> betaPassOption = new("--beta-pass", "-bp")
    {
      Description = "Beta password (for protected branches)",
      DefaultValueFactory = _ => null
    };

    Option<string> branchOption = new("--branch", "-b")
    {
      Description = "Branch to query",
      DefaultValueFactory = _ => "public"
    };

    Command resolveVersionCommand = new("resolve-version", "Resolve and display the version for a given branch")
        {
            versionOption,
            manifestIdOption,
            steamUserOption,
            steamPassOption,
            betaPassOption,
            branchOption
        };

    resolveVersionCommand.SetAction(async parseResult =>
    {
      string? version = parseResult.GetValue(versionOption);
      string? manifestId = parseResult.GetValue(manifestIdOption);
      string? steamUser = parseResult.GetValue(steamUserOption);
      string? steamPass = parseResult.GetValue(steamPassOption);
      string? betaPass = parseResult.GetValue(betaPassOption);
      string branch = parseResult.GetValue(branchOption)!;

      return await ResoniteDownloader.ResolveVersionCommand(version, manifestId, steamUser, steamPass, betaPass, branch);
    });

    return resolveVersionCommand;
  }
}

static class ResoniteDownloader
{
  private const int AppId = 2519830;
  private const int DepotId = 2519832;
  private const string VersionMonitorUrl = "https://raw.githubusercontent.com/resonite-love/resonite-version-monitor/refs/heads/master/data/versions.json";

  public static async Task<int> DownloadCommand(string gameDir, string steamUser, string steamPass,
      string betaPass, string? requestedVersion, string? requestedManifestId, string branch)
  {
    Console.WriteLine("=== Resonite Downloader ===");

    var (version, manifestId) = await ResolveVersion(requestedVersion, requestedManifestId, branch);
    if (version is null)
      return 1;

    Console.WriteLine($"Target version: {version}");
    if (!string.IsNullOrEmpty(manifestId))
      Console.WriteLine($"Manifest ID: {manifestId}");
    Console.WriteLine($"Branch: {branch}");

    // Only use Headless subdirectory for headless branch
    var targetDir = branch == "headless"
        ? Path.Combine(gameDir, "Headless")
        : gameDir;

    if (!await DownloadIfNeeded(gameDir, targetDir, version, manifestId, steamUser, steamPass, betaPass, branch))
      return 1;

    // Read actual version from Build.version if it exists
    var buildVersionFile = Path.Combine(gameDir, "Build.version");
    var actualVersion = version;
    if (File.Exists(buildVersionFile))
    {
      actualVersion = File.ReadAllText(buildVersionFile).Trim();
      if (actualVersion != version)
        Console.WriteLine($"Actual version from Build.version: {actualVersion}");
    }

    // Output for GitHub Actions
    var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
    if (!string.IsNullOrEmpty(githubOutput))
    {
      await File.AppendAllTextAsync(githubOutput,
          $"version={actualVersion}\n" +
          $"build-id={actualVersion.Replace(".", "")}\n");

      if (!string.IsNullOrEmpty(manifestId))
        await File.AppendAllTextAsync(githubOutput, $"manifest-id={manifestId}\n");
    }

    return 0;
  }

  public static async Task<int> ResolveVersionCommand(string? requestedVersion, string? requestedManifestId,
      string? steamUser, string? steamPass, string? betaPass, string branch)
  {
    Console.WriteLine("=== Resonite Version Resolver ===");
    Console.WriteLine($"Branch: {branch}");

    var (version, manifestId) = await ResolveVersion(requestedVersion, requestedManifestId, branch);
    if (version is null)
      return 1;

    // If manifestId was provided, download Build.version to get the actual version
    if (!string.IsNullOrEmpty(requestedManifestId))
    {
      // Validate Steam credentials are provided
      if (string.IsNullOrEmpty(steamUser) || string.IsNullOrEmpty(steamPass))
      {
        Console.Error.WriteLine("ERROR: --steam-user and --steam-pass are required when using --manifest-id");
        return 1;
      }

      Console.WriteLine($"\nDownloading Build.version for manifest {requestedManifestId}...");

      var tempDir = Path.Combine(Path.GetTempPath(), "resonite-version-check-" + Guid.NewGuid().ToString());
      try
      {
        Directory.CreateDirectory(tempDir);

        // Create filelist with just Build.version
        var filelistPath = Path.Combine(tempDir, "files.txt");
        await File.WriteAllTextAsync(filelistPath, "regex:Build\\.version\n");

        // manifestId should not be null here since we passed requestedManifestId to ResolveVersion
        if (string.IsNullOrEmpty(manifestId))
        {
          Console.Error.WriteLine("ERROR: Failed to resolve manifest ID");
          return 1;
        }

        // Download just Build.version using the manifest
        var exitCode = await RunDepotDownloaderWithFilelist(tempDir, filelistPath, manifestId,
            steamUser, steamPass, betaPass ?? "", branch);

        if (exitCode != 0)
        {
          Console.Error.WriteLine("ERROR: Failed to download Build.version");
          return 1;
        }

        var buildVersionFile = Path.Combine(tempDir, "Build.version");
        if (!File.Exists(buildVersionFile))
        {
          Console.Error.WriteLine("ERROR: Build.version not found after download");
          return 1;
        }

        version = File.ReadAllText(buildVersionFile).Trim();
        Console.WriteLine($"\nResolved version: {version}");
        Console.WriteLine($"Manifest ID: {manifestId}");
      }
      finally
      {
        // Clean up temp directory
        try
        {
          if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
          // Ignore cleanup errors
        }
      }
    }
    else
    {
      Console.WriteLine($"\nResolved version: {version}");
      if (!string.IsNullOrEmpty(manifestId))
        Console.WriteLine($"Manifest ID: {manifestId}");
    }

    // Output for GitHub Actions or scripting
    var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
    if (!string.IsNullOrEmpty(githubOutput))
    {
      await File.AppendAllTextAsync(githubOutput,
          $"version={version}\n" +
          $"build-id={version.Replace(".", "")}\n");
      if (!string.IsNullOrEmpty(manifestId))
        await File.AppendAllTextAsync(githubOutput, $"manifest-id={manifestId}\n");
    }

    return 0;
  }

  private static async Task<(string? version, string? manifestId)> ResolveVersion(string? requestedVersion, string? requestedManifestId, string branch)
  {
    // If both version and manifestId are provided manually, use them directly
    if (!string.IsNullOrEmpty(requestedVersion) && !string.IsNullOrEmpty(requestedManifestId))
    {
      Console.WriteLine($"Using requested version: {requestedVersion}");
      Console.WriteLine($"Using requested manifest ID: {requestedManifestId}");
      return (requestedVersion, requestedManifestId);
    }

    // If only manifestId is provided, use it and get version after download from Build.version
    if (!string.IsNullOrEmpty(requestedManifestId))
    {
      Console.WriteLine($"Using requested manifest ID: {requestedManifestId}");
      Console.WriteLine("Version will be determined from Build.version after download");
      return ("manifest-" + requestedManifestId, requestedManifestId);
    }

    // If only version is provided, use it but manifestId will be null
    if (!string.IsNullOrEmpty(requestedVersion))
    {
      Console.WriteLine($"Using requested version: {requestedVersion}");
      return (requestedVersion, null);
    }

    // Neither version nor manifestId provided - fetch latest from API
    Console.WriteLine($"Fetching latest Resonite version for branch '{branch}'...");
    try
    {
      using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
      var json = await http.GetStringAsync(VersionMonitorUrl);
      var data = JsonDocument.Parse(json);

      if (!data.RootElement.TryGetProperty(branch, out var branchArray))
      {
        Console.Error.WriteLine($"ERROR: Version monitor response missing '{branch}' field");
        Console.Error.WriteLine($"Available fields: {string.Join(", ", data.RootElement.EnumerateObject().Select(p => p.Name))}");
        return (null, null);
      }

      var versionEntry = branchArray
          .EnumerateArray()
          .Select(v => new
          {
            Version = v.TryGetProperty("gameVersion", out var gv) ? gv.GetString() : null,
            ManifestId = v.TryGetProperty("manifestId", out var mid) ? mid.GetString() : null
          })
          .Where(v => !string.IsNullOrEmpty(v.Version) && Version.TryParse(v.Version, out _))
          .OrderByDescending(v => Version.Parse(v.Version!))
          .FirstOrDefault();

      if (versionEntry is null || string.IsNullOrEmpty(versionEntry.Version))
      {
        Console.Error.WriteLine($"ERROR: Failed to fetch latest version from resonite-version-monitor for branch '{branch}'");
        return (null, null);
      }

      return (versionEntry.Version, versionEntry.ManifestId);
    }
    catch (HttpRequestException ex)
    {
      Console.Error.WriteLine($"ERROR: Failed to fetch version info: {ex.Message}");
      return (null, null);
    }
    catch (TaskCanceledException)
    {
      Console.Error.WriteLine("ERROR: Timed out fetching version info");
      return (null, null);
    }
    catch (JsonException ex)
    {
      Console.Error.WriteLine($"ERROR: Invalid JSON from version monitor: {ex.Message}");
      return (null, null);
    }
  }

  private static async Task<bool> DownloadIfNeeded(string gameDir, string targetDir, string targetVersion,
      string? manifestId, string steamUser, string steamPass, string betaPass, string branch)
  {
    var buildVersionFile = Path.Combine(gameDir, "Build.version");
    var resoniteDll = Path.Combine(targetDir, "Resonite.dll");

    var installedVersion = File.Exists(buildVersionFile)
        ? File.ReadAllText(buildVersionFile).Trim()
        : null;

    var needsDownload = DetermineIfDownloadNeeded(resoniteDll, installedVersion, targetVersion);

    if (!needsDownload)
      return true;

    Console.WriteLine($"Downloading Resonite {targetVersion} (branch: {branch})...");
    CleanDirectory(gameDir);

    var exitCode = await RunDepotDownloader(gameDir, manifestId, steamUser, steamPass, betaPass, branch);

    if (exitCode != 0)
    {
      Console.Error.WriteLine("ERROR: DepotDownloader failed");
      return false;
    }

    if (!File.Exists(resoniteDll))
    {
      Console.Error.WriteLine("ERROR: Download failed - Resonite.dll not found");
      return false;
    }

    var finalVersion = File.Exists(buildVersionFile)
        ? File.ReadAllText(buildVersionFile).Trim()
        : targetVersion;

    Console.WriteLine($"Resonite {finalVersion} installed successfully");
    return true;
  }

  private static bool DetermineIfDownloadNeeded(string resoniteDll, string? installedVersion, string targetVersion)
  {
    if (!File.Exists(resoniteDll))
    {
      Console.WriteLine("Resonite not found, download required");
      return true;
    }

    if (installedVersion is null)
    {
      Console.WriteLine("No version info found, download required");
      return true;
    }

    if (!Version.TryParse(installedVersion, out var installed) ||
        !Version.TryParse(targetVersion, out var target))
    {
      Console.WriteLine("Unable to parse version, download required");
      return true;
    }

    if (installed >= target)
    {
      Console.WriteLine($"Resonite {installedVersion} already installed [target: {targetVersion}]");
      return false;
    }

    Console.WriteLine($"Upgrade needed: {installedVersion} -> {targetVersion}");
    return true;
  }

  private static void CleanDirectory(string directory)
  {
    if (!Directory.Exists(directory))
      return;

    foreach (var entry in Directory.GetFileSystemEntries(directory))
    {
      try
      {
        if (Directory.Exists(entry))
          Directory.Delete(entry, recursive: true);
        else
          File.Delete(entry);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Warning: Failed to delete {entry}: {ex.Message}");
      }
    }
  }

  private static async Task<int> RunDepotDownloader(string gameDir, string? manifestId, string steamUser, string steamPass, string betaPass, string branch)
  {
    var args = $"-app {AppId} -depot {DepotId} -username {steamUser} -password {steamPass} -dir {gameDir}";

    // Add manifest if provided
    if (!string.IsNullOrEmpty(manifestId))
    {
      args += $" -manifest {manifestId}";
    }

    // Only add beta args if downloading headless branch
    if (branch == "headless")
    {
      args += $" -beta {branch} -betapassword {betaPass}";
    }

    var startInfo = new ProcessStartInfo("DepotDownloader")
    {
      Arguments = args,
      UseShellExecute = false
    };

    var process = Process.Start(startInfo);
    if (process is null)
    {
      Console.Error.WriteLine("ERROR: Failed to start DepotDownloader");
      return -1;
    }

    await process.WaitForExitAsync();
    return process.ExitCode;
  }

  private static async Task<int> RunDepotDownloaderWithFilelist(string gameDir, string filelistPath,
      string manifestId, string steamUser, string steamPass, string betaPass, string branch)
  {
    var args = $"-app {AppId} -depot {DepotId} -manifest {manifestId} -beta {branch} -username {steamUser} -password {steamPass} -dir {gameDir} -filelist \"{filelistPath}\"";

    // Add beta args if downloading headless or other protected branch
    if (!string.IsNullOrEmpty(betaPass))
    {
      args += $" -betapassword {betaPass}";
    }

    var startInfo = new ProcessStartInfo("DepotDownloader")
    {
      Arguments = args,
      UseShellExecute = false
    };

    var process = Process.Start(startInfo);
    if (process is null)
    {
      Console.Error.WriteLine("ERROR: Failed to start DepotDownloader");
      return -1;
    }

    await process.WaitForExitAsync();
    return process.ExitCode;
  }


}
