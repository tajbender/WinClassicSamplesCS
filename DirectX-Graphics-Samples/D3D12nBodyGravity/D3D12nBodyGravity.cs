using static Vanara.PInvoke.DirectXMath;

internal partial class D3D12nBodyGravity(int width, int height, string name) : DXSample(width, height, name)
{
	private const int FrameCount = 2;
	private const uint ParticleCount = 10000;
	private const float ParticleSpread = 400.0f;
	private const uint ThreadCount = 1;
	private static Random m_random = new();
	private readonly SimpleCamera m_camera = new(new(0f, 0f, 1500f));
	private readonly ID3D12CommandAllocator?[] m_commandAllocators = new ID3D12CommandAllocator[FrameCount];
	private readonly ID3D12CommandAllocator[] m_computeAllocator = new ID3D12CommandAllocator[ThreadCount];
	private readonly ID3D12GraphicsCommandList8[] m_computeCommandList = new ID3D12GraphicsCommandList8[ThreadCount];
	private readonly ID3D12CommandQueue[] m_computeCommandQueue = new ID3D12CommandQueue[ThreadCount];
	private readonly ulong[] m_frameFenceValues = new ulong[FrameCount];
	private readonly ID3D12Resource[] m_particleBuffer0 = new ID3D12Resource[ThreadCount];
	private readonly ID3D12Resource[] m_particleBuffer0Upload = new ID3D12Resource[ThreadCount];
	private readonly ID3D12Resource[] m_particleBuffer1 = new ID3D12Resource[ThreadCount];
	private readonly ID3D12Resource[] m_particleBuffer1Upload = new ID3D12Resource[ThreadCount];
	private IntPtr m_pConstantBufferGSData;
	private readonly ulong[] m_renderContextFenceValues = new ulong[ThreadCount];
	private readonly ID3D12Resource[] m_renderTargets = new ID3D12Resource[FrameCount];
	private readonly uint[] m_srvIndex = new uint[ThreadCount];
	private readonly ThreadData[] m_threadData = new ThreadData[ThreadCount];
	private readonly SafeEventHandle[] m_threadFenceEvents = new SafeEventHandle[ThreadCount];
	private readonly ID3D12Fence[] m_threadFences = new ID3D12Fence[ThreadCount];
	private readonly ulong[] m_threadFenceValues = new ulong[ThreadCount];
	private readonly SafeHTHREAD[] m_threadHandles = new SafeHTHREAD[ThreadCount];
	private bool m_bIsEnhancedBarriersEnabled;
	private ID3D12GraphicsCommandList8? m_commandList;
	private ID3D12CommandQueue? m_commandQueue;
	private ID3D12RootSignature? m_computeRootSignature;
	private ID3D12PipelineState? m_computeState;
	private ID3D12Resource? m_constantBufferCS;
	private ID3D12Resource? m_constantBufferGS;
	private ID3D12Device10? m_device;
	private uint m_frameIndex;
	private uint m_heightInstances = 1;
	private ID3D12PipelineState? m_pipelineState;
	private ID3D12Fence? m_renderContextFence;
	private SafeEventHandle m_renderContextFenceEvent = SafeEventHandle.Null;
	private ulong m_renderContextFenceValue;
	private ID3D12RootSignature? m_rootSignature;
	private uint m_rtvDescriptorSize;
	private ID3D12DescriptorHeap? m_rtvHeap;
	private RECT m_scissorRect = new(0, 0, width, height);
	private uint m_srvUavDescriptorSize;
	private ID3D12DescriptorHeap? m_srvUavHeap;
	private IDXGISwapChain3? m_swapChain;
	private SafeEventHandle m_swapChainEvent = SafeEventHandle.Null;
	private long m_terminating;
	private readonly StepTimer m_timer = new();
	private ID3D12Resource? m_vertexBuffer;
	private ID3D12Resource? m_vertexBufferUpload;
	private D3D12_VERTEX_BUFFER_VIEW m_vertexBufferView;
	private D3D12_VIEWPORT m_viewport = new(0f, 0f, width, height);
	private uint m_widthInstances = 1;

	private enum ComputeRootParameters : uint
	{
		ComputeRootCBV = 0,
		ComputeRootSRVTable,
		ComputeRootUAVTable,
		ComputeRootParametersCount
	};

	// Indices of shader resources in the descriptor heap.
	private enum DescriptorHeapIndex : uint
	{
		UavParticlePosVelo0 = 0,
		UavParticlePosVelo1 = UavParticlePosVelo0 + ThreadCount,
		SrvParticlePosVelo0 = UavParticlePosVelo1 + ThreadCount,
		SrvParticlePosVelo1 = SrvParticlePosVelo0 + ThreadCount,
		DescriptorCount = SrvParticlePosVelo1 + ThreadCount
	};

	// Indices of the root signature parameters.
	private enum GraphicsRootParameters : uint
	{
		GraphicsRootCBV = 0,
		GraphicsRootSRVTable,
		GraphicsRootParametersCount
	};

	public override void OnDestroy()
	{
		// Notify the compute threads that the app is shutting down.
		_ = Interlocked.Exchange(ref m_terminating, 1);
		if (m_threadHandles.All(h => h is not null && !h.IsInvalid))
			_ = WaitForMultipleObjects(m_threadHandles, true, INFINITE);

		// Ensure that the GPU is no longer referencing resources that are about to be cleaned up by the destructor.
		if (m_renderContextFence is not null)
			WaitForRenderContext();

		// Close handles to fence events and threads.
		m_renderContextFenceEvent.Dispose();
		for (int n = 0; n < ThreadCount; n++)
		{
			m_threadHandles[n]?.Dispose();
			m_threadFenceEvents[n]?.Dispose();
		}
	}

	public override void OnInit()
	{
		// From ctor
		for (int i = 0; i < ThreadCount; i++)
			m_renderContextFenceValues[i] = m_threadFenceValues[i] = 0;

		float sqRootNumAsyncContexts = (float)Math.Sqrt(ThreadCount);
		m_heightInstances = (uint)Math.Ceiling(sqRootNumAsyncContexts);
		m_widthInstances = (uint)Math.Ceiling(sqRootNumAsyncContexts);

		if (m_widthInstances * (m_heightInstances - 1) >= ThreadCount)
			m_heightInstances--;

		HRESULT.ThrowIfFailed(DXGIDeclareAdapterRemovalSupport());

		m_camera.SetMoveSpeed(250f);

		LoadPipeline();
		LoadAssets();
		CreateAsyncContexts();
	}

	public override void OnKeyDown(VK key) => m_camera!.OnKeyDown(key);

