using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SvgCreator.Core.Tests.Configuration;

public sealed class OpenCvSharpRuntimeTests
{
    // SvgCreatorCoreProject_UsesCrossPlatformOpenCvSharpRuntime の挙動を検証します。
    [Fact]
    public void SvgCreatorCoreProject_UsesCrossPlatformOpenCvSharpRuntime()
    {
        var projectFile = GetSvgCreatorCoreProjectFile();
        Assert.True(File.Exists(projectFile), $"Project file not found at path '{projectFile}'.");

        var document = XDocument.Load(projectFile);
        var packageReferences = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(
            packageReferences,
            element => string.Equals(element.Attribute("Include")?.Value, "OpenCvSharp4.runtime.win", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            packageReferences,
            element => string.Equals(element.Attribute("Include")?.Value, "OpenCvSharp4.runtime.linux-x64", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSvgCreatorCoreProjectFile()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var rootDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "../../../../../"));
        return Path.Combine(rootDirectory, "src", "SvgCreator.Core", "SvgCreator.Core.csproj");
    }
}
