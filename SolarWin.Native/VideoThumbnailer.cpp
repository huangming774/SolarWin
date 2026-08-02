#include "VideoThumbnailer.h"

#include <Windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <mfapi.h>
#include <mferror.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <wincodec.h>
#include <wrl/client.h>
#include <algorithm>
#include <cstring>
#include <filesystem>
#include <vector>

using Microsoft::WRL::ComPtr;

#define RETURN_IF_FAILED(expression) do { const HRESULT _hr = (expression); if (FAILED(_hr)) return _hr; } while (false)

namespace
{
    thread_local int g_lastStage = 0;
    thread_local long long g_diagnostics[4]{};
    class Runtime final
    {
    public:
        Runtime()
        {
            _com = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
            _mf = MFStartup(MF_VERSION, MFSTARTUP_LITE);
        }

        ~Runtime()
        {
            if (SUCCEEDED(_mf)) MFShutdown();
            if (SUCCEEDED(_com)) CoUninitialize();
        }

        HRESULT Status() const noexcept
        {
            return FAILED(_com) && _com != RPC_E_CHANGED_MODE ? _com : _mf;
        }

    private:
        HRESULT _com = E_FAIL;
        HRESULT _mf = E_FAIL;
    };

    HRESULT WritePng(
        const wchar_t* path,
        const D3D11_MAPPED_SUBRESOURCE& mapped,
        UINT width,
        UINT height)
    {
        ComPtr<IWICImagingFactory> factory;
        RETURN_IF_FAILED(CoCreateInstance(
            CLSID_WICImagingFactory2, nullptr, CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&factory)));

        ComPtr<IWICStream> stream;
        RETURN_IF_FAILED(factory->CreateStream(&stream));
        RETURN_IF_FAILED(stream->InitializeFromFilename(path, GENERIC_WRITE));

        ComPtr<IWICBitmapEncoder> encoder;
        RETURN_IF_FAILED(factory->CreateEncoder(GUID_ContainerFormatPng, nullptr, &encoder));
        RETURN_IF_FAILED(encoder->Initialize(stream.Get(), WICBitmapEncoderNoCache));

        ComPtr<IWICBitmapFrameEncode> frame;
        ComPtr<IPropertyBag2> properties;
        RETURN_IF_FAILED(encoder->CreateNewFrame(&frame, &properties));
        RETURN_IF_FAILED(frame->Initialize(properties.Get()));
        RETURN_IF_FAILED(frame->SetSize(width, height));
        WICPixelFormatGUID format = GUID_WICPixelFormat32bppBGRA;
        RETURN_IF_FAILED(frame->SetPixelFormat(&format));
        if (format != GUID_WICPixelFormat32bppBGRA) return WINCODEC_ERR_UNSUPPORTEDPIXELFORMAT;

