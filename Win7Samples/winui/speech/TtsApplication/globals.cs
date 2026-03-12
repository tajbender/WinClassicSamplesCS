using static Vanara.PInvoke.ComCtl32;
using static Vanara.PInvoke.SpeechApi;

internal partial class CTTSApp
{
    // Other global variables
    static int g_iBmp = 0;
    static SafeHIMAGELIST g_hListBmp = SafeHIMAGELIST.Null;

    // Output formats
    static readonly int[] g_aMapVisemeToImage = [
        0,  // SP_VISEME_0 = 0,    // Silence
        11, // SP_VISEME_1,        // AE, AX, AH
        11, // SP_VISEME_2,        // AA
        11, // SP_VISEME_3,        // AO
        10, // SP_VISEME_4,        // EY, EH, UH
        11, // SP_VISEME_5,        // ER
        9,  // SP_VISEME_6,        // y, IY, IH, IX
        2,  // SP_VISEME_7,        // w, UW
        13, // SP_VISEME_8,        // OW
        9,  // SP_VISEME_9,        // AW
        12, // SP_VISEME_10,       // OY
        11, // SP_VISEME_11,       // AY
        9,  // SP_VISEME_12,       // h
        3,  // SP_VISEME_13,       // r
        6,  // SP_VISEME_14,       // l
        7,  // SP_VISEME_15,       // s, z
        8,  // SP_VISEME_16,       // SH, CH, JH, ZH
        5,  // SP_VISEME_17,       // TH, DH
        4,  // SP_VISEME_18,       // f, v
        7,  // SP_VISEME_19,       // d, t, n
        9,  // SP_VISEME_20,       // k, g, NG
        1 ];// SP_VISEME_21,       // p, b, m

    // Output formats
    static readonly SPSTREAMFORMAT[] g_aOutputFormat = [
        SPSTREAMFORMAT.SPSF_8kHz8BitMono,
        SPSTREAMFORMAT.SPSF_8kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_8kHz16BitMono,
        SPSTREAMFORMAT.SPSF_8kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_11kHz8BitMono,
        SPSTREAMFORMAT.SPSF_11kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_11kHz16BitMono,
        SPSTREAMFORMAT.SPSF_11kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_12kHz8BitMono,
        SPSTREAMFORMAT.SPSF_12kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_12kHz16BitMono,
        SPSTREAMFORMAT.SPSF_12kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_16kHz8BitMono,
        SPSTREAMFORMAT.SPSF_16kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_16kHz16BitMono,
        SPSTREAMFORMAT.SPSF_16kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_22kHz8BitMono,
        SPSTREAMFORMAT.SPSF_22kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_22kHz16BitMono,
        SPSTREAMFORMAT.SPSF_22kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_24kHz8BitMono,
        SPSTREAMFORMAT.SPSF_24kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_24kHz16BitMono,
        SPSTREAMFORMAT.SPSF_24kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_32kHz8BitMono,
        SPSTREAMFORMAT.SPSF_32kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_32kHz16BitMono,
        SPSTREAMFORMAT.SPSF_32kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_44kHz8BitMono,
        SPSTREAMFORMAT.SPSF_44kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_44kHz16BitMono,
        SPSTREAMFORMAT.SPSF_44kHz16BitStereo,
        SPSTREAMFORMAT.SPSF_48kHz8BitMono,
        SPSTREAMFORMAT.SPSF_48kHz8BitStereo,
        SPSTREAMFORMAT.SPSF_48kHz16BitMono,
        SPSTREAMFORMAT.SPSF_48kHz16BitStereo ];

    static readonly string[] g_aszOutputFormat = [
        "8kHz 8 Bit Mono",
        "8kHz 8 Bit Stereo",
        "8kHz 16 Bit Mono",
        "8kHz 16 Bit Stereo",
        "11kHz 8 Bit Mono",
        "11kHz 8 Bit Stereo",
        "11kHz 16 Bit Mono",
        "11kHz 16 Bit Stereo",
        "12kHz 8 Bit Mono",
        "12kHz 8 Bit Stereo",
        "12kHz 16 Bit Mono",
        "12kHz 16 Bit Stereo",
        "16kHz 8 Bit Mono",
        "16kHz 8 Bit Stereo",
        "16kHz 16 Bit Mono",
        "16kHz 16 Bit Stereo",
        "22kHz 8 Bit Mono",
        "22kHz 8 Bit Stereo",
        "22kHz 16 Bit Mono",
        "22kHz 16 Bit Stereo",
        "24kHz 8 Bit Mono",
        "24kHz 8 Bit Stereo",
        "24kHz 16 Bit Mono",
        "24kHz 16 Bit Stereo",
        "32kHz 8 Bit Mono",
        "32kHz 8 Bit Stereo",
        "32kHz 16 Bit Mono",
        "32kHz 16 Bit Stereo",
        "44kHz 8 Bit Mono",
        "44kHz 8 Bit Stereo",
        "44kHz 16 Bit Mono",
        "44kHz 16 Bit Stereo",
        "48kHz 8 Bit Mono",
        "48kHz 8 Bit Stereo",
        "48kHz 16 Bit Mono",
        "48kHz 16 Bit Stereo"];

    // ITextServices interface guid
    static readonly Guid IID_ITextServices = new(0X8D33F740, 0XCF58, 0x11ce, 0XA8, 0X9D, 0X00, 0XAA, 0X00, 0X6C, 0XAD, 0XC5);

    // ITextDocument interface guid
    static readonly Guid IID_ITextDocument = new Guid(0x8CC497C0, 0xA1DF, 0x11ce, 0x80, 0x98, 0x00, 0xAA, 0x00, 0x47, 0xBE, 0x5D);
}
