using System.Diagnostics;
using Vanara;
using static Vanara.PInvoke.DirectXMath;

internal partial class D3D12Fullscreen(int width, int height, string name) : DXSample(width, height, name)
{
	private const int FrameCount = 2;
	private static readonly float[] ClearColor = [0.0f, 0.2f, 0.4f, 1f];
	private static readonly float[] LetterboxColor = [0, 0, 0, 1];
	private static readonly float QuadHeight = 720.0f;
	private static readonly float QuadWidth = 20.0f;

	private static readonly Resolution[] m_resolutionOptions = [
		new(800u, 600u),
		new(1200u, 900u),
		new(1280u, 720u),
		new(1920u, 1080u),
		new(1920u, 1200u),
		new(2560u, 1440u),
		new(3440u, 1440u),
		new(3840u, 2160u)
	];
	private static readonly int m_resolutionOptionsCount = m_resolutionOptions.Length;
	private static uint m_resolutionIndex = 2;  // Index of the current scene rendering resolution from m_resolutionOptions.

	// Pipeline objects.
	private D3D12_VIEWPORT m_sceneViewport;
	private uint m_cbvSrvDescriptorSize;
	private ID3D12DescriptorHeap? m_cbvSrvHeap;
	private ID3D12CommandQueue? m_commandQueue;
	private ID3D12Device? m_device;
	private ID3D12Resource? m_intermediateRenderTarget;
	private readonly ID3D12CommandAllocator[] m_postCommandAllocators = new ID3D12CommandAllocator[FrameCount];
	private ID3D12GraphicsCommandList? m_postCommandList;
	private ID3D12PipelineState? m_postPipelineState;
	private ID3D12RootSignature? m_postRootSignature;
	private RECT m_postScissorRect;
	private D3D12_VIEWPORT m_postViewport;
	private ID3D12Resource?[]? m_renderTargets = new ID3D12Resource[FrameCount];
	private uint m_rtvDescriptorSize;
	private ID3D12DescriptorHeap? m_rtvHeap;
	private readonly ID3D12CommandAllocator[] m_sceneCommandAllocators = new ID3D12CommandAllocator[FrameCount];
	private ID3D12GraphicsCommandList? m_sceneCommandList;
	private ID3D12PipelineState? m_scenePipelineState;
	private ID3D12RootSignature? m_sceneRootSignature;
	private RECT m_sceneScissorRect;
	private IDXGISwapChain3? m_swapChain;

	// Synchronization objects.
	private int m_frameIndex;
	private ID3D12Fence? m_fence;
	private SafeEventHandle m_fenceEvent = SafeEventHandle.Null;
	private readonly ulong[] m_fenceValues = new ulong[FrameCount];

	// App resources.
	private ID3D12Resource? m_sceneVertexBuffer;
	private D3D12_VERTEX_BUFFER_VIEW m_sceneVertexBufferView;
	private ID3D12Resource? m_postVertexBuffer;
	private D3D12_VERTEX_BUFFER_VIEW m_postVertexBufferView;
	private ID3D12Resource? m_sceneConstantBuffer;
	private SceneConstantBuffer m_sceneConstantBufferData;
	private IntPtr m_pCbvDataBegin;

	// Track the state of the window. If it's minimized the app may decide not to render frames.
	private bool m_windowVisible = true;
	private bool m_windowedMode = true;

	public override void OnDestroy()
	{
		// Ensure that the GPU is no longer referencing resources that are about to be cleaned up by the destructor.
		WaitForGpu();

		if (!TearingSupport)
		{
			// Fullscreen state should always be false before exiting the app.
			m_swapChain!.SetFullscreenState(false);
		}

		m_fenceEvent.Dispose();
	}

	public override void OnInit()
	{
		LoadPipeline();
		LoadAssets();
	}

	public override void OnKeyDown(VK key)
	{
		switch (key)
		{
			// Instrument the Space Bar to toggle between fullscreen states. The window message loop callback will receive a WM_SIZE message
			// once the window is in the fullscreen state. At that point, the IDXGISwapChain should be resized to match the new window size.
			//
			// NOTE: ALT+Enter will perform a similar operation; the code below is not required to enable that key combination.
			case VK.VK_SPACE:
				if (TearingSupport)
				{
					Win32App!.ToggleFullscreenWindow();
				}
				else
				{
					_ = m_swapChain!.GetFullscreenState(out var fullscreenState);
					try
					{
						m_swapChain.SetFullscreenState(!fullscreenState);
					}
					catch
					{
						// Transitions to fullscreen mode can fail when running apps over terminal services or for some other unexpected
						// reason. Consider notifying the user in some way when this happens.
						OutputDebugString("Fullscreen transition failed");
						Debug.Assert(false);
					}
				}
				break;

			// Instrument the Right Arrow key to change the scene rendering resolution to the next resolution option.
			case VK.VK_RIGHT:
				m_resolutionIndex = (m_resolutionIndex + 1) % (uint)m_resolutionOptionsCount;

				// Wait for the GPU to finish with the resources we're about to free.
				WaitForGpu();

				// Update resources dependent on the scene rendering resolution.
				LoadSceneResolutionDependentResources();
				break;

			// Instrument the Left Arrow key to change the scene rendering resolution to the previous resolution option.
			case VK.VK_LEFT:
				if (m_resolutionIndex == 0)
				{
					m_resolutionIndex = (uint)m_resolutionOptionsCount - 1;
				}
				else
				{
					m_resolutionIndex--;
				}

				// Wait for the GPU to finish with the resources we're about to free.
				WaitForGpu();

				// Update resources dependent on the scene rendering resolution.
				LoadSceneResolutionDependentResources();
				break;
		}
	}

