internal enum DUPL_RETURN
{
	DUPL_RETURN_SUCCESS,
	DUPL_RETURN_ERROR_EXPECTED = 1,
	DUPL_RETURN_ERROR_UNEXPECTED = 2
}

//
// Holds info about the pointer/cursor
//
internal class PTR_INFO : IDisposable
{
	public IntPtr PtrShapeBuffer;
	public DXGI_OUTDUPL_POINTER_SHAPE_INFO ShapeInfo;
	public POINT Position;
	public bool Visible;
	public uint BufferSize;
	public uint WhoUpdatedPositionLast;
	public long LastTimeStamp;

	public void Dispose() { if (PtrShapeBuffer == 0) return; Marshal.FreeHGlobal(PtrShapeBuffer); PtrShapeBuffer = 0; }
}

//
// Structure that holds D3D resources not directly tied to any one thread
//
internal class DX_RESOURCES : IDisposable
{
	public ID3D11Device? Device;
	public ID3D11DeviceContext? Context;
	public ID3D11VertexShader? VertexShader;
	public ID3D11PixelShader? PixelShader;
	public ID3D11InputLayout? InputLayout;
	public ID3D11SamplerState? SamplerLinear;

	public void Dispose()
	{
		Device = null;
		Context = null;
		VertexShader = null;
		PixelShader = null;
		InputLayout = null;
		SamplerLinear = null;
	}
}

//
// Structure to pass to a new thread
//
internal class THREAD_DATA
{
	// Used to indicate abnormal error condition
	public SafeEventHandle UnexpectedErrorEvent = SafeEventHandle.Null;

	// Used to indicate a transition event occurred e.g. PnpStop, PnpStart, mode change, TDR, desktop switch and the application needs to recreate the duplication interface
	public SafeEventHandle ExpectedErrorEvent = SafeEventHandle.Null;

	// Used by WinProc to signal to threads to exit
	public SafeEventHandle TerminateThreadsEvent = SafeEventHandle.Null;

	public HANDLE TexSharedHandle;
	public uint Output;
	public int OffsetX;
	public int OffsetY;
	public PTR_INFO? PtrInfo;
	public DX_RESOURCES DxRes = new();
}

//
// FRAME_DATA holds information about an acquired frame
//
internal class FRAME_DATA
{
	[MarshalAs(UnmanagedType.IUnknown)]
	public ID3D11Texture2D? Frame;
	public DXGI_OUTDUPL_FRAME_INFO FrameInfo;
	public IntPtr MetaData;
	public int DirtyCount;
	public int MoveCount;
}

//
// A vertex with a position and texture coordinate
//
[StructLayout(LayoutKind.Sequential)]
internal struct VERTEX(XMFLOAT3 pos, XMFLOAT2 texCoord)
{
	public XMFLOAT3 Pos = pos;
	public XMFLOAT2 TexCoord = texCoord;
}
