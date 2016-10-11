/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* What this file is for:
 * 
 * This file implements a Media Foundation transforms (MFT) to
 * rotate and flip video.
 * 
 * This MFT requires RGB32 mediatype as input.
 * 
 * While it seems like it would be simple to add support for other types 
 * (RGB24, RGB565, etc), there is a catch.  For those formats, MF adds a 
 * transform downstream to convert back to a usable format (probably RGB32).  
 * And *that* filter doesn't support dynamic format changes (as of W8.1).
 * 
 * So while this can be done (I've done it), you end up accepting all the 
 * various input types, but always producing RGB32 as output, to prevent MF 
 * from adding that transform.  But that turns out to be a bit much to put in 
 * a sample.
 */

using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace MFT_RotateAsync
{
    [ComVisible(true),
    Guid("C2206097-AE39-44BD-99D0-4226205753FC"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class RotateAsync : AsyncMFTBase
    {
        #region Overrides

        override protected HResult OnCheckInputType(IMFMediaType pmt)
        {
            // We only check to see if the type is valid as an input type.  We
            // do NOT check if it is consistent with the current output type.
            // This is required in order to support dynamic format change (a 
            // requirement for Async MFTs).  Any incompatibility will be 
            // caught and handled if/when the type actually gets set (see 
            // MySetInput).

            HResult hr;

            hr = OnCheckMediaType(pmt);

            return hr;
        }
        override protected void OnGetInputStreamInfo(ref MFTInputStreamInfo pStreamInfo)
        {
            pStreamInfo.cbSize = m_cbImageSize;
            pStreamInfo.dwFlags = MFTInputStreamInfoFlags.WholeSamples |
                MFTInputStreamInfoFlags.FixedSampleSize |
                MFTInputStreamInfoFlags.SingleSamplePerBuffer |
                MFTInputStreamInfoFlags.ProcessesInPlace;
        }
        override protected void OnGetOutputStreamInfo(ref MFTOutputStreamInfo pStreamInfo)
        {
            pStreamInfo.cbSize = m_cbImageSize;
            pStreamInfo.dwFlags = MFTOutputStreamInfoFlags.WholeSamples |
                                  MFTOutputStreamInfoFlags.SingleSamplePerBuffer |
                                  MFTOutputStreamInfoFlags.FixedSampleSize |
                                  MFTOutputStreamInfoFlags.ProvidesSamples;
        }
        override protected void OnProcessSample(IMFSample pInputSample, bool Discontinuity, int InputMessageNumber)
        {
            MFError throwonhr;

            IMFMediaBuffer pInput;

            // While we accept types that *might* be interlaced, if we actually receive
            // an interlaced sample, reject it.
            if (m_MightBeInterlaced)
            {
                int ix;

                // Returns a bool: true = interlaced, false = progressive
                throwonhr = pInputSample.GetUINT32(MFAttributesClsid.MFSampleExtension_Interlaced, out ix);

                if (ix != 0)
                {
                    SafeRelease(pInputSample);
                    return;
                }
            }

            // Set the Discontinuity flag on the sample that's going to OutputSample.
            HandleDiscontinuity(Discontinuity, pInputSample);

            int i = MFExtern.MFGetAttributeUINT32(Attributes, ClsidRotate, 0);
            bool IsOdd = (i & 1) == 1;

            // Does the specified rotation give a different orientation than 
            // the old one?
            if (IsOdd != m_WasOdd)
            {
                // Yes, change the output type.
                OutputSample(null, InputMessageNumber);
                m_WasOdd = IsOdd;
            }

            // Get the data buffer from the input sample.  If the sample has
            // multiple buffers, you might be able to get (slightly) better
            // performance processing each buffer in turn rather than forcing
            // a new, full-sized buffer to get created.
            throwonhr = pInputSample.ConvertToContiguousBuffer(out pInput);

            try
            {
                // Process it.
                DoWork(pInput, (RotateFlipType)i);

                // Send the modified input sample to the output sample queue.
                OutputSample(pInputSample, InputMessageNumber);
            }
            finally
            {
                // If (somewhere) there is .Net code that is holding on to an instance of
                // the same buffer as pInput, this will yank the RCW out from underneath 
                // it, probably causing it to crash.  But if we don't release it, our memory 
                // usage explodes.
                SafeRelease(pInput);
            }
        }

        override protected HResult OnEnumInputTypes(int dwTypeIndex, out IMFMediaType pInputType)
        {
            return CreatePartialType(dwTypeIndex, MFMediaType.Video, m_MediaSubtypes, out pInputType);
        }

        override protected void OnSetInputType()
        {
            MFError throwonhr;

            m_imageWidthInPixels = 0;
            m_imageHeightInPixels = 0;
            m_cbImageSize = 0;
            m_lInputStride = 0;

            IMFMediaType pmt = InputType;

            // type can be null to clear
            if (pmt != null)
            {
                TraceAttributes(pmt);

                throwonhr = MFExtern.MFGetAttributeSize(pmt, MFAttributesClsid.MF_MT_FRAME_SIZE, out m_imageWidthInPixels, out m_imageHeightInPixels);

                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_DEFAULT_STRIDE, out m_lInputStride);

                // Calculate the image size (not including padding)
                m_cbImageSize = m_imageHeightInPixels * m_lInputStride;

                // If the output type isn't set yet, we can pre-populate it,
                // since output is based on the input.  This can 
                // save a (tiny) bit of time in negotiating types.

                OnSetOutputType();
            }
            else
            {
                // Since the input must be set before the output, nulling the 
                // input must also clear the output.  Note that nulling the 
                // input is only valid if we are not actively streaming.

                OutputType = null;
            }
        }

        protected override void OnSetOutputType()
        {
            // If the output type is null or is being reset to null (by 
            // dynamic format change), pre-populate it.
            if (InputType != null && OutputType == null)
            {
                OutputType = CreateOutputFromInput();
            }
            else
            {
                TraceAttributes(OutputType);
            }
        }

        protected override IMFMediaType CreateOutputFromInput()
        {
            // For some MFTs, the output type is the same as the input type.  
            // However, since we are rotating, several attributes in the 
            // media type (like frame size) must be different on our output.  
            // This routine generates the appropriate output type for the 
            // current input type, given the current state of m_WasOdd.

            IMFMediaType inType = InputType;

            IMFMediaType pOutputType = CloneMediaType(inType);

            if (m_WasOdd)
            {
                MFError throwonhr;
                int h, w;

                // Intentionally backward
                throwonhr = MFExtern.MFGetAttributeSize(inType, MFAttributesClsid.MF_MT_FRAME_SIZE, out h, out w);

                throwonhr = MFExtern.MFSetAttributeSize(pOutputType, MFAttributesClsid.MF_MT_FRAME_SIZE, w, h);

                MFVideoArea a = GetArea(inType, MFAttributesClsid.MF_MT_GEOMETRIC_APERTURE);
                if (a != null)
                {
                    a.Area.Height = h;
                    a.Area.Width = w;
                    SetArea(pOutputType, MFAttributesClsid.MF_MT_GEOMETRIC_APERTURE, a);
                }

                a = GetArea(inType, MFAttributesClsid.MF_MT_MINIMUM_DISPLAY_APERTURE);
                if (a != null)
                {
                    a.Area.Height = h;
                    a.Area.Width = w;
                    SetArea(pOutputType, MFAttributesClsid.MF_MT_MINIMUM_DISPLAY_APERTURE, a);
                }

                throwonhr = pOutputType.SetUINT32(MFAttributesClsid.MF_MT_DEFAULT_STRIDE, w * bpp);
            }

            return pOutputType;
        }

        #endregion

        #region Member variables

        // Format information
        private int m_imageWidthInPixels;
        private int m_imageHeightInPixels;
        private int m_cbImageSize;              // Image size, in bytes.
        private int m_lInputStride;
        private const int bpp = 4;

        private bool m_MightBeInterlaced;

        private readonly Guid[] m_MediaSubtypes = new Guid[] { MFMediaType.RGB32 };

        private static readonly Guid ClsidRotate = new Guid("AC776FB5-858F-4891-A5DC-FD01E79B5AD6");
        private bool m_WasOdd;

        #endregion

        // The '1' indicates there should only be 1 processing thread.  
        // This MFT supports multiple threads, but I'm not sure there's 
        // much benefit.
        public RotateAsync()
            : base(1)
        {
            Trace("RotateAsync Constructor");

            const int DefaultRotate = 5;
            m_WasOdd = (DefaultRotate & 1) == 1;

            MFError throwonhr = Attributes.SetUINT32(ClsidRotate, DefaultRotate);
        }

#if DEBUG
        ~RotateAsync()
        {
            Trace("RotateAsync Destructor");
        }
#endif

        #region COM Registration methods

        [ComRegisterFunctionAttribute]
        static private void DllRegisterServer(Type t)
        {
            HResult hr = MFExtern.MFTRegister(
                t.GUID,
                MFTransformCategory.MFT_CATEGORY_VIDEO_EFFECT,
                t.Name,
                MFT_EnumFlag.AsyncMFT,
                0,
                null,
                0,
                null,
                null
                );
            MFError.ThrowExceptionForHR(hr);
        }

        [ComUnregisterFunctionAttribute]
        static private void DllUnregisterServer(Type t)
        {
            HResult hr = MFExtern.MFTUnregister(t.GUID);

            // In Windows 7, MFTUnregister reports an error even if it succeeds:
            // https://social.msdn.microsoft.com/forums/windowsdesktop/en-us/7d3dc70f-8eae-4ad0-ad90-6c596cf78c80
            //MFError.ThrowExceptionForHR(hr);
        }

        #endregion

        #region Helpers

        private static MFVideoArea GetArea(IMFAttributes ia, Guid g)
        {
            PropVariant pv = new PropVariant();

            HResult hr = ia.GetItem(g, pv);
            if (hr == HResult.MF_E_ATTRIBUTENOTFOUND)
                return null;

            MFError.ThrowExceptionForHR(hr);

            return pv.GetBlob(typeof(MFVideoArea)) as MFVideoArea;
        }

        private static void SetArea(IMFAttributes ia, Guid g, MFVideoArea a)
        {
            PropVariant pv = new PropVariant(a);

            MFError throwonhr = ia.SetItem(g, pv);
        }

        /// <summary>
        /// Validates a media type for this transform.
        /// </summary>
        /// <param name="pmt">The media type to validate.</param>
        /// <returns>S_Ok or MF_E_INVALIDTYPE.</returns>
        private HResult OnCheckMediaType(IMFMediaType pmt)
        {
            HResult hr = HResult.S_OK;

            // Check the Major type and subtype
            hr = CheckMediaType(pmt, MFMediaType.Video, m_MediaSubtypes);
            if (Succeeded(hr))
            {
                int interlace;

                m_MightBeInterlaced = false;

                MFError throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, out interlace);

                MFVideoInterlaceMode im = (MFVideoInterlaceMode)interlace;

                // Mostly we only accept Progressive.
                if (im != MFVideoInterlaceMode.Progressive)
                {
                    // If the type MIGHT be interlaced, we'll accept it.
                    if (im != MFVideoInterlaceMode.MixedInterlaceOrProgressive)
                    {
                        hr = HResult.MF_E_INVALIDTYPE;
                    }
                    else
                    {
                        // But we will check to see if any samples actually
                        // are interlaced, and reject them.
                        m_MightBeInterlaced = true;
                    }
                }
            }

            return hr;
        }

        // Get the buffers and sizes to be modified, then pass them
        // to the rotate routine.
        private void DoWork(IMFMediaBuffer pIn, RotateFlipType ft)
        {
            IntPtr pSrc = IntPtr.Zero;			// Source buffer.
            int lSrcStride;		// Source stride.
            IMF2DBuffer pIn2D = null;

            // Lock the input buffer. Use IMF2DBuffer if available.
            Lockit(pIn, out pIn2D, out lSrcStride, out pSrc);

            try
            {
                WriteIt(pSrc, ft);
            }
            finally
            {
                UnlockIt(pSrc, pIn2D, pIn);
            }
        }

        private void WriteIt(IntPtr pBuffer, RotateFlipType fm)
        {
            using (Bitmap v = new Bitmap(m_imageWidthInPixels, m_imageHeightInPixels, m_lInputStride, PixelFormat.Format32bppRgb, pBuffer))
            {
                v.RotateFlip(fm);

                Rectangle r = new Rectangle(0, 0, v.Width, v.Height);

                BitmapData bmd = new BitmapData();
                bmd.Width = r.Width;
                bmd.Height = r.Height;
                bmd.Stride = bpp*bmd.Width;
                bmd.PixelFormat = PixelFormat.Format32bppRgb; 
                bmd.Scan0 = pBuffer;
                bmd.Reserved = 0;

                BitmapData bmdOut = v.LockBits(r, ImageLockMode.ReadOnly | ImageLockMode.UserInputBuffer, PixelFormat.Format32bppRgb, bmd);

                v.UnlockBits(bmdOut);
            }
        }

        private void Lockit(IMFMediaBuffer pOut, out IMF2DBuffer pOut2D, out int lDestStride, out IntPtr pDest)
        {
            MFError throwonhr;

            pOut2D = pOut as IMF2DBuffer;
            if (pOut2D != null)
            {
                throwonhr = pOut2D.Lock2D(out pDest, out lDestStride);
            }
            else
            {
                int ml;
                int cb;
                throwonhr = pOut.Lock(out pDest, out ml, out cb);
                lDestStride = m_lInputStride;
            }
        }

        private static void UnlockIt(IntPtr pSrc, IMF2DBuffer pIn2D, IMFMediaBuffer pIn)
        {
            if (pSrc != IntPtr.Zero)
            {
                MFError throwonhr;

                if (pIn2D != null)
                {
                    throwonhr = pIn2D.Unlock2D();
                }
                else
                {
                    throwonhr = pIn.Unlock();
                }
            }
        }

        #endregion
    }
}