	public override void OnRender()
	{
		if (m_windowVisible)
		{
			try
			{
				//using (var evt = new PIXEvent(m_commandQueue!, "Render"))
				using (PIXEvent pe = new(m_commandQueue!, "Render"))
				{
					// Record all the commands we need to render the scene into the command lists.
					PopulateCommandLists();

					// Execute the command lists.
					m_commandQueue!.ExecuteCommandLists([m_sceneCommandList!, m_postCommandList!]);
				}

				// When using sync interval 0, it is recommended to always pass the tearing flag when it is supported, even when presenting
				// in windowed mode. However, this flag cannot be used if the app is in fullscreen mode as a result of calling SetFullscreenState.
				DXGI_PRESENT presentFlags = (TearingSupport && m_windowedMode) ? DXGI_PRESENT.DXGI_PRESENT_ALLOW_TEARING : 0;

				// Present the frame.
				m_swapChain!.Present(0, presentFlags).ThrowIfFailed();

				MoveToNextFrame();
			}
			catch (Exception e)
			{
				if (e.HResult is HRESULT.DXGI_ERROR_DEVICE_REMOVED or HRESULT.DXGI_ERROR_DEVICE_RESET)
				{
					RestoreD3DResources();
				}
				else
				{
					throw;
				}
			}
		}
	}

	public override void OnSizeChanged(int width, int height, bool minimized)
	{
		// Determine if the swap buffers and other resources need to be resized or not.
		if ((width != Width || height != Height) && !minimized)
		{
			// Flush all current GPU commands.
			WaitForGpu();

			// Release the resources holding references to the swap chain (requirement of IDXGISwapChain::ResizeBuffers) and reset the frame
			// fence values to the current fence value.
			for (uint n = 0; n < FrameCount; n++)
			{
				m_renderTargets![n] = null;
				m_fenceValues[n] = m_fenceValues[m_frameIndex];
			}

			// Resize the swap chain to the desired dimensions.
			DXGI_SWAP_CHAIN_DESC1 desc = m_swapChain!.GetDesc1();
			m_swapChain.ResizeBuffers(FrameCount, (uint)width, (uint)height, desc.Format, desc.Flags);

			_ = m_swapChain.GetFullscreenState(out var fullscreenState);
			m_windowedMode = !fullscreenState;

			// Reset the frame index to the current back buffer index.
			m_frameIndex = (int)m_swapChain.GetCurrentBackBufferIndex();

			// Update the width, height, and aspect ratio member variables.
			UpdateForSizeChange(width, height);

			LoadSizeDependentResources();
		}

		m_windowVisible = !minimized;
	}

	public override void OnUpdate()
	{
		const float translationSpeed = 0.0001f;
		const float offsetBounds = 1.0f;

		m_sceneConstantBufferData.offset.x += translationSpeed;
		if (m_sceneConstantBufferData.offset.x > offsetBounds)
		{
			m_sceneConstantBufferData.offset.x = -offsetBounds;
		}

		XMMATRIX transform = XMMatrixOrthographicLH(m_resolutionOptions[m_resolutionIndex].Width, m_resolutionOptions[m_resolutionIndex].Height, 0.0f, 100.0f) *
			XMMatrixTranslation(m_sceneConstantBufferData.offset.x, 0.0f, 0.0f);

		XMStoreFloat4x4(out m_sceneConstantBufferData.transform, transform.XMMatrixTranspose());

		int offset = m_frameIndex * Marshal.SizeOf<SceneConstantBuffer>();
		Marshal.StructureToPtr(m_sceneConstantBufferData, m_pCbvDataBegin.Offset(offset), false);
	}

