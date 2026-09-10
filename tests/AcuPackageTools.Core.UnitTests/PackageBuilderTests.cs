using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using AcuPackageTools.Helper;
using Xunit;

namespace AcuPackageTools.Core.UnitTests;

public class PackageBuilderTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "AcuPackageToolsTests_" + Guid.NewGuid().ToString("N"));

    private static string TestPackagePath
        => Path.Combine(AppContext.BaseDirectory, "TestResources", "TestPackage");

    public PackageBuilderTests() => Directory.CreateDirectory(_workDir);

    public void Dispose() => Directory.Delete(_workDir, recursive: true);

    private string BuildToZip(string customizationPath, string description = "Built", int? level = 1, string productVersion = "20.200")
    {
        string zipPath = Path.Combine(_workDir, Guid.NewGuid().ToString("N") + ".zip");
        PackageBuilder.BuildCustomizationPackage(customizationPath, zipPath, description, level, productVersion);
        return zipPath;
    }

    private static XmlDocument ReadProjectXml(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("project.xml");
        Assert.NotNull(entry);
        var doc = new XmlDocument();
        using var stream = entry.Open();
        doc.Load(stream);
        return doc;
    }

    [Fact]
    public void TestPackage_BuildsZip_WithOnlyProjectXml_AndRootAttributes()
    {
        string zipPath = BuildToZip(TestPackagePath);

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            Assert.Equal(new[] { "project.xml" }, zip.Entries.Select(e => e.FullName));
        }

        var root = ReadProjectXml(zipPath).DocumentElement;
        Assert.Equal("Customization", root.Name);
        Assert.Equal("1", root.GetAttribute("level"));
        Assert.Equal("Built", root.GetAttribute("description"));
        Assert.Equal("20.200", root.GetAttribute("product-version"));
    }

    [Fact]
    public void TestPackage_NullArguments_FallBackToMetadata()
    {
        string zipPath = BuildToZip(TestPackagePath, description: null, level: null);

        var root = ReadProjectXml(zipPath).DocumentElement;
        Assert.Equal("", root.GetAttribute("level"));
        Assert.Equal("", root.GetAttribute("description"));
    }

    [Fact]
    public void ContentFiles_BecomeFileAndPerTenantFileItems()
    {
        string source = Path.Combine(_workDir, "src");
        Directory.CreateDirectory(Path.Combine(source, "Bin"));
        File.WriteAllText(Path.Combine(source, "Bin", "Custom.dll"), "binary");
        string devDir = Path.Combine(source, @"FrontendSources\screen\src\development\screens\LS\LSPT1010");
        Directory.CreateDirectory(devDir);
        File.WriteAllText(Path.Combine(devDir, "LSPT1010.ts"), "export {};");

        string zipPath = BuildToZip(source);

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            Assert.Equal(
                new[] { "project.xml", "Bin/Custom.dll", "screens/LS/LSPT1010/LSPT1010.ts" },
                zip.Entries.Select(e => e.FullName));
        }

        var root = ReadProjectXml(zipPath).DocumentElement;
        var file = root.SelectSingleNode("File") as XmlElement;
        Assert.NotNull(file);
        Assert.Equal(@"Bin\Custom.dll", file.GetAttribute("AppRelativePath"));
        var perTenant = root.SelectSingleNode("PerTenantFile") as XmlElement;
        Assert.NotNull(perTenant);
        Assert.Equal(@"screens\LS\LSPT1010\LSPT1010.ts", perTenant.GetAttribute("AppRelativePath"));
        Assert.Equal("LSPT1010", perTenant.GetAttribute("ScreenId"));
    }

    [Fact]
    public void ProjectFragments_AreMergedAndProjectMetadataExcluded()
    {
        string source = Path.Combine(_workDir, "src2");
        string projectDir = Path.Combine(source, "_project");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "Sql.xml"), @"<Sql TableName=""MyProc"" CustomScript=""select 1"" />");
        File.WriteAllText(Path.Combine(projectDir, "ProjectMetadata.xml"),
            @"<project name=""p"" level=""3"" description=""from metadata"" />");

        string zipPath = BuildToZip(source, description: null, level: null);

        var root = ReadProjectXml(zipPath).DocumentElement;
        Assert.Equal("3", root.GetAttribute("level"));
        Assert.Equal("from metadata", root.GetAttribute("description"));
        var sql = root.SelectSingleNode("Sql") as XmlElement;
        Assert.NotNull(sql);
        Assert.Equal("MyProc", sql.GetAttribute("TableName"));
    }
}
