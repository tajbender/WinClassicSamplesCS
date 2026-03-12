using System.Diagnostics;
using Vanara.PInvoke;

internal class D3D12HelloGenericPrograms : DXSample
{
	private const uint FrameCount = 2;

	private ID3D12CommandAllocator? m_commandAllocator;
	private ID3D12GraphicsCommandList10? m_commandList;
	private ID3D12CommandQueue? m_commandQueue;
	private ID3D12Device14? m_device;
	private ID3D12Fence? m_fence;
	private SafeEventHandle? m_fenceEvent;
	private ulong m_fenceValue;
	private uint m_frameIndex = 0;
	private readonly D3D12_PROGRAM_IDENTIFIER[] m_genericProgram = new D3D12_PROGRAM_IDENTIFIER[2];
	private readonly ID3D12Resource[] m_renderTargets = new ID3D12Resource[FrameCount];
	private ID3D12RootSignature? m_rootSignature;
	private uint m_rtvDescriptorSize;
	private ID3D12DescriptorHeap? m_rtvHeap;
	private RECT m_scissorRect;
	private readonly ID3D12StateObject[] m_stateObject = new ID3D12StateObject[2];
	private IDXGISwapChain3? m_swapChain;
	private ID3D12Resource? m_vertexBuffer;
	private D3D12_VERTEX_BUFFER_VIEW m_vertexBufferView;
	private readonly D3D12_VIEWPORT[] m_viewport = new D3D12_VIEWPORT[2];

	public D3D12HelloGenericPrograms(int width, int height, string name) : base(width, height, name)
	{
		m_scissorRect = new(0, 0, width, height);
		m_viewport[0] = new(0.0f, 0.0f, width / 2.0f, height);
		m_viewport[1] = new(width / 2.0f, 0.0f, width / 2.0f, height);
	}

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

#pragma warning disable CS0618 // Type or member is obsolete
	private static HRESULT CompileDxilLibraryFromFile(string pFile, string pEntry, string pTarget, DxcDefine[]? pDefines, out ID3DBlob? ppCode)
	{
		ppCode = default;

		var hr = DxcCreateInstance(CLSID_DxcLibrary, out IDxcLibrary? library);
		if (hr.Failed)
		{
			Debug.WriteLine("Failed to instantiate compiler.");
			return hr;
		}

		HRESULT createBlobHr = library!.CreateBlobFromFile(pFile, default, out var source);
		if (createBlobHr != HRESULT.S_OK)
		{
			Debug.WriteLine("Create Blob From File Failed - perhaps file is missing?");
			return HRESULT.E_FAIL;
		}

		hr = library!.CreateIncludeHandler(out var includeHandler);
		if (hr.Failed)
		{
			Debug.WriteLine("Failed to create include handler.");
			return hr;
		}
		hr = DxcCreateInstance(CLSID_DxcCompiler, out IDxcCompiler? compiler);
		if (hr.Failed)
		{
			Debug.WriteLine("Failed to instantiate compiler.");
			return hr;
		}

		string[] args = [""];
		hr = compiler!.Compile(source, default, pEntry, pTarget, args, (uint)args.Length, pDefines, pDefines?.Length ?? 0, includeHandler, out var operationResult);
		if (hr.Failed)
		{
			Debug.WriteLine("Failed to compile.");
			return hr;
		}

		operationResult.GetStatus(out hr);
		if (hr.Succeeded)
		{
			hr = operationResult.GetResult(out ppCode);
			if (hr.Failed)
			{
				Debug.WriteLine("Failed to retrieve compiled code.");
			}
		}
		if (operationResult.GetErrorBuffer(out var pErrors).Failed)
		{
			Debug.WriteLine((StrPtrAnsi)pErrors!.GetBufferPointer());
		}

		return hr;
	}
#pragma warning restore CS0618 // Type or member is obsolete