	// Load the sample assets.
	private void LoadAssets()
	{
		// This is the highest version the sample supports. If CheckFeatureSupport succeeds, the HighestVersion returned will not be greater than this.
		D3D12_FEATURE_DATA_ROOT_SIGNATURE featureData = new() { HighestVersion = D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1_1 };
		if (m_device!.CheckFeatureSupport(ref featureData, D3D12_FEATURE.D3D12_FEATURE_ROOT_SIGNATURE).Failed)
			featureData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1_0;

		// Create a root signature consisting of a descriptor table with a single CBV.
		{
			D3D12_DESCRIPTOR_RANGE1[] ranges = [new(D3D12_DESCRIPTOR_RANGE_TYPE.D3D12_DESCRIPTOR_RANGE_TYPE_CBV, 1, 0, 0, D3D12_DESCRIPTOR_RANGE_FLAGS.D3D12_DESCRIPTOR_RANGE_FLAG_DATA_STATIC)];
			D3D12_ROOT_PARAMETER1[] rootParameters = [D3D12_ROOT_PARAMETER1.InitAsDescriptorTable(ranges, D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_VERTEX, out var rp1)];

			// Allow input layout and deny uneccessary access to certain pipeline stages.
			D3D12_ROOT_SIGNATURE_FLAGS rootSignatureFlags =
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT |
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_HULL_SHADER_ROOT_ACCESS |
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_DOMAIN_SHADER_ROOT_ACCESS |
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_GEOMETRY_SHADER_ROOT_ACCESS |
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_PIXEL_SHADER_ROOT_ACCESS;

			D3D12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc = new(new D3D12_ROOT_SIGNATURE_DESC1(rootParameters, default, rootSignatureFlags, out var rsd));

			D3D12SerializeVersionedRootSignature(rootSignatureDesc, out var signature).ThrowIfFailed();
			m_sceneRootSignature = m_device!.CreateRootSignature<ID3D12RootSignature>(0, signature);
			NAME_D3D12_OBJECT(m_sceneRootSignature);
		}

		// Create a root signature consisting of a descriptor table with a SRV and a sampler.
		{
			// We don't modify the SRV in the post-processing command list after
			// SetGraphicsRootDescriptorTable is executed on the GPU so we can use the default
			// range behavior: D3D12_DESCRIPTOR_RANGE_FLAG_DATA_STATIC_WHILE_SET_AT_EXECUTE
			D3D12_DESCRIPTOR_RANGE1[] ranges = [new(D3D12_DESCRIPTOR_RANGE_TYPE.D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 0)];
			D3D12_ROOT_PARAMETER1[] rootParameters = [D3D12_ROOT_PARAMETER1.InitAsDescriptorTable(ranges, D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_PIXEL, out var rp1)];

			// Allow input layout and pixel shader access and deny uneccessary access to certain pipeline stages.
			D3D12_ROOT_SIGNATURE_FLAGS rootSignatureFlags =
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT |
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_HULL_SHADER_ROOT_ACCESS |
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_DOMAIN_SHADER_ROOT_ACCESS |
				D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_GEOMETRY_SHADER_ROOT_ACCESS;

			// Create a sampler.
			D3D12_STATIC_SAMPLER_DESC sampler = new()
			{
				Filter = D3D12_FILTER.D3D12_FILTER_MIN_MAG_MIP_LINEAR,
				AddressU = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_BORDER,
				AddressV = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_BORDER,
				AddressW = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_BORDER,
				MipLODBias = 0,
				MaxAnisotropy = 0,
				ComparisonFunc = D3D12_COMPARISON_FUNC.D3D12_COMPARISON_FUNC_NEVER,
				BorderColor = D3D12_STATIC_BORDER_COLOR.D3D12_STATIC_BORDER_COLOR_TRANSPARENT_BLACK,
				MinLOD = 0f,
				MaxLOD = D3D12_FLOAT32_MAX,
				ShaderRegister = 0,
				RegisterSpace = 0,
				ShaderVisibility = D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_PIXEL
			};
			DumpVal(sampler, nameof(sampler));

			D3D12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc = new(new D3D12_ROOT_SIGNATURE_DESC1(rootParameters, [sampler], rootSignatureFlags, out var rsd));

			D3DX12SerializeVersionedRootSignature(rootSignatureDesc, featureData.HighestVersion, out var signature, out _).ThrowIfFailed();
			m_postRootSignature = m_device!.CreateRootSignature<ID3D12RootSignature>(0, signature!);
			NAME_D3D12_OBJECT(m_postRootSignature);
		}

		// Create the pipeline state, which includes compiling and loading shaders.
		{
#if DEBUG
			// Enable better shader debugging with the graphics debugging tools.
			D3DCOMPILE compileFlags = D3DCOMPILE.D3DCOMPILE_DEBUG | D3DCOMPILE.D3DCOMPILE_SKIP_OPTIMIZATION;
#else
			D3DCOMPILE compileFlags = 0;
#endif

			D3DCompileFromFile(GetAssetFullPath("sceneShaders.hlsl"), default, default, "VSMain", "vs_5_0", compileFlags, 0, out var pSceneVertexShaderData, out _).ThrowIfFailed();
			D3DCompileFromFile(GetAssetFullPath("sceneShaders.hlsl"), default, default, "PSMain", "ps_5_0", compileFlags, 0, out var pScenePixelShaderData, out _).ThrowIfFailed();
			D3DCompileFromFile(GetAssetFullPath("postShaders.hlsl"), default, default, "VSMain", "vs_5_0", compileFlags, 0, out var pPostVertexShaderData, out _).ThrowIfFailed();
			D3DCompileFromFile(GetAssetFullPath("postShaders.hlsl"), default, default, "PSMain", "ps_5_0", compileFlags, 0, out var pPostPixelShaderData, out _).ThrowIfFailed();

			// Define the vertex input layouts.
			D3D12_INPUT_ELEMENT_DESC[] inputElementDescs = [
				new("POSITION", DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT, 0, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA),
				new("COLOR", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 12, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA)
			];
			D3D12_INPUT_ELEMENT_DESC[] scaleInputElementDescs = [
				new("POSITION", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 0, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA),
				new("TEXCOORD", DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT, D3D12_APPEND_ALIGNED_ELEMENT, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA)
			];

			// Describe and create the graphics pipeline state objects (PSOs).
			D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = new()
			{
				InputLayout = new(inputElementDescs, out var ied),
				pRootSignature = m_sceneRootSignature,
				VS = new(pSceneVertexShaderData),
				PS = new(pScenePixelShaderData),
				RasterizerState = new(),
				BlendState = new(),
				DepthStencilState = new(depthEnable: false, stencilEnable: false),
				SampleMask = uint.MaxValue,
				PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE,
				SampleDesc = new(1, 0)
			};
			psoDesc.SetRTVFormats([DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM]);
			DumpVal(psoDesc, nameof(psoDesc));
			m_scenePipelineState = m_device!.CreateGraphicsPipelineState<ID3D12PipelineState>(psoDesc);
			NAME_D3D12_OBJECT(m_scenePipelineState);

			psoDesc.InputLayout = new(scaleInputElementDescs, out ied);
			psoDesc.pRootSignature = m_postRootSignature;
			psoDesc.VS = new(pPostVertexShaderData);
			psoDesc.PS = new(pPostPixelShaderData);
			DumpVal(psoDesc, nameof(psoDesc));

			m_postPipelineState = m_device!.CreateGraphicsPipelineState<ID3D12PipelineState>(psoDesc);
			NAME_D3D12_OBJECT(m_postPipelineState);
		}

		// Single-use command allocator and command list for creating resources.
		HRESULT.ThrowIfFailed(m_device!.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, out ID3D12CommandAllocator? commandAllocator));
		HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, commandAllocator!, default, out ID3D12GraphicsCommandList? commandList));

		// Create the command lists.
		{
			HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_sceneCommandAllocators[m_frameIndex],
				m_scenePipelineState, out m_sceneCommandList));
			NAME_D3D12_OBJECT(m_sceneCommandList!);

			HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_postCommandAllocators[m_frameIndex],
				m_postPipelineState, out m_postCommandList));
			NAME_D3D12_OBJECT(m_postCommandList!);

			// Close the command lists.
			HRESULT.ThrowIfFailed(m_sceneCommandList!.Close());
			HRESULT.ThrowIfFailed(m_postCommandList!.Close());
		}

		LoadSizeDependentResources();
		LoadSceneResolutionDependentResources();

		// Create/update the vertex buffer.
		{
			// Define the geometry for a thin quad that will animate across the screen.
			float x = QuadWidth / 2.0f;
			float y = QuadHeight / 2.0f;
			SafeNativeArray<SceneVertex> quadVertices = [
				new(-x, -y, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f),
				new(-x, y, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f),
				new(x, -y, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f),
				new(x, y, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f)
			];

			uint vertexBufferSize = quadVertices.Size;

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(
				new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
				default,
				out m_sceneVertexBuffer));

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(
				new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
				default,
				out ID3D12Resource? sceneVertexBufferUpload));

			NAME_D3D12_OBJECT(m_sceneVertexBuffer!);

			// Copy data to the intermediate upload heap and then schedule a copy 
			// from the upload heap to the vertex buffer.
			D3D12_RANGE readRange = new(0, 0); // We do not intend to read from this resource on the CPU.
			HRESULT.ThrowIfFailed(sceneVertexBufferUpload!.Map(0, readRange, out var pVertexDataBegin));
			quadVertices.CopyTo(pVertexDataBegin, quadVertices.Size);
			sceneVertexBufferUpload!.Unmap(0, default);

			commandList!.CopyBufferRegion(m_sceneVertexBuffer!, 0, sceneVertexBufferUpload, 0, vertexBufferSize);
			commandList.ResourceBarrier([D3D12_RESOURCE_BARRIER.CreateTransition(m_sceneVertexBuffer!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER)]);

			// Initialize the vertex buffer views.
			m_sceneVertexBufferView = new()
			{
				BufferLocation = m_sceneVertexBuffer!.GetGPUVirtualAddress(),
				StrideInBytes = (uint)Marshal.SizeOf<SceneVertex>(),
				SizeInBytes = vertexBufferSize
			};
		}

		// Create/update the fullscreen quad vertex buffer.
		{
			// Define the geometry for a fullscreen quad.
			SafeNativeArray<PostVertex> postquadVertices = [
				new(-1.0f, -1.0f, 0.0f, 1.0f, 0.0f, 0.0f), // Bottom left.
				new(-1.0f,  1.0f, 0.0f, 1.0f, 0.0f, 1.0f), // Top left.
				new( 1.0f, -1.0f, 0.0f, 1.0f, 1.0f, 0.0f), // Bottom right.
				new( 1.0f,  1.0f, 0.0f, 1.0f, 1.0f, 1.0f)  // Top right.
			];

			uint vertexBufferSize = postquadVertices.Size;

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
				default,
				out m_postVertexBuffer));

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(vertexBufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
				default,
				out ID3D12Resource? postVertexBufferUpload));

			NAME_D3D12_OBJECT(m_postVertexBuffer!);

			// Copy data to the intermediate upload heap and then schedule a copy 
			// from the upload heap to the vertex buffer.
			D3D12_RANGE readRange = new(0, 0); // We do not intend to read from this resource on the CPU.
			HRESULT.ThrowIfFailed(postVertexBufferUpload!.Map(0, readRange, out var pVertexDataBegin));
			postquadVertices.CopyTo(pVertexDataBegin, postquadVertices.Size);
			postVertexBufferUpload!.Unmap(0, default);

			commandList.CopyBufferRegion(m_postVertexBuffer!, 0, postVertexBufferUpload, 0, vertexBufferSize);
			commandList.ResourceBarrier([D3D12_RESOURCE_BARRIER.CreateTransition(m_postVertexBuffer!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER)]);

			// Initialize the vertex buffer views.
			m_postVertexBufferView = new()
			{
				BufferLocation = m_postVertexBuffer!.GetGPUVirtualAddress(),
				StrideInBytes = (uint)Marshal.SizeOf<PostVertex>(),
				SizeInBytes = vertexBufferSize
			};
		}

		// Create the constant buffer.
		{
			var sceneConstantBufferSize = (uint)Marshal.SizeOf<SceneConstantBuffer>();

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(
				new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer((ulong)sceneConstantBufferSize * FrameCount),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
				default,
				out m_sceneConstantBuffer));

			NAME_D3D12_OBJECT(m_sceneConstantBuffer!);

			// Describe and create constant buffer views.
			D3D12_CONSTANT_BUFFER_VIEW_DESC cbvDesc = new()
			{
				BufferLocation = m_sceneConstantBuffer!.GetGPUVirtualAddress(),
				SizeInBytes = sceneConstantBufferSize
			};

			D3D12_CPU_DESCRIPTOR_HANDLE cpuHandle = new(m_cbvSrvHeap!.GetCPUDescriptorHandleForHeapStart(), 1, m_cbvSrvDescriptorSize);

			for (uint n = 0; n < FrameCount; n++)
			{
				m_device!.CreateConstantBufferView(cbvDesc, cpuHandle);

				cbvDesc.BufferLocation += sceneConstantBufferSize;
				cpuHandle.Offset((int)m_cbvSrvDescriptorSize);
			}

			// Map and initialize the constant buffer. We don't unmap this until the
			// app closes. Keeping things mapped for the lifetime of the resource is okay.
			D3D12_RANGE readRange = new(0, 0); // We do not intend to read from this resource on the CPU.
			HRESULT.ThrowIfFailed(m_sceneConstantBuffer.Map(0, readRange, out m_pCbvDataBegin));
			Marshal.StructureToPtr(m_sceneConstantBufferData, m_pCbvDataBegin, false);
		}

		// Close the resource creation command list and execute it to begin the vertex buffer copy into
		// the default heap.
		HRESULT.ThrowIfFailed(commandList.Close());
		ID3D12CommandList[] ppCommandLists = [commandList];
		m_commandQueue!.ExecuteCommandLists(ppCommandLists);

		// Create synchronization objects and wait until assets have been uploaded to the GPU.
		{
			HRESULT.ThrowIfFailed(m_device!.CreateFence(m_fenceValues[m_frameIndex], D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE, out m_fence));
			m_fenceValues[m_frameIndex]++;

			// Create an event handle to use for frame synchronization.
			Win32Error.ThrowLastErrorIfInvalid(m_fenceEvent = CreateEvent());

			// Wait for the command list to execute before continuing.
			WaitForGpu();
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
		NAME_D3D12_OBJECT(m_commandQueue);

		// Describe the swap chain.
		// The resolution of the swap chain buffers will match the resolution of the window, enabling the
		// app to enter iFlip when in fullscreen mode. We will also keep a separate buffer that is not part
		// of the swap chain as an intermediate render target, whose resolution will control the rendering
		// resolution of the scene.
		// It is recommended to always use the tearing flag when it is available.
		DXGI_SWAP_CHAIN_DESC1 swapChainDesc = new()
		{
			BufferCount = FrameCount,
			Flags = TearingSupport ? DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING : 0,
			Width = (uint)Width,
			Height = (uint)Height,
			Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
			BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
			SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_DISCARD,
			SampleDesc = new(1, 0),
		};

		// Swap chain needs the queue so that it can force a flush on it.
		IDXGISwapChain1 swapChain = factory.CreateSwapChainForHwnd(m_commandQueue, Win32App!.Handle, swapChainDesc);

		if (TearingSupport)
		{
			// When tearing support is enabled we will handle ALT+Enter key presses in the
			// window message loop rather than let DXGI handle it by calling SetFullscreenState.
			factory.MakeWindowAssociation(Win32App!.Handle, DXGI_MWA.DXGI_MWA_NO_ALT_ENTER);
		}

		m_swapChain = (IDXGISwapChain3)swapChain;
		m_frameIndex = (int)m_swapChain.GetCurrentBackBufferIndex();

		// Create descriptor heaps.
		{
			// Describe and create a render target view (RTV) descriptor heap.
			D3D12_DESCRIPTOR_HEAP_DESC rtvHeapDesc = new()
			{
				NumDescriptors = FrameCount + 1, // + 1 for the intermediate render target.
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_NONE
			};
			m_rtvHeap = m_device!.CreateDescriptorHeap<ID3D12DescriptorHeap>(rtvHeapDesc);

			// Describe and create a constant buffer view (CBV) and shader resource view (SRV) descriptor heap.
			D3D12_DESCRIPTOR_HEAP_DESC cbvSrvHeapDesc = new()
			{
				NumDescriptors = FrameCount + 1, // One CBV per frame and one SRV for the intermediate render target.
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE
			};
			m_cbvSrvHeap = m_device!.CreateDescriptorHeap<ID3D12DescriptorHeap>(cbvSrvHeapDesc);

			m_rtvDescriptorSize = m_device!.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
			m_cbvSrvDescriptorSize = m_device.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
		}

		// Create command allocators for each frame.
		for (uint n = 0; n < FrameCount; n++)
		{
			m_device!.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, out m_sceneCommandAllocators[n]!).ThrowIfFailed();
			m_device!.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, out m_postCommandAllocators[n]!).ThrowIfFailed();
		}
	}

	// Set up appropriate views for the intermediate render target.
	private void LoadSceneResolutionDependentResources()
	{
		// Update resolutions shown in app title.
		UpdateTitle();

		// Set up the scene viewport and scissor rect to match the current scene rendering resolution.
		{
			m_sceneViewport.Width = (float)(m_resolutionOptions[m_resolutionIndex].Width);
			m_sceneViewport.Height = (float)(m_resolutionOptions[m_resolutionIndex].Height);

			m_sceneScissorRect.right = (int)(m_resolutionOptions[m_resolutionIndex].Width);
			m_sceneScissorRect.bottom = (int)(m_resolutionOptions[m_resolutionIndex].Height);
		}

		// Update post-process viewport and scissor rectangle.
		UpdatePostViewAndScissor();

		// Create RTV for the intermediate render target.
		{
			m_renderTargets![m_frameIndex]!.GetDesc(out var swapChainDesc);
			D3D12_CLEAR_VALUE clearValue = new(swapChainDesc.Format, ClearColor);
			var renderTargetDesc = D3D12_RESOURCE_DESC.Tex2D(swapChainDesc.Format, m_resolutionOptions[m_resolutionIndex].Width,
				m_resolutionOptions[m_resolutionIndex].Height, 1, 1, swapChainDesc.SampleDesc.Count, swapChainDesc.SampleDesc.Quality,
				D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET, D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_UNKNOWN, 0u);

			D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(), (int)FrameCount, m_rtvDescriptorSize);
			m_intermediateRenderTarget = m_device!.CreateCommittedResource<ID3D12Resource>(new D3D12_HEAP_PROPERTIES(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE, renderTargetDesc, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, clearValue);
			m_device!.CreateRenderTargetView(m_intermediateRenderTarget, default, rtvHandle);
			NAME_D3D12_OBJECT(m_intermediateRenderTarget);
		}

		// Create SRV for the intermediate render target.
		m_device.CreateShaderResourceView(m_intermediateRenderTarget, default, m_cbvSrvHeap!.GetCPUDescriptorHandleForHeapStart());
	}

	private void LoadSizeDependentResources()
	{
		UpdatePostViewAndScissor();

		// Create frame resources.
		{
			D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = m_rtvHeap!.GetCPUDescriptorHandleForHeapStart();

			// Create a RTV for each frame.
			for (uint n = 0; n < FrameCount; n++)
			{
				m_swapChain!.GetBuffer(n, out m_renderTargets![n]).ThrowIfFailed();
				m_device!.CreateRenderTargetView(m_renderTargets[n], default, rtvHandle);
				rtvHandle.Offset(1, m_rtvDescriptorSize);

				NAME_D3D12_OBJECT_INDEXED(m_renderTargets.WhereNotNull().ToList(), n);
			}
		}

		// Update resolutions shown in app title.
		UpdateTitle();

		// This is where you would create/resize intermediate render targets, depth stencils, or other resources dependent on the window size.
	}

	// Prepare to render the next frame.
	private void MoveToNextFrame()
	{
		// Schedule a Signal command in the queue.
		ulong currentFenceValue = m_fenceValues[m_frameIndex];
		m_commandQueue!.Signal(m_fence!, currentFenceValue).ThrowIfFailed();

		// Update the frame index.
		m_frameIndex = (int)m_swapChain!.GetCurrentBackBufferIndex();

		// If the next frame is not ready to be rendered yet, wait until it is ready.
		if (m_fence!.GetCompletedValue() < m_fenceValues[m_frameIndex])
		{
			m_fence.SetEventOnCompletion(m_fenceValues[m_frameIndex], m_fenceEvent!).ThrowIfFailed();
			m_fenceEvent!.Wait();
		}

		// Set the fence value for the next frame.
		m_fenceValues[m_frameIndex] = currentFenceValue + 1;
	}

	// Fill the command list with all the render commands and dependent state.
	private void PopulateCommandLists()
	{
		// Command list allocators can only be reset when the associated command lists have finished execution on the GPU; apps should use
		// fences to determine GPU execution progress.
		HRESULT.ThrowIfFailed(m_sceneCommandAllocators[m_frameIndex].Reset());
		HRESULT.ThrowIfFailed(m_postCommandAllocators[m_frameIndex].Reset());

		// However, when ExecuteCommandList() is called on a particular command list, that command list can then be reset at any time and
		// must be before re-recording.
		HRESULT.ThrowIfFailed(m_sceneCommandList!.Reset(m_sceneCommandAllocators[m_frameIndex], m_scenePipelineState));
		HRESULT.ThrowIfFailed(m_postCommandList!.Reset(m_postCommandAllocators[m_frameIndex], m_postPipelineState));

		// Populate m_sceneCommandList to render scene to intermediate render target.
		{
			// Set necessary state.
			m_sceneCommandList.SetGraphicsRootSignature(m_sceneRootSignature);

			m_sceneCommandList.SetDescriptorHeaps([m_cbvSrvHeap!]);

			D3D12_GPU_DESCRIPTOR_HANDLE cbvHandle = new(m_cbvSrvHeap!.GetGPUDescriptorHandleForHeapStart(), m_frameIndex + 1, m_cbvSrvDescriptorSize);
			m_sceneCommandList.SetGraphicsRootDescriptorTable(0, cbvHandle);
			m_sceneCommandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
			m_sceneCommandList.RSSetViewports([m_sceneViewport]);
			m_sceneCommandList.RSSetScissorRects([m_sceneScissorRect]);

			D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(), FrameCount, m_rtvDescriptorSize);
			m_sceneCommandList.OMSetRenderTargets([rtvHandle], false);

			// Record commands.
			m_sceneCommandList.ClearRenderTargetView(rtvHandle, ClearColor);
			m_sceneCommandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
			m_sceneCommandList.IASetVertexBuffers(0, [m_sceneVertexBufferView]);

			using (new PIXEvent(m_sceneCommandList, "Draw a thin rectangle"))
				m_sceneCommandList.DrawInstanced(4, 1, 0, 0);
		}

		HRESULT.ThrowIfFailed(m_sceneCommandList.Close());

		// Populate m_postCommandList to scale intermediate render target to screen.
		{
			// Set necessary state.
			m_postCommandList.SetGraphicsRootSignature(m_postRootSignature);

			m_postCommandList.SetDescriptorHeaps([m_cbvSrvHeap!]);

			// Indicate that the back buffer will be used as a render target and the intermediate render target will be used as a SRV.
			D3D12_RESOURCE_BARRIER[] barriers = [
				D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets![m_frameIndex]!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET),
				D3D12_RESOURCE_BARRIER.CreateTransition(m_intermediateRenderTarget!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE)
			];

			m_postCommandList.ResourceBarrier(barriers);

			m_postCommandList.SetGraphicsRootDescriptorTable(0, m_cbvSrvHeap!.GetGPUDescriptorHandleForHeapStart());
			m_postCommandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
			m_postCommandList.RSSetViewports([m_postViewport]);
			m_postCommandList.RSSetScissorRects([m_postScissorRect]);

			D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(), m_frameIndex, m_rtvDescriptorSize);
			m_postCommandList.OMSetRenderTargets([rtvHandle], false);

			// Record commands.
			m_postCommandList.ClearRenderTargetView(rtvHandle, LetterboxColor);
			m_postCommandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
			m_postCommandList.IASetVertexBuffers(0, [m_postVertexBufferView]);

			using (new PIXEvent(m_postCommandList, "Draw texture to screen."))
				m_postCommandList.DrawInstanced(4, 1, 0, 0);

			// Revert resource states back to original values.
			barriers = [
				D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets![m_frameIndex]!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT),
				D3D12_RESOURCE_BARRIER.CreateTransition(m_intermediateRenderTarget!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET)
			];

			m_postCommandList.ResourceBarrier(barriers);
		}

		HRESULT.ThrowIfFailed(m_postCommandList.Close());
	}

	// Release sample's D3D objects.
	private void ReleaseD3DResources()
	{
		m_fence = null;
		m_renderTargets?.Fill(null);
		m_renderTargets = null;
		m_commandQueue = null;
		m_swapChain = null;
		m_device = null;
		GC.Collect();
	}

	// Tears down D3D resources and reinitializes them.
	private void RestoreD3DResources()
	{
		// Give GPU a chance to finish its execution in progress.
		try { WaitForGpu(); }
		catch { } // Do nothing, currently attached adapter is unresponsive.
		ReleaseD3DResources();
		OnInit();
	}

	// Set up the screen viewport and scissor rect to match the current window size and scene rendering resolution.
	private void UpdatePostViewAndScissor()
	{
		float viewWidthRatio = (float)(m_resolutionOptions[m_resolutionIndex].Width) / Width;
		float viewHeightRatio = (float)(m_resolutionOptions[m_resolutionIndex].Height) / Height;

		float x = 1.0f;
		float y = 1.0f;

		if (viewWidthRatio < viewHeightRatio)
		{
			// The scaled image's height will fit to the viewport's height and its width will be smaller than the viewport's width.
			x = viewWidthRatio / viewHeightRatio;
		}
		else
		{
			// The scaled image's width will fit to the viewport's width and its height may be smaller than the viewport's height.
			y = viewHeightRatio / viewWidthRatio;
		}

		m_postViewport.TopLeftX = Width * (1.0f - x) / 2.0f;
		m_postViewport.TopLeftY = Height * (1.0f - y) / 2.0f;
		m_postViewport.Width = x * Width;
		m_postViewport.Height = y * Height;

		m_postScissorRect.left = (int)(m_postViewport.TopLeftX);
		m_postScissorRect.right = (int)(m_postViewport.TopLeftX + m_postViewport.Width);
		m_postScissorRect.top = (int)(m_postViewport.TopLeftY);
		m_postScissorRect.bottom = (int)(m_postViewport.TopLeftY + m_postViewport.Height);
	}

	private void UpdateTitle() =>
		// Update resolutions shown in app title.
		SetCustomWindowText($"( {m_resolutionOptions[m_resolutionIndex].Width} x {m_resolutionOptions[m_resolutionIndex].Height} ) scaled to ( {Width} x {Height} )");

	// Wait for pending GPU work to complete.
	private void WaitForGpu()
	{
		// Schedule a Signal command in the queue.
		m_commandQueue!.Signal(m_fence!, m_fenceValues[m_frameIndex]).ThrowIfFailed();

		// Wait until the fence has been processed.
		m_fence!.SetEventOnCompletion(m_fenceValues[m_frameIndex], m_fenceEvent!).ThrowIfFailed();
		m_fenceEvent!.Wait();

		// Increment the fence value for the current frame.
		m_fenceValues[m_frameIndex]++;
	}

	private struct PostVertex(float x, float y, float z, float w, float x2, float y2)
	{
		public D2D_VECTOR_4F position = new(x, y, z, w);
		public D2D_VECTOR_2F uv = new(x2, y2);
	}

	private struct Resolution(uint w, uint h)
	{
		public uint Height = h;
		public uint Width = w;
	}

	[StructLayout(LayoutKind.Sequential, Size = 256)]
	private struct SceneConstantBuffer
	{
		public XMMATRIX transform;
		public XMVECTOR offset;
	}

	private struct SceneVertex(float r, float g, float b, float a, float x, float y, float z)
	{
		public D2D_VECTOR_4F color = new(r,b,g,a);
		public D2D_VECTOR_3F position = new(x,y,z);
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Vertex(float r, float g, float b, float a, float x, float y, float z)
	{
		public D3DCOLORVALUE color = new(r, b, g, a);
		public D2D_VECTOR_3F position = new(x, y, z);
	}
}

internal static class Ext
{
	public static void Fill<T>(this IList<T> l, T? value = default)
	{
		for (int i = 0; i < l.Count; i++)
			l[i] = value!;
	}
}