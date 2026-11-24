using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class Eatable_IterateDespawn_PatchTest
{
    [TestMethod]
    public void PrefixTakesTheFoodWhoseDespawnItGates()
    {
        MethodInfo prefix = typeof(Eatable_IterateDespawn_Patch).GetMethod(nameof(Eatable_IterateDespawn_Patch.Prefix));

        prefix.GetParameters().Single().ParameterType.Should().Be(typeof(Eatable), "Harmony passes the patched Eatable whatever type is declared");
    }
}
