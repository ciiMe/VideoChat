/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

// This file implements a Media Foundation transform (MFT).  It uses the 
// abstract MFTBase class to handle the majority of the plumbing.  
// In an effort to improve performance, it has a processing thread that starts
// doing in place processing as soon as the sample arrives to ProcessInput.
// Since clients may call other IMFTransform methods before actually
// retrieving the sample via ProcessOutput, there is a *bit* of savings.
//
// The MFT can handle 3 different media types:
//
//    YUY2
//    UYVY
//    NV12
// 
// The processing in this MFT is done in OnProcessOutput, and it converts
// all pixels in images to grayscale.
//
// A few things to know:
// - The processing is done "in-place" (ie the input sample is modified).
// - The output types must be exactly equal to the input types.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace MFT_GrayscaleEx
{
    [ComVisible(true),
    Guid("81FE27FA-5BAC-4CB8-8F35-F2366FB84035"),
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

        override protected HResult OnProcessOutput(ref MFTOutputDataBuffer pOutputSamples)
        {
            HResult hr;

            // Since we specified MFTOutputStreamInfoFlags.ProvidesSamples, this should be null.
            if (pOutputSamples.pSample == IntPtr.Zero)
            {
                // Set status flags.
                pOutputSamples.dwStatus = MFTOutputDataBufferFlags.None;

                // Wait for the thread to finish processing.
                m_SampleDone.WaitOne();

                pOutputSamples.pSample = Marshal.GetIUnknownForObject(InputSample);

                // This does an implicit Marshal.ReleaseComObject, which is
                // a bit risky, since some other .Net component could have a 
                // pointer to this same object, and this would yank the RCW 
                // out from under them.  But we go thru a LOT of samples.
                InputSample = null;

                hr = m_hr;
            }
            else
            {
                hr = HResult.E_INVALIDARG;
            }

            return hr;
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
                    throw new COMException("bad type", (int)HResult.E_UNEXPECTED);
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

            if (Succeeded(hr))
            {
                // Tell the background thread to start processing;
                m_SampleReady.Set();
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

        private AutoResetEvent m_SampleReady;
        private AutoResetEvent m_SampleDone;

        private HResult m_hr;

        private Thread m_ProcessThread;

        private delegate void TransformImage(IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels);

        #endregion

        public Grayscale()
            : base()
        {
            Trace("Grayscale Constructor");

            m_pTransformFn = null;

            m_MediaSubtypes = new Guid[] { FOURCC_NV12.ToMediaSubtype(), FOURCC_YUY2.ToMediaSubtype(), FOURCC_UYVY.ToMediaSubtype() };

            m_SampleDone = new AutoResetEvent(false);
            m_SampleReady = new AutoResetEvent(false);

            m_ProcessThread = new Thread(new ThreadStart(ProcessingThread));
            m_ProcessThread.IsBackground = true;
#if DEBUG
            m_ProcessThread.Name = "MFT Processing Thread for Grayscale";
#endif
            m_ProcessThread.Start();
        }

        ~Grayscale()
        {
            Trace("Grayscale Destructor");

            m_ProcessThread.Abort();

            m_SampleReady.Dispose();
            m_SampleDone.Dispose();
        }

        public void ProcessingThread()
        {
            while (true)
            {
                m_SampleReady.WaitOne();

                IMFMediaBuffer pInput = null;

                try
                {
                    // Get the data buffer from the input sample.  If the sample has
                    // multiple buffers, you might be able to get (slightly) better
                    // performance processing each buffer in turn rather than forcing
                    // a new, full-sized buffer to get created.
                    m_hr = InputSample.ConvertToContiguousBuffer(out pInput);
                    if (Succeeded(m_hr))
                    {
                        m_hr = DoWork(pInput);
                    }
                }
                catch (Exception e)
                {
                    m_hr = (HResult)Marshal.GetHRForException(e);
                }
                finally
                {
                    // If (somewhere) there is .Net code that is holding on to an instance of
                    // the same buffer as pInput, this will yank the RCW out from underneath 
                    // it, probably causing it to crash.  But if we don't release it, our memory 
                    // usage explodes.
                    SafeRelease(pInput);
                    m_SampleDone.Set();
                }
            }
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

        // Get the buffers and sizes to be modified, then pass them
        // to the appropriate Update_* routine.
        private HResult DoWork(IMFMediaBuffer pIn)
        {
            MFError throwonhr;
            int cb;

            IntPtr pSrc;			// Source buffer.
            int lSrcStride = 0;		// Source stride.

            bool bLockedInputBuffer = false;

            IMF2DBuffer pIn2D;

            // While there are exceptions thrown here, they should never happen
            // in real life.

            // Lock the input buffer. Use IMF2DBuffer if available.
            pIn2D = pIn as IMF2DBuffer;
            if (pIn2D != null)
            {
                throwonhr = pIn2D.Lock2D(out pSrc, out lSrcStride);
            }
            else
            {
                int ml;
                throwonhr = pIn.Lock(out pSrc, out ml, out cb);
                lSrcStride = m_lStrideIfContiguous;
            }
            bLockedInputBuffer = true;

            // Invoke the image transform function.
            if (m_pTransformFn != null)
            {
                m_pTransformFn(pSrc, lSrcStride,
                    m_imageWidthInPixels, m_imageHeightInPixels);
            }
            else
            {
                return HResult.E_UNEXPECTED;
            }

            if (bLockedInputBuffer)
            {
                if (pIn2D != null)
                {
                    throwonhr = pIn2D.Unlock2D();
                }
                else
                {
                    throwonhr = pIn.Unlock();
                }
            }

            // Set the data size on the output buffer.
            throwonhr = pIn.SetCurrentLength(m_cbImageSize);

            return HResult.S_OK;
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
        /// Converts an image in UYVY format to grayscale.
        /// </summary>
        /// <param name="pSrc">Pointer to the source buffer.</param>
        /// <param name="lSrcStride">Stride of the source buffer, in bytes.</param>
        /// <param name="dwWidthInPixels">Frame width in pixels.</param>
        /// <param name="dwHeightInPixels">Frame height, in pixels.</param>
        unsafe private static void Update_UYVY(
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            // This routine uses unsafe pointers for perf reasons.

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
        unsafe private static void Update_YUY2(
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            // This routine uses unsafe pointers for perf reasons.

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
        private static void Update_NV12(
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels
            )
        {
            // This routine uses unsafe pointers for perf reasons.

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
