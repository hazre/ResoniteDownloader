using System.Reflection;
using Xunit;

namespace ResoniteDownloader.Tests;

public class DepotDownloaderArgsTests
{
  [Fact]
  public void BuildDepotDownloaderArgs_WithManifestForHeadlessBranch_IncludesManifestBetaFlags()
  {
    var method = GetMethod("BuildDepotDownloaderArgs");
    var args = (string)method.Invoke(null, ["C:\\game", "user", "pass", "betaPass", "headless", "123", null])!;

    Assert.Contains("-manifest 123", args);
    Assert.Contains("-beta headless", args);
    Assert.Contains("-betapassword betaPass", args);
    Assert.DoesNotContain("-filelist", args);
  }

  [Fact]
  public void BuildDepotDownloaderArgs_PublicBranchWithoutOptionalArgs_OnlyIncludesRequiredArgs()
  {
    var method = GetMethod("BuildDepotDownloaderArgs");
    var args = (string)method.Invoke(null, ["C:\\game", "user", "pass", "", "public", null, null])!;

    Assert.DoesNotContain("-manifest", args);
    Assert.DoesNotContain("-betapassword", args);
    Assert.DoesNotContain("-filelist", args);
    Assert.Contains("-beta public", args);
  }

  [Fact]
  public void BuildDepotDownloaderArgs_WithFilelistForProtectedBranch_IncludesFilelistOptionalManifest()
  {
    var method = GetMethod("BuildDepotDownloaderArgs");
    var args = (string)method.Invoke(null, ["C:\\game", "user", "pass", "secret", "headless", "999", "C:\\game\\files.txt"])!;

    Assert.Contains("-beta headless", args);
    Assert.Contains("-manifest 999", args);
    Assert.Contains("-betapassword secret", args);
    Assert.Contains("-filelist \"C:\\game\\files.txt\"", args);
  }

  private static MethodInfo GetMethod(string name)
  {
    return ReflectionTestHelpers.GetDownloaderMethod(name);
  }
}
