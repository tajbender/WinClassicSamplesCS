using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.SpeechApi;

bool fUseTTS = true; // turn TTS play back on or off
bool fReplay = true; // turn Audio replay on or off

// Process optional arguments
HRESULT hr = HRESULT.E_FAIL;
if (args.Length > 0)
{
	int i;

	for (i = 0; i < args.Length; i++)
	{
		if (string.Compare(args[i], "-noTTS", true) == 0)
		{
			fUseTTS = false;
			continue;
		}
		if (string.Compare(args[i], "-noReplay", true) == 0)
		{
			fReplay = false;
			continue;
		}
		Console.Write("Usage: {0} [-noTTS] [-noReplay] \n", args[0]);
		return (int)hr;
	}
}

ISpRecoContext cpRecoCtxt = (ISpRecoContext)new SpSharedRecoContext();
cpRecoCtxt.GetVoice(out var cpVoice);
cpRecoCtxt.SetNotifyWin32Event();
cpRecoCtxt.SetInterest(SPFEI(SPEVENTENUM.SPEI_RECOGNITION), SPFEI(SPEVENTENUM.SPEI_RECOGNITION));
cpRecoCtxt.SetAudioOptions(SPAUDIOOPTIONS.SPAO_RETAIN_AUDIO);
cpRecoCtxt.CreateGrammar(0, out var cpGrammar);
cpGrammar.LoadDictation(default, SPLOADOPTIONS.SPLO_STATIC);
cpGrammar.SetDictationState(SPRULESTATE.SPRS_ACTIVE);

string pchStop = StopWord();

Console.Write("I will repeat everything you say.\nSay \"{0}\" to exit.\n", pchStop);

while ((hr = BlockForResult(cpRecoCtxt, out var cpResult)).Succeeded)
{
	cpGrammar.SetDictationState(SPRULESTATE.SPRS_INACTIVE);

	try
	{
		cpResult.GetText(SP_GETWHOLEPHRASE, SP_GETWHOLEPHRASE, true, out var dstrText, out _);
		Console.Write("I heard: {0}\n", dstrText);

		if (fUseTTS)
		{
			cpVoice.Speak("I heard", SPEAKFLAGS.SPF_ASYNC, out _);
			cpVoice.Speak(dstrText, SPEAKFLAGS.SPF_ASYNC, out _);
		}

		if (fReplay)
		{
			if (fUseTTS)
				cpVoice.Speak("when you said", SPEAKFLAGS.SPF_ASYNC, out _);
			else
				Console.Write("\twhen you said...\n");
			cpResult.SpeakAudio(default, 0, default, out _);
		}

		cpResult = null;

		if (string.Compare(dstrText, pchStop, true) == 0)
			break;
	}
	catch { }

	cpGrammar.SetDictationState(SPRULESTATE.SPRS_ACTIVE);
}

return (int)hr;

HRESULT BlockForResult(ISpRecoContext pRecoCtxt, out ISpRecoResult ppResult)
{
	HRESULT hr = HRESULT.S_OK;
	CSpEvent @event = new();

	while (hr.Succeeded && (hr = @event.GetFrom(pRecoCtxt)).Succeeded && hr == HRESULT.S_FALSE)
	{
		hr = pRecoCtxt.WaitForNotifyEvent(INFINITE);
	}

	ppResult = @event.RecoResult();

	return hr;
}

string StopWord()
{
	LANGID LangId = SpGetUserDefaultUILanguage();

	return LangId == new LANGID(LANGID.LANG.LANG_JAPANESE, LANGID.SUBLANG.SUBLANG_DEFAULT) ?
		"\x7d42\x4e86\\\x30b7\x30e5\x30fc\x30ea\x30e7\x30fc/\x3057\x3085\x3046\x308a\x3087\x3046" :
		"Stop";
}