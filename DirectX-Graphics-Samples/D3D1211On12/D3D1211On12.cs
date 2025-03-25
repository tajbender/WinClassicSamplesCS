using System.Drawing;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.D3D11;
using static Vanara.PInvoke.Dwrite;

internal partial class D3D1211on12(int width, int height, string name) : DXSample(width, height, name)
{
	private const uint FrameCount = 3;

	private readonly ID3D12CommandAllocator[] m_commandAllocators = new ID3D12CommandAllocator[FrameCount];
	private ID3D12GraphicsCommandList? m_commandList;
	private ID3D12CommandQueue? m_commandQueue;
	private ID2D1Device2? m_d2dDevice;
	private ID2D1DeviceContext2? m_d2dDeviceContext;
	private ID2D1Factory3? m_d2dFactory;
	private readonly ID2D1Bitmap1[] m_d2dRenderTargets = new ID2D1Bitmap1[FrameCount];
	private ID3D11DeviceContext? m_d3d11DeviceContext;
	private ID3D11On12Device? m_d3d11On12Device;
	private ID3D12Device? m_d3d12Device;
	private IDWriteFactory? m_dWriteFactory;
	private ID3D12Fence? m_fence;
	private SafeEventHandle? m_fenceEvent;
	private readonly ulong[] m_fenceValues = new ulong[FrameCount];
	private uint m_frameIndex = 0, m_rtvDescriptorSize = 0;
	private ID3D12PipelineState? m_pipelineState;
	private readonly ID3D12Resource[] m_renderTargets = new ID3D12Resource[FrameCount];
	private ID3D12RootSignature? m_rootSignature;
	private ID3D12DescriptorHeap? m_rtvHeap;
	private RECT m_scissorRect = new(0, 0, width, height);
	private IDXGISwapChain3? m_swapChain;
	private ID2D1SolidColorBrush? m_textBrush;
	private IDWriteTextFormat? m_textFormat;
	private ID3D12Resource? m_vertexBuffer;
	private D3D12_VERTEX_BUFFER_VIEW m_vertexBufferView;
	private D3D12_VIEWPORT m_viewport = new(0.0f, 0.0f, width, height);
	private readonly ID3D11Resource[] m_wrappedBackBuffers = new ID3D11Resource[FrameCount];

	public override void OnDestroy() =>
		// Ensure that the GPU is no longer referencing resources that are about to be cleaned up by the destructor.
		WaitForGpu();

	public override void OnInit()
	{
		LoadPipeline();
		LoadAssets();
	}

	public override void OnRender()
	{
		using (PIXEvent evt = new(m_commandQueue!, "Render 3D"))
		{
			// Record all the commands we need to render the scene into the command list.
			PopulateCommandList();

			// Execute the command list.
			ID3D12CommandList[] ppCommandLists = [m_commandList!];
			m_commandQueue!.ExecuteCommandLists(ppCommandLists.Length, ppCommandLists);
		}

		using (PIXEvent evt = new(m_commandQueue!, "Render UI"))
			RenderUI();

		// Present the frame.
		m_swapChain!.Present(1, 0);

		MoveToNextFrame();
	}

