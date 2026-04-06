using System.Runtime.InteropServices;
using Valve.VR;
using Vortice.Direct3D11;
using Vortice.DXGI;
using VRDiscordOverlay.Config;

namespace VRDiscordOverlay.VR;

public class VrButton : IDisposable
{
    private ulong _overlayHandle;
    private readonly string _overlayKey;
    private readonly string _overlayName;
    private readonly ID3D11Device _d3dDevice;
    private readonly ID3D11DeviceContext _d3dContext;
    private ID3D11Texture2D?[] _textures = new ID3D11Texture2D?[2];
    private int _texIndex;
    private int _texWidth, _texHeight;
    private bool _initialized;
    private bool _visible;

    public event Action? OnClicked;

    public VrButton(string overlayKey, string overlayName,
                    ID3D11Device device, ID3D11DeviceContext context)
    {
        _overlayKey = overlayKey;
        _overlayName = overlayName;
        _d3dDevice = device;
        _d3dContext = context;
    }

    public bool Initialize()
    {
        var err = OpenVR.Overlay.CreateOverlay(_overlayKey, _overlayName, ref _overlayHandle);
        if (err != EVROverlayError.None) return false;

        OpenVR.Overlay.SetOverlayInputMethod(_overlayHandle, VROverlayInputMethod.Mouse);
        OpenVR.Overlay.SetOverlayFlag(_overlayHandle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, true);
        OpenVR.Overlay.SetOverlaySortOrder(_overlayHandle, 100);
        OpenVR.Overlay.SetOverlayWidthInMeters(_overlayHandle, 0.04f);
        // Start hidden — only show when SteamVR dashboard is open
        OpenVR.Overlay.HideOverlay(_overlayHandle);
        _initialized = true;
        return true;
    }

    public void Show()
    {
        if (_initialized && !_visible)
        {
            OpenVR.Overlay.ShowOverlay(_overlayHandle);
            _visible = true;
        }
    }

    public void Hide()
    {
        if (_initialized && _visible)
        {
            OpenVR.Overlay.HideOverlay(_overlayHandle);
            _visible = false;
        }
    }

    public void UpdatePosition(ButtonSettings settings)
    {
        if (!_initialized) return;

        OpenVR.Overlay.SetOverlayWidthInMeters(_overlayHandle, settings.Scale);
        OpenVR.Overlay.SetOverlayAlpha(_overlayHandle, settings.Opacity);

        float yawRad = settings.Yaw * MathF.PI / 180f;
        float pitchRad = settings.Pitch * MathF.PI / 180f;
        float rollRad = settings.Rotation * MathF.PI / 180f;
        float cy = MathF.Cos(yawRad), sy = MathF.Sin(yawRad);
        float cp = MathF.Cos(pitchRad), sp = MathF.Sin(pitchRad);
        float cr = MathF.Cos(rollRad), sr = MathF.Sin(rollRad);

        var transform = new HmdMatrix34_t();
        transform.m0 = cy * cr + sy * sp * sr;   transform.m1 = -cy * sr + sy * sp * cr;  transform.m2 = sy * cp;
        transform.m4 = cp * sr;                    transform.m5 = cp * cr;                   transform.m6 = -sp;
        transform.m8 = -sy * cr + cy * sp * sr;  transform.m9 = sy * sr + cy * sp * cr;    transform.m10 = cy * cp;
        transform.m3 = settings.X;
        transform.m7 = settings.Y;
        transform.m11 = settings.Z;

        switch (settings.AttachTo)
        {
            case "left":
                var leftIdx = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
                if (leftIdx != OpenVR.k_unTrackedDeviceIndexInvalid)
                    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(_overlayHandle, leftIdx, ref transform);
                break;
            case "right":
                var rightIdx = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
                if (rightIdx != OpenVR.k_unTrackedDeviceIndexInvalid)
                    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(_overlayHandle, rightIdx, ref transform);
                break;
            case "hmd":
                OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(
                    _overlayHandle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref transform);
                break;
            case "playspace":
                OpenVR.Overlay.SetOverlayTransformAbsolute(
                    _overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref transform);
                break;
        }
    }

    public bool PollClick()
    {
        if (!_initialized || !_visible) return false;

        var evt = new VREvent_t();
        uint size = (uint)Marshal.SizeOf<VREvent_t>();
        while (OpenVR.Overlay.PollNextOverlayEvent(_overlayHandle, ref evt, size))
        {
            if (evt.eventType == (uint)EVREventType.VREvent_MouseButtonDown)
            {
                OnClicked?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void SetTexture(byte[] bgraPixels, int width, int height)
    {
        if (!_initialized) return;

        if (width != _texWidth || height != _texHeight)
        {
            foreach (var t in _textures) t?.Dispose();
            _textures = new ID3D11Texture2D?[2];
            _texWidth = width;
            _texHeight = height;

            var mouseScale = new HmdVector2_t { v0 = width, v1 = height };
            OpenVR.Overlay.SetOverlayMouseScale(_overlayHandle, ref mouseScale);

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
            _textures[0] = _d3dDevice.CreateTexture2D(desc);
            _textures[1] = _d3dDevice.CreateTexture2D(desc);
        }

        _texIndex = 1 - _texIndex;
        var tex = _textures[_texIndex]!;
        var mapped = _d3dContext.Map(tex, 0, MapMode.WriteDiscard);
        try
        {
            unsafe
            {
                int srcStride = width * 4;
                byte* dst = (byte*)mapped.DataPointer;
                fixed (byte* src = bgraPixels)
                {
                    for (int row = 0; row < height; row++)
                        Buffer.MemoryCopy(src + row * srcStride, dst + row * mapped.RowPitch, mapped.RowPitch, srcStride);
                }
            }
        }
        finally { _d3dContext.Unmap(tex, 0); }

        var texInfo = new Texture_t
        {
            handle = tex.NativePointer,
            eType = ETextureType.DirectX,
            eColorSpace = EColorSpace.Auto
        };
        OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref texInfo);
        _d3dContext.Flush();
    }

    public void Dispose()
    {
        if (_initialized)
        {
            OpenVR.Overlay.DestroyOverlay(_overlayHandle);
            _initialized = false;
        }
        foreach (var t in _textures) t?.Dispose();
    }
}
