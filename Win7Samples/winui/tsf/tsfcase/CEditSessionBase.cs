using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.MSCTF;

namespace tsfcase;

internal class CEditSessionBase(MSCTF.ITfContext pContext) : ITfEditSession
{
	protected ITfContext pContext = pContext;

	public virtual HRESULT DoEditSession([In] uint ec) => HRESULT.S_OK;
}