	// Load the sample assets.
	private void LoadAssets()
	{
		// Create an empty root signature.
		{
			D3D12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc = new(D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT);
			D3D12SerializeVersionedRootSignature(rootSignatureDesc, out var signature).ThrowIfFailed();
			m_rootSignature = m_d3d12Device!.CreateRootSignature<ID3D12RootSignature>(0, signature);
			NAME_D3D12_OBJECT(m_rootSignature);
		}

		// Create the pipeline state, which includes compiling and loading shaders.
		{
#if DEBUG
			// Enable better shader debugging with the graphics debugging tools.
			D3DCOMPILE compileFlags = D3DCOMPILE.D3DCOMPILE_DEBUG | D3DCOMPILE.D3DCOMPILE_SKIP_OPTIMIZATION;
#else
			D3DCOMPILE compileFlags = 0;
#endif

			D3DCompileFromFile(GetAssetFullPath("shaders.hlsl"), default, default, "VSMain", "vs_5_0", compileFlags, 0, out var vertexShader, default).ThrowIfFailed();
			D3DCompileFromFile(GetAssetFullPath("shaders.hlsl"), default, default, "PSMain", "ps_5_0", compileFlags, 0, out var pixelShader, default).ThrowIfFailed();

			// Define the vertex input layout.
			D3D12_INPUT_ELEMENT_DESC[] inputElementDescs = [
				new("POSITION", DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT),
				new("COLOR", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, alignedByteOffset: 12)
			];

			// Describe and create the graphics pipeline state object (PSO).
			D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = new()
			{
				InputLayout = new(inputElementDescs, out var pied),
				pRootSignature = m_rootSignature,
				VS = new D3D12_SHADER_BYTECODE(vertexShader),
				PS = new D3D12_SHADER_BYTECODE(pixelShader),
				RasterizerState = new(),
				BlendState = new(),
				SampleMask = uint.MaxValue,
				PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE,
				NumRenderTargets = 1,
				RTVFormats = [DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, default, default, default, default, default, default, default],
				DepthStencilState = { DepthEnable = false, StencilEnable = false },
				SampleDesc = { Count = 1 }
			};

			m_pipelineState = m_d3d12Device!.CreateGraphicsPipelineState<ID3D12PipelineState>(psoDesc);
			NAME_D3D12_OBJECT(m_pipelineState);
		}

		m_commandList = m_d3d12Device!.CreateCommandList<ID3D12GraphicsCommandList>(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_commandAllocators[m_frameIndex], m_pipelineState);
		NAME_D3D12_OBJECT(m_commandList);

		// Create D2D/DWrite objects for rendering text.
		{
			m_textBrush = m_d2dDeviceContext!.CreateSolidColorBrush((D3DCOLORVALUE)(COLORREF)Color.Black);
			m_textFormat = m_dWriteFactory!.CreateTextFormat("Verdana", default, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
				DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, 50, "en-us");
			m_textFormat.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);
			m_textFormat.SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
		}

		// Note: ComPtr's are CPU objects but this resource needs to stay in scope until the command list that references it has finished
		// executing on the GPU. We will flush the GPU at the end of this method to ensure the resource is not prematurely destroyed.

		// Create the vertex buffer.
		{
			// Define the geometry for a triangle.
			using SafeNativeArray<Vertex> triangleVertices = [
				new() { position = new(0.0f, 0.25f * m_aspectRatio, 0.0f), color = new(1.0f, 0.0f, 0.0f, 1.0f) },
				new() { position = new(0.25f, -0.25f * m_aspectRatio, 0.0f), color = new(0.0f, 1.0f, 0.0f, 1.0f) },
				new() { position = new(-0.25f, -0.25f * m_aspectRatio, 0.0f), color = new(0.0f, 0.0f, 1.0f, 1.0f) }
			];

			uint vertexBufferSize = triangleVertices.Size;

			m_vertexBuffer = m_d3d12Device!.CreateCommittedResource<ID3D12Resource>(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT), D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize), D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST);

			ID3D12Resource vertexBufferUpload = m_d3d12Device!.CreateCommittedResource<ID3D12Resource>(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD), D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize), (D3D12_RESOURCE_STATES)(0x1 | 0x2 | 0x40 | 0x80 | 0x200 | 0x800));

			NAME_D3D12_OBJECT(m_vertexBuffer);

			// Copy data to the intermediate upload heap and then schedule a copy from the upload heap to the vertex buffer.
			D3D12_SUBRESOURCE_DATA vertexData = new()
			{
				pData = triangleVertices,
				RowPitch = vertexBufferSize,
				SlicePitch = vertexBufferSize
			};

			UpdateSubresources(m_commandList, m_vertexBuffer, vertexBufferUpload, 0, 0, 1, [vertexData]);
			m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_vertexBuffer, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER)]);

			// Initialize the vertex buffer view.
			m_vertexBufferView.BufferLocation = m_vertexBuffer.GetGPUVirtualAddress();
			m_vertexBufferView.StrideInBytes = (uint)Marshal.SizeOf(typeof(Vertex));
			m_vertexBufferView.SizeInBytes = vertexBufferSize;
		}

		// Close the command list and execute it to begin the vertex buffer copy into the default heap.
		m_commandList.Close().ThrowIfFailed();
		ID3D12CommandList[] ppCommandLists = [m_commandList];
		m_commandQueue!.ExecuteCommandLists(ppCommandLists.Length, ppCommandLists);

		// Create synchronization objects and wait until assets have been uploaded to the GPU.
		{
			m_fence = m_d3d12Device!.CreateFence<ID3D12Fence>(m_fenceValues[m_frameIndex]);
			m_fenceValues[m_frameIndex]++;

			// Create an event handle to use for frame synchronization.
			m_fenceEvent = CreateEvent(default, false, false, default);
			if (m_fenceEvent.IsInvalid)
			{
				Win32Error.GetLastError().ToHRESULT().ThrowIfFailed();
			}

			// Wait for the command list to execute; we are reusing the same command list in our main loop but for now, we just want to wait
			// for setup to complete before continuing.
			WaitForGpu();
		}
	}

	// Load the rendering pipeline dependencies.
	private void LoadPipeline()
	{
		DXGI_CREATE_FACTORY dxgiFactoryFlags = 0;
		D3D11_CREATE_DEVICE_FLAG d3d11DeviceFlags = D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT;
		D2D1_FACTORY_OPTIONS d2dFactoryOptions = default;

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
				d3d11DeviceFlags |= D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_DEBUG;
				d2dFactoryOptions.debugLevel = D2D1_DEBUG_LEVEL.D2D1_DEBUG_LEVEL_INFORMATION;
			}
		}
