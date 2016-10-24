using System;
using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace VideoPlayer.Render
{
    enum CLSCTX
    {
        CLSCTX_INPROC_SERVER = 0x1,
        CLSCTX_INPROC_HANDLER = 0x2,
        CLSCTX_LOCAL_SERVER = 0x4,
        CLSCTX_INPROC_SERVER16 = 0x8,
        CLSCTX_REMOTE_SERVER = 0x10,
        CLSCTX_INPROC_HANDLER16 = 0x20,
        CLSCTX_RESERVED1 = 0x40,
        CLSCTX_RESERVED2 = 0x80,
        CLSCTX_RESERVED3 = 0x100,
        CLSCTX_RESERVED4 = 0x200,
        CLSCTX_NO_CODE_DOWNLOAD = 0x400,
        CLSCTX_RESERVED5 = 0x800,
        CLSCTX_NO_CUSTOM_MARSHAL = 0x1000,
        CLSCTX_ENABLE_CODE_DOWNLOAD = 0x2000,
        CLSCTX_NO_FAILURE_LOG = 0x4000,
        CLSCTX_DISABLE_AAA = 0x8000,
        CLSCTX_ENABLE_AAA = 0x10000,
        CLSCTX_FROM_DEFAULT_CONTEXT = 0x20000,
        CLSCTX_ACTIVATE_32_BIT_SERVER = 0x40000,
        CLSCTX_ACTIVATE_64_BIT_SERVER = 0x80000,
        CLSCTX_ENABLE_CLOAKING = 0x100000,
        CLSCTX_APPCONTAINER = 0x400000,
        CLSCTX_ACTIVATE_AAA_AS_IU = 0x800000,
        //CLSCTX_PS_DLL = (int)0x80000000
    }    ;

    public class H264Decoder
    {


        static H264Decoder()
        {
            // Create H.264 decoder.
            //CHECK_HR( CoCreateInstance(CLSID_CMSH264DecoderMFT, NULL, CLSCTX.CLSCTX_INPROC_SERVER, IID_IUnknown,   out spDecTransformUnk), "Failed to create H264 decoder MFT.\n");
            //CHECK_HR(spDecTransformUnk.QueryInterface(IID_PPV_ARGS(&pDecoderTransform)), "Failed to get IMFTransform interface from H264 decoder MFT object.\n");
        }



        private static void CHECK_HR(HResult hResult, string v)
        {
            throw new NotImplementedException();
        }

        private void ThrowIfError(HResult hr)
        {
            if (!MFError.Succeeded(hr))
            {
                MFError.ThrowExceptionForHR(hr);
            }
        }

        private void Throw(HResult hr)
        {
            MFError.ThrowExceptionForHR(hr);
        }
    }
}
