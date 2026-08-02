#pragma once

#ifdef SOLARWIN_NATIVE_EXPORTS
#define SOLARWIN_API extern "C" __declspec(dllexport)
#else
#define SOLARWIN_API extern "C" __declspec(dllimport)
#endif

// Extracts one video frame using Media Foundation hardware transforms, scales it
// with the D3D11 video processor and writes a PNG. Returns an HRESULT.
SOLARWIN_API long __stdcall SolarWin_GenerateVideoThumbnail(
    const wchar_t* inputPath,
    const wchar_t* outputPath,
    unsigned int outputWidth,
    unsigned int outputHeight,
    long long position100ns);

SOLARWIN_API int __stdcall SolarWin_GetLastVideoThumbnailStage();
SOLARWIN_API long long __stdcall SolarWin_GetLastVideoThumbnailDiagnostic(int index);
