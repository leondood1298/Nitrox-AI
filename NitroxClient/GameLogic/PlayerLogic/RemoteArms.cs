namespace NitroxClient.GameLogic.PlayerLogic;

/// <summary>
///     Tells the arms of another player's body apart from the local player's. A body is a copy of the local player's, so
///     its <see cref="ArmsController" /> takes the local <see cref="Player" /> in <c>ArmsController.Start</c>: the
///     animation events of its animator (a knife swing, a bash, a fast swim stroke) would act on the local player's hand
///     and tool.
/// </summary>
public static class RemoteArms
{
    public static bool IsRemote(ArmsController arms)
    {
        return Player.main && arms && Player.main.armsController != arms;
    }

    public static bool TryGetRemotePlayer(ArmsController arms, out RemotePlayer remotePlayer)
    {
        remotePlayer = null;
        if (!IsRemote(arms))
        {
            return false;
        }
        RemotePlayerIdentifier identifier = arms.GetComponentInParent<RemotePlayerIdentifier>();
        if (!identifier)
        {
            return false;
        }
        remotePlayer = identifier.RemotePlayer;
        return remotePlayer != null;
    }
}
