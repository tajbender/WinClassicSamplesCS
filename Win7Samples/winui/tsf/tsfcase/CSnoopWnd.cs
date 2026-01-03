using Vanara.PInvoke;
using static Vanara.PInvoke.MSCTF;

namespace tsfcase;

internal class CSnoopWnd(CCaseTextService pCase)
{
	private static readonly ushort atomWndClass;
	private readonly string achText;
	private readonly uint cchText;
	private readonly HWND hWnd;

	public static bool InitClass() => throw new NotImplementedException();

	public static void UninitClass() => throw new NotImplementedException();

	public void Hide() => throw new NotImplementedException();

	public bool Init() => throw new NotImplementedException();

	public void Show() => throw new NotImplementedException();

	public void Uninit() => throw new NotImplementedException();

	public void UpdateText(ITfRange pRange) => throw new NotImplementedException();

	public void UpdateText(uint ec, ITfContext pContext, ITfRange pRange) => throw new NotImplementedException();
}