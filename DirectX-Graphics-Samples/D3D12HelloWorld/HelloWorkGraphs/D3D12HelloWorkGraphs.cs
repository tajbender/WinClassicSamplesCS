using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.D3D12;
using static Vanara.PInvoke.D3DCompiler;
using static Vanara.PInvoke.D3d12Add;
using static Vanara.PInvoke.DXC;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.Kernel32;

internal class Program
{
	// use a warp device instead of a hardware device
	static bool g_useWarpDevice = false;

	//=================================================================================================================================
	// Start of interesting code
	//=================================================================================================================================
	private static int Main(string[] args)
	{
		const string g_File = "D3D12HelloWorkGraphs.hlsl";

		try
		{
			PRINT("""
			==================================================================================
			 Hello Work Graphs
			==================================================================================
			>>> Compiling library...
			""");

			CompileDxilLibraryFromFile(g_File, "lib_6_8", default, out var library);

			PRINT(">>> Device init...");
			D3DContext D3D = new();

			D3D12_FEATURE_DATA_D3D12_OPTIONS21 Options = new();
			D3D.spDevice!.CheckFeatureSupport(ref Options, D3D12_FEATURE.D3D12_FEATURE_D3D12_OPTIONS21).ThrowIfFailed();
			if (Options.WorkGraphsTier == D3D12_WORK_GRAPHS_TIER.D3D12_WORK_GRAPHS_TIER_NOT_SUPPORTED)
			{
				PRINT("Device does not report support for work graphs.");
				return -1;
			}

			// Initialize GPU buffers
			uint bufSizeInUints = 16777216;
			uint bufSize = bufSizeInUints * sizeof(uint);
			SafeCoTaskMemHandle initialData = new(bufSize);
			MakeBufferAndInitialize(D3D, out var spGPUBuffer, initialData, bufSize, out _, true, D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS);
			MakeBuffer(D3D, out ID3D12Resource? spReadbackBuffer, bufSize, D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE, D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_READBACK);

			// Create work graph
			PRINT(">>> Creating work graph...");
			D3D12_STATE_OBJECT_DESC_MGD SO = new(D3D12_STATE_OBJECT_TYPE.D3D12_STATE_OBJECT_TYPE_EXECUTABLE);
			D3D12_SHADER_BYTECODE libCode = new(library!);
			SO.Add(new D3D12_DXIL_LIBRARY_DESC { DXILLibrary = libCode });
			D3D.spDevice!.CreateRootSignatureFromSubobjectInLibrary(0, libCode.pShaderBytecode, libCode.BytecodeLength, "globalRS", out ID3D12RootSignature? spRS).ThrowIfFailed();

			const string workGraphName = "HelloWorkGraphs";
			D3D12_WORK_GRAPH_DESC pWG = new() { Flags = D3D12_WORK_GRAPH_FLAGS.D3D12_WORK_GRAPH_FLAG_INCLUDE_ALL_AVAILABLE_NODES, ProgramName = workGraphName };
			SO.Add(pWG);

			D3D.spDevice!.CreateStateObject(SO, out ID3D12StateObject? spSO).ThrowIfFailed();
			WorkGraphContext WG = new(D3D, spSO, "HelloWorkGraphs");

			// Setup program
			D3D.spCL.SetComputeRootSignature(spRS);
			D3D.spCL.SetComputeRootUnorderedAccessView(0, spGPUBuffer!.GetGPUVirtualAddress());

			D3D12_SET_PROGRAM_DESC setProg = new()
			{
				Type = D3D12_PROGRAM_TYPE.D3D12_PROGRAM_TYPE_WORK_GRAPH,
				WorkGraph = new()
				{
					ProgramIdentifier = WG.hWorkGraph,
					Flags = D3D12_SET_WORK_GRAPH_FLAGS.D3D12_SET_WORK_GRAPH_FLAG_INITIALIZE,
					BackingMemory = WG.BackingMemory
				}
			};
			D3D.spCL.SetProgram(setProg);

			// Generate graph inputs
			SafeNativeArray<EntryRecord> inputData = new(4);
			for (int recordIndex = 0; recordIndex < inputData.Count; recordIndex++)
				inputData[recordIndex] = new((uint)recordIndex);

			// Spawn work
			D3D12_DISPATCH_GRAPH_DESC DSDesc = new()
			{
				Mode = D3D12_DISPATCH_MODE.D3D12_DISPATCH_MODE_NODE_CPU_INPUT,
				NodeCPUInput = new()
				{
					EntrypointIndex = 0, // just one entrypoint in this graph
					NumRecords = (uint)inputData.Count,
					RecordStrideInBytes = sizeof(uint) * 2,
					pRecords = inputData
				}
			};
			D3D.spCL.DispatchGraph(DSDesc);

			PRINT(">>> Dispatching work graph...\n");
			FlushAndFinish(D3D);

			// Readback GPU buffer
			Transition(D3D.spCL, spGPUBuffer, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_SOURCE);
			D3D.spCL.CopyResource(spReadbackBuffer!, spGPUBuffer);
			FlushAndFinish(D3D);

			D3D12_RANGE range = new(0, bufSize);
			spReadbackBuffer!.Map(0, range, out var p).ThrowIfFailed();
			ArrayPointer<uint> pDataOutput = p;
			uint numUintsToPrint = (uint)inputData.Count * 2;
			PRINT(">>> Dumping {numUintsToPrint} uints from UAV:\n");
			for (int i = 0; i < numUintsToPrint; i++)
			{
				PRINT($" UAV[{i}] = 0x{pDataOutput[i]:X}");
			}
			spReadbackBuffer!.Unmap(0, default);
			PRINT("""
			==================================================================================
			 Execution complete
			==================================================================================
			""");
		}
		catch (Exception ex)
		{
			PRINT($"Aborting. Error: {ex.Message} Line: {ex.StackTrace}");
			return -1;
		}

		return 0;
	}

