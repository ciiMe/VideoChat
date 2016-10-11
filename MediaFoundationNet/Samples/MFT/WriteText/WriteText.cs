/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* What this file is for:
 * 
 * This file implements a Media Foundation transforms (MFT) to
 * draw graphics on frames.
 * 
 * This MFT requires RGB32 mediatype input.
 */

using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace MFT_WriteTextAsync
{
    [ComVisible(true),
    Guid("FBC659ED-BACD-4F8F-9560-EC26319C935C"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class WriteTextAsync : AsyncMFTBase
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

            hr = CheckMediaType(pmt, MFMediaType.Video, m_MediaSubtypes);

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

                m_FrameCount++;
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
            m_lStrideIfContiguous = 0;

            IMFMediaType pmt = InputType;

            // type can be null to clear
            if (pmt != null)
            {
                throwonhr = MFExtern.MFGetAttributeSize(pmt, MFAttributesClsid.MF_MT_FRAME_SIZE, out m_imageWidthInPixels, out m_imageHeightInPixels);

                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_DEFAULT_STRIDE, out m_lStrideIfContiguous);

                // Calculate the image size (not including padding)
                m_cbImageSize = m_imageHeightInPixels * m_lStrideIfContiguous;

                float fSize;

                // scale the font size in some portion to the video image
                fSize = 9;
                fSize *= (m_imageWidthInPixels / 64.0f);

                if (m_fontOverlay != null)
                    m_fontOverlay.Dispose();

                m_fontOverlay = new Font("Times New Roman", fSize, System.Drawing.FontStyle.Bold,
                    System.Drawing.GraphicsUnit.Point);

                // scale the font size in some portion to the video image
                fSize = 5;
                fSize *= (m_imageWidthInPixels / 64.0f);

                if (m_transparentFont != null)
                    m_transparentFont.Dispose();

                m_transparentFont = new Font("Tahoma", fSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);

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

        protected override void OnStartStream()
        {
            m_FrameCount = 0;
        }

        #endregion

        #region Member variables

        // Format information
        private int m_imageWidthInPixels;
        private int m_imageHeightInPixels;
        private int m_cbImageSize;              // Image size, in bytes.
        private int m_lStrideIfContiguous;

        private static readonly SolidBrush m_transparentBrush = new SolidBrush(Color.FromArgb(96, 0, 0, 255));
        private Font m_fontOverlay;
        private Font m_transparentFont;

        private int m_FrameCount;
        private readonly Guid[] m_MediaSubtypes = new Guid[] { MFMediaType.RGB32 };

        #endregion

        // The '1' indicates there should only be 1 processing thread.
        // As written, this MFT won't work correctly for multiple threads.
        // It doesn't really benefit from it anyway.  See the TypeConverter 
        // for an MFT that does.
        public WriteTextAsync()
            : base(1)
        {
            Trace("WriteTextAsync Constructor");
        }

        ~WriteTextAsync()
        {
            Trace("WriteTextAsync Destructor");

            SafeRelease(m_transparentBrush);
            SafeRelease(m_fontOverlay);
            SafeRelease(m_transparentFont);
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

        // Get the buffers and sizes to be modified, then pass them
        // to the textwrite routine.
        private void DoWork(IMFMediaBuffer pIn)
        {
            IntPtr pSrc = IntPtr.Zero;			// Source buffer.
            int lSrcStride;		// Source stride.
            IMF2DBuffer pIn2D = null;

            // Lock the input buffer. Use IMF2DBuffer if available.
            Lockit(pIn, out pIn2D, out lSrcStride, out pSrc);

            try
            {
                WriteIt(pSrc);
            }
            finally
            {
                UnlockIt(pSrc, pIn2D, pIn);
            }
        }

        private void WriteIt(IntPtr pBuffer)
        {
            // The strings to display.
            string sString1 = "Hi mom!";
            string sString2 = m_FrameCount.ToString();

            // A wrapper around the video data.
            using (Bitmap v = new Bitmap(m_imageWidthInPixels, m_imageHeightInPixels, m_lStrideIfContiguous, PixelFormat.Format32bppRgb, pBuffer))
            {
                using (Graphics g = Graphics.FromImage(v))
                {
                    float sLeft;
                    float sTop;
                    SizeF d;

                    // String1 goes right in the middle of the video.
                    d = g.MeasureString(sString1, m_fontOverlay);

                    sLeft = (m_imageWidthInPixels - d.Width) / 2.0f;
                    sTop = (m_imageHeightInPixels - d.Height) / 2.0f;

                    g.DrawString(sString1, m_fontOverlay, System.Drawing.Brushes.Red, sLeft, sTop, StringFormat.GenericTypographic);

                    // Add a frame number in the bottom right.
                    d = g.MeasureString(sString2, m_transparentFont);

                    sLeft = (m_imageWidthInPixels - d.Width) - 10.0f;
                    sTop = (m_imageHeightInPixels - d.Height) - 10.0f;

                    g.DrawString(sString2, m_transparentFont, m_transparentBrush, sLeft, sTop, StringFormat.GenericTypographic);
                }
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

        #endregion
    }
}