        const UINT size = mapped.RowPitch * height;
        RETURN_IF_FAILED(frame->WritePixels(
            height, mapped.RowPitch, size,
            static_cast<BYTE*>(mapped.pData)));
        RETURN_IF_FAILED(frame->Commit());
        return encoder->Commit();
    }

    HRESULT CreateDevice(
        ComPtr<ID3D11Device>& device,
        ComPtr<ID3D11DeviceContext>& context,
        ComPtr<IMFDXGIDeviceManager>& manager)
    {
        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT;
        D3D_FEATURE_LEVEL featureLevel{};
        HRESULT hr = D3D11CreateDevice(
            nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
            nullptr, 0, D3D11_SDK_VERSION,
            &device, &featureLevel, &context);
        if (FAILED(hr))
        {
            hr = D3D11CreateDevice(
                nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags,
                nullptr, 0, D3D11_SDK_VERSION,
                &device, &featureLevel, &context);
        }
        RETURN_IF_FAILED(hr);

        UINT resetToken = 0;
        RETURN_IF_FAILED(MFCreateDXGIDeviceManager(&resetToken, &manager));
        return manager->ResetDevice(device.Get(), resetToken);
    }

    HRESULT ConfigureReader(
        const wchar_t* inputPath,
        IMFDXGIDeviceManager* manager,
        ComPtr<IMFSourceReader>& reader,
        UINT& sourceWidth,
        UINT& sourceHeight,
        UINT requestedWidth,
        UINT requestedHeight,
        UINT& targetWidth,
        UINT& targetHeight)
    {
        g_lastStage = 20;
        ComPtr<IMFAttributes> attributes;
        RETURN_IF_FAILED(MFCreateAttributes(&attributes, 5));
        g_lastStage = 21;
        RETURN_IF_FAILED(attributes->SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, TRUE));
        RETURN_IF_FAILED(attributes->SetUINT32(MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, TRUE));
        RETURN_IF_FAILED(attributes->SetUINT32(MF_SOURCE_READER_DISCONNECT_MEDIASOURCE_ON_SHUTDOWN, TRUE));
        // The decoder is still allowed to use a hardware MFT. We deliberately accept
        // a system-memory RGB frame here because several display drivers deadlock when
        // SourceReader advanced processing is combined with decoder texture arrays.
        // The frame is uploaded once and all scaling remains on D3D11.
        (void)manager;
        g_lastStage = 22;
        RETURN_IF_FAILED(MFCreateSourceReaderFromURL(inputPath, attributes.Get(), &reader));
        g_lastStage = 23;
        RETURN_IF_FAILED(reader->SetStreamSelection(static_cast<DWORD>(MF_SOURCE_READER_ALL_STREAMS), FALSE));
        RETURN_IF_FAILED(reader->SetStreamSelection(static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM), TRUE));

        g_lastStage = 24;
        ComPtr<IMFMediaType> nativeType;
        RETURN_IF_FAILED(reader->GetNativeMediaType(static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM), 0, &nativeType));
        RETURN_IF_FAILED(MFGetAttributeSize(nativeType.Get(), MF_MT_FRAME_SIZE, &sourceWidth, &sourceHeight));

        g_lastStage = 25;
        ComPtr<IMFMediaType> outputType;
        RETURN_IF_FAILED(MFCreateMediaType(&outputType));
        RETURN_IF_FAILED(outputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
        RETURN_IF_FAILED(outputType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32));
        RETURN_IF_FAILED(outputType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));
        const double ratio = std::min(
            static_cast<double>(requestedWidth) / std::max(1u, sourceWidth),
            static_cast<double>(requestedHeight) / std::max(1u, sourceHeight));
        targetWidth = std::max(1u, static_cast<UINT>(sourceWidth * ratio));
        targetHeight = std::max(1u, static_cast<UINT>(sourceHeight * ratio));
        RETURN_IF_FAILED(MFSetAttributeSize(outputType.Get(), MF_MT_FRAME_SIZE, sourceWidth, sourceHeight));
        RETURN_IF_FAILED(outputType->SetUINT32(MF_MT_DEFAULT_STRIDE, sourceWidth * 4));
        g_lastStage = 26;
        RETURN_IF_FAILED(reader->SetCurrentMediaType(
            static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM), nullptr, outputType.Get()));
        ComPtr<IMFMediaType> actualType;
        RETURN_IF_FAILED(reader->GetCurrentMediaType(
            static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM), &actualType));
        RETURN_IF_FAILED(MFGetAttributeSize(actualType.Get(), MF_MT_FRAME_SIZE, &sourceWidth, &sourceHeight));
        const double actualRatio = std::min(
            static_cast<double>(requestedWidth) / std::max(1u, sourceWidth),
            static_cast<double>(requestedHeight) / std::max(1u, sourceHeight));
        targetWidth = std::max(1u, static_cast<UINT>(sourceWidth * actualRatio));
        targetHeight = std::max(1u, static_cast<UINT>(sourceHeight * actualRatio));
        return S_OK;
    }

    HRESULT ReadDxgiFrame(
        IMFSourceReader* reader,
        ID3D11Device* device,
        UINT width,
        UINT height,
        LONGLONG position,
        ComPtr<ID3D11Texture2D>& texture,
        UINT& subresource)
    {
        PROPVARIANT seek{};
        PropVariantInit(&seek);
        seek.vt = VT_I8;
        seek.hVal.QuadPart = std::max<LONGLONG>(0, position);
        const HRESULT seekHr = reader->SetCurrentPosition(GUID_NULL, seek);
        PropVariantClear(&seek);
        if (FAILED(seekHr) && position != 0) return seekHr;

        for (int attempt = 0; attempt < 120; ++attempt)
        {
            DWORD flags = 0;
            ComPtr<IMFSample> sample;
            RETURN_IF_FAILED(reader->ReadSample(
                static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM), 0,
                nullptr, &flags, nullptr, &sample));
            if (flags & MF_SOURCE_READERF_ENDOFSTREAM) return MF_E_END_OF_STREAM;
            if (!sample) continue;

            ComPtr<IMFMediaBuffer> buffer;
            RETURN_IF_FAILED(sample->ConvertToContiguousBuffer(&buffer));
            ComPtr<IMFDXGIBuffer> dxgiBuffer;
            if (SUCCEEDED(buffer.As(&dxgiBuffer)))
            {
                RETURN_IF_FAILED(dxgiBuffer->GetResource(IID_PPV_ARGS(&texture)));
                RETURN_IF_FAILED(dxgiBuffer->GetSubresourceIndex(&subresource));
                return S_OK;
            }

            const UINT required = width * height * 4;
            std::vector<BYTE> packed(required);
            ComPtr<IMF2DBuffer> buffer2d;
            if (SUCCEEDED(buffer.As(&buffer2d)))
            {
                BYTE* scanline0 = nullptr;
                LONG pitch = 0;
                RETURN_IF_FAILED(buffer2d->Lock2D(&scanline0, &pitch));
                g_diagnostics[2] = pitch;
                const UINT rowBytes = width * 4;
                if (static_cast<UINT>(std::abs(pitch)) < rowBytes)
                {
                    buffer2d->Unlock2D();
                    continue;
                }
                for (UINT y = 0; y < height; ++y)
                {
                    std::memcpy(
                        packed.data() + static_cast<size_t>(y) * rowBytes,
                        scanline0 + static_cast<ptrdiff_t>(y) * pitch,
                        rowBytes);
                }
                buffer2d->Unlock2D();
            }
            else
            {
                BYTE* data = nullptr;
                DWORD maxLength = 0;
                DWORD currentLength = 0;
                RETURN_IF_FAILED(buffer->Lock(&data, &maxLength, &currentLength));
                g_diagnostics[2] = width * 4;
                g_diagnostics[3] = currentLength;
                if (currentLength < required)
                {
                    buffer->Unlock();
                    continue;
                }
                std::memcpy(packed.data(), data, required);
                buffer->Unlock();
            }

            D3D11_TEXTURE2D_DESC desc{};
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            desc.SampleDesc.Count = 1;
            desc.Usage = D3D11_USAGE_DEFAULT;
            desc.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;
            D3D11_SUBRESOURCE_DATA initial{};
            initial.pSysMem = packed.data();
            initial.SysMemPitch = width * 4;
            const HRESULT createHr = device->CreateTexture2D(&desc, &initial, &texture);
            RETURN_IF_FAILED(createHr);
            subresource = 0;
            return S_OK;
        }
        return MF_E_END_OF_STREAM;
    }

    HRESULT ScaleWithVideoProcessor(
        ID3D11Device* device,
        ID3D11DeviceContext* context,
        ID3D11Texture2D* input,
        UINT inputSubresource,
        UINT outputWidth,
        UINT outputHeight,
        ComPtr<ID3D11Texture2D>& output)
    {
        D3D11_TEXTURE2D_DESC inputDesc{};
        input->GetDesc(&inputDesc);

        ComPtr<ID3D11VideoDevice> videoDevice;
        ComPtr<ID3D11VideoContext> videoContext;
        RETURN_IF_FAILED(device->QueryInterface(IID_PPV_ARGS(&videoDevice)));
        RETURN_IF_FAILED(context->QueryInterface(IID_PPV_ARGS(&videoContext)));

        D3D11_VIDEO_PROCESSOR_CONTENT_DESC content{};
        content.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
        content.InputWidth = inputDesc.Width;
        content.InputHeight = inputDesc.Height;
        content.OutputWidth = outputWidth;
        content.OutputHeight = outputHeight;
        content.Usage = D3D11_VIDEO_USAGE_PLAYBACK_NORMAL;

        ComPtr<ID3D11VideoProcessorEnumerator> enumerator;
        RETURN_IF_FAILED(videoDevice->CreateVideoProcessorEnumerator(&content, &enumerator));
        ComPtr<ID3D11VideoProcessor> processor;
        RETURN_IF_FAILED(videoDevice->CreateVideoProcessor(enumerator.Get(), 0, &processor));

        D3D11_TEXTURE2D_DESC outputDesc{};
        outputDesc.Width = outputWidth;
        outputDesc.Height = outputHeight;
        outputDesc.MipLevels = 1;
        outputDesc.ArraySize = 1;
        outputDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        outputDesc.SampleDesc.Count = 1;
        outputDesc.Usage = D3D11_USAGE_DEFAULT;
        outputDesc.BindFlags = D3D11_BIND_RENDER_TARGET;
        RETURN_IF_FAILED(device->CreateTexture2D(&outputDesc, nullptr, &output));

        D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputViewDesc{};
        inputViewDesc.FourCC = 0;
        inputViewDesc.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
        inputViewDesc.Texture2D.MipSlice = 0;
        inputViewDesc.Texture2D.ArraySlice = inputSubresource;
        ComPtr<ID3D11VideoProcessorInputView> inputView;
        RETURN_IF_FAILED(videoDevice->CreateVideoProcessorInputView(
            input, enumerator.Get(), &inputViewDesc, &inputView));

        D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC outputViewDesc{};
        outputViewDesc.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;
        outputViewDesc.Texture2D.MipSlice = 0;
        ComPtr<ID3D11VideoProcessorOutputView> outputView;
        RETURN_IF_FAILED(videoDevice->CreateVideoProcessorOutputView(
            output.Get(), enumerator.Get(), &outputViewDesc, &outputView));

        RECT sourceRect{ 0, 0, static_cast<LONG>(inputDesc.Width), static_cast<LONG>(inputDesc.Height) };
        RECT destRect{ 0, 0, static_cast<LONG>(outputWidth), static_cast<LONG>(outputHeight) };
        videoContext->VideoProcessorSetStreamSourceRect(processor.Get(), 0, TRUE, &sourceRect);
        videoContext->VideoProcessorSetStreamDestRect(processor.Get(), 0, TRUE, &destRect);
        videoContext->VideoProcessorSetOutputTargetRect(processor.Get(), TRUE, &destRect);

        D3D11_VIDEO_PROCESSOR_STREAM stream{};
        stream.Enable = TRUE;
        stream.pInputSurface = inputView.Get();
        return videoContext->VideoProcessorBlt(processor.Get(), outputView.Get(), 0, 1, &stream);
    }

    HRESULT CopyAndWritePng(
        ID3D11Device* device,
        ID3D11DeviceContext* context,
        ID3D11Texture2D* texture,
        const wchar_t* outputPath)
    {
        D3D11_TEXTURE2D_DESC desc{};
        texture->GetDesc(&desc);
        D3D11_TEXTURE2D_DESC stagingDesc = desc;
        stagingDesc.Usage = D3D11_USAGE_STAGING;
        stagingDesc.BindFlags = 0;
        stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        stagingDesc.MiscFlags = 0;

        ComPtr<ID3D11Texture2D> staging;
        RETURN_IF_FAILED(device->CreateTexture2D(&stagingDesc, nullptr, &staging));
        context->CopyResource(staging.Get(), texture);

        D3D11_MAPPED_SUBRESOURCE mapped{};
        RETURN_IF_FAILED(context->Map(staging.Get(), 0, D3D11_MAP_READ, 0, &mapped));
        const HRESULT hr = WritePng(outputPath, mapped, desc.Width, desc.Height);
        context->Unmap(staging.Get(), 0);
        return hr;
    }
}

