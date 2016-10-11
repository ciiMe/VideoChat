/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* What this file is for:
 * 
 * This file implements a Media Foundation transform (MFT) to turn video
 * from color to grayscale. It is essentially the same MFT as the 
 * MFT_Grayscale sample (with a few improvements), but it shows how to use the
 * AsyncMFTBase to do the majority of the work.  All the 'grayscale-specific'
 * code is in this file, so you can use the template to easily create your
 * own MFT.
 * 
 * Read the comments at the top of AsyncMFTBase.cs for getting started 
 * instructions
 * 
 * This MFT can handle 3 different media types:
 *
 *    YUY2
 *    UYVY
 *    NV12
 * 
 * A few things to know about the GrayscaleAsync MFT:
 * 
 * - The output media type must be exactly equal to the input type.
 * - The processing is done "in-place" (ie the input sample is modified).
 * - Not all clients support Async MFTs (Vista, the (now-deprecated) 
 * IMFPMediaPlayer interface, etc).
 */

using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace MFT_GrayscaleAsync
{
    [ComVisible(true),
    Guid("E6AAA34E-A092-418A-A037-6634EED63CB5"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class GrayscaleAsync : AsyncMFTBase
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

            HResult hr = HResult.S_OK;

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

            // Get the data buffer from the input sample.  If the sample has
            // multiple buffers, you might be able to get (slightly) better
            // performance processing each buffer in turn rather than forcing
            // a new, full-sized buffer to get created.
            throwonhr = pInputSample.ConvertToContiguousBuffer(out pInput);

            try
            {
                // Process it.
                DoWork(pInput);

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
            m_videoFOURCC = new FourCC(0);
            m_cbImageSize = 0;
            m_lStrideIfContiguous = 0;

            m_pTransformFn = null;

            IMFMediaType pmt = InputType;

            // type can be null to clear
            if (pmt != null)
            {
                Guid subtype;

                throwonhr = pmt.GetGUID(MFAttributesClsid.MF_MT_SUBTYPE, out subtype);

                throwonhr = MFExtern.MFGetAttributeSize(pmt, MFAttributesClsid.MF_MT_FRAME_SIZE, out m_imageWidthInPixels, out m_imageHeightInPixels);

                m_videoFOURCC = new FourCC(subtype);

                if (m_videoFOURCC == FOURCC_YUY2)
                {
                    m_pTransformFn = Update_YUY2;
                    m_lStrideIfContiguous = m_imageWidthInPixels * 2;
                }
                else if (m_videoFOURCC == FOURCC_UYVY)
                {
                    m_pTransformFn = Update_UYVY;
                    m_lStrideIfContiguous = m_imageWidthInPixels * 2;
                }
                else if (m_videoFOURCC == FOURCC_NV12)
                {
                    m_pTransformFn = Update_NV12;
                    m_lStrideIfContiguous = m_imageWidthInPixels;
                }
                else
                {
                    throw new COMException("Unrecognized type", (int)HResult.E_UNEXPECTED);
                }

                // Calculate the image size (not including padding)
                int lImageSize;
                if (Succeeded(pmt.GetUINT32(MFAttributesClsid.MF_MT_SAMPLE_SIZE, out lImageSize)))
                    m_cbImageSize = lImageSize;
                else
                    m_cbImageSize = GetImageSize(m_videoFOURCC, m_imageWidthInPixels, m_imageHeightInPixels);

                int lStrideIfContiguous;
                if (Succeeded(pmt.GetUINT32(MFAttributesClsid.MF_MT_DEFAULT_STRIDE, out lStrideIfContiguous)))
                    m_lStrideIfContiguous = lStrideIfContiguous;

                // If the output type isn't set yet, we can pre-populate it, 
                // since output must always exactly equal input.  This can 
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
        }

        #endregion

        #region Member variables

        // Format information
        private FourCC m_videoFOURCC; // type of samples being processed
        private int m_imageWidthInPixels;
        private int m_imageHeightInPixels;
        private int m_cbImageSize;              // Image size, in bytes.
        private int m_lStrideIfContiguous;
        private TransformImage m_pTransformFn;

        // Video FOURCC codes.
        private readonly static FourCC FOURCC_YUY2 = new FourCC('Y', 'U', 'Y', '2');
        private readonly static FourCC FOURCC_UYVY = new FourCC('U', 'Y', 'V', 'Y');
        private readonly static FourCC FOURCC_NV12 = new FourCC('N', 'V', '1', '2');

        private readonly Guid[] m_MediaSubtypes;

        private bool m_MightBeInterlaced;

        private delegate void TransformImage(IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels);

        #endregion

        // The '1' indicates there should only be 1 processing thread.
        // While you *can* set this higher, Grayscaling doesn't really 
        // benefit from it.  See the TypeConverter for an MFT that does.
        public GrayscaleAsync()
            : base(1)
        {
            Trace("GrayscaleAsync Constructor");

            m_pTransformFn = null;

            m_MediaSubtypes = new Guid[] { FOURCC_NV12.ToMediaSubtype(), FOURCC_YUY2.ToMediaSubtype(), FOURCC_UYVY.ToMediaSubtype() };
        }

        ~GrayscaleAsync()
        {
            Trace("GrayscaleAsync Destructor");
        }

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

        /// <summary>
        /// Validates a media type for this transform.
        /// </summary>
        /// <param name="pmt">The media type to validate.</param>
        /// <returns>S_Ok or MF_E_INVALIDTYPE.</returns>
        private HResult OnCheckMediaType(IMFMediaType pmt)
        {
            HResult hr;

            // Check the Major and Subtype
            hr = CheckMediaType(pmt, MFMediaType.Video, m_MediaSubtypes);
            if (Succeeded(hr))
            {
                int interlace;

                // Video must be progressive frames.
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

        /// <summary>
        /// Calculates the buffer size needed, based on the video format.
        /// </summary>
        /// <param name="fcc">Video type</param>
        /// <param name="width">Frame width</param>
        /// <param name="height">Frame height</param>
        /// <returns>Size in bytes</returns>
        private int GetImageSize(FourCC fcc, int width, int height)
        {
            int pcbImage;

            if ((fcc == FOURCC_YUY2) || (fcc == FOURCC_UYVY))
            {
                // check overflow
                if ((width > int.MaxValue / 2) ||
                    (width * 2 > int.MaxValue / height))
                {
                    throw new COMException("Bad size", (int)HResult.E_INVALIDARG);
                }

                // 16 bpp
                pcbImage = width * height * 2;
            }
            else if (fcc == FOURCC_NV12)
            {
                // check overflow
                if ((height / 2 > int.MaxValue - height) ||
                    ((height + height / 2) > int.MaxValue / width))
                {
                    throw new COMException("Bad size", (int)HResult.E_INVALIDARG);
                }

                // 12 bpp
                pcbImage = width * (height + (height / 2));
            }
            else
            {
                throw new COMException("Unrecognized type", (int)HResult.E_FAIL);
            }

            return pcbImage;
        }

        // Get the buffers and sizes to be modified, then pass them
        // to the appropriate Update_* routine.
        private void DoWork(IMFMediaBuffer pIn)
        {
            IntPtr pSrc = IntPtr.Zero;			// Source buffer.
            int lSrcStride;		// Source stride.
            IMF2DBuffer pIn2D = null;

            try
            {
                // Lock the input buffer. Use IMF2DBuffer if available.
                Lockit(pIn, out pIn2D, out lSrcStride, out pSrc);

                // Invoke the image transform function.
                if (m_pTransformFn != null)
                {
                    m_pTransformFn(pSrc, lSrcStride,
                        m_imageWidthInPixels, m_imageHeightInPixels);
                }
                else
                {
                    throw new COMException("Transform type not set", (int)HResult.E_UNEXPECTED);
                }

                // Set the data size on the output buffer.
                MFError throwonhr = pIn.SetCurrentLength(m_cbImageSize);
            }
            finally
            {
                UnlockIt(pSrc, pIn2D, pIn);
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
                lDestStride = m_lStrideIfContiguous;
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

        /// <summary>
        /// Converts an image in UYVY format to grayscale.
        /// </summary>
        /// <param name="pSrc">Pointer to the source buffer.</param>
        /// <param name="lSrcStride">Stride of the source buffer, in bytes.</param>
        /// <param name="dwWidthInPixels">Frame width in pixels.</param>
        /// <param name="dwHeightInPixels">Frame height, in pixels.</param>
        unsafe private void Update_UYVY(
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            ushort* pSrc_Pixel = (ushort*)pSrc;
            int lMySrcStride = (lSrcStride / 2);  // lSrcStride is in bytes and we need words

            for (int y = 0; y < dwHeightInPixels; y++)
            {
                for (int x = 0; x < dwWidthInPixels; x++)
                {
                    // Byte order is U0 Y0 V0 Y1
                    // Each WORD is a byte pair (U/V, Y)
                    // Windows is little-endian so the order appears reversed.

                    pSrc_Pixel[x] = (ushort)((pSrc_Pixel[x] & 0xFF00) | 0x0080);
                }

                pSrc_Pixel += lMySrcStride;
            }
        }

        /// <summary>
        /// Converts an image in YUY2 format to grayscale.
        /// </summary>
        /// <param name="pSrc">Pointer to the source buffer.</param>
        /// <param name="lSrcStride">Stride of the source buffer, in bytes.</param>
        /// <param name="dwWidthInPixels">Frame width in pixels.</param>
        /// <param name="dwHeightInPixels">Frame height, in pixels.</param>
        unsafe private void Update_YUY2(
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            ushort* pSrc_Pixel = (ushort*)pSrc;
            int lMySrcStride = (lSrcStride / 2);  // lSrcStride is in bytes and we need words

            for (int y = 0; y < dwHeightInPixels; y++)
            {
                for (int x = 0; x < dwWidthInPixels; x++)
                {
                    // Byte order is Y0 U0 Y1 V0
                    // Each WORD is a byte pair (Y, U/V)
                    // Windows is little-endian so the order appears reversed.

                    pSrc_Pixel[x] = (ushort)((pSrc_Pixel[x] & 0x00FF) | 0x8000);
                }

                pSrc_Pixel += lMySrcStride;
            }
        }

        /// <summary>
        /// Converts an image in NV12 format to grayscale.
        /// </summary>
        /// <param name="pSrc">Pointer to the source buffer.</param>
        /// <param name="lSrcStride">Stride of the source buffer, in bytes.</param>
        /// <param name="dwWidthInPixels">Frame width in pixels.</param>
        /// <param name="dwHeightInPixels">Frame height, in pixels.</param>
        private void Update_NV12(
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            // NV12 is planar: Y plane, followed by packed U-V plane.

            if (lSrcStride != dwWidthInPixels)
            {
                // Y plane
                for (int y = 0; y < dwHeightInPixels; y++)
                {
                    pSrc += lSrcStride;
                }
                // U-V plane
                for (int y = 0; y < dwHeightInPixels / 2; y++)
                {
                    FillMemory(pSrc, dwWidthInPixels, 0x80);
                    pSrc += lSrcStride;
                }
            }
            else
            {
                int iSize = dwHeightInPixels * lSrcStride;

                FillMemory(pSrc + iSize, (dwHeightInPixels / 2) * lSrcStride, 0x80);
            }
        }

        #endregion

        #region Externs

        [DllImport("kernel32.dll"), System.Security.SuppressUnmanagedCodeSecurity]
        private static extern void FillMemory(IntPtr destination, int len, byte val);

        #endregion
    }
}
