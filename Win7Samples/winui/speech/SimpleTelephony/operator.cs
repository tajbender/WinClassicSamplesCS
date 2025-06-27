using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.SpeechApi;
using static Vanara.PInvoke.TAPI3;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.WinMm;

// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright © Microsoft Corporation. All rights reserved

//////////////////////////////////////////////////////////
// SimpleTelephony.EXE
//
// Sample application that handles incoming TAPI calls
// and does some speech recognition and text-to-speech.
//
// In order to receive incoming calls, the application must
// implement and register the outgoing ITCallNotification
// interface.
//
// This application will register to receive calls on
// all addresses that support at least the audio media type.
//
// NOTE:  This application is limited to working with one
// call at at time, and will not work correctly if multiple
// calls are present at the same time.
//
// NOTE: The call-handling sequence in this application is
// short, so it is fine to make it all the way through to
// the end of the call even if the caller hung up in the 
// middle.  If your telephony app involves a lengthy 
// call-handling sequence, you need a separate thread to
// listen for TAPI's CS_DISCONNECT message; otherwise your
// app will have no way of being notified of the hang-up,
// and your app will appear to have hung.
//////////////////////////////////////////////////////////

/****************************************************************************
*	Operator.cpp
*		Implementation of the COperator class for the SimpleTelephony
*       project.
*****************************************************************************/

// Create an instance of our main call-handling class
new COperator().Show();

class COperator : ModalDialog
{
	public const int SPSF_NUM_FORMATS = 69;
	public static readonly HRESULT TAPI_E_DROPPED = new(0x80040031);

	// resource.h
	const int IDD_MAINDLG = 101;
	const int IDC_STATUS = 1000;
	const int IDC_ANSWER = 1001;
	const int IDC_DISCONNECT = 1002;
	const int IDC_DISCONNECTED = 1003;
	const int IDC_AUTOANSWER = 1004;
	const int IDC_STATIC = -1;

	internal const int WM_PRIVATETAPIEVENT = (int)WindowMessage.WM_USER + 101;

	// Global data
	const int CALLER_TIMEOUT = 8000; // Wait 8 seconds before giving up on a caller
	const string gszTelephonySample = "SAPI 5.0 Telephony sample";

	// Win32 data
	bool m_fAutoAnswer;

	// TAPI data
	ITTAPI? m_pTapi;
	ITBasicCallControl? m_pCall;
	ITTAPIEventNotification? m_pTAPIEventNotification;
	ComConnectionPoint? cp;
	int m_ulAdvise;

	// SAPI data

	// Audio
	ISpMMSysAudio? m_cpMMSysAudioOut;
	ISpMMSysAudio? m_cpMMSysAudioIn;

	// Text to speech
	ISpVoice? m_cpLocalVoice;
	ISpVoice? m_cpOutgoingVoice;

	// Speech Recognition
	ISpRecognizer? m_cpIncomingRecognizer;
	ISpRecoContext? m_cpIncomingRecoCtxt;
	ISpRecoGrammar? m_cpDictGrammar;

	public COperator() : base("SimpleTelephonyRes.dll", IDD_MAINDLG) { }

	protected override nint OnClosing() => 1;

