using static Program;

internal class THREADMANAGER : IDisposable
{
	PTR_INFO m_PtrInfo = new();
	int m_ThreadCount { get => m_ThreadHandles.Length; set { m_ThreadHandles = new SafeHTHREAD[value]; m_ThreadData = new GCHandle[value]; } }
	SafeHTHREAD[] m_ThreadHandles = [];
	GCHandle[] m_ThreadData = [];

	public void Dispose() => Clean();

	//
	// Clean up resources
	//
	public void Clean()
	{
		m_PtrInfo.Dispose();

		foreach (var h in m_ThreadHandles)
			h.Dispose();
		m_ThreadHandles = [];

		foreach (var h in m_ThreadData)
		{
			((THREAD_DATA)h.Target!).DxRes.Dispose();
			h.Free();
		}
		m_ThreadData = [];
	}

	//
	// Start up threads for DDA
	//
	public DUPL_RETURN Initialize(int SingleOutput, int OutputCount, SafeEventHandle UnexpectedErrorEvent, SafeEventHandle ExpectedErrorEvent,
		SafeEventHandle TerminateThreadsEvent, HANDLE SharedHandle, in RECT DesktopDim)
	{
		m_ThreadCount = OutputCount;

		// Create appropriate # of threads for duplication
		DUPL_RETURN Ret = DUPL_RETURN.DUPL_RETURN_SUCCESS;
		for (uint i = 0; i < m_ThreadCount; ++i)
		{
			THREAD_DATA Data = new()
			{
				UnexpectedErrorEvent = UnexpectedErrorEvent,
				ExpectedErrorEvent = ExpectedErrorEvent,
				TerminateThreadsEvent = TerminateThreadsEvent,
				Output = (SingleOutput < 0) ? i : (uint)SingleOutput,
				TexSharedHandle = SharedHandle,
				OffsetX = DesktopDim.left,
				OffsetY = DesktopDim.top,
				PtrInfo = m_PtrInfo
			};
			Ret = InitializeDx(out Data.DxRes);
			if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
			{
				return Ret;
			}

			m_ThreadData[i] = GCHandle.Alloc(Data, GCHandleType.Normal);
			m_ThreadHandles[i] = CreateThread(default, 0, DDProc, GCHandle.ToIntPtr(m_ThreadData[i]), 0, out _);
			if (m_ThreadHandles[i].IsInvalid)
			{
				return ProcessFailure(default, "Failed to create thread", "Error", HRESULT.E_FAIL);
			}
		}
		return Ret;
	}

	//
	// Get DX_RESOURCES
	//
	DUPL_RETURN InitializeDx(out DX_RESOURCES Data)
	{
		HRESULT hr = HRESULT.S_OK;
		Data = new();

		// Driver types supported
		D3D_DRIVER_TYPE[] DriverTypes =
		{
		D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
		D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP,
		D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_REFERENCE,
		};
		uint NumDriverTypes = (uint)DriverTypes.Length;

		// Feature levels supported
		D3D_FEATURE_LEVEL[] FeatureLevels =
		{
		D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
		D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
		D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
		D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1
		};
		uint NumFeatureLevels = (uint)FeatureLevels.Length;

		// Create device
		for (uint DriverTypeIndex = 0; DriverTypeIndex < NumDriverTypes; ++DriverTypeIndex)
		{
			hr = D3D11CreateDevice(default, DriverTypes[DriverTypeIndex], default, 0, FeatureLevels, NumFeatureLevels,
				D3D11_SDK_VERSION, out Data.Device, out _, out Data.Context);
			if (hr.Succeeded)
			{
				// Device creation success, no need to loop anymore
				break;
			}
		}
		if (hr.Failed)
		{
			return ProcessFailure(default, "Failed to create device in InitializeDx", "Error", hr);
		}

		// VERTEX shader
		SizeT Size = g_VS.GetBufferSize();
		hr = Data.Device!.CreateVertexShader(g_VS.GetBufferPointer(), Size, default, out Data.VertexShader);
		if (hr.Failed)
		{
			return ProcessFailure(Data.Device, "Failed to create vertex shader in InitializeDx", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Input layout
		D3D11_INPUT_ELEMENT_DESC[] Layout = [
			new() { SemanticName = "POSITION", Format = DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT, InputSlotClass = D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA },
			new() { SemanticName = "TEXCOORD", Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT, AlignedByteOffset = 12, InputSlotClass = D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA }
		];

		uint NumElements = (uint)Layout.Length;
		hr = Data.Device.CreateInputLayout(Layout, (int)NumElements, g_VS.GetBufferPointer(), Size, out Data.InputLayout);
		if (hr.Failed)
		{
			return ProcessFailure(Data.Device, "Failed to create input layout in InitializeDx", "Error", hr, SystemTransitionsExpectedErrors);
		}
		Data.Context!.IASetInputLayout(Data.InputLayout);

		// Pixel shader
		Size = g_PS.GetBufferSize();
		hr = Data.Device.CreatePixelShader(g_PS.GetBufferPointer(), Size, default, out Data.PixelShader);
		if (hr.Failed)
		{
			return ProcessFailure(Data.Device, "Failed to create pixel shader in InitializeDx", "Error", hr, SystemTransitionsExpectedErrors);
		}

		// Set up sampler
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
		hr = Data.Device.CreateSamplerState(SampDesc, out Data.SamplerLinear);
		if (hr.Failed)
		{
			return ProcessFailure(Data.Device, "Failed to create sampler state in InitializeDx", "Error", hr, SystemTransitionsExpectedErrors);
		}

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Getter for the PTR_INFO structure
	//
	public PTR_INFO GetPointerInfo() => m_PtrInfo;

	//
	// Waits infinitely for all spawned threads to terminate
	//
	public void WaitForThreadTermination()
	{
		if (m_ThreadCount != 0)
		{
			WaitForMultipleObjectsEx(m_ThreadHandles, true, INFINITE, false);
		}
	}
}