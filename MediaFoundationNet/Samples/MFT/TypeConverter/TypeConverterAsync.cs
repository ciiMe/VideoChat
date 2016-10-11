/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* What this file is for:
 * 
 * This file implements a Media Foundation transform (MFT) to turn YUY2 video
 * into RGB32.  While not a particularly useful thing to do, it does show how 
 * to use the AsyncMFTBase to do the majority of the work.  All the 
 * 'TypeConverter-specific' code is in this file, so you can use the template 
 * to easily create your own async MFT.
 * 
 * Read the comments at the top of AsyncMFTBase.cs for getting started 
 * instructions
 * 
 * A few things to know about the TypeConverter MFT:
 * 
 * - The input (YUY2) and output (RGB32) media types are fixed.
 * - Changing the number of threads (the parameter to the base
 * class constructor) can have a big impact on the performance
 * of this MFT.
 * - While it could, this MFT does NOT register itself as a decoder using
 * MFTRegister.  If it did, it would get loaded every time MF needed the
 * type of conversion it supplies (or even if MF thinks it *might*).
 */

using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace TypeConverter
{
    [ComVisible(true),
    Guid("567C527F-9025-4057-BE42-527554D10ADE"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class TypeConverterAsync : AsyncMFTBase
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

            hr = OnCheckMediaType(pmt, m_MediaSubtypesIn);

            return hr;
        }
        override protected void OnGetInputStreamInfo(ref MFTInputStreamInfo pStreamInfo)
        {
            pStreamInfo.dwFlags = MFTInputStreamInfoFlags.WholeSamples |
                MFTInputStreamInfoFlags.SingleSamplePerBuffer |
                MFTInputStreamInfoFlags.FixedSampleSize;
            pStreamInfo.cbSize = m_cbImageSizeInput;
        }
        override protected void OnGetOutputStreamInfo(ref MFTOutputStreamInfo pStreamInfo)
        {
            pStreamInfo.dwFlags = MFTOutputStreamInfoFlags.WholeSamples |
                MFTOutputStreamInfoFlags.SingleSamplePerBuffer |
                 MFTOutputStreamInfoFlags.FixedSampleSize |
                 MFTOutputStreamInfoFlags.ProvidesSamples;
            pStreamInfo.cbSize = m_cbImageSizeOutput;
        }
        protected override void OnProcessSample(IMFSample pInputSample, bool Discontinuity, int InputMessageNumber)
        {
            MFError throwonhr;

            // While we accept types that *might* be interlaced, if we actually receive
            // an interlaced sample, reject it.
            if (m_MightBeInterlaced)
            {
                int ix;

                // Returns a bool: true = interlaced, false = progressive
                HResult hr = pInputSample.GetUINT32(MFAttributesClsid.MFSampleExtension_Interlaced, out ix);

                // Can be S_False.
                if (hr != HResult.S_OK || ix != 0)
                    throw new COMException("Interlaced", (int)HResult.E_FAIL);
            }

            IMFMediaBuffer pInput = null;
            IMFMediaBuffer pOutput = null;
            IMFSample pOutSample = null;

            // Get the data buffer from the input sample.  If the sample has
            // multiple buffers, you might be able to get (slightly) better
            // performance processing each buffer in turn rather than forcing
            // a new, full-sized buffer to get created.
            throwonhr = pInputSample.ConvertToContiguousBuffer(out pInput);

            try
            {
                // Make a duplicate of the input sample
                DuplicateSample(pInputSample, out pOutSample);

                // Set the Discontinuity flag on the sample that's going to OutputSample.
                HandleDiscontinuity(Discontinuity, pOutSample);

                throwonhr = pOutSample.ConvertToContiguousBuffer(out pOutput);

                // Process it.
                DoWork(pInput, pOutput);

                // Send the new output sample to the output sample queue.
                OutputSample(pOutSample, InputMessageNumber);
            }
            catch
            {
                SafeRelease(pOutSample);
                throw;
            }
            finally
            {
                // If (somewhere) there is .Net code that is holding on to an instance of
                // the same buffer as pInput, this will yank the RCW out from underneath 
                // it, probably causing it to crash.  But if we don't release it, our memory 
                // usage explodes.
                SafeRelease(pInput);

                SafeRelease(pOutput);
                SafeRelease(pInputSample);
            }
        }

        override protected HResult OnEnumInputTypes(int dwTypeIndex, out IMFMediaType pInputType)
        {
            return CreatePartialType(dwTypeIndex, MFMediaType.Video, m_MediaSubtypesIn, out pInputType);
        }

        override protected void OnSetInputType()
        {
            MFError throwonhr;

            m_imageWidthInPixels = 0;
            m_imageHeightInPixels = 0;
            m_cbImageSizeInput = 0;
            m_cbImageSizeOutput = 0;
            m_StrideOut = 0;

            IMFMediaType pmt = InputType;

            // type can be null to clear
            if (pmt != null)
            {
                TraceAttributes(pmt);

                throwonhr = MFExtern.MFGetAttributeSize(pmt, MFAttributesClsid.MF_MT_FRAME_SIZE, out m_imageWidthInPixels, out m_imageHeightInPixels);

                // Calculate the image size (not including padding)
                m_StrideOut = m_imageWidthInPixels * 4;

                if (Failed(pmt.GetUINT32(MFAttributesClsid.MF_MT_DEFAULT_STRIDE, out m_StrideIn)))
                {
                    m_StrideIn = m_imageWidthInPixels * 2;
                }
                m_cbImageSizeInput = m_StrideIn * m_imageHeightInPixels;
                m_cbImageSizeOutput = m_StrideOut * m_imageHeightInPixels;

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
        }

        /// <summary>
        /// Given an input media type, create the associated output type.
        /// </summary>
        /// <param name="inType">The input type from which to create the output type</param>
        /// <returns>An output type generated from the input type.</returns>
        protected override IMFMediaType CreateOutputFromInput()
        {
            MFError throwonhr;
            IMFMediaType pOutputType = CloneMediaType(InputType);

            throwonhr = pOutputType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, m_MediaSubtypesOut[0]);
            throwonhr = pOutputType.SetUINT32(MFAttributesClsid.MF_MT_DEFAULT_STRIDE, m_StrideOut);
            throwonhr = pOutputType.SetUINT32(MFAttributesClsid.MF_MT_SAMPLE_SIZE, m_cbImageSizeOutput);

            return pOutputType;
        }

        #endregion

        #region Member variables

        // Format information
        private int m_imageWidthInPixels;
        private int m_imageHeightInPixels;
        private int m_cbImageSizeInput;
        private int m_cbImageSizeOutput;
        private int m_StrideIn;
        private int m_StrideOut;
        private bool m_MightBeInterlaced;

        private readonly Guid[] m_MediaSubtypesIn = new Guid[] {MFMediaType.YUY2};
        private readonly Guid[] m_MediaSubtypesOut = new Guid[] { MFMediaType.RGB32 };

        #endregion

        public TypeConverterAsync()
            : base((Environment.ProcessorCount + 1) / 2)
        {
            Trace("TypeConverter Constructor");
        }

        #region Helpers

        private void DuplicateSample(IMFSample pInSample, out IMFSample pOutSample)
        {
            MFError throwonhr;
            int flags;
            long lTime;

            throwonhr = MFExtern.MFCreateSample(out pOutSample);
            throwonhr = pInSample.CopyAllItems(pOutSample);

            HResult hr = pInSample.GetSampleDuration(out lTime);
            if (Succeeded(hr))
            {
                throwonhr = pOutSample.SetSampleDuration(lTime);
            }

            hr = pInSample.GetSampleTime(out lTime);
            if (Succeeded(hr))
            {
                throwonhr = pOutSample.SetSampleTime(lTime);
            }

            hr = pInSample.GetSampleFlags(out flags);
            if (Succeeded(hr))
            {
                throwonhr = pOutSample.SetSampleFlags(flags);
            }

            IMFMediaBuffer mb;

            throwonhr = MFExtern.MFCreateMemoryBuffer(m_imageHeightInPixels * m_imageWidthInPixels * 4, out mb);

            try
            {
                // Set the data size on the output buffer.
                throwonhr = mb.SetCurrentLength(m_cbImageSizeOutput);

                throwonhr = pOutSample.AddBuffer(mb);
            }
            finally
            {
                SafeRelease(mb);
            }
        }

        /// <summary>
        /// Validates a media type for this transform.
        /// </summary>
        /// <param name="pmt">The media type to validate.</param>
        /// <returns>S_Ok or MF_E_INVALIDTYPE.</returns>
        /// <remarks>Since both input and output types must be
        /// the same, they both call this routine.</remarks>
        private HResult OnCheckMediaType(IMFMediaType pmt, Guid[] gType)
        {
            HResult hr = HResult.S_OK;

            hr = CheckMediaType(pmt, MFMediaType.Video, gType);
            if (Succeeded(hr))
            {
                MFError throwonhr;
                int interlace;

                // Video must be progressive frames.
                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, out interlace);

                MFVideoInterlaceMode im = (MFVideoInterlaceMode)interlace;

                if (im == MFVideoInterlaceMode.Progressive)
                {
                    m_MightBeInterlaced = false;
                }
                else
                {
                    if (im == MFVideoInterlaceMode.MixedInterlaceOrProgressive)
                    {
                        m_MightBeInterlaced = true;
                    }
                    else
                    {
                        hr = HResult.MF_E_INVALIDTYPE;
                    }
                }
            }

            // In theory we should check to ensure sizes etc are the same as
            // input, but I'm just going to assume.

            return hr;
        }

        // Get the buffers and sizes to be modified, then pass them
        // to the transform routine.
        private void DoWork(IMFMediaBuffer pIn, IMFMediaBuffer pOut)
        {
            IntPtr pSrc = IntPtr.Zero;
            int lSrcStride = 0;
            IMF2DBuffer pIn2D = null;

            // Lock the input buffer. Use IMF2DBuffer if available.
            Lockit(pIn, out pIn2D, out lSrcStride, out pSrc);

            try
            {
                IntPtr pDest;
                int mlo;
                int cb;
                MFError throwonhr = pOut.Lock(out pDest, out mlo, out cb);

                try
                {
                    // Invoke the image transform function.
                    YUY2_TO_RGB32(pSrc, pDest, m_imageWidthInPixels, m_imageHeightInPixels, lSrcStride);
                }
                finally
                {
                    pOut.Unlock();
                }
            }
            finally
            {
                // Ignore error
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

                lDestStride = m_StrideIn;
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

        // Field is never assigned to, and will always have its default value 0
#pragma warning disable 649
        private struct YUYV
        {
            public byte Y;
            public byte U;
            public byte Y2;
            public byte V;
        }
#pragma warning restore 649

        unsafe static void YUY2_TO_RGB32(
            IntPtr ipSrc,
            IntPtr ipDest,
            int dwWidthInPixels,
            int dwHeightInPixels,
            int dwSrcStride
            )
        {
            // Assumes no padding on dest buffer

            YUYV* pSrcRow = (YUYV*)ipSrc;
            uint* pDestPixel = (uint*)ipDest;

            int dwUseWidth = dwWidthInPixels / 2;
            int dwUseStride = dwSrcStride / sizeof(YUYV);

            int y = dwHeightInPixels;
            do
            {
                YUYV* pSrcPixel = pSrcRow;

                int x = dwUseWidth;
                do
                {
                    int c = pSrcPixel->Y; c -= 16; c *= 298; c += 128;
                    int d = pSrcPixel->U; d -= 128;
                    int e = pSrcPixel->V; e -= 128;

                    // Expanding it out like this gains a bit of perf

                    uint i = 0;

                    int j = (c + 516 * d) >> 8; // blue

                    if (j > 0)
                    {
                        if (j < 255)
                        {
                            i = (uint)j;
                        }
                        else
                        {
                            i = 255;
                        }
                    }

                    j = (c - 100 * d - 208 * e) >> 8; // green

                    if (j > 0)
                    {
                        if (j < 255)
                        {
                            i |= ((uint)j) << 8;
                        }
                        else
                        {
                            i |= 255 << 8;
                        }
                    }

                    j = (c + 409 * e) >> 8; // red

                    if (j > 0)
                    {
                        if (j < 255)
                        {
                            i |= ((uint)j) << 16;
                        }
                        else
                        {
                            i |= 255 << 16;
                        }
                    }

                    *pDestPixel = i;

                    c = pSrcPixel->Y2; c -= 16; c *= 298; c += 128;
                    pDestPixel++;

                    i = 0;
                    j = (c + 516 * d) >> 8; // blue

                    if (j > 0)
                    {
                        if (j < 255)
                        {
                            i = (uint)j;
                        }
                        else
                        {
                            i = 255;
                        }
                    }

                    j = (c - 100 * d - 208 * e) >> 8; // green

                    if (j > 0)
                    {
                        if (j < 255)
                        {
                            i |= ((uint)j) << 8;
                        }
                        else
                        {
                            i |= 255 << 8;
                        }
                    }

                    j = (c + 409 * e) >> 8; // red
                    if (j > 0)
                    {
                        if (j < 255)
                        {
                            i |= ((uint)j) << 16;
                        }
                        else
                        {
                            i |= 255 << 16;
                        }
                    }

                    *pDestPixel = i;

                    pDestPixel++;
                    pSrcPixel++;
                    x--;
                } while (x > 0);

                // Account for padding on input buffers
                pSrcRow += dwUseStride;
                y--;
            } while (y > 0);
        }

        #endregion
    }
}
