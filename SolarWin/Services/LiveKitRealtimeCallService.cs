using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using LiveKit.Proto;
using LiveKit.Rtc;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.Wave;
using SolarWin.Models;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Graphics.DirectX;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using RoomOptions = LiveKit.Rtc.RoomOptions;
using TrackPublishOptions = LiveKit.Rtc.TrackPublishOptions;

namespace SolarWin.Services;

/// <summary>
/// LiveKit RTC: mic/speaker/camera device selection, multi-remote video tiles,
/// screen share, sidetone, connection quality + reconnect status.
/// </summary>
public sealed class LiveKitRealtimeCallService : IRealtimeCallService
{
    private const int SampleRate = 48000;
    private const int Channels = 1;
    private const int SurfaceShrinkAfterFrames = 120;

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, RemoteAudioSink> _audioSinks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _videoLoops = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _qualityByIdentity = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CallVideoTile> _tilesByKey = new(StringComparer.Ordinal);
    // Per-tile frame handoff: latest-wins gate + double buffer so a slow UI thread
    // drops frames instead of piling multi-MB closures onto the DispatcherQueue.
    private readonly ConcurrentDictionary<string, TileFrameState> _frameStates = new(StringComparer.Ordinal);
    private readonly CanvasDevice _videoCanvasDevice = CanvasDevice.GetSharedDevice();

    private Room? _room;
    private AudioSource? _audioSource;
    private LocalAudioTrack? _localAudioTrack;
    private VideoSource? _videoSource;
    private LocalVideoTrack? _localVideoTrack;
    private VideoSource? _screenSource;
    private LocalVideoTrack? _screenTrack;
    private ScreenCapturePublisher? _screenPublisher;
    private WaveInEvent? _mic;
    private WaveOutEvent? _sidetoneOut;
    private BufferedWaveProvider? _sidetoneBuffer;
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private CancellationTokenSource? _sessionCts;
    private DispatcherQueue? _uiQueue;
    private bool _micEnabled = true;
    private bool _cameraEnabled;
    private bool _screenEnabled;
    private bool _sidetoneEnabled;
    private bool _disposed;
    private int _micDeviceIndex = -1; // default device
    private int _speakerDeviceIndex = -1;
    private string? _cameraDeviceId;
    private string? _focusedIdentity;
    private List<MediaDeviceInfo> _mics = [];
    private List<MediaDeviceInfo> _speakers = [];
    private List<MediaDeviceInfo> _cameras = [];
    private List<string> _cameraIds = [];

    public LiveKitRealtimeCallService()
    {
        RefreshDeviceLists();
    }

    public bool IsConnected { get; private set; }

    public bool IsConnecting { get; private set; }

    public bool IsMicEnabled => _micEnabled;

    public bool IsCameraEnabled => _cameraEnabled;

    public bool IsScreenShareEnabled => _screenEnabled;

    public bool IsSidetoneEnabled => _sidetoneEnabled;

    public bool IsReconnecting { get; private set; }

    public string? StatusText { get; private set; }

    public string? RoomName { get; private set; }

    public string ConnectionQualityText { get; private set; } = "未知";

    public WriteableBitmap? LocalVideo { get; private set; }

    public ImageSource? FocusedRemoteVideo { get; private set; }

    public string? FocusedIdentity => _focusedIdentity;

    public ObservableCollection<CallVideoTile> VideoTiles { get; } = [];

    public IReadOnlyList<MediaDeviceInfo> Microphones => _mics;

    public IReadOnlyList<MediaDeviceInfo> Speakers => _speakers;

    public IReadOnlyList<MediaDeviceInfo> Cameras => _cameras;

    public int SelectedMicIndex =>
        _mics.FindIndex(m => m.Index == _micDeviceIndex);

    public int SelectedSpeakerIndex =>
        _speakers.FindIndex(m => m.Index == _speakerDeviceIndex);

    public int SelectedCameraIndex
    {
        get
        {
            if (string.IsNullOrEmpty(_cameraDeviceId))
            {
                return _cameras.Count > 0 ? 0 : -1;
            }

            var i = _cameraIds.IndexOf(_cameraDeviceId);
            return i >= 0 ? i : 0;
        }
    }

    private readonly List<RealtimeMediaParticipant> _participants = [];

    public IReadOnlyList<RealtimeMediaParticipant> Participants
    {
        get
        {
            lock (_gate)
            {
                return _participants.ToList();
            }
        }
    }

    public event EventHandler? StateChanged;

    public event EventHandler? ParticipantsChanged;

    public event EventHandler? VideoFrameUpdated;

    public event EventHandler? DevicesChanged;

    public void RefreshDeviceLists()
    {
        var mics = new List<MediaDeviceInfo>();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            try
            {
                var caps = WaveIn.GetCapabilities(i);
                mics.Add(new MediaDeviceInfo { Index = i, Name = caps.ProductName });
            }
            catch
            {
                // skip
            }
        }

