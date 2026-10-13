using SolarWin.Models;

namespace SolarWin.Helpers;

/// <summary>Pure identity rules shared by chat echo reconciliation and regression tests.</summary>
public static class ChatMessageReconciliation
{
    public static bool HasSameClientMessageId(SnChatMessage left, SnChatMessage right)
        => !string.IsNullOrWhiteSpace(left.ClientMessageId)
           && string.Equals(left.ClientMessageId, right.ClientMessageId, StringComparison.Ordinal);

    public static int FindSameClientMessageIndex(
        IReadOnlyList<SnChatMessage> messages,
        SnChatMessage candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.ClientMessageId))
        {
            return -1;
        }

        for (var i = 0; i < messages.Count; i++)
        {
            if (HasSameClientMessageId(messages[i], candidate))
            {
                return i;
            }
        }

        return -1;
    }
}
