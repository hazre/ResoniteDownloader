using System.CommandLine;
using Xunit;

namespace ResoniteDownloader.Tests;

public class CommandDefinitionTests
{
  [Fact]
  public void CreateRootCommand_ContainsExpectedSubcommands()
  {
    var method = ReflectionTestHelpers.GetProgramMethod("CreateRootCommand");
    var root = (RootCommand)method.Invoke(null, null)!;

    var names = root.Subcommands.Select(c => c.Name).ToArray();
    Assert.Contains("download", names);
    Assert.Contains("resolve-version", names);
  }

  [Fact]
  public void DownloadCommand_ParseWithRequiredOptions_HasNoErrors()
  {
    var method = ReflectionTestHelpers.GetProgramMethod("CreateRootCommand");
    var root = (RootCommand)method.Invoke(null, null)!;

    var result = root.Parse(["download", "-d", "C:\\game", "-u", "user", "-p", "pass"]);
    Assert.Empty(result.Errors);
  }

  [Fact]
  public void DownloadCommand_ParseWithoutRequiredOptions_HasErrors()
  {
    var method = ReflectionTestHelpers.GetProgramMethod("CreateRootCommand");
    var root = (RootCommand)method.Invoke(null, null)!;

    var result = root.Parse(["download", "-d", "C:\\game"]);
    Assert.NotEmpty(result.Errors);
  }

  [Fact]
  public void ResolveVersionCommand_ParseWithoutOptions_HasNoErrors()
  {
    var method = ReflectionTestHelpers.GetProgramMethod("CreateRootCommand");
    var root = (RootCommand)method.Invoke(null, null)!;

    var result = root.Parse(["resolve-version"]);
    Assert.Empty(result.Errors);
  }
}