	protected override void OnCommand(int id, HWND hControl, uint code)
	{
		switch (id)
		{
			case (int)MB_RESULT.IDCANCEL:
				Close();
				break;

			case IDC_AUTOANSWER:
				// Auto answer check box state was changed
				m_fAutoAnswer = !m_fAutoAnswer;
				break;

			case IDC_ANSWER:
				// Answer the call

				SetStatusMessage("Answering...");

				if (HRESULT.S_OK == AnswerTheCall())
				{
					SetStatusMessage("Connected");

					EnableWindow(GetDlgItem(Handle, IDC_ANSWER), false);

					// Connected: Talk to the caller

					// PLEASE NOTE: This is a single-threaded app, so if the caller
					// hangs up after the call-handling sequence has started, the 
					// app will not be notified until after the entire call sequence 
					// has finished. 
					// If you want to be able to cut the call-handling short because
					// the caller hung up, you need to have a separate thread listening
					// for TAPI's CS_DISCONNECT notification.
					HRESULT hrHandleCall = HandleCall();
					if (hrHandleCall.Failed)
					{
						if (TAPI_E_DROPPED == hrHandleCall)
						{
							SetStatusMessage("Caller hung up prematurely");
						}
						else
						{
							DoMessage("Error encountered handling the call");
						}
					}

					// Hang up if we still need to
					if (default != m_pCall)
					{
						// The caller is still around; hang up on him
						SetStatusMessage("Disconnecting...");
						if (HRESULT.S_OK != DisconnectTheCall())
						{
							DoMessage("Disconnect failed");
						}
					}
				}
				else
				{
					EnableWindow(GetDlgItem(Handle, IDC_ANSWER), false);
					DoMessage("Answer failed");
				}

				// Waiting for the next call...
				SetStatusMessage("Waiting for a call...");
				break;

			case IDC_DISCONNECTED:
				// This message is sent from OnTapiEvent()
				// Disconnected notification -- release the call
				ReleaseTheCall();

				EnableWindow(GetDlgItem(Handle, IDC_ANSWER), false);

				SetStatusMessage("Waiting for a call...");

				break;

			default:
				break;
		}
	}

	protected override void OnDestroying()
	{
		ShutdownSapi();
		ShutdownTapi();
	}

	protected override bool OnInitDialog()
	{
		InitializeSapi().ThrowIfFailed();
		InitializeTapi().ThrowIfFailed();

		// Wait for a call
		EnableWindow(GetDlgItem(Handle, IDC_ANSWER), false);
		SetStatusMessage("Waiting for a call...");

		return true;
	}

	///////////////////////////////////////////////////////////
	// Miscellany
	///////////////////////////////////////////////////////////

	/**********************************************************
	* COperator::DoMessage *
	*----------------------*
	*   Description:
	*       Displays a MessageBox
	************************************************************/
	void DoMessage(string pszMessage) => MessageBox(Handle, pszMessage, gszTelephonySample, MB_FLAGS.MB_OK);

	/**********************************************************
	* COperator::SetStatusMessage *
	*-----------------------------*
	*   Description:
	*       Displays a status message
	************************************************************/
	void SetStatusMessage(string pszMessage) => SetDlgItemText(Handle, IDC_STATUS, pszMessage);

	/*************************************************************
	* AddressSupportsMediaType *
	*--------------------------*
	*   Return:
	*       TRUE iff the given address supports the given media 
	**************************************************************/
	bool AddressSupportsMediaType(ITAddress pAddress, TAPIMEDIATYPE lMediaType)
	{
		ITMediaSupport pMediaSupport = (ITMediaSupport)pAddress;

		// does it support this media type?
		return pMediaSupport.QueryMediaType(lMediaType);
	}

	/**********************************************************
	* COperator::InitializeSapi *
	*---------------------------*
	*   Description:
	*       Various SAPI initializations.
	*   Return:
	*       S_OK if SAPI initialized successfully
	*       Return values of failed SAPI initialization 
	*           functions
	***********************************************************/
	HRESULT InitializeSapi()
	{
		try
		{
			// Create a voice for speaking on this machine
			m_cpLocalVoice = (ISpVoice)new SpVoice();

			// Create a reco engine for recognizing speech over the phone
			// This is an inproc recognizer since it will likely be
			// using a format other than the default
			m_cpIncomingRecognizer = (ISpRecognizer)new SpInprocRecognizer();

			// Create a reco context for this engine
			m_cpIncomingRecoCtxt = m_cpIncomingRecognizer.CreateRecoContext();

			// Set interest only in PHRASE_START, RECOGNITION, FALSE_RECOGNITION
			var ullInterest = SPFEI(SPEVENTENUM.SPEI_PHRASE_START) | SPFEI(SPEVENTENUM.SPEI_RECOGNITION) | SPFEI(SPEVENTENUM.SPEI_FALSE_RECOGNITION);
			m_cpIncomingRecoCtxt.SetInterest(ullInterest, ullInterest);

			// Retain recognized audio
			m_cpIncomingRecoCtxt.SetAudioOptions(SPAUDIOOPTIONS.SPAO_RETAIN_AUDIO);

			// Create a dictation grammar and load it
			m_cpIncomingRecoCtxt.CreateGrammar(0, out m_cpDictGrammar);

			m_cpDictGrammar.LoadDictation(default, SPLOADOPTIONS.SPLO_STATIC);

			// Create a voice for talking on the phone.
			m_cpIncomingRecoCtxt.GetVoice(out m_cpOutgoingVoice);

			return HRESULT.S_OK;
		}
		catch (Exception ex)
		{
			DoMessage($"Error initializing SAPI: {ex.Message}");
			return ex.HResult;
		}
	}

