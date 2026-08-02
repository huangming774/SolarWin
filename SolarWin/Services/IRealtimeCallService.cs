using System.Collections.ObjectModel;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Media layer for room realtime calls (LiveKit / WebRTC via JoinCallResponse).
/// </summary>
public interface IRealtimeCallService : IAsyncDisposable
{
    bool IsConnected { get; }

    bool IsConnecting { get; }

    bool IsMicEnabled { get; }

    bool IsCameraEnabled { get; }

    bool IsScreenShareEnabled { get; }

    bool IsSidetoneEnabled { get; }

    bool IsReconnecting { get; }

    string? StatusText { get; }

    string? RoomName { get; }

    /// <summary>excellent / good / poor / lost / unknown</summary>
    string ConnectionQualityText { get; }

    IReadOnlyList<RealtimeMediaParticipant> Participants { get; }

    /// <summary>Remote + local video tiles (multi-party). UI binds to collection changes.</summary>
    ObservableCollection<CallVideoTile> VideoTiles { get; }

    WriteableBitmap? LocalVideo { get; }

    /// <summary>Focused remote tile (or first remote).</summary>
    ImageSource? FocusedRemoteVideo { get; }

    string? FocusedIdentity { get; }

    IReadOnlyList<MediaDeviceInfo> Microphones { get; }

    IReadOnlyList<MediaDeviceInfo> Speakers { get; }

    IReadOnlyList<MediaDeviceInfo> Cameras { get; }

    int SelectedMicIndex { get; }

    int SelectedSpeakerIndex { get; }

    int SelectedCameraIndex { get; }

    event EventHandler? StateChanged;

    event EventHandler? ParticipantsChanged;

    event EventHandler? VideoFrameUpdated;

    event EventHandler? DevicesChanged;

    Task ConnectAsync(JoinCallResponse join, CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    Task SetMicEnabledAsync(bool enabled);

    Task SetCameraEnabledAsync(bool enabled);

    Task SetScreenShareEnabledAsync(bool enabled);

    Task SetSidetoneEnabledAsync(bool enabled);

    Task SelectMicrophoneAsync(int deviceIndex);

    Task SelectSpeakerAsync(int deviceIndex);

    Task SelectCameraAsync(int deviceIndex);

    void RefreshDeviceLists();

    void SetFocusedIdentity(string? identity);
}

public sealed class MediaDeviceInfo
{
    public required int Index { get; init; }

    public required string Name { get; init; }
}

public sealed class RealtimeMediaParticipant
{
    public required string Identity { get; init; }

    public string? Name { get; init; }

    public bool IsLocal { get; init; }

    public bool HasAudio { get; init; }

    public bool HasVideo { get; init; }

    public bool IsScreenShare { get; init; }

    public string? QualityText { get; init; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Identity : Name!;
}

/// <summary>One video surface in grid / focus layout.</summary>
public partial class CallVideoTile : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public required string Identity { get; init; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsLocal { get; init; }

    public bool IsScreenShare { get; init; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial ImageSource? Bitmap { get; set; }

    internal CanvasBitmap? GpuBitmap { get; set; }

    internal CanvasImageSource? GpuSurface { get; set; }

    internal int FrameWidth { get; set; }

    internal int FrameHeight { get; set; }

    internal int SurfaceWidth { get; set; }

    internal int SurfaceHeight { get; set; }

    internal int SurfaceSmallFrameCount { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool IsFocused { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string QualityText { get; set; } = string.Empty;
}