#endif

		IDXGIFactory4 factory = CreateDXGIFactory2<IDXGIFactory4>(dxgiFactoryFlags);

		if (m_useWarpDevice)
		{
			factory.EnumWarpAdapter(out IDXGIAdapter? warpAdapter).ThrowIfFailed();

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, (IDXGIAdapter)warpAdapter!, out m_d3d12Device).ThrowIfFailed();
		}
		else
		{
			GetHardwareAdapter(factory, out var hardwareAdapter);

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, hardwareAdapter, out m_d3d12Device).ThrowIfFailed();
		}

#if DEBUG
		// Filter a debug error coming from the 11on12 layer.
		if (m_d3d12Device is ID3D12InfoQueue infoQueue)
		{
			// Suppress whole categories of messages.
			//D3D12_MESSAGE_CATEGORY[] categories = default;

			// Suppress messages based on their severity level.
			using SafeNativeArray<D3D12_MESSAGE_SEVERITY> severities = [D3D12_MESSAGE_SEVERITY.D3D12_MESSAGE_SEVERITY_INFO];

			// Suppress individual messages by their ID.
			using SafeNativeArray<int> denyIds = [
				// This occurs when there are uninitialized descriptors in a descriptor table, even when a shader does not access the
				// missing descriptors.
				(int)D3D12_MESSAGE_ID.D3D12_MESSAGE_ID_INVALID_DESCRIPTOR_HANDLE,
			];

			D3D12_INFO_QUEUE_FILTER filter = new()
			{
				DenyList = new()
				{
					//NumCategories = countof(categories),
					//pCategoryList = categories,
					NumSeverities = 1,
					pSeverityList = severities,
					NumIDs = (uint)denyIds.Count,
					pIDList = denyIds,
				}
			};

			infoQueue.PushStorageFilter(filter).ThrowIfFailed();
		}
