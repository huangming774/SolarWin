using CommunityToolkit.Mvvm.ComponentModel;
using SolarWin.Models;

namespace SolarWin.ViewModels;

public partial class ChatGroupItemViewModel : ObservableObject
{
    public ChatGroupItemViewModel(SnChatGroup group)
    {
        Group = group;
        Id = group.Id;
        Name = string.IsNullOrWhiteSpace(group.Name) ? "(未命名分组)" : group.Name!;
        RoomCount = group.RoomIds?.Count ?? 0;
        Subtitle = $"会话 {RoomCount}";
    }

    public SnChatGroup Group { get; }

    public Guid Id { get; }

    public string Name { get; }

    public int RoomCount { get; }

    public string Subtitle { get; }
}
