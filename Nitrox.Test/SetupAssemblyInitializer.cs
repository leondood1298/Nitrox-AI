global using Nitrox.Test.Helper;
using Nitrox.Model.Core;
using Nitrox.Model.Helper;
using Nitrox.Model.Logger;

namespace Nitrox.Test;

[TestClass]
public static class SetupAssemblyInitializer
{
    /// <summary>
    ///     Tests write logs, caches and settings like the real programs do; they must never land in the player's own
    ///     Nitrox folder (%APPDATA%\Nitrox), so every test run gets a data folder of its own.
    /// </summary>
    public static string TestDataPath { get; } = Path.Combine(Path.GetTempPath(), $"NitroxTestData_{Guid.NewGuid():N}");

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        // Must come before anything reads NitroxDirectory, which keeps the first answer.
        Environment.SetEnvironmentVariable(NitroxDirectory.DATA_PATH_ENV_VAR_NAME, TestDataPath);
        NitroxEnvironment.Set(NitroxEnvironment.Types.TESTING);
        Log.Setup();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        try
        {
            Directory.Delete(TestDataPath, true);
        }
        catch (IOException)
        {
            // The log file can still be held open by the logger; the folder is in the temp folder anyway.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