	private void LoadAssets()
	{
		// Create an empty root signature.
		{
			D3D12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc = new(D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT);
			D3D12SerializeVersionedRootSignature(rootSignatureDesc, out var signature).ThrowIfFailed();
			m_rootSignature = m_device!.CreateRootSignature<ID3D12RootSignature>(0, signature);
		}

		// Create a generic program (like a PSO) in a state object, including first compiling shaders
		{
			CompileDxilLibraryFromFile(GetAssetFullPath("shaders.hlsl"), "VSMain", "vs_6_0", null, out var vertexShader).ThrowIfFailed();
			CompileDxilLibraryFromFile(GetAssetFullPath("shaders.hlsl"), "PSMain", "ps_6_0", null, out var pixelShader).ThrowIfFailed();

			// Define the vertex input layout.
			D3D12_INPUT_ELEMENT_DESC[] inputElementDescs = [
				new("POSITION", DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT),
				new("COLOR", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, alignedByteOffset: 12)
			];

			// Describe and create the graphics pipeline as a generic program.

			/* Here is what it would have looked like as a PSO:
			D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = default;
			psoDesc.InputLayout = { inputElementDescs, countof(inputElementDescs) };
			psoDesc.pRootSignature = m_rootSignature;
			psoDesc.VS = D3D12_SHADER_BYTECODE(vertexShader);
			psoDesc.PS = D3D12_SHADER_BYTECODE(pixelShader);
			psoDesc.RasterizerState = D3D12_RASTERIZER_DESC(D3D12_DEFAULT);
			psoDesc.BlendState = D3D12_BLEND_DESC(D3D12_DEFAULT);
			psoDesc.DepthStencilState.DepthEnable = false;
			psoDesc.DepthStencilState.StencilEnable = false;
			psoDesc.SampleMask = UINT_MAX;
			psoDesc.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
			psoDesc.NumRenderTargets = 1;
			psoDesc.RTVFormats[0] = DXGI_FORMAT_R8G8B8A8_UNORM;
			psoDesc.SampleDesc.Count = 1;
			ThrowIfFailed(m_device.CreateGraphicsPipelineState(&psoDesc, IID_PPV_ARGS(&m_pipelineState)));
			*/

			D3D12_STATE_OBJECT_DESC_MGD SODesc = new(D3D12_STATE_OBJECT_TYPE.D3D12_STATE_OBJECT_TYPE_EXECUTABLE);

			// Optional flag to allow state object additions
			SODesc.Add(D3D12_STATE_OBJECT_FLAGS.D3D12_STATE_OBJECT_FLAG_ALLOW_STATE_OBJECT_ADDITIONS);

			// Define the building blocks for the program - the individual subobjects / shaders
			D3D12_INPUT_LAYOUT_DESC iL = new(inputElementDescs, out var _);
			SODesc.Add(iL);

			var rootSig = new D3D12_GLOBAL_ROOT_SIGNATURE() { pGlobalRootSignature = m_rootSignature };
			SODesc.Add(rootSig);

			// Take whatever the shader is and rename it to myVS. Instead of "*" could have used the actual name of the shader. Also could
			// have omitted this line completely, which would just import all exports in the binary (just one shader here), using the name
			// of the shader in the lib. Could also have listed the name of the shader in the lib on its own for the same effect (would
			// ensure that the shader you are expecting is actually there)
			var pVS = SafeCoTaskMemHandle.CreateFromList([new D3D12_EXPORT_DESC("*", "myRenamedVS")]);
			var VS = new D3D12_DXIL_LIBRARY_DESC() { DXILLibrary = new(vertexShader!), NumExports = 1, pExports = pVS };
			SODesc.Add(VS);

			var PS = new D3D12_DXIL_LIBRARY_DESC() { DXILLibrary = new(pixelShader!) };
			SODesc.Add(PS);

			// Don't need to add the following descs since they're all just default
			//var pRast = SODesc.CreateSubobject<D3D12_RASTERIZER_SUBOBJECT>();
			//var pBlend = SODesc.CreateSubobject<D3D12_BLEND_SUBOBJECT>();
			//var pDepth = SODesc.CreateSubobject<D3D12_DEPTH_STENCIL_DESC2>();
			//var pSampleMask = SODesc.CreateSubobject<D3D12_SAMPLE_MASK_SUBOBJECT>();
			//var pSampleDesc = SODesc.CreateSubobject<D3D12_SAMPLE_DESC_SUBOBJECT>();
			var primitiveTopology = D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
			SODesc.Add(primitiveTopology);

			D3D12_RT_FORMAT_ARRAY rtFormats = new([DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM]);
			SODesc.Add(rtFormats);

			// Then define a generic program out of the building blocks: name, list of shaders, list of subobjects: (Can define multiple
			// generic programs in the same state object, each picking the building blocks it wants)

			D3D12_GENERIC_PROGRAM_DESC genericProgram = new("myGenericProgram", ["myRenamedVS", "PSMain"], [primitiveTopology, rtFormats], out var _);
			SODesc.Add(genericProgram);
			// Notice the root signature isn't being added to the list here. Root signatures are associated with shader exports directly,
			// not programs. The single root sig in the state objcet above with no associations defined automatically becomes a default root
			// sig that applies to all exports, so myVS and myPS get it.

			m_device!.CreateStateObject(SODesc, out m_stateObject[0]!).ThrowIfFailed();

			ID3D12StateObjectProperties1 pSOProperties = (ID3D12StateObjectProperties1)m_stateObject[0];
			m_genericProgram[0] = pSOProperties.GetProgramIdentifier("myGenericProgram");
		}

		// Add an additional program thatMake an additional permutationCreate the pipeline state, which includes compiling and loading shaders.
		{
			CompileDxilLibraryFromFile(GetAssetFullPath("shaders.hlsl"), "PSMain2", "ps_6_0", null, out var pixelShader2).ThrowIfFailed();

			// Define the vertex input layout.
			D3D12_INPUT_ELEMENT_DESC[] inputElementDescs = [
				new("POSITION", DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT),
				new("COLOR", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, alignedByteOffset: 12)
			];

			D3D12_STATE_OBJECT_DESC_MGD SODesc = new(D3D12_STATE_OBJECT_TYPE.D3D12_STATE_OBJECT_TYPE_EXECUTABLE);

			// Optional flag to allow state object additions
			SODesc.Add(D3D12_STATE_OBJECT_FLAGS.D3D12_STATE_OBJECT_FLAG_ALLOW_STATE_OBJECT_ADDITIONS);

			// First define the building blocks for the program - the individual subobjects / shaders Subobjects like input layout etc. need
			// to be redefined, as there currently isn't a way to define them in DXIL such that they can be reused from an existing state
			// object being added to. Root signatures can be defined in DXIL and reused (by referering to them by name), but that isn't
			// shown here.
			D3D12_INPUT_LAYOUT_DESC iL = new(inputElementDescs, out var _);
			SODesc.Add(iL);

			var rootSig = new D3D12_GLOBAL_ROOT_SIGNATURE() { pGlobalRootSignature = m_rootSignature };
			SODesc.Add(rootSig);

			// not listing any exports means what's in the bytecode will be used as is, which is "PSMain2"
			var PS = new D3D12_DXIL_LIBRARY_DESC() { DXILLibrary = new(pixelShader2!) };
			SODesc.Add(PS);

			var primitiveTopology = D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
			SODesc.Add(primitiveTopology);

			D3D12_RT_FORMAT_ARRAY rtFormats = new([DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM]);
			SODesc.Add(rtFormats);

			D3D12_GENERIC_PROGRAM_DESC genericProgram = new("myGenericProgram2", ["myRenamedVS", "PSMain2"], [iL, primitiveTopology, rtFormats], out var _);
			SODesc.Add(genericProgram);
			// Notice the root signature isn't being added to the list here. Root signatures are associated with shader exports directly,
			// not programs. The single root sig in the state objcet above with no associations defined automatically becomes a default root
			// sig that applies to all exports, so myVS and myPS get it.

			m_device!.AddToStateObject(SODesc, m_stateObject[0], out m_stateObject[1]!).ThrowIfFailed();

			ID3D12StateObjectProperties1 pSOProperties = (ID3D12StateObjectProperties1)m_stateObject[1];
			m_genericProgram[1] = pSOProperties.GetProgramIdentifier("myGenericProgram2");
		}

		// Create the command list.
		m_commandList = m_device!.CreateCommandList<ID3D12GraphicsCommandList10>(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_commandAllocator!);

		// Command lists are created in the recording state, but there is nothing to record yet. The main loop expects it to be closed, so
		// close it now.
		m_commandList.Close().ThrowIfFailed();

		// Create the vertex buffer.
		{
			// Define the geometry for a triangle.
			Vertex[] triangleVertices = [
				new() { position = new(0.0f, 0.25f * m_aspectRatio, 0.0f), color = new(1.0f, 0.0f, 0.0f, 1.0f) },
				new() { position = new(0.25f, -0.25f * m_aspectRatio, 0.0f), color = new(0.0f, 1.0f, 0.0f, 1.0f) },
				new() { position = new(-0.25f, -0.25f * m_aspectRatio, 0.0f), color = new(0.0f, 0.0f, 1.0f, 1.0f) }
			];

			uint vertexBufferSize = (uint)(Marshal.SizeOf(typeof(Vertex)) * triangleVertices.Length);

			// Note: using upload heaps to transfer static data like vert buffers is not recommended. Every time the GPU needs it, the
			// upload heap will be marshalled over. Please read up on Default Heap usage. An upload heap is used here for code simplicity
			// and because there are very few verts to actually transfer.
			m_vertexBuffer = m_device!.CreateCommittedResource<ID3D12Resource>(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD), D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize), D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ);

