using RhinoCNCExporter.Core.Naming;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class NameServiceTests
{
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
}
