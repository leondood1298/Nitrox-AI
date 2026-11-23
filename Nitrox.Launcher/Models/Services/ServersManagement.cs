using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Grpc.Core;
using MagicOnion.Server.Hubs;
using Nitrox.Launcher.Models.Design;
using Nitrox.Model.Logger;
using Nitrox.Model.MagicOnion;

namespace Nitrox.Launcher.Models.Services;

/// <summary>
///     Scoped service (i.e. instance for each server) that handles server management (like sending commands and
///     receiving output).
/// </summary>
internal sealed class ServersManagement(ServerService serverService) : StreamingHubBase<IServersManagement, IServerManagementReceiver>, IServersManagement
{
    private readonly CancellationTokenSource cts = new();
    private readonly ServerService serverService = serverService;
    private int processId;
    private string saveName;

    public ValueTask SetPlayers(string[] players)
    {
        ServerEntry? entry = serverService.GetServerEntryByAnyOf(processId, saveName);
        if (entry == null)
        {
            return CompletedTask;
        }
        entry.PlayerCount = players.Length;
        entry.PlayerNames = [..players];
        return CompletedTask;
    }

    public async ValueTask AddOutputLine(string category, DateTimeOffset? localTime, int level, string message)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ServerEntry? entry = serverService.GetServerEntryByAnyOf(processId, saveName);
                if (entry != null)
                {

                    // First output received, server is no longer starting
                    entry.IsServerStarting = false;
                    entry.AddOutput(new OutputLine
                    {
                        LogText = message,
                        LocalTime = localTime,
                        Type = (OutputLineType)level
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            throw;
        }
    }

    public ValueTask SetLoadingProgress(string stage, float progress)
    {
        try
        {
            ServerEntry? entry = serverService.GetServerEntryByAnyOf(processId, saveName);
            if (entry != null)
            {
                entry.LoadingStage = stage;
                entry.LoadingProgress = progress;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            throw;
        }
        return CompletedTask;
    }

    protected override async ValueTask OnConnected()
    {
        Metadata headers = Context.CallContext.RequestHeaders;
        if (!int.TryParse(headers.GetValue("ProcessId"), out processId))
        {
            return;
        }
        saveName = headers.GetValue("SaveName") ?? "";
        if (saveName == "")
        {
            return;
        }
        ServerEntry? entry = await WaitForEntryAsync(cts.Token);
        if (entry == null)
        {
            return;
        }
        await entry.RefreshFromProcessAsync(processId);
        entry.OnManagementConnected();
        _ = HandleCommandLoopAsync().ContinueWithHandleError();
    }

    protected override async ValueTask OnDisconnected()
    {
        ServerEntry? entry = serverService.GetServerEntryByAnyOf(processId, saveName);
        int lostProcessId = processId;
        processId = 0;
        saveName = "";
        // Stops this connection's command loop, so that commands go to the server's next connection, not a dead one
        await cts.CancelAsync();
        if (entry != null && lostProcessId > 0)
        {
            _ = entry.OnManagementConnectionLostAsync(lostProcessId).ContinueWithHandleError();
        }
    }

    private async Task<ServerEntry?> WaitForEntryAsync(CancellationToken cancellationToken)
    {
        try
        {
            do
            {
                ServerEntry? entry = serverService.GetServerEntryByAnyOf(processId, saveName);
                if (entry != null)
                {
                    return entry;
                }
                await Task.Delay(250, cancellationToken);
            } while (!cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        return null;
    }

    private async Task HandleCommandLoopAsync()
    {
        ServerEntry? entry = serverService.GetServerEntryByAnyOf(processId, saveName);
        if (entry == null)
        {
            return;
        }
        try
        {
            await foreach (string command in entry.CommandQueue.Reader.ReadAllAsync(cts.Token))
            {
                Client.OnCommand(command);
            }
        }
        catch (OperationCanceledException)
        {
            // The connection closed
        }
    }
}
