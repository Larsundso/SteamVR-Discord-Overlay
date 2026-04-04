using System.Runtime.InteropServices;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using VRDiscordOverlay.Config;

namespace VRDiscordOverlay.VR;

public class SteamVrOverlay : IDisposable
{
    private ulong _overlayHandle;
    private bool _initialized;
    private readonly AppSettings _settings;
    private const string OverlayKey = "vr.discord.vc.overlay";
    private const string OverlayName = "Discord VC Overlay";

    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private ID3D11Texture2D?[] _textures = new ID3D11Texture2D?[2];
    private int _texIndex;
    private int _texWidth, _texHeight;

    public bool IsInitialized => _initialized;

    public SteamVrOverlay(AppSettings settings)
    {
        _settings = settings;
    }

    public bool Initialize()
    {
        EVRInitError error = EVRInitError.None;
        OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

        if (error != EVRInitError.None)
        {
            ConsoleUI.Log($"Could not connect to SteamVR ({error})");
            return false;
        }

        var overlayError = OpenVR.Overlay.CreateOverlay(OverlayKey, OverlayName, ref _overlayHandle);
        if (overlayError != EVROverlayError.None)
        {
            ConsoleUI.Log($"Could not create overlay ({overlayError})");
            return false;
        }

        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            Array.Empty<FeatureLevel>(),
            out _d3dDevice,
            out _d3dContext);

        OpenVR.Overlay.SetOverlayWidthInMeters(_overlayHandle, _settings.OverlayWidth);
        OpenVR.Overlay.SetOverlayAlpha(_overlayHandle, _settings.OverlayOpacity);
        OpenVR.Overlay.ShowOverlay(_overlayHandle);

        _initialized = true;
        UpdatePosition();
        RegisterManifest();
        ConsoleUI.Log("SteamVR overlay ready");
        return true;
    }

    private float _lastHeightMeters;

    public void UpdatePosition(float? contentHeightMeters = null)
    {
        if (!_initialized) return;

        if (contentHeightMeters.HasValue)
            _lastHeightMeters = contentHeightMeters.Value;

        float yOffset = -_lastHeightMeters / 2f;
        float yawRad = _settings.OverlayYaw * MathF.PI / 180f;
        float pitchRad = _settings.OverlayPitch * MathF.PI / 180f;
        float cy = MathF.Cos(yawRad), sy = MathF.Sin(yawRad);
        float cp = MathF.Cos(pitchRad), sp = MathF.Sin(pitchRad);

        var transform = new HmdMatrix34_t();
        transform.m0 = cy;   transform.m1 = sy * sp;  transform.m2 = sy * cp;  transform.m3 = _settings.OverlayX;
        transform.m4 = 0f;   transform.m5 = cp;       transform.m6 = -sp;      transform.m7 = _settings.OverlayY + yOffset;
        transform.m8 = -sy;  transform.m9 = cy * sp;  transform.m10 = cy * cp; transform.m11 = _settings.OverlayZ;

        OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(
            _overlayHandle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref transform);
    }

    private int _lastW, _lastH;

    public void SetTexture(byte[] bgraPixels, int width, int height)
    {
        if (!_initialized || _d3dDevice == null || _d3dContext == null) return;

        float metersPerPixel = _settings.OverlayWidth / 200f;
        float widthMeters = width * metersPerPixel;
        float heightMeters = height * metersPerPixel;

        OpenVR.Overlay.SetOverlayWidthInMeters(_overlayHandle, widthMeters);
        OpenVR.Overlay.SetOverlayAlpha(_overlayHandle, _settings.OverlayOpacity);
        if (width != _lastW || height != _lastH)
        {
            _lastW = width;
            _lastH = height;
        }
        UpdatePosition(heightMeters);

        EnsureTextures(width, height);

        var tex = _textures[_texIndex]!;
        var mapped = _d3dContext.Map(tex, 0, MapMode.WriteDiscard);
        try
        {
            int srcRowBytes = width * 4;
            for (int row = 0; row < height; row++)
            {
                Marshal.Copy(bgraPixels, row * srcRowBytes,
                    mapped.DataPointer + row * (int)mapped.RowPitch, srcRowBytes);
            }
        }
        finally
        {
            _d3dContext.Unmap(tex, 0);
        }

        var vrTexture = new Texture_t
        {
            handle = tex.NativePointer,
            eType = ETextureType.DirectX,
            eColorSpace = EColorSpace.Auto,
        };
        OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref vrTexture);
        _d3dContext.Flush();

        _texIndex = 1 - _texIndex;
    }

    private void EnsureTextures(int width, int height)
    {
        if (_textures[0] != null && _texWidth == width && _texHeight == height)
            return;

        _textures[0]?.Dispose();
        _textures[1]?.Dispose();
        _texWidth = width;
        _texHeight = height;

        var desc = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.Write,
        };
        _textures[0] = _d3dDevice!.CreateTexture2D(desc);
        _textures[1] = _d3dDevice!.CreateTexture2D(desc);
        _texIndex = 0;
    }

    private void RegisterManifest()
    {
        if (!_settings.AutoStartWithSteamVR) return;
        try
        {
            var manifestPath = Path.Combine(AppContext.BaseDirectory, "vrmanifest.json");
            if (File.Exists(manifestPath))
            {
                var appError = OpenVR.Applications.AddApplicationManifest(manifestPath, false);
                if (appError == EVRApplicationError.None)
                {
                    OpenVR.Applications.SetApplicationAutoLaunch("vr.discord.vc.overlay", true);
                    ConsoleUI.Log("Auto-start with SteamVR enabled");
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _textures[0]?.Dispose();
        _textures[1]?.Dispose();
        _d3dContext?.Dispose();
        _d3dDevice?.Dispose();
        if (_initialized)
        {
            OpenVR.Overlay.DestroyOverlay(_overlayHandle);
            OpenVR.Shutdown();
            _initialized = false;
        }
    }
}
