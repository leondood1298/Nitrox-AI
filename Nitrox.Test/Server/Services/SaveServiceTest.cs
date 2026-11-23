using System.Diagnostics;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Test.Server.Services;

[TestClass]
public sealed class SaveServiceTest
{
    private static readonly string folder = Path.Combine(Path.GetTempPath(), "nitrox-hooks");

    [TestMethod]
    public void PowerShellScriptRunsInPowerShell()
    {
        string script = Path.Combine(folder, "copy world.ps1");

        ProcessStartInfo startInfo = SaveService.CreatePostSaveStartInfo(script);

        startInfo.FileName.Should().BeOneOf("powershell.exe", "pwsh");
        startInfo.ArgumentList.Should().Equal("-NoProfile", "-File", script);
        startInfo.UseShellExecute.Should().BeFalse();
        startInfo.WorkingDirectory.Should().Be(folder);
    }

    [TestMethod]
    public void BatchFileRunsInCmd()
    {
        string script = Path.Combine(folder, "backup.bat");

        ProcessStartInfo startInfo = SaveService.CreatePostSaveStartInfo(script);

        startInfo.FileName.Should().Be("cmd.exe");
        startInfo.ArgumentList.Should().Equal("/c", script);
    }

    [TestMethod]
    public void ProgramRunsItself()
    {
        string program = Path.Combine(folder, "tool.exe");

        SaveService.CreatePostSaveStartInfo(program).FileName.Should().Be(program);
    }
}