	/**********************************************************
	* COperator::InitializeTapi *
	*---------------------------*
	*   Description:
	*       Various TAPI initializations
	*   Return:
	*       S_OK iff TAPI initialized successfully
	*       Return values of failed TAPI initialization 
	*           functions
	***********************************************************/
	HRESULT InitializeTapi()
	{
		try
		{
			// Cocreate the TAPI object
			m_pTapi = new();

			// call ITTAPIInitialize(). this must be called before
			// any other tapi functions are called.
			m_pTapi.Initialize();

			// Create our own event notification object and register it
			// See callnot.h and callnot.cpp
			m_pTAPIEventNotification = new CTAPIEventNotification(Handle);

			RegisterTapiEventInterface();

			// Set the Event filter to only give us only the events we process
			m_pTapi.EventFilter = TAPI_EVENT.TE_CALLNOTIFICATION | TAPI_EVENT.TE_CALLSTATE;

			// Find all address objects that we will use to listen for calls on
			ListenOnAddresses().ThrowIfFailed();
		}
		catch (Exception ex)
		{
			DoMessage($"Error initializing TAPI: {ex.Message}");
			return ex.HResult;
		}

		return HRESULT.S_OK;
	}

	/**********************************************************
	* COperator::ShutdownSapi *
	*-------------------------*
	*   Description:
	*       Releases all SAPI objects
	***********************************************************/
	void ShutdownSapi()
	{
		m_cpMMSysAudioIn = null;
		m_cpMMSysAudioOut = null;
		m_cpDictGrammar = null;
		m_cpIncomingRecoCtxt = null;
		m_cpIncomingRecognizer = null;
		m_cpLocalVoice = null;
		m_cpOutgoingVoice = null;
	}

	/**********************************************************
	* COperator::ShutdownTapi *
	*-------------------------*
	*   Description:
	*       Releases all TAPI objects
	***********************************************************/
	void ShutdownTapi()
	{
		// if there is still a call, release it
		if (default != m_pCall)
		{
			m_pCall = default;
		}

		// release main object.
		if (default != m_pTapi)
		{
			m_pTapi.Shutdown();
			m_pTapi = null;
		}
	}

	///////////////////////////////////////////////////////////
	// TAPI actions
	///////////////////////////////////////////////////////////

	/**********************************************************
	* COperator::RegisterTapiEventInterface *
	*---------------------------------------*
	*   Description:
	*       Get a unique identifier (m_ulAdvice) for 
	*       this connection point.
	*   Return:
	*       S_OK
	*       Failed HRESULTs from QI(), 
	*           IConnectionPointContainer::FindConnectionPoint(),
	*           or IConnectionPoint::Advise()
	***********************************************************/
	HRESULT RegisterTapiEventInterface()
	{
		cp = new(m_pTapi!, m_pTAPIEventNotification);
		return HRESULT.S_OK;
	}

	/**********************************************************
	* COperator::ListenOnAddresses *
	*------------------------------*
	*   Description:
	*       Find all addresses that support audio in and audio
	*       out.  Call ListenOnThisAddress to start listening
	*   Return:
	*       S_OK
	*       S_FALSE if there are no addresses to listen on
	*       Failed HRESULT ITTAPI::EnumberateAddresses() 
	*           or IEnumAddress::Next()
	************************************************************/
	HRESULT ListenOnAddresses()
	{
		// enumerate the addresses
		IEnumAddress pEnumAddress = m_pTapi!.EnumerateAddresses();

		ITAddress[] pAddress = new ITAddress[1];
		bool fAddressExists = false;
		while (true)
		{
			// get the next address
			var hr = pEnumAddress.Next(1, pAddress, out var fetched);
			if (hr != HRESULT.S_OK)
			{
				// Done dealing with all the addresses
				break;
			}

			// Does the address support audio?
			if (AddressSupportsMediaType(pAddress[0], TAPIMEDIATYPE.TAPIMEDIATYPE_AUDIO))
			{
				// If it does then we'll listen.
				HRESULT hrListen = ListenOnThisAddress(pAddress[0]);

				if (HRESULT.S_OK == hrListen)
				{
					fAddressExists = true;
				}
			}

		}

		if (!fAddressExists)
		{
			DoMessage("Could not find any addresses to listen on");
		}
		return fAddressExists ? HRESULT.S_OK : HRESULT.S_FALSE;
	}

