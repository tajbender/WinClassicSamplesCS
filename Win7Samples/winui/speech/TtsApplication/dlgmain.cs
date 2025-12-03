using System.Diagnostics;
using System.Text.RegularExpressions;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.ComDlg32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Macros;
using static Vanara.PInvoke.MsftEdit;
using static Vanara.PInvoke.SpeechApi;
using static Vanara.PInvoke.User32;

internal partial class CTTSApp
{
	// ---------------------------------------------------------------------------
	// DlgProcMain
	// ---------------------------------------------------------------------------
	// Description:         Main window procedure.
	// Arguments:
	//  HWND [in]           Window handle.
	//  UINT [in]           Message identifier.
	//  WPARAM [in]         Depends on message.
	//  LPARAM [in]         Depends on message.
	// Returns:
	//  LPARAM              Depends on message.
	protected override nint DialogProc(HWND hwnd, uint uMsg, nint wParam, nint lParam)
	{
		// Call the appropriate function to handle the messages
		switch ((WindowMessage)uMsg)
		{
			case WM_TTSAPPCUSTOMEVENT:
				MainHandleSynthEvent();
				return 1;

			case WindowMessage.WM_HSCROLL:
				HandleScroll((HWND)lParam);
				return 1;
		}

		return 0;
	}

	/////////////////////////////////////////////////////////////////
	protected override nint OnClosing()
	/////////////////////////////////////////////////////////////////
	{
		// Call helper functions from sphelper.h to destroy combo boxes
		// created with SpInitTokenComboBox
		SpDestroyTokenComboBox(GetDlgItem(Handle, IDC_COMBO_VOICES));

		// Return success
		return 0;
	}

