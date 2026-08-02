using System.Runtime.InteropServices;

namespace SolarWin.Services;

/// <summary>Small P/Invoke boundary around the Media Foundation + D3D11 thumbnail DLL.</summary>
internal static class NativeVideoThumbnailer
{
    [DllImport("SolarWin.Native.dll", EntryPoint = "SolarWin_GenerateVideoThumbnail",
        ExactSpelling = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int GenerateVideoThumbnail(
        string inputPath,
        string outputPath,
        uint outputWidth,
        uint outputHeight,
        long position100ns);

    public static async Task<bool> GenerateAsync(
        string inputPath,
        string outputPath,
        int width,
        int height,
        TimeSpan position,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();

        var tempPath = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var result = await Task.Run(
                () => GenerateVideoThumbnail(
                    inputPath,
                    tempPath,
                    (uint)Math.Clamp(width, 1, 4096),
                    (uint)Math.Clamp(height, 1, 4096),
                    Math.Max(0, position.Ticks)),
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (result < 0 || !File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Move(tempPath, outputPath, overwrite: true);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // Best-effort temporary cleanup.
            }
        }
    }
}
