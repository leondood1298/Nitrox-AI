namespace NitroxClient.MonoBehaviours;

/// <summary>
///     What changed since the frame before about the Scanner Room camera the local player flies. The diagnostics get
///     a line for each change and none while nothing changes.
/// </summary>
internal enum ScannerAnchorTransition
{
    NONE,
    ENTER,

    /// <summary>
    ///     The player went on to another camera without leaving the screen.
    /// </summary>
    SWITCH,

    /// <summary>
    ///     The same camera has another id than in the frame before, or has one now and had none.
    /// </summary>
    IDENTIFIED,

    EXIT
}
