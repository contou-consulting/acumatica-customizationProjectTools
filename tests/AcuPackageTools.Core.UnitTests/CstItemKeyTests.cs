using System;
using System.Xml;
using AcuPackageTools.Helper;
using Xunit;

namespace AcuPackageTools.Core.UnitTests;

public class CstItemKeyTests
{
    private static XmlElement Elem(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc.DocumentElement;
    }

    [Fact]
    public void GetKey_File_PrefixesAppRelativePath()
        => Assert.Equal(@"File#Bin\Custom.dll", CstItemKey.GetKey(Elem(@"<File AppRelativePath=""Bin\Custom.dll"" />")));

    [Fact]
    public void GetKey_PerTenantFile_PrefixesAppRelativePath()
        => Assert.Equal(@"PerTenantFile#screens\LS\a.ts", CstItemKey.GetKey(Elem(@"<PerTenantFile AppRelativePath=""screens\LS\a.ts"" />")));

    [Fact]
    public void GetKey_Table_IsBareTableName()
        => Assert.Equal("MyTable", CstItemKey.GetKey(Elem(@"<Table TableName=""MyTable"" />")));

    [Fact]
    public void GetKey_Page_LowercasesPath()
        => Assert.Equal("~/pages/so/so301000.aspx", CstItemKey.GetKey(Elem(@"<Page path=""~/Pages/SO/SO301000.aspx"" />")));

    [Fact]
    public void GetKey_Screen_UppercasesId()
        => Assert.Equal("SCREEN#SO301000", CstItemKey.GetKey(Elem(@"<Screen ID=""so301000"" />")));

    [Fact]
    public void GetKey_EntityEndpoint_CombinesVersionAndName()
        => Assert.Equal("EntityEndpoint#20.200.001§Default",
            CstItemKey.GetKey(Elem(@"<EntityEndpoint Version=""20.200.001"" Name=""Default"" />")));

    [Fact]
    public void GetKey_EntityWithMainTable_ReadsKeyFromFirstRow()
    {
        var e = Elem(
            @"<GenericInquiryScreen>
                <relations main-table=""GIDesign"" />
                <GIDesign><row Name=""MyInquiry"" /></GIDesign>
              </GenericInquiryScreen>");
        Assert.Equal("GenericInquiryScreen#MyInquiry", CstItemKey.GetKey(e));
    }

    [Fact]
    public void GetKey_CompositeEntityKey_JoinsWithSectionSign()
    {
        var e = Elem(
            @"<SharedFilter>
                <relations main-table=""FilterHeader"" />
                <FilterHeader><row FilterName=""My Filter"" ScreenID=""SO301000"" /></FilterHeader>
              </SharedFilter>");
        Assert.Equal("SharedFilter#My Filter§SO301000", CstItemKey.GetKey(e));
    }

    [Fact]
    public void GetKey_UnknownTag_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => CstItemKey.GetKey(Elem("<Bogus />")));
        Assert.Equal("Unknown tag Bogus", ex.Message);
    }

    [Theory]
    [InlineData("lspt1010.ts", "LSPT1010")]
    [InlineData("LSPT1010_extensions.ts", "LSPT1010")]
    [InlineData(@"screens\LS\LSPT1010\LSPT1010.ts", "LSPT1010")]
    [InlineData("abc.ts", null)]
    [InlineData(null, null)]
    public void TryParseScreenId_Cases(string path, string expected)
        => Assert.Equal(expected, CstItemKey.TryParseScreenId(path));
}
