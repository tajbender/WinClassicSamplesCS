using System.Diagnostics;
using Vanara.PInvoke;
using static Vanara.PInvoke.D2d1;
using static Vanara.PInvoke.DXGI;
using static Vanara.PInvoke.User32;

internal abstract class GraphicsScene
{
	protected float m_fScaleX = 1f;
	protected float m_fScaleY = 1f;

	// D2D Resources
	protected ID2D1Factory m_pFactory;
	protected ID2D1HwndRenderTarget? m_pRenderTarget;

	protected GraphicsScene()
	{
		m_pFactory = D2D1CreateFactory<ID2D1Factory>();
		CreateDeviceIndependentResources();
	}

	public void CleanUp()
	{
		DiscardDeviceDependentResources();
		DiscardDeviceIndependentResources();
	}

	public void Render(HWND hwnd)
	{
		HRESULT hr = CreateGraphicsResources(hwnd);
		if (hr.Failed)
		{
			return;
		}

		Debug.Assert(m_pRenderTarget is not null);

		try
		{
			m_pRenderTarget.BeginDraw();
			RenderScene();
			m_pRenderTarget.EndDraw(out _, out _);
		}
		catch
		{
			DiscardDeviceDependentResources();
			m_pRenderTarget = null;
		}
	}

	public HRESULT Resize(int x, int y)
	{
		HRESULT hr = HRESULT.S_OK;
		if (m_pRenderTarget is not null)
		{
			try
			{
				m_pRenderTarget.Resize(SizeU((uint)x, (uint)y));
				CalculateLayout();
			}
			catch (Exception ex)
			{
				hr = ex.HResult;
			}
		}
		return hr;
	}

	protected virtual void CalculateLayout() { }

	protected virtual HRESULT CreateDeviceDependentResources() => HRESULT.S_OK;

	protected virtual HRESULT CreateDeviceIndependentResources() => HRESULT.S_OK;

	protected HRESULT CreateGraphicsResources(HWND hwnd)
	{
		if (m_pRenderTarget is null)
		{
			try
			{
				GetClientRect(hwnd, out var rc);

				D2D_SIZE_U size = new((uint)rc.right, (uint)rc.bottom);
				m_pRenderTarget = m_pFactory!.CreateHwndRenderTarget(RenderTargetProperties(), HwndRenderTargetProperties(hwnd, size));

				CreateDeviceDependentResources();

				CalculateLayout();
			}
			catch (Exception ex)
			{
				m_pRenderTarget = null;
				return ex.HResult;
			}
		}
		return HRESULT.S_OK;
	}

	protected virtual void DiscardDeviceDependentResources() { }

	protected virtual void DiscardDeviceIndependentResources() { }

	protected T PixelToDipX<T>(T pixels) where T : unmanaged, IConvertible =>
		(T)Convert.ChangeType(pixels.ToSingle(null) / m_fScaleX, typeof(T));

	protected T PixelToDipY<T>(T pixels) where T : unmanaged, IConvertible =>
		(T)Convert.ChangeType(pixels.ToSingle(null) / m_fScaleY, typeof(T));

	protected abstract void RenderScene();
}