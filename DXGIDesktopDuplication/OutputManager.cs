using static Program;
using static Vanara.PInvoke.D3D12;

internal class OUTPUTMANAGER : IDisposable
{
	IDXGISwapChain1? m_SwapChain;
	ID3D11Device? m_Device;
	IDXGIFactory2? m_Factory;
	ID3D11DeviceContext? m_DeviceContext;
	ID3D11RenderTargetView? m_RTV;
	ID3D11SamplerState? m_SamplerLinear;
	ID3D11BlendState? m_BlendState;
	ID3D11VertexShader? m_VertexShader;
	ID3D11PixelShader? m_PixelShader;
	ID3D11InputLayout? m_InputLayout;
	ID3D11Texture2D? m_SharedSurf;
	IDXGIKeyedMutex? m_KeyMutex;
	HWND m_WindowHandle;
	bool m_NeedsResize;
	uint m_OcclusionCookie;

	public void Dispose() => CleanRefs();

	//
	// Indicates that window has been resized.
	//
	public void WindowResize() => m_NeedsResize = true;

	//
	// Initialize all state
	//
	public DUPL_RETURN InitOutput(HWND Window, int SingleOutput, out uint OutCount, out RECT DeskBounds)
	{
		OutCount = 0;
		DeskBounds = default;

		// Store window handle
		m_WindowHandle = Window;

		// Driver types supported
		D3D_DRIVER_TYPE[] DriverTypes =
		[
D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP,
D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_REFERENCE,
];
		int NumDriverTypes = DriverTypes.Length;

		// Feature levels supported
		D3D_FEATURE_LEVEL[] FeatureLevels =
		[
D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1
];
		uint NumFeatureLevels = (uint)FeatureLevels.Length;
		// Create device
		HRESULT hr = 0;
		for (uint DriverTypeIndex = 0; DriverTypeIndex < NumDriverTypes; ++DriverTypeIndex)
		{
			hr = D3D11CreateDevice(default, DriverTypes[DriverTypeIndex], default, 0, FeatureLevels, NumFeatureLevels,
				D3D11_SDK_VERSION, out m_Device, out var FeatureLevel, out m_DeviceContext);
			if (hr.Succeeded)
			{
				// Device creation succeeded, no need to loop anymore
				break;
			}
		}
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Device creation in OUTPUTMANAGER failed", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Get DXGI factory
		IDXGIDevice? DxgiDevice = m_Device as IDXGIDevice;
		if (DxgiDevice is null)
		{
			return ProcessFailure(default, "Failed to QI for DXGI Device", "Error", hr, default);
		}

		IDXGIAdapter? DxgiAdapter = DxgiDevice.GetParent<IDXGIAdapter>();
		DxgiDevice = default;
		if (DxgiAdapter is null)
		{
			return ProcessFailure(m_Device, "Failed to get parent DXGI Adapter", "Error", hr, SystemTransitionsExpectedErrors);
		}

		m_Factory = DxgiAdapter.GetParent<IDXGIFactory2>();
		DxgiAdapter = default;
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to get parent DXGI Factory", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Register for occlusion status windows message
		m_OcclusionCookie = m_Factory!.RegisterOcclusionStatusWindow(Window, OCCLUSION_STATUS_MSG);

		// Get window size
		GetClientRect(m_WindowHandle, out var WindowRect);
		uint Width = (uint)(WindowRect.right - WindowRect.left);
		uint Height = (uint)(WindowRect.bottom - WindowRect.top);

		// Create swapchain for window
		DXGI_SWAP_CHAIN_DESC1 SwapChainDesc = new()
		{
			SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
			BufferCount = 2,
			Width = Width,
			Height = Height,
			Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
			BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
			SampleDesc = new() { Count = 1 }
		};
		m_SwapChain = m_Factory.CreateSwapChainForHwnd(m_Device!, Window, SwapChainDesc, default, default);

		// Disable the ALT-ENTER shortcut for entering full-screen mode
		m_Factory.MakeWindowAssociation(Window, DXGI_MWA.DXGI_MWA_NO_ALT_ENTER);

		// Create shared texture
		DUPL_RETURN Return = CreateSharedSurf(SingleOutput, out OutCount, out DeskBounds);
		if (Return != DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			return Return;
		}

		// Make new render target view
		Return = MakeRTV();
		if (Return != DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			return Return;
		}

		// Set view port
		SetViewPort(Width, Height);

		// Create the sample state
		D3D11_SAMPLER_DESC SampDesc = new()
		{
			Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
			AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
			AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
			AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
			ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
			MinLOD = 0,
			MaxLOD = D3D11_FLOAT32_MAX
		};
		hr = m_Device!.CreateSamplerState(SampDesc, out m_SamplerLinear);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create sampler state in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Create the blend state
		D3D11_RENDER_TARGET_BLEND_DESC target = new()
		{
			BlendEnable = true,
			SrcBlend = D3D11_BLEND.D3D11_BLEND_SRC_ALPHA,
			DestBlend = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
			BlendOp = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
			SrcBlendAlpha = D3D11_BLEND.D3D11_BLEND_ONE,
			DestBlendAlpha = D3D11_BLEND.D3D11_BLEND_ZERO,
			BlendOpAlpha = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
			RenderTargetWriteMask = (byte)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL
		};
		D3D11_BLEND_DESC BlendStateDesc = new()
		{
			AlphaToCoverageEnable = false,
			IndependentBlendEnable = false,
			RenderTarget = [target, default, default, default, default, default, default, default]
		};
		hr = m_Device.CreateBlendState(BlendStateDesc, out m_BlendState);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create blend state in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Initialize shaders
		Return = InitShaders();
		if (Return != DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			return Return;
		}

		GetWindowRect(m_WindowHandle, out WindowRect);
		MoveWindow(m_WindowHandle, WindowRect.left, WindowRect.top, (DeskBounds.right - DeskBounds.left) / 2, (DeskBounds.bottom - DeskBounds.top) / 2, true);

		return Return;
	}

