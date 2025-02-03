internal partial class D3D12DepthBoundsTest(int width, int height, string name) : DXSample(width, height, name)
{
	private const int FrameCount = 2;

	// Pipeline objects.
	D3D12_VIEWPORT m_viewport = new(0, 0, width, height);
	readonly ID3D12Resource[] m_renderTargets = new ID3D12Resource[FrameCount];
	bool DepthBoundsTestSupported;
	ID3D12CommandAllocator? m_commandAllocator;
	ID3D12GraphicsCommandList1? m_commandList;
	ID3D12CommandQueue? m_commandQueue;
	ID3D12PipelineState? m_depthOnlyPipelineState;
	ID3D12Resource? m_depthStencil;
	ID3D12Device2? m_device;
	ID3D12DescriptorHeap? m_dsvHeap;
	ID3D12PipelineState? m_pipelineState;
	ID3D12RootSignature? m_rootSignature;
	uint m_rtvDescriptorSize = 0;
	ID3D12DescriptorHeap? m_rtvHeap;
	RECT m_scissorRect = new(0, 0, width, height);
	IDXGISwapChain3? m_swapChain;

	// App resources.
	ID3D12Resource? m_vertexBuffer;
	D3D12_VERTEX_BUFFER_VIEW m_vertexBufferView;

	// Synchronization objects.
	int m_frameIndex = 0;
	uint m_frameNumber = 0;
	SafeEventHandle m_fenceEvent = SafeEventHandle.Null;
	ID3D12Fence? m_fence;
	ulong m_fenceValue;

	public override void OnDestroy()
	{
		// Ensure that the GPU is no longer referencing resources that are about to be
		// cleaned up by the destructor.
		WaitForPreviousFrame();

		m_fenceEvent.Dispose();
	}

	public override void OnInit()
	{
		LoadPipeline();
		LoadAssets();
	}

	public override void OnRender()
	{
		// Record all the commands we need to render the scene into the command list.
		PopulateCommandList();

		// Execute the command list.
		ID3D12CommandList[] ppCommandLists = [m_commandList!];
		m_commandQueue!.ExecuteCommandLists(ppCommandLists.Length, ppCommandLists);

		// Present the frame.
		m_swapChain!.Present(1, 0);

		WaitForPreviousFrame();
	}

	public override void OnUpdate() => m_frameNumber++;

	// Load the sample assets.
	private void LoadAssets()
	{
		// Create an empty root signature.
		{
			D3D12_ROOT_SIGNATURE_DESC rootSignatureDesc = new(0, default, 0, default, D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT);

#pragma warning disable CS0618 // Type or member is obsolete
			HRESULT.ThrowIfFailed(D3D12SerializeRootSignature(rootSignatureDesc, D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1, out var signature));
#pragma warning restore CS0618 // Type or member is obsolete
			m_rootSignature = m_device!.CreateRootSignature<ID3D12RootSignature>(0, signature);
		}

		/// Create the pipeline state, which includes compiling and loading shaders.
		{
#if DEBUG
			// Enable better shader debugging with the graphics debugging tools.
			D3DCOMPILE compileFlags = D3DCOMPILE.D3DCOMPILE_DEBUG | D3DCOMPILE.D3DCOMPILE_SKIP_OPTIMIZATION;
#else
			D3DCOMPILE compileFlags = 0;
#endif

			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("shaders.hlsl"), IntPtr.Zero, null, "VSMain", "vs_5_0", compileFlags, 0, out var vertexShader));
			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("shaders.hlsl"), IntPtr.Zero, null, "PSMain", "ps_5_0", compileFlags, 0, out var pixelShader));

			// Define the vertex input layout.
			SafeNativeArray<D3D12_INPUT_ELEMENT_DESC> inputElementDescs = [
				new("POSITION", DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT, 0, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA),
				new("COLOR", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 12, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA)
			];

			// To bring up depth bounds feature, DX12 introduces a new concept to create pipeline state, called PSO stream. 
			// It is required to use PSO stream to enable depth bounds test.
			//
			// PSO stream is a more flexible way to extend the design of pipeline state. In this new concept, you can think 
			// each subobject (e.g. Root Signature, Vertex Shader, or Pixel Shader) in the pipeline state is a token and the 
			// whole pipeline state is a token stream. To create a PSO stream, you describe a set of subobjects required for rendering, and 
			// then use the descriptor to create the a PSO. For any pipeline state subobject not found in the descriptor, 
			// defaults will be used. Defaults will also be used if an old version of a subobject is found in the stream. For example, 
			// an old DepthStencil State desc would not contain depth bounds test information so the depth bounds test value will  
			// default to disabled.

			// Wraps an array of render target format(s).
			D3D12_RT_FORMAT_ARRAY RTFormatArray = new([DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM]);

			// Check for the feature support of Depth Bounds Test
			D3D12_FEATURE_DATA_D3D12_OPTIONS2 Options = new();
			DepthBoundsTestSupported = m_device!.CheckFeatureSupport(ref Options, D3D12_FEATURE.D3D12_FEATURE_D3D12_OPTIONS2).Succeeded && Options.DepthBoundsTestSupported;

			// Create a PSO with depth bounds test enabled (or disabled, based on the result of the feature query).
			D3D12_DEPTH_STENCIL_DESC1 depthDesc = new()
			{
				DepthBoundsTestEnable = DepthBoundsTestSupported,
				DepthEnable = false
			};

			// Define the pipeline state for rendering a triangle with depth bounds test enabled.
			using SafeCoTaskMemStruct<RENDER_WITH_DBT_PSO_STREAM> renderWithDBTPSOStream = new RENDER_WITH_DBT_PSO_STREAM()
			{
				RootSignature = new(m_rootSignature!),
				InputLayout = new(new(inputElementDescs.Count, inputElementDescs)),
				PrimitiveTopologyType = new(D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE),
				VS = new(new(vertexShader)),
				PS = new(new(pixelShader)),
				DepthStencilState = new(depthDesc),
				DSVFormat = new(DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT),
				RTVFormats = new(RTFormatArray),
			};

			// Create the descriptor of the PSO stream
			D3D12_PIPELINE_STATE_STREAM_DESC renderWithDBTPSOStreamDesc = renderWithDBTPSOStream;
			m_device!.CreatePipelineState(renderWithDBTPSOStreamDesc, out m_pipelineState).ThrowIfFailed();

			// Create a PSO to prime depth only.
			using SafeCoTaskMemStruct<DEPTH_ONLY_PSO_STREAM> depthOnlyPSOStream = new DEPTH_ONLY_PSO_STREAM()
			{
				RootSignature = new(m_rootSignature!),
				InputLayout = new(new(inputElementDescs.Count, inputElementDescs)),
				PrimitiveTopologyType = new(D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE),
				VS = new(new(vertexShader)),
				DSVFormat = new(DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT),
				RTVFormats = new(RTFormatArray)
			};

			D3D12_PIPELINE_STATE_STREAM_DESC depthOnlyPSOStreamDesc = depthOnlyPSOStream;
			m_device!.CreatePipelineState(depthOnlyPSOStreamDesc, out m_depthOnlyPipelineState).ThrowIfFailed();
		}

		// Create the command list.
		HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_commandAllocator!, m_pipelineState, out m_commandList));

		// Command lists are created in the recording state, but there is nothing to record yet. The main loop expects it to be closed, so
		// close it now.
		HRESULT.ThrowIfFailed(m_commandList!.Close());

		// Create the vertex buffer.
		{
			// Define the geometry for a triangle.
			SafeNativeArray<Vertex> triangleVertices = [
				new(0.00f,  0.25f * m_aspectRatio, 0.1f,    // Top
					1.0f,   0.0f,  0.0f,  1.0f),        // Red
				new(0.25f, -0.25f * m_aspectRatio, 0.9f,    // Right
					0.0f,   1.0f,  0.0f,  1.0f),        // Green
				new(-0.25f, -0.25f * m_aspectRatio, 0.5f,    // Left
					0.0f,   0.0f,  1.0f,  1.0f),        // Blue
			];

			uint vertexBufferSize = triangleVertices.Size;

			// Note: using upload heaps to transfer static data like vert buffers is not 
			// recommended. Every time the GPU needs it, the upload heap will be marshalled 
			// over. Please read up on Default Heap usage. An upload heap is used here for 
			// code simplicity and because there are very few verts to actually transfer.
			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new D3D12_HEAP_PROPERTIES(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
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

			// Initialize the vertex buffer views.
			m_vertexBufferView.BufferLocation = m_vertexBuffer!.GetGPUVirtualAddress();
			m_vertexBufferView.StrideInBytes = (uint)Marshal.SizeOf(typeof(Vertex));
			m_vertexBufferView.SizeInBytes = vertexBufferSize;
		}

		// Create the depth stencil view.
		{
			SafeCoTaskMemStruct<D3D12_DEPTH_STENCIL_VIEW_DESC> depthStencilDesc = new D3D12_DEPTH_STENCIL_VIEW_DESC()
			{
				Format = DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT,
				ViewDimension = D3D12_DSV_DIMENSION.D3D12_DSV_DIMENSION_TEXTURE2D,
				Flags = D3D12_DSV_FLAGS.D3D12_DSV_FLAG_NONE
			};

			D3D12_CLEAR_VALUE depthOptimizedClearValue = new()
			{
				Format = DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT,
				DepthStencil = new() { Depth = 1.0f, Stencil = 0 }
			};

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Tex2D(DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT, (ulong)Width, (uint)Height, 1, 0, 1, 0, D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_DEPTH_WRITE,
				depthOptimizedClearValue,
				out m_depthStencil));

			NAME_D3D12_OBJECT(m_depthStencil!);

			m_device!.CreateDepthStencilView(m_depthStencil, depthStencilDesc, m_dsvHeap!.GetCPUDescriptorHandleForHeapStart());
		}

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

	// Load the rendering pipeline dependencies.
	private void LoadPipeline()
	{
		DXGI_CREATE_FACTORY dxgiFactoryFlags = 0;

#if DEBUG
		// Enable the debug layer
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

		// Describe the swap chain.
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

		// Swap chain needs the queue so that it can force a flush on it.
		IDXGISwapChain1 swapChain = factory.CreateSwapChainForHwnd(m_commandQueue, Win32App!.Handle, swapChainDesc);

		// This sample does not support fullscreen transitions.
		factory.MakeWindowAssociation(Win32App!.Handle, DXGI_MWA.DXGI_MWA_NO_ALT_ENTER);

		m_swapChain = (IDXGISwapChain3)swapChain;
		m_frameIndex = (int)m_swapChain.GetCurrentBackBufferIndex();

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

			// Describe and create a depth stencil view (DSV) descriptor heap.
			D3D12_DESCRIPTOR_HEAP_DESC dsvHeapDesc = new()
			{
				NumDescriptors = 1,
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_DSV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_NONE
			};
			m_dsvHeap = m_device!.CreateDescriptorHeap<ID3D12DescriptorHeap>(dsvHeapDesc);

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

	// Fill the command list with all the render commands and dependent state.
	void PopulateCommandList()
	{
		// Command list allocators can only be reset when the associated 
		// command lists have finished execution on the GPU; apps should use 
		// fences to determine GPU execution progress.
		HRESULT.ThrowIfFailed(m_commandAllocator!.Reset());

		// However, when ExecuteCommandList() is called on a particular command 
		// list, that command list can then be reset at any time and must be before 
		// re-recording.
		HRESULT.ThrowIfFailed(m_commandList!.Reset(m_commandAllocator, m_depthOnlyPipelineState));

		// Set necessary state.
		m_commandList.SetGraphicsRootSignature(m_rootSignature);
		m_commandList.RSSetViewports(1, [m_viewport]);
		m_commandList.RSSetScissorRects(1, [m_scissorRect]);

		// Indicate that the back buffer will be used as a render target.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET)]);

		D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(), m_frameIndex, m_rtvDescriptorSize);
		D3D12_CPU_DESCRIPTOR_HANDLE dsvHandle = new(m_dsvHeap!.GetCPUDescriptorHandleForHeapStart());
		m_commandList.OMSetRenderTargets([rtvHandle], false, dsvHandle);

		// Record commands.
		float[] clearColor = [0.392f, 0.584f, 0.929f, 1.0f];
		m_commandList.ClearRenderTargetView(rtvHandle, clearColor);
		m_commandList.ClearDepthStencilView(m_dsvHeap!.GetCPUDescriptorHandleForHeapStart(), D3D12_CLEAR_FLAGS.D3D12_CLEAR_FLAG_DEPTH, 1.0f, 0);

		m_commandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		m_commandList.IASetVertexBuffers(0, 1, [m_vertexBufferView]);

		// Render only the depth stencil view of the triangle to prime the depth value of the triangle
		m_commandList.DrawInstanced(3, 1, 0, 0);

		// Move depth bounds so we can see they move. Depth bound test will test against DEST depth
		// that we primed previously
		float f = 0.125f + (float)Math.Sin((m_frameNumber & 0x7F) / 127.0f) * 0.125f;      // [0.. 0.25]
		if (DepthBoundsTestSupported)
		{
			m_commandList.OMSetDepthBounds(0.0f + f, 1.0f - f);
		}

		// Render the triangle with depth bounds
		m_commandList.SetPipelineState(m_pipelineState!);
		m_commandList.DrawInstanced(3, 1, 0, 0);

		// Disable depth bounds on Direct3D 12 by resetting back to the default range
		if (DepthBoundsTestSupported)
		{
			m_commandList.OMSetDepthBounds(0.0f, 1.0f);
		}

		// Indicate that the back buffer will now be used to present.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT)]);

		HRESULT.ThrowIfFailed(m_commandList.Close());
	}

	private void WaitForPreviousFrame()
	{
		if (m_fence is null)
			return;

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

		m_frameIndex = (int)m_swapChain!.GetCurrentBackBufferIndex();
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	struct RENDER_WITH_DBT_PSO_STREAM
	{
		public CD3DX12_PIPELINE_STATE_STREAM_ROOT_SIGNATURE RootSignature;
		public CD3DX12_PIPELINE_STATE_STREAM_INPUT_LAYOUT InputLayout;
		public CD3DX12_PIPELINE_STATE_STREAM_PRIMITIVE_TOPOLOGY PrimitiveTopologyType;
		public CD3DX12_PIPELINE_STATE_STREAM_VS VS;
		public CD3DX12_PIPELINE_STATE_STREAM_PS PS;
		public CD3DX12_PIPELINE_STATE_STREAM_DEPTH_STENCIL1 DepthStencilState; // New depth stencil subobject with depth bounds test toggle
		private readonly int spacing;
		public CD3DX12_PIPELINE_STATE_STREAM_DEPTH_STENCIL_FORMAT DSVFormat;
		public CD3DX12_PIPELINE_STATE_STREAM_RENDER_TARGET_FORMATS RTVFormats;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct DEPTH_ONLY_PSO_STREAM
	{
		public CD3DX12_PIPELINE_STATE_STREAM_ROOT_SIGNATURE RootSignature;
		public CD3DX12_PIPELINE_STATE_STREAM_INPUT_LAYOUT InputLayout;
		public CD3DX12_PIPELINE_STATE_STREAM_PRIMITIVE_TOPOLOGY PrimitiveTopologyType;
		public CD3DX12_PIPELINE_STATE_STREAM_VS VS;
		public CD3DX12_PIPELINE_STATE_STREAM_DEPTH_STENCIL_FORMAT DSVFormat;
		public CD3DX12_PIPELINE_STATE_STREAM_RENDER_TARGET_FORMATS RTVFormats;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Vertex(float x, float y, float z, float r, float g, float b, float a)
	{
		public D2D_VECTOR_3F position = new(x, y, z);
		public D3DCOLORVALUE color = new(r, b, g, a);
	}
}