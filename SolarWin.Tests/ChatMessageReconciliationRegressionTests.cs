using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Tests;

/// <summary>
/// Add this file and ChatMessageReconciliation.cs to the test project source list when the
/// test project is maintained. It intentionally requires no WinUI host or service mocks.
/// </summary>
public sealed class ChatMessageReconciliationRegressionTests
{
    [Fact]
    public void WebSocketThenHttpResult_UsesOneClientIdentity()
    {
        var clientId = "send-1";
        var server = new SnChatMessage { Id = Guid.NewGuid(), ClientMessageId = clientId };
        var pending = new SnChatMessage { Id = Guid.Empty, ClientMessageId = clientId };
        var messages = new List<SnChatMessage> { server };

        Assert.Equal(0, ChatMessageReconciliation.FindSameClientMessageIndex(messages, pending));
        Assert.True(ChatMessageReconciliation.HasSameClientMessageId(server, pending));
    }

    [Fact]
    public void HttpResultThenWebSocket_UsesOneClientIdentity()
    {
        var clientId = "send-2";
        var pending = new SnChatMessage { Id = Guid.Empty, ClientMessageId = clientId };
        var server = new SnChatMessage { Id = Guid.NewGuid(), ClientMessageId = clientId };
        var messages = new List<SnChatMessage> { pending };

        Assert.Equal(0, ChatMessageReconciliation.FindSameClientMessageIndex(messages, server));
        Assert.True(ChatMessageReconciliation.HasSameClientMessageId(pending, server));
    }
}
