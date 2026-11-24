namespace Nitrox.Model.Helper;

[TestClass]
public class GameLaunchArgumentsTest
{
    [TestMethod]
    public void ADataPathWithSpacesReachesTheGameAsOneArgument()
    {
        string commandLine = GameLaunchArguments.Build("-vrmode none", @"D:\My Nitrox Data", ["--instantlaunch", "Player One"]);

        commandLine.Should().Be(@"-vrmode none --data-path ""D:\My Nitrox Data"" --instantlaunch ""Player One""");
        GameLaunchArguments.Split(commandLine).Should().Equal("-vrmode", "none", "--data-path", @"D:\My Nitrox Data", "--instantlaunch", "Player One");
        new[] { "--data-path", @"D:\My Nitrox Data" }.GetCommandArgs("--data-path").Should().Equal(@"D:\My Nitrox Data");
    }

    [TestMethod]
    public void NoDataPathMeansNoDataPathArgument()
    {
        GameLaunchArguments.Build("-vrmode none", null).Should().Be("-vrmode none");
        GameLaunchArguments.Build(null, null).Should().BeEmpty();
    }

    [TestMethod]
    public void QuoteKeepsATrailingBackslashFromEscapingTheClosingQuote()
    {
        GameLaunchArguments.Quote(@"C:\My Data\").Should().Be(@"""C:\My Data\\""");
        GameLaunchArguments.Quote(@"C:\Data").Should().Be(@"C:\Data");
        GameLaunchArguments.Quote("").Should().Be(@"""""");
    }
}
