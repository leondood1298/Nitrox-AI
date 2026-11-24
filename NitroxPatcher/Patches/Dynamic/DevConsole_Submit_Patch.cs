using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Disables the DevConsole's submit functionality for players with insufficient permissions, and refuses the commands that
/// change only this game (see <see cref="ConsoleCommandPolicy" />).
/// TODO: Implement https://github.com/SubnauticaNitrox/Nitrox/issues/1689
/// </summary>
public sealed partial class DevConsole_Submit_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((DevConsole t) => t.Submit(default));

    public static bool Prefix(string value)
    {
        Log.Info($"Used cheat command : '{value}'");
        Resolve<IPacketSender>().Send(new CheatCommand(value));

        if (!ConsoleCommandPolicy.MaySubmit(Resolve<LocalPlayer>().Permissions))
        {
            Log.InGame(Language.main.Get("Nitrox_MissingPermission").Replace("{PERMISSION}", ConsoleCommandPolicy.REQUIRED_PERMS.ToString()));
            return false;
        }
        if (ConsoleCommandPolicy.IsRefused(value))
        {
            Log.InGame(Language.main.Get("Nitrox_CommandNotAvailable"));
            return false;
        }
        return true;
    }
}