	/////////////////////////////////////////////////////////////////
	protected override void OnCommand(int id, HWND hControl, uint codeNotify)
	/////////////////////////////////////////////////////////////////
	//
	// Handle each of the WM_COMMAND messages that come in, and deal with
	// them appropriately
	//
	{
		if (!initialized) return;

		HRESULT hr = HRESULT.S_OK;
		SafeLPTSTR szAFileName = new("", MAX_PATH);

		// Get handle to the main edit box
		var hwndEdit = GetDlgItem(Handle, IDE_EDITBOX);

		switch (id)
		{
			// About Box display
			case IDC_ABOUT:
				DialogBox(LibHandle, IDD_ABOUT, Handle, About);
				break;

			// Any change to voices is sent to VoiceChange() function
			case IDC_COMBO_VOICES:
				if (codeNotify == (uint)ComboBoxNotification.CBN_SELCHANGE)
				{
					hr = VoiceChange();
				}

				if (hr.Failed)
				{
					TTSAppStatusMessage(Handle, "Error changing voices\r\n");
				}

				break;

			// If user wants to speak a file pop the standard windows open file
			// dialog box and load the text into a global buffer (m_pszwFileText)
			// which will be used when the user hits speak.
			case IDB_OPEN:
				var bFileOpened = CallOpenFileDialog(szAFileName, "TXT (*.txt)\0*.txt\0XML (*.xml)\0*.xml\0All Files (*.*)\0*.*\0");
				if (bFileOpened)
				{
					m_szWFileName = szAFileName;
					ReadTheFile(m_szWFileName!, out var bIsUnicode, out m_pszwFileText);

					if (bIsUnicode)
					{
						// Unicode source
						UpdateEditCtlW(m_pszwFileText!);
					}
					else
					{
						// MBCS source
						string? pszFileText = m_pszwFileText;

						if (pszFileText is not null)
						{
							SetDlgItemText(Handle, IDE_EDITBOX, pszFileText);
						}
					}
				}
				else
				{
					m_szWFileName = "";
				}
				// Always SetFocus back to main edit window so text highlighting will work
				SetFocus(hwndEdit);
				break;

			// Handle speak
			case IDB_SPEAK:
				HandleSpeak();
				break;

			case IDB_PAUSE:
				if (!m_bStop)
				{
					if (!m_bPause)
					{
						SetWindowText(GetDlgItem(Handle, IDB_PAUSE), "Resume");
						// Pause the voice...
						m_cpVoice!.Pause();
						m_bPause = true;
						TTSAppStatusMessage(Handle, "Pause\r\n");
					}
					else
					{
						SetWindowText(GetDlgItem(Handle, IDB_PAUSE), "Pause");
						m_cpVoice!.Resume();
						m_bPause = false;
					}
				}
				SetFocus(hwndEdit);
				break;

			case IDB_STOP:
				TTSAppStatusMessage(Handle, "Stop\r\n");
				// Set the global audio state to stop
				Stop();
				SetFocus(hwndEdit);
				break;

			case IDB_SKIP:
				{
					SetFocus(hwndEdit);
					uint SkipNum = GetDlgItemInt(Handle, IDC_SKIP_EDIT, out var fSuccess, true);
					uint ulGarbage = 0;
					string szGarbage = "Sentence";
					if (fSuccess)
					{
						TTSAppStatusMessage(Handle, "Skip\r\n");
						m_cpVoice!.Skip(szGarbage, (int)SkipNum, out ulGarbage);
					}
					else
					{
						TTSAppStatusMessage(Handle, "Skip failed\r\n");
					}
				}
				break;

			case IDE_EDITBOX:
				// Set the global audio state to stop if user has changed contents of edit control
				if (codeNotify == (uint)EditNotification.EN_CHANGE)
				{
					Stop();
				}
				break;

			case IDB_SPEAKWAV:
				var bWavFileOpened = CallOpenFileDialog(szAFileName, "WAV (*.wav)\0*.wav\0All Files (*.*)\0*.*\0");
				// Speak the wav file using SpeakStream
				if (bWavFileOpened)
				{
					string szwWavFileName = szAFileName!;
					unsafe
					{
						// User helper function found in sphelper.h to open the wav file and
						// get back an IStream pointer to pass to SpeakStream
						hr = SPBindToFile(szwWavFileName, SPFILEMODE.SPFM_OPEN_READONLY, out var cpWavStream);

						if (hr.Succeeded)
						{
							hr = GetHR(() => m_cpVoice!.SpeakStream(cpWavStream, SPEAKFLAGS.SPF_ASYNC, out _));
						}

						if (hr.Failed)
						{
							TTSAppStatusMessage(Handle, "Speak error\r\n");
						}
					}
				}
				break;

			// Reset all values to defaults
			case IDB_RESET:
				TTSAppStatusMessage(Handle, "Reset\r\n");
				SendDlgItemMessage(Handle, IDC_VOLUME_SLIDER, TrackBarMessage.TBM_SETPOS, true, m_DefaultVolume);
				SendDlgItemMessage(Handle, IDC_RATE_SLIDER, TrackBarMessage.TBM_SETPOS, true, m_DefaultRate);
				SendDlgItemMessage(Handle, IDC_SAVETOWAV, ButtonMessage.BM_SETCHECK, ButtonStateFlags.BST_UNCHECKED, 0);
				SendDlgItemMessage(Handle, IDC_EVENTS, ButtonMessage.BM_SETCHECK, ButtonStateFlags.BST_UNCHECKED, 0);
				SetDlgItemText(Handle, IDE_EDITBOX, "Enter text you wish spoken here.");

				// reset output format
				SendDlgItemMessage(Handle, IDC_COMBO_OUTPUT, ComboBoxMessage.CB_SETCURSEL, m_DefaultFormatIndex, 0);
				SendMessage(Handle, WindowMessage.WM_COMMAND, MAKELPARAM(IDC_COMBO_OUTPUT, ComboBoxNotification.CBN_SELCHANGE), 0);

				// Change the volume and the rate to reflect what the UI says
				HandleScroll(GetDlgItem(Handle, IDC_VOLUME_SLIDER));
				HandleScroll(GetDlgItem(Handle, IDC_RATE_SLIDER));

				SetFocus(hwndEdit);
				break;

			case IDC_COMBO_OUTPUT:
				if (codeNotify == (uint)ComboBoxNotification.CBN_SELCHANGE)
				{
					// Get the audio output format and set it's Guid
					var iFormat = SendDlgItemMessage(Handle, IDC_COMBO_OUTPUT, ComboBoxMessage.CB_GETCURSEL, 0, 0);
					SPSTREAMFORMAT eFmt = (SPSTREAMFORMAT)(int)SendDlgItemMessage(Handle, IDC_COMBO_OUTPUT,
						ComboBoxMessage.CB_GETITEMDATA, iFormat, 0);
					CSpStreamFormat Fmt = new();
					Fmt.AssignFormat(eFmt);
					if (m_cpOutAudio is not null)
					{
						unsafe { hr = GetHR(() => m_cpOutAudio.SetFormat(Fmt.FormatId(), Fmt.WaveFormatExPtr())); }
					}
					else
					{
						hr = HRESULT.E_FAIL;
					}

					if (hr.Succeeded)
					{
						hr = GetHR(() => m_cpVoice!.SetOutput(m_cpOutAudio!, false));
					}

					if (hr.Failed)
					{
						TTSAppStatusMessage(Handle, "Format rejected\r\n");
					}

					EnableSpeakButtons(hr.Succeeded);
				}
				break;

			case IDC_SAVETOWAV:
				{
					bFileOpened = CallSaveFileDialog(szAFileName, "WAV (*.wav)\0*.wav\0All Files (*.*)\0*.*\0");

					if (bFileOpened == false) break;

					m_szWFileName = szAFileName;

					ISpStreamFormat? cpOldStream = null;
					CSpStreamFormat OriginalFmt = new();
					hr = GetHR(() => m_cpVoice!.GetOutputStream(out cpOldStream));
					if (hr == HRESULT.S_OK)
					{
						hr = OriginalFmt.AssignFormat(cpOldStream!);
					}
					else
					{
						hr = HRESULT.E_FAIL;
					}
					// User SAPI helper function in sphelper.h to create a wav file
					ISpStream? cpWavStream = null;
					if (hr.Succeeded)
					{
						unsafe
						{
							hr = SPBindToFile(m_szWFileName!, SPFILEMODE.SPFM_CREATE_ALWAYS, out cpWavStream, OriginalFmt.FormatId(),
								OriginalFmt.WaveFormatExPtr());
						}
					}
					if (hr.Succeeded)
					{
						// Set the voice's output to the wav file instead of the speakers
						hr = GetHR(() => m_cpVoice!.SetOutput(cpWavStream!, true));
					}

					if (hr.Succeeded)
					{
						// Do the Speak
						HandleSpeak();
					}

					// Set output back to original stream
					// Wait until the speak is finished if saving to a wav file so that
					// the smart pointer cpWavStream doesn't get released before its
					// finished writing to the wav.
					m_cpVoice!.WaitUntilDone(INFINITE);
					cpWavStream = null!;

					// Reset output
					m_cpVoice.SetOutput(cpOldStream!, false);

					if (hr.Succeeded)
					{
						var szConfString = LoadString(LibHandle, IDS_SAVE_NOTIFY);
						var szTitle = LoadString(LibHandle, IDS_NOTIFY_TITLE);
						MessageBox(Handle, szConfString!, szTitle, MB_FLAGS.MB_OK | MB_FLAGS.MB_ICONINFORMATION);
					}
					else
					{
						var szConfString = LoadString(LibHandle, IDS_SAVE_ERROR);
						MessageBox(Handle, szConfString!, default, MB_FLAGS.MB_ICONEXCLAMATION);
					}
				}
				break;
		}
	}

