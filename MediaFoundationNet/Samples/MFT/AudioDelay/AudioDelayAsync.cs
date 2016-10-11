/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* What this file is for:
 * 
 * This file implements a Media Foundation transform (MFT) to add
 * delay to audio. It is essentially the same MFT as Microsoft's c++
 * mft_audiodelay sample (with a few improvements), but it shows how to use the
 * AsyncMFTBase to do the majority of the work.  All the 'audiodelay-specific'
 * code is in this file, so you can use the template to easily create your
 * own MFT.
 * 
 * Read the comments at the top of AsyncMFTBase.cs for getting started 
 * instructions
 */

using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;

namespace MFT_AudioDelayAsync
{
    [ComVisible(true),
    Guid("8F17BC18-4242-40E8-BEE8-06FD8B11EB33"),
    ClassInterface(ClassInterfaceType.None)]
    public sealed class AudioDelayAsync : AsyncMFTBase
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

            hr = ValidatePCMAudioType(pmt);

            return hr;
        }
        override protected void OnGetInputStreamInfo(ref MFTInputStreamInfo pStreamInfo)
        {
            pStreamInfo.dwFlags = MFTInputStreamInfoFlags.WholeSamples |
                MFTInputStreamInfoFlags.FixedSampleSize |
                MFTInputStreamInfoFlags.ProcessesInPlace;
            pStreamInfo.cbSize = m_Alignment;
        }
        override protected void OnGetOutputStreamInfo(ref MFTOutputStreamInfo pStreamInfo)
        {
            pStreamInfo.dwFlags = MFTOutputStreamInfoFlags.WholeSamples |
                                  MFTOutputStreamInfoFlags.FixedSampleSize |
                                  MFTOutputStreamInfoFlags.ProvidesSamples;
            pStreamInfo.cbSize = m_Alignment;
        }
        override protected void OnProcessSample(IMFSample pInputSample, bool Discontinuity, int InputMessageNumber)
        {
            MFError throwonhr;

            // Set the Discontinuity flag on the sample that's going to OutputSample.
            HandleDiscontinuity(Discontinuity, pInputSample);

            int cb;
            int cbBytesProcessed = 0;   // How much data we processed.

            int cbInputLength;
            IntPtr pbInputData = IntPtr.Zero;

            IMFMediaBuffer pInputBuffer;
            // Convert to a single contiguous buffer.
            // NOTE: This does not cause a copy unless there are multiple buffers
            throwonhr = pInputSample.ConvertToContiguousBuffer(out pInputBuffer);

            try
            {
                // Lock the input buffer.
                throwonhr = pInputBuffer.Lock(out pbInputData, out cb, out cbInputLength);

                // Round to the next lowest multiple of nBlockAlign.
                cbBytesProcessed = cbInputLength - (cbInputLength % m_Alignment);

                // Process the data.
                ProcessAudio(pbInputData, pbInputData, cbBytesProcessed / m_Alignment);

                // Set the data length on the output buffer.
                throwonhr = pInputBuffer.SetCurrentLength(cbBytesProcessed);

                long hnsTime = 0;

                // Ignore failure if the input sample does not have a time stamp. This is 
                // not an error condition. The client may not care about time stamps, and 
                // we don't need them.
                if (Succeeded(pInputSample.GetSampleTime(out hnsTime)))
                {
                    long hnsDuration = (cbBytesProcessed / m_AvgBytesPerSec) * UNITS;
                    m_rtTimestamp = hnsTime + hnsDuration;

                }
                else
                {
                    m_rtTimestamp = -1;
                }

                OutputSample(pInputSample, InputMessageNumber);
            }
            finally
            {
                if (pbInputData != IntPtr.Zero)
                {
                    pInputBuffer.Unlock();
                }

                SafeRelease(pInputBuffer);
            }
        }

        override protected HResult OnEnumInputTypes(int dwTypeIndex, out IMFMediaType pInputType)
        {
            return CreatePartialType(dwTypeIndex, MFMediaType.Audio, m_MediaSubtypes, out pInputType);
        }
        override protected void OnSetInputType()
        {
            IMFMediaType pmt = InputType;

            if (pmt != null)
            {
                m_Alignment = MFExtern.MFGetAttributeUINT32(pmt, MFAttributesClsid.MF_MT_AUDIO_BLOCK_ALIGNMENT, 0);
                m_AvgBytesPerSec = MFExtern.MFGetAttributeUINT32(pmt, MFAttributesClsid.MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 0);
                m_SamplesPerSec = MFExtern.MFGetAttributeUINT32(pmt, MFAttributesClsid.MF_MT_AUDIO_SAMPLES_PER_SECOND, 0);
                m_BitsPerSample = MFExtern.MFGetAttributeUINT32(pmt, MFAttributesClsid.MF_MT_AUDIO_BITS_PER_SAMPLE, 0);
                m_NumChannels = MFExtern.MFGetAttributeUINT32(pmt, MFAttributesClsid.MF_MT_AUDIO_NUM_CHANNELS, 0);

                // If the output type isn't set yet, we can pre-populate it, 
                // since output must always exactly equal input.  This can 
                // save a (tiny) bit of time in negotiating types.

                OnSetOutputType();
            }
            else
            {
                m_Alignment = 0;
                m_AvgBytesPerSec = 0;
                m_SamplesPerSec = 0;
                m_BitsPerSample = 0;
                m_NumChannels = 0;

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

        protected override void OnDrain(int InputMessageNumber)
        {
            bool bDone = m_cbDelayBuffer == 0;
            m_cbTailSamples = m_cbDelayBuffer;

            if (!bDone)
            {
                ProcessEffectTail(InputMessageNumber);
            }
        }
        protected override void OnStartStream()
        {
            AllocateStreamingResources();
        }
        protected override void OnEndStream()
        {
            FreeStreamingResources();
        }

        #endregion

        #region Member variables

        private const int UNITS = 10000000;            // 1 sec = 1 * UNITS
        private const int DEFAULT_WET_DRY_MIX = 25;    // Percentage of "wet" (delay) audio in the mix.
        private const int DEFAULT_DELAY = 1000;        // Delay in msec

        // MF_AUDIODELAY_DELAY_LENGTH: {95915546-B07C-4234-A237-1AF27187DEEE}
        // Type: UINT32
        // Specifies the length of the delay effect, in milliseconds.
        // This attribute must be set before the MFT_MESSAGE_NOTIFY_BEGIN_STREAMING
        // message is sent, or before the first call to ProcessInput. 
        private readonly Guid MF_AUDIODELAY_DELAY_LENGTH = new Guid(0x95915546, 0xb07c, 0x4234, 0xa2, 0x37, 0x1a, 0xf2, 0x71, 0x87, 0xde, 0xee);

        // MF_AUDIODELAY_WET_DRY_MIX: {72127F43-5878-4ea8-8269-D1AF3BB11CB2}
        // Type: UINT32
        // Specifies the wet/dry mix. (Range: 0 - 100. 0 = no delay, 100 = all delay)
        // This attribute can be set before each call to ProcessOutput()
        private readonly Guid MF_AUDIODELAY_WET_DRY_MIX = new Guid(0x72127f43, 0x5878, 0x4ea8, 0x82, 0x69, 0xd1, 0xaf, 0x3b, 0xb1, 0x1c, 0xb2);

        private readonly Guid[] m_MediaSubtypes = new Guid[] { MFMediaType.PCM };

        private IntPtr m_pbDelayBuffer;
        private int m_cbDelayBuffer;
        private IntPtr m_pbDelayPtr;
        private int m_dwDelay;
        private int m_cbTailSamples;
        private long m_rtTimestamp;

        private int m_Alignment;
        private int m_AvgBytesPerSec;
        private int m_SamplesPerSec;
        private int m_BitsPerSample;
        private int m_NumChannels;

        #endregion

        // The '1' indicates there should only be 1 processing thread.
        // AudioDelayAsync doesn't support more than 1 thread.
        public AudioDelayAsync()
            : base(1)
        {
            Trace("AudioDelayAsync Constructor");

            m_pbDelayBuffer = IntPtr.Zero;
            m_cbDelayBuffer = 0;
            m_pbDelayPtr = IntPtr.Zero;
            m_dwDelay = DEFAULT_DELAY;
            m_cbTailSamples = 0;
            m_rtTimestamp = -1;

            // Add some attributes to the MFTs attribute list.
            IMFAttributes ia = Attributes;
            MFError throwonhr;

            throwonhr = ia.SetUINT32(MF_AUDIODELAY_WET_DRY_MIX, DEFAULT_WET_DRY_MIX);
            throwonhr = ia.SetUINT32(MF_AUDIODELAY_DELAY_LENGTH, DEFAULT_DELAY);

            m_Alignment = 0;
            m_AvgBytesPerSec = 0;
            m_SamplesPerSec = 0;
            m_BitsPerSample = 0;
            m_NumChannels = 0;
        }

        ~AudioDelayAsync()
        {
            Trace("AudioDelayAsync Destructor");
            FreeStreamingResources();
        }

        #region COM Registration methods

        [ComRegisterFunctionAttribute]
        static private void DllRegisterServer(Type t)
        {
            HResult hr = MFExtern.MFTRegister(
                t.GUID,
                MFTransformCategory.MFT_CATEGORY_AUDIO_EFFECT,
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

        /// <summary>Generates the "tail" of the audio effect.</summary>
        /// <param name="InputMessageNumber">Message number to use with OutputSample.</param>
        /// <remarks>
        /// Generates the "tail" of the audio effect. The tail is the portion
        /// of the delay effect that is heard after the input stream ends.
        /// 
        /// To generate the tail, the client must drain the MFT by sending
        /// the MFT_MESSAGE_COMMAND_DRAIN message and then call ProcessOutput
        /// to get the tail samples.
        /// </remarks>
        private void ProcessEffectTail(int InputMessageNumber)
        {
            IMFMediaBuffer pOutputBuffer = null;

            MFError throwonhr;
            IntPtr pbOutputData = IntPtr.Zero;   // Pointer to the memory in the output buffer.
            int cbOutputLength = 0;     // Size of the output buffer.
            int cbBytesProcessed = 0;   // How much data we processed.

            IMFSample pOutSample = null;

            // Allocate an output buffer.
            throwonhr = MFExtern.MFCreateMemoryBuffer(m_cbTailSamples, out pOutputBuffer);

            try
            {
                throwonhr = MFExtern.MFCreateSample(out pOutSample);
                throwonhr = pOutSample.AddBuffer(pOutputBuffer);

                // Lock the output buffer.
                int cb;
                throwonhr = pOutputBuffer.Lock(out pbOutputData, out cbOutputLength, out cb);

                // Calculate how many audio samples we can process.
                cbBytesProcessed = Math.Min(m_cbTailSamples, cbOutputLength);

                // Round to the next lowest multiple of nBlockAlign.
                cbBytesProcessed -= (cbBytesProcessed % m_Alignment);

                // Fill the output buffer with silence, because we are also using it as the input buffer.
                FillBufferWithSilence(pbOutputData, cbBytesProcessed);

                // Process the data.
                ProcessAudio(pbOutputData, pbOutputData, cbBytesProcessed / m_Alignment);

                // Set the data length on the output buffer.
                throwonhr = pOutputBuffer.SetCurrentLength(cbBytesProcessed);

                if (m_rtTimestamp >= 0)
                {
                    long hnsDuration = (cbBytesProcessed / m_AvgBytesPerSec) * UNITS;

                    // Set the time stamp and duration on the output sample.
                    throwonhr = pOutSample.SetSampleTime(m_rtTimestamp);
                    throwonhr = pOutSample.SetSampleDuration(hnsDuration);
                }

                // Done.
                m_cbTailSamples = 0;

                OutputSample(pOutSample, InputMessageNumber);
            }
            catch
            {
                SafeRelease(pOutSample);
                throw;
            }
            finally
            {
                if (pbOutputData != IntPtr.Zero)
                {
                    pOutputBuffer.Unlock();
                }
                SafeRelease(pOutputBuffer);
            }
        }

        /// <summary>Allocates resources needed to process data.</summary>
        private void AllocateStreamingResources()
        {
            // Get the delay length. 
            m_dwDelay = MFExtern.MFGetAttributeUINT32(Attributes, MF_AUDIODELAY_DELAY_LENGTH, DEFAULT_DELAY);

            // A zero-length delay buffer will complicate things, so disallow zero.
            // Use the default instead.
            if (m_dwDelay <= 0)
            {
                m_dwDelay = DEFAULT_DELAY;
            }
            else
            {
                // Make sure the delay buffer won't exceed MAXDWORD bytes.
                m_dwDelay = Math.Min(m_dwDelay, int.MaxValue / (m_SamplesPerSec * m_Alignment));
            }

            // Allocate the buffer that holds the delayed samples.
            int cbDelayBuffer = (m_dwDelay * m_SamplesPerSec * m_Alignment) / 1000;

            if (cbDelayBuffer != m_cbDelayBuffer)
            {
                if (m_pbDelayBuffer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(m_pbDelayBuffer);
                }
                m_cbDelayBuffer = cbDelayBuffer;
                m_pbDelayBuffer = Marshal.AllocCoTaskMem(m_cbDelayBuffer);
            }

            FillBufferWithSilence(m_pbDelayBuffer, m_cbDelayBuffer);

            // Set the current pointer to the start of the buffer.
            m_pbDelayPtr = m_pbDelayBuffer;
        }

        /// <summary>Releases resources allocated during streaming.</summary>
        private void FreeStreamingResources()
        {
            // Free the delay buffer.  Don't need to check for null.
            Marshal.FreeCoTaskMem(m_pbDelayBuffer);

            m_pbDelayBuffer = IntPtr.Zero;
            m_pbDelayPtr = IntPtr.Zero;

            m_cbDelayBuffer = 0;
            m_cbTailSamples = 0;
        }

        /// <summary>Increment pointer within circular delay buffer.</summary>
        /// <param name="size">Number of bytes by which to increase pointer.</param>
        void IncrementDelayPtr(int size)
        {
            m_pbDelayPtr += size;
            if ((m_pbDelayPtr + size).ToInt64() > (m_pbDelayBuffer + m_cbDelayBuffer).ToInt64())
            {
                m_pbDelayPtr = m_pbDelayBuffer;
            }
        }

        /// <summary>Processes a block of audio data.</summary>
        /// <param name="pbDest">Destination buffer.</param>
        /// <param name="pbInputData">Buffer that contains the input data.</param>
        /// <param name="dwQuanta">Number of audio samples to process.</param>
        /// <returns></returns>
        unsafe private void ProcessAudio(IntPtr pbDest, IntPtr pbInputData, int dwQuanta)
        {
            int nWet = 0;  // Wet portion of wet/dry mix
            int sample = 0, channel = 0, cChannels = 0;

            cChannels = m_NumChannels;

            // Get the wet/dry mix.
            nWet = MFExtern.MFGetAttributeUINT32(Attributes, MF_AUDIODELAY_WET_DRY_MIX, DEFAULT_WET_DRY_MIX);

            // Clip the value to [0...100]
            nWet = Math.Min(nWet, 100);

            if (m_BitsPerSample == 8)
            {
                for (sample = 0; sample < dwQuanta; sample++)
                {
                    for (channel = 0; channel < cChannels; channel++)
                    {
                        // 8-bit sound is 0..255 with 128 == silence

                        // Get the input sample and normalize to -128 .. 127
                        int i = ((byte*)pbInputData)[sample * cChannels + channel] - 128;

                        // Get the delay sample and normalize to -128 .. 127
                        int delay = ((byte*)m_pbDelayPtr)[0] - 128;

                        ((byte*)m_pbDelayPtr)[0] = (byte)(i + 128);
                        IncrementDelayPtr(sizeof(byte));

                        i = (i * (100 - nWet)) / 100 + (delay * nWet) / 100;

                        // Truncate
                        if (i > 127)
                        {
                            i = 127;
                        }
                        else if (i < -128)
                        {
                            i = -128;
                        }

                        ((byte*)pbDest)[sample * cChannels + channel] = (byte)(i + 128);
                    }
                }
            }
            else  // 16-bit
            {
                for (sample = 0; sample < dwQuanta; sample++)
                {
                    for (channel = 0; channel < cChannels; channel++)
                    {
                        int i = ((short*)pbInputData)[sample * cChannels + channel];

                        int delay = ((short*)m_pbDelayPtr)[0];

                        ((short*)m_pbDelayPtr)[0] = (short)(i);
                        IncrementDelayPtr(sizeof(short));

                        i = (i * (100 - nWet)) / 100 + (delay * nWet) / 100;

                        // Truncate
                        if (i > 32767)
                        {
                            i = 32767;
                        }
                        else if (i < -32768)
                        {
                            i = -32768;
                        }

                        ((short*)pbDest)[sample * cChannels + channel] = (short)i;
                    }
                }
            }
        }

        /// <summary>Fill a buffer with silence.</summary>
        /// <param name="pBuffer">Pointer to the buffer to fill.</param>
        /// <param name="cb">Size of the buffer.</param>
        private void FillBufferWithSilence(IntPtr pBuffer, int cb)
        {
            Debug.Assert(pBuffer != IntPtr.Zero);

            byte fill;

            // The definition of 'silence' depends on the audio format.
            if (m_BitsPerSample == 8)
            {
                fill = 0x80;
            }
            else
            {
                fill = 0;
            }

            FillMemory(pBuffer, cb, fill);
        }

        /// <summary>Validate a PCM audio media type.</summary>
        /// <param name="pmt">The audio type to validate.</param>
        /// <returns>S_Ok if valid, else MF_E_INVALIDMEDIATYPE.</returns>
        HResult ValidatePCMAudioType(IMFMediaType pmt)
        {
            HResult hr = HResult.S_OK;

            hr = CheckMediaType(pmt, MFMediaType.Audio, m_MediaSubtypes);
            if (Succeeded(hr))
            {
                // Get attributes from the media type.
                // Each of these attributes is required for uncompressed PCM
                // audio, so fail if any are not present.

                int nChannels = 0;
                int nSamplesPerSec = 0;
                int nAvgBytesPerSec = 0;
                int nBlockAlign = 0;
                int wBitsPerSample = 0;

                MFError throwonhr;

                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_NUM_CHANNELS, out nChannels);
                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_SAMPLES_PER_SECOND, out nSamplesPerSec);
                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_AVG_BYTES_PER_SECOND, out nAvgBytesPerSec);
                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_BLOCK_ALIGNMENT, out nBlockAlign);
                throwonhr = pmt.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_BITS_PER_SAMPLE, out wBitsPerSample);

                // Validate the values.
 
                if (
                    (nChannels == 1 || nChannels == 2) &&
                    (wBitsPerSample == 8 || wBitsPerSample == 16) &&
                    (nBlockAlign == nChannels * (wBitsPerSample / 8)) &&
                    (nSamplesPerSec < (int)(int.MaxValue / nBlockAlign)) && 
                    (nAvgBytesPerSec == nSamplesPerSec * nBlockAlign)
                   )
                {
                    hr = HResult.S_OK;
                }
                else
                {
                    hr = HResult.MF_E_INVALIDMEDIATYPE;
                }
            }

            return hr;
        }

        #endregion

        #region Externs

        [DllImport("kernel32.dll"), System.Security.SuppressUnmanagedCodeSecurity]
        private static extern void FillMemory(IntPtr destination, int len, byte val);

        #endregion
    }
}
