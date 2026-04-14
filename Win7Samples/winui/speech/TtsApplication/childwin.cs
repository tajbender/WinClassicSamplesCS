using Vanara.PInvoke;
using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.User32;

internal partial class CTTSApp
{
	// ---------------------------------------------------------------------------
	// ChildWndProc
	// ---------------------------------------------------------------------------
	// Description:         Main window procedure.
	// Arguments:
	//  HWND [in]           Window handle.
	//  UINT [in]           Message identifier.
	//  WPARAM [in]         Depends on message.
	//  LPARAM [in]         Depends on message.
	// Returns:
	//  LPARAM              Depends on message.
	internal static IntPtr ChildWndProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
	{
		// Call the appropriate message handler
		switch ((WindowMessage)uMsg)
		{
			case WindowMessage.WM_PAINT:
				{
					// Get the Display Context so we have someplace to blit.
					var hdc = BeginPaint(hWnd, out var ps);

					// Create a compatible DC.
					using var hmemDC = CreateCompatibleDC(hdc);

					GetClientRect(hWnd, out var rect);
					// Create a bitmap big enough for our client rectangle.
					using var hmemBMP = CreateCompatibleBitmap(hdc,
						rect.right - rect.left,
						rect.bottom - rect.top);

					// Select the bitmap into the off-screen DC.
					using var hobjOld = hmemDC.SelectObject(hmemBMP);

					// Erase the background.
					using (var hbrBkGnd = CreateSolidBrush(GetSysColor(SystemColorIndex.COLOR_3DFACE)))
						FillRect(hmemDC, rect, hbrBkGnd);

					// Draw into memory DC
					ImageList_Draw(g_hListBmp, 0, hmemDC, 0, 0, (IMAGELISTDRAWFLAGS)INDEXTOOVERLAYMASK(g_iBmp));
					if (g_iBmp % 6 == 2)
					{
						ImageList_Draw(g_hListBmp, WEYESNAR, hmemDC, 0, 0, 0);
					}
					if (g_iBmp % 6 == 5)
					{
						ImageList_Draw(g_hListBmp, WEYESCLO, hmemDC, 0, 0, 0);
					}


					// Blit to window DC
					StretchBlt(hdc, 0, 0, rect.right, rect.bottom,
						hmemDC, 0, 0, rect.right, rect.bottom, RasterOperationMode.SRCCOPY);

					// Clean up and get outta here
					EndPaint(hWnd, ps);
				}
				break;

			case WindowMessage.WM_DESTROY:
				// Delete Mouth Bitmaps
				ImageList_Destroy(g_hListBmp);
				break;
		}

		// Call the default message handler
		return DefWindowProc(hWnd, uMsg, wParam, lParam);
	}
}