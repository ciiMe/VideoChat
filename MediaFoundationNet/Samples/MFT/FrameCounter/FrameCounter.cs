/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* What this file is for:
 * 
 * This file implements two Media Foundation transforms (MFT) to count
 * frames.  One is synchronous, the other is asynchronous.
 * 
 * They are about the simplest MFTs one can write using the templates.
 * 
 * It is unlikely that you will need to write both sync and async 
 * versions of the same MFT.  It is done here only to illustrate the 
 * differences.
 * 
 * Read the docs in HowTo.txt or HowToAsync.txt
 * for getting started instructions on writing your own MFT.
 * 
 * Both MFTs can handle any video input media type, and outputs the same type.
 * Note that they each have their own unique guid.
 */

using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace FrameCounterSamples
{
    [ComVisible(true),
    Guid("FC8AFE7E-2624-4437-A6B8-D071C862A52B"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class FrameCounter : MFTBase
    {
        #region Overrides

        override protected HResult OnCheckInputType(IMFMediaType pmt)
        {
            HResult hr;

            // Accept any input type.
            if (OutputType == null)
            {
                hr = HResult.S_OK;
            }
            else
            {
                // Otherwise, proposed input must be identical to output.
                hr = IsIdentical(pmt, OutputType);
            }

            return hr;
        }
        override protected void OnGetInputStreamInfo(ref MFTInputStreamInfo pStreamInfo)
        {
            pStreamInfo.cbSize = 0;
            pStreamInfo.dwFlags = MFTInputStreamInfoFlags.ProcessesInPlace;
        }
        override protected void OnGetOutputStreamInfo(ref MFTOutputStreamInfo pStreamInfo)
        {
            pStreamInfo.cbSize = 0;
            pStreamInfo.dwFlags = MFTOutputStreamInfoFlags.ProvidesSamples;
        }
        protected override HResult OnProcessOutput(ref MFTOutputDataBuffer pOutputSamples)
        {
            HResult hr = HResult.S_OK;

            if (pOutputSamples.pSample == IntPtr.Zero)
            {
                // Synchronous MFTs don't (by default) have an IMFAttributes.
                // So I'm putting the sample number on the actual sample, just
                // to have some place to put it.
                MFError throwonhr = InputSample.SetUINT32(m_SampleCountGuid, m_iSampleCount);

                m_iSampleCount++;

                // The output sample is the input sample.
                pOutputSamples.pSample = Marshal.GetIUnknownForObject(InputSample);

                // Release the current input sample so we can get another one.
                InputSample = null;
            }
            else
            {
                hr = HResult.E_INVALIDARG;
            }

            return hr;
        }

        protected override HResult OnEnumInputTypes(int dwTypeIndex, out IMFMediaType pInputType)
        {
            // I'd like to skip implementing this, but while some clients 
            // don't require it (PlaybackFX), some do (MEPlayer/IMFMediaEngine).  
            // Although frame counting should be able to run against any type, 
            // we must at a minimum provide a major type.

            return CreatePartialType(dwTypeIndex, MFMediaType.Video, m_MediaSubtypes, out pInputType);
        }

        #endregion

        #region Member variables

        // This is not a standard MF attribute.
        private readonly Guid m_SampleCountGuid = new Guid("AA99FFD1-4DF6-45F5-8BB1-8AF5BAEDAA85");
        private int m_iSampleCount;
        private readonly Guid[] m_MediaSubtypes = new Guid[] { Guid.Empty };

        #endregion

        public FrameCounter()
            : base()
        {
            Trace("FrameCounter Constructor");

            m_iSampleCount = 0;
        }

#if DEBUG
        ~FrameCounter()
        {
            Debug.WriteLine(m_iSampleCount);
        }
#endif
    }

    [ComVisible(true),
    Guid("2137B262-D5D7-4F81-AE90-D3A2ECC66E14"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class FrameCounterAsync : AsyncMFTBase
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

            return HResult.S_OK;
        }
        override protected void OnGetInputStreamInfo(ref MFTInputStreamInfo pStreamInfo)
        {
            pStreamInfo.dwFlags = MFTInputStreamInfoFlags.ProcessesInPlace;
        }
        override protected void OnGetOutputStreamInfo(ref MFTOutputStreamInfo pStreamInfo)
        {
            pStreamInfo.dwFlags = MFTOutputStreamInfoFlags.ProvidesSamples;
        }
        override protected void OnProcessSample(IMFSample pInputSample, bool Discontinuity, int InputMessageNumber)
        {
            // Set the Discontinuity flag on the sample that's going to OutputSample.
            HandleDiscontinuity(Discontinuity, pInputSample);

            // Since m_pAttributes is not threadsafe, bad things
            // could happen here if m_ThreadCount > 1.  If I really
            // needed to support multiple threads here, I would
            // probably copy the m_Slims.Wait() logic from OutputSample.
            MFError throwonhr = Attributes.SetUINT32(m_SampleCountGuid, m_iSampleCount);

            m_iSampleCount++;

            // Send the unmodified input sample to the output sample queue.
            OutputSample(pInputSample, InputMessageNumber);
        }

        protected override HResult OnEnumInputTypes(int dwTypeIndex, out IMFMediaType ppType)
        {
            // I'd like to skip implementing this, but while some clients 
            // don't require it (PlaybackFX), some do (MEPlayer/IMFMediaEngine).  
            // Although frame counting should be able to run against any type, 
            // we must at a minimum provide a major type.

            return CreatePartialType(dwTypeIndex, MFMediaType.Video, m_MediaSubtypes, out ppType);
        }

#if DEBUG
        protected override void OnEndStream()
        {
            // The caller can use the IMFAttributes returned from our 
            // IMFTransform::GetAttributes to read this value.  For 
            // debugging purposes, we'll print it on shutdown.
            int i;
            Attributes.GetUINT32(m_SampleCountGuid, out i);
            Debug.WriteLine(i);
        }
#endif

        #endregion

        #region Member variables

        // This is not a standard MF attribute.
        private readonly Guid m_SampleCountGuid = new Guid("AA99FFD1-4DF6-45F5-8BB1-8AF5BAEDAA85");
        private int m_iSampleCount;
        private readonly Guid[] m_MediaSubtypes = new Guid[] { Guid.Empty };

        #endregion

        public FrameCounterAsync()
            : base()
        {
            Trace("FrameCounterAsync Constructor");

            MFError throwonhr = Attributes.SetUINT32(m_SampleCountGuid, 0);
        }
    }
}
