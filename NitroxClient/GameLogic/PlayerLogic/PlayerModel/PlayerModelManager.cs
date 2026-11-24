using System.Collections;
using System.Collections.Generic;
using NitroxClient.GameLogic.PlayerLogic.PlayerModel.Abstract;
using NitroxClient.GameLogic.PlayerLogic.PlayerModel.ColorSwap;
using NitroxClient.MonoBehaviours;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NitroxClient.GameLogic.PlayerLogic.PlayerModel;

public class PlayerModelManager
{
    private readonly IEnumerable<IColorSwapManager> colorSwapManagers;

    public PlayerModelManager(IEnumerable<IColorSwapManager> colorSwapManagers)
    {
        this.colorSwapManagers = colorSwapManagers;
    }

    public void BeginApplyPlayerColor(INitroxPlayer player)
    {
        Multiplayer.Main.StartCoroutine(ApplyPlayerColor(player, colorSwapManagers));
    }

    private IEnumerator CreateSignalPrototype(IOut<GameObject> result)
    {
        CoroutineTask<GameObject> signalHandle = AddressablesUtility.InstantiateAsync("WorldEntities/Environment/Generated/Signal.prefab", Multiplayer.Main.transform, awake: false);
        yield return signalHandle;

        GameObject go = signalHandle.GetResult();
        go.name = "RemotePlayerSignalPrototype";
        go.transform.localScale = new Vector3(.5f, .5f, .5f);
        go.transform.localPosition = new Vector3(0, 0.8f, 0);
        go.SetActive(false);

        result.Set(go);
    }

    public IEnumerator AttachPing(INitroxPlayer player)
    {
        TaskResult<GameObject> result = new();
        yield return CreateSignalPrototype(result);

        GameObject signalBase = Object.Instantiate(result.value, player.PlayerModel.transform, false);
        signalBase.name = $"signal_{player.PlayerName}";
        signalBase.SetActive(true);

        PingInstance ping = signalBase.GetComponent<PingInstance>();
        ping.Initialize();
        ping.SetLabel($"Player {player.PlayerName}");
        ping.pingType = PingType.Signal;
        // ping will be moved to the player list tab
        ping.displayPingInManager = false;

        // SignalPing is not required for player as we don't need to display text or anchor to a specific world position
        // we also take a dependency on the lack of signalping later to differentiate remote player pings from others.
        Object.DestroyImmediate(signalBase.GetComponent<SignalPing>());

        SetInGamePingColor(player, ping);
    }

    private static void SetInGamePingColor(INitroxPlayer player, PingInstance ping)
    {
        uGUI_Pings pings = Object.FindObjectOfType<uGUI_Pings>();
        if (!pings)
        {
            // Without the HUD's pings there is nothing to colour; the ping keeps the default colour
            return;
        }

        pings.OnColor(ping.Id, player.PlayerSettings.PlayerColor.ToUnity());
    }

    private static IEnumerator ApplyPlayerColor(INitroxPlayer player, IEnumerable<IColorSwapManager> colorSwapManagers)
    {
        ColorSwapAsyncOperation swapOperation = new(player, colorSwapManagers);

        swapOperation.BeginColorSwap();
        yield return new WaitUntil(() => swapOperation.IsColorSwapComplete());
        // A player who left while the colours were worked out has no body any more
        if (!player.PlayerModel)
        {
            yield break;
        }
        swapOperation.ApplySwappedColors();
    }
}
