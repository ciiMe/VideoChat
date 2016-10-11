GrayscaleEx - Convert a video stream to grayscale.

This sample is basically the MS mft_Grayscale sample, re-written to use the framework.

It also shows how to use multiple input types.

This is one of 3 re-writes of the grayscale sample.  This one is 
semi-synchronous, in that it is based on the synchronous template, but uses 
a thread for computation.

CLSID: 81FE27FA-5BAC-4CB8-8F35-F2366FB84035

Input types supported:
MFMediaType_Video + MFVideoFormat_NV12
MFMediaType_Video + MFVideoFormat_YUY2
MFMediaType_Video + MFVideoFormat_UYVY

Output types supported:
Matches input type.