	//=================================================================================================================================
	// Helper / setup code, not specific to work graphs
	// Look for "Start of interesting code" further below.
	//=================================================================================================================================

	static void PRINT(string? text) => Console.WriteLine(text);

	//=================================================================================================================================
	static void CompileDxilLibraryFromFile(string pFile, string pTarget, string[]? pArguments, out ID3DBlob? ppCode)
	{
		DxcCreateInstance(CLSID_DxcUtils, out IDxcUtils? utils).ThrowIfFailed("Failed to instantiate compiler.");

		utils!.LoadFile(pFile, default, out IDxcBlobEncoding? source).ThrowIfFailed("Create Blob From File Failed - perhaps file is missing?");

		utils.CreateDefaultIncludeHandler(out var includeHandler).ThrowIfFailed("Failed to create include handler.");

		DxcCreateInstance(CLSID_DxcCompiler, out IDxcCompiler3? compiler).ThrowIfFailed("Failed to instantiate compiler.");

		DxcBuffer dxcBuffer = new()
		{
			Ptr = source.GetBufferPointer(),
			Size = source.GetBufferSize()
		};
		compiler!.Compile(dxcBuffer, pArguments, includeHandler, out IDxcResult? operationResult).
			ThrowIfFailed("Failed to compile.");

		operationResult!.GetStatus(out var hr);
		if (hr.Succeeded)
		{
			hr = operationResult.GetResult(out ppCode);
			if (hr.Failed)
				PRINT("Failed to retrieve compiled code.");
		}
		else
			ppCode = null;
		if (operationResult.GetErrorBuffer(out IDxcBlobEncoding? pErrors).Succeeded)
		{
			var pText = pErrors!.GetBufferPointer();
			PRINT(StringHelper.GetString(pText, CharSet.Unicode));
		}
	}

