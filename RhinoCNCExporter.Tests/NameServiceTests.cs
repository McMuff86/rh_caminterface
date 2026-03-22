using RhinoCNCExporter.Core.Naming;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class NameServiceTests
{
    [Fact]
    public void CreateUnique_FirstCall_ReturnsBaseName()
    {
        var svc = new NameService(maxLength: 31);
        var name = svc.CreateUnique("CUT_1");
        Assert.Equal("CUT_1", name);
    }

    [Fact]
    public void CreateUnique_Trims_And_Appends_Index()
    {
        var svc = new NameService(maxLength: 10);
        var a = svc.CreateUnique("VeryLongName");
        var b = svc.CreateUnique("VeryLongName");
        Assert.True(a.Length <= 10);
        Assert.True(b.Length <= 10);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void CreateUnique_31CharLimit_Maestro()
    {
        var svc = new NameService(maxLength: 31);
        var longName = "POCKET_E010_Z12_S3_D8_O5_ring1_OP_extra_long_suffix";
        var name = svc.CreateUnique(longName);
        Assert.True(name.Length <= 31, $"Name '{name}' exceeds 31 chars (was {name.Length})");
    }

    [Fact]
    public void CreateUnique_Duplicates_GetSuffix()
    {
        var svc = new NameService(maxLength: 31);
        var first = svc.CreateUnique("CUT_1");
        var second = svc.CreateUnique("CUT_1");
        var third = svc.CreateUnique("CUT_1");
        Assert.Equal("CUT_1", first);
        Assert.Equal("CUT_1_2", second);
        Assert.Equal("CUT_1_3", third);
    }

    [Fact]
    public void CreateUnique_Sonderzeichen_Sanitized()
    {
        var svc = new NameService(maxLength: 31);
        var name = svc.CreateUnique("Nut-in-X (test)");
        Assert.DoesNotContain("-", name);
        Assert.DoesNotContain("(", name);
        Assert.DoesNotContain(")", name);
        Assert.DoesNotContain(" ", name);
        // Should be replaced with underscores
        Assert.Contains("_", name);
    }

    [Fact]
    public void CreateUnique_Unicode_Sanitized()
    {
        var svc = new NameService(maxLength: 31);
        var name = svc.CreateUnique("Fräsung_Nüt_Böhr");
        // Non-ASCII chars (ä, ü, ö) should be replaced with _
        Assert.DoesNotContain("ä", name);
        Assert.DoesNotContain("ü", name);
        Assert.DoesNotContain("ö", name);
        Assert.True(name.Length <= 31);
    }

    [Fact]
    public void CreateUnique_EmptyString_Works()
    {
        var svc = new NameService(maxLength: 31);
        var name = svc.CreateUnique("");
        Assert.NotNull(name);
    }

    [Fact]
    public void CreateUnique_ExactLength31_NotTrimmed()
    {
        var svc = new NameService(maxLength: 31);
        var input = "ABCDEFGHIJKLMNOPQRSTUVWXYZ_1234"; // exactly 31 chars
        Assert.Equal(31, input.Length);
        var name = svc.CreateUnique(input);
        Assert.Equal(input, name);
        Assert.Equal(31, name.Length);
    }

    [Fact]
    public void CreateUnique_32Chars_Trimmed()
    {
        var svc = new NameService(maxLength: 31);
        var input = "ABCDEFGHIJKLMNOPQRSTUVWXYZ_12345"; // 32 chars
        Assert.Equal(32, input.Length);
        var name = svc.CreateUnique(input);
        Assert.True(name.Length <= 31);
    }

    [Fact]
    public void CreateUnique_ManyDuplicates_AllUnique()
    {
        var svc = new NameService(maxLength: 31);
        var names = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var name = svc.CreateUnique("DRILL");
            Assert.True(names.Add(name), $"Duplicate name: {name}");
            Assert.True(name.Length <= 31);
        }
    }

    [Fact]
    public void CreateUnique_ShortMaxLength_StillUnique()
    {
        var svc = new NameService(maxLength: 6);
        var a = svc.CreateUnique("ABCDEFGHIJ");
        var b = svc.CreateUnique("ABCDEFGHIJ");
        Assert.True(a.Length <= 6);
        Assert.True(b.Length <= 6);
        Assert.NotEqual(a, b);
    }
}
