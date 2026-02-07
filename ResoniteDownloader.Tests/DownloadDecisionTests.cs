using System.Reflection;
using Xunit;

namespace ResoniteDownloader.Tests;

public class DownloadDecisionTests
{
  [Fact]
  public void DetermineIfDownloadNeeded_WhenDllMissing_ReturnsTrue()
  {
    var method = ReflectionTestHelpers.GetDownloaderMethod("DetermineIfDownloadNeeded");
    var result = (bool)method.Invoke(null, ["C:\\does-not-exist\\Resonite.dll", "2026.2.1.1", "2026.2.1.1"])!;
    Assert.True(result);
  }

  [Fact]
  public void DetermineIfDownloadNeeded_WhenInstalledVersionMissing_ReturnsTrue()
  {
    var dir = CreateTempDir();
    try
    {
      var dllPath = Path.Combine(dir, "Resonite.dll");
      File.WriteAllText(dllPath, "x");

      var method = ReflectionTestHelpers.GetDownloaderMethod("DetermineIfDownloadNeeded");
      var result = (bool)method.Invoke(null, [dllPath, null, "2026.2.1.1"])!;
      Assert.True(result);
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void DetermineIfDownloadNeeded_WhenInstalledVersionIsInvalid_ReturnsTrue()
  {
    var dir = CreateTempDir();
    try
    {
      var dllPath = Path.Combine(dir, "Resonite.dll");
      File.WriteAllText(dllPath, "x");

      var method = ReflectionTestHelpers.GetDownloaderMethod("DetermineIfDownloadNeeded");
      var result = (bool)method.Invoke(null, [dllPath, "invalid", "2026.2.1.1"])!;
      Assert.True(result);
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void DetermineIfDownloadNeeded_WhenTargetVersionIsInvalid_ReturnsTrue()
  {
    var dir = CreateTempDir();
    try
    {
      var dllPath = Path.Combine(dir, "Resonite.dll");
      File.WriteAllText(dllPath, "x");

      var method = ReflectionTestHelpers.GetDownloaderMethod("DetermineIfDownloadNeeded");
      var result = (bool)method.Invoke(null, [dllPath, "2026.2.1.1", "invalid"])!;
      Assert.True(result);
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void DetermineIfDownloadNeeded_WhenInstalledEqualsTarget_ReturnsFalse()
  {
    var dir = CreateTempDir();
    try
    {
      var dllPath = Path.Combine(dir, "Resonite.dll");
      File.WriteAllText(dllPath, "x");

      var method = ReflectionTestHelpers.GetDownloaderMethod("DetermineIfDownloadNeeded");
      var result = (bool)method.Invoke(null, [dllPath, "2026.2.1.1", "2026.2.1.1"])!;
      Assert.False(result);
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void DetermineIfDownloadNeeded_WhenInstalledAboveTarget_ReturnsFalse()
  {
    var dir = CreateTempDir();
    try
    {
      var dllPath = Path.Combine(dir, "Resonite.dll");
      File.WriteAllText(dllPath, "x");

      var method = ReflectionTestHelpers.GetDownloaderMethod("DetermineIfDownloadNeeded");
      var result = (bool)method.Invoke(null, [dllPath, "2026.2.1.2", "2026.2.1.1"])!;
      Assert.False(result);
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void DetermineIfDownloadNeeded_WhenInstalledBelowTarget_ReturnsTrue()
  {
    var dir = CreateTempDir();
    try
    {
      var dllPath = Path.Combine(dir, "Resonite.dll");
      File.WriteAllText(dllPath, "x");

      var method = ReflectionTestHelpers.GetDownloaderMethod("DetermineIfDownloadNeeded");
      var result = (bool)method.Invoke(null, [dllPath, "2026.2.1.1", "2026.2.1.2"])!;
      Assert.True(result);
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void CleanDirectory_WhenDirectoryExists_RemovesContents()
  {
    var dir = CreateTempDir();
    try
    {
      File.WriteAllText(Path.Combine(dir, "file.txt"), "x");
      var nested = Path.Combine(dir, "nested");
      Directory.CreateDirectory(nested);
      File.WriteAllText(Path.Combine(nested, "inner.txt"), "x");

      var method = ReflectionTestHelpers.GetDownloaderMethod("CleanDirectory");
      method.Invoke(null, [dir]);

      Assert.Empty(Directory.GetFileSystemEntries(dir));
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void CleanDirectory_WhenDirectoryMissing_DoesNotThrow()
  {
    var method = ReflectionTestHelpers.GetDownloaderMethod("CleanDirectory");
    method.Invoke(null, [Path.Combine(Path.GetTempPath(), "resonite-tests-missing-" + Guid.NewGuid())]);
  }

  private static string CreateTempDir()
  {
    var dir = Path.Combine(Path.GetTempPath(), "resonite-tests-" + Guid.NewGuid());
    Directory.CreateDirectory(dir);
    return dir;
  }
}