        var speakers = new List<MediaDeviceInfo>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            try
            {
                var caps = WaveOut.GetCapabilities(i);
                speakers.Add(new MediaDeviceInfo { Index = i, Name = caps.ProductName });
            }
            catch
            {
                // skip
            }
        }

        _mics = mics;
        _speakers = speakers;
        _ = LoadCamerasAsync();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadCamerasAsync()
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var list = new List<MediaDeviceInfo>();
            var ids = new List<string>();
            var idx = 0;
            foreach (var d in devices)
            {
                list.Add(new MediaDeviceInfo { Index = idx, Name = d.Name });
                ids.Add(d.Id);
                idx++;
            }

            _cameras = list;
            _cameraIds = ids;
            if (_cameraDeviceId is null && ids.Count > 0)
            {
                _cameraDeviceId = ids[0];
            }

            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // ignore
        }
    }

    public async Task ConnectAsync(JoinCallResponse join, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(join);

        if (string.IsNullOrWhiteSpace(join.Token))
        {
            throw new InvalidOperationException("通话令牌为空，无法连接媒体服务器");
        }

        var url = NormalizeLiveKitUrl(join.Endpoint);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("通话 endpoint 为空，请确认服务端返回 LiveKit 地址");
        }

        await DisconnectAsync().ConfigureAwait(false);

        IsConnecting = true;
        IsReconnecting = false;
        ConnectionQualityText = "连接中";
        StatusText = "正在连接媒体…";
        RaiseState();

        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _sessionCts.Token;
        _uiQueue = DispatcherQueue.GetForCurrentThread() ?? App.DispatcherQueue;

        var room = new Room();
        room.ParticipantConnected += OnParticipantConnected;
        room.ParticipantDisconnected += OnParticipantDisconnected;
        room.TrackSubscribed += OnTrackSubscribed;
        room.TrackUnsubscribed += OnTrackUnsubscribed;
        room.Disconnected += OnRoomDisconnected;
        room.ConnectionQualityChanged += OnConnectionQualityChanged;
        room.Reconnecting += OnRoomReconnecting;
        room.Reconnected += OnRoomReconnected;

        try
        {
            await room.ConnectAsync(url, join.Token!, new RoomOptions
            {
                AutoSubscribe = true,
                AdaptiveStream = true,
            }, ct).ConfigureAwait(false);

            _room = room;
            RoomName = room.Name ?? join.RoomName ?? join.RoomTitle;
            IsConnected = true;
            IsConnecting = false;
            StatusText = $"通话中 · {RoomName ?? "房间"}";
            RaiseState();

            await StartMicrophoneAsync(ct).ConfigureAwait(false);

            RefreshParticipants();
            RaiseParticipants();
        }
        catch
        {
            IsConnecting = false;
            IsConnected = false;
            StatusText = "媒体连接失败";
            ConnectionQualityText = "失败";
            RaiseState();

            // `_room` is only assigned on success, so TeardownMediaAsync never sees this
            // instance — release the half-open room (native FFI handles) right here.
            DetachRoomHandlers(room);
            try
            {
                await room.DisconnectAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            try
            {
                room.Dispose();
            }
            catch
            {
                // ignore
            }

            await TeardownMediaAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _sessionCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        await TeardownMediaAsync().ConfigureAwait(false);

        IsConnected = false;
        IsConnecting = false;
        IsReconnecting = false;
        _micEnabled = true;
        _cameraEnabled = false;
        _screenEnabled = false;
        _sidetoneEnabled = false;
        RoomName = null;
        StatusText = "未加入通话";
        ConnectionQualityText = "未知";
        LocalVideo = null;
        FocusedRemoteVideo = null;
        _focusedIdentity = null;
        lock (_gate)
        {
            _participants.Clear();
        }

        EnqueueUi(ClearVideoTiles, ReleaseVideoTileResourcesFallback);

        RaiseState();
        RaiseParticipants();
        RaiseVideo();
    }

    public async Task SetMicEnabledAsync(bool enabled)
    {
        _micEnabled = enabled;
        try
        {
            if (_localAudioTrack is not null)
            {
                if (enabled)
                {
                    _localAudioTrack.Unmute();
                }
                else
                {
                    _localAudioTrack.Mute();
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = "麦克风切换失败：" + ex.Message;
        }

        StatusText = enabled ? "麦克风已开" : "麦克风已关";
        RaiseState();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task SetCameraEnabledAsync(bool enabled)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("尚未加入通话");
        }

        if (enabled == _cameraEnabled)
        {
            return;
        }

        if (enabled)
        {
            if (_screenEnabled)
            {
                await SetScreenShareEnabledAsync(false).ConfigureAwait(false);
            }

            await StartCameraAsync(_sessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
            _cameraEnabled = true;
            StatusText = "摄像头已开";
        }
        else
        {
            await StopCameraAsync().ConfigureAwait(false);
            _cameraEnabled = false;
            LocalVideo = null;
            RemoveTile("local:camera");
            RaiseVideo();
            StatusText = "摄像头已关";
        }

        RaiseState();
        RefreshParticipants();
        RaiseParticipants();
    }

    public async Task SetScreenShareEnabledAsync(bool enabled)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("尚未加入通话");
        }

        if (enabled == _screenEnabled)
        {
            return;
        }

        if (enabled)
        {
            if (_cameraEnabled)
            {
                await SetCameraEnabledAsync(false).ConfigureAwait(false);
            }

            await StartScreenShareAsync(_sessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
            _screenEnabled = true;
            StatusText = "屏幕共享已开";
        }
        else
        {
            await StopScreenShareAsync().ConfigureAwait(false);
            _screenEnabled = false;
            RemoveTile("local:screen");
            StatusText = "屏幕共享已关";
        }

        RaiseState();
        RefreshParticipants();
        RaiseParticipants();
    }

    public async Task SetSidetoneEnabledAsync(bool enabled)
    {
        _sidetoneEnabled = enabled;
        if (enabled)
        {
            EnsureSidetonePlayback();
            StatusText = "耳返已开（注意啸叫）";
        }
        else
        {
            StopSidetonePlayback();
            StatusText = "耳返已关";
        }

        RaiseState();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task SelectMicrophoneAsync(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= _mics.Count)
        {
            return;
        }

        _micDeviceIndex = _mics[deviceIndex].Index;
        if (IsConnected && _localAudioTrack is not null)
        {
            // Restart mic capture with new device
            await RestartMicrophoneAsync().ConfigureAwait(false);
        }

        StatusText = "麦克风：" + _mics[deviceIndex].Name;
        RaiseState();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SelectSpeakerAsync(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= _speakers.Count)
        {
            return;
        }

        _speakerDeviceIndex = _speakers[deviceIndex].Index;
        foreach (var sink in _audioSinks.Values)
        {
            sink.SetDevice(_speakerDeviceIndex);
        }

        if (_sidetoneEnabled)
        {
            StopSidetonePlayback();
            EnsureSidetonePlayback();
        }

        StatusText = "扬声器：" + _speakers[deviceIndex].Name;
        RaiseState();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task SelectCameraAsync(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= _cameraIds.Count)
        {
            return;
        }

        _cameraDeviceId = _cameraIds[deviceIndex];
        if (_cameraEnabled)
        {
            await StopCameraAsync().ConfigureAwait(false);
            await StartCameraAsync(_sessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }

        StatusText = "摄像头：" + _cameras[deviceIndex].Name;
        RaiseState();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetFocusedIdentity(string? identity)
    {
        _focusedIdentity = identity;
        EnqueueUi(() =>
        {
            foreach (var t in VideoTiles)
            {
                t.IsFocused = !string.IsNullOrEmpty(identity)
                              && string.Equals(t.Identity, identity, StringComparison.Ordinal);
            }

            if (!string.IsNullOrEmpty(identity)
                && _tilesByKey.TryGetValue(TileKey(identity!, isLocal: false, screen: false), out var tile)
                && tile.Bitmap is not null)
            {
                FocusedRemoteVideo = tile.Bitmap;
            }
            else
            {
                // first remote non-local
                FocusedRemoteVideo = VideoTiles.FirstOrDefault(t => !t.IsLocal)?.Bitmap;
            }

            RaiseVideo();
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
    }

    // —— Media setup ——

    private async Task StartMicrophoneAsync(CancellationToken cancellationToken)
    {
        if (_room?.LocalParticipant is null)
        {
            return;
        }

        _audioSource = new AudioSource(SampleRate, Channels);
        _localAudioTrack = LocalAudioTrack.Create("microphone", _audioSource);
        await _room.LocalParticipant.PublishTrackAsync(
            _localAudioTrack,
            new TrackPublishOptions { Source = TrackSource.SourceMicrophone },
            cancellationToken).ConfigureAwait(false);

        StartWaveIn();
        _micEnabled = true;
    }

    private async Task RestartMicrophoneAsync()
    {
        if (_mic is not null)
        {
            try
            {
                _mic.DataAvailable -= OnMicData;
                _mic.StopRecording();
            }
            catch
            {
                // ignore
            }

            _mic.Dispose();
            _mic = null;
        }

        StartWaveIn();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void StartWaveIn()
    {
        var format = new WaveFormat(SampleRate, 16, Channels);
        _mic = new WaveInEvent
        {
            WaveFormat = format,
            BufferMilliseconds = 20,
            DeviceNumber = _micDeviceIndex >= 0 ? _micDeviceIndex : 0,
        };
        _mic.DataAvailable += OnMicData;
        _mic.StartRecording();
    }

    private void OnMicData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        if (_sidetoneEnabled && _sidetoneBuffer is not null)
        {
            try
            {
                // Quiet sidetone to reduce feedback
                var attenuated = new byte[e.BytesRecorded];
                for (var i = 0; i < e.BytesRecorded; i += 2)
                {
                    var sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                    sample = (short)(sample / 6);
                    attenuated[i] = (byte)(sample & 0xFF);
                    attenuated[i + 1] = (byte)((sample >> 8) & 0xFF);
                }

                _sidetoneBuffer.AddSamples(attenuated, 0, attenuated.Length);
            }
            catch
            {
                // ignore
            }
        }

        if (!_micEnabled || _audioSource is null)
        {
            return;
        }

        try
        {
            var sampleCount = e.BytesRecorded / 2;
            var samples = new short[sampleCount];
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
            var samplesPerChannel = sampleCount / Channels;
            if (samplesPerChannel <= 0)
            {
                return;
            }

            var frame = new AudioFrame(samples, SampleRate, Channels, samplesPerChannel);
            _audioSource.CaptureFrame(frame);
        }
        catch
        {
            // drop frame
        }
    }

    private void EnsureSidetonePlayback()
    {
        if (_sidetoneOut is not null)
        {
            return;
        }

        _sidetoneBuffer = new BufferedWaveProvider(new WaveFormat(SampleRate, 16, Channels))
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(300),
        };
        _sidetoneOut = new WaveOutEvent
        {
            DesiredLatency = 80,
            DeviceNumber = _speakerDeviceIndex >= 0 ? _speakerDeviceIndex : 0,
        };
        _sidetoneOut.Init(_sidetoneBuffer);
        _sidetoneOut.Play();
    }

    private void StopSidetonePlayback()
    {
        try
        {
            _sidetoneOut?.Stop();
        }
        catch
        {
            // ignore
        }

        _sidetoneOut?.Dispose();
        _sidetoneOut = null;
        _sidetoneBuffer = null;
    }

    private async Task StartCameraAsync(CancellationToken cancellationToken)
    {
        if (_room?.LocalParticipant is null)
        {
            return;
        }

        const int width = 640;
        const int height = 360;

        _videoSource = new VideoSource(width, height);
        _localVideoTrack = LocalVideoTrack.Create("camera", _videoSource);
        await _room.LocalParticipant.PublishTrackAsync(
            _localVideoTrack,
            new TrackPublishOptions { Source = TrackSource.SourceCamera },
            cancellationToken).ConfigureAwait(false);

        _mediaCapture = new MediaCapture();
        var settings = new MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
        };
        if (!string.IsNullOrEmpty(_cameraDeviceId))
        {
            settings.VideoDeviceId = _cameraDeviceId;
        }

        try
        {
            await _mediaCapture.InitializeAsync(settings);
        }
        catch (Exception ex)
        {
            try
            {
                if (_localVideoTrack?.Sid is { } sid)
                {
                    await _room.LocalParticipant.UnpublishTrackAsync(sid).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignore
            }

            _localVideoTrack = null;
            _videoSource?.Dispose();
            _videoSource = null;
            _mediaCapture?.Dispose();
            _mediaCapture = null;
            throw new InvalidOperationException("无法打开摄像头：" + ex.Message, ex);
        }

        MediaFrameSource? colorSource = null;
        foreach (var kv in _mediaCapture.FrameSources)
        {
            if (kv.Value.Info.SourceKind == MediaFrameSourceKind.Color)
            {
                colorSource = kv.Value;
                break;
            }
        }

        if (colorSource is null)
        {
            throw new InvalidOperationException("未找到摄像头彩色帧源");
        }

        string? subtype = null;
        foreach (var fmt in colorSource.SupportedFormats)
        {
            if (string.Equals(fmt.Subtype, MediaEncodingSubtypes.Bgra8, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fmt.Subtype, "BGRA8", StringComparison.OrdinalIgnoreCase))
            {
                subtype = fmt.Subtype;
                try
                {
                    await colorSource.SetFormatAsync(fmt);
                }
                catch
                {
                    // ignore
                }

                break;
            }
        }

        _frameReader = await _mediaCapture.CreateFrameReaderAsync(
            colorSource,
            subtype ?? MediaEncodingSubtypes.Bgra8);
        _frameReader.FrameArrived += OnCameraFrameArrived;
        var status = await _frameReader.StartAsync();
        if (status != MediaFrameReaderStartStatus.Success)
        {
            throw new InvalidOperationException("摄像头帧读取启动失败：" + status);
        }
    }

    private void OnCameraFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_videoSource is null || !_cameraEnabled)
        {
            return;
        }

        try
        {
            using var frameRef = sender.TryAcquireLatestFrame();
            var sb = frameRef?.VideoMediaFrame?.SoftwareBitmap;
            if (sb is null)
            {
                return;
            }

            // Only convert when the camera didn't deliver BGRA directly — the bitmap
            // stays valid for the whole handler, so no defensive copy is needed.
            SoftwareBitmap? converted = null;
            var bgra = sb;
            if (sb.BitmapPixelFormat != BitmapPixelFormat.Bgra8
                || sb.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                converted = SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                bgra = converted;
            }

            try
            {
                var w = bgra.PixelWidth;
                var h = bgra.PixelHeight;
                var needed = w * h * 4;
                if (needed <= 0)
                {
                    return;
                }

                const string key = "local:camera";
                var state = _frameStates.GetOrAdd(key, static _ => new TileFrameState());
                var scratch = state.GetWriteBuffer(needed);
                bgra.CopyToBuffer(scratch.AsBuffer());
                _videoSource.CaptureFrame(new VideoFrame(w, h, VideoBufferType.Bgra, scratch));

                // Latest-wins: skip the preview update while a previous one is still queued.
                if (Interlocked.CompareExchange(ref state.UiPending, 1, 0) != 0)
                {
                    return;
                }

                state.FlipWriteBuffer();
                EnqueueUi(() =>
                {
                    try
                    {
                        var tile = UpsertTile(key, "我", isLocal: true, screen: false);
                        EnsureTileBitmap(tile, w, h);
                        if (tile.Bitmap is not WriteableBitmap localBitmap)
                        {
                            return;
                        }

                        using var stream = localBitmap.PixelBuffer.AsStream();
                        stream.Write(scratch, 0, needed);
                        localBitmap.Invalidate();
                        LocalVideo = localBitmap;
                        RaiseVideo();
                    }
                    catch
                    {
                        // ignore
                    }
                    finally
                    {
                        Interlocked.Exchange(ref state.UiPending, 0);
                    }
                });
            }
            finally
            {
                converted?.Dispose();
            }
        }
        catch
        {
            // drop
        }
    }

    private async Task StopCameraAsync()
    {
        if (_frameReader is not null)
        {
            try
            {
                _frameReader.FrameArrived -= OnCameraFrameArrived;
                await _frameReader.StopAsync();
            }
            catch
            {
                // ignore
            }

            _frameReader.Dispose();
            _frameReader = null;
        }

        if (_mediaCapture is not null)
        {
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }

        if (_room?.LocalParticipant is not null && _localVideoTrack?.Sid is { } sid)
        {
            try
            {
                await _room.LocalParticipant.UnpublishTrackAsync(sid).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        _localVideoTrack = null;
        _videoSource?.Dispose();
        _videoSource = null;
    }

    private async Task StartScreenShareAsync(CancellationToken cancellationToken)
    {
        if (_room?.LocalParticipant is null)
        {
            return;
        }

        const int width = 960;
        const int height = 540;
        _screenSource = new VideoSource(width, height);
        _screenTrack = LocalVideoTrack.Create("screen", _screenSource);
        await _room.LocalParticipant.PublishTrackAsync(
            _screenTrack,
            new TrackPublishOptions { Source = TrackSource.SourceScreenshare },
            cancellationToken).ConfigureAwait(false);

        _screenPublisher = new ScreenCapturePublisher(_screenSource, width, height);
        await _screenPublisher.StartAsync().ConfigureAwait(false);

        EnqueueUi(() =>
        {
            UpsertTile("local:screen", "我 · 屏幕", isLocal: true, screen: true);
        });
    }

    private async Task StopScreenShareAsync()
    {
        if (_screenPublisher is not null)
        {
            await _screenPublisher.DisposeAsync().ConfigureAwait(false);
            _screenPublisher = null;
        }

        if (_room?.LocalParticipant is not null && _screenTrack?.Sid is { } sid)
        {
            try
            {
                await _room.LocalParticipant.UnpublishTrackAsync(sid).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        _screenTrack = null;
        _screenSource?.Dispose();
        _screenSource = null;
    }

    // —— Remote media ——

    private void OnTrackSubscribed(object? sender, TrackSubscribedEventArgs e)
    {
        try
        {
            if (e.Track is RemoteAudioTrack audioTrack)
            {
                StartRemoteAudio(audioTrack);
            }
            else if (e.Track is RemoteVideoTrack videoTrack)
            {
                var identity = e.Participant?.Identity ?? "remote";
                var name = e.Participant?.Name ?? identity;
                var isScreen = e.Publication?.Source == TrackSource.SourceScreenshare;
                StartRemoteVideo(videoTrack, identity, name, isScreen);
            }

            RefreshParticipants();
            RaiseParticipants();
        }
        catch (Exception ex)
        {
            StatusText = "订阅媒体失败：" + ex.Message;
            RaiseState();
        }
    }

    private void OnTrackUnsubscribed(object? sender, TrackSubscribedEventArgs e)
    {
        try
        {
            var sid = e.Track.Sid;
            if (!string.IsNullOrEmpty(sid))
            {
                if (_audioSinks.TryRemove(sid, out var sink))
                {
                    sink.Dispose();
                }

                if (_videoLoops.TryRemove(sid, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                var identity = e.Participant?.Identity;
                if (!string.IsNullOrEmpty(identity))
                {
                    RemoveTile(TileKey(identity!, isLocal: false, screen: false));
                    RemoveTile(TileKey(identity!, isLocal: false, screen: true));
                }
            }

            RefreshParticipants();
            RaiseParticipants();
        }
        catch
        {
            // ignore
        }
    }

    private void StartRemoteAudio(RemoteAudioTrack track)
    {
        var sid = track.Sid ?? Guid.NewGuid().ToString("N");
        if (_audioSinks.ContainsKey(sid))
        {
            return;
        }

        var sink = new RemoteAudioSink(track, SampleRate, Channels, _speakerDeviceIndex);
        if (_audioSinks.TryAdd(sid, sink))
        {
            sink.Start();
        }
        else
        {
            sink.Dispose();
        }
    }

    private void StartRemoteVideo(RemoteVideoTrack track, string identity, string displayName, bool isScreen)
    {
        var sid = track.Sid ?? Guid.NewGuid().ToString("N");
        if (_videoLoops.ContainsKey(sid))
        {
            return;
        }

        var key = TileKey(identity, isLocal: false, screen: isScreen);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _sessionCts?.Token ?? CancellationToken.None);
        if (!_videoLoops.TryAdd(sid, cts))
        {
            cts.Dispose();
            return;
        }

        EnqueueUi(() => UpsertTile(key, displayName + (isScreen ? " · 屏幕" : string.Empty), isLocal: false, screen: isScreen));

        _ = PumpRemoteFramesAsync();

        async Task PumpRemoteFramesAsync()
        {
            try
            {
                // Bgra straight from the track: WriteableBitmap's native layout, no swizzle.
                using var stream = VideoStream.FromTrack(track, VideoBufferType.Bgra);
                await foreach (var evt in stream.WithCancellation(cts.Token))
                {
                    var frame = evt.Frame;
                    if (frame is null)
                    {
                        continue;
                    }

                    var w = frame.Width;
                    var h = frame.Height;
                    var needed = w * h * 4;
                    if (needed <= 0)
                    {
                        continue;
                    }

                    var state = _frameStates.GetOrAdd(key, static _ => new TileFrameState());
                    // Latest-wins: UI still busy with the previous frame → drop this one
                    // before spending a copy on it. Prevents unbounded DispatcherQueue growth.
                    if (Interlocked.CompareExchange(ref state.UiPending, 1, 0) != 0)
                    {
                        continue;
                    }

                    try
                    {
                        var span = frame.DataBytes;
                        if (span.Length < needed)
                        {
                            Interlocked.Exchange(ref state.UiPending, 0);
                            continue;
                        }

                        var buf = state.GetWriteBuffer(needed);
                        span[..needed].CopyTo(buf);
                        state.FlipWriteBuffer();

                        EnqueueUi(() =>
                        {
                            try
                            {
                                if (!_tilesByKey.TryGetValue(key, out var tile))
                                {
                                    tile = UpsertTile(key, displayName, isLocal: false, screen: isScreen);
                                }

                                PresentRemoteFrame(tile, buf, w, h);
                                if (tile.Bitmap is null) return;

                                if (_focusedIdentity is null
                                    || string.Equals(_focusedIdentity, identity, StringComparison.Ordinal))
                                {
                                    _focusedIdentity ??= identity;
                                    FocusedRemoteVideo = tile.Bitmap;
                                    tile.IsFocused = true;
                                }

                                RaiseVideo();
                            }
                            catch
                            {
                                // ignore
                            }
                            finally
                            {
                                Interlocked.Exchange(ref state.UiPending, 0);
                            }
                        });
                    }
                    catch
                    {
                        Interlocked.Exchange(ref state.UiPending, 0);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            catch
            {
                // ended
            }
            finally
            {
                if (_videoLoops.TryRemove(sid, out var completedLoop))
                {
                    completedLoop.Dispose();
                }
            }
        }
    }

    private void OnParticipantConnected(object? sender, Participant participant)
    {
        RefreshParticipants();
        RaiseParticipants();
    }

    private void OnParticipantDisconnected(object? sender, Participant participant)
    {
        if (participant.Identity is { } id)
        {
            RemoveTile(TileKey(id, false, false));
            RemoveTile(TileKey(id, false, true));
            _qualityByIdentity.TryRemove(id, out _);
        }

        RefreshParticipants();
        RaiseParticipants();
    }

    private void OnConnectionQualityChanged(object? sender, ConnectionQualityChangedEventArgs e)
    {
        var identity = e.Participant?.Identity ?? "local";
        var q = MapQuality(e.Quality);
        _qualityByIdentity[identity] = q;

        if (e.Participant is LocalParticipant || string.Equals(identity, _room?.LocalParticipant?.Identity, StringComparison.Ordinal))
        {
            ConnectionQualityText = q;
            if (q is "差" or "丢失")
            {
                StatusText = $"弱网：连接质量{q}";
            }
        }

        EnqueueUi(() =>
        {
            foreach (var t in VideoTiles)
            {
                if (string.Equals(t.Identity, identity, StringComparison.Ordinal)
                    || (t.IsLocal && e.Participant is LocalParticipant))
                {
                    t.QualityText = q;
                }
            }
        });

        RaiseState();
        RefreshParticipants();
        RaiseParticipants();
    }

    private static string MapQuality(ConnectionQuality quality) => quality switch
    {
        ConnectionQuality.QualityExcellent => "优",
        ConnectionQuality.QualityGood => "良",
        ConnectionQuality.QualityPoor => "差",
        ConnectionQuality.QualityLost => "丢失",
        _ => "未知",
    };

    private void OnRoomDisconnected(object? sender, DisconnectReason e)
    {
        IsConnected = false;
        StatusText = "通话已断开：" + e;
        ConnectionQualityText = "断开";
        RaiseState();
        _ = DisconnectAsync();
    }

    private void OnRoomReconnecting(object? sender, EventArgs e)
    {
        IsReconnecting = true;
        ConnectionQualityText = "重连中";
        StatusText = "弱网 / 媒体重连中…";
        RaiseState();
    }

    private void OnRoomReconnected(object? sender, EventArgs e)
    {
        IsReconnecting = false;
        StatusText = "媒体已重连";
        ConnectionQualityText = "已恢复";
        RaiseState();
    }

    private void DetachRoomHandlers(Room room)
    {
        room.ParticipantConnected -= OnParticipantConnected;
        room.ParticipantDisconnected -= OnParticipantDisconnected;
        room.TrackSubscribed -= OnTrackSubscribed;
        room.TrackUnsubscribed -= OnTrackUnsubscribed;
        room.Disconnected -= OnRoomDisconnected;
        room.ConnectionQualityChanged -= OnConnectionQualityChanged;
        room.Reconnecting -= OnRoomReconnecting;
        room.Reconnected -= OnRoomReconnected;
    }

    private void RefreshParticipants()
    {
        lock (_gate)
        {
            _participants.Clear();
            if (_room is null)
            {
                return;
            }

            if (_room.LocalParticipant is { } local)
            {
                _qualityByIdentity.TryGetValue(local.Identity ?? "me", out var q);
                _participants.Add(new RealtimeMediaParticipant
                {
                    Identity = local.Identity ?? "me",
                    Name = local.Name ?? "我",
                    IsLocal = true,
                    HasAudio = _micEnabled && _localAudioTrack is not null,
                    HasVideo = (_cameraEnabled && _localVideoTrack is not null) || _screenEnabled,
                    IsScreenShare = _screenEnabled,
                    QualityText = q ?? ConnectionQualityText,
                });
            }

            foreach (var remote in _room.RemoteParticipants.Values)
            {
                var pubs = remote.TrackPublications?.Values;
                var hasAudio = false;
                var hasVideo = false;
                var isScreen = false;
                if (pubs is not null)
                {
                    foreach (var p in pubs)
                    {
                        var kind = p.Track?.Kind;
                        var source = p.Source;
                        if (kind == TrackKind.KindAudio || source == TrackSource.SourceMicrophone)
                        {
                            hasAudio = true;
                        }

                        if (kind == TrackKind.KindVideo
                            || source is TrackSource.SourceCamera or TrackSource.SourceScreenshare)
                        {
                            hasVideo = true;
                        }

                        if (source == TrackSource.SourceScreenshare)
                        {
                            isScreen = true;
                        }
                    }
                }

                var id = remote.Identity ?? remote.Sid ?? "?";
                _qualityByIdentity.TryGetValue(id, out var q);
                _participants.Add(new RealtimeMediaParticipant
                {
                    Identity = id,
                    Name = remote.Name,
                    IsLocal = false,
                    HasAudio = hasAudio,
                    HasVideo = hasVideo,
                    IsScreenShare = isScreen,
                    QualityText = q,
                });
            }
        }
    }

    private CallVideoTile UpsertTile(string key, string displayName, bool isLocal, bool screen)
    {
        if (_tilesByKey.TryGetValue(key, out var existing))
        {
            existing.DisplayName = displayName;
            return existing;
        }

        var identity = key.Contains(':') ? key.Split(':')[^1] : key;
        if (key.StartsWith("local:", StringComparison.Ordinal))
        {
            identity = "local";
        }
        else if (key.StartsWith("remote:", StringComparison.Ordinal))
        {
            // remote:{identity} or remote:{identity}:screen
            var parts = key.Split(':');
            identity = parts.Length >= 2 ? parts[1] : key;
        }

        var tile = new CallVideoTile
        {
            Identity = identity,
            DisplayName = displayName,
            IsLocal = isLocal,
            IsScreenShare = screen,
        };
        _tilesByKey[key] = tile;
        VideoTiles.Add(tile);
        return tile;
    }

    private void RemoveTile(string key)
    {
        _frameStates.TryRemove(key, out _);
        EnqueueUi(() =>
        {
            if (_tilesByKey.Remove(key, out var tile))
            {
                ReleaseTileResources(tile);
                VideoTiles.Remove(tile);
            }
        }, () => ReleaseTileByKeyFallback(key));
    }

    private static string TileKey(string identity, bool isLocal, bool screen)
    {
        if (isLocal)
        {
            return screen ? "local:screen" : "local:camera";
        }

        return screen ? $"remote:{identity}:screen" : $"remote:{identity}";
    }

    private static void EnsureTileBitmap(CallVideoTile tile, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (tile.Bitmap is not WriteableBitmap bitmap
            || bitmap.PixelWidth != width
            || bitmap.PixelHeight != height)
        {
            tile.Bitmap = new WriteableBitmap(width, height);
        }
    }

    private void PresentRemoteFrame(CallVideoTile tile, byte[] bgra, int width, int height)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
        {
            return;
        }

        if (tile.GpuBitmap is null
            || tile.FrameWidth != width
            || tile.FrameHeight != height)
        {
            tile.GpuBitmap?.Dispose();
            tile.GpuBitmap = CanvasBitmap.CreateFromBytes(
                _videoCanvasDevice,
                bgra,
                width,
                height,
                DirectXPixelFormat.B8G8R8A8UIntNormalized);
            tile.FrameWidth = width;
            tile.FrameHeight = height;
        }
        else
        {
            tile.GpuBitmap.SetPixelBytes(bgra);
        }

        // CanvasImageSource has no deterministic Dispose API. Keep the largest surface
        // seen by this tile and scale subsequent smaller adaptive-resolution frames into
        // it, so resolution oscillation does not continuously allocate native surfaces.
        var surfaceArea = (long)tile.SurfaceWidth * tile.SurfaceHeight;
        var frameArea = (long)width * height;
        if (surfaceArea > frameArea * 2)
        {
            tile.SurfaceSmallFrameCount++;
        }
        else
        {
            tile.SurfaceSmallFrameCount = 0;
        }

        var shouldShrinkSurface = tile.SurfaceSmallFrameCount >= SurfaceShrinkAfterFrames;
        if (tile.GpuSurface is null
            || width > tile.SurfaceWidth
            || height > tile.SurfaceHeight
            || shouldShrinkSurface)
        {
            var surfaceWidth = shouldShrinkSurface ? width : Math.Max(width, tile.SurfaceWidth);
            var surfaceHeight = shouldShrinkSurface ? height : Math.Max(height, tile.SurfaceHeight);
            tile.Bitmap = null;
            tile.GpuSurface = null;
            tile.GpuSurface = new CanvasImageSource(_videoCanvasDevice, surfaceWidth, surfaceHeight, 96);
            tile.SurfaceWidth = surfaceWidth;
            tile.SurfaceHeight = surfaceHeight;
            tile.SurfaceSmallFrameCount = 0;
            tile.Bitmap = tile.GpuSurface;
        }

        using var drawing = tile.GpuSurface.CreateDrawingSession(Microsoft.UI.Colors.Black);
        drawing.DrawImage(
            tile.GpuBitmap,
            new Windows.Foundation.Rect(0, 0, tile.SurfaceWidth, tile.SurfaceHeight),
            new Windows.Foundation.Rect(0, 0, width, height));
    }

    private async Task TeardownMediaAsync()
    {
        foreach (var kv in _videoLoops)
        {
            try
            {
                kv.Value.Cancel();
            }
            catch
            {
                // ignore
            }

            kv.Value.Dispose();
        }

        _videoLoops.Clear();

        foreach (var kv in _audioSinks)
        {
            kv.Value.Dispose();
        }

        _audioSinks.Clear();
        _qualityByIdentity.Clear();
        _frameStates.Clear();

        try
        {
            await StopCameraAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        try
        {
            await StopScreenShareAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        StopSidetonePlayback();

        if (_mic is not null)
        {
            try
            {
                _mic.DataAvailable -= OnMicData;
                _mic.StopRecording();
            }
            catch
            {
                // ignore
            }

            _mic.Dispose();
            _mic = null;
        }

        if (_room is not null)
        {
            try
            {
                DetachRoomHandlers(_room);
                await _room.DisconnectAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            try
            {
                _room.Dispose();
            }
            catch
            {
                // ignore
            }

            _room = null;
        }

        _localAudioTrack = null;
        _audioSource?.Dispose();
        _audioSource = null;

        try
        {
            _sessionCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _sessionCts = null;
    }

    private bool EnqueueUi(Action action, Action? onEnqueueFailure = null)
    {
        var q = _uiQueue ?? App.DispatcherQueue;
        if (q is null)
        {
            try
            {
                action();
                return true;
            }
            catch
            {
                try
                {
                    onEnqueueFailure?.Invoke();
                }
                catch
                {
                    // no dispatcher is available; cleanup remains best effort
                }

                return false;
            }
        }

        if (q.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch
            {
                // ignore
            }
        }))
        {
            return true;
        }

        try
        {
            onEnqueueFailure?.Invoke();
        }
        catch
        {
            // Queue shutdown is already in progress; cleanup remains best effort.
        }

        return false;
    }

    private void ClearVideoTiles()
    {
        foreach (var tile in VideoTiles)
        {
            ReleaseTileResources(tile);
        }

        VideoTiles.Clear();
        _tilesByKey.Clear();
    }

    private void ReleaseVideoTileResourcesFallback()
    {
        foreach (var tile in _tilesByKey.Values.ToArray())
        {
            ReleaseTileResourcesFallback(tile);
        }

        _tilesByKey.Clear();
    }

    private void ReleaseTileByKeyFallback(string key)
    {
        if (_tilesByKey.Remove(key, out var tile))
        {
            ReleaseTileResourcesFallback(tile);
        }
    }

    private static void ReleaseTileResources(CallVideoTile tile)
    {
        tile.Bitmap = null;
        tile.GpuBitmap?.Dispose();
        tile.GpuBitmap = null;
        tile.GpuSurface = null;
        tile.FrameWidth = 0;
        tile.FrameHeight = 0;
        tile.SurfaceWidth = 0;
        tile.SurfaceHeight = 0;
        tile.SurfaceSmallFrameCount = 0;
    }

    private static void ReleaseTileResourcesFallback(CallVideoTile tile)
    {
        // The dispatcher is gone, so XAML property notification may reject this thread.
        // Dispose every native resource independently and never let one failure prevent
        // cleanup of the remaining tiles.
        try
        {
            tile.GpuBitmap?.Dispose();
        }
        catch
        {
            // ignore
        }

        tile.GpuBitmap = null;
        tile.GpuSurface = null;
        tile.FrameWidth = 0;
        tile.FrameHeight = 0;
        tile.SurfaceWidth = 0;
        tile.SurfaceHeight = 0;
        tile.SurfaceSmallFrameCount = 0;
        try
        {
            tile.Bitmap = null;
        }
        catch
        {
            // The process/window is shutting down; no UI dispatcher remains.
        }
    }

    private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void RaiseParticipants() => ParticipantsChanged?.Invoke(this, EventArgs.Empty);

    private void RaiseVideo() => VideoFrameUpdated?.Invoke(this, EventArgs.Empty);

    internal static string? NormalizeLiveKitUrl(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var u = endpoint.Trim();
        if (u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "wss://" + u["https://".Length..];
        }

        if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return "ws://" + u["http://".Length..];
        }

        return u;
    }

    /// <summary>
    /// Per-tile frame handoff state. The producer (camera callback / remote track loop)
    /// writes into the current write buffer, flips, then the UI reads that buffer while
    /// the producer would write the other one. Combined with the UiPending gate, at most
    /// one UI update is queued per tile and steady-state allocations are zero.
    /// </summary>
    private sealed class TileFrameState
    {
        private const int ShrinkAfterFrames = 120;

        public int UiPending;

        private byte[]? _bufferA;
        private byte[]? _bufferB;
        private int _writeIndex;
        private int _smallFrameCount;

        public byte[] GetWriteBuffer(int needed)
        {
            var currentCapacity = Math.Max(_bufferA?.Length ?? 0, _bufferB?.Length ?? 0);
            if (currentCapacity > (long)needed * 2)
            {
                _smallFrameCount++;
                if (_smallFrameCount >= ShrinkAfterFrames)
                {
                    // UiPending guarantees the previous presentation completed before a
                    // new frame reaches this method, so both buffers are safe to replace.
                    _bufferA = new byte[needed];
                    _bufferB = new byte[needed];
                    _writeIndex = 0;
                    _smallFrameCount = 0;
                }
            }
            else
            {
                _smallFrameCount = 0;
            }

            if (_writeIndex == 0)
            {
                if (_bufferA is null || _bufferA.Length < needed)
                {
                    _bufferA = new byte[needed];
                }

                return _bufferA;
            }

            if (_bufferB is null || _bufferB.Length < needed)
            {
                _bufferB = new byte[needed];
            }

            return _bufferB;
        }

        public void FlipWriteBuffer() => _writeIndex ^= 1;
    }

    private sealed class RemoteAudioSink : IDisposable
    {
        private readonly RemoteAudioTrack _track;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly BufferedWaveProvider _buffer;
        private WaveOutEvent _waveOut;
        private CancellationTokenSource? _cts;
        private byte[] _pcmBuffer = [];
        private int _deviceNumber;

        public RemoteAudioSink(RemoteAudioTrack track, int sampleRate, int channels, int deviceNumber)
        {
            _track = track;
            _sampleRate = sampleRate;
            _channels = channels;
            _deviceNumber = deviceNumber;
            _buffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 16, channels))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2),
            };
            _waveOut = CreateWaveOut();
        }

        public void SetDevice(int deviceNumber)
        {
            _deviceNumber = deviceNumber;
            try
            {
                _waveOut.Stop();
            }
            catch
            {
                // ignore
            }

            _waveOut.Dispose();
            _waveOut = CreateWaveOut();
            _waveOut.Play();
        }

        private WaveOutEvent CreateWaveOut()
        {
            var wo = new WaveOutEvent
            {
                DesiredLatency = 100,
                DeviceNumber = _deviceNumber >= 0 ? _deviceNumber : 0,
            };
            wo.Init(_buffer);
            return wo;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _waveOut.Play();
            _ = PumpAsync(_cts.Token);
        }

        private async Task PumpAsync(CancellationToken ct)
        {
            try
            {
                using var stream = AudioStream.FromTrack(_track, (uint)_sampleRate, (uint)_channels);
                await foreach (var evt in stream.WithCancellation(ct))
                {
                    var frame = evt.Frame;
                    if (frame is null)
                    {
                        continue;
                    }

                    var shorts = frame.DataArray;
                    if (shorts is null || shorts.Length == 0)
                    {
                        var bytes = frame.DataBytes;
                        if (bytes.Length > 0)
                        {
                            EnsurePcmBuffer(bytes.Length);
                            bytes.CopyTo(_pcmBuffer);
                            _buffer.AddSamples(_pcmBuffer, 0, bytes.Length);
                        }

                        continue;
                    }

                    var pcmLength = shorts.Length * 2;
                    EnsurePcmBuffer(pcmLength);
                    Buffer.BlockCopy(shorts, 0, _pcmBuffer, 0, pcmLength);
                    _buffer.AddSamples(_pcmBuffer, 0, pcmLength);
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            catch
            {
                // ended
            }
        }

        private void EnsurePcmBuffer(int requiredLength)
        {
            if (_pcmBuffer.Length < requiredLength)
            {
                _pcmBuffer = new byte[requiredLength];
            }
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ignore
            }

            try
            {
                _waveOut.Stop();
            }
            catch
            {
                // ignore
            }

            _waveOut.Dispose();
            _cts?.Dispose();
        }
    }
}
