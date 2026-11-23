using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;

namespace Nitrox.Test.Server.Commands;

/// <summary>
///     Stands in for the server console a command is run from and keeps what the command answered.
/// </summary>
internal sealed class RecordingCommandContext : ICommandContext
{
    public List<string> Replies { get; } = [];
    public ILogger Logger { get; set; } = NullLogger.Instance;
    public CommandOrigin Origin { get; init; } = CommandOrigin.SERVER;
    public string OriginName => "TEST";
    public SessionId OriginId { get; init; } = SessionId.SERVER_ID;
    public Perms Permissions { get; init; } = Perms.HOST;

    public Task ReplyAsync<T>(T data)
    {
        Replies.Add(data?.ToString() ?? "");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     What the command sent to one player, with that player's session.
    /// </summary>
    public List<(SessionId SessionId, object? Data)> Sent { get; } = [];

    /// <summary>
    ///     What the command sent to every player.
    /// </summary>
    public List<object?> SentToAll { get; } = [];

    public ValueTask SendAsync<T>(SessionId sessionId, T data)
    {
        Sent.Add((sessionId, data));
        return ValueTask.CompletedTask;
    }

    public ValueTask SendToAllAsync<T>(T data)
    {
        SentToAll.Add(data);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendToOthersAsync<T>(T data) => ValueTask.CompletedTask;

    /// <summary>
    ///     Runs a command that takes no arguments.
    /// </summary>
    /// <returns>Its one answer.</returns>
    public static async Task<string> RunAsync(ICommandHandler command)
    {
        RecordingCommandContext context = new();
        await command.Execute(context);
        return context.Replies.Single();
    }
}
