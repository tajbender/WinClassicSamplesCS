using static Program;

internal class DUPLICATIONMANAGER : IDisposable
{
	ID3D11Device? m_Device;
	IDXGIOutputDuplication? m_DeskDupl;
	ID3D11Texture2D? m_AcquiredDesktopImage;
	IntPtr m_MetaDataBuffer;
	uint m_MetaDataSize;
	uint m_OutputNumber;
	DXGI_OUTPUT_DESC m_OutputDesc = default;

	//
	// Initialize duplication interfaces
	//
	public DUPL_RETURN InitDupl([In] ID3D11Device Device, uint Output)
	{
		m_OutputNumber = Output;

		// Take a reference on the device
		m_Device = Device;

		// Get DXGI device
		IDXGIDevice? DxgiDevice = (IDXGIDevice)Device;

		// Get DXGI adapter
		IDXGIAdapter? DxgiAdapter = DxgiDevice.GetParent<IDXGIAdapter>();
		DxgiDevice = default;

		// Get output
		var hr = DxgiAdapter!.EnumOutputs(Output, out var DxgiOutput);
		DxgiAdapter = default;
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to get specified output in DUPLICATIONMANAGER", "Error", hr, EnumOutputsExpectedErrors);
		}

		m_OutputDesc = DxgiOutput.GetDesc();

		// QI for Output 1
		IDXGIOutput1? DxgiOutput1 = DxgiOutput as IDXGIOutput1;
		DxgiOutput = default;
		if (DxgiOutput1 is null)
		{
			return ProcessFailure(default, "Failed to QI for DxgiOutput1 in DUPLICATIONMANAGER", "Error", hr);
		}

		// Create desktop duplication
		hr = DxgiOutput1.DuplicateOutput(m_Device, out m_DeskDupl);
		DxgiOutput1 = default;
		if (hr.Failed)
		{
			if (hr == HRESULT.DXGI_ERROR_NOT_CURRENTLY_AVAILABLE)
			{
				MessageBox(default, "There is already the maximum number of applications using the Desktop Duplication API running, please close one of those applications and then try again.", "Error", MB_FLAGS.MB_OK);
				return DUPL_RETURN.DUPL_RETURN_ERROR_UNEXPECTED;
			}
			return ProcessFailure(m_Device, "Failed to get duplicate output in DUPLICATIONMANAGER", "Error", hr, CreateDuplicationExpectedErrors);
		}
		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Retrieves mouse info and write it into PtrInfo
	//
	public DUPL_RETURN GetMouse([In, Out] PTR_INFO PtrInfo, in DXGI_OUTDUPL_FRAME_INFO FrameInfo, int OffsetX, int OffsetY)
	{
		// A non-zero mouse update timestamp indicates that there is a mouse position update and optionally a shape change
		if (FrameInfo.LastMouseUpdateTime == 0)
		{
			return DUPL_RETURN.DUPL_RETURN_SUCCESS;
		}

		bool UpdatePosition = true;

		// Make sure we don't update pointer position wrongly
		// If pointer is invisible, make sure we did not get an update from another output that the last time that said pointer
		// was visible, if so, don't set it to invisible or update.
		if (!FrameInfo.PointerPosition.Visible && (PtrInfo.WhoUpdatedPositionLast != m_OutputNumber))
		{
			UpdatePosition = false;
		}

		// If two outputs both say they have a visible, only update if new update has newer timestamp
		if (FrameInfo.PointerPosition.Visible && PtrInfo.Visible && (PtrInfo.WhoUpdatedPositionLast != m_OutputNumber) && (PtrInfo.LastTimeStamp > FrameInfo.LastMouseUpdateTime))
		{
			UpdatePosition = false;
		}

		// Update position
		if (UpdatePosition)
		{
			PtrInfo.Position.x = FrameInfo.PointerPosition.Position.x + m_OutputDesc.DesktopCoordinates.left - OffsetX;
			PtrInfo.Position.y = FrameInfo.PointerPosition.Position.y + m_OutputDesc.DesktopCoordinates.top - OffsetY;
			PtrInfo.WhoUpdatedPositionLast = m_OutputNumber;
			PtrInfo.LastTimeStamp = FrameInfo.LastMouseUpdateTime;
			PtrInfo.Visible = FrameInfo.PointerPosition.Visible;
		}

		// No new shape
		if (FrameInfo.PointerShapeBufferSize == 0)
		{
			return DUPL_RETURN.DUPL_RETURN_SUCCESS;
		}

		// Old buffer too small
		if (FrameInfo.PointerShapeBufferSize > PtrInfo.BufferSize)
		{
			if (PtrInfo.PtrShapeBuffer != 0)
			{
				//delete[] PtrInfo.PtrShapeBuffer;
				PtrInfo.PtrShapeBuffer = default;
			}
			PtrInfo.PtrShapeBuffer = Marshal.AllocHGlobal((int)FrameInfo.PointerShapeBufferSize);
			if (PtrInfo.PtrShapeBuffer == 0)
			{
				PtrInfo.BufferSize = 0;
				return ProcessFailure(default, "Failed to allocate memory for pointer shape in DUPLICATIONMANAGER", "Error", HRESULT.E_OUTOFMEMORY);
			}

			// Update buffer size
			PtrInfo.BufferSize = FrameInfo.PointerShapeBufferSize;
		}

		// Get shape
		HRESULT hr = m_DeskDupl!.GetFramePointerShape(FrameInfo.PointerShapeBufferSize, PtrInfo.PtrShapeBuffer, out var BufferSizeRequired, out PtrInfo.ShapeInfo);
		if (hr.Failed)
		{
			Marshal.FreeHGlobal(PtrInfo.PtrShapeBuffer);
			PtrInfo.PtrShapeBuffer = default;
			PtrInfo.BufferSize = 0;
			return ProcessFailure(m_Device, "Failed to get frame pointer shape in DUPLICATIONMANAGER", "Error", hr, FrameInfoExpectedErrors);
		}
		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}


