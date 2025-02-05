internal class D3D12HelloTexture(int width, int height, string name) : DXSample(width, height, name)
{
	private const uint FrameCount = 2;
	private const uint TextureHeight = 256;
	private const uint TexturePixelSize = 4;    // The number of bytes used to represent a pixel in the texture.
	private const uint TextureWidth = 256;

	private struct Vertex(D2D_VECTOR_3F position, D2D_VECTOR_2F uv)
	{
		public D2D_VECTOR_3F position = position;
		public D2D_VECTOR_2F uv = uv;
	}

	// Pipeline objects.
	D3D12_VIEWPORT m_viewport = new(0, 0, width, height);
	RECT m_scissorRect = new(0, 0, width, height);
	IDXGISwapChain3? m_swapChain;
	ID3D12Device? m_device;
	readonly ID3D12Resource?[] m_renderTargets = new ID3D12Resource?[FrameCount];
	ID3D12CommandAllocator? m_commandAllocator;
	ID3D12CommandQueue? m_commandQueue;
	ID3D12RootSignature? m_rootSignature;
	ID3D12DescriptorHeap? m_rtvHeap;
	ID3D12DescriptorHeap? m_srvHeap;
	ID3D12PipelineState? m_pipelineState;
	ID3D12GraphicsCommandList? m_commandList;
	uint m_rtvDescriptorSize;

	// App resources.
	ID3D12Resource? m_vertexBuffer;
	D3D12_VERTEX_BUFFER_VIEW m_vertexBufferView;
	ID3D12Resource? m_texture;

	// Synchronization objects.
	uint m_frameIndex;
	SafeEventHandle m_fenceEvent = SafeEventHandle.Null;
	ID3D12Fence? m_fence;
	ulong m_fenceValue;

	public override void OnInit()
	{
		LoadPipeline();
		LoadAssets();
	}

	public override void OnUpdate() { }

	public override void OnRender()
	{
		// Record all the commands we need to render the scene into the command list.
		PopulateCommandList();

		// Execute the command list.
		ID3D12CommandList[] ppCommandLists = [ m_commandList! ];
		m_commandQueue!.ExecuteCommandLists(ppCommandLists.Length, ppCommandLists);

		// Present the frame.
		m_swapChain!.Present(1, 0);

		WaitForPreviousFrame();
	}

	public override void OnDestroy()
	{
		// Ensure that the GPU is no longer referencing resources that are about to be
		// cleaned up by the destructor.
		WaitForPreviousFrame();

		m_fenceEvent.Dispose();
	}

	// Load the rendering pipeline dependencies.
	void LoadPipeline()
	{
		DXGI_CREATE_FACTORY dxgiFactoryFlags = 0;

#if DEBUG
		// Enable the debug layer (requires the Graphics Tools "optional feature").
		// NOTE: Enabling the debug layer after device creation will invalidate the active device.
		{
			ID3D12Debug? debugController = D3D12GetDebugInterface<ID3D12Debug>();
			if (debugController is not null)
			{
				debugController.EnableDebugLayer();

				// Enable additional debug layers.
				dxgiFactoryFlags |= DXGI_CREATE_FACTORY.DXGI_CREATE_FACTORY_DEBUG;
			}
		}
#endif

		IDXGIFactory4 factory = CreateDXGIFactory2<IDXGIFactory4>(dxgiFactoryFlags);

		if (m_useWarpDevice)
		{
			factory.EnumWarpAdapter(typeof(IDXGIAdapter).GUID, out var warpAdapter).ThrowIfFailed();

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, (IDXGIAdapter)warpAdapter, out m_device).ThrowIfFailed();
		}
		else
		{
			GetHardwareAdapter(factory, out var hardwareAdapter);

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, hardwareAdapter, out m_device).ThrowIfFailed();
		}

		// Describe and create the command queue.
		D3D12_COMMAND_QUEUE_DESC queueDesc = new()
		{
			Flags = D3D12_COMMAND_QUEUE_FLAGS.D3D12_COMMAND_QUEUE_FLAG_NONE,
			Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT
		};
		m_commandQueue = m_device!.CreateCommandQueue<ID3D12CommandQueue>(queueDesc);

		// Describe and create the swap chain.
		DXGI_SWAP_CHAIN_DESC1 swapChainDesc = new()
		{
			BufferCount = FrameCount,
			Width = (uint)Width,
			Height = (uint)Height,
			Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
			BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
			SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_DISCARD,
			SampleDesc = new() { Count = 1 },
		};

		IDXGISwapChain1 swapChain = factory.CreateSwapChainForHwnd(m_commandQueue, Win32App!.Handle, swapChainDesc);

		// This sample does not support fullscreen transitions.
		factory.MakeWindowAssociation(Win32App!.Handle, DXGI_MWA.DXGI_MWA_NO_ALT_ENTER);

		m_swapChain = (IDXGISwapChain3)swapChain;
		m_frameIndex = m_swapChain.GetCurrentBackBufferIndex();

		// Create descriptor heaps.
		{
			// Describe and create a render target view (RTV) descriptor heap.
			D3D12_DESCRIPTOR_HEAP_DESC rtvHeapDesc = new()
			{
				NumDescriptors = FrameCount,
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_NONE
			};
			m_rtvHeap = m_device!.CreateDescriptorHeap<ID3D12DescriptorHeap>(rtvHeapDesc);

			// Describe and create a shader resource view (SRV) heap for the texture.
			D3D12_DESCRIPTOR_HEAP_DESC srvHeapDesc = new()
			{
				NumDescriptors = FrameCount,
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE
			};
			m_srvHeap = m_device!.CreateDescriptorHeap<ID3D12DescriptorHeap>(srvHeapDesc);

			m_rtvDescriptorSize = m_device!.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
		}

		// Create frame resources.
		{
			D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap.GetCPUDescriptorHandleForHeapStart());

			// Create a RTV for each frame.
			for (uint n = 0; n < FrameCount; n++)
			{
				m_renderTargets[n] = m_swapChain.GetBuffer<ID3D12Resource>(n);
				m_device.CreateRenderTargetView(m_renderTargets[n], default, rtvHandle);
				rtvHandle.Offset(1, m_rtvDescriptorSize);
			}
		}

		m_device!.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, out m_commandAllocator).ThrowIfFailed();
	}

	void LoadAssets()
	{
		// Create the root signature.
		{
			// This is the highest version the sample supports. If CheckFeatureSupport succeeds, the HighestVersion returned will not be greater than this.
			D3D12_FEATURE_DATA_ROOT_SIGNATURE featureData = new() { HighestVersion = D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1_1 };
			if (m_device!.CheckFeatureSupport(ref featureData, D3D12_FEATURE.D3D12_FEATURE_ROOT_SIGNATURE).Failed)
				featureData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1_0;

			D3D12_DESCRIPTOR_RANGE1[] ranges = [new(D3D12_DESCRIPTOR_RANGE_TYPE.D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 0, 0, D3D12_DESCRIPTOR_RANGE_FLAGS.D3D12_DESCRIPTOR_RANGE_FLAG_DATA_STATIC)];
			D3D12_ROOT_PARAMETER1[] rootParameters = [D3D12_ROOT_PARAMETER1.InitAsDescriptorTable(ranges, D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_PIXEL, out var dtmem)];

			D3D12_STATIC_SAMPLER_DESC sampler = new()
			{
				Filter = D3D12_FILTER.D3D12_FILTER_MIN_MAG_MIP_POINT,
				AddressU = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_BORDER,
				AddressV = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_BORDER,
				AddressW = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_BORDER,
				ComparisonFunc = D3D12_COMPARISON_FUNC.D3D12_COMPARISON_FUNC_NEVER,
				BorderColor = D3D12_STATIC_BORDER_COLOR.D3D12_STATIC_BORDER_COLOR_TRANSPARENT_BLACK,
				MaxLOD = D3D12_FLOAT32_MAX,
				ShaderVisibility = D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_PIXEL
			};

			D3D12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc = new(new D3D12_ROOT_SIGNATURE_DESC1(rootParameters, [sampler], D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT, out var sdmem));

			HRESULT.ThrowIfFailed(D3DX12SerializeVersionedRootSignature(rootSignatureDesc, featureData.HighestVersion, out var signature, out _));
			m_rootSignature = m_device!.CreateRootSignature<ID3D12RootSignature>(0, signature!);
		}

		// Create the pipeline state, which includes compiling and loading shaders.
		{
#if DEBUG
			// Enable better shader debugging with the graphics debugging tools.
			D3DCOMPILE compileFlags = D3DCOMPILE.D3DCOMPILE_DEBUG | D3DCOMPILE.D3DCOMPILE_SKIP_OPTIMIZATION;
#else
			D3DCOMPILE compileFlags = 0;
#endif

			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("shaders.hlsl"), default, default, "VSMain", "vs_5_0", compileFlags, 0, out var vertexShader));
			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("shaders.hlsl"), default, default, "PSMain", "ps_5_0", compileFlags, 0, out var pixelShader));

			// Define the vertex input layout.
			SafeNativeArray<D3D12_INPUT_ELEMENT_DESC> inputElementDescs = [
				new("POSITION", DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT, 0, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA),
				new("COLOR", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 12, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA)
			];

			// Describe and create the graphics pipeline state object (PSO).
			D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = new()
			{
				InputLayout = new(inputElementDescs.Count, inputElementDescs),
				pRootSignature = m_rootSignature,
				VS = new(vertexShader),
				PS = new(pixelShader),
				RasterizerState = new(),
				BlendState = new(),
				DepthStencilState = new() { DepthEnable = false, StencilEnable = false },
				SampleMask = uint.MaxValue,
				PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE,
				NumRenderTargets = 1,
				RTVFormats = [DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, 0, 0, 0, 0, 0, 0, 0],
				SampleDesc = new() { Count = 1 }
			};
			m_pipelineState = m_device!.CreateGraphicsPipelineState<ID3D12PipelineState>(psoDesc);
		}

		// Create the command list.
		HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_commandAllocator!, m_pipelineState, out m_commandList));

		// Create the vertex buffer.
		{
			// Define the geometry for a triangle.
			SafeNativeArray<Vertex> triangleVertices = [
				new(new( 0.0f, 0.25f * m_aspectRatio, 0.0f), new(0.5f, 0.0f)),
				new(new( 0.25f, -0.25f * m_aspectRatio, 0.0f), new(1.0f, 1.0f)),
				new(new( -0.25f, -0.25f * m_aspectRatio, 0.0f), new(0.0f, 1.0f))
			];

			uint vertexBufferSize = triangleVertices.Size;

			// Note: using upload heaps to transfer static data like vert buffers is not 
			// recommended. Every time the GPU needs it, the upload heap will be marshalled 
			// over. Please read up on Default Heap usage. An upload heap is used here for 
			// code simplicity and because there are very few verts to actually transfer.
			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(
				new D3D12_HEAP_PROPERTIES(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
				default,
				out m_vertexBuffer));

			// Copy the triangle data to the vertex buffer.
			D3D12_RANGE readRange = new(0, 0); // We do not intend to read from this resource on the CPU.
			HRESULT.ThrowIfFailed(m_vertexBuffer!.Map(0, readRange, out var pVertexDataBegin));
			triangleVertices.CopyTo(pVertexDataBegin, triangleVertices.Size);
			m_vertexBuffer!.Unmap(0, default);

			// Initialize the vertex buffer view.
			m_vertexBufferView = new()
			{
				BufferLocation = m_vertexBuffer.GetGPUVirtualAddress(),
				StrideInBytes = (uint)Marshal.SizeOf(typeof(Vertex)),
				SizeInBytes = vertexBufferSize
			};
		}

		// Note: ComPtr's are CPU objects but this resource needs to stay in scope until
		// the command list that references it has finished executing on the GPU.
		// We will flush the GPU at the end of this method to ensure the resource is not
		// prematurely destroyed.
		ID3D12Resource textureUploadHeap;

		// Create the texture.
		{
			// Describe and create a Texture2D.
			D3D12_RESOURCE_DESC textureDesc = new(D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE2D,
				0, TextureWidth, TextureHeight, 1, 1, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, 1, 0, 0,
				D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE);

			m_texture = m_device!.CreateCommittedResource<ID3D12Resource>(
				new D3D12_HEAP_PROPERTIES(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE, textureDesc,
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST);

			ulong uploadBufferSize = GetRequiredIntermediateSize(m_texture, 0, 1);

			// Create the GPU upload buffer.
			textureUploadHeap = m_device!.CreateCommittedResource<ID3D12Resource>(
				new D3D12_HEAP_PROPERTIES(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(uploadBufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ);

			// Copy data to the intermediate upload heap and then schedule a copy 
			// from the upload heap to the Texture2D.
			byte[] texture = GenerateTextureData();

			D3D12_SUBRESOURCE_DATA textureData = new()
			{
				pData = texture[0],
				RowPitch = TextureWidth * TexturePixelSize,
				SlicePitch = TextureWidth * TexturePixelSize * TextureHeight
			};

			UpdateSubresources(m_commandList!, m_texture, textureUploadHeap, 0, 0, 1, [textureData]);
			m_commandList!.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_texture,
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE)]);

			// Describe and create a SRV for the texture.
			SafeCoTaskMemStruct<D3D12_SHADER_RESOURCE_VIEW_DESC> srvDesc = new D3D12_SHADER_RESOURCE_VIEW_DESC()
			{
				Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
				Format = textureDesc.Format,
				ViewDimension = D3D12_SRV_DIMENSION.D3D12_SRV_DIMENSION_TEXTURE2D,
				Texture2D = new() { MipLevels = 1 }
			};
			m_device!.CreateShaderResourceView(m_texture, srvDesc, m_srvHeap!.GetCPUDescriptorHandleForHeapStart());
		}

		// Close the command list and execute it to begin the initial GPU setup.
		HRESULT.ThrowIfFailed(m_commandList.Close());
		ID3D12CommandList[] ppCommandLists = [ m_commandList ];
		m_commandQueue!.ExecuteCommandLists(ppCommandLists.Length, ppCommandLists);

		// Create synchronization objects and wait until assets have been uploaded to the GPU.
		{
			HRESULT.ThrowIfFailed(m_device.CreateFence(0, D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE, out m_fence));
			m_fenceValue = 1;

			// Create an event handle to use for frame synchronization.
			Win32Error.ThrowLastErrorIfInvalid(m_fenceEvent = CreateEvent(default, false, false, default));

			// Wait for the command list to execute; we are reusing the same command 
			// list in our main loop but for now, we just want to wait for setup to 
			// complete before continuing.
			WaitForPreviousFrame();
		}
	}

	void PopulateCommandList()
	{
		// Command list allocators can only be reset when the associated 
		// command lists have finished execution on the GPU; apps should use 
		// fences to determine GPU execution progress.
		HRESULT.ThrowIfFailed(m_commandAllocator!.Reset());

		// However, when ExecuteCommandList() is called on a particular command 
		// list, that command list can then be reset at any time and must be before 
		// re-recording.
		HRESULT.ThrowIfFailed(m_commandList!.Reset(m_commandAllocator, m_pipelineState));

		// Set necessary state.
		m_commandList.SetGraphicsRootSignature(m_rootSignature);

		ID3D12DescriptorHeap[] ppHeaps = [m_srvHeap!];
		m_commandList.SetDescriptorHeaps(ppHeaps.Length, ppHeaps);

		m_commandList.SetGraphicsRootDescriptorTable(0, m_srvHeap!.GetGPUDescriptorHandleForHeapStart());
		m_commandList.RSSetViewports(1, [m_viewport]);
		m_commandList.RSSetScissorRects(1, [m_scissorRect]);

		// Indicate that the back buffer will be used as a render target.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex]!,
			D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET)]);

		D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(), (int)m_frameIndex, m_rtvDescriptorSize);
		m_commandList.OMSetRenderTargets(1, [rtvHandle], false, default);

		// Record commands.
		float[] clearColor = [0.0f, 0.2f, 0.4f, 1.0f];
		m_commandList.ClearRenderTargetView(rtvHandle, clearColor, 0, default);
		m_commandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		m_commandList.IASetVertexBuffers(0, 1, [m_vertexBufferView]);
		m_commandList.DrawInstanced(3, 1, 0, 0);

		// Indicate that the back buffer will now be used to present.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex]!,
			D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT)]);

		HRESULT.ThrowIfFailed(m_commandList.Close());
	}

	void WaitForPreviousFrame()
	{
		if (m_fence is null) return;

		// WAITING FOR THE FRAME TO COMPLETE BEFORE CONTINUING IS NOT BEST PRACTICE.
		// This is code implemented as such for simplicity. The D3D12HelloFrameBuffering
		// sample illustrates how to use fences for efficient resource usage and to
		// maximize GPU utilization.

		// Signal and increment the fence value.
		ulong fence = m_fenceValue;
		HRESULT.ThrowIfFailed(m_commandQueue!.Signal(m_fence!, fence));
		m_fenceValue++;

		// Wait until the previous frame is finished.
		if (m_fence!.GetCompletedValue() < fence)
		{
			HRESULT.ThrowIfFailed(m_fence.SetEventOnCompletion(fence, m_fenceEvent));
			WaitForSingleObject(m_fenceEvent, INFINITE);
		}

		m_frameIndex = m_swapChain!.GetCurrentBackBufferIndex();
	}

	static byte[] GenerateTextureData()
	{
		uint rowPitch = TextureWidth * TexturePixelSize;
		uint cellPitch = rowPitch >> 3;        // The width of a cell in the checkboard texture.
		uint cellHeight = TextureWidth >> 3;    // The height of a cell in the checkerboard texture.
		uint textureSize = rowPitch * TextureHeight;

		byte[] pData = new byte[textureSize];
		for (uint n = 0; n < textureSize; n += TexturePixelSize)
		{
			uint x = n % rowPitch;
			uint y = n / rowPitch;
			uint i = x / cellPitch;
			uint j = y / cellHeight;

			if (i % 2 == j % 2)
			{
				pData[n] = 0x00;        // R
				pData[n + 1] = 0x00;    // G
				pData[n + 2] = 0x00;    // B
				pData[n + 3] = 0xff;    // A
			}
			else
			{
				pData[n] = 0xff;        // R
				pData[n + 1] = 0xff;    // G
				pData[n + 2] = 0xff;    // B
				pData[n + 3] = 0xff;    // A
			}
		}

		return pData;
	}

	private const int D3D12_SHADER_COMPONENT_MAPPING_MASK = 0x7;
	private const int D3D12_SHADER_COMPONENT_MAPPING_SHIFT = 3;
	private const int D3D12_SHADER_COMPONENT_MAPPING_ALWAYS_SET_BIT_AVOIDING_ZEROMEM_MISTAKES = 1 << (D3D12_SHADER_COMPONENT_MAPPING_SHIFT * 4);

	public static uint D3D12_ENCODE_SHADER_4_COMPONENT_MAPPING(uint Src0, uint Src1, uint Src2, uint Src3) => ((Src0) & D3D12_SHADER_COMPONENT_MAPPING_MASK) |
		(((Src1) & D3D12_SHADER_COMPONENT_MAPPING_MASK) << D3D12_SHADER_COMPONENT_MAPPING_SHIFT) |
		(((Src2) & D3D12_SHADER_COMPONENT_MAPPING_MASK) << (D3D12_SHADER_COMPONENT_MAPPING_SHIFT * 2)) |
		(((Src3) & D3D12_SHADER_COMPONENT_MAPPING_MASK) << (D3D12_SHADER_COMPONENT_MAPPING_SHIFT * 3)) |
		D3D12_SHADER_COMPONENT_MAPPING_ALWAYS_SET_BIT_AVOIDING_ZEROMEM_MISTAKES;
	public static D3D12_SHADER_COMPONENT_MAPPING D3D12_DECODE_SHADER_4_COMPONENT_MAPPING(int ComponentToExtract, int Mapping) => (D3D12_SHADER_COMPONENT_MAPPING)(Mapping >> (D3D12_SHADER_COMPONENT_MAPPING_SHIFT * ComponentToExtract) & D3D12_SHADER_COMPONENT_MAPPING_MASK);
	public static uint D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING => D3D12_ENCODE_SHADER_4_COMPONENT_MAPPING(0, 1, 2, 3);

}