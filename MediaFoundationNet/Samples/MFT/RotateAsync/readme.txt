Rotate - Rotate/Flip video streams

This sample rotates/flips video (including during playback).  When using 
this MFT in PlaybackFX, press 'r' to rotate the video.

Some rotations result in a change to the output type (height/width changes), 
so this MFT shows how to dynamically change the output type, using
MF_E_TRANSFORM_STREAM_CHANGE messages to notify downstream components of 
changes to media types.

CLSID: C2206097-AE39-44BD-99D0-4226205753FC

Attributes returned from IMFTransform::GetAttributes

// Using the IMFAttributes returned from IMFTransform::GetAttributes, you can 
// set this VT_UI4 to a member of System.Drawing.RotateFlipType.
// AC776FB5-858F-4891-A5DC-FD01E79B5AD6
static const GUID AttribRotate = {0xAC776FB5, 0x858F, 0x4891, 0xA5, 0xDC, 0xFD, 0x01, 0xE7, 0x9B, 0x5A, 0xD6};

Input types supported:
MFMediaType_Video + MFVideoFormat_RGB32

Output types supported:
Matches input type, except that width/height may be swapped for 
MF_MT_FRAME_SIZE, MF_MT_GEOMETRIC_APERTURE, MF_MT_MINIMUM_DISPLAY_APERTURE 
& MF_MT_DEFAULT_STRIDE.