	/**********************************************************
	* COperator::ListenOnThisAddress *
	*--------------------------------*
	*   Description:
	*       Call RegisterCallNotifications() to inform TAPI 
	*       that we want notifications of calls on this 
	*       address.  We already registered our notification
	*       interface with TAPI, so now we are just telling
	*       TAPI that we want calls from this address to 
	*       trigger our existing notification interface.
	*   Return:
	*       Return value of ITTAPI::RegisterCallNotifications()
	************************************************************/
	HRESULT ListenOnThisAddress(ITAddress pAddress)
	{
		// RegisterCallNotifications takes a media type set of flags indicating
		// the set of media types we are interested in. We know the
		// address supports audio (see ListenOnAddress())
		try { m_pTapi!.RegisterCallNotifications(pAddress, true, true, TAPIMEDIATYPE.TAPIMEDIATYPE_AUDIO, m_ulAdvise); return HRESULT.S_OK; }
		catch (Exception ex) { return ex.HResult; }
	}

	/**********************************************************
	* COperator::AnswerTheCall *
	*--------------------------*
	*   Description:
	*       Called whenever notification is received of an 
	*       incoming call and the app wants to answer it.
	*       Answers and handles the call.
	*   Return:
	*       S_OK
	*       Failed retval of QI(), ITCallInfo::get_Address(),
	*           COperator::SetAudioOutForCall(),
	************************************************************/
	HRESULT AnswerTheCall()
	{
		if (default == m_pCall)
		{
			return HRESULT.E_UNEXPECTED;
		}

		// Get the LegacyCallMediaControl interface so that we can 
		// get a device ID to reroute the audio 
		ITLegacyCallMediaControl2 pLegacyCallMediaControl = (ITLegacyCallMediaControl2)m_pCall;

		// Set the audio for the SAPI objects so that the call 
		// can be handled
		var hr = SetAudioOutForCall(pLegacyCallMediaControl);
		if (hr.Failed)
		{
			DoMessage("Could not set up audio out for text-to-speech");
		}

		hr = SetAudioInForCall(pLegacyCallMediaControl);
		if (hr.Failed)
		{
			DoMessage("Could not set up audio in for speech recognition");
		}

		if (hr.Failed)
		{
			return hr;
		}

		// Now we can actually answer the call
		try { m_pCall.Answer(); }
		catch (Exception ex) { hr = ex.HResult; }

		return hr;
	}

	/**********************************************************
	* COperator::DisconnectTheCall *
	*------------------------------*
	*   Description:
	*       Disconnects the call
	*   Return:
	*       S_OK
	*       S_FALSE if there was no call
	*       Failed return value of ITCall::Disconnect()
	************************************************************/
	HRESULT DisconnectTheCall()
	{
		if (m_pCall is null)
			return HRESULT.S_FALSE;

		try
		{
			// Hang up
			m_pCall?.Disconnect(DISCONNECT_CODE.DC_NORMAL);
			return HRESULT.S_OK;
			// Do not release the call yet, as that would prevent
			// us from receiving the disconnected notification.
		}
		catch (Exception ex) { return ex.HResult; }
	}

	/**********************************************************
	* COperator::ReleaseTheCall *
	*---------------------------*
	*   Description:
	*       Releases the call
	************************************************************/
	void ReleaseTheCall()
	{
		if (default != m_pCall)
		{
			m_pCall = default;
		}

		// Let go of the SAPI audio in and audio out objects
		m_cpMMSysAudioOut?.SetState(SPAUDIOSTATE.SPAS_STOP, 0);

		m_cpMMSysAudioIn?.SetState(SPAUDIOSTATE.SPAS_STOP, 0);

		m_cpMMSysAudioOut = null;
		m_cpMMSysAudioIn = null;
	}

