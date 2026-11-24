using Nitrox.Server.Subnautica.Models.Commands.ArgConverters;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Test.Server.Services;

/// <summary>
///     A whisper of more than one word, a broadcast of more than eight words and a ban reason with spaces did not reach
///     their command; passwords were written to the log.
/// </summary>
[TestClass]
public sealed class CommandServiceTest
{
    [TestMethod]
    public void AFinalTextParameterTakesTheRestOfTheLine()
    {
        List<string>? args = CommandService.GetRestOfLineArguments([typeof(ServerPlayerStandIn), typeof(string)], "Bob hello there, how are you?");

        args.Should().Equal("Bob", "hello there, how are you?");
    }

    [TestMethod]
    public void ALongLineIsNotCutAtEightWords()
    {
        List<string>? args = CommandService.GetRestOfLineArguments([typeof(string)], "one two three four five six seven eight nine ten");

        args.Should().Equal("one two three four five six seven eight nine ten");
    }

    [TestMethod]
    public void AQuotedFirstArgumentStaysOneArgument()
    {
        List<string>? args = CommandService.GetRestOfLineArguments([typeof(KnownPlayer), typeof(string)], "\"John Smith\" was rude");

        args.Should().Equal("John Smith", "was rude");
    }

    [TestMethod]
    public void OnlyAFinalTextParameterTakesTheRest()
    {
        CommandService.GetRestOfLineArguments([typeof(string), typeof(int)], "a b c").Should().BeNull();
        CommandService.GetRestOfLineArguments([typeof(string)], "single").Should().BeNull("one word is an exact match, handled first");
    }

    [TestMethod]
    public void PasswordsDoNotReachTheLog()
    {
        CommandLogText.Redact("login hunter2").Should().Be("login ***");
        CommandLogText.Redact("/ChangeServerPassword my secret").Should().Be("/ChangeServerPassword ***");
        CommandLogText.Redact("changeadminpassword x").Should().Be("changeadminpassword ***");
        CommandLogText.Redact("say hello").Should().Be("say hello");
        CommandLogText.Redact("login").Should().Be("login");
    }

    private sealed class ServerPlayerStandIn;
}
