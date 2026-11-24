using System.ComponentModel;
using System.IO;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Server.Subnautica.Models.Commands.Core;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresOrigin(CommandOrigin.SERVER)]
internal sealed class ConfigCommand(IOptions<ServerStartOptions> optionsProvider) : ICommandHandler
{
    private readonly IOptions<ServerStartOptions> optionsProvider = optionsProvider;

    internal const string EDIT_WARNING = "Stop the server before editing this file: it is read when the server starts, and the server writes it back with its own settings on every save.";

    [Description("Opens the server configuration file (to edit it, stop the server first)")]
    public async Task Execute(ICommandContext context)
    {
        string filePath = optionsProvider.Value.GetServerConfigFilePath();
        if (!File.Exists(filePath))
        {
            // TODO: Handle this case to generate config?
            await context.ReplyAsync("No configuration file exists");
            return;
        }

        await context.ReplyAsync(EDIT_WARNING);
        FileSystem.Instance.OpenOrExecuteFile(filePath);
    }
}
