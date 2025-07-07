using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.SpeechApi;
using static Vanara.PInvoke.User32;

// ---------------------------------------------------------------------------
// WinMain
// ---------------------------------------------------------------------------
// Description:         Program entry point.
ComCtl32.InitCommonControlsEx(ComCtl32.CommonControlClass.ICC_UPDOWN_CLASS | ComCtl32.CommonControlClass.ICC_BAR_CLASSES);

using (var hInst = LoadLibraryEx("riched20.dll", LoadLibraryExFlags.LOAD_LIBRARY_AS_DATAFILE))
{
	if (hInst.IsInvalid)
	{
		MessageBox(IntPtr.Zero, "Couldn't find riched20.dll. Shutting down!", "Error - Missing dll", MB_FLAGS.MB_OK);
		return 1;
	}
	WindowClass.MakeVisibleWindowClass(CTTSApp.CHILD_CLASS, CTTSApp.ChildWndProc);
	CTTSApp.InitSapi();
	return new CTTSApp().Show().ToInt32();
}

internal partial class CTTSApp : ModalDialog
{
	// resource.h
	const int IDS_SAVE_NOTIFY = 1;
	const int IDS_NOTIFY_TITLE = 2;
	const int IDS_SAVE_ERROR = 3;
	const int IDS_AUDIOOUT_ERROR = 4;
	const int IDS_VOICE_INIT_ERROR = 4;
	const int IDS_UNSUPPORTED_FORMAT = 5;
	const int IDI_APPICON = 101;
	const int IDD_MAIN = 101;
	const int IDB_MICFULL = 127;
	const int IDB_MICEYESCLO = 128;
	const int IDB_MICEYESNAR = 129;
	const int IDB_MICMOUTH10 = 148;
	const int IDB_MICMOUTH11 = 149;
	const int IDB_MICMOUTH12 = 150;
	const int IDB_MICMOUTH13 = 151;
	const int IDB_MICMOUTH2 = 152;
	const int IDB_MICMOUTH3 = 153;
	const int IDB_MICMOUTH4 = 154;
	const int IDB_MICMOUTH5 = 155;
	const int IDB_MICMOUTH6 = 156;
	const int IDB_MICMOUTH7 = 157;
	const int IDB_MICMOUTH8 = 158;
	const int IDB_MICMOUTH9 = 159;
	const int IDD_ABOUT = 161;
	const int IDE_EDITBOX = 1000;
	const int IDC_ABOUT_TTSAPP_VERSION = 1000;
	const int IDC_MOUTHPOS = 1001;
	const int IDB_SPEAK = 1002;
	const int IDB_PAUSE = 1003;
	const int IDB_STOP = 1004;
	const int IDC_EVENTS = 1006;
	const int IDC_DEBUG = 1007;
	const int IDC_SPEAKXML = 1008;
	const int IDB_OPEN = 1009;
	const int IDC_COMBO_VOICES = 1011;
	const int IDB_RESET = 1012;
	const int IDC_RATE_SLIDER = 1014;
	const int IDC_VOLUME_SLIDER = 1015;
	const int IDC_COMBO_OUTPUT = 1018;
	const int IDC_SAVETOWAV = 1020;
	const int IDB_SPEAKWAV = 1021;
	const int IDC_CHARACTER = 1023;
	const int IDB_SKIP = 1026;
	const int IDC_SKIP_EDIT = 1031;
	const int IDC_SKIP_SPIN = 1032;
	const int IDC_ABOUT = 1033;

	// Constant definitions
	const int MAX_SIZE = 102400;//100K
	const int NORM_SIZE = 256;
	const int NUM_OUTPUTFORMATS = 36;
	const WindowMessage WM_TTSAPPCUSTOMEVENT = WindowMessage.WM_APP; // Window message used for systhesis events
	internal const string CHILD_CLASS = "TTSAppChildWin";  // Child window for blitting mouth to
	const int WEYESNAR = 14; // eye positions
	const int WEYESCLO = 15;
	const int NUM_PHONEMES = 6;
	const int CHARACTER_WIDTH = 128;
	const int CHARACTER_HEIGHT = 128;
	const int MAX_FILE_PATH = 256;

	//
	//  Member data
	//  
	SafeHWND m_hChildWnd = SafeHWND.Null;
	static ISpVoice? m_cpVoice = null;
	ISpAudio m_cpOutAudio = null!;
	bool m_bPause = false;
	bool m_bStop = true;
	ushort m_DefaultVolume = 0;
	int m_DefaultRate = 0;
	int m_DefaultFormatIndex = 0;
	string? m_pszwFileText = null;
	string? m_szWFileName = "";

	public CTTSApp() : base("TtsApplicationRes.dll", IDD_MAIN) { }
}