	private bool initialized = false;

	/////////////////////////////////////////////////////////////////
	protected override bool OnInitDialog()
	/////////////////////////////////////////////////////////////////
	{
		HRESULT hr = HRESULT.S_OK;
		initialized = true;

		// Add some default text to the main edit control
		SetDlgItemText(Handle, IDE_EDITBOX, "Enter text you wish spoken here.");

		// Set the event mask in the rich edit control so that it notifies us when text is
		// changed in the control
		SendMessage(GetDlgItem(Handle, IDE_EDITBOX), RichEditMessage.EM_SETEVENTMASK, 0, ENM.ENM_CHANGE);

		// Initialize the Output Format combo box
		int i;
		for (i = 0; i < NUM_OUTPUTFORMATS; i++)
		{
			SendDlgItemMessage(Handle, IDC_COMBO_OUTPUT, ComboBoxMessage.CB_ADDSTRING, 0, g_aszOutputFormat[i]);
			SendDlgItemMessage(Handle, IDC_COMBO_OUTPUT, ComboBoxMessage.CB_SETITEMDATA, i, (int)g_aOutputFormat[i]);
		}

		if (m_cpVoice is null)
		{
			hr = HRESULT.E_FAIL;
		}

		// Set the default output format as the current selection.
		if (hr.Succeeded)
		{
			try
			{
				m_cpVoice!.GetOutputStream(out var cpStream);

				CSpStreamFormat Fmt = new();
				hr = Fmt.AssignFormat(cpStream);
				if (hr.Succeeded)
				{
					SPSTREAMFORMAT eFmt = Fmt.ComputeFormatEnum();
					for (i = 0; i < NUM_OUTPUTFORMATS; i++)
					{
						if (g_aOutputFormat[i] == eFmt)
						{
							m_DefaultFormatIndex = i;
							SendDlgItemMessage(Handle, IDC_COMBO_OUTPUT, ComboBoxMessage.CB_SETCURSEL, m_DefaultFormatIndex, 0);
						}
					}
				}
			}
			catch
			{
				SendDlgItemMessage(Handle, IDC_COMBO_OUTPUT, ComboBoxMessage.CB_SETCURSEL, 0, 0);
			}
		}

		// Use the SAPI5 helper function in sphelper.h to initialize the Voice combo box.
		if (hr.Succeeded)
		{
			hr = SpInitTokenComboBox(GetDlgItem(Handle, IDC_COMBO_VOICES), SPCAT_VOICES);
		}

		if (hr.Succeeded)
		{
			SpCreateDefaultObjectFromCategoryId(SPCAT_AUDIOOUT, out m_cpOutAudio);
		}

		// Set default voice data 
		VoiceChange();

		// Set Range for Skip Edit Box...
		SendDlgItemMessage(Handle, IDC_SKIP_SPIN, UpDownMessage.UDM_SETRANGE, true, (IntPtr)MAKELONG(50, -50));

		// Set the notification message for the voice
		if (hr.Succeeded)
		{
			m_cpVoice!.SetNotifyWindowMessage(Handle, (uint)WM_TTSAPPCUSTOMEVENT, 0, 0);
		}

		// We're interested in all TTS events
		if (hr.Succeeded)
		{
			m_cpVoice!.SetInterest(SPEVENTENUM.SPFEI_ALL_TTS_EVENTS, SPEVENTENUM.SPFEI_ALL_TTS_EVENTS);
		}

		// Get default rate and volume
		if (hr.Succeeded)
		{
			m_cpVoice!.GetRate(out m_DefaultRate);
			// initialize sliders and edit boxes with default rate
			if (hr.Succeeded)
			{
				SendDlgItemMessage(Handle, IDC_RATE_SLIDER, TrackBarMessage.TBM_SETRANGE, true, (int)MAKELONG(SPVLIMITS.SPMIN_RATE, SPVLIMITS.SPMAX_RATE));
				SendDlgItemMessage(Handle, IDC_RATE_SLIDER, TrackBarMessage.TBM_SETPOS, true, m_DefaultRate);
				SendDlgItemMessage(Handle, IDC_RATE_SLIDER, TrackBarMessage.TBM_SETPAGESIZE, true, 5);
			}
		}

		if (hr.Succeeded)
		{
			m_cpVoice!.GetVolume(out m_DefaultVolume);
			// initialize sliders and edit boxes with default volume
			if (hr.Succeeded)
			{
				SendDlgItemMessage(Handle, IDC_VOLUME_SLIDER, TrackBarMessage.TBM_SETRANGE, true, (int)MAKELONG(SPVLIMITS.SPMIN_VOLUME, SPVLIMITS.SPMAX_VOLUME));
				SendDlgItemMessage(Handle, IDC_VOLUME_SLIDER, TrackBarMessage.TBM_SETPOS, true, m_DefaultVolume);
				SendDlgItemMessage(Handle, IDC_VOLUME_SLIDER, TrackBarMessage.TBM_SETPAGESIZE, true, 10);
			}
		}

		// If any SAPI initialization failed, shut down!
		if (hr.Failed)
		{
			MessageBox(default, "Error initializing speech objects. Shutting down.", "Error", MB_FLAGS.MB_OK);
			SendMessage(Handle, WindowMessage.WM_CLOSE);
			return (false);

		}
		else
		{
			//
			// Create the child windows to which we'll blit our result
			//
			HWND hCharWnd = GetDlgItem(Handle, IDC_CHARACTER);

			GetClientRect(hCharWnd, out var rc);
			rc.left = (rc.right - CHARACTER_WIDTH) / 2;
			rc.top = (rc.bottom - CHARACTER_HEIGHT) / 2;
			m_hChildWnd = CreateWindow(CHILD_CLASS, default, WindowStyles.WS_CHILDWINDOW | WindowStyles.WS_VISIBLE,
				rc.left, rc.top,
				rc.left + CHARACTER_WIDTH, rc.top + CHARACTER_HEIGHT,
				hCharWnd, default, LibHandle, default);

			if (!m_hChildWnd)
			{
				MessageBox(Handle, "Error initializing speech objects. Shutting down.", "Error", MB_FLAGS.MB_OK);
				SendMessage(Handle, WindowMessage.WM_CLOSE);
				return (false);

			}
			else
			{
				// Load Mouth Bitmaps and use and ImageList since we'll blit the mouth
				// and eye positions over top of the full image
				g_hListBmp = InitImageList();
			}
		}
		return (true);
	}