	//
	// Get next frame and write it into Data
	//
	public DUPL_RETURN GetFrame(out FRAME_DATA Data, out bool Timeout)
	{
		Data = new();

		// Get new frame
		HRESULT hr = m_DeskDupl!.AcquireNextFrame(500, out var FrameInfo, out var DesktopResource);
		if (hr == HRESULT.DXGI_ERROR_WAIT_TIMEOUT)
		{
			Timeout = true;
			return DUPL_RETURN.DUPL_RETURN_SUCCESS;
		}
		Timeout = false;

		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to acquire next frame in DUPLICATIONMANAGER", "Error", hr, FrameInfoExpectedErrors);
		}

		// If still holding old frame, destroy it
		if (m_AcquiredDesktopImage is not null)
		{
			m_AcquiredDesktopImage = default;
		}

		// QI for IDXGIResource
		m_AcquiredDesktopImage = DesktopResource as ID3D11Texture2D;
		DesktopResource = default;
		if (m_AcquiredDesktopImage is null)
		{
			return ProcessFailure(default, "Failed to QI for ID3D11Texture2D from acquired IDXGIResource in DUPLICATIONMANAGER", "Error", hr);
		}

		// Get metadata
		if (FrameInfo.TotalMetadataBufferSize != 0)
		{
			// Old buffer too small
			if (FrameInfo.TotalMetadataBufferSize > m_MetaDataSize)
			{
				if (m_MetaDataBuffer is not 0)
				{
					Marshal.FreeHGlobal(m_MetaDataBuffer);
					m_MetaDataBuffer = default;
				}
				m_MetaDataBuffer = Marshal.AllocHGlobal((int)FrameInfo.TotalMetadataBufferSize);
				if (m_MetaDataBuffer == 0)
				{
					m_MetaDataSize = 0;
					Data.MoveCount = 0;
					Data.DirtyCount = 0;
					return ProcessFailure(default, "Failed to allocate memory for metadata in DUPLICATIONMANAGER", "Error", HRESULT.E_OUTOFMEMORY);
				}
				m_MetaDataSize = FrameInfo.TotalMetadataBufferSize;
			}

			uint BufSize = FrameInfo.TotalMetadataBufferSize;

			// Get move rectangles
			hr = m_DeskDupl.GetFrameMoveRects(BufSize, m_MetaDataBuffer, out BufSize);
			if (hr.Failed)
			{
				Data.MoveCount = 0;
				Data.DirtyCount = 0;
				return ProcessFailure(default, "Failed to get frame move rects in DUPLICATIONMANAGER", "Error", hr, FrameInfoExpectedErrors);
			}
			Data.MoveCount = (int)BufSize / Marshal.SizeOf<DXGI_OUTDUPL_MOVE_RECT>();

			IntPtr DirtyRects = m_MetaDataBuffer.Offset(BufSize);
			BufSize = FrameInfo.TotalMetadataBufferSize - BufSize;

			// Get dirty rectangles
			hr = m_DeskDupl.GetFrameDirtyRects(BufSize, DirtyRects, out BufSize);
			if (hr.Failed)
			{
				Data.MoveCount = 0;
				Data.DirtyCount = 0;
				return ProcessFailure(default, "Failed to get frame dirty rects in DUPLICATIONMANAGER", "Error", hr, FrameInfoExpectedErrors);
			}
			Data.DirtyCount = (int)BufSize / Marshal.SizeOf<RECT>();

			Data.MetaData = m_MetaDataBuffer;
		}

		Data.Frame = m_AcquiredDesktopImage;
		Data.FrameInfo = FrameInfo;

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Release frame
	//
	public DUPL_RETURN DoneWithFrame()
	{
		HRESULT hr = m_DeskDupl!.ReleaseFrame();
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to release frame in DUPLICATIONMANAGER", "Error", hr, FrameInfoExpectedErrors);
		}

		if (m_AcquiredDesktopImage is not null)
		{
			m_AcquiredDesktopImage = default;
		}

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Gets output desc into DescPtr
	//
	public void GetOutputDesc(out DXGI_OUTPUT_DESC DescPtr) => DescPtr = m_OutputDesc;

	public void Dispose() { if (m_MetaDataBuffer == 0) return; Marshal.FreeHGlobal(m_MetaDataBuffer); m_MetaDataBuffer = 0; }
}