	//=================================================================================================================================
	static void FlushAndFinish(D3DContext D3D)
	{
		D3D.spCL.Close().ThrowIfFailed();
		D3D.spCQ.ExecuteCommandLists(1, [D3D.spCL]);

		D3D.spCQ.Signal(D3D.spFence, ++D3D.FenceValue).ThrowIfFailed();
		D3D.spFence.SetEventOnCompletion(D3D.FenceValue, D3D.hEvent).ThrowIfFailed();

		var waitResult = WaitForSingleObject(D3D.hEvent, INFINITE);
		if (waitResult != WAIT_STATUS.WAIT_OBJECT_0)
		{
			PRINT("Flush and finish wait failed");
			throw new COMException();
		}
		D3D.spDevice!.GetDeviceRemovedReason().ThrowIfFailed();

		D3D.spCA.Reset().ThrowIfFailed();
		D3D.spCL.Reset(D3D.spCA, default).ThrowIfFailed();
	}

	//=================================================================================================================================
	static void Transition(ID3D12GraphicsCommandList pCL, ID3D12Resource pResource, D3D12_RESOURCE_STATES StateBefore, D3D12_RESOURCE_STATES StateAfter)
	{
		if (StateBefore == StateAfter) return;
		var RB = D3D12_RESOURCE_BARRIER.CreateTransition(pResource, StateBefore, StateAfter);
		pCL.ResourceBarrier(1, [RB]);
	}

	//=================================================================================================================================
	static void MakeBuffer(D3DContext D3D, out ID3D12Resource? ppResource, ulong SizeInBytes, D3D12_RESOURCE_FLAGS ResourceMiscFlags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE, D3D12_HEAP_TYPE HeapType = D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT)
	{
		D3D12_RESOURCE_DESC rd = D3D12_RESOURCE_DESC.Buffer(SizeInBytes);
		rd.Flags = ResourceMiscFlags;
		D3D12_HEAP_PROPERTIES hp = new(HeapType);

		D3D.spDevice!.CreateCommittedResource(hp, D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE, rd, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON,
			null, out ppResource).ThrowIfFailed();
	}

	//=================================================================================================================================
	static void UploadData(D3DContext D3D, ID3D12Resource pResource, nint pData, SizeT Size, out ID3D12Resource? ppStagingResource,
			D3D12_RESOURCE_STATES CurrentState, bool doFlush)
	{
		D3D12_HEAP_PROPERTIES HeapProps = new(D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD);
		ulong IntermediateSize = GetRequiredIntermediateSize(pResource, 0, 1);
		if (Size != IntermediateSize)
		{
			PRINT("Provided Size of pData needs to account for the whole buffer (i.e. equal to GetRequiredIntermediateSize() output)");
			throw new COMException();
		}
		D3D12_RESOURCE_DESC BufferDesc = D3D12_RESOURCE_DESC.Buffer(IntermediateSize);
		D3D.spDevice!.CreateCommittedResource(HeapProps, D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE, BufferDesc,
			D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ, default, out ppStagingResource).ThrowIfFailed();

		bool NeedTransition = (CurrentState & D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST) == 0;
		D3D12_RESOURCE_BARRIER BarrierDesc = default;
		if (NeedTransition)
		{
			BarrierDesc = D3D12_RESOURCE_BARRIER.CreateTransition(pResource, CurrentState, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST);
			D3D.spCL.ResourceBarrier(1, [BarrierDesc]);
			swap(ref BarrierDesc.Transition.StateBefore, ref BarrierDesc.Transition.StateAfter); // ensure StateBefore represents current state
		}

		// Execute upload
		D3D12_SUBRESOURCE_DATA SubResourceData = new(pData, Size, Size);
		if (Size != UpdateSubresources(D3D.spCL, pResource, ppStagingResource!, 0, 0, 1, [SubResourceData]))
		{
			PRINT("UpdateSubresources returns the number of bytes updated, so 0 if nothing was updated");
			throw new COMException();
		}
		if (NeedTransition)
		{
			// Transition back to whatever the app had
			D3D.spCL.ResourceBarrier(1, [BarrierDesc]);
			swap(ref BarrierDesc.Transition.StateBefore, ref BarrierDesc.Transition.StateAfter); // ensure StateBefore represents current state
		}
		if (doFlush == true)
			// Finish Upload
			FlushAndFinish(D3D);
	}

