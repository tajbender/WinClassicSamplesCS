using System.Diagnostics;
using static Program;

internal class DISPLAYMANAGER : IDisposable
{
	ID3D11Device? m_Device;
	ID3D11DeviceContext? m_DeviceContext;
	ID3D11Texture2D? m_MoveSurf;
	ID3D11VertexShader? m_VertexShader;
	ID3D11PixelShader? m_PixelShader;
	ID3D11InputLayout? m_InputLayout;
	ID3D11RenderTargetView? m_RTV;
	ID3D11SamplerState? m_SamplerLinear;
	SafeNativeArray<VERTEX> m_DirtyVertexBufferAlloc = [];

	public void Dispose() => CleanRefs();

	//
	// Initialize D3D variables
	//
	public void InitD3D(in DX_RESOURCES Data)
	{
		m_Device = Data.Device;
		m_DeviceContext = Data.Context;
		m_VertexShader = Data.VertexShader;
		m_PixelShader = Data.PixelShader;
		m_InputLayout = Data.InputLayout;
		m_SamplerLinear = Data.SamplerLinear;
	}

	//
	// Process a given frame and its metadata
	//
	public DUPL_RETURN ProcessFrame(in FRAME_DATA Data, [In, Out] ID3D11Texture2D SharedSurf, int OffsetX, int OffsetY, in DXGI_OUTPUT_DESC DeskDesc)
	{
		DUPL_RETURN Ret = DUPL_RETURN.DUPL_RETURN_SUCCESS;

		// Process dirties and moves
		if (Data.FrameInfo.TotalMetadataBufferSize != 0)
		{
			Data.Frame!.GetDesc(out var Desc);

			if (Data.MoveCount != 0)
			{
				Ret = CopyMove(SharedSurf, Data.MetaData.ToArray<DXGI_OUTDUPL_MOVE_RECT>(Data.MoveCount)!, Data.MoveCount, OffsetX, OffsetY, DeskDesc, (int)Desc.Width, (int)Desc.Height);
				if (Ret != DUPL_RETURN.DUPL_RETURN_SUCCESS)
				{
					return Ret;
				}
			}
			if (Data.DirtyCount != 0)
			{
				Ret = CopyDirty(Data.Frame, SharedSurf, Data.MetaData.Offset(Data.MoveCount * Marshal.SizeOf<DXGI_OUTDUPL_MOVE_RECT>()).ToArray<RECT>(Data.DirtyCount)!, Data.DirtyCount, OffsetX, OffsetY, DeskDesc);
			}
		}
		return Ret;
	}

	//
	// Returns D3D device being used
	//
	ID3D11Device? GetDevice() => m_Device;

	//
	// Set appropriate source and destination rects for move rects
	//
	void SetMoveRect(out RECT SrcRect, out RECT DestRect, in DXGI_OUTPUT_DESC DeskDesc, in DXGI_OUTDUPL_MOVE_RECT MoveRect, int TexWidth, int TexHeight)
	{
		switch (DeskDesc.Rotation)
		{
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_UNSPECIFIED:
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_IDENTITY:
				{
					SrcRect.left = MoveRect.SourcePoint.x;
					SrcRect.top = MoveRect.SourcePoint.y;
					SrcRect.right = MoveRect.SourcePoint.x + MoveRect.DestinationRect.right - MoveRect.DestinationRect.left;
					SrcRect.bottom = MoveRect.SourcePoint.y + MoveRect.DestinationRect.bottom - MoveRect.DestinationRect.top;

					DestRect = MoveRect.DestinationRect;
					break;
				}
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE90:
				{
					SrcRect.left = TexHeight - (MoveRect.SourcePoint.y + MoveRect.DestinationRect.bottom - MoveRect.DestinationRect.top);
					SrcRect.top = MoveRect.SourcePoint.x;
					SrcRect.right = TexHeight - MoveRect.SourcePoint.y;
					SrcRect.bottom = MoveRect.SourcePoint.x + MoveRect.DestinationRect.right - MoveRect.DestinationRect.left;

					DestRect.left = TexHeight - MoveRect.DestinationRect.bottom;
					DestRect.top = MoveRect.DestinationRect.left;
					DestRect.right = TexHeight - MoveRect.DestinationRect.top;
					DestRect.bottom = MoveRect.DestinationRect.right;
					break;
				}
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE180:
				{
					SrcRect.left = TexWidth - (MoveRect.SourcePoint.x + MoveRect.DestinationRect.right - MoveRect.DestinationRect.left);
					SrcRect.top = TexHeight - (MoveRect.SourcePoint.y + MoveRect.DestinationRect.bottom - MoveRect.DestinationRect.top);
					SrcRect.right = TexWidth - MoveRect.SourcePoint.x;
					SrcRect.bottom = TexHeight - MoveRect.SourcePoint.y;

					DestRect.left = TexWidth - MoveRect.DestinationRect.right;
					DestRect.top = TexHeight - MoveRect.DestinationRect.bottom;
					DestRect.right = TexWidth - MoveRect.DestinationRect.left;
					DestRect.bottom = TexHeight - MoveRect.DestinationRect.top;
					break;
				}
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE270:
				{
					SrcRect.left = MoveRect.SourcePoint.x;
					SrcRect.top = TexWidth - (MoveRect.SourcePoint.x + MoveRect.DestinationRect.right - MoveRect.DestinationRect.left);
					SrcRect.right = MoveRect.SourcePoint.y + MoveRect.DestinationRect.bottom - MoveRect.DestinationRect.top;
					SrcRect.bottom = TexWidth - MoveRect.SourcePoint.x;

					DestRect.left = MoveRect.DestinationRect.top;
					DestRect.top = TexWidth - MoveRect.DestinationRect.right;
					DestRect.right = MoveRect.DestinationRect.bottom;
					DestRect.bottom = TexWidth - MoveRect.DestinationRect.left;
					break;
				}
			default:
				{
					DestRect = default;
					SrcRect = default;
					break;
				}
		}
	}