			// Copy the triangle data to the vertex buffer. We do not intend to read from this resource on the CPU.
			m_vertexBuffer.Map(0, new(0, 0), out IntPtr pVertexDataBegin).ThrowIfFailed();
			pVertexDataBegin.Write(triangleVertices, 0, vertexBufferSize);
			m_vertexBuffer.Unmap(0, default);

			// Initialize the vertex buffer view.
			m_vertexBufferView.BufferLocation = m_vertexBuffer.GetGPUVirtualAddress();
			m_vertexBufferView.StrideInBytes = (uint)Marshal.SizeOf(typeof(Vertex));
			m_vertexBufferView.SizeInBytes = vertexBufferSize;
		}

		// Create synchronization objects and wait until assets have been uploaded to the GPU.
		{
			m_fence = m_device!.CreateFence<ID3D12Fence>();
			m_fenceValue = 1;

			// Create an event handle to use for frame synchronization.
			m_fenceEvent = CreateEvent(default, false, false, default);
			Win32Error.ThrowLastErrorIfInvalid(m_fenceEvent);

			// Wait for the command list to execute; we are reusing the same command list in our main loop but for now, we just want to wait
			// for setup to complete before continuing.
			WaitForPreviousFrame();
		}
	}

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
			factory.EnumWarpAdapter(out IDXGIAdapter? warpAdapter).ThrowIfFailed();

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, (IDXGIAdapter)warpAdapter!, out m_device).ThrowIfFailed();
		}
		else
		{
			GetHardwareAdapter(factory, out var hardwareAdapter);

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, hardwareAdapter, out m_device).ThrowIfFailed();
		}

		D3D12_FEATURE_DATA_SHADER_MODEL shaderModel = new() { HighestShaderModel = D3D_SHADER_MODEL.D3D_SHADER_MODEL_6_8 };
		m_device!.CheckFeatureSupport(ref shaderModel, D3D12_FEATURE.D3D12_FEATURE_SHADER_MODEL).ThrowIfFailed();
		if (shaderModel.HighestShaderModel < D3D_SHADER_MODEL.D3D_SHADER_MODEL_6_8)
		{
			Debug.WriteLine("Generic Programs require a device with shader model 6.8 support (though the shaders used don't have be 6.8 shaders).");
			HRESULT.ThrowIfFailed(HRESULT.E_FAIL);
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

		// Swap chain needs the queue so that it can force a flush on it.
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
			m_rtvHeap.GetCPUDescriptorHandleForHeapStart(out var rtvHandle);

			// Create a RTV for each frame.
			for (uint n = 0; n < FrameCount; n++)
			{
				m_renderTargets[n] = m_swapChain.GetBuffer<ID3D12Resource>(n);
				m_device.CreateRenderTargetView(m_renderTargets[n], default, rtvHandle);
				rtvHandle.Offset(1, m_rtvDescriptorSize);
			}
		}

		m_commandAllocator = m_device.CreateCommandAllocator<ID3D12CommandAllocator>(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT);
	}

	private void PopulateCommandList()
	{
		// Command list allocators can only be reset when the associated command lists have finished execution on the GPU; apps should use
		// fences to determine GPU execution progress.
		m_commandAllocator!.Reset().ThrowIfFailed();

		// However, when ExecuteCommandList() is called on a particular command list, that command list can then be reset at any time and
		// must be before re-recording.
		m_commandList!.Reset(m_commandAllocator, default);

		// Set necessary state.
		m_commandList.SetGraphicsRootSignature(m_rootSignature);
		m_commandList.RSSetScissorRects(1, [m_scissorRect]);

		// Indicate that the back buffer will be used as a render target.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[(int)m_frameIndex], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET)]);

		m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(out var cpuHandle);
		D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(cpuHandle, (int)m_frameIndex, m_rtvDescriptorSize);
		m_commandList.OMSetRenderTargets(1, [rtvHandle], false, default);

		// Record commands.
		float[] clearColor = [0.0f, 0.2f, 0.4f, 1.0f];
		m_commandList.ClearRenderTargetView(rtvHandle, clearColor);
		m_commandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		m_commandList.IASetVertexBuffers(0, 1, [m_vertexBufferView]);

		for (uint i = 0; i < 2; i++)
		{
			D3D12_SET_PROGRAM_DESC SP = new()
			{
				Type = D3D12_PROGRAM_TYPE.D3D12_PROGRAM_TYPE_GENERIC_PIPELINE
			};
			SP.GenericPipeline = new() { ProgramIdentifier = m_genericProgram[i] };
			m_commandList.SetProgram(SP);
			m_commandList.RSSetViewports(1, [m_viewport[i]]);
			m_commandList.DrawInstanced(3, 1, 0, 0);
		}

		// Indicate that the back buffer will now be used to present.
		m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT)]);

		m_commandList.Close().ThrowIfFailed();
	}

	private void WaitForPreviousFrame()
	{
		if (m_fence is null) return;

		// WAITING FOR THE FRAME TO COMPLETE BEFORE CONTINUING IS NOT BEST PRACTICE. This is code implemented as such for simplicity. The
		// D3D12HelloFrameBuffering sample illustrates how to use fences for efficient resource usage and to maximize GPU utilization.

		// Signal and increment the fence value.
		ulong fence = m_fenceValue;
		m_commandQueue!.Signal(m_fence!, fence).ThrowIfFailed();
		m_fenceValue++;

		// Wait until the previous frame is finished.
		if (m_fence!.GetCompletedValue() < fence)
		{
			m_fence.SetEventOnCompletion(fence, m_fenceEvent!).ThrowIfFailed();
			m_fenceEvent!.Wait();
		}

		m_frameIndex = m_swapChain!.GetCurrentBackBufferIndex();
	}

	private struct Vertex
	{
		public D3DCOLORVALUE color;
		public D2D_VECTOR_3F position;
	}
}