	/**********************************************************
	* COperator::OnTapiEvent *
	*------------------------*
	*   Description:
	*       This is the real TAPI event handler, called on our
	*       UI thread upon receipt of the WM_PRIVATETAPIEVENT
	*       message.
	*   Return:
	*       S_OK
	************************************************************/
	void OnTapiEvent(TAPI_EVENT TapiEvent, object pEvent)
	{
		switch (TapiEvent)
		{
			case TAPI_EVENT.TE_CALLNOTIFICATION:
				// TE_CALLNOTIFICATION means that the application is being notified
				// of a new call.
				//
				// Note that we don't answer to call at this point. The application
				// should wait for a CS_OFFERING CallState message before answering
				// the call.
				//
				ITCallNotificationEvent? pNotify = (ITCallNotificationEvent)pEvent;

				// Get the call
				ITCallInfo? pCall = pNotify.Call;

				pNotify = null;

				// Check to see if we own the call
				CALL_PRIVILEGE cp = pCall.Privilege;
				if (CALL_PRIVILEGE.CP_OWNER != cp)
				{
					// Just ignore it if we don't own it
					pCall = null;
					pEvent = null!; // We addrefed it CTAPIEventNotificationEvent()
					return;
				}

				// Get the ITBasicCallControl interface and save it in our
				// member variable.
				m_pCall = (ITBasicCallControl?)pCall;
				pCall = null;

				// Update UI
				EnableWindow(GetDlgItem(Handle, IDC_ANSWER), true);
				SetStatusMessage("Incoming Owner Call");

				break;

			case TAPI_EVENT.TE_CALLSTATE:
				// TE_CALLSTATE is a call state event. pEvent is
				// an ITCallStateEvent

				// Get the interface
				ITCallStateEvent pCallStateEvent = (ITCallStateEvent)pEvent;

				// Get the CallState that we are being notified of.
				CALL_STATE cs = pCallStateEvent.State;
				pCallStateEvent = null!;

				// If it's offering to be answered, update our UI
				if (CALL_STATE.CS_OFFERING == cs)
				{
					if (m_fAutoAnswer)
					{
						PostMessage(Handle, WindowMessage.WM_COMMAND, IDC_ANSWER, 0);
					}
					else
					{
						SetStatusMessage("Click the Answer button");
					}
				}
				else if (CALL_STATE.CS_DISCONNECTED == cs)
				{
					PostMessage(Handle, WindowMessage.WM_COMMAND, IDC_DISCONNECTED, 0);
				}
				else if (CALL_STATE.CS_CONNECTED == cs)
				{
					// Nothing to do -- we handle connection synchronously
				}
				break;

			default:
				break;
		}

		pEvent = null!; // We addrefed it CTAPIEventNotificationEvent()
	}

	///////////////////////////////////////////////////////////
	// SAPI actions
	///////////////////////////////////////////////////////////

