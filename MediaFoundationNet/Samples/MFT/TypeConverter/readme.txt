TypeConverter - Convert MFVideoFormat_YUY2 input to MFVideoFormat_RGB32 output.

This sample takes YUY2 input samples and converts them to RGB32.  If registered
with MFTRegister, MF can use this to translate streams.

It also shows how to create new samples for output.

CLSID: 567C527F-9025-4057-BE42-527554D10ADE

Input types supported:
MFMediaType_Video + MFVideoFormat_YUY2

Output types supported:
Input type modified to corresponding MFVideoFormat_RGB32 values.
