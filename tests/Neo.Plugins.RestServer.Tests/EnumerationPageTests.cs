using Neo.Plugins.RestServer.Exceptions;
using Neo.Plugins.RestServer.Helpers;

namespace Neo.Plugins.RestServer.Tests;

[TestClass]
public class EnumerationPageTests
{
    [TestMethod]
    public void GetOffset_AcceptsPagesWithinBudget()
    {
        RestServerSettings.Load(TestUtility.CreateConfigurationFromJson("{}").GetSection("PluginConfiguration"));
        Assert.AreEqual(0, EnumerationPage.GetOffset(1, 50));
        Assert.AreEqual(950, EnumerationPage.GetOffset(20, 50));
        Assert.AreEqual(995, EnumerationPage.GetOffset(200, 5, 5));
    }

    [TestMethod]
    public void GetOffset_RejectsOversizedAndOverflowingPages()
    {
        RestServerSettings.Load(TestUtility.CreateConfigurationFromJson("{}").GetSection("PluginConfiguration"));
        Assert.ThrowsExactly<InvalidParameterRangeException>(() => EnumerationPage.GetOffset(21, 50));
        Assert.ThrowsExactly<InvalidParameterRangeException>(() => EnumerationPage.GetOffset(int.MaxValue, 50));
        Assert.ThrowsExactly<InvalidParameterRangeException>(() => EnumerationPage.GetOffset(1, 51));
        Assert.ThrowsExactly<InvalidParameterRangeException>(() => EnumerationPage.GetOffset(1, 6, 5));
        Assert.ThrowsExactly<InvalidParameterRangeException>(() => EnumerationPage.GetOffset(0, 5));
    }
}