	public override void OnKeyUp(VK key) => m_camera!.OnKeyUp(key);

	public override void OnRender()
	{
		try
		{
			// Let the compute thread know that a new frame is being rendered.
			for (int n = 0; n < ThreadCount; n++)
			{
				_ = Interlocked.Exchange(ref m_renderContextFenceValues[n], m_renderContextFenceValue);
			}

			// Compute work must be completed before the frame can render or else the SRV will be in the wrong state.
			for (uint n = 0; n < ThreadCount; n++)
			{
				ulong threadFenceValue = Interlocked.Read(ref m_threadFenceValues[n]);
				if (m_threadFences[n].GetCompletedValue() < threadFenceValue)
				{
					// Instruct the rendering command queue to wait for the current compute work to complete.
					HRESULT.ThrowIfFailed(m_commandQueue!.Wait(m_threadFences[n], threadFenceValue));
				}
			}

			using (PIXEvent pix = new(m_commandQueue!, "Render"))
			{
				// Record all the commands we need to render the scene into the command list.
				PopulateCommandList();

				// Execute the command list.
				m_commandQueue!.ExecuteCommandLists([m_commandList!]);
			}

			// Present the frame.
			HRESULT.ThrowIfFailed(m_swapChain!.Present(1, 0));

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

	public override void OnUpdate()
	{
		// Wait for the previous Present to complete.
		_ = WaitForSingleObjectEx(m_swapChainEvent, 100, false);

		m_timer.Tick(default);
		m_camera.Update((float)m_timer.ElapsedSeconds);

		ConstantBufferGS constantBufferGS = default;
		XMStoreFloat4x4(out constantBufferGS.worldViewProjection, m_camera.GetViewMatrix() * m_camera.GetProjectionMatrix(0.8f, m_aspectRatio, 1.0f, 5000.0f));
		XMStoreFloat4x4(out constantBufferGS.inverseView, DirectXMath.XMMatrixInverse(m_camera.GetViewMatrix(), out _));

		if (m_pConstantBufferGSData != IntPtr.Zero)
		{
			IntPtr destination = m_pConstantBufferGSData.Offset(Marshal.SizeOf<ConstantBufferGS>() * m_frameIndex);
			_ = destination.Write(constantBufferGS);
		}
	}

	private static void LoadParticles([Out] Particle[] pParticles, uint offset, in XMFLOAT3 center, in XMFLOAT4 velocity, float spread, uint numParticles)
	{
		m_random = new(0);
		for (uint i = offset; i < numParticles + offset; i++)
		{
			XMFLOAT3 delta = new(spread, spread, spread);

			while (DirectXMath.XMVectorGetX(DirectXMath.XMVector3LengthSq(XMLoadFloat3(delta))) > spread * spread)
			{
				delta.x = RandomPercent() * spread;
				delta.y = RandomPercent() * spread;
				delta.z = RandomPercent() * spread;
			}

			pParticles[i].position.x = center.x + delta.x;
			pParticles[i].position.y = center.y + delta.y;
			pParticles[i].position.z = center.z + delta.z;
			pParticles[i].position.w = 10000.0f * 10000.0f;

			pParticles[i].velocity = velocity;
		}
	}

	// Random percent value, from -1 to 1.
	private static float RandomPercent() => (m_random.Next() % 10000 - 5000) / 5000.0f;

	private uint ThreadProc(IntPtr pData)
	{
		ThreadData data = m_threadData[pData.ToInt32()];
		return data.pContext!.AsyncComputeThreadProc(data.threadIndex);
	}

	private uint AsyncComputeThreadProc(int threadIndex)
	{
		ID3D12CommandQueue pCommandQueue = m_computeCommandQueue[threadIndex];
		ID3D12CommandAllocator pCommandAllocator = m_computeAllocator[threadIndex];
		ID3D12GraphicsCommandList pCommandList = m_computeCommandList[threadIndex];
		ID3D12Fence pFence = m_threadFences[threadIndex];

		while (0 == Interlocked.Read(ref m_terminating))
		{
			// Run the particle simulation.
			Simulate(threadIndex);

			// Close and execute the command list.
			HRESULT.ThrowIfFailed(pCommandList.Close());
			ID3D12CommandList[] ppCommandLists = [pCommandList];

			using (PIXEvent pix = new(pCommandQueue, "Thread {0}: Iterate on the particle simulation", threadIndex))
				pCommandQueue.ExecuteCommandLists(1, ppCommandLists);

			// Wait for the compute shader to complete the simulation.
			ulong threadFenceValue = Interlocked.Increment(ref m_threadFenceValues[threadIndex]);
			HRESULT.ThrowIfFailed(pCommandQueue.Signal(pFence, threadFenceValue));
			HRESULT.ThrowIfFailed(pFence.SetEventOnCompletion(threadFenceValue, m_threadFenceEvents[threadIndex]));
			WaitForSingleObject(m_threadFenceEvents[threadIndex], INFINITE);

			// Wait for the render thread to be done with the SRV so that the next frame in the simulation can run.
			ulong renderContextFenceValue = Interlocked.Read(ref m_renderContextFenceValues[threadIndex]);
			if (m_renderContextFence!.GetCompletedValue() < renderContextFenceValue)
			{
				HRESULT.ThrowIfFailed(pCommandQueue.Wait(m_renderContextFence, renderContextFenceValue));
				Interlocked.Exchange(ref m_renderContextFenceValues[threadIndex], 0);
			}

			// Swap the indices to the SRV and UAV.
			m_srvIndex[threadIndex] = 1 - m_srvIndex[threadIndex];

			// Prepare for the next frame.
			HRESULT.ThrowIfFailed(pCommandAllocator.Reset());
			HRESULT.ThrowIfFailed(pCommandList.Reset(pCommandAllocator, m_computeState));
		}

		return 0;
	}

	private void CreateAsyncContexts()
	{
		for (int threadIndex = 0; threadIndex < ThreadCount; ++threadIndex)
		{
			// Create compute resources.
			D3D12_COMMAND_QUEUE_DESC queueDesc = new() { Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_COMPUTE };
			HRESULT.ThrowIfFailed(m_device!.CreateCommandQueue(queueDesc, out m_computeCommandQueue[threadIndex]!));
			HRESULT.ThrowIfFailed(m_device!.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_COMPUTE, out m_computeAllocator[threadIndex]!));
			HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_COMPUTE, m_computeAllocator[threadIndex], default, out m_computeCommandList[threadIndex]!));
			HRESULT.ThrowIfFailed(m_device!.CreateFence(0, D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_SHARED, out m_threadFences[threadIndex]!));

			m_threadFenceEvents[threadIndex] = CreateEvent(default, false, false, default);
			if (m_threadFenceEvents[threadIndex].IsInvalid)
				Win32Error.ThrowLastError();

			m_threadData[threadIndex] = new() { pContext = this, threadIndex = threadIndex };

			m_threadHandles[threadIndex] = CreateThread(default,
				0,
				ThreadProc,
				threadIndex,
				CREATE_THREAD_FLAGS.CREATE_SUSPENDED,
				out _);

			_ = ResumeThread(m_threadHandles[threadIndex]);
		}
	}

	// Create the position and velocity buffer shader resources.
	private void CreateParticleBuffers()
	{
		// Initialize the data in the buffers.
		Particle[] data = new Particle[(int)ParticleCount];
		uint dataSize = ParticleCount * (uint)Marshal.SizeOf<Particle>();

		// Split the particles into two groups.
		float centerSpread = ParticleSpread * 0.50f;
		LoadParticles(data, 0, new XMFLOAT3(centerSpread, 0, 0), new XMFLOAT4(0, 0, -20, 1 / 100000000.0f), ParticleSpread, ParticleCount / 2);
		LoadParticles(data, ParticleCount / 2, new XMFLOAT3(-centerSpread, 0, 0), new XMFLOAT4(0, 0, 20, 1 / 100000000.0f), ParticleSpread, ParticleCount / 2);

		D3D12_HEAP_PROPERTIES defaultHeapProperties = new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT);
		D3D12_HEAP_PROPERTIES uploadHeapProperties = new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD);
		D3D12_RESOURCE_DESC bufferDesc = D3D12_RESOURCE_DESC.Buffer(dataSize, D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS);
		D3D12_RESOURCE_DESC uploadBufferDesc = D3D12_RESOURCE_DESC.Buffer(dataSize);

		for (uint index = 0; index < ThreadCount; index++)
		{
			// Create two buffers in the GPU, each with a copy of the particles data. The compute shader will update one of them while the
			// rendering thread renders the other. When rendering completes, the threads will swap which buffer they work on.

			if (m_bIsEnhancedBarriersEnabled)
			{
				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(defaultHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					new D3D12_RESOURCE_DESC1(bufferDesc),
					D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
					default,
					default,
					default,
					out m_particleBuffer0[index]!));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(defaultHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					new D3D12_RESOURCE_DESC1(bufferDesc),
					D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
					default,
					default,
					default,
					out m_particleBuffer1[index]!));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(uploadHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					new D3D12_RESOURCE_DESC1(uploadBufferDesc),
					D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
					default,
					default,
					default,
					out m_particleBuffer0Upload[index]!));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(uploadHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					new D3D12_RESOURCE_DESC1(uploadBufferDesc),
					D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
					default,
					default,
					default,
					out m_particleBuffer1Upload[index]!));
			}
			else
			{
				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(defaultHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					bufferDesc,
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
					default,
					out m_particleBuffer0[index]!));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(defaultHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					bufferDesc,
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
					default,
					out m_particleBuffer1[index]!));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(uploadHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					uploadBufferDesc,
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
					default,
					out m_particleBuffer0Upload[index]!));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(uploadHeapProperties,
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					uploadBufferDesc,
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
					default,
					out m_particleBuffer1Upload[index]!));
			}

			NAME_D3D12_OBJECT_INDEXED(m_particleBuffer0, index);
			NAME_D3D12_OBJECT_INDEXED(m_particleBuffer1, index);

			D3D12_SUBRESOURCE_DATA particleData = new()
			{
				pData = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0),
				RowPitch = dataSize,
				SlicePitch = dataSize
			};

			_ = UpdateSubresources(m_commandList!, m_particleBuffer0[index], m_particleBuffer0Upload[index], 0, 0, 1, [particleData]);
			_ = UpdateSubresources(m_commandList!, m_particleBuffer1[index], m_particleBuffer1Upload[index], 0, 0, 1, [particleData]);

			if (m_bIsEnhancedBarriersEnabled)
			{
				D3D12_BUFFER_BARRIER[] ParticleBufBarriers =
				[
					new(D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_COPY, // SyncBefore
						D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_NON_PIXEL_SHADING, // SyncAfter
						D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_COPY_DEST, // AccessBefore
						D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_SHADER_RESOURCE, // AccessAfter
						m_particleBuffer0[index]
					),
					new(D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_COPY, // SyncBefore
						D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_NON_PIXEL_SHADING, // SyncAfter
						D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_COPY_DEST, // AccessBefore
						D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_SHADER_RESOURCE, // AccessAfter
						m_particleBuffer1[index]
					)
				];
				D3D12_BARRIER_GROUP[] ParticleBufBarrierGroups = [new(ParticleBufBarriers, out _)];
				m_commandList!.Barrier(ParticleBufBarrierGroups);
			}
			else
			{
				m_commandList!.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_particleBuffer0[index], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE)]);
				m_commandList!.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_particleBuffer1[index], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE)]);
			}

			D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = new()
			{
				Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
				Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
				ViewDimension = D3D12_SRV_DIMENSION.D3D12_SRV_DIMENSION_BUFFER,
				Buffer = new()
				{
					FirstElement = 0,
					NumElements = ParticleCount,
					StructureByteStride = (uint)Marshal.SizeOf<Particle>(),
					Flags = D3D12_BUFFER_SRV_FLAGS.D3D12_BUFFER_SRV_FLAG_NONE
				}
			};

			D3D12_CPU_DESCRIPTOR_HANDLE srvHandle0 = new(m_srvUavHeap!.GetCPUDescriptorHandleForHeapStart(), (int)((uint)DescriptorHeapIndex.SrvParticlePosVelo0 + index), m_srvUavDescriptorSize);
			D3D12_CPU_DESCRIPTOR_HANDLE srvHandle1 = new(m_srvUavHeap!.GetCPUDescriptorHandleForHeapStart(), (int)((uint)DescriptorHeapIndex.SrvParticlePosVelo1 + index), m_srvUavDescriptorSize);
			m_device!.CreateShaderResourceView(m_particleBuffer0[index], srvDesc, srvHandle0);
			m_device!.CreateShaderResourceView(m_particleBuffer1[index], srvDesc, srvHandle1);

			D3D12_UNORDERED_ACCESS_VIEW_DESC uavDesc = new()
			{
				Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
				ViewDimension = D3D12_UAV_DIMENSION.D3D12_UAV_DIMENSION_BUFFER,
				Buffer = new()
				{
					FirstElement = 0,
					NumElements = ParticleCount,
					StructureByteStride = (uint)Marshal.SizeOf<Particle>(),
					CounterOffsetInBytes = 0,
					Flags = D3D12_BUFFER_UAV_FLAGS.D3D12_BUFFER_UAV_FLAG_NONE
				}
			};

			D3D12_CPU_DESCRIPTOR_HANDLE uavHandle0 = new(m_srvUavHeap!.GetCPUDescriptorHandleForHeapStart(), (int)((uint)DescriptorHeapIndex.UavParticlePosVelo0 + index), m_srvUavDescriptorSize);
			D3D12_CPU_DESCRIPTOR_HANDLE uavHandle1 = new(m_srvUavHeap!.GetCPUDescriptorHandleForHeapStart(), (int)((uint)DescriptorHeapIndex.UavParticlePosVelo1 + index), m_srvUavDescriptorSize);
			m_device!.CreateUnorderedAccessView(m_particleBuffer0[index], default, uavDesc, uavHandle0);
			m_device!.CreateUnorderedAccessView(m_particleBuffer1[index], default, uavDesc, uavHandle1);
		}
	}

	// Create the particle vertex buffer.
	private void CreateVertexBuffer()
	{
		List<ParticleVertex> vertices = [];
		for (uint i = 0; i < ParticleCount; i++)
			vertices.Add(new() { color = new(1.0f, 1.0f, 0.2f, 1.0f) });

		uint bufferSize = ParticleCount * (uint)Marshal.SizeOf<ParticleVertex>();

		if (m_bIsEnhancedBarriersEnabled)
		{
			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC1.Buffer(bufferSize),
				D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
				default,
				default,
				default,
				out m_vertexBuffer));

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC1.Buffer(bufferSize),
				D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
				default,
				default,
				default,
				out m_vertexBufferUpload));
		}
		else
		{
			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(bufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
				default,
				out m_vertexBuffer));

			HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
				D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
				D3D12_RESOURCE_DESC.Buffer(bufferSize),
				D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
				default,
				out m_vertexBufferUpload));
		}

		NAME_D3D12_OBJECT(m_vertexBuffer!);

		using var pVertexData = SafeCoTaskMemHandle.CreateFromList(vertices);
		D3D12_SUBRESOURCE_DATA vertexData = new()
		{
			pData = pVertexData,
			RowPitch = bufferSize,
			SlicePitch = bufferSize
		};

		_ = UpdateSubresources(m_commandList!, m_vertexBuffer!, m_vertexBufferUpload!, 0, 0, 1, [vertexData]);

		if (m_bIsEnhancedBarriersEnabled)
		{
			D3D12_BUFFER_BARRIER[] VertexBufBarriers = [
				new(D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_COPY, // SyncBefore
					D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_VERTEX_SHADING, // SyncAfter
					D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_COPY_DEST, // AccessBefore
					D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_VERTEX_BUFFER, // AccessAfter
					m_vertexBuffer!)];
			D3D12_BARRIER_GROUP[] VertexBufBarrierGroups = [new(VertexBufBarriers, out SafeAllocatedMemoryHandle? vbbg)];
			m_commandList!.Barrier(VertexBufBarrierGroups);
		}
		else
		{
			m_commandList!.ResourceBarrier([D3D12_RESOURCE_BARRIER.CreateTransition(m_vertexBuffer!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER)]);
		}

		m_vertexBufferView.BufferLocation = m_vertexBuffer!.GetGPUVirtualAddress();
		m_vertexBufferView.SizeInBytes = bufferSize;
		m_vertexBufferView.StrideInBytes = (uint)Marshal.SizeOf<ParticleVertex>();
	}

	// Load the sample assets.
	private void LoadAssets()
	{
		// Create the root signatures.
		{
			// This is the highest version the sample supports. If CheckFeatureSupport succeeds, the HighestVersion returned will not be
			// greater than this.
			D3D12_FEATURE_DATA_ROOT_SIGNATURE featureData = new() { HighestVersion = D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1_1 };
			if (m_device!.CheckFeatureSupport(featureData).Failed)
				featureData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1_0;

			// Graphics root signature.
			{
				D3D12_DESCRIPTOR_RANGE1[] ranges = [new(D3D12_DESCRIPTOR_RANGE_TYPE.D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 0, 0, D3D12_DESCRIPTOR_RANGE_FLAGS.D3D12_DESCRIPTOR_RANGE_FLAG_DATA_STATIC)];

				D3D12_ROOT_PARAMETER1[] rootParameters = [
					D3D12_ROOT_PARAMETER1.InitAsConstantBufferView(0, 0, D3D12_ROOT_DESCRIPTOR_FLAGS.D3D12_ROOT_DESCRIPTOR_FLAG_DATA_STATIC, D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_ALL),
					D3D12_ROOT_PARAMETER1.InitAsDescriptorTable(ranges, D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_VERTEX, out SafeAllocatedMemoryHandle? rp1) ];

				D3D12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc = new(new D3D12_ROOT_SIGNATURE_DESC1(rootParameters, null, D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT, out SafeAllocatedMemoryHandle? rsd));

				HRESULT.ThrowIfFailed(D3DX12SerializeVersionedRootSignature(rootSignatureDesc, featureData.HighestVersion, out ID3DBlob? signature, out _));
				m_rootSignature = m_device!.CreateRootSignature<ID3D12RootSignature>(0, signature!);
				NAME_D3D12_OBJECT(m_rootSignature);
			}

			// Compute root signature.
			{
				D3D12_DESCRIPTOR_RANGE1[] ranges = [
					new(D3D12_DESCRIPTOR_RANGE_TYPE.D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 0, 0, D3D12_DESCRIPTOR_RANGE_FLAGS.D3D12_DESCRIPTOR_RANGE_FLAG_DESCRIPTORS_VOLATILE),
					new(D3D12_DESCRIPTOR_RANGE_TYPE.D3D12_DESCRIPTOR_RANGE_TYPE_UAV, 1, 0, 0, D3D12_DESCRIPTOR_RANGE_FLAGS.D3D12_DESCRIPTOR_RANGE_FLAG_DATA_VOLATILE) ];

				D3D12_ROOT_PARAMETER1[] rootParameters = [
					D3D12_ROOT_PARAMETER1.InitAsConstantBufferView(0, 0, D3D12_ROOT_DESCRIPTOR_FLAGS.D3D12_ROOT_DESCRIPTOR_FLAG_DATA_STATIC, D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_ALL),
					D3D12_ROOT_PARAMETER1.InitAsDescriptorTable([ranges[0]], D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_ALL, out SafeAllocatedMemoryHandle? dt0),
					D3D12_ROOT_PARAMETER1.InitAsDescriptorTable([ranges[1]], D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_ALL, out SafeAllocatedMemoryHandle? dt1) ];

				D3D12_VERSIONED_ROOT_SIGNATURE_DESC computeRootSignatureDesc = new(new D3D12_ROOT_SIGNATURE_DESC1(rootParameters, null, 0, out SafeAllocatedMemoryHandle? rsd));

				HRESULT.ThrowIfFailed(D3DX12SerializeVersionedRootSignature(computeRootSignatureDesc, featureData.HighestVersion, out ID3DBlob? signature, out _));
				m_computeRootSignature = m_device!.CreateRootSignature<ID3D12RootSignature>(0, signature!);
				NAME_D3D12_OBJECT(m_computeRootSignature);
			}
		}

		// Create the pipeline states, which includes compiling and loading shaders.
		{
#if DEBUG
		    // Enable better shader debugging with the graphics debugging tools.
	        D3DCOMPILE compileFlags = D3DCOMPILE.D3DCOMPILE_DEBUG | D3DCOMPILE.D3DCOMPILE_SKIP_OPTIMIZATION;
#else
			D3DCOMPILE compileFlags = 0;
#endif

			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("ParticleDraw.hlsl"), default, null, "VSParticleDraw", "vs_5_0", compileFlags, 0, out var vertexShader, default));
			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("ParticleDraw.hlsl"), default, null, "GSParticleDraw", "gs_5_0", compileFlags, 0, out var geometryShader, default));
			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("ParticleDraw.hlsl"), default, null, "PSParticleDraw", "ps_5_0", compileFlags, 0, out var pixelShader, default));
			HRESULT.ThrowIfFailed(D3DCompileFromFile(GetAssetFullPath("NBodyGravityCS.hlsl"), default, null, "CSMain", "cs_5_0", compileFlags, 0, out var computeShader, default));

			D3D12_INPUT_ELEMENT_DESC[] inputElementDescs = [new("COLOR", DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 0, D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0)];

			// Describe the blend and depth states.
			D3D12_BLEND_DESC blendDesc = new();
			blendDesc._RenderTarget[0].BlendEnable = true;
			blendDesc._RenderTarget[0].SrcBlend = D3D12_BLEND.D3D12_BLEND_SRC_ALPHA;
			blendDesc._RenderTarget[0].DestBlend = D3D12_BLEND.D3D12_BLEND_ONE;
			blendDesc._RenderTarget[0].SrcBlendAlpha = D3D12_BLEND.D3D12_BLEND_ZERO;
			blendDesc._RenderTarget[0].DestBlendAlpha = D3D12_BLEND.D3D12_BLEND_ZERO;

			D3D12_DEPTH_STENCIL_DESC depthStencilDesc = new() { DepthEnable = false, DepthWriteMask = D3D12_DEPTH_WRITE_MASK.D3D12_DEPTH_WRITE_MASK_ZERO };

			// Describe and create the graphics pipeline state object (PSO).
			D3D12_GRAPHICS_PIPELINE_STATE_DESC psoDesc = new()
			{
				InputLayout = new(inputElementDescs, out SafeAllocatedMemoryHandle? pelem),
				pRootSignature = m_rootSignature,
				VS = new(vertexShader),
				GS = new(geometryShader),
				PS = new(pixelShader),
				RasterizerState = new(),
				BlendState = blendDesc,
				DepthStencilState = depthStencilDesc,
				SampleMask = uint.MaxValue,
				PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_POINT,
				DSVFormat = DXGI_FORMAT.DXGI_FORMAT_D24_UNORM_S8_UINT,
				SampleDesc = new() { Count = 1 }
			};
			psoDesc.SetRTVFormats([DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM]);

			HRESULT.ThrowIfFailed(m_device!.CreateGraphicsPipelineState(psoDesc, out m_pipelineState));
			NAME_D3D12_OBJECT(m_pipelineState!);

			// Describe and create the compute pipeline state object (PSO).
			D3D12_COMPUTE_PIPELINE_STATE_DESC computePsoDesc = new()
			{
				pRootSignature = m_computeRootSignature,
				CS = new(computeShader)
			};

			HRESULT.ThrowIfFailed(m_device!.CreateComputePipelineState(computePsoDesc, out m_computeState));
			NAME_D3D12_OBJECT(m_computeState!);
		}

		// Create the command list.
		HRESULT.ThrowIfFailed(m_device!.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, m_commandAllocators[m_frameIndex]!, m_pipelineState, out m_commandList));
		NAME_D3D12_OBJECT(m_commandList!);

		CreateVertexBuffer();
		CreateParticleBuffers();

		// Note: ComPtr's are CPU objects but this resource needs to stay in scope until the command list that references it has finished
		// executing on the GPU. We will flush the GPU at the end of this method to ensure the resource is not prematurely destroyed.
		ID3D12Resource? constantBufferCSUpload;

		// Create the compute shader's constant buffer.
		{
			uint bufferSize = (uint)Marshal.SizeOf<ConstantBufferCS>();

			if (m_bIsEnhancedBarriersEnabled)
			{
				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					D3D12_RESOURCE_DESC1.Buffer(bufferSize),
					D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
					default,
					default,
					default,
					out m_constantBufferCS));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					D3D12_RESOURCE_DESC1.Buffer(bufferSize),
					D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
					default,
					default,
					default,
					out constantBufferCSUpload));
			}
			else
			{
				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT),
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					D3D12_RESOURCE_DESC.Buffer(bufferSize),
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
					default,
					out m_constantBufferCS));

				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					D3D12_RESOURCE_DESC.Buffer(bufferSize),
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
					default,
					out constantBufferCSUpload));
			}

			NAME_D3D12_OBJECT(m_constantBufferCS!);

			unsafe
			{
				ConstantBufferCS constantBufferCS = new();
				constantBufferCS.param[0] = ParticleCount;
				constantBufferCS.param[1] = (uint)Math.Ceiling(ParticleCount / 128.0f);
				constantBufferCS.paramf[0] = 0.1f;
				constantBufferCS.paramf[1] = 1.0f;

				D3D12_SUBRESOURCE_DATA computeCBData = new()
				{
					pData = (IntPtr)(void*)&constantBufferCS,
					RowPitch = bufferSize,
					SlicePitch = bufferSize
				};

				_ = UpdateSubresources(m_commandList!, m_constantBufferCS!, constantBufferCSUpload!, 0, 0, 1, [computeCBData]);
			}

			if (m_bIsEnhancedBarriersEnabled)
			{
				D3D12_BUFFER_BARRIER[] ComputeBufBarriers = [new(
					D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_COPY,
					D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_COMPUTE_SHADING,
					D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_COPY_DEST,
					D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_CONSTANT_BUFFER,
					m_constantBufferCS!)];
				D3D12_BARRIER_GROUP[] ComputeBufBarrierGroups = [new(ComputeBufBarriers, out SafeAllocatedMemoryHandle? cbbg)];
				m_commandList!.Barrier(ComputeBufBarrierGroups);
			}
			else
			{
				m_commandList!.ResourceBarrier([D3D12_RESOURCE_BARRIER.CreateTransition(m_constantBufferCS!, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER)]);
			}
		}

		// Create the geometry shader's constant buffer.
		{
			uint constantBufferGSSize = (uint)Marshal.SizeOf<ConstantBufferGS>() * FrameCount;

			if (m_bIsEnhancedBarriersEnabled)
			{
				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource3(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					D3D12_RESOURCE_DESC1.Buffer(constantBufferGSSize),
					D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_UNDEFINED,
					default,
					default,
					default,
					out m_constantBufferGS));
			}
			else
			{
				HRESULT.ThrowIfFailed(m_device!.CreateCommittedResource(new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD),
					D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
					D3D12_RESOURCE_DESC.Buffer(constantBufferGSSize),
					D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
					default,
					out m_constantBufferGS));
			}

			NAME_D3D12_OBJECT(m_constantBufferGS!);

			unsafe
			{
				D3D12_RANGE readRange = new(0, 0); // We do not intend to read from this resource on the CPU.
				HRESULT.ThrowIfFailed(m_constantBufferGS!.Map(0, readRange, out m_pConstantBufferGSData));
				m_pConstantBufferGSData.FillMemory(0, constantBufferGSSize);
			}
		}

		// Close the command list and execute it to begin the initial GPU setup.
		HRESULT.ThrowIfFailed(m_commandList!.Close());
		ID3D12CommandList[] ppCommandLists = [m_commandList];
		m_commandQueue!.ExecuteCommandLists(ppCommandLists);

		// Create synchronization objects and wait until assets have been uploaded to the GPU.
		{
			HRESULT.ThrowIfFailed(m_device!.CreateFence(m_renderContextFenceValue, D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE, out m_renderContextFence));
			m_renderContextFenceValue++;

			m_renderContextFenceEvent = CreateEvent(default, false, false, default);
			if (m_renderContextFenceEvent.IsInvalid)
				Win32Error.ThrowLastError();

			WaitForRenderContext();
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

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, warpAdapter!, out m_device).ThrowIfFailed();
		}
		else
		{
			GetHardwareAdapter(factory, out IDXGIAdapter1? hardwareAdapter);

			D3D12CreateDevice(D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0, hardwareAdapter, out m_device).ThrowIfFailed();
		}

		D3D12_FEATURE_DATA_D3D12_OPTIONS12 options12 = new();
		HRESULT.ThrowIfFailed(m_device!.CheckFeatureSupport(ref options12));
		m_bIsEnhancedBarriersEnabled = options12.EnhancedBarriersSupported;

		// Describe and create the command queue.
		D3D12_COMMAND_QUEUE_DESC queueDesc = new() { Flags = D3D12_COMMAND_QUEUE_FLAGS.D3D12_COMMAND_QUEUE_FLAG_NONE, Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT };

		HRESULT.ThrowIfFailed(m_device!.CreateCommandQueue(queueDesc, out m_commandQueue));
		NAME_D3D12_OBJECT(m_commandQueue!);

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
			Flags = DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT
		};

		IDXGISwapChain1? swapChain = factory.CreateSwapChainForHwnd(m_commandQueue!, // Swap chain needs the queue so that it can force a flush on it.
			Win32App!.Handle, swapChainDesc);

		// This sample does not support fullscreen transitions.
		factory.MakeWindowAssociation(Win32App!.Handle, DXGI_MWA.DXGI_MWA_NO_ALT_ENTER);

		m_swapChain = (IDXGISwapChain3)swapChain;
		m_frameIndex = m_swapChain.GetCurrentBackBufferIndex();
		m_swapChainEvent = new((IntPtr)m_swapChain.GetFrameLatencyWaitableObject(), true);

		// Create descriptor heaps.
		{
			// Describe and create a render target view (RTV) descriptor heap.
			D3D12_DESCRIPTOR_HEAP_DESC rtvHeapDesc = new()
			{
				NumDescriptors = FrameCount,
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_NONE
			};
			HRESULT.ThrowIfFailed(m_device!.CreateDescriptorHeap(rtvHeapDesc, out m_rtvHeap));

			// Describe and create a shader resource view (SRV) and unordered access view (UAV) descriptor heap.
			D3D12_DESCRIPTOR_HEAP_DESC srvUavHeapDesc = new()
			{
				NumDescriptors = (uint)DescriptorHeapIndex.DescriptorCount,
				Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
				Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE
			};
			HRESULT.ThrowIfFailed(m_device!.CreateDescriptorHeap(srvUavHeapDesc, out m_srvUavHeap));
			NAME_D3D12_OBJECT(m_srvUavHeap!);

			m_rtvDescriptorSize = m_device!.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
			m_srvUavDescriptorSize = m_device.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
		}

		// Create frame resources.
		{
			D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart());

			// Create a RTV and a command allocator for each frame.
			for (uint n = 0; n < FrameCount; n++)
			{
				HRESULT.ThrowIfFailed(m_swapChain.GetBuffer(n, out m_renderTargets[n]!));
				m_device.CreateRenderTargetView(m_renderTargets[n], default, rtvHandle);
				rtvHandle.Offset(1, m_rtvDescriptorSize);

				NAME_D3D12_OBJECT_INDEXED(m_renderTargets!, n);

				HRESULT.ThrowIfFailed(m_device.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, out m_commandAllocators[n]));
			}
		}
	}

	// Cycle through the frame resources. This method blocks execution if the next frame resource in the queue has not yet had its previous
	// contents processed by the GPU.
	private void MoveToNextFrame()
	{
		// Assign the current fence value to the current frame.
		m_frameFenceValues[m_frameIndex] = m_renderContextFenceValue;

		// Signal and increment the fence value.
		HRESULT.ThrowIfFailed(m_commandQueue!.Signal(m_renderContextFence!, m_renderContextFenceValue));
		m_renderContextFenceValue++;

		// Update the frame index.
		m_frameIndex = m_swapChain!.GetCurrentBackBufferIndex();

		// If the next frame is not ready to be rendered yet, wait until it is ready.
		if (m_renderContextFence!.GetCompletedValue() < m_frameFenceValues[m_frameIndex])
		{
			HRESULT.ThrowIfFailed(m_renderContextFence.SetEventOnCompletion(m_frameFenceValues[m_frameIndex], m_renderContextFenceEvent));
			WaitForSingleObject(m_renderContextFenceEvent, INFINITE);
		}
	}

	// Fill the command list with all the render commands and dependent state.
	private void PopulateCommandList()
	{
		// Command list allocators can only be reset when the associated command lists have finished execution on the GPU; apps should use
		// fences to determine GPU execution progress.
		HRESULT.ThrowIfFailed(m_commandAllocators[m_frameIndex]!.Reset());

		// However, when ExecuteCommandList() is called on a particular command list, that command list can then be reset at any time and
		// must be before re-recording.
		HRESULT.ThrowIfFailed(m_commandList!.Reset(m_commandAllocators[m_frameIndex]!, m_pipelineState));

		// Set necessary state.
		m_commandList.SetPipelineState(m_pipelineState!);
		m_commandList.SetGraphicsRootSignature(m_rootSignature);

		m_commandList.SetGraphicsRootConstantBufferView((uint)GraphicsRootParameters.GraphicsRootCBV, m_constantBufferGS!.GetGPUVirtualAddress() + m_frameIndex * (ulong)Marshal.SizeOf<ConstantBufferGS>());

		ID3D12DescriptorHeap[] ppHeaps = [m_srvUavHeap!];
		m_commandList.SetDescriptorHeaps(ppHeaps);

		m_commandList.IASetVertexBuffers(0, [m_vertexBufferView]);
		m_commandList.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_POINTLIST);
		m_commandList.RSSetScissorRects([m_scissorRect]);

		if (m_bIsEnhancedBarriersEnabled)
		{
			D3D12_TEXTURE_BARRIER[] BeginFrameBarriers =
			[
			// Using SYNC_NONE and ACCESS_NO_ACCESS with Enhanced Barrier to avoid unnecessary sync/flush. Using them explicitly tells the
			// GPU it is okay to immediately transition the layout without waiting for preceding work to complete. In this case, the legacy
			// barrier would flush and finish any preceding work that may potentially be reading from the RT resource (in this case there is none)
			new(D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_NONE, // SyncBefore
				D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_RENDER_TARGET, // SyncAfter
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_NO_ACCESS, // AccessBefore
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_RENDER_TARGET, // AccessAfter
				D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_PRESENT, // LayoutBefore
				D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_RENDER_TARGET, // LayoutAfter
				m_renderTargets[m_frameIndex],
				new(0xffffffff), // All subresources
				D3D12_TEXTURE_BARRIER_FLAGS.D3D12_TEXTURE_BARRIER_FLAG_NONE)
			];
			D3D12_BARRIER_GROUP[] BeginFrameBarriersGroups = [new(BeginFrameBarriers, out _)];
			m_commandList.Barrier(BeginFrameBarriersGroups);
		}
		else
		{
			// Indicate that the back buffer will be used as a render target.
			m_commandList.ResourceBarrier(1, [D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET)]);
		}

		D3D12_CPU_DESCRIPTOR_HANDLE rtvHandle = new(m_rtvHeap!.GetCPUDescriptorHandleForHeapStart(), (int)m_frameIndex, m_rtvDescriptorSize);
		m_commandList.OMSetRenderTargets([rtvHandle], false, default);

		// Record commands.
		float[] clearColor = [0.0f, 0.0f, 0.1f, 0.0f];
		m_commandList.ClearRenderTargetView(rtvHandle, clearColor, 0, default);

		// Render the particles.
		float viewportHeight = (uint)m_viewport.Height / m_heightInstances;
		float viewportWidth = (uint)m_viewport.Width / m_widthInstances;
		for (uint n = 0; n < ThreadCount; n++)
		{
			uint srvIndex = n + (m_srvIndex[n] == 0 ? (uint)DescriptorHeapIndex.SrvParticlePosVelo0 : (uint)DescriptorHeapIndex.SrvParticlePosVelo1);

			D3D12_VIEWPORT viewport = new(n % m_widthInstances * viewportWidth, n / m_widthInstances * viewportHeight, viewportWidth, viewportHeight);

			m_commandList.RSSetViewports([viewport]);

			D3D12_GPU_DESCRIPTOR_HANDLE srvHandle = new(m_srvUavHeap!.GetGPUDescriptorHandleForHeapStart(), (int)srvIndex, m_srvUavDescriptorSize);
			m_commandList.SetGraphicsRootDescriptorTable((uint)GraphicsRootParameters.GraphicsRootSRVTable, srvHandle);

			using PIXEvent pix = new(m_commandList, "Draw particles for thread {0}", n);
			m_commandList.DrawInstanced(ParticleCount, 1, 0, 0);
		}

		m_commandList.RSSetViewports([m_viewport]);

		if (true)
		{
			D3D12_TEXTURE_BARRIER[] EndFrameBarriers =
			[
			// Using SYNC_NONE and ACCESS_NO_ACCESS with Enhanced Barrier means subsequent commands are unblocked without having to wait for
			// the barrier to complete.
			new(D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_RENDER_TARGET, // SyncBefore
				D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_NONE, // SyncAfter
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_RENDER_TARGET, // AccessBefore
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_NO_ACCESS, // AccessAfter
				D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_RENDER_TARGET, // LayoutBefore
				D3D12_BARRIER_LAYOUT.D3D12_BARRIER_LAYOUT_PRESENT, // LayoutAfter
				m_renderTargets[m_frameIndex],
				new(0xffffffff), // All subresources
				D3D12_TEXTURE_BARRIER_FLAGS.D3D12_TEXTURE_BARRIER_FLAG_NONE)
			];
			D3D12_BARRIER_GROUP[] EndFrameBarrierGroups = [new(EndFrameBarriers, out _)];
			m_commandList.Barrier(EndFrameBarrierGroups);
		}
		else
		{
			// Indicate that the back buffer will now be used to present.
#pragma warning disable CS0162 // Unreachable code detected
			m_commandList!.ResourceBarrier([D3D12_RESOURCE_BARRIER.CreateTransition(m_renderTargets[m_frameIndex], D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT)]);
#pragma warning restore CS0162 // Unreachable code detected
		}

		HRESULT.ThrowIfFailed(m_commandList.Close());
	}

	// Release sample's D3D objects.
	private void ReleaseD3DResources()
	{
		m_renderContextFence = null;
		for (int i = 0; i < m_renderTargets.Length; i++)
			m_renderTargets[i] = null!;
		m_commandQueue = null;
		m_swapChain = null;
		m_device = null;
	}

	// Tears down D3D resources and reinitializes them.
	private void RestoreD3DResources()
	{
		// Give GPU a chance to finish its execution in progress.
		try
		{
			WaitForGpu();
		}
		catch
		{
			// Do nothing, currently attached adapter is unresponsive.
		}
		ReleaseD3DResources();
		OnInit();
	}

	// Run the particle simulation using the compute shader.
	private void Simulate(int threadIndex)
	{
		ID3D12GraphicsCommandList8 pCommandList = m_computeCommandList[threadIndex];
		ID3D12Resource pUavResource;

		uint srvIndex, uavIndex;
		if (m_srvIndex[threadIndex] == 0)
		{
			srvIndex = (uint)DescriptorHeapIndex.SrvParticlePosVelo0;
			uavIndex = (uint)DescriptorHeapIndex.UavParticlePosVelo1;
			pUavResource = m_particleBuffer1[threadIndex];
		}
		else
		{
			srvIndex = (uint)DescriptorHeapIndex.SrvParticlePosVelo1;
			uavIndex = (uint)DescriptorHeapIndex.UavParticlePosVelo0;
			pUavResource = m_particleBuffer0[threadIndex];
		}

		if (m_bIsEnhancedBarriersEnabled)
		{
			D3D12_BUFFER_BARRIER[] BeginUAVBufBarriers = [
			new(D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_NON_PIXEL_SHADING, // SyncBefore
				D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_COMPUTE_SHADING, // SyncAfter
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_SHADER_RESOURCE, // AccessBefore
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_UNORDERED_ACCESS, // AccessAfter
				pUavResource)
			];
			D3D12_BARRIER_GROUP[] BeginUAVBufBarrierGroups = [new(BeginUAVBufBarriers, out _)];
			pCommandList.Barrier(BeginUAVBufBarrierGroups.Length, BeginUAVBufBarrierGroups);
		}
		else
		{
			pCommandList.ResourceBarrier([D3D12_RESOURCE_BARRIER.CreateTransition(pUavResource, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS)]);
		}

		pCommandList.SetPipelineState(m_computeState!);
		pCommandList.SetComputeRootSignature(m_computeRootSignature!);

		ID3D12DescriptorHeap[] ppHeaps = [m_srvUavHeap!];
		pCommandList.SetDescriptorHeaps(ppHeaps);

		D3D12_GPU_DESCRIPTOR_HANDLE srvHandle = new(m_srvUavHeap!.GetGPUDescriptorHandleForHeapStart(), (int)srvIndex + threadIndex, m_srvUavDescriptorSize);
		D3D12_GPU_DESCRIPTOR_HANDLE uavHandle = new(m_srvUavHeap!.GetGPUDescriptorHandleForHeapStart(), (int)uavIndex + threadIndex, m_srvUavDescriptorSize);

		pCommandList.SetComputeRootConstantBufferView((uint)ComputeRootParameters.ComputeRootCBV, m_constantBufferCS!.GetGPUVirtualAddress());
		pCommandList.SetComputeRootDescriptorTable((uint)ComputeRootParameters.ComputeRootSRVTable, srvHandle);
		pCommandList.SetComputeRootDescriptorTable((uint)ComputeRootParameters.ComputeRootUAVTable, uavHandle);

		pCommandList.Dispatch((uint)Math.Ceiling(ParticleCount / 128.0f), 1, 1);

		if (m_bIsEnhancedBarriersEnabled)
		{
			D3D12_BUFFER_BARRIER[] EndUAVBufBarriers = [
			new(D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_COMPUTE_SHADING, // SyncBefore
				D3D12_BARRIER_SYNC.D3D12_BARRIER_SYNC_NON_PIXEL_SHADING, // SyncAfter
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_UNORDERED_ACCESS, // AccessBefore
				D3D12_BARRIER_ACCESS.D3D12_BARRIER_ACCESS_SHADER_RESOURCE, // AccessAfter
				pUavResource)
			];
			D3D12_BARRIER_GROUP[] EndUAVBufBarrierGroups = [new(EndUAVBufBarriers, out _)];
			pCommandList.Barrier(EndUAVBufBarrierGroups);
		}
		else
		{
			pCommandList.ResourceBarrier([D3D12_RESOURCE_BARRIER.CreateTransition(pUavResource, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE)]);
		}
	}

	// Wait for pending GPU work to complete.
	private void WaitForGpu()
	{
		// Schedule a Signal command in the queue.
		HRESULT.ThrowIfFailed(m_commandQueue!.Signal(m_renderContextFence!, m_renderContextFenceValues[m_frameIndex]));

		// Wait until the fence has been processed.
		HRESULT.ThrowIfFailed(m_renderContextFence!.SetEventOnCompletion(m_renderContextFenceValues[m_frameIndex], m_renderContextFenceEvent));
		_ = WaitForSingleObjectEx(m_renderContextFenceEvent, INFINITE, false);

		// Increment the fence value for the current frame.
		m_renderContextFenceValues[m_frameIndex]++;
	}

	private void WaitForRenderContext()
	{
		// Add a signal command to the queue.
		HRESULT.ThrowIfFailed(m_commandQueue!.Signal(m_renderContextFence!, m_renderContextFenceValue));

		// Instruct the fence to set the event object when the signal command completes.
		HRESULT.ThrowIfFailed(m_renderContextFence!.SetEventOnCompletion(m_renderContextFenceValue, m_renderContextFenceEvent));
		m_renderContextFenceValue++;

		// Wait until the signal command has been processed.
		WaitForSingleObject(m_renderContextFenceEvent, INFINITE);
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ConstantBufferCS
	{
		public unsafe fixed uint param[4];
		public unsafe fixed float paramf[4];
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ConstantBufferGS
	{
		public XMMATRIX worldViewProjection;
		public XMMATRIX inverseView;

		// Constant buffers are 256-byte aligned in GPU memory. Padding is added for convenience when computing the struct's size.
		public unsafe fixed float padding[32];
	}

	// Position and velocity data for the particles in the system. Two buffers full of Particle data are utilized in this sample. The compute
	// thread alternates writing to each of them. The render thread renders using the buffer that is not currently in use by the compute shader.
	[StructLayout(LayoutKind.Sequential)]
	private struct Particle
	{
		public XMFLOAT4 position;
		public XMFLOAT4 velocity;
	}

	[StructLayout(LayoutKind.Sequential)]
	// "Vertex" definition for particles. Triangle vertices are generated by the geometry shader. Color data will be assigned to those
	// vertices via this struct.
	private struct ParticleVertex
	{
		public XMFLOAT4 color;
	}

	private class ThreadData
	{
		public D3D12nBodyGravity? pContext;
		public int threadIndex;
	}
}