	private static HRESULT GetHR(Action action)
	{
		try { action(); return HRESULT.S_OK; }
		catch (Exception ex) { return ex.HResult; }
	}

	/*****************************************************************************************
	* About() *
	*---------*
	*   Description:
	*       Message handler for the "About" box.
	******************************************************************************************/
	IntPtr About(HWND hDlg, uint message, IntPtr wParam, IntPtr lParam)
	{
		switch (message)
		{
			case (uint)WindowMessage.WM_COMMAND:
				{
					var wId = (MB_RESULT)LOWORD(wParam);

					switch (wId)
					{
						case MB_RESULT.IDOK:
						case MB_RESULT.IDCANCEL:
							EndDialog(hDlg, LOWORD(wParam));
							return 1;
					}
				}
				break;
		}
		return 0;
	}

	/////////////////////////////////////////////////////////////////
	bool CallOpenFileDialog(SafeLPTSTR szFileName, string szFilter)
	/////////////////////////////////////////////////////////////////
	//
	// Display the open dialog box to retrieve the user-selected 
	// .txt or .xml file for synthisizing
	{
		bool bRetVal = true;
		string szPath = "";

		// Open the last directory used by this app (stored in registry)
		var lRetVal = RegCreateKeyEx(HKEY.HKEY_CURRENT_USER, "SOFTWARE\\Microsoft\\Speech\\AppData\\TTSApp", 0, default, 0,
			REGSAM.KEY_ALL_ACCESS, default, out var hkResult, out _);
		if (lRetVal == Win32Error.ERROR_SUCCESS)
		{
			szPath = RegQueryValueEx(hkResult, "TTSFiles") as string ?? "";
		}

		var ofn = new OPENFILENAME
		{
			lStructSize = (uint)Marshal.SizeOf(typeof(OPENFILENAME)),
			hwndOwner = Handle,
			lpstrFilter = szFilter,
			nFilterIndex = 1,
			lpstrInitialDir = szPath,
			lpstrFile = szFileName,
			nMaxFile = (uint)szFileName.Capacity,
			Flags = OFN.OFN_FILEMUSTEXIST | OFN.OFN_READONLY | OFN.OFN_PATHMUSTEXIST | OFN.OFN_HIDEREADONLY
		};

		// Pop the dialog
		bRetVal = GetOpenFileName(ref ofn);

		// Write the directory path you're in to the registry
		string? pathstr = System.IO.Path.GetDirectoryName(szFileName);

		// Now write the string to the registry
		RegSetValueEx(hkResult, "TTSFiles", default, REG_VALUE_TYPE.REG_EXPAND_SZ, pathstr!, (uint)StringHelper.GetByteCount(pathstr));

		RegCloseKey(hkResult);

		return bRetVal;
	}

