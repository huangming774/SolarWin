using SolarWin.Models;

namespace SolarWin.ViewModels;

public sealed class CallParticipantItemViewModel
{
    public CallParticipantItemViewModel(CallParticipant p)
    {
        Participant = p;
        AccountId = p.AccountId ?? Guid.Empty;
        DisplayName = p.Name ?? p.Identity ?? (p.AccountId?.ToString("D")[..8] ?? "参与者");
    }

    public CallParticipant Participant { get; }

    public Guid AccountId { get; }

    public string DisplayName { get; }
}
