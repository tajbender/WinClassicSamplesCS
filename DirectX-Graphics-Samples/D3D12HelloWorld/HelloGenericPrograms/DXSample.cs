using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Vanara;

internal abstract class DXSample
{
	protected float m_aspectRatio;

	// Override to be able to start without Dx11on12 UI for PIX. PIX doesn't support 11 on 12.
	protected bool m_enableUI;

	protected bool m_useWarpDevice = false;
	private const uint D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT = 256;
	private readonly string m_assetsPath;

	protected DXSample(int width, int height, string name)
	{
		Title = name;
		m_assetsPath = GetAssetsPath();

		UpdateForSizeChange(width, height);
		CheckTearingSupport();
	}

	public int Height { get; protected set; }

	public bool TearingSupport { get; protected set; }

	public string Title { get; protected set; }

	public int Width { get; protected set; }

	public Win32Application? Win32App { get; internal set; }

	public RECT WindowBounds { get; set; }

	public virtual IDXGISwapChain? GetSwapchain() => null;

	public virtual void OnDestroy() { }

	public virtual void OnDisplayChanged() { }

	public virtual void OnInit() { }

	public virtual void OnKeyDown(VK key) { }

	public virtual void OnKeyUp(VK key) { }

	public virtual void OnLeftButtonDown(uint x, uint y) { }

	public virtual void OnLeftButtonUp(uint x, uint y) { }

	public virtual void OnMouseMove(uint x, uint y) { }

	public virtual void OnRender() { }

	public virtual void OnSizeChanged(int width, int height, bool minimized) => UpdateForSizeChange(width, height);

	public virtual void OnUpdate() { }

	public virtual void OnWindowMoved(int x, int y) { }

	public virtual void ParseCommandLineArgs(string[] args)
	{
		if (m_useWarpDevice = args.Any(a => a.ToLower() is "-warp" or "/warp"))
			Title += " (WARP)";
		m_enableUI = args.Any(a => a.ToLower() is "-disableui" or "/disableui");
	}

	public void UpdateForSizeChange(int clientWidth, int clientHeight)
	{
		Width = clientWidth;
		Height = clientHeight;
		m_aspectRatio = clientWidth / (float)clientHeight;
	}

	protected internal static uint CalculateConstantBufferByteSize(uint byteSize) => (byteSize + (D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT - 1)) & ~(D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT - 1);

	protected internal static ID3DBlob CompileShader(string filename, in D3D_SHADER_MACRO defines, string entrypoint, string target)
	{
		D3DCOMPILE compileFlags = 0;
#if DEBUG
		compileFlags = D3DCOMPILE.D3DCOMPILE_DEBUG | D3DCOMPILE.D3DCOMPILE_SKIP_OPTIMIZATION;
#endif

		HRESULT hr = D3DCompileFromFile(filename, defines, default, entrypoint, target, compileFlags, 0, out var byteCode, out var errors);
		if (errors is not null)
		{
			Debug.WriteLine(Marshal.PtrToStringAnsi(errors.GetBufferPointer()) ?? "");
		}
		hr.ThrowIfFailed();

		return byteCode;
	}

	[Conditional("DEBUG")]
	protected internal static void DumpVal<T>(T val, string name) where T : struct => DumpVal(SafeCoTaskMemHandle.CreateFromStructure(val), name);

	[Conditional("DEBUG")]
	protected internal static void DumpVal(SafeAllocatedMemoryHandleBase val, string name) => Debug.WriteLine($"{name}\r\n{val.DangerousGetHandle().ToHexDumpString(val.Size, 32, 32, 0, true)}");

	protected internal static string GetAssetsPath() => Path.GetDirectoryName(Environment.ProcessPath) ?? "";

	protected internal static void NAME_D3D12_OBJECT<T>(T obj, [CallerArgumentExpression(nameof(obj))] string? objName = null) where T : ID3D12Object => SetName(obj, objName);

	protected internal static void NAME_D3D12_OBJECT_INDEXED<T>(IReadOnlyList<T> obj, SizeT idx, [CallerArgumentExpression(nameof(obj))] string? objName = null) where T : ID3D12Object => SetNameIndexed(obj[idx], objName, idx);

