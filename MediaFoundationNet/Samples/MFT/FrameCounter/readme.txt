/************************************************************************

FrameCounter - Two MFTs illustrating using the template classes.

**************************************************************************/

This sample shows writing two different MFTs to count the number of frames.
One MFT is synchronous (using MFTBase) and the other is asynchronous
(using AsyncMFTBase).  They are about the simplest MFTs that can be
written using the templates.

It is unlikely that you will need to write both sync and async versions of 
the same MFT.  It is done here only to illustrate the differences between
the two.

=====================================================
FrameCounter - Counts the number of video samples in a stream.

This sample is about the simplest possible sample that can be written using the framework.

It send all input samples, unmodified, as output.

CLSID: FC8AFE7E-2624-4437-A6B8-D071C862A52B

Input types supported:
MFMediaType_Video + any

Output types supported:
Matches input type.

=======================================================================
FrameCounterAsync - Counts the number of video samples in a stream.

This sample is about the simplest possible sample that can be written using the async framework.

It send all input samples, unmodified, as output.

CLSID: 2137B262-D5D7-4F81-AE90-D3A2ECC66E14

Attributes returned from IMFTransform::GetAttributes

// Using the IMFAttributes returned from IMFTransform::GetAttributes, you can 
// read this VT_UI4 to get the current sample count processed by this MFT.
// This is not a standard MF attribute. 
// AA99FFD1-4DF6-45F5-8BB1-8AF5BAEDAA85
static const GUID m_SampleCountGuid = { 0xAA99FFD1, 0x4DF6, 0x45F5, { 0x8b, 0xb1, 0x8a, 0xf5, 0xba, 0xed, 0xaa, 0x85 } };

Input types supported:
MFMediaType_Video + any

Output types supported:
Matches input type.