	/**********************************************************
	* COperator::SetAudioOutForCall *
	*-------------------------------*
	*   Description:
	*       Uses the legacy call media control in TAPI to 
	*       get the device IDs for audio out.
	*       Uses these device IDs to set up the audio 
	*       for text-to-speech
	*   Return:
	*       S_OK
	*       E_INVALIDARG if the pLegacyCallMediaControl is NULL
	*       E_OUTOFMEMORY
	*       SPERR_DEVICE_NOT_SUPPORTED if no supported
	*           formats could be found
	*       Failed return value of QI(), 
	*           ITLegacyCallMediaControl::GetID(),
	*           CoCreateInstance(), 
	*           ISpMMSysAudio::SetDeviceID(),
	*           ISpMMSysAudio::SetFormat(),
	*           ISpVoice::SetOutput()                                
	************************************************************/
	HRESULT SetAudioOutForCall(ITLegacyCallMediaControl2 pLegacyCallMediaControl)
	{
		if (default == m_pCall)
		{
			return HRESULT.E_UNEXPECTED;
		}

		if (default == pLegacyCallMediaControl)
		{
			return HRESULT.E_INVALIDARG;
		}

		try
		{
			// Get the device ID from ITLegacyCallMediaControlGetID()
			var puDeviceID = pLegacyCallMediaControl.GetIDAsVariant("wave/out");

			// Find out what, if any, formats are supported
			Guid guidWave = SPDFID_WaveFormatEx;
			SafeCoTaskMemStruct<WAVEFORMATEX> pWaveFormatEx = SafeCoTaskMemStruct<WAVEFORMATEX>.Null;

			// Loop through all of the SAPI audio formats and query the wave/out device
			// about whether it supports each one.
			// We will take the first one that we find
			SPSTREAMFORMAT enumFmtId = 0;
			MMRESULT mmr = MMRESULT.MMSYSERR_ALLOCATED;
			for (int dw = 0; MMRESULT.MMSYSERR_NOERROR != mmr && dw < SPSF_NUM_FORMATS; dw++)
			{
				if (!pWaveFormatEx.IsInvalid && MMRESULT.MMSYSERR_NOERROR != mmr)
				{
					// No dice: The audio device does not support this format

					// Free up the WAVEFORMATEX pointer
					pWaveFormatEx.Dispose();
				}

				// Get the next format from SAPI and convert it into a WAVEFORMATEX
				enumFmtId = SPSTREAMFORMAT.SPSF_8kHz8BitMono + dw;
				HRESULT hrConvert = SpConvertStreamFormatEnum(enumFmtId, out guidWave, out pWaveFormatEx);
				if (hrConvert.Succeeded)
				{
					if (puDeviceID is not null)
					{
						// This call to waveOutOpen() does not actually open the device;
						// it just queries the device whether it supports the given format
						mmr = waveOutOpen(out _, (uint)puDeviceID, pWaveFormatEx, 0, 0, WAVE_OPEN.WAVE_FORMAT_QUERY);
					}
					else
					{
						return HRESULT.E_UNEXPECTED;
					}
				}
			}

			// If we made it all the way through the loop without breaking, that
			// means we found no supported formats
			if (enumFmtId == SPSTREAMFORMAT.SPSF_NUM_FORMATS)
			{
				return (HRESULT)(int)SPERR.SPERR_DEVICE_NOT_SUPPORTED;
			}

			// Cocreate a SAPI audio out object
			m_cpMMSysAudioOut = (ISpMMSysAudio)new SpMMAudioOut();

			// Give the audio out object the device ID
			m_cpMMSysAudioOut.SetDeviceId((uint)puDeviceID);

			// Use the format that we found works
			m_cpMMSysAudioOut.SetFormat(guidWave, pWaveFormatEx);

			// We are now done with the wave format pointer
			if (pWaveFormatEx)
			{
				pWaveFormatEx.Dispose();
			}

			// Set the appropriate output to the outgoing voice
			m_cpOutgoingVoice!.SetOutput(m_cpMMSysAudioOut, false);

			return HRESULT.S_OK;
		}
		catch (Exception ex) { return ex.HResult; }
	}