	//
	// Recreate shared texture
	//
	DUPL_RETURN CreateSharedSurf(int SingleOutput, out uint OutCount, out RECT DeskBounds)
	{
		HRESULT hr = 0;
		OutCount = 0;
		DeskBounds = default;

		// Get DXGI resources
		IDXGIDevice? DxgiDevice = m_Device as IDXGIDevice;
		if (DxgiDevice is null)
		{
			return ProcessFailure(default, "Failed to QI for DXGI Device", "Error", hr);
		}

		IDXGIAdapter? DxgiAdapter = DxgiDevice.GetParent<IDXGIAdapter>();
		DxgiDevice = default;
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to get parent DXGI Adapter", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Set initial values so that we always catch the right coordinates
		DeskBounds.left = int.MaxValue;
		DeskBounds.right = int.MinValue;
		DeskBounds.top = int.MaxValue;
		DeskBounds.bottom = int.MinValue;

		IDXGIOutput? DxgiOutput = default;

		// Figure out right dimensions for full size desktop texture and # of outputs to duplicate
		uint OutputCount = 0;
		if (SingleOutput < 0)
		{
			hr = HRESULT.S_OK;
			for (; hr.Succeeded; ++OutputCount)
			{
				if (DxgiOutput is not null)
				{
					DxgiOutput = default;
				}
				hr = DxgiAdapter!.EnumOutputs(OutputCount, out DxgiOutput);
				if (DxgiOutput is not null && (hr != HRESULT.DXGI_ERROR_NOT_FOUND))
				{
					DXGI_OUTPUT_DESC DesktopDesc = DxgiOutput.GetDesc();

					DeskBounds.left = int.Min(DesktopDesc.DesktopCoordinates.left, DeskBounds.left);
					DeskBounds.top = int.Min(DesktopDesc.DesktopCoordinates.top, DeskBounds.top);
					DeskBounds.right = int.Max(DesktopDesc.DesktopCoordinates.right, DeskBounds.right);
					DeskBounds.bottom = int.Max(DesktopDesc.DesktopCoordinates.bottom, DeskBounds.bottom);
				}
			}

			--OutputCount;
		}
		else
		{
			hr = DxgiAdapter!.EnumOutputs((uint)SingleOutput, out DxgiOutput);
			if (hr.Failed)
			{
				DxgiAdapter = default;
				return ProcessFailure(m_Device, "Output specified to be duplicated does not exist", "Error", hr);
			}
			DXGI_OUTPUT_DESC DesktopDesc = DxgiOutput!.GetDesc();
			DeskBounds = DesktopDesc.DesktopCoordinates;

			DxgiOutput = default;

			OutputCount = 1;
		}

		DxgiAdapter = default;

		// Set passed in output count ref variable OutCount = OutputCount;

		if (OutputCount == 0)
		{
			// We could not find any outputs, the system must be in a transition so return expected error
			// so we will attempt to recreate
			return DUPL_RETURN.DUPL_RETURN_ERROR_EXPECTED;
		}

		// Create shared texture for all duplication threads to draw into
		D3D11_TEXTURE2D_DESC DeskTexD = new()
		{
			Width = (uint)(DeskBounds.right - DeskBounds.left),
			Height = (uint)(DeskBounds.bottom - DeskBounds.top),
			MipLevels = 1,
			ArraySize = 1,
			Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
			SampleDesc = new() { Count = 1 },
			Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
			BindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
			CPUAccessFlags = 0,
			MiscFlags = D3D11_RESOURCE_MISC_FLAG.D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX
		};
		hr = m_Device!.CreateTexture2D(DeskTexD, default, out m_SharedSurf);
		if (hr.Failed)
		{
			if (OutputCount != 1)
			{
				// If we are duplicating the complete desktop we try to create a single texture to hold the
				// complete desktop image and blit updates from the per output DDA interface. The GPU can
				// always support a texture size of the maximum resolution of any single output but there is no
				// guarantee that it can support a texture size of the desktop.
				// The sample only use this large texture to display the desktop image in a single window using DX
				// we could revert back to using GDI to update the window in this failure case.
				return ProcessFailure(m_Device, "Failed to create DirectX shared texture - we are attempting to create a texture the size of the complete desktop and this may be larger than the maximum texture size of your GPU. Please try again using the -output command line parameter to duplicate only 1 monitor or configure your computer to a single monitor configuration", "Error", hr, SystemTransitionsExpectedErrors);
			}
			else
			{
				return ProcessFailure(m_Device, "Failed to create shared texture", "Error", hr, SystemTransitionsExpectedErrors);
			}
		}

		// Get keyed mutex
		m_KeyMutex = m_SharedSurf as IDXGIKeyedMutex;
		if (m_KeyMutex is null)
		{
			return ProcessFailure(m_Device, "Failed to query for keyed mutex in OUTPUTMANAGER", "Error", hr);
		}

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Present to the application window
	//
	public DUPL_RETURN UpdateApplicationWindow(in PTR_INFO PointerInfo, [In, Out] ref bool Occluded)
	{
		// In a typical desktop duplication application there would be an application running on one system collecting the desktop images
		// and another application running on a different system that receives the desktop images via a network and display the image. This
		// sample contains both these aspects into a single application.
		// This routine is the part of the sample that displays the desktop image onto the display

		// Try and acquire sync on common display buffer
		HRESULT hr = m_KeyMutex!.AcquireSync(1, 100);
		if (hr == (int)WAIT_STATUS.WAIT_TIMEOUT)
		{
			// Another thread has the keyed mutex so try again later
			return DUPL_RETURN.DUPL_RETURN_SUCCESS;
		}
		else if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to acquire Keyed mutex in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Got mutex, so draw
		DUPL_RETURN Ret = DrawFrame();
		if (Ret == DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			// We have keyed mutex so we can access the mouse info
			if (PointerInfo.Visible)
			{
				// Draw mouse into texture
				Ret = DrawMouse(PointerInfo);
			}
		}

		// Release keyed mutex
		hr = m_KeyMutex.ReleaseSync(0);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to Release Keyed mutex in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Present to window if all worked
		if (Ret == DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			// Present to window
			hr = m_SwapChain!.Present(1, 0);
			if (hr.Failed)
			{
				return ProcessFailure(m_Device, "Failed to present", "Error", hr, SystemTransitionsExpectedErrors);
			}
			else if (hr == (int)DXGI_STATUS.DXGI_STATUS_OCCLUDED)
			{
				Occluded = true;
			}
		}

		return Ret;
	}

	//
	// Returns shared handle
	//
	public HANDLE GetSharedHandle()
	{
		HANDLE Hnd = default;

		// QI IDXGIResource interface to synchronized shared surface.
		IDXGIResource? DXGIResource = m_SharedSurf as IDXGIResource;
		if (DXGIResource is not null)
		{
			// Obtain handle to IDXGIResource object.
			DXGIResource.GetSharedHandle(out Hnd);
			DXGIResource = default;
		}

		return Hnd;
	}

	//
	// Draw frame into backbuffer
	//
	DUPL_RETURN DrawFrame()
	{
		HRESULT hr;

		// If window was resized, resize swapchain
		if (m_NeedsResize)
		{
			DUPL_RETURN Ret = ResizeSwapChain();
			if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
			{
				return Ret;
			}
			m_NeedsResize = false;
		}

		// Vertices for drawing whole texture
		SafeNativeArray<VERTEX> Vertices = [
			new(new XMFLOAT3(-1.0f, -1.0f, 0), new XMFLOAT2(0.0f, 1.0f)),
			new(new XMFLOAT3(-1.0f, 1.0f, 0), new XMFLOAT2(0.0f, 0.0f)),
			new(new XMFLOAT3(1.0f, -1.0f, 0), new XMFLOAT2(1.0f, 1.0f)),
			new(new XMFLOAT3(1.0f, -1.0f, 0), new XMFLOAT2(1.0f, 1.0f)),
			new(new XMFLOAT3(-1.0f, 1.0f, 0), new XMFLOAT2(0.0f, 0.0f)),
			new(new XMFLOAT3(1.0f, 1.0f, 0), new XMFLOAT2(1.0f, 0.0f)),
		];

		m_SharedSurf!.GetDesc(out var FrameDesc);

		SafeCoTaskMemStruct<D3D11_SHADER_RESOURCE_VIEW_DESC> ShaderDesc = new D3D11_SHADER_RESOURCE_VIEW_DESC
		{
			Format = FrameDesc.Format,
			ViewDimension = D3D11_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2D,
			Texture2D = new() { MostDetailedMip = FrameDesc.MipLevels - 1, MipLevels = FrameDesc.MipLevels }
		};

		// Create new shader resource view
		hr = m_Device!.CreateShaderResourceView(m_SharedSurf, ShaderDesc, out var ShaderResource);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create shader resource when drawing a frame", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Set resources
		uint Stride = (uint)Marshal.SizeOf(typeof(VERTEX));
		uint Offset = 0;
		float[] blendFactor = [0.0f, 0.0f, 0.0f, 0.0f];
		m_DeviceContext!.OMSetBlendState(default, blendFactor, 0xffffffff);
		m_DeviceContext.OMSetRenderTargets(1, [m_RTV!], default);
		m_DeviceContext.VSSetShader(m_VertexShader, default, 0);
		m_DeviceContext.PSSetShader(m_PixelShader, default, 0);
		m_DeviceContext.PSSetShaderResources(0, 1, [ShaderResource!]);
		m_DeviceContext.PSSetSamplers(0, 1, [m_SamplerLinear!]);
		m_DeviceContext.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

		D3D11_BUFFER_DESC BufferDesc = new()
		{
			Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
			ByteWidth = (uint)(Marshal.SizeOf(typeof(VERTEX)) * NUMVERTICES),
			BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
			CPUAccessFlags = 0
		};
		SafeCoTaskMemStruct<D3D11_SUBRESOURCE_DATA> InitData = new D3D11_SUBRESOURCE_DATA() { pSysMem = Vertices };

		// Create vertex buffer
		hr = m_Device.CreateBuffer(BufferDesc, InitData, out var VertexBuffer);
		if (hr.Failed)
		{
			ShaderResource = default;
			return ProcessFailure(m_Device, "Failed to create vertex buffer when drawing a frame", "Error", hr, SystemTransitionsExpectedErrors);
		}
		m_DeviceContext.IASetVertexBuffers(0, 1, [VertexBuffer!], [Stride], [Offset]);

		// Draw textured quad onto render target
		m_DeviceContext.Draw(NUMVERTICES, 0);
		VertexBuffer = default;

		// Release shader resource
		ShaderResource = default;
		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Process both masked and monochrome pointers
	//
	DUPL_RETURN ProcessMonoMask(bool IsMono, [In, Out] ref PTR_INFO PtrInfo, out int PtrWidth, out int PtrHeight, out int PtrLeft, out int PtrTop, out IntPtr InitBuffer, out D3D11_BOX Box)
	{
		PtrWidth = PtrHeight = PtrLeft = PtrTop = 0;
		InitBuffer = default;
		Box = default;

		// Desktop dimensions
		m_SharedSurf!.GetDesc(out var FullDesc);
		int DesktopWidth = (int)FullDesc.Width;
		int DesktopHeight = (int)FullDesc.Height;

		// Pointer position
		int GivenLeft = PtrInfo.Position.x;
		int GivenTop = PtrInfo.Position.y;

		// Figure out if any adjustment is needed for out of bound positions
		if (GivenLeft < 0)
		{
			PtrWidth = GivenLeft + (int)(PtrInfo.ShapeInfo.Width);
		}

		else if ((GivenLeft + (int)(PtrInfo.ShapeInfo.Width)) > DesktopWidth)
		{
			PtrWidth = DesktopWidth - GivenLeft;
		}
		else
		{
			PtrWidth = (int)(PtrInfo.ShapeInfo.Width);
		}

		if (IsMono)
		{
			PtrInfo.ShapeInfo.Height = PtrInfo.ShapeInfo.Height / 2;
		}

		if (GivenTop < 0)
		{
			PtrHeight = GivenTop + (int)(PtrInfo.ShapeInfo.Height);
		}
		else if ((GivenTop + (int)(PtrInfo.ShapeInfo.Height)) > DesktopHeight)
		{
			PtrHeight = DesktopHeight - GivenTop;
		}
		else
		{
			PtrHeight = (int)(PtrInfo.ShapeInfo.Height);
		}

		if (IsMono)
		{
			PtrInfo.ShapeInfo.Height *= 2;
		}

		PtrLeft = (GivenLeft < 0) ? 0 : GivenLeft;
		PtrTop = (GivenTop < 0) ? 0 : GivenTop;

		// Staging buffer/texture
		D3D11_TEXTURE2D_DESC CopyBufferDesc = new()
		{
			Width = (uint)PtrWidth,
			Height = (uint)PtrHeight,
			MipLevels = 1,
			ArraySize = 1,
			Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
			SampleDesc = new() { Count = 1 },
			Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
			BindFlags = 0,
			CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
			MiscFlags = 0
		};

		HRESULT hr = m_Device!.CreateTexture2D(CopyBufferDesc, default, out var CopyBuffer);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed creating staging texture for pointer", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Copy needed part of desktop image
		Box.left = (uint)PtrLeft;
		Box.top = (uint)PtrTop;
		Box.right = (uint)(PtrLeft + PtrWidth);
		Box.bottom = (uint)(PtrTop + PtrHeight);
		using (var pBox = new PinnedObject(Box))
			m_DeviceContext!.CopySubresourceRegion(CopyBuffer!, 0, 0, 0, 0, m_SharedSurf, 0, (IntPtr)pBox);

		// QI for IDXGISurface
		IDXGISurface? CopySurface = CopyBuffer as IDXGISurface;
		CopyBuffer = default;
		if (hr.Failed)
		{
			return ProcessFailure(default, "Failed to QI staging texture into IDXGISurface for pointer", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Map pixels
		CopySurface!.Map(out var MappedSurface, DXGI_MAP.DXGI_MAP_READ);

		// New mouseshape buffer
		InitBuffer = Marshal.AllocHGlobal(PtrWidth * PtrHeight * BPP);
		if (InitBuffer == 0)
		{
			return ProcessFailure(default, "Failed to allocate memory for new mouse shape buffer.", "Error", HRESULT.E_OUTOFMEMORY);
		}

		uint DesktopPitchInPixels = (uint)MappedSurface.Pitch / (uint)Marshal.SizeOf(typeof(uint));

		// What to skip (pixel offset)
		int SkipX = (GivenLeft < 0) ? (-1 * GivenLeft) : (0);
		int SkipY = (GivenTop < 0) ? (-1 * GivenTop) : (0);
		unsafe
		{
			uint* InitBuffer32 = (uint*)InitBuffer;
			uint* Desktop32 = (uint*)MappedSurface.pBits;
			byte* Buffer32 = (byte*)PtrInfo.PtrShapeBuffer;
			if (IsMono)
			{
				for (int Row = 0; Row < PtrHeight; ++Row)
				{
					// Set mask
					byte Mask = 0x80;
					Mask = (byte)(Mask >> ((int)SkipX % 8));
					for (int Col = 0; Col < PtrWidth; ++Col)
					{
						// Get masks using appropriate offsets
						byte AndMask = (byte)(Buffer32[((Col + SkipX) / 8) + ((Row + SkipY) * ((int)PtrInfo.ShapeInfo.Pitch))] & Mask);
						byte XorMask = (byte)(Buffer32[((Col + SkipX) / 8) + ((Row + SkipY + ((int)PtrInfo.ShapeInfo.Height / 2)) * ((int)PtrInfo.ShapeInfo.Pitch))] & Mask);
						uint AndMask32 = (AndMask != 0) ? 0xFFFFFFFFu : 0xFF000000;
						uint XorMask32 = (XorMask != 0) ? 0x00FFFFFFu : 0x00000000;

						// Set[] new = new[] Set = new[] new = new[] new = new[] new = new[] new = new[] new = new[] new = new new[] pixel = new new[] InitBuffer32 = new pixel[(ref ref Row PtrWidth) + Col] = (Desktop32[(ref Row DesktopPitchInPixels) + Col] & AndMask32) ^ XorMask32;

						// Adjust mask
						if (Mask == 0x01)
						{
							Mask = 0x80;
						}
						else
						{
							Mask = (byte)(Mask >> 1);
						}
					}
				}
			}
			else
			{
				// Iterate through pixels
				for (int Row = 0; Row < PtrHeight; ++Row)
				{
					for (int Col = 0; Col < PtrWidth; ++Col)
					{
						// Set up mask
						uint MaskVal = 0xFF000000 & Buffer32[(Col + SkipX) + ((Row + SkipY) * (PtrInfo.ShapeInfo.Pitch / Marshal.SizeOf(typeof(uint))))];
						if (MaskVal != 0)
						{
							// Mask was 0xFF
							InitBuffer32[(Row * PtrWidth) + Col] = (Desktop32[(Row * DesktopPitchInPixels) + Col] ^ Buffer32[(Col + SkipX) + ((Row + SkipY) * (PtrInfo.ShapeInfo.Pitch / sizeof(uint)))]) | 0xFF000000;
						}
						else
						{
							// Mask was 0x00
							InitBuffer32[(Row * PtrWidth) + Col] = Buffer32[(Col + SkipX) + ((Row + SkipY) * (PtrInfo.ShapeInfo.Pitch / sizeof(uint)))] | 0xFF000000;
						}
					}
				}
			}
		}

		// Done with resource
		CopySurface.Unmap();
		CopySurface = default;
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to unmap surface for pointer", "Error", hr, SystemTransitionsExpectedErrors);
		}

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Draw mouse provided in buffer to backbuffer
	//
	DUPL_RETURN DrawMouse(PTR_INFO PtrInfo)
	{
		// Vars to be used
		ID3D11Texture2D? MouseTex = default;
		ID3D11ShaderResourceView? ShaderRes = default;
		ID3D11Buffer? VertexBufferMouse = default;

		// Position will be changed based on mouse position

		m_SharedSurf!.GetDesc(out var FullDesc);
		int DesktopWidth = (int)FullDesc.Width;
		int DesktopHeight = (int)FullDesc.Height;

		// Center of desktop dimensions
		int CenterX = (DesktopWidth / 2);
		int CenterY = (DesktopHeight / 2);

		// Clipping adjusted coordinates / dimensions
		int PtrWidth;
		int PtrHeight;
		int PtrLeft;
		int PtrTop;

		// Buffer used if necessary (in case of monochrome or masked pointer)
		IntPtr InitBuffer = default;

		// Used for copying pixels
		D3D11_BOX Box = new()
		{
			front = 0,
			back = 1
		};

		D3D11_TEXTURE2D_DESC Desc = new()
		{
			MipLevels = 1,
			ArraySize = 1,
			Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
			SampleDesc = new() { Count = 1 },
			Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
			BindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
			CPUAccessFlags = 0,
			MiscFlags = 0
		};

		// Set shader resource properties
		SafeCoTaskMemStruct<D3D11_SHADER_RESOURCE_VIEW_DESC> SDesc = new D3D11_SHADER_RESOURCE_VIEW_DESC()
		{
			Format = Desc.Format,
			ViewDimension = D3D11_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2D,
			Texture2D = new() { MostDetailedMip = Desc.MipLevels - 1, MipLevels = Desc.MipLevels }
		};

		switch (PtrInfo.ShapeInfo.Type)
		{
			case DXGI_OUTDUPL_POINTER_SHAPE_TYPE.DXGI_OUTDUPL_POINTER_SHAPE_TYPE_COLOR:
				{
					PtrLeft = PtrInfo.Position.x;
					PtrTop = PtrInfo.Position.y;

					PtrWidth = (int)(PtrInfo.ShapeInfo.Width);
					PtrHeight = (int)(PtrInfo.ShapeInfo.Height);
					break;
				}

			case DXGI_OUTDUPL_POINTER_SHAPE_TYPE.DXGI_OUTDUPL_POINTER_SHAPE_TYPE_MONOCHROME:
				{
					ProcessMonoMask(true, ref PtrInfo, out PtrWidth, out PtrHeight, out PtrLeft, out PtrTop, out InitBuffer, out Box);
					break;
				}

			case DXGI_OUTDUPL_POINTER_SHAPE_TYPE.DXGI_OUTDUPL_POINTER_SHAPE_TYPE_MASKED_COLOR:
				{
					ProcessMonoMask(false, ref PtrInfo, out PtrWidth, out PtrHeight, out PtrLeft, out PtrTop, out InitBuffer, out Box);
					break;
				}

			default:
				PtrWidth = PtrHeight = PtrLeft = PtrTop = 0;
				break;
		}

		// Vertices for drawing whole texture
		SafeNativeArray<VERTEX> Vertices = [
			new(new XMFLOAT3((PtrLeft - CenterX) / (float)CenterX, -1 * ((PtrTop + PtrHeight) - CenterY) / (float)CenterY, 0), new XMFLOAT2(0.0f, 1.0f)),
			new(new XMFLOAT3((PtrLeft - CenterX) / (float)CenterX, -1 * (PtrTop - CenterY) / (float)CenterY, 0), new XMFLOAT2(0.0f, 0.0f)),
			new(new XMFLOAT3(((PtrLeft + PtrWidth) - CenterX) / (float)CenterX, -1 * ((PtrTop + PtrHeight) - CenterY) / (float)CenterY, 0), new XMFLOAT2(1.0f, 1.0f)),
			new(new XMFLOAT3(((PtrLeft + PtrWidth) - CenterX) / (float)CenterX, -1 * ((PtrTop + PtrHeight) - CenterY) / (float)CenterY, 0), new XMFLOAT2(1.0f, 1.0f)),
			new(new XMFLOAT3((PtrLeft - CenterX) / (float)CenterX, -1 * (PtrTop - CenterY) / (float)CenterY, 0), new XMFLOAT2(0.0f, 0.0f)),
			new(new XMFLOAT3(((PtrLeft + PtrWidth) - CenterX) / (float)CenterX, -1 * (PtrTop - CenterY) / (float)CenterY, 0), new XMFLOAT2(1.0f, 0.0f)),
		];

		// Set texture properties
		Desc.Width = (uint)PtrWidth;
		Desc.Height = (uint)PtrHeight;

		// Set up init data
		D3D11_SUBRESOURCE_DATA InitData = new()
		{
			pSysMem = (PtrInfo.ShapeInfo.Type == DXGI_OUTDUPL_POINTER_SHAPE_TYPE.DXGI_OUTDUPL_POINTER_SHAPE_TYPE_COLOR) ? PtrInfo.PtrShapeBuffer : InitBuffer,
			SysMemPitch = (PtrInfo.ShapeInfo.Type == DXGI_OUTDUPL_POINTER_SHAPE_TYPE.DXGI_OUTDUPL_POINTER_SHAPE_TYPE_COLOR) ? PtrInfo.ShapeInfo.Pitch : (uint)PtrWidth * BPP,
			SysMemSlicePitch = 0
		};

		// Create mouseshape as texture
		HRESULT hr = m_Device!.CreateTexture2D(Desc, [InitData], out MouseTex);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create mouse pointer texture", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Create shader resource from texture
		hr = m_Device.CreateShaderResourceView(MouseTex!, SDesc, out ShaderRes);
		if (hr.Failed)
		{
			MouseTex = default;
			return ProcessFailure(m_Device, "Failed to create shader resource from mouse pointer texture", "Error", hr, SystemTransitionsExpectedErrors);
		}

		D3D11_BUFFER_DESC BDesc = new()
		{
			Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
			ByteWidth = (uint)Marshal.SizeOf(typeof(VERTEX)) * NUMVERTICES,
			BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
			CPUAccessFlags = 0
		};

		// Create vertex buffer
		unsafe
		{
			D3D11_SUBRESOURCE_DATA InitData2 = new() { pSysMem = Vertices };
			hr = m_Device.CreateBuffer(BDesc, InitData2, out VertexBufferMouse);
		}
		if (hr.Failed)
		{
			ShaderRes = default;
			MouseTex = default;
			return ProcessFailure(m_Device, "Failed to create mouse pointer vertex buffer in OutputManager", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Set resources
		float[] BlendFactor = new float[4];
		uint Stride = (uint)Marshal.SizeOf(typeof(VERTEX));
		uint Offset = 0;
		m_DeviceContext!.IASetVertexBuffers(0, 1, [VertexBufferMouse!], [Stride], [Offset]);
		m_DeviceContext.OMSetBlendState(m_BlendState, BlendFactor, 0xFFFFFFFF);
		m_DeviceContext.OMSetRenderTargets(1, [m_RTV!], default);
		m_DeviceContext.VSSetShader(m_VertexShader, default, 0);
		m_DeviceContext.PSSetShader(m_PixelShader, default, 0);
		m_DeviceContext.PSSetShaderResources(0, 1, [ShaderRes!]);
		m_DeviceContext.PSSetSamplers(0, 1, [m_SamplerLinear!]);

		// Draw
		m_DeviceContext.Draw(NUMVERTICES, 0);

		// Clean
		if (VertexBufferMouse is not null)
		{
			VertexBufferMouse = default;
		}
		if (ShaderRes is not null)
		{
			ShaderRes = default;
		}
		if (MouseTex is not null)
		{
			MouseTex = default;
		}
		if (InitBuffer is not 0)
		{
			Marshal.FreeHGlobal(InitBuffer);
			InitBuffer = default;
		}

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Initialize shaders for drawing to screen
	//
	DUPL_RETURN InitShaders()
	{
		SizeT Size = g_VS.GetBufferSize();
		var hr = m_Device!.CreateVertexShader(g_VS.GetBufferPointer(), Size, default, out m_VertexShader);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create vertex shader in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}

		D3D11_INPUT_ELEMENT_DESC[] Layout = [
			new() { SemanticName = "POSITION", Format = DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT, InputSlotClass = D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA },
			new() { SemanticName = "TEXCOORD", Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT, AlignedByteOffset = 12, InputSlotClass = D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA }
		];

		hr = m_Device.CreateInputLayout(Layout, Layout.Length, g_VS.GetBufferPointer(), Size, out m_InputLayout);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create input layout in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}
		m_DeviceContext!.IASetInputLayout(m_InputLayout);

		Size = g_PS.GetBufferSize();
		hr = m_Device.CreatePixelShader(g_PS.GetBufferPointer(), Size, default, out m_PixelShader);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create pixel shader in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Reset render target view
	//
	DUPL_RETURN MakeRTV()
	{
		// Get backbuffer
		ID3D11Texture2D? BackBuffer = m_SwapChain!.GetBuffer<ID3D11Texture2D>(0);

		// Create a render target view
		var hr = m_Device!.CreateRenderTargetView(BackBuffer, default, out m_RTV);
		BackBuffer = null;
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create render target view in OUTPUTMANAGER", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Set new render target
		m_DeviceContext!.OMSetRenderTargets(1, [m_RTV!], default);

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Set new viewport
	//
	void SetViewPort(uint Width, uint Height)
	{
		D3D11_VIEWPORT VP = new()
		{
			Width = (float)(Width),
			Height = (float)(Height),
			MinDepth = 0.0f,
			MaxDepth = 1.0f,
			TopLeftX = 0,
			TopLeftY = 0
		};
		m_DeviceContext!.RSSetViewports(1, [VP]);
	}

	//
	// Resize swapchain
	//
	DUPL_RETURN ResizeSwapChain()
	{
		m_RTV = default;

		GetClientRect(m_WindowHandle, out var WindowRect);
		uint Width = (uint)(WindowRect.right - WindowRect.left);
		uint Height = (uint)(WindowRect.bottom - WindowRect.top);

		// Resize swapchain
		DXGI_SWAP_CHAIN_DESC SwapChainDesc = m_SwapChain!.GetDesc();
		m_SwapChain.ResizeBuffers(SwapChainDesc.BufferCount, Width, Height, SwapChainDesc.BufferDesc.Format, SwapChainDesc.Flags);

		// Make new render target view
		DUPL_RETURN Ret = MakeRTV();
		if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
		{
			return Ret;
		}

		// Set new viewport
		SetViewPort(Width, Height);

		return Ret;
	}

	//
	// Releases all references
	//
	public void CleanRefs()
	{
		m_VertexShader = default;
		m_PixelShader = default;
		m_InputLayout = default;
		m_RTV = default;
		m_SamplerLinear = default;
		m_BlendState = default;
		m_DeviceContext = default;
		m_Device = default;
		m_SwapChain = default;
		m_SharedSurf = default;
		m_KeyMutex = default;

		if (m_Factory is not null)
		{
			if (m_OcclusionCookie != 0)
			{
				m_Factory.UnregisterOcclusionStatus(m_OcclusionCookie);
				m_OcclusionCookie = 0;
			}
			m_Factory = default;
		}
	}
}