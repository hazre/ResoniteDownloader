using System.Diagnostics;
using System.CommandLine;
using System.Text.Json;

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

    // If no explicit version was provided, resolve the actual version from Build.version.
    // For "latest", this asks DepotDownloader for the latest depot state.
    var shouldDownloadBuildVersion = string.IsNullOrEmpty(requestedVersion);
    var hasSteamCredentials = !string.IsNullOrEmpty(steamUser) && !string.IsNullOrEmpty(steamPass);

    if (shouldDownloadBuildVersion && hasSteamCredentials)
    {
      var label = string.IsNullOrEmpty(requestedManifestId) ? "latest build" : $"manifest {requestedManifestId}";
      Console.WriteLine($"\nDownloading Build.version for {label}...");

      var buildVersion = await DownloadBuildVersion(
          requestedManifestId,
          steamUser!,
          steamPass!,
          betaPass ?? "",
          branch);

      if (buildVersion is null)
      {
        Console.Error.WriteLine("ERROR: Failed to resolve version from Build.version");
        return 1;
      }

      version = buildVersion;
      Console.WriteLine($"\nResolved version: {version}");
      if (!string.IsNullOrEmpty(manifestId))
        Console.WriteLine($"Manifest ID: {manifestId}");
    }
    else if (shouldDownloadBuildVersion && !string.IsNullOrEmpty(requestedManifestId))
    {
      Console.Error.WriteLine("ERROR: --steam-user and --steam-pass are required when using --manifest-id");
      return 1;
    }
    else if (shouldDownloadBuildVersion)
    {
      Console.WriteLine("\nSteam credentials were not provided. Falling back to version monitor for latest version.");

      var latestFromMonitor = await ResolveLatestFromMonitor(branch);
      if (latestFromMonitor.version is null)
        return 1;

      version = latestFromMonitor.version;
      manifestId = latestFromMonitor.manifestId;

      Console.WriteLine($"\nResolved version: {version}");
      if (!string.IsNullOrEmpty(manifestId))
        Console.WriteLine($"Manifest ID: {manifestId}");
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

    // If only version is provided, resolve its manifest ID from version monitor.
    // DepotDownloader only resolves latest automatically when no manifest is supplied.
    if (!string.IsNullOrEmpty(requestedVersion))
    {
      Console.WriteLine($"Resolving manifest ID for requested version '{requestedVersion}' on branch '{branch}'...");
      try
      {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var json = await http.GetStringAsync(VersionMonitorUrl);
        var (version, manifestId) = ResolveVersionFromMonitorJson(json, branch, requestedVersion);

        if (string.IsNullOrEmpty(manifestId))
        {
          Console.Error.WriteLine($"ERROR: Could not find manifest ID for version '{requestedVersion}' on branch '{branch}'");
          return (null, null);
        }

        Console.WriteLine($"Resolved manifest ID: {manifestId}");
        return (version, manifestId);
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

    // Neither version nor manifestId provided - use latest directly from Steam/depot
    Console.WriteLine($"No version or manifest provided for branch '{branch}', using latest");
    return ("latest", null);
  }

  private static (string? version, string? manifestId) ResolveVersionFromMonitorJson(string json, string branch, string requestedVersion)
  {
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
      .FirstOrDefault(v =>
        !string.IsNullOrEmpty(v.Version) &&
        string.Equals(v.Version, requestedVersion, StringComparison.Ordinal));

    if (versionEntry is null)
      return (null, null);

    return (versionEntry.Version, versionEntry.ManifestId);
  }

  private static async Task<(string? version, string? manifestId)> ResolveLatestFromMonitor(string branch)
  {
    Console.WriteLine($"Fetching latest Resonite version for branch '{branch}' from version monitor...");
    try
    {
      using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
      var json = await http.GetStringAsync(VersionMonitorUrl);
      var (version, manifestId) = ResolveLatestFromMonitorJson(json, branch);

      if (string.IsNullOrEmpty(version))
      {
        Console.Error.WriteLine($"ERROR: Failed to fetch latest version from resonite-version-monitor for branch '{branch}'");
        return (null, null);
      }

      return (version, manifestId);
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

  private static (string? version, string? manifestId) ResolveLatestFromMonitorJson(string json, string branch)
  {
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

    if (versionEntry is null)
      return (null, null);

    return (versionEntry.Version, versionEntry.ManifestId);
  }

  private static async Task<string?> DownloadBuildVersion(string? manifestId, string steamUser, string steamPass, string betaPass, string branch)
  {
    var tempDir = Path.Combine(Path.GetTempPath(), "resonite-version-check-" + Guid.NewGuid());
    try
    {
      Directory.CreateDirectory(tempDir);

      var filelistPath = Path.Combine(tempDir, "files.txt");
      await File.WriteAllTextAsync(filelistPath, "regex:Build\\.version\n");

      var exitCode = await RunDepotDownloaderWithFilelist(tempDir, filelistPath, manifestId, steamUser, steamPass, betaPass, branch);
      if (exitCode != 0)
        return null;

      var buildVersionFile = Path.Combine(tempDir, "Build.version");
      if (!File.Exists(buildVersionFile))
        return null;

      return File.ReadAllText(buildVersionFile).Trim();
    }
    finally
    {
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

  private static async Task<bool> DownloadIfNeeded(string gameDir, string targetDir, string targetVersion,
      string? manifestId, string steamUser, string steamPass, string betaPass, string branch)
  {
    var buildVersionFile = Path.Combine(gameDir, "Build.version");
    var resoniteExe = Path.Combine(targetDir, "Resonite.exe");

    var installedVersion = File.Exists(buildVersionFile)
        ? File.ReadAllText(buildVersionFile).Trim()
        : null;

    var needsDownload = DetermineIfDownloadNeeded(resoniteExe, installedVersion, targetVersion);

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

    if (!File.Exists(resoniteExe))
    {
      Console.Error.WriteLine("ERROR: Download failed - Resonite.exe not found");
      return false;
    }

    var finalVersion = File.Exists(buildVersionFile)
        ? File.ReadAllText(buildVersionFile).Trim()
        : targetVersion;

    Console.WriteLine($"Resonite {finalVersion} installed successfully");
    return true;
  }

  private static bool DetermineIfDownloadNeeded(string resoniteExe, string? installedVersion, string targetVersion)
  {
    if (!File.Exists(resoniteExe))
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
    var args = BuildDepotDownloaderArgs(gameDir, steamUser, steamPass, betaPass, branch, manifestId);

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
      string? manifestId, string steamUser, string steamPass, string betaPass, string branch)
  {
    var args = BuildDepotDownloaderArgs(gameDir, steamUser, steamPass, betaPass, branch, manifestId, filelistPath);

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

  private static string BuildDepotDownloaderArgs(string gameDir, string steamUser, string steamPass, string betaPass, string branch, string? manifestId = null, string? filelistPath = null)
  {
    var args = $"-app {AppId} -depot {DepotId} -beta {branch} -username {steamUser} -password {steamPass} -dir {gameDir}";

    if (!string.IsNullOrEmpty(manifestId))
    {
      args += $" -manifest {manifestId}";
    }

    if (!string.IsNullOrEmpty(betaPass))
    {
      args += $" -betapassword {betaPass}";
    }

    if (!string.IsNullOrEmpty(filelistPath))
    {
      args += $" -filelist \"{filelistPath}\"";
    }

    return args;
  }
}
