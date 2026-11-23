using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Putting out the intro fire ends a played intro: the game then fades the pod to its emergency lighting, even when
///     another player has repaired the pod already. The repair state the server sent is put back afterwards.
/// </summary>
public sealed partial class IntroLifepodDirector_ConcludeIntroSequence_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((IntroLifepodDirector t) => t.ConcludeIntroSequence());

    public static void Postfix()
    {
        Resolve<EscapePodRepairState>().ApplyAfterIntro();
    }
}