	/**********************************************************
	* COperator::SetAudioInForCall *
	*------------------------------*
	*   Description:
	*       Uses the legacy call media control in TAPI to 
	*       get the device IDs for audio input.
	*       Uses these device IDs to set up the audio 
	*       for speech recognition
	*   Return:
	*       S_OK
	*       E_INVALIDARG if pLegacyCallMediaControl is NULL
	*       E_OUTOFMEMORY
	*       SPERR_DEVICE_NOT_SUPPORTED if no supported
	*           formats could be found
	*       Failed return value of QI(), 
	*           ITLegacyCallMediaControl::GetID(),
	*           CoCreateInstance(), 
	*           ISpMMSysAudio::SetDeviceID(),
	*           ISpMMSysAudio::SetFormat()
	************************************************************/
	HRESULT SetAudioInForCall(ITLegacyCallMediaControl2 pLegacyCallMediaControl)
	{
		if (default == m_pCall)
		{
			return HRESULT.E_UNEXPECTED;
		}

		if (default == pLegacyCallMediaControl)
		{
			return HRESULT.E_INVALIDARG;
		}

		try
		{
			// Get the device ID
			var puDeviceID = pLegacyCallMediaControl.GetIDAsVariant("wave/in");

			// Find out what, if any, formats are supported
			Guid guidWave = SPDFID_WaveFormatEx;
			SafeCoTaskMemStruct<WAVEFORMATEX> pWaveFormatEx = SafeCoTaskMemStruct<WAVEFORMATEX>.Null;

			// Loop through all of the SAPI audio formats and query the wave/out device
			// about each one.
			// We will take the first one that we find
			SPSTREAMFORMAT enumFmtId;
			MMRESULT mmr = MMRESULT.MMSYSERR_ALLOCATED;
			int dw;
			for (dw = 0; MMRESULT.MMSYSERR_NOERROR != mmr && dw < SPSF_NUM_FORMATS; dw++)
			{
				if (!pWaveFormatEx.IsInvalid && MMRESULT.MMSYSERR_NOERROR != mmr)
				{
					// Free up the WAVEFORMATEX pointer
					pWaveFormatEx.Dispose();
				}

				// Get the next format from SAPI and convert it into a WAVEFORMATEX
				enumFmtId = SPSTREAMFORMAT.SPSF_8kHz8BitMono + dw;
				HRESULT hrConvert = SpConvertStreamFormatEnum(enumFmtId, out guidWave, out pWaveFormatEx);
				if (hrConvert.Succeeded)
				{
					if (puDeviceID is not null)
					{
						// This call to waveOutOpen() does not actually open the device;
						// it just queries the device whether it supports the given format
						mmr = waveInOpen(out _, (uint)puDeviceID, pWaveFormatEx, 0, 0, WAVE_OPEN.WAVE_FORMAT_QUERY);
					}
					else
					{
						return HRESULT.E_UNEXPECTED;
					}
				}
			}

			// If we made it all the way through the loop without breaking, that
			// means we found no supported formats
			if (SPSF_NUM_FORMATS == dw)
			{
				return (HRESULT)(int)SPERR.SPERR_DEVICE_NOT_SUPPORTED;
			}

			// Cocreate a SAPI audio in object
			m_cpMMSysAudioIn = (ISpMMSysAudio)new SpMMAudioIn();

			// Give the audio in object the device ID
			m_cpMMSysAudioIn.SetDeviceId((uint)puDeviceID);

			// Use the format that we found works
			m_cpMMSysAudioIn.SetFormat(guidWave, pWaveFormatEx);

			// We are now done with the wave format pointer
			if (pWaveFormatEx)
			{
				pWaveFormatEx.Dispose();
			}

			// Set this as input to the reco context
			m_cpIncomingRecognizer!.SetInput(m_cpMMSysAudioIn, false);

			return HRESULT.S_OK;
		}
		catch (Exception ex) { return ex.HResult; }
	}

