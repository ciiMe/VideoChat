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
 * MFTBase to do the majority of the work.  All the 'grayscale-specific'
 * code is in this file, so you can use the template to easily create your
 * own MFT.
 * 
 * Read the comments at the top of MFTBase.cs for getting started 
 * instructions
 * 
 * This MFT can handle 3 different media types:
 * 
 *    YUY2
 *    UYVY
 *    NV12
 *    
 * A few things to know about the Grayscale MFT:
 * 
 * - The output media type must be exactly equal to the input type.
 * - It would be MUCH more efficient to do in-place processing.  However, not
 * all clients support it (or even check for it).  For maximum portability,
 * this MFT always uses separate output samples.  To see in-place processing,
 * look at GrayscaleAsync.
 */

using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace MFT_Grayscale
{
    [ComVisible(true),
    Guid("69042198-8146-4735-90F0-BEFD5BFAEDB7"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class Grayscale : MFTBase
    {
        #region Overrides

        override protected HResult OnCheckInputType(IMFMediaType pmt)
        {
            HResult hr;

            if (OutputType == null)
            {
                hr = OnCheckMediaType(pmt);
            }
            else
            {
                hr = IsIdentical(pmt, OutputType);
            }

            return hr;
        }
        override protected void OnGetInputStreamInfo(ref MFTInputStreamInfo pStreamInfo)
        {
            pStreamInfo.cbSize = m_cbImageSize;
            pStreamInfo.dwFlags = MFTInputStreamInfoFlags.WholeSamples |
                                  MFTInputStreamInfoFlags.FixedSampleSize |
                                  MFTInputStreamInfoFlags.SingleSamplePerBuffer;
        }
        override protected void OnGetOutputStreamInfo(ref MFTOutputStreamInfo pStreamInfo)
        {
            pStreamInfo.cbSize = m_cbImageSize;
            pStreamInfo.dwFlags = MFTOutputStreamInfoFlags.WholeSamples |
                                  MFTOutputStreamInfoFlags.SingleSamplePerBuffer |
                                  MFTOutputStreamInfoFlags.FixedSampleSize;
        }

        override protected HResult OnProcessOutput(ref MFTOutputDataBuffer pOutputSamples)
        {
            HResult hr = HResult.S_OK;
            MFError throwonhr;

            // Since we don't specify MFTOutputStreamInfoFlags.ProvidesSamples, this can't be null.
            if (pOutputSamples.pSample != IntPtr.Zero)
            {
                long hnsDuration;
                long hnsTime;

                IMFMediaBuffer pInput = null;
                IMFMediaBuffer pOutput = null;
                IMFSample pOutSample = null;

                try
                {
                    // Get the data buffer from the input sample.  If the sample has
                    // multiple buffers, you might be able to get (slightly) better
                    // performance processing each buffer in turn rather than forcing
                    // a new, full-sized buffer to get created.
                    throwonhr = InputSample.ConvertToContiguousBuffer(out pInput);

                    // Turn pointer to interface
                    pOutSample = Marshal.GetUniqueObjectForIUnknown(pOutputSamples.pSample) as IMFSample;

                    // Get the output buffer.
                    throwonhr = pOutSample.ConvertToContiguousBuffer(out pOutput);

                    OnProcessOutput(pInput, pOutput);

                    // Set status flags.
                    pOutputSamples.dwStatus = MFTOutputDataBufferFlags.None;

                    // Copy the duration and time stamp from the input sample,
                    // if present.

                    hr = InputSample.GetSampleDuration(out hnsDuration);
                    if (Succeeded(hr))
                    {
                        throwonhr = pOutSample.SetSampleDuration(hnsDuration);
                    }

                    hr = InputSample.GetSampleTime(out hnsTime);
                    if (Succeeded(hr))
                    {
                        throwonhr = pOutSample.SetSampleTime(hnsTime);
                    }
                }
                finally
                {
                    SafeRelease(pInput);
                    SafeRelease(pOutput);
                    SafeRelease(pOutSample);

                    // Release the current input sample so we can get another one.
                    InputSample = null;
                }
            }
            else
            {
                return HResult.E_INVALIDARG;
            }

            return HResult.S_OK;
        }

        override protected HResult OnEnumInputTypes(int dwTypeIndex, out IMFMediaType pInputType)
        {
            return CreatePartialType(dwTypeIndex, MFMediaType.Video, m_MediaSubtypes, out pInputType);
        }

        // Called when the input type gets set
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
                    m_pTransformFn = TransformImage_YUY2;
                    m_lStrideIfContiguous = m_imageWidthInPixels * 2;
                }
                else if (m_videoFOURCC == FOURCC_UYVY)
                {
                    m_pTransformFn = TransformImage_UYVY;
                    m_lStrideIfContiguous = m_imageWidthInPixels * 2;
                }
                else if (m_videoFOURCC == FOURCC_NV12)
                {
                    m_pTransformFn = TransformImage_NV12;
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

        protected override HResult OnProcessInput()
        {
            HResult hr = HResult.S_OK;

            // While we accept types that *might* be interlaced, if we actually receive
            // an interlaced sample, reject it.
            if (m_MightBeInterlaced)
            {
                int ix;

                // Returns a bool: true = interlaced, false = progressive
                hr = InputSample.GetUINT32(MFAttributesClsid.MFSampleExtension_Interlaced, out ix);

                if (hr != HResult.S_OK || ix != 0)
                    hr = HResult.E_FAIL;
            }

            return hr;
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

        private delegate void TransformImage(IntPtr pDest, int lDestStride, IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels);

        #endregion

        public Grayscale()
            : base()
        {
            Trace("Grayscale Constructor");

            m_pTransformFn = null;

            m_MediaSubtypes = new Guid[] { FOURCC_NV12.ToMediaSubtype(), FOURCC_YUY2.ToMediaSubtype(), FOURCC_UYVY.ToMediaSubtype() };
        }

        ~Grayscale()
        {
            Trace("Grayscale Destructor");
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
        /// <remarks>Since both input and output types must be
        /// the same, they both call this routine.</remarks>
        private HResult OnCheckMediaType(IMFMediaType pmt)
        {
            HResult hr = HResult.S_OK;

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

        /// <summary>
        /// Given the input and output media buffers, do the transform
        /// </summary>
        /// <param name="pIn">Input buffer</param>
        /// <param name="pOut">Output buffer</param>
        private void OnProcessOutput(IMFMediaBuffer pIn, IMFMediaBuffer pOut)
        {
            IntPtr pDest = IntPtr.Zero;			// Destination buffer.
            int lDestStride;	// Destination stride.
            IMF2DBuffer pOut2D = null;

            IntPtr pSrc = IntPtr.Zero;			// Source buffer.
            int lSrcStride;		// Source stride.
            IMF2DBuffer pIn2D = null;

            try
            {
                // Lock the output buffer. Use IMF2DBuffer if available.
                Lockit(pOut, out pOut2D, out lDestStride, out pDest);

                // Lock the input buffer. Use IMF2DBuffer if available.
                Lockit(pIn, out pIn2D, out lSrcStride, out pSrc);

                // Invoke the image transform function.
                if (m_pTransformFn != null)
                {
                    m_pTransformFn(pDest, lDestStride,
                                   pSrc, lSrcStride,
                                   m_imageWidthInPixels, m_imageHeightInPixels);
                }
                else
                {
                    throw new COMException("Transform type not set", (int)HResult.E_UNEXPECTED);
                }

                // Set the data size on the output buffer.
                MFError throwonhr = pOut.SetCurrentLength(m_cbImageSize);
            }
            finally
            {
                // Unlock the buffers.
                UnlockIt(pDest, pOut2D, pOut);
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
        /// Copy a UYVY formatted image to an output buffer while converting to grayscale.
        /// </summary>
        /// <param name="pDest">Pointer to the destination buffer.</param>
        /// <param name="lDestStride">Stride of the destination buffer, in bytes.</param>
        /// <param name="pSrc">Pointer to the source buffer.</param>
        /// <param name="lSrcStride">Stride of the source buffer, in bytes.</param>
        /// <param name="dwWidthInPixels">Frame width in pixels.</param>
        /// <param name="dwHeightInPixels">Frame height, in pixels.</param>
        unsafe private void TransformImage_UYVY(
            IntPtr pDest,
            int lDestStride,
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            // This routine uses unsafe pointers for perf reasons.

            ushort* pSrc_Pixel = (ushort*)pSrc;
            ushort* pDest_Pixel = (ushort*)pDest;
            int lMySrcStride = (lSrcStride / 2);  // lSrcStride is in bytes and we need words
            int lMyDestStride = (lDestStride / 2); // lSrcStride is in bytes and we need words

            for (int y = 0; y < dwHeightInPixels; y++)
            {
                for (int x = 0; x < dwWidthInPixels; x++)
                {
                    // Byte order is U0 Y0 V0 Y1
                    // Each WORD is a byte pair (U/V, Y)
                    // Windows is little-endian so the order appears reversed.

                    pDest_Pixel[x] = (ushort)((pSrc_Pixel[x] & 0xFF00) | 0x0080);
                }

                pSrc_Pixel += lMySrcStride;
                pDest_Pixel += lMyDestStride;
            }
        }

        /// <summary>
        /// Copy a YUY2 formatted image to an output buffer while converting to grayscale.
        /// </summary>
        /// <param name="pDest">Pointer to the destination buffer.</param>
        /// <param name="lDestStride">Stride of the destination buffer, in bytes.</param>
        /// <param name="pSrc">Pointer to the source buffer.</param>
        /// <param name="lSrcStride">Stride of the source buffer, in bytes.</param>
        /// <param name="dwWidthInPixels">Frame width in pixels.</param>
        /// <param name="dwHeightInPixels">Frame height, in pixels.</param>
        unsafe private void TransformImage_YUY2(
            IntPtr pDest,
            int lDestStride,
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            // This routine uses unsafe pointers for perf reasons.

            ushort* pSrc_Pixel = (ushort*)pSrc;
            ushort* pDest_Pixel = (ushort*)pDest;
            int lMySrcStride = (lSrcStride / 2);  // lSrcStride is in bytes and we need words
            int lMyDestStride = (lDestStride / 2); // lSrcStride is in bytes and we need words

            for (int y = 0; y < dwHeightInPixels; y++)
            {
                for (int x = 0; x < dwWidthInPixels; x++)
                {
                    // Byte order is Y0 U0 Y1 V0 
                    // Each WORD is a byte pair (Y, U/V)
                    // Windows is little-endian so the order appears reversed.

                    pDest_Pixel[x] = (ushort)((pSrc_Pixel[x] & 0x00FF) | 0x8000);
                }

                pSrc_Pixel += lMySrcStride;
                pDest_Pixel += lMyDestStride;
            }
        }

        /// <summary>
        /// Copy a NV12 formatted image to an output buffer while converting to grayscale.
        /// </summary>
        /// <param name="pDest">Pointer to the destination buffer.</param>
        /// <param name="lDestStride">Stride of the destination buffer, in bytes.</param>
        /// <param name="pSrc">Pointer to the source buffer.</param>
        /// <param name="lSrcStride">Stride of the source buffer, in bytes.</param>
        /// <param name="dwWidthInPixels">Frame width in pixels.</param>
        /// <param name="dwHeightInPixels">Frame height, in pixels.</param>
        private void TransformImage_NV12(
            IntPtr pDest,
            int lDestStride,
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            // This routine uses unsafe pointers for perf reasons.

            // NV12 is planar: Y plane, followed by packed U-V plane.

            // Y plane
            for (int y = 0; y < dwHeightInPixels; y++)
            {
                CopyMemory(pDest, pSrc, dwWidthInPixels);
                pDest = new IntPtr(pDest.ToInt64() + lDestStride);
                pSrc = new IntPtr(pSrc.ToInt64() + lSrcStride);
            }

            // U-V plane
            for (int y = 0; y < dwHeightInPixels / 2; y++)
            {
                FillMemory(pDest, dwWidthInPixels, 0x80);
                pDest = new IntPtr(pDest.ToInt64() + lDestStride);
            }
        }

        #endregion

        #region Externs

        [DllImport("Kernel32.dll"), System.Security.SuppressUnmanagedCodeSecurity]
        private static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);

        [DllImport("kernel32.dll"), System.Security.SuppressUnmanagedCodeSecurity]
        private static extern void FillMemory(IntPtr destination, int len, byte val);

        #endregion
    }
}