	/////////////////////////////////////////////////////////////////
	bool CallSaveFileDialog(SafeLPTSTR szFileName, string szFilter)
	/////////////////////////////////////////////////////////////////
	//
	// Display the save dialog box to save the wav file
	{
		// Open the last directory used by this app (stored in registry)
		var lRetVal = RegCreateKeyEx(HKEY.HKEY_CLASSES_ROOT, "PathTTSDataFiles", 0, default, 0,
			REGSAM.KEY_ALL_ACCESS, default, out var hkResult, out _);

		string szPath = "";
		if (lRetVal == Win32Error.ERROR_SUCCESS)
		{
			szPath = RegQueryValueEx(hkResult, "TTSFiles") as string ?? "";
			hkResult.Dispose();
		}

		OPENFILENAME ofn = new()
		{
			lStructSize = (uint)Marshal.SizeOf<OPENFILENAME>(),
			hwndOwner = Handle,
			lpstrFilter = szFilter,
			lpstrCustomFilter = default,
			nFilterIndex = 1,
			lpstrInitialDir = szPath,
			lpstrFile = szFileName,
			nMaxFile = (uint)szFileName.Capacity,
			lpstrTitle = default,
			lpstrFileTitle = default,
			lpstrDefExt = "wav",
			Flags = OFN.OFN_OVERWRITEPROMPT
		};

		// Pop the dialog
		var bRetVal = GetSaveFileName(ref ofn);

		// Write the directory path you're in to the registry
		string pathstr = szFileName.ToString() ?? "";

		if (ofn.Flags.IsFlagSet(OFN.OFN_EXTENSIONDIFFERENT))
		{
			pathstr += ".wav";
		}

		pathstr = System.IO.Path.GetDirectoryName(pathstr) ?? "";

		// Now write the string to the registry
		lRetVal = RegCreateKeyEx(HKEY.HKEY_CLASSES_ROOT, "PathTTSDataFiles", 0, default, 0,
			REGSAM.KEY_ALL_ACCESS, default, out hkResult, out _);

		if (lRetVal == Win32Error.ERROR_SUCCESS)
		{
			RegSetValueEx(hkResult, "TTSFiles", default, REG_VALUE_TYPE.REG_EXPAND_SZ, pathstr, (uint)StringHelper.GetByteCount(pathstr));

			RegCloseKey(hkResult);
		}

		return bRetVal;
	}

	/////////////////////////////////////////////////////////////////////////
	void EnableSpeakButtons(bool fEnable)
	/////////////////////////////////////////////////////////////////////////
	{
		EnableWindow(GetDlgItem(Handle, IDB_SPEAK), fEnable);
		EnableWindow(GetDlgItem(Handle, IDB_PAUSE), fEnable);
		EnableWindow(GetDlgItem(Handle, IDB_STOP), fEnable);
		EnableWindow(GetDlgItem(Handle, IDB_SKIP), fEnable);
		EnableWindow(GetDlgItem(Handle, IDB_SPEAKWAV), fEnable);
		EnableWindow(GetDlgItem(Handle, IDC_SAVETOWAV), fEnable);
	}

	/////////////////////////////////////////////////////////////////////////
	void HandleScroll(HWND hCtl)
	/////////////////////////////////////////////////////////////////////////
	{
		HRESULT hr = HRESULT.S_OK;

		// Get the current position of the slider
		var hpos = (int)SendMessage(hCtl, TrackBarMessage.TBM_GETPOS);

		// Get the Handle for the scroll bar so it can be associated with an edit box
		var hVolume = GetDlgItem(Handle, IDC_VOLUME_SLIDER);
		var hRate = GetDlgItem(Handle, IDC_RATE_SLIDER);

		if (hCtl == hVolume)
		{
			hr = GetHR(() => m_cpVoice!.SetVolume((ushort)hpos));
		}
		else if (hCtl == hRate)
		{
			hr = GetHR(() => m_cpVoice!.SetRate(hpos));
		}

		if (hr.Failed)
		{
			TTSAppStatusMessage(Handle, "Error setting volume / rate\r\n");
		}

		return;
	}