	//=================================================================================================================================
	static void MakeBufferAndInitialize(D3DContext D3D, out ID3D12Resource? ppResource, nint pInitialData, ulong SizeInBytes, out ID3D12Resource? ppStagingResource,
		bool doFlush = true, D3D12_RESOURCE_FLAGS ResourceMiscFlags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE)
	{
		MakeBuffer(D3D, out ppResource, SizeInBytes, ResourceMiscFlags, D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT);
		UploadData(D3D, ppResource!, pInitialData, SizeInBytes, out ppStagingResource, D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON, doFlush);
	}

	//=================================================================================================================================
	[StructLayout(LayoutKind.Sequential)]
	struct EntryRecord(uint i) // equivalent to the definition in HLSL code
	{
		public uint gridSize = i + 1; // : SV_DispatchGrid;
		public uint recordIndex = i;
	}

	//=================================================================================================================================
	class D3DContext
	{
		public ID3D12Device14? spDevice;
		public ID3D12GraphicsCommandList10 spCL;
		public ID3D12CommandQueue spCQ;
		public ID3D12CommandAllocator spCA;
		public ID3D12Fence spFence;
		public ulong FenceValue;
		public SafeEventHandle hEvent;

		public D3DContext()
		{
			hEvent = CreateEvent(default, false, false, default);

#if DEBUG
			var pDebug = D3D12GetDebugInterface<ID3D12Debug1>();
			pDebug!.EnableDebugLayer();
#endif

			D3D_FEATURE_LEVEL FL = D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0;
			if (g_useWarpDevice)
			{
				var factory = CreateDXGIFactory2<IDXGIFactory4>();

				factory.EnumWarpAdapter(out IDXGIAdapter? warpAdapter).ThrowIfFailed();
				D3D12CreateDevice(FL, warpAdapter, out spDevice!).ThrowIfFailed();
			}
			else
			{
				D3D12CreateDevice(FL, null, out spDevice!).ThrowIfFailed();
			}

			spDevice.CreateFence(0, D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE, out spFence!).ThrowIfFailed();

			spDevice.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, out spCA!).ThrowIfFailed();

			D3D12_COMMAND_QUEUE_DESC CQD = new();
			CQD.Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT;
			spDevice.CreateCommandQueue(CQD, out spCQ!).ThrowIfFailed();

			spDevice.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT, spCA!, default, out spCL!).ThrowIfFailed();
		}
	}

	//=================================================================================================================================
	class WorkGraphContext
	{
		public ID3D12Resource? spBackingMemory;
		public D3D12_GPU_VIRTUAL_ADDRESS_RANGE BackingMemory = default;
		public D3D12_PROGRAM_IDENTIFIER hWorkGraph = default;
		public D3D12_WORK_GRAPH_MEMORY_REQUIREMENTS MemReqs = default;

		public WorkGraphContext(D3DContext D3D, ID3D12StateObject? spSO, string pWorkGraphName)
		{
			ID3D12StateObjectProperties1? spSOProps = (ID3D12StateObjectProperties1?)spSO;
			hWorkGraph = spSOProps!.GetProgramIdentifier(pWorkGraphName);
			ID3D12WorkGraphProperties? spWGProps = (ID3D12WorkGraphProperties?)spSO;
			uint WorkGraphIndex = spWGProps!.GetWorkGraphIndex(pWorkGraphName);
			spWGProps.GetWorkGraphMemoryRequirements(WorkGraphIndex, out MemReqs);
			BackingMemory.SizeInBytes = MemReqs.MaxSizeInBytes;
			MakeBuffer(D3D, out spBackingMemory, BackingMemory.SizeInBytes, D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS);
			BackingMemory.StartAddress = spBackingMemory!.GetGPUVirtualAddress();
		}
	}
}