	/**********************************************************
	* COperator::HandleCall *
	*-----------------------*
	*   Description:
	*       Deals with the call
	*   Return:
	*       S_OK
	*       Failed return value of ISpMMSysAudio::SetState(),
	*           ISpVoice::Speak()
	************************************************************/
	HRESULT HandleCall()
	{
		// PLEASE NOTE: This is a single-threaded app, so if the caller
		// hangs up after the call-handling sequence has started, the 
		// app will not be notified until after the entire call sequence 
		// has finished. 
		// If you want to be able to cut the call-handling short because
		// the caller hung up, you need to have a separate thread listening
		// for TAPI's CS_DISCONNECT notification.

		HRESULT hr = HRESULT.S_OK;

		try
		{
			// Now that the call is connected, we can start up the audio output
			m_cpOutgoingVoice!.Speak("Hello, please say something to me", 0, out _);

			// Start listening
			m_cpDictGrammar!.SetDictationState(SPRULESTATE.SPRS_ACTIVE);
		}
		catch { }

		// We are expecting a PHRASESTART followed by either a RECOGNITION or a 
		// FALSERECOGNITION

		// Wait for the PHRASE_START
		CSpEvent evt = new();
		SPEVENTENUM eLastEventID = SPEVENTENUM.SPEI_FALSE_RECOGNITION;
		hr = m_cpIncomingRecoCtxt!.WaitForNotifyEvent(CALLER_TIMEOUT);
		if (hr.Succeeded)
		{
			hr = evt.GetFrom(m_cpIncomingRecoCtxt);
		}

		// Enter this block only if we have not timed out (the user started speaking)
		if (HRESULT.S_OK == hr && SPEVENTENUM.SPEI_PHRASE_START == evt.spEvent.eEventId)
		{
			// Caller has started to speak, block "forever" until the 
			// result (or lack thereof) comes back.
			// This is all right, since every PHRASE_START is guaranteed
			// to be followed up by a RECOGNITION or FALSE_RECOGNITION
			hr = m_cpIncomingRecoCtxt.WaitForNotifyEvent(INFINITE);

			if (HRESULT.S_OK == hr)
			{
				// Get the RECOGNITION or FALSE_RECOGNITION 
				hr = evt.GetFrom(m_cpIncomingRecoCtxt);
				eLastEventID = evt.spEvent.eEventId;

				// This had better be either a RECOGNITION or FALSERECOGNITION!
				if (eLastEventID is not SPEVENTENUM.SPEI_RECOGNITION and not SPEVENTENUM.SPEI_FALSE_RECOGNITION)
					return HRESULT.E_FAIL;
			}
		}

		// Make sure a recognition result was actually received (as opposed to a false recognition
		// or timeout on the caller)
		string? pwszCoMemText = default;
		ISpRecoResult? pResult = default;
		if (hr.Succeeded && SPEVENTENUM.SPEI_RECOGNITION == evt.spEvent.eEventId)
		{
			// Get the text of the result
			try
			{
				pResult = evt.RecoResult();
				pResult.GetText(SP_GETWHOLEPHRASE, SP_GETWHOLEPHRASE, false, out pwszCoMemText, out _);
			}
			catch (Exception ex) { hr = ex.HResult; }
		}
		if (hr.Succeeded && pResult is not null)
		{
			// Speak the result back locally
			m_cpLocalVoice!.Speak("I think the person on the phone said", SPEAKFLAGS.SPF_ASYNC, out _);
			m_cpLocalVoice.Speak(pwszCoMemText, SPEAKFLAGS.SPF_ASYNC, out _);
			m_cpLocalVoice.Speak("when he said", SPEAKFLAGS.SPF_ASYNC, out _);

			// Get the audio so that the local voice can speak it back
			try
			{
				ISpStreamFormat cpStreamFormat = pResult.GetAudio(0, 0);
				m_cpLocalVoice.SpeakStream(cpStreamFormat, SPEAKFLAGS.SPF_ASYNC, out _);
			}
			catch
			{
				m_cpLocalVoice.Speak("no audio was available", SPEAKFLAGS.SPF_ASYNC, out _);
			}
		}

		// Stop listening
		if (hr.Succeeded)
		{
			m_cpDictGrammar!.SetDictationState(SPRULESTATE.SPRS_INACTIVE);

			// Close the audio input so that we can open the audio output
			// (half-duplex device)
			m_cpMMSysAudioIn!.SetState(SPAUDIOSTATE.SPAS_CLOSED, 0);

			// The caller may have hung up on us, in which case we don't want to do 
			// the following
			if (m_pCall is not null)
			{
				if (pResult is not null)
				{
					// There's a result to playback
					m_cpOutgoingVoice!.Speak("I think I heard you say", 0, out _);

					m_cpOutgoingVoice.Speak(pwszCoMemText, 0, out _);

					m_cpOutgoingVoice.Speak("when you said", 0, out _);

					pResult.SpeakAudio(default, 0, default, out _);
				}
				else
				{
					// Caller didn't say anything
					m_cpOutgoingVoice!.Speak("I don't believe you said anything!", 0, out _);
				}

				m_cpOutgoingVoice.Speak("OK bye now", 0, out _);
			}
			else
			{
				m_cpLocalVoice!.Speak("Prematurely terminated call", 0, out _);
			}
		}

		return m_pCall is not null ? hr : TAPI_E_DROPPED;
	}
}

public class CTAPIEventNotification(HWND m_hWnd) : ITTAPIEventNotification
{
	public HRESULT Event([In] TAPI_EVENT TapiEvent, [In, MarshalAs(UnmanagedType.IDispatch)] object pEvent)
	{
		PostMessage(m_hWnd, COperator.WM_PRIVATETAPIEVENT);
		return HRESULT.S_OK;
	}
}