long __stdcall SolarWin_GenerateVideoThumbnail(
    const wchar_t* inputPath,
    const wchar_t* outputPath,
    unsigned int outputWidth,
    unsigned int outputHeight,
    long long position100ns)
{
    g_lastStage = 0;
    std::fill(std::begin(g_diagnostics), std::end(g_diagnostics), 0);
    if (!inputPath || !outputPath || !*inputPath || !*outputPath || outputWidth == 0 || outputHeight == 0)
        return E_INVALIDARG;

    Runtime runtime;
    if (FAILED(runtime.Status())) return runtime.Status();

    try
    {
        const auto parent = std::filesystem::path(outputPath).parent_path();
        if (!parent.empty()) std::filesystem::create_directories(parent);

        ComPtr<ID3D11Device> device;
        ComPtr<ID3D11DeviceContext> context;
        ComPtr<IMFDXGIDeviceManager> manager;
        g_lastStage = 1;
        HRESULT hr = CreateDevice(device, context, manager);
        if (FAILED(hr)) return hr;

        ComPtr<IMFSourceReader> reader;
        UINT sourceWidth = 0;
        UINT sourceHeight = 0;
        UINT targetWidth = 0;
        UINT targetHeight = 0;
        g_lastStage = 2;
        hr = ConfigureReader(
            inputPath, manager.Get(), reader, sourceWidth, sourceHeight,
            outputWidth, outputHeight, targetWidth, targetHeight);
        if (FAILED(hr)) return hr;

        ComPtr<ID3D11Texture2D> inputTexture;
        UINT inputSubresource = 0;
        HRESULT readHr = ReadDxgiFrame(
            reader.Get(), device.Get(), sourceWidth, sourceHeight,
            position100ns, inputTexture, inputSubresource);
        if (FAILED(readHr) && position100ns > 0)
            readHr = ReadDxgiFrame(
                reader.Get(), device.Get(), sourceWidth, sourceHeight,
                0, inputTexture, inputSubresource);
        g_lastStage = 3;
        if (FAILED(readHr)) return readHr;
        g_diagnostics[0] = sourceWidth;
        g_diagnostics[1] = sourceHeight;

        ComPtr<ID3D11Texture2D> scaled;
        g_lastStage = 4;
        hr = ScaleWithVideoProcessor(
            device.Get(), context.Get(), inputTexture.Get(), inputSubresource,
            targetWidth, targetHeight, scaled);
        if (FAILED(hr)) return hr;
        g_lastStage = 5;
        hr = CopyAndWritePng(device.Get(), context.Get(), scaled.Get(), outputPath);
        if (FAILED(hr)) return hr;
        g_lastStage = 6;
        return S_OK;
    }
    catch (const std::filesystem::filesystem_error&)
    {
        return HRESULT_FROM_WIN32(ERROR_CANNOT_MAKE);
    }
    catch (...)
    {
        return E_FAIL;
    }
}

int __stdcall SolarWin_GetLastVideoThumbnailStage()
{
    return g_lastStage;
}

long long __stdcall SolarWin_GetLastVideoThumbnailDiagnostic(int index)
{
    return index >= 0 && index < 4 ? g_diagnostics[index] : 0;
}
