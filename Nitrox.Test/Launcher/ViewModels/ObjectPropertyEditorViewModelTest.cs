using Nitrox.Launcher.ViewModels;
using Nitrox.Model.Configuration;

namespace Nitrox.Test.Launcher.ViewModels;

[TestClass]
public sealed class ObjectPropertyEditorViewModelTest
{
    [TestMethod]
    public void AValueOutsideTheSettingsRangeIsRefused()
    {
        PropertyInfo property = typeof(SubnauticaServerOptions).GetProperty(nameof(SubnauticaServerOptions.PictureFrameMaxKilobytes))!;

        ObjectPropertyEditorViewModel.GetValidationError(new SubnauticaServerOptions(), property, 99999).Should().NotBeNull();
    }

    [TestMethod]
    public void AValueInsideTheSettingsRangeIsAccepted()
    {
        PropertyInfo property = typeof(SubnauticaServerOptions).GetProperty(nameof(SubnauticaServerOptions.MaxBackups))!;

        ObjectPropertyEditorViewModel.GetValidationError(new SubnauticaServerOptions(), property, 5).Should().BeNull();
        ObjectPropertyEditorViewModel.GetValidationError(new SubnauticaServerOptions(), property, -1).Should().NotBeNull();
    }
}
