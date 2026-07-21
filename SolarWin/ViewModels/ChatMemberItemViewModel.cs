using CommunityToolkit.Mvvm.ComponentModel;
using SolarWin.Models;

namespace SolarWin.ViewModels;

public partial class ChatMemberItemViewModel : ObservableObject
{
    public ChatMemberItemViewModel(SnChatMember member)
    {
        Member = member;
        MemberId = member.Id;
        AccountId = member.AccountId;
        DisplayName = member.Nick
            ?? member.Username
            ?? member.Account?.Nick
            ?? member.Account?.Name
            ?? member.AccountId.ToString("D")[..8];
        NotifyText = member.Notify switch
        {
            ChatMemberNotify.Mentions => "仅提及",
            ChatMemberNotify.None => "关闭",
            _ => "全部",
        };
        IsTimedOut = member.TimeoutUntil is { } t && t > DateTimeOffset.UtcNow;
        TimeoutText = IsTimedOut
            ? $"禁言至 {member.TimeoutUntil:MM-dd HH:mm}"
            : string.Empty;
    }

    public SnChatMember Member { get; }

    public Guid MemberId { get; }

    public Guid AccountId { get; }

    public string DisplayName { get; }

    public string NotifyText { get; }

    public bool IsTimedOut { get; }

    public string TimeoutText { get; }
}
