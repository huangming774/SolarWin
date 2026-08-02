using SolarWin.Models;

namespace SolarWin.Services;

public static class MlsRoomSendPolicy
{
    public static bool RequiresLocalMlsState(SnChatRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return room.EncryptionMode == ChatRoomEncryptionMode.Mls;
    }

    public static bool CanUsePlaintextMessageApi(bool encryptionStateKnown, bool requiresUnavailableMlsState)
        => encryptionStateKnown && !requiresUnavailableMlsState;
}
