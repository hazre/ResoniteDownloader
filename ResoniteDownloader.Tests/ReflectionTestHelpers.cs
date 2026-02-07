using System.Reflection;
using Xunit;

namespace ResoniteDownloader.Tests;

internal static class ReflectionTestHelpers
{
  internal static MethodInfo GetDownloaderMethod(string methodName)
  {
    var assembly = Assembly.Load("ResoniteDownloader");
    var type = assembly.GetType("ResoniteDownloader");
    var method = type?.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
    Assert.NotNull(method);
    return method!;
  }

  internal static MethodInfo GetProgramMethod(string methodName)
  {
    var assembly = Assembly.Load("ResoniteDownloader");
    var type = assembly.GetType("Program");
    var method = type?.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
    Assert.NotNull(method);
    return method!;
  }
}