#endif

		// Describe and create the command queue.
		D3D12_COMMAND_QUEUE_DESC queueDesc = new()
		{
			Flags = D3D12_COMMAND_QUEUE_FLAGS.D3D12_COMMAND_QUEUE_FLAG_NONE,
			Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT
		};
		m_commandQueue = m_d3d12Device!.CreateCommandQueue<ID3D12CommandQueue>(queueDesc);

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
		m_frameIndex = m_swapChain.GetCurrentBackBufferIndex();

		// Create an 11 device wrapped around the 12 device and share 12's command queue.
		D3D11On12CreateDevice(m_d3d12Device!, d3d11DeviceFlags, default, 0, [m_commandQueue], 1, 0,
			out var d3d11Device, out m_d3d11DeviceContext, out _);

		// Query the 11On12 device from the 11 device.
		m_d3d11On12Device = (ID3D11On12Device)d3d11Device;

		// Create D2D/DWrite components.
		{
			D2D1_DEVICE_CONTEXT_OPTIONS deviceOptions = D2D1_DEVICE_CONTEXT_OPTIONS.D2D1_DEVICE_CONTEXT_OPTIONS_NONE;
			m_d2dFactory = D2D1CreateFactory<ID2D1Factory3>(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, d2dFactoryOptions);
			IDXGIDevice dxgiDevice = (IDXGIDevice)m_d3d11On12Device;
			m_d2dFactory.CreateDevice(dxgiDevice, out m_d2dDevice);
			m_d2dDevice.CreateDeviceContext(deviceOptions, out m_d2dDeviceContext);
			m_dWriteFactory = DWriteCreateFactory<IDWriteFactory>(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED);
		}

		// Query the desktop's dpi settings, which will be used to create D2D's render targets.
#pragma warning disable CS0618 // Type or member is obsolete
		m_d2dFactory.GetDesktopDpi(out var dpiX, out var dpiY);
