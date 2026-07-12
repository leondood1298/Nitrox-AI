using Nitrox.Model.DataStructures;
using NitroxClient.Extensions;

namespace NitroxClient.GameLogic;

public static class CrafterIdentity
{
    public static bool TryGetId(CrafterLogic crafterLogic, out NitroxId crafterId)
    {
        if (crafterLogic.TryGetNitroxId(out crafterId))
        {
            return true;
        }

        MapRoomFunctionality mapRoom = crafterLogic.GetComponentInParent<MapRoomFunctionality>();
        return mapRoom && mapRoom.TryGetNitroxId(out crafterId);
    }
}
