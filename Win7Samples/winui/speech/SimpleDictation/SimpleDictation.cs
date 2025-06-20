using Vanara;
using Vanara.Extensions;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.SpeechApi;

namespace SimpleDictation;
internal class CSimpleDict
{
	// resource.h
	const int IDD_SIMPLEDICTDLG = 100;
	const int IDC_BUTTON_EXIT = 1006;
	const int IDC_EDIT_DICT = 1021;

	const int GID_DICTATION = 0; // Dictation grammar has grammar ID 0
	const WindowMessage WM_RECOEVENT = WindowMessage.WM_APP;      // Window message used for recognition events

	bool m_bInSound;
	bool m_bGotReco;
	ISpRecoContext? m_cpRecoCtxt;
	ISpRecoGrammar? m_cpDictationGrammar;
	SafeHINSTANCE g_hInst;
	private HWND m_hDlg;

	internal static void Main() => new CSimpleDict().Run();

	public CSimpleDict() => Win32Error.ThrowLastErrorIfInvalid(g_hInst = LoadLibraryEx("SimpleDictationRes.dll", LoadLibraryExFlags.LOAD_LIBRARY_AS_DATAFILE));

	public void Run() => DialogBox(g_hInst, IDD_SIMPLEDICTDLG, HWND.NULL, SimpleDictDlgProc);

	/*********************************************************************************************
	* CSimpleDict::SimpleDictDlgProc()
	*       Main message handler for SimpleDictation dialog box.
	*       At initialization time, LPARAM is the CSimpleDict object, to which the
	*       window long is set.
	**********************************************************************************************/
	private nint SimpleDictDlgProc(HWND hDlg, uint message, nint wParam, nint lParam)
	{
		switch ((WindowMessage)message)
		{
			case WindowMessage.WM_INITDIALOG:

				if (!InitDialog(hDlg))
					DestroyWindow(hDlg);
				break;

			case WM_RECOEVENT:
				// All recognition events send this message, because that is how we
				// specified we should be notified
				RecoEvent();
				return 1;

			case WindowMessage.WM_COMMAND:
				if (Macros.LOWORD(wParam) == IDC_BUTTON_EXIT)
					EndDialog(hDlg, 1);
				break;

			case WindowMessage.WM_CLOSE:
				EndDialog(hDlg, 1);
				break;

			case WindowMessage.WM_DESTROY:
				// Release the recognition context and the dictation grammar
				m_cpRecoCtxt = null;
				m_cpDictationGrammar = null;
				break;

			default:
				return 0;
		}

		return 1;
	}

	/*********************************************************************************************
	* CSimpleDict::InitDialog()
	* Creates the recognition context and activates the grammar. 
	* Returns true iff successful.
	**********************************************************************************************/
	bool InitDialog(HWND hDlg)
	{
		m_hDlg = hDlg;

		try
		{
			ISpRecognizer cpRecoEngine = (ISpRecognizer)new SpInprocRecognizer();

			m_cpRecoCtxt = cpRecoEngine.CreateRecoContext();

			// Set recognition notification for dictation
			m_cpRecoCtxt.SetNotifyWindowMessage(hDlg, (uint)WM_RECOEVENT, 0, 0);

			// This specifies which of the recognition events are going to trigger notifications.
			// Here, all we are interested in is the beginning and ends of sounds, as well as
			// when the engine has recognized something
			SPEVENTENUM ullInterest = SPFEI(SPEVENTENUM.SPEI_RECOGNITION);
			m_cpRecoCtxt.SetInterest(ullInterest, ullInterest);

			// create default audio object
			SpCreateDefaultObjectFromCategoryId(SPCAT_AUDIOIN, out ISpAudio cpAudio).ThrowIfFailed();

			// set the input for the engine
			cpRecoEngine.SetInput(cpAudio, true);
			cpRecoEngine.SetRecoState(SPRECOSTATE.SPRST_ACTIVE);

			// Specifies that the grammar we want is a dictation grammar.
			// Initializes the grammar (m_cpDictationGrammar)
			m_cpRecoCtxt.CreateGrammar(GID_DICTATION, out m_cpDictationGrammar);

			m_cpDictationGrammar.LoadDictation(default, SPLOADOPTIONS.SPLO_STATIC);

			m_cpDictationGrammar.SetDictationState(SPRULESTATE.SPRS_ACTIVE);

			return true;
		}
		catch { return false; }
	}

	/*****************************************************************************************
	* CSimpleDict::RecoEvent()
	* Called whenever the dialog process is notified of a recognition event.
	* Inserts whatever is recognized into the edit box. 
	******************************************************************************************/
	void RecoEvent()
	{
		CSpEvent spevent = new();

		// Process all of the recognition events
		while (spevent.GetFrom(m_cpRecoCtxt!) == HRESULT.S_OK)
		{
			switch (spevent.spEvent.eEventId)
			{
				case SPEVENTENUM.SPEI_SOUND_START:
					m_bInSound = true;
					break;

				case SPEVENTENUM.SPEI_SOUND_END:
					if (m_bInSound)
					{
						m_bInSound = false;
						if (!m_bGotReco)
						{
							// The sound has started and ended, 
							// but the engine has not succeeded in recognizing anything
							SafeLPTSTR szNoise = new("<noise>");

							SendDlgItemMessage(m_hDlg, IDC_EDIT_DICT, (uint)EditMessage.EM_REPLACESEL, (BOOL)true, (IntPtr)szNoise);
						}
						m_bGotReco = false;
					}
					break;

				case SPEVENTENUM.SPEI_RECOGNITION:
					// There may be multiple recognition results, so get all of them
					{
						m_bGotReco = true;
						string wszUnrecognized = "<Unrecognized>";

						string dstrText;
						try { spevent.RecoResult().GetText(SP_GETWHOLEPHRASE, SP_GETWHOLEPHRASE, true, out dstrText, out _); }
						catch { dstrText = wszUnrecognized; }

						// Concatenate a space onto the end of the recognized word
						dstrText += " ";

						SendDlgItemMessage(m_hDlg, IDC_EDIT_DICT, (uint)EditMessage.EM_REPLACESEL, (BOOL)true, new SafeLPTSTR(dstrText));
					}
					break;

			}
		}
	}
}