#pragma warning restore CS0618 // Type or member is obsolete
		D2D1_BITMAP_PROPERTIES1 bitmapProperties = new()
		{
			bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
			pixelFormat = new D2D1_PIXEL_FORMAT() { format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
			dpiX = dpiX,
			dpiY = dpiY
		};

		// Create descriptor heaps.
		{
			// Describe and create a render target view (RTV) descriptor heap.
			D3D12_DESCRIPTOR_HEAP_DESC rtvHeapDesc = new()
			{
				NumDescriptors = FrameCount,
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_NONE
			};
			m_rtvHeap = m_d3d12Device!.CreateDescriptorHeap<ID3D12DescriptorHeap>(rtvHeapDesc);

			m_rtvDescriptorSize = m_d3d12Device!.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
		}

		// Create frame resources.
		{
			m_rtvHeap.GetCPUDescriptorHandleForHeapStart(out var hCpuDesc);
			D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(hCpuDesc, 0);

			// Create a RTV, D2D render target, and a command allocator for each frame.
			for (uint n = 0; n < FrameCount; n++)
			{
				m_renderTargets[n] = m_swapChain.GetBuffer<ID3D12Resource>(n);
				m_d3d12Device.CreateRenderTargetView(m_renderTargets[n], default, rtvHandle);

				NAME_D3D12_OBJECT_INDEXED(m_renderTargets, n);

				// Create a wrapped 11On12 resource of this back buffer. Since we are rendering all D3D12 content first and then all D2D
				// content, we specify the In resource state as RENDER_TARGET - because D3D12 will have last used it in this state - and the
				// Out resource state as PRESENT. When ReleaseWrappedResources() is called on the 11On12 device, the resource will be
				// transitioned to the PRESENT state.
				D3D11_RESOURCE_FLAGS d3d11Flags = new() { BindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET };
				m_d3d11On12Device.CreateWrappedResource(m_renderTargets[n], d3d11Flags,
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT,
					typeof(ID3D11Resource).GUID, out var ppv).ThrowIfFailed();
				m_wrappedBackBuffers[n] = (ID3D11Resource)ppv;

				// Create a render target for D2D to draw directly to this back buffer.
				IDXGISurface surface = (IDXGISurface)m_wrappedBackBuffers[n];
				using (SafeCoTaskMemStruct<D2D1_BITMAP_PROPERTIES1> bp = bitmapProperties)
					m_d2dRenderTargets[n] = m_d2dDeviceContext.CreateBitmapFromDxgiSurface(surface, bp);

				rtvHandle.Offset(1, m_rtvDescriptorSize);

				m_commandAllocators[n] = m_d3d12Device.CreateCommandAllocator<ID3D12CommandAllocator>(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT);
			}
		}
	}

	// Prepare to render the next frame.
	private void MoveToNextFrame()
	{
		// Schedule a Signal command in the queue.
		ulong currentFenceValue = m_fenceValues[m_frameIndex];
		m_commandQueue!.Signal(m_fence!, currentFenceValue).ThrowIfFailed();

		// Update the frame index.
		m_frameIndex = m_swapChain!.GetCurrentBackBufferIndex();

		// If the next frame is not ready to be rendered yet, wait until it is ready.
		if (m_fence!.GetCompletedValue() < m_fenceValues[m_frameIndex])
		{
			m_fence.SetEventOnCompletion(m_fenceValues[m_frameIndex], m_fenceEvent!).ThrowIfFailed();
			m_fenceEvent!.Wait();
		}

		// Set the fence value for the next frame.
		m_fenceValues[m_frameIndex] = currentFenceValue + 1;
	}

	private void PopulateCommandList()
	{
		// Command list allocators can only be reset when the associated command lists have finished execution on the GPU; apps should use
		// fences to determine GPU execution progress.
		m_commandAllocators![m_frameIndex].Reset().ThrowIfFailed();

		// However, when ExecuteCommandList() is called on a particular command list, that command list can then be reset at any time and
		// must be before re-recording.
		m_commandList!.Reset(m_commandAllocators![m_frameIndex], default);

		// Set necessary state.
		m_commandList.SetGraphicsRootSignature(m_rootSignature);
		m_commandList.RSSetViewports(1, [m_viewport]);
		m_commandList.RSSetScissorRects(1, [m_scissorRect]);

		// Indicate that the back buffer will be used as a render target.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[(int)m_frameIndex], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET)]);

		m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(out var hCpuDesc);
		D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(hCpuDesc, (int)m_frameIndex, m_rtvDescriptorSize);
		m_commandList.OMSetRenderTargets(1, [rtvHandle], false, default);

		// Record commands.
		float[] clearColor = [0.0f, 0.2f, 0.4f, 1.0f];
		m_commandList.ClearRenderTargetView(rtvHandle, clearColor);
		m_commandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		m_commandList.IASetVertexBuffers(0, 1, [m_vertexBufferView]);
		m_commandList.DrawInstanced(3, 1, 0, 0);

		// Note: do not transition the render target to present here. the transition will occur when the wrapped 11On12 render target
		// resource is released.

		m_commandList.Close().ThrowIfFailed();
	}

	// Render text over D3D12 using D2D via the 11On12 device.
	private void RenderUI()
	{
		D2D_SIZE_F rtSize = m_d2dRenderTargets[m_frameIndex].GetSize();
		D2D_RECT_F textRect = new(0, 0, rtSize.width, rtSize.height);
		const string text = "11On12";

		// Acquire our wrapped render target resource for the current back buffer.
		m_d3d11On12Device!.AcquireWrappedResources([m_wrappedBackBuffers[m_frameIndex]], 1);

		// Render text directly to the back buffer.
		m_d2dDeviceContext!.SetTarget(m_d2dRenderTargets[m_frameIndex]);
		m_d2dDeviceContext.BeginDraw();
		m_d2dDeviceContext.SetTransform(new() { m11 = 1f, m22 = 1f });
		m_d2dDeviceContext.DrawText(text, (uint)text.Length, m_textFormat!, textRect, m_textBrush!);
		m_d2dDeviceContext.EndDraw(out _, out _);

		// Release our wrapped render target resource. Releasing transitions the back buffer resource to the state specified as the OutState
		// when the wrapped resource was created.
		m_d3d11On12Device.ReleaseWrappedResources([m_wrappedBackBuffers[m_frameIndex]], 1);

		// Flush to submit the 11 command list to the shared command queue.
		m_d3d11DeviceContext!.Flush();
	}

	// Wait for pending GPU work to complete.
	private void WaitForGpu()
	{
		if (m_fence is null) return;

		// Schedule a Signal command in the queue.
		m_commandQueue!.Signal(m_fence!, m_fenceValues[m_frameIndex]).ThrowIfFailed();

		// Wait until the fence has been processed.
		m_fence!.SetEventOnCompletion(m_fenceValues[m_frameIndex], m_fenceEvent!).ThrowIfFailed();
		m_fenceEvent!.Wait();

		// Increment the fence value for the current frame.
		m_fenceValues[m_frameIndex]++;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Vertex
	{
		public D3DCOLORVALUE color;
		public D2D_VECTOR_3F position;
	}
}