	/////////////////////////////////////////////////////////////////////////////////////////////
	void HandleSpeak()
	/////////////////////////////////////////////////////////////////////////////////////////////
	{
		HRESULT hr = HRESULT.S_OK;

		// Get handle to the main edit box
		var hwndEdit = GetDlgItem(Handle, IDE_EDITBOX);

		TTSAppStatusMessage(Handle, "Speak\r\n");
		SetFocus(hwndEdit);
		m_bStop = false;

		// only get the string if we're not paused
		if (!m_bPause)
		{
			// Find the length of the string in the buffer
			GETTEXTLENGTHEX gtlx = new() { codepage = 1200, flags = GTL.GTL_DEFAULT };
			IntPtr lTextLen = SendDlgItemMessage(Handle, IDE_EDITBOX, RichEditMessage.EM_GETTEXTLENGTHEX, gtlx, 0);
			using SafeLPWSTR szWTextString = new((int)lTextLen + 1);

			GETTEXTEX GetText = new()
			{
				cb = szWTextString.Size,
				codepage = 1200,
				flags = GT.GT_DEFAULT,
				lpDefaultChar = null,
				lpUsedDefChar = default
			};

			// Get the string in a unicode buffer
			SendDlgItemMessage(Handle, IDE_EDITBOX, RichEditMessage.EM_GETTEXTEX, GetText, (IntPtr)szWTextString);

			// do we speak or interpret the XML
			hr = GetHR(() => m_cpVoice!.Speak(szWTextString, SPEAKFLAGS.SPF_ASYNC | ((IsDlgButtonChecked(Handle, IDC_SPEAKXML) != 0) ? SPEAKFLAGS.SPF_IS_XML : SPEAKFLAGS.SPF_IS_NOT_XML), out _));

			if (hr.Failed)
			{
				TTSAppStatusMessage(Handle, "Speak error\r\n");
			}
		}
		m_bPause = false;
		SetWindowText(GetDlgItem(Handle, IDB_PAUSE), "Pause");
		SetFocus(hwndEdit);
		// Set state to run
		hr = GetHR(() => m_cpVoice!.Resume());

		if (hr.Failed)
		{
			TTSAppStatusMessage(Handle, "Speak error\r\n");
		}
	}