	protected internal static HRESULT ReadDataFromDDSFile(string filename, out byte[] data, ref uint offset, ref uint size)
	{
		if (ReadDataFromFile(filename, out data).Failed)
		{
			return HRESULT.E_FAIL;
		}

		// DDS files always start with the same magic number.
		const uint DDS_MAGIC = 0x20534444;
		uint magicNumber = BitConverter.ToUInt32(data);
		if (magicNumber != DDS_MAGIC)
		{
			return HRESULT.E_FAIL;
		}

		unsafe
		{
			fixed (byte* pData = data)
			{
				var ddsHeader = (DDS_HEADER*)(pData + sizeof(uint));
				if (ddsHeader->size != sizeof(DDS_HEADER) || ddsHeader->ddsPixelFormat.size != sizeof(DDS_PIXELFORMAT))
				{
					return HRESULT.E_FAIL;
				}

				nint ddsDataOffset = sizeof(uint) + sizeof(DDS_HEADER);
				offset = (uint)ddsDataOffset;
				size -= offset;
			}
		}

		return HRESULT.S_OK;
	}

	protected internal static HRESULT ReadDataFromFile(string filename, out byte[] data)
	{
		data = System.IO.File.ReadAllBytes(filename);
		return HRESULT.S_OK;
	}

	[Conditional("DEBUG")]
	protected internal static void SetName(ID3D12Object pObject, string? name) => pObject.SetName(name ?? "");

	[Conditional("DEBUG")]
	protected internal static void SetNameIndexed(ID3D12Object pObject, string? name, uint index) => pObject.SetName($"{name ?? ""}[{index}]");

	protected internal void CheckTearingSupport()
	{
		if (FunctionHelper.IidGetObj(CreateDXGIFactory1, out IDXGIFactory6 factory).Succeeded)
		{
			BOOL allowTearing = false;
			TearingSupport = factory.CheckFeatureSupport(ref allowTearing, DXGI_FEATURE.DXGI_FEATURE_PRESENT_ALLOW_TEARING).Succeeded && allowTearing;
		}
	}

	protected static void GetHardwareAdapter(IDXGIFactory1 pFactory, out IDXGIAdapter1 ppAdapter, bool requestHighPerformanceAdapter = false)
	{
		IDXGIFactory6 dxgiFactory = pFactory as IDXGIFactory6 ?? throw new ArgumentException("Unable to get IDXGIFactory6 instance", nameof(pFactory));
		var gpuPref = requestHighPerformanceAdapter ? DXGI_GPU_PREFERENCE.DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE : DXGI_GPU_PREFERENCE.DXGI_GPU_PREFERENCE_UNSPECIFIED;
		IDXGIAdapter1? adapter = dxgiFactory.EnumAdapterByGpuPreference<IDXGIAdapter1>(gpuPref).Where(IsHW12Adapter).FirstOrDefault();
		adapter ??= dxgiFactory.EnumAdapters1().Where(IsHW12Adapter).FirstOrDefault() ?? throw new ArgumentException("Unable to get IDXGIAdapter1 instance", nameof(pFactory));
		ppAdapter = adapter;

		static bool IsHW12Adapter(IDXGIAdapter1 adapter) => !adapter.GetDesc1().Flags.IsFlagSet(DXGI_ADAPTER_FLAG.DXGI_ADAPTER_FLAG_SOFTWARE) &&
			D3D12CreateDevice(adapter, D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, typeof(ID3D12Device).GUID) == HRESULT.S_FALSE;
	}

	// Helper function for resolving the full path of assets.
	protected string GetAssetFullPath(string assetName) => Path.Combine(m_assetsPath, assetName);

	// Helper function for setting the window's title text.
	protected void SetCustomWindowText(string text) => Win32App!.Text = $"{Title}: {text}";

	[StructLayout(LayoutKind.Sequential)]
	private struct DDS_HEADER
	{
		public uint size;
		public uint flags;
		public uint height;
		public uint width;
		public uint pitchOrLinearSize;
		public uint depth;
		public uint mipMapCount;
		public unsafe fixed uint reserved1[11];
		public DDS_PIXELFORMAT ddsPixelFormat;
		public uint caps;
		public uint caps2;
		public uint caps3;
		public uint caps4;
		public uint reserved2;
	};

	[StructLayout(LayoutKind.Sequential)]
	private struct DDS_PIXELFORMAT
	{
		public uint size;
		public uint flags;
		public uint fourCC;
		public uint rgbBitCount;
		public uint rBitMask;
		public uint gBitMask;
		public uint bBitMask;
		public uint aBitMask;
	};
}