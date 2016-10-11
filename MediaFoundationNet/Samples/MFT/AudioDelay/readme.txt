audiodelay - Add an echo to audio streams.

This sample is basically the MS mft_audiodelay sample, re-written to use the framework.

It also shows how to use the attributes to configure an MFT (including during 
playback).  And how to use OnDrain, and OnStartStream/OnEndStream to allocate 
and release resources.

CLSID: 8F17BC18-4242-40E8-BEE8-06FD8B11EB33

Attributes returned from IMFTransform::GetAttributes

// MF_AUDIODELAY_DELAY_LENGTH: {95915546-B07C-4234-A237-1AF27187DEEE}
// Type: UINT32
// Specifies the length of the delay effect, in milliseconds.
// This attribute must be set before the MFT_MESSAGE_NOTIFY_BEGIN_STREAMING
// message is sent.
static const GUID MF_AUDIODELAY_DELAY_LENGTH = { 0x95915546, 0xb07c, 0x4234, 0xa2, 0x37, 0x1a, 0xf2, 0x71, 0x87, 0xde, 0xee };

// MF_AUDIODELAY_WET_DRY_MIX: {72127F43-5878-4ea8-8269-D1AF3BB11CB2}
// Type: UINT32
// Specifies the wet/dry mix. (Range: 0 - 100. 0 = no delay, 100 = all delay)
// This attribute can be set while the MFT is active.
static const GUID MF_AUDIODELAY_WET_DRY_MIX = { 0x72127f43, 0x5878, 0x4ea8, 0x82, 0x69, 0xd1, 0xaf, 0x3b, 0xb1, 0x1c, 0xb2 };

Input types supported:
MFMediaType_Audio + MFAudioFormat_PCM

Output types supported:
Matches input type.
