using System.Reflection;
using NitroxClient.GameLogic.Settings;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game saves its options with the static settings as they are; during a session PDA pause is forced off, so the
///     player's own value is put in place while the options are read or written.
/// </summary>
public sealed partial class GameSettings_SerializeMiscSettings_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => GameSettings.SerializeMiscSettings(default));

    public static void Prefix()
    {
        PdaPauseOverride.Game.BeforeSettingsSerialized();
    }

    public static void Postfix()
    {
        PdaPauseOverride.Game.AfterSettingsSerialized();
    }
}
