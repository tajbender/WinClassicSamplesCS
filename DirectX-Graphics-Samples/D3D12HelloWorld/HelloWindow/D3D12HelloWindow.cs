internal class D3D12HelloWindow(int width, int height, string name) : DXSample(width, height, name)
{
	private const uint FrameCount = 2;

	// Pipeline objects.
	IDXGISwapChain3? m_swapChain;
	ID3D12Device? m_device;
	readonly ID3D12Resource[] m_renderTargets = new ID3D12Resource[FrameCount];
	ID3D12CommandAllocator? m_commandAllocator;
	ID3D12CommandQueue? m_commandQueue;
	ID3D12DescriptorHeap? m_rtvHeap;
	ID3D12PipelineState? m_pipelineState = null;
	ID3D12GraphicsCommandList? m_commandList;
	uint m_rtvDescriptorSize = 0;

	// Synchronization objects.
	uint m_frameIndex = 0;
	SafeEventHandle m_fenceEvent = SafeEventHandle.Null;
	ID3D12Fence? m_fence;
	ulong m_fenceValue;

	public override void OnDestroy() =>
		// Ensure that the GPU is no longer referencing resources that are about to be cleaned up by the destructor.
		WaitForPreviousFrame();

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
		m_commandQueue!.ExecuteCommandLists(1, [m_commandList!]);

		// Present the frame.
		m_swapChain!.Present(1, 0);

		WaitForPreviousFrame();
	}

	public override void OnUpdate() => base.OnUpdate();

	private void LoadAssets()
	{
		// Create the command list.
		HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_commandAllocator!, null, out m_commandList));

		// Command lists are created in the recording state, but there is nothing
		// to record yet. The main loop expects it to be closed, so close it now.
		HRESULT.ThrowIfFailed(m_commandList!.Close());

		// Create synchronization objects and wait until assets have been uploaded to the GPU.
		{
			HRESULT.ThrowIfFailed(m_device!.CreateFence(0, D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE, out m_fence));
			m_fenceValue = 1;

			// Create an event handle to use for frame synchronization.
			Win32Error.ThrowLastErrorIfInvalid(m_fenceEvent = CreateEvent(default, false, false, default));

			// Wait for the command list to execute; we are reusing the same command 
			// list in our main loop but for now, we just want to wait for setup to 
			// complete before continuing.
			WaitForPreviousFrame();
		}
	}

	private void LoadPipeline()
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
			factory.EnumWarpAdapter(out IDXGIAdapter? warpAdapter).ThrowIfFailed();

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, (IDXGIAdapter)warpAdapter!, out m_device).ThrowIfFailed();
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

	private void PopulateCommandList()
	{
		// Command list allocators can only be reset when the associated 
		// command lists have finished execution on the GPU; apps should use 
		// fences to determine GPU execution progress.
		HRESULT.ThrowIfFailed(m_commandAllocator!.Reset());

		// However, when ExecuteCommandList() is called on a particular command 
		// list, that command list can then be reset at any time and must be before 
		// re-recording.
		HRESULT.ThrowIfFailed(m_commandList!.Reset(m_commandAllocator, m_pipelineState));

		// Indicate that the back buffer will be used as a render target.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex],
			D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET)]);

		D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(), (int)m_frameIndex, m_rtvDescriptorSize);
		m_commandList.OMSetRenderTargets(1, [rtvHandle], false, default);

		// Record commands.
		float[] clearColor = [0.0f, 0.2f, 0.4f, 1.0f];
		m_commandList.ClearRenderTargetView(rtvHandle, clearColor, 0, default);

		// Indicate that the back buffer will now be used to present.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex],
			D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT)]);

		HRESULT.ThrowIfFailed(m_commandList.Close());
	}

	private void WaitForPreviousFrame()
	{
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

	private struct Vertex(D2D_VECTOR_3F position, D3DCOLORVALUE color)
	{
		public D2D_VECTOR_3F position = position;
		public D3DCOLORVALUE color = color;
	}
}