	//
	// Copy move rectangles
	//
	DUPL_RETURN CopyMove([In, Out] ID3D11Texture2D SharedSurf, [In] DXGI_OUTDUPL_MOVE_RECT[] MoveBuffer, int MoveCount, int OffsetX, int OffsetY, in DXGI_OUTPUT_DESC DeskDesc, int TexWidth, int TexHeight)
	{
		SharedSurf.GetDesc(out var FullDesc);

		// Make new intermediate surface to copy into for moving
		if (m_MoveSurf is null)
		{
			D3D11_TEXTURE2D_DESC MoveDesc = FullDesc;
			MoveDesc.Width = (uint)(DeskDesc.DesktopCoordinates.right - DeskDesc.DesktopCoordinates.left);
			MoveDesc.Height = (uint)(DeskDesc.DesktopCoordinates.bottom - DeskDesc.DesktopCoordinates.top);
			MoveDesc.BindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET;
			MoveDesc.MiscFlags = 0;
			HRESULT hr = m_Device!.CreateTexture2D(MoveDesc, default, out m_MoveSurf);
			if (hr.Failed)
			{
				return ProcessFailure(m_Device, "Failed to create staging texture for move rects", "Error", hr, SystemTransitionsExpectedErrors);
			}
		}

		for (uint i = 0; i < MoveCount; ++i)
		{
			SetMoveRect(out var SrcRect, out var DestRect, DeskDesc, MoveBuffer[i], TexWidth, TexHeight);

			// Copy rect out of shared surface
			unsafe
			{
				D3D11_BOX Box;
				Box.left = (uint)(SrcRect.left + DeskDesc.DesktopCoordinates.left - OffsetX);
				Box.top = (uint)(SrcRect.top + DeskDesc.DesktopCoordinates.top - OffsetY);
				Box.front = 0;
				Box.right = (uint)(SrcRect.right + DeskDesc.DesktopCoordinates.left - OffsetX);
				Box.bottom = (uint)(SrcRect.bottom + DeskDesc.DesktopCoordinates.top - OffsetY);
				Box.back = 1;
				m_DeviceContext!.CopySubresourceRegion(m_MoveSurf!, 0, (uint)SrcRect.left, (uint)SrcRect.top, 0, SharedSurf, 0, Box);

				// Copy back to shared surface
				Box.left = (uint)SrcRect.left;
				Box.top = (uint)SrcRect.top;
				Box.front = 0;
				Box.right = (uint)SrcRect.right;
				Box.bottom = (uint)SrcRect.bottom;
				Box.back = 1;
				m_DeviceContext!.CopySubresourceRegion(SharedSurf, 0, (uint)(DestRect.left + DeskDesc.DesktopCoordinates.left - OffsetX),
					(uint)(DestRect.top + DeskDesc.DesktopCoordinates.top - OffsetY), 0, m_MoveSurf!, 0, Box);
			}
		}

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Sets up vertices for dirty rects for rotated desktops
	//
	static void SetDirtyVert([Out] Span<VERTEX> Vertices, in RECT Dirty, int OffsetX, int OffsetY, in DXGI_OUTPUT_DESC DeskDesc, in D3D11_TEXTURE2D_DESC FullDesc, in D3D11_TEXTURE2D_DESC ThisDesc)
	{
		int CenterX = (int)FullDesc.Width / 2;
		int CenterY = (int)FullDesc.Height / 2;

		int Width = DeskDesc.DesktopCoordinates.right - DeskDesc.DesktopCoordinates.left;
		int Height = DeskDesc.DesktopCoordinates.bottom - DeskDesc.DesktopCoordinates.top;

		// Rotation compensated destination rect
		RECT DestDirty = Dirty;

		// Set appropriate coordinates compensated for rotation
		switch (DeskDesc.Rotation)
		{
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE90:
				{
					DestDirty.left = Width - Dirty.bottom;
					DestDirty.top = Dirty.left;
					DestDirty.right = Width - Dirty.top;
					DestDirty.bottom = Dirty.right;

					Vertices[0].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					Vertices[1].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					Vertices[2].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					Vertices[5].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					break;
				}
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE180:
				{
					DestDirty.left = Width - Dirty.right;
					DestDirty.top = Height - Dirty.bottom;
					DestDirty.right = Width - Dirty.left;
					DestDirty.bottom = Height - Dirty.top;

					Vertices[0].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					Vertices[1].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					Vertices[2].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					Vertices[5].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					break;
				}
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_ROTATE270:
				{
					DestDirty.left = Dirty.top;
					DestDirty.top = Height - Dirty.right;
					DestDirty.right = Dirty.bottom;
					DestDirty.bottom = Height - Dirty.left;

					Vertices[0].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					Vertices[1].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					Vertices[2].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					Vertices[5].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					break;
				}
			default:
				Debug.Assert(false); // drop through
				goto case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_UNSPECIFIED;

			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_UNSPECIFIED:
			case DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_IDENTITY:
				{
					Vertices[0].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					Vertices[1].TexCoord = new XMFLOAT2(Dirty.left / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					Vertices[2].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.bottom / (float)(ThisDesc.Height));
					Vertices[5].TexCoord = new XMFLOAT2(Dirty.right / (float)(ThisDesc.Width), Dirty.top / (float)(ThisDesc.Height));
					break;
				}
		}

		// Set positions
		Vertices[0].Pos = new XMFLOAT3((DestDirty.left + DeskDesc.DesktopCoordinates.left - OffsetX - CenterX) / (float)(CenterX),
								 -1 * (DestDirty.bottom + DeskDesc.DesktopCoordinates.top - OffsetY - CenterY) / (float)(CenterY),
								 0.0f);
		Vertices[1].Pos = new XMFLOAT3((DestDirty.left + DeskDesc.DesktopCoordinates.left - OffsetX - CenterX) / (float)(CenterX),
								 -1 * (DestDirty.top + DeskDesc.DesktopCoordinates.top - OffsetY - CenterY) / (float)(CenterY),
								 0.0f);
		Vertices[2].Pos = new XMFLOAT3((DestDirty.right + DeskDesc.DesktopCoordinates.left - OffsetX - CenterX) / (float)(CenterX),
								 -1 * (DestDirty.bottom + DeskDesc.DesktopCoordinates.top - OffsetY - CenterY) / (float)(CenterY),
								 0.0f);
		Vertices[3].Pos = Vertices[2].Pos;
		Vertices[4].Pos = Vertices[1].Pos;
		Vertices[5].Pos = new XMFLOAT3((DestDirty.right + DeskDesc.DesktopCoordinates.left - OffsetX - CenterX) / (float)(CenterX),
								 -1 * (DestDirty.top + DeskDesc.DesktopCoordinates.top - OffsetY - CenterY) / (float)(CenterY),
								 0.0f);

		Vertices[3].TexCoord = Vertices[2].TexCoord;
		Vertices[4].TexCoord = Vertices[1].TexCoord;
	}

	//
	// Copies dirty rectangles
	//
	DUPL_RETURN CopyDirty([In] ID3D11Texture2D SrcSurface, [In, Out] ID3D11Texture2D SharedSurf, [In] RECT[] DirtyBuffer, int DirtyCount, int OffsetX, int OffsetY, in DXGI_OUTPUT_DESC DeskDesc)
	{
		HRESULT hr;

		SharedSurf.GetDesc(out var FullDesc);

		SrcSurface.GetDesc(out var ThisDesc);

		if (m_RTV is null)
		{
			hr = m_Device!.CreateRenderTargetView(SharedSurf, default, out m_RTV);
			if (hr.Failed)
			{
				return ProcessFailure(m_Device, "Failed to create render target view for dirty rects", "Error", hr, SystemTransitionsExpectedErrors);
			}
		}

		SafeCoTaskMemStruct<D3D11_SHADER_RESOURCE_VIEW_DESC> ShaderDesc = new D3D11_SHADER_RESOURCE_VIEW_DESC
		{
			Format = ThisDesc.Format,
			ViewDimension = D3D11_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2D,
			Texture2D = new() { MostDetailedMip = ThisDesc.MipLevels - 1, MipLevels = ThisDesc.MipLevels }
		};

		// Create new shader resource view
		hr = m_Device!.CreateShaderResourceView(SrcSurface, ShaderDesc, out var ShaderResource);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create shader resource view for dirty rects", "Error", hr, SystemTransitionsExpectedErrors);
		}

		float[] BlendFactor = [0.0f, 0.0f, 0.0f, 0.0f];
		m_DeviceContext!.OMSetBlendState(default, BlendFactor, 0xFFFFFFFF);
		m_DeviceContext.OMSetRenderTargets(1, [m_RTV!], default);
		m_DeviceContext.VSSetShader(m_VertexShader, default, 0);
		m_DeviceContext.PSSetShader(m_PixelShader, default, 0);
		m_DeviceContext.PSSetShaderResources(0, 1, [ShaderResource!]);
		m_DeviceContext.PSSetSamplers(0, 1, [m_SamplerLinear!]);
		m_DeviceContext.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

		// Create space for vertices for the dirty rects if the current space isn't large enough
		if (NUMVERTICES * DirtyCount > m_DirtyVertexBufferAlloc.Count)
			m_DirtyVertexBufferAlloc = new(NUMVERTICES * DirtyCount);

		// Fill them in
		Span<VERTEX> DirtyVertex = m_DirtyVertexBufferAlloc.AsSpan<VERTEX>(m_DirtyVertexBufferAlloc.Count);
		for (int i = 0; i < DirtyCount; ++i)
		{
			SetDirtyVert(DirtyVertex.Slice(i * NUMVERTICES, NUMVERTICES), DirtyBuffer[i], OffsetX, OffsetY, DeskDesc, FullDesc, ThisDesc);
		}

		// Create vertex buffer
		D3D11_BUFFER_DESC BufferDesc = new()
		{
			Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
			ByteWidth = (uint)(Marshal.SizeOf<VERTEX>() * NUMVERTICES * DirtyCount),
			BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
			CPUAccessFlags = 0
		};

		SafeCoTaskMemStruct<D3D11_SUBRESOURCE_DATA> InitData = new D3D11_SUBRESOURCE_DATA() { pSysMem = m_DirtyVertexBufferAlloc };

		hr = m_Device.CreateBuffer(BufferDesc, InitData, out var VertBuf);
		if (hr.Failed)
		{
			return ProcessFailure(m_Device, "Failed to create vertex buffer in dirty rect processing", "Error", hr, SystemTransitionsExpectedErrors);
		}
		uint Stride = (uint)Marshal.SizeOf<VERTEX>();
		uint Offset = 0;
		m_DeviceContext.IASetVertexBuffers(0, 1, [VertBuf!], [Stride], [Offset]);

		D3D11_VIEWPORT VP = new()
		{
			Width = (float)(FullDesc.Width),
			Height = (float)(FullDesc.Height),
			MinDepth = 0.0f,
			MaxDepth = 1.0f,
			TopLeftX = 0.0f,
			TopLeftY = 0.0f
		};
		m_DeviceContext.RSSetViewports(1, [VP]);

		m_DeviceContext.Draw((uint)(NUMVERTICES * DirtyCount), 0);

		return DUPL_RETURN.DUPL_RETURN_SUCCESS;
	}

	//
	// Clean all references
	//
	void CleanRefs()
	{
		m_DeviceContext = default;
		m_Device = default;
		m_MoveSurf = default;
		m_VertexShader = default;
		m_PixelShader = default;
		m_InputLayout = default;
		m_SamplerLinear = default;
		m_RTV = default;
	}
}