	/////////////////////////////////////////////////////////////////
	SafeHIMAGELIST InitImageList()
	/////////////////////////////////////////////////////////////////
	{
		var hListBmp = ImageList_Create(CHARACTER_WIDTH, CHARACTER_HEIGHT, ILC.ILC_COLOR32 | ILC.ILC_MASK, 1, 0);
		if (hListBmp)
		{
			using (var hBmp = LoadBitmap(LibHandle, IDB_MICFULL))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH2))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH3))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH4))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH5))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH6))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH7))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH8))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH9))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH10))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH11))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH12))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICMOUTH13))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICEYESNAR))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			using (var hBmp = LoadBitmap(LibHandle, IDB_MICEYESCLO))
				ImageList_AddMasked(hListBmp, hBmp, 0xff00ff);

			ImageList_SetOverlayImage(hListBmp, 1, 1);
			ImageList_SetOverlayImage(hListBmp, 2, 2);
			ImageList_SetOverlayImage(hListBmp, 3, 3);
			ImageList_SetOverlayImage(hListBmp, 4, 4);
			ImageList_SetOverlayImage(hListBmp, 5, 5);
			ImageList_SetOverlayImage(hListBmp, 6, 6);
			ImageList_SetOverlayImage(hListBmp, 7, 7);
			ImageList_SetOverlayImage(hListBmp, 8, 8);
			ImageList_SetOverlayImage(hListBmp, 9, 9);
			ImageList_SetOverlayImage(hListBmp, 10, 10);
			ImageList_SetOverlayImage(hListBmp, 11, 11);
			ImageList_SetOverlayImage(hListBmp, 12, 12);
			ImageList_SetOverlayImage(hListBmp, 13, 13);
			ImageList_SetOverlayImage(hListBmp, 14, WEYESNAR);
			ImageList_SetOverlayImage(hListBmp, 15, WEYESCLO);
		}
		return hListBmp;
	}

	/////////////////////////////////////////////////////////////////
	internal static void InitSapi()
	/////////////////////////////////////////////////////////////////
	{
		m_cpVoice = (ISpVoice)new SpVoice();
	}

	/////////////////////////////////////////////////////////////////
	void MainHandleSynthEvent()
	/////////////////////////////////////////////////////////////////
	//
	// Handles the WM_TTSAPPCUSTOMEVENT application defined message and all
	// of it's appropriate SAPI5 events.
	//
	{
		using CSpEvent evt = new(); // helper class in sphelper.h for events that releases any 
									// allocated memory in it's destructor - SAFER than SPEVENT
		IntPtr nStart;
		IntPtr nEnd;
		HRESULT hr = HRESULT.S_OK;

		while (evt.GetFrom(m_cpVoice!) == HRESULT.S_OK)
		{
			switch (evt.spEvent.eEventId)
			{
				case SPEVENTENUM.SPEI_START_INPUT_STREAM:
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "StartStream event\r\n");
					}
					break;

				case SPEVENTENUM.SPEI_END_INPUT_STREAM:
					// Set global boolean stop to true when finished speaking
					m_bStop = true;
					// Highlight entire text
					nStart = 0;
					nEnd = SendDlgItemMessage(Handle, IDE_EDITBOX, WindowMessage.WM_GETTEXTLENGTH, 0, 0);
					SendDlgItemMessage(Handle, IDE_EDITBOX, EditMessage.EM_SETSEL, nStart, nEnd);
					// Mouth closed
					g_iBmp = 0;
					InvalidateRect(m_hChildWnd, default, false);
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "EndStream event\r\n");
					}
					break;

				case SPEVENTENUM.SPEI_VOICE_CHANGE:
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "Voicechange event\r\n");
					}
					break;

				case SPEVENTENUM.SPEI_TTS_BOOKMARK:
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						// Get the string associated with the bookmark
						// and add the default terminator.
						string szBuff2 = $"Bookmark event: {evt.String()}\r\n";
						TTSAppStatusMessage(Handle, szBuff2);
					}
					break;

				case SPEVENTENUM.SPEI_WORD_BOUNDARY:
					SPVOICESTATUS Stat = default;
					hr = GetHR(() => m_cpVoice!.GetStatus(out Stat, out _));
					if (hr.Failed)
					{
						TTSAppStatusMessage(Handle, "Voice GetStatus error\r\n");
					}

					// Highlight word
					nStart = (IntPtr)(Stat.ulInputWordPos / Marshal.SizeOf(typeof(byte)));
					nEnd = nStart.Offset(Stat.ulInputWordLen);
					SendDlgItemMessage(Handle, IDE_EDITBOX, EditMessage.EM_SETSEL, nStart, nEnd);
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "Wordboundary event\r\n");
					}
					break;

				case SPEVENTENUM.SPEI_PHONEME:
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "Phoneme event\r\n");
					}
					break;

				case SPEVENTENUM.SPEI_VISEME:
					// Get the current mouth viseme position and map it to one of the 
					// 7 mouth bitmaps. 
					g_iBmp = g_aMapVisemeToImage[(int)evt.Viseme()]; // current viseme

					InvalidateRect(m_hChildWnd, default, false);
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "Viseme event\r\n");
					}
					break;

				case SPEVENTENUM.SPEI_SENTENCE_BOUNDARY:
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "Sentence event\r\n");
					}
					break;

				case SPEVENTENUM.SPEI_TTS_AUDIO_LEVEL:
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						string wszBuff = $"Audio level: {evt.spEvent.wParam.ToInt32()}\r\n";
						TTSAppStatusMessage(Handle, wszBuff);
					}
					break;

				case SPEVENTENUM.SPEI_TTS_PRIVATE:
					if (IsDlgButtonChecked(Handle, IDC_EVENTS) != 0)
					{
						TTSAppStatusMessage(Handle, "Private engine event\r\n");
					}
					break;

				default:
					TTSAppStatusMessage(Handle, "Unknown message\r\n");
					break;
			}
		}
	}

	/////////////////////////////////////////////////////
	HRESULT ReadTheFile(string szFileName, out bool bIsUnicode, out string? ppszwBuff)
	/////////////////////////////////////////////////////
	//
	// This file opens and reads the contents of a file. It
	// returns a pointer to the string.
	// Warning, this function allocates memory for the string on 
	// the heap so the caller must free it with 'delete'.
	//
	{
		// Open up the file and copy it's contents into a buffer to return
		bIsUnicode = false;
		ppszwBuff = null;
		uint dwSize = 0;
		uint dwBytesRead = 0;

		// First delete any memory previously allocated by this function
		if (m_pszwFileText is not null)
		{
			m_pszwFileText = null;
		}

		using var hFile = CreateFile(szFileName, FileAccess.GENERIC_READ, FILE_SHARE.FILE_SHARE_READ, default, CreationOption.OPEN_EXISTING,
			FileFlagsAndAttributes.FILE_ATTRIBUTE_HIDDEN | FileFlagsAndAttributes.FILE_ATTRIBUTE_READONLY, default);
		HRESULT hr = 0;
		if (hFile.IsInvalid)
		{
			hr = Win32Error.GetLastError().ToHRESULT();
		}

		if (hr.Succeeded)
		{
			dwSize = GetFileSize(hFile, out _);
			if (dwSize == INVALID_FILE_SIZE)
			{
				hr = Win32Error.GetLastError().ToHRESULT();
			}
		}

		if (hr.Succeeded)
		{
			// Read the file contents into a wide buffer and then determine
			// if it's a unicode or ascii file
			ushort Signature = 0;
			if (ReadFile(hFile, out Signature))
			{
				// Check to see if its a unicode file by looking at the signature of the first character.
				if (0xFEFF == Signature)
				{
					using SafeLPWSTR szwBuff = new((int)dwSize / 2);
					bIsUnicode = true;
					if (ReadFile(hFile, szwBuff, szwBuff.Size - 2, out dwBytesRead, default))
					{
						ppszwBuff = szwBuff;
					}
					else
					{
						hr = Win32Error.GetLastError().ToHRESULT();
					}

				}
				else // MBCS source
				{
					using SafeLPSTR szaBuff = new((int)dwSize + 1);
					bIsUnicode = false;
					SetFilePointer(hFile, default, default, System.IO.SeekOrigin.Begin);
					if (ReadFile(hFile, szaBuff, dwSize, out dwBytesRead, default))
					{
						ppszwBuff = szaBuff;
					}
					else
					{
						hr = Win32Error.GetLastError().ToHRESULT();
					}
				}
			}
			else
			{
				hr = Win32Error.GetLastError().ToHRESULT();
			}
		}

		return hr;
	}

	/////////////////////////////////////////////////////////////////
	void Stop()
	/////////////////////////////////////////////////////////////////
	// 
	// Resets global audio state to stopped, updates the Pause/Resume button
	// and repaints the mouth in a closed position
	//
	{
		// Stop current rendering with a PURGEBEFORESPEAK...
		m_cpVoice!.Speak(default, SPEAKFLAGS.SPF_PURGEBEFORESPEAK, out _);

		TTSAppStatusMessage(Handle, "Stop error\r\n");

		SetWindowText(GetDlgItem(Handle, IDB_PAUSE), "Pause");
		m_bPause = false;
		m_bStop = true;
		// Mouth closed
		g_iBmp = 0;
		SendMessage(m_hChildWnd, WindowMessage.WM_PAINT);
		InvalidateRect(m_hChildWnd, default, false);
	}
	/////////////////////////////////////////////////////
	void TTSAppStatusMessage(HWND hWnd, string szMessage)
	/////////////////////////////////////////////////////
	//
	// This function prints debugging messages to the debug edit control
	//
	{
		StringBuilder szDebugText = new(4096);
		GetDlgItemText(hWnd, IDC_DEBUG, szDebugText, szDebugText.Capacity);

		// Clear out the buffer after 100 lines of text have been written
		// to the debug window since it can only hold 4096 characters.
		if (szDebugText.Length >= 4095)
			szDebugText.Clear();

		// Attach the new message to the ongoing list of messages
		szDebugText.AppendLine(szMessage);
		SetDlgItemText(hWnd, IDC_DEBUG, szDebugText.ToString());
		var i = szDebugText.Length == 0 ? 0 : Regex.Matches(szDebugText.ToString(), "\r\n|\r|\n").Count;
		SendDlgItemMessage(hWnd, IDC_DEBUG, EditMessage.EM_LINESCROLL, 0, i);

		return;
	}

	/////////////////////////////////////////////////////////////////////////
	void UpdateEditCtlW(string pszwText)
	/////////////////////////////////////////////////////////////////////////
	{
		HRESULT hr = HRESULT.S_OK;

		// Use rich edit control interface pointers to update text
		IntPtr pcpRichEdit = default;
		using PinnedObject ppcpRichEdit = new(pcpRichEdit);
		if (SendDlgItemMessage(Handle, IDE_EDITBOX, RichEditMessage.EM_GETOLEINTERFACE, 0, (IntPtr)ppcpRichEdit) != IntPtr.Zero)
		{
			ITextServices? pTextServices = Marshal.GetObjectForIUnknown(pcpRichEdit) as ITextServices;
			if (pTextServices is not null)
			{
				hr = pTextServices.TxSetText(pszwText);
			}
		}

		// Add text the old fashon way by converting it to ansi. Note information
		// loss will occur because of the WC2MB conversion.
		if (pcpRichEdit == IntPtr.Zero || hr.Failed)
		{
			SetDlgItemText(Handle, IDE_EDITBOX, pszwText);
		}
	}

	/////////////////////////////////////////////////////////////////
	HRESULT VoiceChange()
	/////////////////////////////////////////////////////////////////
	//
	// This function is called during initialization and whenever the 
	// selection for the voice combo box changes. 
	// It gets the token pointer associated with the voice.
	// If the new voice is different from the one that's currently 
	// selected, it first stops any synthesis that is going on and
	// sets the new voice on the global voice object. 
	//
	{
		HRESULT hr = HRESULT.S_OK;

		// Get the token associated with the selected voice
		ISpObjectToken? pToken = SpGetCurSelComboBoxToken(GetDlgItem(Handle, IDC_COMBO_VOICES));

		//Determine if it is the current voice
		ISpObjectToken? pOldToken = null;
		hr = GetHR(() => m_cpVoice!.GetVoice(out pOldToken));

		if (hr.Succeeded)
		{
			if (pOldToken != pToken)
			{
				// Stop speaking. This is not necesary, for the next call to work,
				// but just to show that we are changing voices.
				hr = GetHR(() => m_cpVoice!.Speak(null, SPEAKFLAGS.SPF_PURGEBEFORESPEAK, out _));
				// And set the new voice on the global voice object
				if (hr.Succeeded)
				{
					hr = GetHR(() => m_cpVoice!.SetVoice(pToken!));
				}
			}
		}

		EnableSpeakButtons(hr.Succeeded);

		return hr;
	}
}