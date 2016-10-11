/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* File Version: 2015-06-26 from http://mfnet.sf.net */

/* What this file is for and how to use it:
 *
 * The goal of this file is to handle all the plumbing for writing an
 * asynchronous MFT, leaving just a few abstract routines that must be
 * implemented by a subclass to have a fully working c# Async MFT.
 *
 * To read about asynchronous MFTs, visit:
 *
 * https://msdn.microsoft.com/en-us/library/windows/desktop/dd317909(v=vs.85).aspx
 *
 * While IMFTransform allows for variations, this code assumes that there
 * will be exactly 1 input stream, and 1 output stream.
 *
 * This code handles all the work for:
 *
 * - Sending METransformNeedInput, METransformHaveOutput,
 * METransformDrainComplete, & METransformMarker events to
 * IMFMediaEventGenerator as required.
 * - Command Drain.
 * - Command Flush.
 * - Command Marker.
 * - Dynamic format changes.
 * - Processing is done on MFT-specific thread(s).
 * - Setting the IMFAttributes for async MFT operation.
 * - Validating parameters.
 * - Rejecting calls to unsupported methods.
 * - Unlock error handling.
 * - Shutdown via IMFShutdown.
 *
 * Since the framework handles all that for you, mostly what is left is
 * enumerating the IMFMediaTypes you support and performing the actual
 * transform.  Unless you have specific needs, no changes to this file
 * should be required.
 * 
 * For detailed instructions on how to use this file, see HowToAsync.txt.
 */

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using MediaFoundation.Misc;

namespace MediaFoundation.Transform
{
    [ComImport, System.Security.SuppressUnmanagedCodeSecurity,
    Guid("0000010c-0000-0000-C000-000000000046"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersist
    {
        [PreserveSig]
        HResult GetClassID(out Guid pClassID);
    }

    abstract public class AsyncMFTBase : COMBase, IMFTransform, IMFMediaEventGenerator, IMFShutdown, IPersist, ICustomQueryInterface
    {
        #region Members

        /// <summary>
        /// Synchronization object used by public entry points
        /// to prevent multiple methods from being invoked at
        /// once.  Some work (for example parameter validation)
        /// is done before invoking this lock.
        /// </summary>
        private object m_TransformLockObject;

        /// <summary>
        /// Input type set by SetInputType.
        /// </summary>
        /// <remarks>Can be null if not set yet or if reset by SetInputType(null).</remarks>
        private IMFMediaType m_pInputType;

        /// <summary>
        /// Output type set by SetOutputType.
        /// </summary>
        /// <remarks>Can be null if not set yet or if reset by
        /// SetOutputType(null).</remarks>
        private IMFMediaType m_pOutputType;

        /// <summary>
        /// Attributes returned to IMFTransform::GetAttributes.
        /// </summary>
        /// <remarks>The MF_TRANSFORM_ASYNC_UNLOCK is stored here.</remarks>
        private readonly IMFAttributes m_pAttributes;

        /// <summary>
        /// Queue of output samples to be returned to IMFTransform::ProcessOutput.
        /// </summary>
        /// <remarks>Derived classes should populate this by using OutputSample().</remarks>
        private readonly ConcurrentQueue<IMFSample> m_outSamples;
        /// <summary>
        /// MF Helper class to support IMFMediaEventGenerator.
        /// </summary>
        private readonly IMFMediaEventQueue m_events;

        /// <summary>
        /// Used to control how many samples are active at one time.
        /// </summary>
        /// <remarks>Defaults to m_ThreadCount * 2.  See SendNeedEvent for details.</remarks>
        private int m_Threshold;

        /// <summary>
        /// The number of processing threads.
        /// </summary>
        /// <remarks>Set by derived class' constructor.  Must be at least 1.</remarks>
        private readonly int m_ThreadCount;
        /// <summary>
        /// Used by dynamic format change to perform synchronization.
        /// </summary>
        private int m_ThreadsBlocked;
        /// <summary>
        /// Used by dynamic format change to perform synchronization.
        /// </summary>
        private SemaphoreSlim m_FormatEvent;

        /// <summary>
        /// A count of how many NeedInput messages have been sent that haven't
        /// yet been satisfied.  Also used by SendNeedEvent.
        /// </summary>
        private int m_NeedInputPending;
        /// <summary>
        /// Used to shut down the processing threads.
        /// </summary>
        private int m_ShutdownThreads;
        /// <summary>
        /// Have we been shutdown?
        /// </summary>
        private bool m_Shutdown;

        /// <summary>
        /// Has a stream been started that has not yet been ended?
        /// </summary>
        /// <remarks>Streams are not guaranteed to receive end stream 
        /// notices.</remarks>
        private bool m_StreamIsActive;

        /// <summary>
        /// Has the MF_TRANSFORM_ASYNC_UNLOCK been set?
        /// </summary>
        private bool m_Unlocked;
        /// <summary>
        /// Should the next sample be marked as Discontinuity?
        /// </summary>
        private bool m_bDiscontinuity;

        /// <summary>
        /// Has Command Flush been received?
        /// </summary>
        /// <remarks>Cleared by next call to StartStream.</remarks>
        private volatile bool m_Flushing;

        /// <summary>
        /// Queue of events to be processed by threads.
        /// </summary>
        /// <remarks>Derived classes shouldn't touch this.</remarks>
        private readonly BlockingQueue m_inSamples;
        /// <summary>
        /// Used to order access to m_outSamples.
        /// </summary>
        private readonly ManualResetEventSlim[] m_Slims;

        #endregion

        #region Abstracts

        /// <summary>
        /// Report whether a proposed input type is accepted by the MFT.
        /// </summary>
        /// <param name="pmt">The type to check.  Should never be null (which are always valid).</param>
        /// <returns>S_Ok if the type is valid or MF_E_INVALIDTYPE.</returns>
        /// <remarks>For async MFTs, you should NOT check to see if a 
        /// proposed input type is compatible with the currently-set output 
        /// type, just whether the specified type is a valid input type.</remarks>
        abstract protected HResult OnCheckInputType(IMFMediaType pmt);
        /// <summary>
        /// Return settings to describe input stream
        /// (see IMFTransform::GetInputStreamInfo).
        /// </summary>
        /// <param name="pStreamInfo">The struct where the parameters get set.</param>
        abstract protected void OnGetInputStreamInfo(ref MFTInputStreamInfo pStreamInfo);
        /// <summary>
        /// Return settings to describe output stream
        /// (see IMFTransform::GetOutputStreamInfo).
        /// </summary>
        /// <param name="pStreamInfo">The struct where the parameters get set.</param>
        abstract protected void OnGetOutputStreamInfo(ref MFTOutputStreamInfo pStreamInfo);
        /// <summary>
        /// The routine called by processing threads that actually performs the transform.
        /// </summary>
        /// <param name="sample">The input sample to process into output.</param>
        /// <param name="Discontinuity">Whether or not the sample should be marked as MFSampleExtension_Discontinuity.</param>
        /// <param name="InputMessageNumber">The value to pass to OutputSample.  This is NOT the frame number.</param>
        /// <remarks>
        /// Take the input sample and process it.
        /// Depending on what you set in On*StreamInfo, you can either perform
        /// in-place processing by modifying the input sample, or create a new
        /// IMFSample and fully populate it from the input (including attributes).
        /// When output sample(s) are complete, pass them to OutputSample().  A
        /// single input can generate zero or more outputs.  Use the same value
        /// of InputMessageNumber for all calls to OutputSample.
        /// If the output sample is not the input sample, call SafeRelease
        /// on the input sample.
        /// </remarks>
        abstract protected void OnProcessSample(IMFSample pInputSample, bool Discontinuity, int InputMessageNumber);

        #endregion

        #region Virtuals

        /// <summary>
        /// Report whether a proposed output type is accepted by the MFT.
        /// </summary>
        /// <param name="pmt">The type to check.  Should never be null (which are always valid).</param>
        /// <returns>S_Ok if the type is valid or MF_E_INVALIDTYPE.</returns>
        /// <remarks>The default behavior is to assume that the input type 
        /// must be set before the output type, and that the proposed output 
        /// type must exactly equal the value returned from the virtual
        /// CreateOutputFromInput.  Override as  necessary.
        /// </remarks>
        virtual protected HResult OnCheckOutputType(IMFMediaType pmt)
        {
            HResult hr = HResult.S_OK;

            // If the input type is set, see if they match.
            if (m_pInputType != null)
            {
                IMFMediaType pCheck = CreateOutputFromInput();

                try
                {
                    hr = IsIdentical(pmt, pCheck);
                }
                finally
                {
                    SafeRelease(pCheck);
                }
            }
            else
            {
                // Input type is not set.
                hr = HResult.MF_E_TRANSFORM_TYPE_NOT_SET;
            }

            return hr;
        }

        /// <summary>
        /// Override to get notified when the Input Type is actually being set.
        /// </summary>
        /// <remarks>The new type is in InputType, and can be null.  Input types 
        /// can be changed while the stream is running.  See
        /// the comments at the top of AsyncMFTBase.cs.</remarks>
        virtual protected void OnSetInputType()
        {
        }

        /// <summary>
        /// Override to get notified when the Output Type is actually being set.
        /// </summary>
        /// <remarks>The new type is in OutputType, and can be null.</remarks>
        virtual protected void OnSetOutputType()
        {
        }

        /// <summary>
        /// Override to allow the client to retrieve the MFT's list of supported input types.
        /// </summary>
        /// <param name="dwTypeIndex">The (zero-based) index of the type.</param>
        /// <param name="pInputType">The input type supported by the MFT.</param>
        /// <returns>S_Ok unless error.</returns>
        /// <remarks>This method is virtual since it is (sort of) optional.
        /// For example, if a client *knows* what types the MFT supports, it can
        /// just set it.  Not all clients support MFTs that won't enum types.</remarks>
        virtual protected HResult OnEnumInputTypes(int dwTypeIndex, out IMFMediaType pInputType)
        {
            pInputType = null;
            return HResult.E_NOTIMPL;
        }

        /// <summary>
        /// Override to allow the client to retrieve the MFT's list of supported Output Types.
        /// </summary>
        /// <param name="dwTypeIndex">The (zero-based) index of the type.</param>
        /// <param name="pOutputType">The output type supported by the MFT.</param>
        /// <returns>S_Ok or MFError.MF_E_NO_MORE_TYPES.</returns>
        /// <remarks>By default, assume the input type must be set first, and 
        /// that the output type is the single entry returned from the virtual 
        /// CreateOutputFromInput.  Override as needed.</remarks>
        virtual protected HResult OnEnumOutputTypes(int dwTypeIndex, out IMFMediaType pOutputType)
        {
            HResult hr = HResult.S_OK;

            // If the input type is specified, the output type must be the same.
            if (m_pInputType != null)
            {
                // If the input type is specified, there can be only one output type.
                if (dwTypeIndex == 0)
                {
                    pOutputType = CreateOutputFromInput();
                }
                else
                {
                    pOutputType = null;
                    hr = HResult.MF_E_NO_MORE_TYPES;
                }
            }
            else
            {
                pOutputType = null;
                hr = HResult.MF_E_TRANSFORM_TYPE_NOT_SET;
            }

            return hr;
        }

        /// <summary>
        /// Create a single output type from an input type.
        /// </summary>
        /// <returns>By default, a clone of the input type.  Can be overridden.</returns>
        /// <remarks>
        /// In many cases, there is only one possible output type, and it is 
        /// either identical to, or a direct consequence of the input type.  
        /// Provided with that single output type, OnCheckOutputType and 
        /// OnEnumOutputTypes can be written generically, so they don't have 
        /// to be implemented by the derived class.  At worst, this one method 
        /// may need to be overridden.</remarks>
        virtual protected IMFMediaType CreateOutputFromInput()
        {
            return CloneMediaType(m_pInputType);
        }

        /// <summary>
        /// Called to allow any trailing output samples to be sent (think: Echo).
        /// </summary>
        /// <param name="InputMessageNumber">Parameter to pass to OutputSample().</param>
        /// <remarks>Call OutputSample() with any trailing samples. Use the same
        /// value of InputMessageNumber for all calls to OutputSample.</remarks>
        virtual protected void OnDrain(int InputMessageNumber)
        {
        }

        /// <summary>
        /// Called in response to MFTMessageType.NotifyStartOfStream.
        /// </summary>
        /// <remarks>
        /// This is called after the message, but before any samples
        /// arrive.  All input and output types should be set before this
        /// point.  Note that there is no guarantee that OnEndStream
        /// will be called before a second call to OnStartStream.
        /// </remarks>
        virtual protected void OnStartStream()
        {
        }

        /// <summary>
        /// Called when streaming is ending.
        /// </summary>
        /// <remarks>
        /// A good place to clean up resources at the end of a stream.
        /// Before any more samples are sent, OnStartStream will get
        /// called again.  Note that this routine may never get called,
        /// or may get called more than once.  This should discard any
        /// pending drain information.
        /// </remarks>
        virtual protected void OnEndStream()
        {
        }

        #endregion

        protected AsyncMFTBase() : this(1)
        {
        }
        protected AsyncMFTBase(int iThreads) : this(iThreads, iThreads * 2)
        {
        }
        protected AsyncMFTBase(int iThreads, int iThreshold)
        {
            Trace("MFTImplementation");

            if (iThreads < 1)
                throw new Exception("Invalid thread count specified");

            m_ThreadCount = iThreads;
            m_Threshold = iThreshold;

            m_NeedInputPending = -1;
            m_ShutdownThreads = 0;
            m_ThreadsBlocked = 0;

            m_StreamIsActive = false;
            m_Shutdown = false;
            m_bDiscontinuity = false;
            m_Unlocked = false;
            m_Flushing = false;

            m_pInputType = null;
            m_pOutputType = null;

            m_inSamples = new BlockingQueue();
            m_TransformLockObject = new object();
            m_Slims = new ManualResetEventSlim[iThreads];
            m_outSamples = new ConcurrentQueue<IMFSample>();
            m_FormatEvent = new SemaphoreSlim(0, m_ThreadCount);

            // Build the IMFAttributes to use for IMFTransform::GetAttributes.
            MFError throwonhr;

            throwonhr = MFExtern.MFCreateAttributes(out m_pAttributes, 3);
            throwonhr = m_pAttributes.SetUINT32(MFAttributesClsid.MF_TRANSFORM_ASYNC, 1);
            throwonhr = m_pAttributes.SetUINT32(MFAttributesClsid.MF_TRANSFORM_ASYNC_UNLOCK, 0);
            throwonhr = m_pAttributes.SetUINT32(MFAttributesClsid.MFT_SUPPORT_DYNAMIC_FORMAT_CHANGE, 1);

            // Used for IMFMediaEventGenerator support.  All 
            // METransformNeedInput, METransformHaveOutput, etc go thru here.
            throwonhr = MFExtern.MFCreateEventQueue(out m_events);

            // Start the processing threads.
            for (int x = 0; x < iThreads; x++)
            {
                // The first event is 'set', the rest are not.
                m_Slims[x] = new ManualResetEventSlim(x == 0, 1000);

                Thread ProcessThread = new Thread(new ThreadStart(ProcessingThread));

                // Background threads can be terminated by .Net without waiting for
                // them to shutdown.
                ProcessThread.IsBackground = true;
#if DEBUG
                ProcessThread.Name = "Async MFT Processing Thread #" + ProcessThread.ManagedThreadId.ToString() + " for " + this.GetType().Name;
#endif
                ProcessThread.Start();
            }
        }

        ~AsyncMFTBase()
        {
            MyShutdown();
        }

        #region IMFTransform methods

        public HResult GetStreamLimits(
            MFInt pdwInputMinimum,
            MFInt pdwInputMaximum,
            MFInt pdwOutputMinimum,
            MFInt pdwOutputMaximum
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("GetStreamLimits");

                CheckUnlocked();

                // This template requires a fixed number of input and output
                // streams (1 for each).

                lock (m_TransformLockObject)
                {
                    // Fixed stream limits.
                    if (pdwInputMinimum != null)
                    {
                        pdwInputMinimum.Assign(1);
                    }
                    if (pdwInputMaximum != null)
                    {
                        pdwInputMaximum.Assign(1);
                    }
                    if (pdwOutputMinimum != null)
                    {
                        pdwOutputMinimum.Assign(1);
                    }
                    if (pdwOutputMaximum != null)
                    {
                        pdwOutputMaximum.Assign(1);
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetStreamCount(
            MFInt pcInputStreams,
            MFInt pcOutputStreams
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                // Trace("GetStreamCount"); Not interesting

                // Do not check CheckUnlocked (per spec)

                lock (m_TransformLockObject)
                {
                    // This template requires a fixed number of input and output
                    // streams (1 for each).
                    if (pcInputStreams != null)
                    {
                        pcInputStreams.Assign(1);
                    }

                    if (pcOutputStreams != null)
                    {
                        pcOutputStreams.Assign(1);
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return hr; // CheckReturn(hr);
        }

        public HResult GetStreamIDs(
            int dwInputIDArraySize,
            int[] pdwInputIDs,
            int dwOutputIDArraySize,
            int[] pdwOutputIDs
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                // Trace("GetStreamIDs"); not interesting

                // While the spec says this should be checked, IMFMediaEngine
                // apparently doesn't follow the spec.
                //CheckUnlocked();

                lock (m_TransformLockObject)
                {
                    // Since our stream counts are fixed, we don't need
                    // to implement this method.  As a result, our input
                    // and output streams are always #0.
                    hr = HResult.E_NOTIMPL;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return hr; // CheckReturn(hr);
        }

        public HResult GetInputStreamInfo(
            int dwInputStreamID,
            out MFTInputStreamInfo pStreamInfo
        )
        {
            // Overwrites everything with zeros.
            pStreamInfo = new MFTInputStreamInfo();

            HResult hr = HResult.S_OK;

            try
            {
                Trace("GetInputStreamInfo");

                CheckUnlocked();
                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    // The implementation can override the defaults,
                    // and should set cbSize
                    OnGetInputStreamInfo(ref pStreamInfo);
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetOutputStreamInfo(
            int dwOutputStreamID,
            out MFTOutputStreamInfo pStreamInfo
        )
        {
            // Overwrites everything with zeros.
            pStreamInfo = new MFTOutputStreamInfo();

            HResult hr = HResult.S_OK;

            try
            {
                Trace("GetOutputStreamInfo");

                CheckUnlocked();
                CheckValidStream(dwOutputStreamID);

                lock (m_TransformLockObject)
                {
                    // The implementation can override the defaults,
                    // and should set cbSize.
                    OnGetOutputStreamInfo(ref pStreamInfo);
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetAttributes(
            out IMFAttributes pAttributes
        )
        {
            pAttributes = null;

            HResult hr = HResult.S_OK;

            try
            {
                // Trace("GetAttributes"); not interesting

                // Do not check CheckUnlocked (per spec)

                lock (m_TransformLockObject)
                {
                    // Using GetUniqueRCW means the caller can do
                    // ReleaseComObject without trashing our copy.  We *don't*
                    // want to return a clone because we *do* want them to be
                    // able to change our attributes.
                    pAttributes = GetUniqueRCW(m_pAttributes) as IMFAttributes;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return hr; // CheckReturn(hr);
        }

        public HResult GetInputStreamAttributes(
            int dwInputStreamID,
            out IMFAttributes pAttributes
        )
        {
            pAttributes = null;

            HResult hr = HResult.S_OK;

            try
            {
                // Trace("GetInputStreamAttributes"); not interesting

                CheckUnlocked();
                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    hr = HResult.E_NOTIMPL;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return hr; // CheckReturn(hr);
        }

        public HResult GetOutputStreamAttributes(
            int dwOutputStreamID,
            out IMFAttributes pAttributes
        )
        {
            pAttributes = null;

            HResult hr = HResult.S_OK;

            try
            {
                // Trace("GetOutputStreamAttributes"); not interesting

                CheckUnlocked();
                CheckValidStream(dwOutputStreamID);

                lock (m_TransformLockObject)
                {
                    hr = HResult.E_NOTIMPL;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return hr; // CheckReturn(hr);
        }

        public HResult DeleteInputStream(
            int dwStreamID
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("DeleteInputStream");

                CheckUnlocked();
                CheckValidStream(dwStreamID);

                lock (m_TransformLockObject)
                {
                    // Removing streams not supported.
                    hr = HResult.E_NOTIMPL;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult AddInputStreams(
            int cStreams,
            int[] adwStreamIDs
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("AddInputStreams");

                CheckUnlocked();

                lock (m_TransformLockObject)
                {
                    // Adding streams not supported.
                    hr = HResult.E_NOTIMPL;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetInputAvailableType(
            int dwInputStreamID,
            int dwTypeIndex, // 0-based
            out IMFMediaType ppType
        )
        {
            ppType = null;

            HResult hr = HResult.S_OK;

            try
            {
                Trace(string.Format("GetInputAvailableType (stream = {0}, type index = {1})", dwInputStreamID, dwTypeIndex));

                // While the spec says this should be checked, IMFMediaEngine
                // apparently doesn't follow the spec.
                //CheckUnlocked();

                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    // Get the input media type from the derived class.
                    // No need to pass dwInputStreamID, since it must
                    // always be zero.
                    hr = OnEnumInputTypes(dwTypeIndex, out ppType);
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetOutputAvailableType(
            int dwOutputStreamID,
            int dwTypeIndex, // 0-based
            out IMFMediaType ppType
        )
        {
            ppType = null;

            HResult hr = HResult.S_OK;

            try
            {
                Trace(string.Format("GetOutputAvailableType (stream = {0}, type index = {1})", dwOutputStreamID, dwTypeIndex));

                CheckUnlocked();
                CheckValidStream(dwOutputStreamID);

                lock (m_TransformLockObject)
                {
                    // Get the output media type from the derived class.
                    // No need to pass dwOutputStreamID, since it must
                    // always be zero.
                    hr = OnEnumOutputTypes(dwTypeIndex, out ppType);
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult SetInputType(
            int dwInputStreamID,
            IMFMediaType pType,
            MFTSetTypeFlags dwFlags
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("SetInputType");

                CheckUnlocked();
                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    // Asynchronous MFTs must allow formats to change while
                    // they are running.  However, it must still be a format
                    // that the MFT supports (ie OnCheckInputType).

                    // Allow input type to be cleared.
                    if (pType != null)
                    {
                        // Validate non-null types.
                        hr = OnCheckInputType(pType);
                    }
                    else
                    {
                        if (m_StreamIsActive)
                        {
                            // Can't set input type to null while actively streaming.
                            hr = HResult.MF_E_INVALIDMEDIATYPE;
                        }
                    }
                    if (Succeeded(hr))
                    {
                        // Does the caller want to set the type?  Or just test it?
                        bool bReallySet = ((dwFlags & MFTSetTypeFlags.TestOnly) == 0);

                        if (bReallySet)
                        {
                            // Make a copy of the IMFMediaType and queue
                            // it to the processing thread.  The type will
                            // get changed after any pending inputs have
                            // been processed.
                            IMFMediaType pTmp;

                            pTmp = CloneMediaType(pType);

                            // If we are streaming, we need to delay 
                            // changing the input type until all current 
                            // samples are processed.
                            if (!m_StreamIsActive)
                            {
                                MySetInput(pTmp);
                            }
                            else
                            {
                                ThreadMessage(pTmp);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }
            //finally
            {
                // While we *should* do this, we can't.  If the caller is c#, we
                // would destroy their copy.  Instead we have to leave this for
                // the GC.
                // SafeRelease(pType);
            }

            return CheckReturn(hr);
        }

        public HResult SetOutputType(
            int dwOutputStreamID,
            IMFMediaType pType,
            MFTSetTypeFlags dwFlags
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("SetOutputType");

                // By spec, we are supposed to check this for non-decoders.
                // But knowing what is and isn't a decoder isn't practical.
                // Since MS already breaks the spec for GetStreamIDs and
                // GetAvailableInputType, I'm going to break it here and
                // not check.  Correctly functioning clients don't care,
                // and incorrect ones will still get an error eventually.
                //CheckUnlocked();
                CheckValidStream(dwOutputStreamID);

                lock (m_TransformLockObject)
                {
                    if (pType != null)
                    {
                        // Validate the type.
                        hr = OnCheckOutputType(pType);
                    }

                    if (Succeeded(hr))
                    {
                        // Does the caller want us to set the type, or just test it?
                        bool bReallySet = ((dwFlags & MFTSetTypeFlags.TestOnly) == 0);

                        // Set the type, unless the caller was just testing.
                        if (bReallySet)
                        {
                            // Make our own copy of the type.
                            OutputType = CloneMediaType(pType);

                            // Notify the derived class
                            OnSetOutputType();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }
            //finally
            {
                // While we *should* do this, we can't.  If the caller is c#, we
                // would destroy their copy.  Instead we have to leave this for
                // the GC.
                // SafeRelease(pType);
            }

            return CheckReturn(hr);
        }

        public HResult GetInputCurrentType(
            int dwInputStreamID,
            out IMFMediaType ppType
        )
        {
            ppType = null;

            HResult hr = HResult.S_OK;

            try
            {
                Trace("GetInputCurrentType");

                CheckUnlocked();
                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    if (m_pInputType != null)
                    {
                        ppType = CloneMediaType(m_pInputType);
                    }
                    else
                    {
                        // Type is not set
                        hr = HResult.MF_E_TRANSFORM_TYPE_NOT_SET;
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetOutputCurrentType(
            int dwOutputStreamID,
            out IMFMediaType ppType
        )
        {
            ppType = null;

            HResult hr = HResult.S_OK;

            try
            {
                Trace("GetOutputCurrentType");

                // By spec, we are supposed to check this for non-decoders.
                // But knowing what is and isn't a decoder isn't practical.
                // Since MS already breaks the spec for GetStreamIDs and
                // GetAvailableInputType, I'm going to break it here and
                // not check.  Correctly functioning clients don't care,
                // and incorrect ones will still get an error eventually.
                //CheckUnlocked();
                CheckValidStream(dwOutputStreamID);

                lock (m_TransformLockObject)
                {
                    if (m_pOutputType != null)
                    {
                        ppType = CloneMediaType(m_pOutputType);
                    }
                    else
                    {
                        // No output type set
                        hr = HResult.MF_E_TRANSFORM_TYPE_NOT_SET;
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetInputStatus(
            int dwInputStreamID,
            out MFTInputStatusFlags pdwFlags
        )
        {
            pdwFlags = MFTInputStatusFlags.None;

            HResult hr = HResult.S_OK;

            try
            {
                Trace("GetInputStatus");

                CheckUnlocked();
                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    if (AllowInput())
                    {
                        pdwFlags = MFTInputStatusFlags.AcceptData;
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult GetOutputStatus(
            out MFTOutputStatusFlags pdwFlags
        )
        {
            pdwFlags = MFTOutputStatusFlags.None;

            HResult hr = HResult.S_OK;

            try
            {
                Trace("GetOutputStatus");

                CheckUnlocked();

                lock (m_TransformLockObject)
                {
                    if (HasPendingOutput())
                    {
                        pdwFlags = MFTOutputStatusFlags.SampleReady;
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult SetOutputBounds(
            long hnsLowerBound,
            long hnsUpperBound
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("SetOutputBounds");

                CheckUnlocked();

                lock (m_TransformLockObject)
                {
                    // Output bounds not supported
                    hr = HResult.E_NOTIMPL;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult ProcessEvent(
            int dwInputStreamID,
            IMFMediaEvent pEvent
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("ProcessEvent");

                CheckUnlocked();

                lock (m_TransformLockObject)
                {
                    // Events not supported.
                    hr = HResult.E_NOTIMPL;
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }
            //finally
            {
                // While we *should* do this, we can't.  If the caller is c#, we
                // would destroy their copy.  Instead we have to leave this for
                // the GC.
                // SafeRelease(pEvent);
            }

            return CheckReturn(hr);
        }

        public HResult ProcessMessage(
            MFTMessageType eMessage,
            IntPtr ulParam
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("ProcessMessage " + eMessage.ToString());

                CheckUnlocked();

                lock (m_TransformLockObject)
                {
                    switch (eMessage)
                    {
                        case MFTMessageType.NotifyStartOfStream:
                            // Mandatory message for async MFTs.  Note
                            // that the corresponding EndOfStream is
                            // optional.

                            // Start new stream.
                            MyStartStream();

                            break;

                        case MFTMessageType.CommandFlush:
                            // Tell the processing thread to skip samples
                            // instead of processing them.  Flush stays
                            // in effect until next NotifyStartOfStream.
                            ThreadMessage(MessageType.Flush);

                            // Flushes should happen very quickly, but we can't
                            // risk returning until it is done.
                            while (Thread.VolatileRead(ref m_NeedInputPending) != -1)
                                Thread.Sleep(5);

                            // No more processing until next NotifyStartOfStream
                            MyEndStream();

                            break;

                        case MFTMessageType.CommandDrain:
                            // Stop sending NeedInputs.  Drain stays
                            // in effect until next NotifyStartOfStream.
                            m_NeedInputPending = -1; // Stop sending NeedInput until new stream

                            // The processing thread will signal the drain complete.
                            ThreadMessage(MessageType.Drain);

                            // No more processing until next NotifyStartOfStream

                            // Called by processing thread when the drain is complete.
                            //MyEndStream();

                            break;

                        case MFTMessageType.CommandMarker:
                            // Tell the processing thread to send the message when
                            // all inputs received before this message have been 
                            // turned into outputs.  Note that unlike drain, 
                            // processing does NOT stop at that point.  We just 
                            // post the message and keep going.

                            ThreadMessage(ulParam);
                            break;

                        case MFTMessageType.NotifyEndOfStream:
                            // Optional message.
                            m_NeedInputPending = -1;

                            // For non-Async MFTs, the client could send more
                            // ProcessInput calls after this message.  Not an
                            // option for Async, since we would have to send
                            // some NeedInput messages.
                            MyEndStream();
                            break;

                        case MFTMessageType.NotifyBeginStreaming:
                            // Optional message.
                            break;

                        case MFTMessageType.NotifyEndStreaming:
                            // Optional message.
                            break;

                        case MFTMessageType.SetD3DManager:
                            // The pipeline should never send this message unless the MFT
                            // has the MF_SA_D3D_AWARE attribute set to TRUE. However, if we
                            // do get this message, it's invalid and we don't implement it.
                            hr = HResult.E_NOTIMPL;
                            break;

                        default:
#if DEBUG
                            Debug.Fail("Unknown message type: " + eMessage.ToString());
#endif
                            // The spec doesn't say to do this, but I do it anyway.
                            hr = HResult.S_FALSE;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult ProcessInput(
            int dwInputStreamID,
            IMFSample pSample,
            int dwFlags
        )
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("ProcessInput");

                CheckUnlocked();
                CheckValidStream(dwInputStreamID);

                if (pSample != null)
                {
                    if (dwFlags != 0)
                    {
                        // Invalid flags
                        hr = HResult.E_INVALIDARG;
                    }
                }
                else
                {
                    // No input sample provided
                    hr = HResult.E_POINTER;
                }

                if (Succeeded(hr))
                {
                    lock (m_TransformLockObject)
                    {
                        hr = AllTypesSet();
                        if (Succeeded(hr))
                        {
                            // Unless this call is in response to a
                            // METransformNeedInput message, reject it.
                            if (AllowInput())
                            {
                                m_NeedInputPending--;
                                ThreadMessage(pSample);
                            }
                            else
                            {
                                hr = HResult.MF_E_NOTACCEPTING;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult ProcessOutput(
            MFTProcessOutputFlags dwFlags,
            int cOutputBufferCount,
            MFTOutputDataBuffer[] pOutputSamples, // one per stream
            out ProcessOutputStatus pdwStatus
        )
        {
            pdwStatus = ProcessOutputStatus.None;

            HResult hr = HResult.S_OK;

            try
            {
                Trace("ProcessOutput");

                CheckUnlocked();

                // Check input parameters.

                if (dwFlags != MFTProcessOutputFlags.None || cOutputBufferCount != 1)
                {
                    hr = HResult.E_INVALIDARG;
                }

                if (Succeeded(hr) && pOutputSamples == null)
                {
                    hr = HResult.E_POINTER;
                }

                // pOutputSamples[0].pSample may be null or not depending
                // on how the derived set MFTOutputStreamInfoFlags.  Do the
                // check in MyProcessOutput.

                if (Succeeded(hr))
                {
                    lock (m_TransformLockObject)
                    {
                        hr = AllTypesSet();
                        if (Succeeded(hr))
                        {
                            // Unless this call is in response to a
                            // METransformHaveOutput message, reject it.
                            if (HasPendingOutput())
                            {
                                hr = MyProcessOutput(ref pOutputSamples[0]);
                            }
                            else
                            {
                                hr = HResult.E_UNEXPECTED;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        #endregion

        #region IMFMediaEventGenerator methods

        // These methods are just wrappers around m_events.
        // When we want to send an event back to the client, we just call
        // m_events.QueueEvent, and let m_events handle the plumbing.

        public HResult BeginGetEvent(IMFAsyncCallback pCallback, object o)
        {
            HResult hr = HResult.S_OK;

            try
            {
                hr = m_events.BeginGetEvent(pCallback, null);
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }
            //finally
            {
                // While we *should* do this, we can't.  If the caller is c#, we
                // would destroy their copy.  Instead we have to leave this for
                // the GC.
                //SafeRelease(pCallback);
            }

            return CheckReturn(hr);
        }

        public HResult EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
        {
            ppEvent = null;
            HResult hr = HResult.S_OK;

            try
            {
                hr = m_events.EndGetEvent(pResult, out ppEvent);
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }
            //finally
            {
                // While we *should* do this, we can't.  If the caller is c#, we
                // would destroy their copy.  Instead we have to leave this for
                // the GC.
                //SafeRelease(pResult);
            }

            return CheckReturn(hr);
        }

        public HResult GetEvent(MFEventFlag dwFlags, out IMFMediaEvent ppEvent)
        {
            ppEvent = null;

            HResult hr = HResult.S_OK;

            try
            {
                hr = m_events.GetEvent(dwFlags, out ppEvent);
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult QueueEvent(MediaEventType met, Guid guidExtendedType, HResult hrStatus, ConstPropVariant pvValue)
        {
            IMFMediaEvent pEvent;
            MFError throwonhr;
            HResult hr = HResult.S_OK;

            try
            {
                throwonhr = MFExtern.MFCreateMediaEvent(met, Guid.Empty, HResult.S_OK, null, out pEvent);

                MyQueueEvent(pEvent);
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        #endregion

        #region IMFShutdown methods

        public HResult GetShutdownStatus(out MFShutdownStatus pStatus)
        {
            HResult hr = HResult.S_OK;
            pStatus = 0; // Have to set it to something

            try
            {
                Trace("GetShutdownStatus");

                lock (m_TransformLockObject)
                {
                    if (m_Shutdown)
                    {
                        // By spec, the shutdown must have completed during
                        // the call to Shutdown().
                        pStatus = MFShutdownStatus.Completed;
                    }
                    else
                    {
                        hr = HResult.MF_E_INVALIDREQUEST;
                    }
                }
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        public HResult Shutdown()
        {
            HResult hr = HResult.S_OK;

            try
            {
                Trace("Shutdown");

                MyShutdown();
            }
            catch (Exception e)
            {
                hr = (HResult)Marshal.GetHRForException(e);
            }

            return CheckReturn(hr);
        }

        #endregion

        #region IPersist methods

        public HResult GetClassID(out Guid pClassID)
        {
            pClassID = this.GetType().GUID;
            return HResult.S_OK;
        }

        #endregion

        #region ICustomQueryInterface methods

        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;

#if DEBUG
            if (iid != typeof(IMFTransform).GUID &&
                iid != typeof(IMFMediaEventGenerator).GUID &&
                iid != typeof(IMFShutdown).GUID &&
                iid != typeof(IPersist).GUID)
            {
                string sGuidString = iid.ToString("B");
                string sKeyName = string.Format("HKEY_CLASSES_ROOT\\Interface\\{0}", sGuidString);
                string sName = (string)Microsoft.Win32.Registry.GetValue(sKeyName, null, sGuidString);

                Trace(string.Format("Unhandled interface requested: {0}", sName));
            }
#endif

            return CustomQueryInterfaceResult.NotHandled;
        }

        #endregion

        #region Private methods (Only expected to be used by template)

        /// <summary>
        /// Retrieve a sample from m_outSamples and process it into MFTOutputDataBuffer.
        /// </summary>
        /// <param name="pOutputSamples">The struct into which the output sample is written.</param>
        /// <returns>S_Ok or error.</returns>
        private HResult MyProcessOutput(ref MFTOutputDataBuffer pOutputSamples)
        {
            HResult hr = HResult.S_OK;

            if (pOutputSamples.pSample == IntPtr.Zero)
            {
                IMFSample sample = GetOutputSample();

                if (sample != null)
                {
                    pOutputSamples.dwStatus = MFTOutputDataBufferFlags.None;
                    pOutputSamples.pSample = Marshal.GetIUnknownForObject(sample);

                    // There is some risk in using Marshal.ReleaseComObject here,
                    // since some other .Net component could have a pointer to this
                    // same object, and this would yank the RCW out from under
                    // them.  But we go thru a LOT of samples.
                    SafeRelease(sample);
                }
                else
                {
                    pOutputSamples.dwStatus = MFTOutputDataBufferFlags.FormatChange;

                    // A null entry in this queue is a request to change the 
                    // output type.

                    // There are two times our output type can change.
                    //
                    // 1) As part of a Dynamic format change initiated by 
                    // the client.
                    // 2) At the request of the derived class by sending 
                    // null to OutputSample.
                    //
                    // We need to clear the output type in both cases.  This
                    // ensures that new ProcessInputs won't succeed and 
                    // OnProcessSample won't get called until the new output 
                    // type is set.

                    OutputType = null;

                    // Note: OnSetOutputType can reset the output type.
                    OnSetOutputType();

                    hr = HResult.MF_E_TRANSFORM_STREAM_CHANGE;
                }
            }
            else
            {
                hr = HResult.E_INVALIDARG;
            }

            return hr;
        }

        /// <summary>
        /// Allow for reset between NotifyStartOfStream calls.
        /// </summary>
        private void MyStartStream()
        {
            MyEndStream();

            // In theory, we might have a Flush pending here that this
            // StartOfStream is going to reset.  However, there cannot 
            // be any samples pending in the message queue, since we
            // cleared the message queue as part of flush, and 
            // m_NeedInputPending got set to -1, preventing any more
            // samples from having been accepted.  So it is safe to do 
            // this with no further checking:

            m_Flushing = false;

            m_StreamIsActive = true;
            m_NeedInputPending = 0;

            OnStartStream();

            // Ask for some inputs.
            SendNeedEvent();
        }

        /// <summary>
        /// Called when a stream ends.  Probably.
        /// </summary>
        private void MyEndStream()
        {
            if (m_StreamIsActive)
            {
                m_StreamIsActive = false;

                OnEndStream();
            }
        }

        /// <summary>
        /// Are inputs allowed at the current time?
        /// </summary>
        /// <returns></returns>
        private bool AllowInput()
        {
            return m_NeedInputPending > 0;
        }

        /// <summary>
        /// Check for valid stream number.
        /// </summary>
        /// <param name="dwStreamID"></param>
        /// <returns>S_Ok unless error.</returns>
        /// <remarks>Easy to do since the only valid value is zero.</remarks>
        private static void CheckValidStream(int dwStreamID)
        {
            if (dwStreamID != 0)
            {
                throw new MFException(HResult.MF_E_INVALIDSTREAMNUMBER);
            }
        }

        /// <summary>
        /// Ensure both input and output media types are set.
        /// </summary>
        /// <returns>S_Ok or MFError.MF_E_TRANSFORM_TYPE_NOT_SET.</returns>
        private HResult AllTypesSet()
        {
            if (m_pInputType == null || m_pOutputType == null)
                return HResult.MF_E_TRANSFORM_TYPE_NOT_SET;

            return HResult.S_OK;
        }

        /// <summary>
        /// Print out a debug line when hr doesn't equal S_Ok.
        /// </summary>
        /// <param name="hr">Value to check</param>
        /// <returns>The input value.</returns>
        /// <remarks>This code shows the calling routine and the error text.
        /// All the public interface methods use this to wrap their returns,
        /// which is helpful during debugging.
        /// </remarks>
        private static HResult CheckReturn(HResult hr)
        {
#if DEBUG
            if (hr != HResult.S_OK)
            {
                StackTrace st = new StackTrace();
                StackFrame sf = st.GetFrame(1);

                string sName = sf.GetMethod().Name;
                string sError = MFError.GetErrorText(hr);
                if (sError != null)
                    sError = sError.Trim();
                Trace(string.Format("{1} returning 0x{0:x} ({2})", hr, sName, sError));
            }
#endif
            return hr;
        }

        /// <summary>
        /// Do we have data for ProcessOutput?
        /// </summary>
        /// <returns></returns>
        private bool HasPendingOutput()
        {
            return !m_outSamples.IsEmpty;
        }

        /// <summary>
        /// Does the work of actually changing the input media type.
        /// </summary>
        /// <param name="pType">Input media type (can be null).</param>
        /// <remarks>Called both directly from SetInputType and async
        /// from the processing threads (during dynamic format change).</remarks>
        private void MySetInput(IMFMediaType pType)
        {
            // At this point, any pending input samples with the old type 
            // have been processed.

            // pType is a clone of the IMFMediaType passed to SetInputType or null.

            IMFMediaType oldType = CloneMediaType(m_pOutputType);

            // Set the new type
            InputType = pType;

            // Inform the derived
            OnSetInputType();

            if (pType != null)
            {
                // Are we going to have to have ProcessOutput do a format 
                // change message?
                //
                // There are 2 cases where we might:
                //
                // 1) We have started streaming, and someone changed the 
                //    input type to something that is incompatible with the 
                //    output type.
                // 2) We haven't started streaming yet, but someone
                //    a) Set the input type.
                //    b) Set the output type.
                //    c) Set the input type again to something that was 
                //       incompatible with (b).
                //
                // It may seem a little redundant to trigger a format change 
                // message to ProcessOutput for #2 since streaming hasn't 
                // even started yet.  But we need some way to inform the 
                // client that the output type they set (b) is no longer 
                // valid.  And we can't simply reject the change to the input 
                // type, since that would break the whole concept of dynamic 
                // format changes.  #2 is an unlikely scenario, so I'm not 
                // going to worry too much about the performance impact of
                // this silly case.  NB: It can be avoided by calling 
                // SetOutputType(NULL) before doing (c).

                // If the old type was null, neither condition applies, no 
                // notification is required (the most common case for 
                // SetInputType).
                if (oldType != null)
                {
                    if (
                        // OnSetInputType changed the output type. Since the 
                        // oldtype wasn't null, we need to inform the client 
                        // that there's a new output type.
                        (Failed(IsIdentical(oldType, m_pOutputType))) ||

                        // The new input type is not compatible with current 
                        // output type.
                        (Failed(OnCheckOutputType(m_pOutputType)))
                       )
                    {
                        // After we have sent all the current entries in
                        // the output queue, re-negotiate the output type.
                        OutputSample(null, int.MinValue);
                    }
                }
            }
        }

        /// <summary>
        /// Called from various places to shutdown the MFT.
        /// </summary>
        /// <remarks>There is no coming back from this.</remarks>
        private void MyShutdown()
        {
            if (!m_Shutdown)
            {
                lock (m_TransformLockObject)
                {
                    m_Shutdown = true;
                    m_Unlocked = false;

                    Thread.VolatileWrite(ref m_ShutdownThreads, m_ThreadCount);
                    for (int x = 0; x < m_ThreadCount; x++)
                    {
                        ThreadMessage(MessageType.Shutdown);
                    }

                    MyEndStream();

                    // All attempts by the client to read events will
                    // start returning MF_E_SHUTDOWN.
                    m_events.Shutdown();
                    //SafeRelease(m_events);

                    // Flush the outputs
                    if (m_outSamples != null)
                    {
                        IMFSample pSamp;
                        while (!m_outSamples.IsEmpty)
                        {
                            if (m_outSamples.TryDequeue(out pSamp))
                                SafeRelease(pSamp);
                        }
                    }

                    // We own all these objects, so SafeRelease should
                    // be, well, safe.

                    InputType = null;
                    OutputType = null;

                    SafeRelease(m_pAttributes);

                    GC.SuppressFinalize(this);
                }
                m_TransformLockObject = null;
            }
        }

        /// <summary>
        /// Dequeue sample from m_outSamples
        /// </summary>
        /// <returns>The sample.</returns>
        private IMFSample GetOutputSample()
        {
            IMFSample sample;

            // There is no contention for dequeuing, but
            // a simultaneous enqueue could cause a failure here.
            while (!m_outSamples.TryDequeue(out sample))
                ;

            // See if we need more input.
            SendNeedEvent();

            return sample;
        }

        /// <summary>
        /// Checks to see if the MFT has been unlocked.
        /// </summary>
        /// <remarks>Also handles shutdown.</remarks>
        private void CheckUnlocked()
        {
            if (!m_Shutdown)
            {
                // Shortcut making the call to GetUINT32.
                if (!m_Unlocked)
                {
                    int iVal;

                    HResult hr = m_pAttributes.GetUINT32(MFAttributesClsid.MF_TRANSFORM_ASYNC_UNLOCK, out iVal);
                    if (Succeeded(hr) && iVal != 0)
                    {
                        Trace("Unlocked!");
                        m_Unlocked = true;
                    }
                    else
                    {
                        throw new MFException(HResult.MF_E_TRANSFORM_ASYNC_LOCKED);
                    }
                }
            }
            else
            {
                throw new MFException(HResult.MF_E_SHUTDOWN);
            }
        }

        /// <summary>
        /// Send a message to the client telling it output is ready.
        /// </summary>
        private void SendSampleReady()
        {
            MFError throwonhr;

            throwonhr = QueueEvent(MediaEventType.METransformHaveOutput, Guid.Empty, HResult.S_OK, null);
        }

        /// <summary>
        /// Send a message associated with a stream to the client.
        /// </summary>
        /// <param name="met"></param>
        /// <remarks>Used by METransformNeedInput & METransformDrainComplete
        /// to populate MF_EVENT_MFT_INPUT_STREAM_ID and queue the message.</remarks>
        private void SendStreamEvent(MediaEventType met)
        {
            MFError throwonhr;
            IMFMediaEvent pEvent;

            throwonhr = MFExtern.MFCreateMediaEvent(met, Guid.Empty, HResult.S_OK, null, out pEvent);
            throwonhr = pEvent.SetUINT32(MFAttributesClsid.MF_EVENT_MFT_INPUT_STREAM_ID, 0);

            MyQueueEvent(pEvent);

            //SafeRelease(pEvent);
        }

        /// <summary>
        /// Send message to client asking for more input.
        /// </summary>
        /// <remarks>m_NeedInputPending = -1 indicates the stream is shutting down.
        /// This calc is tougher than you might think.  When I first wrote it, I assumed
        /// that the goal was "always keep the input threads busy."  But I ended up
        /// with hundreds of samples in the output queue.  So the new plan is, we
        /// add these things together.  If there are less than Threshold, ask for more input.
        /// - In the input queue
        /// - In the output queue
        /// - Requested from the client</remarks>
        private void SendNeedEvent()
        {
            if (m_NeedInputPending >= 0)
            {
                int j = m_Threshold - m_NeedInputPending - m_outSamples.Count - m_inSamples.Count;

                while (j-- > 0)
                {
                    SendStreamEvent(MediaEventType.METransformNeedInput);

                    // Keep track of how many requests we have sent.
                    m_NeedInputPending++;
                }
            }
        }

        /// <summary>
        /// Let a client know that a marker has been reached.
        /// </summary>
        /// <param name="ulParam">The parameter they sent along with the marker request.</param>
        private void SendMarker(IntPtr ulParam)
        {
            IMFMediaEvent pEvent;
            MFError throwonhr;

            throwonhr = MFExtern.MFCreateMediaEvent(MediaEventType.METransformMarker, Guid.Empty, HResult.S_OK, null, out pEvent);

            // Send back the parameter they provided.
            throwonhr = pEvent.SetUINT64(MFAttributesClsid.MF_EVENT_MFT_CONTEXT, ulParam.ToInt64());

            MyQueueEvent(pEvent);
            //SafeRelease(pEvent);
        }

        /// <summary>
        /// Send a message back to the client.
        /// </summary>
        /// <param name="pEvent"></param>
        private void MyQueueEvent(IMFMediaEvent pEvent)
        {
            MFError throwonhr;

            throwonhr = m_events.QueueEvent(pEvent);
        }

        #endregion

        #region Utility methods (possibly useful to derived classes).

        /// <summary>
        /// If Discontinuity, set the appropriate attribute on the sample.
        /// </summary>
        /// <param name="Discontinuity">Time to set discontinuity?</param>
        /// <param name="pSample">The sample that will be sent to OutputSample.</param>
        protected static void HandleDiscontinuity(bool Discontinuity, IMFSample pSample)
        {
            if (Discontinuity)
            {
                MFError throwonhr = pSample.SetUINT32(MFAttributesClsid.MFSampleExtension_Discontinuity, 1);
            }
        }

        /// <summary>
        /// Debug.Writeline all attributes.
        /// </summary>
        /// <param name="ia">The attributes to print</param>
        /// <remarks>Useful for examining attributes on IMFMediaTypes, IMFSamples, etc</remarks>
        [Conditional("DEBUG")]
        protected static void TraceAttributes(IMFAttributes ia)
        {
            string s = MFDump.DumpAttribs(ia);
            Debug.Write(s);
        }

        /// <summary>
        /// Check to see if 2 IMFMediaTypes are identical.
        /// </summary>
        /// <param name="a">First media type.</param>
        /// <param name="b">Second media type.</param>
        /// <returns>S_Ok if identical, else MF_E_INVALIDTYPE.</returns>
        /// <remarks>Note that MF defines 'identical' in a unique way.  See 
        /// docs for IMFMediaType::IsEqual.</remarks>
        protected static HResult IsIdentical(IMFMediaType a, IMFMediaType b)
        {
            // Otherwise, proposed input must be identical to output.
            MFMediaEqual flags;
            HResult hr = a.IsEqual(b, out flags);

            // IsEqual can return S_FALSE. Treat this as failure.
            if (hr != HResult.S_OK)
            {
                hr = HResult.MF_E_INVALIDTYPE;
            }

            return hr;
        }

        /// <summary>
        /// Validate that a media type uses the specified Major type and a Subtype from a list.
        /// </summary>
        /// <param name="pmt">IMFMediaType to check</param>
        /// <param name="gMajorType">MajorType to check for.</param>
        /// <param name="gSubtypes">List of SubTypes to check against.</param>
        /// <returns>S_Ok if match, else MF_E_INVALIDTYPE.</returns>
        protected static HResult CheckMediaType(IMFMediaType pmt, Guid gMajorType, Guid[] gSubTypes)
        {
            Guid major_type;

            MFError throwonhr = pmt.GetMajorType(out major_type);
            HResult hr = HResult.S_OK;

            if (major_type == gMajorType)
            {
                Guid subtype;

                // Get the subtype GUID.
                throwonhr = pmt.GetGUID(MFAttributesClsid.MF_MT_SUBTYPE, out subtype);

                // Look for the subtype in our list of accepted types.
                hr = HResult.MF_E_INVALIDTYPE;
                for (int i = 0; i < gSubTypes.Length; i++)
                {
                    if (subtype == gSubTypes[i])
                    {
                        hr = HResult.S_OK;
                        break;
                    }
                }
            }
            else
            {
                hr = HResult.MF_E_INVALIDTYPE;
            }

            return hr;
        }

        /// <summary>
        /// Create a partial type using an array of subtypes.
        /// </summary>
        /// <param name="dwTypeIndex">Index into the array.</param>
        /// <param name="gMajorType">Major type.</param>
        /// <param name="gSubTypes">Array of subtypes.</param>
        /// <param name="ppmt">Newly created media type.</param>
        /// <returns>S_Ok if valid index, else MF_E_NO_MORE_TYPES.</returns>
        protected static HResult CreatePartialType(int dwTypeIndex, Guid gMajorType, Guid[] gSubTypes, out IMFMediaType ppmt)
        {
            HResult hr = HResult.S_OK;
            MFError throwonhr;

            if (dwTypeIndex < gSubTypes.Length)
            {
                throwonhr = MFExtern.MFCreateMediaType(out ppmt);
                throwonhr = ppmt.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, gMajorType);
                throwonhr = ppmt.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, gSubTypes[dwTypeIndex]);
            }
            else
            {
                ppmt = null;
                hr = HResult.MF_E_NO_MORE_TYPES;
            }

            return hr;
        }

        /// <summary>
        /// Used to wrap an object so a call to ReleaseComObject on the returned object won't wipe the original object.
        /// </summary>
        /// <param name="o">Object to wrap.  Can be null.</param>
        /// <returns>Wrapped object.</returns>
        protected static object GetUniqueRCW(object o)
        {
            object oret;

            if (o != null)
            {
                IntPtr p = Marshal.GetIUnknownForObject(o);
                try
                {
                    oret = Marshal.GetUniqueObjectForIUnknown(p);
                }
                finally
                {
                    Marshal.Release(p);
                }
            }
            else
            {
                oret = null;
            }

            return oret;
        }

        [Conditional("DEBUG")]
        protected static void Trace(string s)
        {
            Debug.WriteLine(string.Format("{0}:{1}", DateTime.Now.ToString("HH:mm:ss.ffff"), s));
        }

        /// <summary>
        /// Return a duplicate of a media type. Null input gives null output.
        /// </summary>
        /// <param name="inType">The IMFMediaType to clone or null.</param>
        /// <returns>Duplicate IMFMediaType or null.</returns>
        protected static IMFMediaType CloneMediaType(IMFMediaType inType)
        {
            MFError throwonhr;
            IMFMediaType outType = null;

            if (inType != null)
            {
                throwonhr = MFExtern.MFCreateMediaType(out outType);
                throwonhr = inType.CopyAllItems(outType);
            }

            return outType;
        }

        void WaitForMyTurn(int InputMessageNumber)
        {
            int j = InputMessageNumber % m_ThreadCount;
            m_Slims[j].Wait();
        }

        /// <summary>
        /// Called by OnProcessSample to output processed samples.
        /// </summary>
        /// <param name="pSample">The processed sample (can be the input
        /// sample if using MFTInputStreamInfoFlags.ProcessesInPlace.</param>
        /// <param name="InputMessageNumber">The (exact) value passed to
        /// OnProcessSample or OnDrain.</param>
        /// <remarks>If pSample is null, it triggers a change to the output
        /// media type.  Normally this is done as part of a dynamic format
        /// change when the client changes our input type.  But in theory, an
        /// MFT can change its output type at will while running.</remarks>
        protected void OutputSample(IMFSample pSample, int InputMessageNumber)
        {
            // It's possible for multiple outputs to be generated from
            // a single input.
            // And to ensure ordering, we can't allow the outputs from
            // input #2 to get queued before all the input from #1 has
            // been queued.

            // This will block threads until it's time to accept their
            // output.  Since m_Slims are manual events, we can wait on
            // them multiple times if multiple samples are being sent.

            WaitForMyTurn(InputMessageNumber);

            if (pSample == null)
            {
                Trace("Preparing for dynamic change of output");
            }
            m_outSamples.Enqueue(pSample);

            // Send message to client that another sample is ready.
            SendSampleReady();
        }

        // Accessors.  Mostly derived classes shouldn't need to access our 
        // members, but if they do, here you go.

        protected IMFMediaType InputType
        {
            get { return m_pInputType; }
            set { SafeRelease(m_pInputType); m_pInputType = value; }
        }

        protected IMFMediaType OutputType
        {
            get { return m_pOutputType; }
            set { SafeRelease(m_pOutputType);  m_pOutputType = value; }
        }

        protected IMFAttributes Attributes
        {
            get { return m_pAttributes; }
        }

        protected int Threshold
        {
            get { return m_Threshold; }
            set { m_Threshold = value; }
        }

        protected bool IsShutdown
        {
            get { return m_Shutdown; }
        }

        protected bool IsStreamActive
        {
            get { return m_StreamIsActive; }
        }

        protected bool IsFlushing
        {
            get { return m_Flushing; }
        }

        #endregion

        #region ThreadStuff

        // Classes and enums used by the processing thread.  At one point I
        // moved this stuff to its own class.  But there is so much interaction
        // between the two, it just didn't make sense.

        // Used by the m_inSamples Queue to describe what type
        // of event just got Dequeued.
        private enum MessageType
        {
            Sample,
            Drain,
            Flush,
            Marker,
            Format,
            Shutdown
        }

        // Wrapper around MessageType + event parameters.
        private class MessageHolder
        {
            public IMFSample sample;
            public IntPtr ptr;
            public IMFMediaType dt;
            public MessageType mt;
            public bool bDiscontinuity;
            public int InputNumber;

            public MessageHolder(MessageType pmt)
            {
                mt = pmt;
            }

            /// <summary>
            /// Show something usable in debug window.
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return string.Format("{0}: {1}", InputNumber, mt);
            }
        }

        /// <summary>
        /// Holds MessageHolder events.  Dequeue also blocks (via semaphore)
        /// if no event is ready.
        /// </summary>
        /// <remarks>Up to m_ThreadCount processing threads can be trying to read
        /// from this while a the same time a ProcessInput message could be trying
        /// to add something.  Rather than have the processing threads call
        /// dequeue in a loop, they wait on a counting semaphore which gets released
        /// in Enqueue.</remarks>
        private class BlockingQueue
        {
            #region Members

            protected readonly ConcurrentQueue<MessageHolder> m_queue;
            protected readonly SemaphoreSlim m_sem;
            protected int m_InputsEnqueued;

            #endregion

            public BlockingQueue()
            {
                m_queue = new ConcurrentQueue<MessageHolder>();
                m_sem = new SemaphoreSlim(0, int.MaxValue);
                m_InputsEnqueued = 0;
            }

            public void Enqueue(MessageHolder item)
            {
                lock (m_queue)
                {
                    item.InputNumber = m_InputsEnqueued;

                    m_InputsEnqueued++;

                    m_queue.Enqueue(item);
                }

                m_sem.Release();
            }
            public MessageHolder Dequeue()
            {
                m_sem.Wait();

                MessageHolder mh;
                while (!m_queue.TryDequeue(out mh))
                    ;

                return mh;
            }

            public void Shutdown()
            {
                MessageHolder mh;

                while (!m_queue.IsEmpty)
                    m_queue.TryDequeue(out mh);

                m_sem.Dispose();
            }
            public int Count
            {
                get { return m_queue.Count; }
            }
        }

        // These methods are used by various IMFTransform methods
        // to queue events to the Processing Thread.
        private void ThreadMessage(IntPtr p)
        {
            MessageHolder mh = new MessageHolder(MessageType.Marker);
            mh.ptr = p;
            m_inSamples.Enqueue(mh);
        }
        private void ThreadMessage(IMFSample sample)
        {
            MessageHolder mh = new MessageHolder(MessageType.Sample);
            mh.sample = sample;
            mh.bDiscontinuity = m_bDiscontinuity;
            m_bDiscontinuity = false;

            m_inSamples.Enqueue(mh);
        }
        private void ThreadMessage(IMFMediaType dt)
        {
            // All processing threads need to block
            for (int x = 0; x < m_ThreadCount; x++)
            {
                MessageHolder mh = new MessageHolder(MessageType.Format);
                mh.dt = dt;
                m_inSamples.Enqueue(mh);
            }
        }
        private void ThreadMessage(MessageType mt)
        {
            if (mt == MessageType.Flush || mt == MessageType.Shutdown)
            {
                m_Flushing = true;
            }
            m_inSamples.Enqueue(new MessageHolder(mt));
        }

        // Understanding this routine is key to understanding how this entire
        // template works.  Reading it should only be necessary if a) there is
        // a bug. b) You need to add new features. c) You are just that curious
        // about how this all works.
        //
        // I write this text both for future maintainers, and because writing
        // this focuses my thinking.  I make bold, assertive statements here
        // about how things work, then check the code to make sure they are
        // true.
        //
        // In general, everything that the caller of the MFT (aka the client)
        // does is treated as an event.  All the interesting IMFTransform &
        // IMFShutdown calls are added as events to the m_inSamples queue (see
        // MessageType & MessageHolder).  So while m_inSamples contains samples,
        // it also contains other event types, so they can be processed
        // (mostly) in order (as described here).
        //
        // That said, there is also support for multiple threads (specified as
        // the argument to the base class constructor).  So 'in order' has a
        // special meaning here.  Let's walk thru some cases.  These assume
        // m_ThreadCount is 4 and Threshold is 8.
        //
        // S1) 5 input samples get sent to ProcessInput.
        //
        // The ProcessingThread threads were created and started in the
        // constructor, and have been blocked in m_inSamples.Dequeue waiting
        // for work.  Now that there is something to do, all 4 threads wake up,
        // get a sample, and send it to OnProcessSample.  OnProcessSample is an
        // abstract method, so I can have no idea what it does, but I picture 2
        // general cases:
        //
        // 1) Each sample is completely independent of every other (like
        // Grayscale).
        // 2) There is some interdependence between samples (like MPEG
        // samples).
        //
        // This template treats these cases identically.  If some type of
        // inter-frame coordination is required, it is up to the derived class
        // to provide it.
        //
        // HOWEVER.
        //
        // There is some synchronization required for both cases.
        // OnProcessSample is expected to call OutputSample to output any
        // samples it creates.  But when OutputSample adds the samples
        // to the m_outSamples queue, they must be in the correct order.  IOW
        // all the output samples generated from the first frame, followed by
        // all the samples from the second frame, etc.
        //
        // So while multiple samples can be CALCULATED at the same time, sample
        // processing isn't *completely* asynchronous, since outputs from the
        // second frame cannot be queued until the all the outputs from first
        // frame have been queued.
        //
        // How does a thread indicate that it has finished sending samples?
        // Since a thread may generate 0, 1 or more outputs from a single input
        // sample, calling OutputSample is not a practical way to signal this.
        // Instead, it simply returns from OnProcessSample.  At that point,
        // presumably no more samples can be generated from that input.
        //
        // The implication of this is that until a thread returns from
        // OnProcessSample, no further output can be generated by any thread.
        // So don't try to use one of the ProcessingThreads as anything other
        // than a sample processor.
        //
        // To return to S1, as each sample is processed, the thread returns
        // from OnProcessSample which releases the next thread to enter
        // OutputSample.
        //
        // A reasonable question here is: What if something goes wrong? If a
        // sample is corrupt, an allocation fails, some unexpected failure of
        // some kind, what do you do then?  Mostly you just (try to) ignore it.
        // Skip the sample and move on as best you can.  That's not just the
        // philosophy of this template, that's what MS suggests.  It's ugly,
        // but what is the alternative?
        //
        // There is a try/catch around OnProcessSample, but for performance
        // reasons, you should avoid depending on it for normal/expected
        // errors.
        //
        // S2) 2 samples followed by a drain.
        //
        // By spec, it is expected that no more input samples will be requested
        // after a drain request until the next NotifyStartOfStream (which
        // presumably comes after the drain completes).
        //
        // So the 2 input samples are processed as per normal, but the third
        // thread simply blocks on the same event that synchronizes access to
        // OutputSample.  When its turn arrives, it knows that all outputs
        // from previous inputs have been queued, so it just sends the
        // notification back to the client.  Then it calls MyEndStream since
        // the stream is now complete.
        //
        // S3) 2 samples followed by a Marker.
        //
        // Just like S2, except that (per spec) we *don't* stop sending
        // requests for input samples.
        //
        // S4) 7 samples followed by a flush.
        //
        // While in theory flush could be handled pretty much like drain,
        // ideally we'd like to skip uselessly calling OnProcessSample as much
        // as possible.
        //
        // So, when the flush message is sent, m_Flushing gets set AS PART of
        // queuing the message.  We don't want to wait until the flush message
        // is read out of the input queue, we want to start flushing as soon as
        // we can.
        //
        // So, the 4 threads that are processing samples when the
        // flush gets sent will all complete their processing as per normal.
        // When they are done and loop back to get their next sample, they find
        // the flushing flag is set, so they skip the processing and loop
        // around for more, quickly emptying the input queue.
        //
        // Eventually the flush message is retrieved from the queue.  That
        // thread blocks on the same event that synchronizes access to
        // OutputSample.  When it unblocks, it flushes the output queue.  And
        // (this is important) it sets m_NeedInputPending to -1.  The
        // CommandFlush message that arrived in ProcessMessage HASN'T
        // returned yet.  It is blocking, waiting for us to complete.  If we
        // don't do this blocking, the client  could conceivably call
        // NotifyStartOfStream to start streaming again while we are still
        // flushing, resulting in a mess.
        //
        // I should also point out that if you are doing inter-frame
        // coordination (such as the MPEG example mentioned in S1), you need to
        // be aware that 'waiting for the other part to arrive' may be a
        // problem if the queue is being flushed.  You can check IsFlushing to
        // handle this case.
        //
        // S5) 4 input samples followed by a format change followed by 4
        // input samples.
        //
        // This one is a pain.  By spec, asynchronous MFTs must support
        // 'Dynamic Format Changes' (see ' Handling Stream Changes' in MSDN).
        // So, in the middle of processing frames using formatA, you can receive
        // a change notice and start receiving frames using formatB.
        //
        // This format change could be something as simple as changing the
        // frame size, or something more drastic like changing the subtype.
        //
        // So, my requirements here are:
        //
        // a) m_pInputType must not change until all pending input samples with
        // the old format have been processed.
        // b) m_pOutputType must not change until all pending output samples
        // from the old format have been processed thru both OnProcessSample &
        // MyProcessOutput.  This is not a requirement for the template code,
        // but OnProcessSample is a virtual function.  Who knows what it may 
        // need.
        // c) Allow new inputs to be queued pending the changes.
        //
        // So here's what happens:
        //
        // a) When the new format is proposed (via SetInputType), it is
        // validated with OnCheckInputType.  No change happens unless this
        // approves it.  OnCheckInputType must NOT compare the proposed new
        // with the currently set output type.  This breaks the whole concept 
        // of dynamic format changes.
        // b) When the change is approved by OnCheckInputType, m_ThreadCount 
        // Format messages are queued to m_inSamples.  They all contain the 
        // new media type.
        // c) Any remaining input samples are processed until the format
        // messages start getting hit.  Processing threads will block until the
        // final format message is processed.
        // d) m_pInputType gets changed and OnSetInputType gets called.
        // e) OnCheckOutputType is then called to see if the current output
        // type is still valid with the new input type.
        // f) *If* the output type must be changed, threads remain blocked
        // until the new output type is negotiated.
        // g) The processing threads are released.
        //
        // At this point samples can start getting processed again.
        //
        // Note that by spec, the client cannot change the output type
        // while the stream is active.  The output format type change must be
        // initiated by the MFT.
        //
        // S6) 7 samples followed by Shutdown.
        //
        // As with Flush, Shutdown also sets m_Flushing when the event gets
        // queued.  So while we don't cancel threads in the middle of
        // OnProcessSample, we do skip processing as fast as we can.
        //
        // When a shutdown is issued, threads could be in any number of places:
        //
        // a) Blocked in m_inSamples.Dequeue waiting for work
        // b) Processing in OnProcessSample
        // c) Blocked in OutputSample waiting for their turn
        // d) Waiting for other threads in Drain, Flush, Marker
        // e) Spinning in Format
        //
        // The question is, how to get all these folks to exit?  It might be
        // tempting to do nothing.  Since ProcessingThreads are all
        // IsBackground = true, .net will kill them cleanly at app shutdown.
        // But we may not be DOING an app shutdown.  MFTs can be loaded and
        // unloaded at will during an app run.  So, we really do need to clear
        // out all these threads.
        //
        // Since they might be blocked in m_inSamples.Dequeue (a), we send
        // m_ThreadCount Shutdown messages to wake them up.  However, we don't
        // actually have to wait to process the shutdown messages.  If there
        // are 7 samples queued followed by m_ThreadCount Shutdown messages, a
        // thread could retrieve a sample, see that we are m_Flushing, hit the
        // bottom of the loop and exit without ever having seen a Shutdown
        // event message.  Since we keep doing the 'Release the next guy' stuff
        // at the end of the loop, everybody who is blocked gets released
        // (b, c, d).  By checking for m_ShutdownThreads during the spins in
        // Format, we prevent this from blocking shutdown as well (e).
        //
        // Processing threads in OnProcessSample that are waiting for external
        // events ('waiting for the other part to arrive' or waiting for a new
        // output type to get set, etc) also need to watch for shutdown.  I
        // don't expect this to be a common situation.
        //
        // S7) 2 samples, drain, flush, StartOfStream, 2 samples
        //
        // While processing a flush, no messages can get added to the queue
        // until the flush is complete.  Further, since flush sets 
        // m_NeedInputPending to -1, no samples been accepted since the 
        // flush.
        //
        // When a new NotifyStartOfStream message is received from the client,
        // we need to reset m_Flushing and m_NeedInputPending, and send some
        // NeedInput messages.  But that doesn't require any action by the 
        // processing threads.  Once ProcessMessage has reset everything,
        // new samples start arriving and away we go.

        private void ProcessingThread()
        {
            do
            {
                MessageHolder mh = (MessageHolder)m_inSamples.Dequeue();

                switch (mh.mt)
                {
                    case MessageType.Sample:
                        if (!m_Flushing)
                        {
                            try
                            {
                                OnProcessSample(mh.sample, mh.bDiscontinuity, mh.InputNumber);
                            }
                            catch
                            {
                                // Ignore any errors and just keep going
                            }
                        }
                        else
                        {
                            SafeRelease(mh.sample);
                        }
                        break;

                    case MessageType.Drain:
                        // Wait for my turn (using the same events as
                        // OutputSample).
                        WaitForMyTurn(mh.InputNumber);

                        try
                        {
                            // Process any 'final' samples (ie remaining samples
                            // from an audio echo).
                            OnDrain(mh.InputNumber);
                        }
                        catch
                        {
                            // Ignore any errors and just keep going
                        }
                        SendStreamEvent(MediaEventType.METransformDrainComplete);
                        MyEndStream();
                        m_bDiscontinuity = true;
                        break;

                    case MessageType.Flush:
                        // Flush stays in effect until next StartOfStream.

                        // Also, since the thread that sent the Flush message
                        // is blocked waiting for us to respond (holding the
                        // m_TransformLockObject lock) there are no
                        // messages in the queue following the flush.

                        // Wait for the other threads to finish.  Note that they
                        // might be simply reading/discarding samples from the
                        // queue (which is presumably very quick), or they might
                        // be in OnProcessSample, and could take a while.
                        WaitForMyTurn(mh.InputNumber);

                        // Now that all the input have finished, clear the output
                        // queue.
                        IMFSample samp;
                        while (!m_outSamples.IsEmpty)
                            if (m_outSamples.TryDequeue(out samp))
                                SafeRelease(samp);

                        // Note that ProcessMessage is blocked waiting for this value
                        // to change.  This also signals that no more inputs should
                        // be requested until the next NotifyStartOfStream.
                        Thread.VolatileWrite(ref m_NeedInputPending, -1);

                        break;

                    case MessageType.Marker:
                        // When all the outputs for the previous inputs have been sent,
                        // it's time to send the marker.
                        WaitForMyTurn(mh.InputNumber);

                        try
                        {
                            SendMarker(mh.ptr);
                        }
                        catch
                        {
                            // Ignore any errors and just keep going
                        }

                        break;

                    case MessageType.Format:
                        // When the format changes, m_Thread Format messages
                        // get queued.  We need to wait for them all.
                        int me = Interlocked.Increment(ref m_ThreadsBlocked);

                        // Note that we never blocked requesting more input.  
                        // There could be samples using the new format in the 
                        // m_inSamples queue, which is why we are keeping 
                        // things blocked until the format change is complete.

                        if (me == m_ThreadCount)
                        {
                            // If there are multiple processing threads, all 
                            // but this one are sleeping at the Wait below.

                            // At this point:
                            // - A format change was set via SetInputType
                            // - All samples queued before this (ie using the
                            // old type) have been processed thru OnProcessSample.

                            // Wait for all the samples to go thru 
                            // MyProcessOutput.  While it might be safe/performant
                            // to proceed after all the OnProcessSamples are 
                            // complete, Let's be safe.  
                            // It's possible that instead of processing our 
                            // output, someone could flush it, so check for 
                            // that.
                            while (m_outSamples.Count > 0 && !m_Flushing)
                                Thread.Sleep(10);

                            try
                            {
                                // Time to change the type.  After this, 
                                // m_pInputType has changed, and 
                                // m_pOutputType either isn't going to change, 
                                // or is null, awaiting the client's call to
                                // SetOutputType.

                                // Note that if we are flushing, we still 
                                // need to do the change.  It's a waste of 
                                // time if we are shutting down, but there
                                // you go.
                                MySetInput(mh.dt);

                                // When m_pOutputType isn't null, we have our 
                                // (possibly new) output type, so we can 
                                // proceed with samples processing.  If we are
                                // flushing, no one is reading our outputs, so
                                // there's not point waiting.
                                while (m_pOutputType == null &&
                                    !m_Flushing)
                                    Thread.Sleep(10);

                                // Release the other threads (and ourself).
                                m_FormatEvent.Release(m_ThreadCount);
                            }
                            catch
                            {
                                // Ignore any errors and just keep going
                                // even though we are probably toast.
                            }
                        }

                        // Wait until the both the input type has changed and
                        // (possibly) the output type has been updated.

                        m_FormatEvent.Wait();

                        // Make sure no one tries to loop around and grab 
                        // someone else's semaphore.

                        if (Interlocked.Decrement(ref m_ThreadsBlocked) != 0)
                        {
                            while (Thread.VolatileRead(ref m_ThreadsBlocked) != 0)
                                Thread.Sleep(0);
                        }

                        break;

                    case MessageType.Shutdown:
                        // Nothing to do here.  We will exit at the bottom of
                        // the loop.
                        break;

                    default:
                        Debug.Fail("Unrecognized type");
                        break;
                }

                if (m_ThreadCount > 1)
                {
                    // Note that since this is a MANUAL event, we can wait
                    // on it multiple times. Once it's set, it stays set
                    // until it gets Reset.
                    WaitForMyTurn(mh.InputNumber);

                    // Release the next guy
                    int MyIndex = mh.InputNumber % m_ThreadCount;
                    int k = MyIndex < m_ThreadCount - 1 ? MyIndex + 1 : 0;
                    m_Slims[k].Set();

                    // And now we are resetting it.
                    m_Slims[MyIndex].Reset();
                }

            } while (Thread.VolatileRead(ref m_ShutdownThreads) == 0);

            // Last guy out: free the thread stuff
            if (Interlocked.Decrement(ref m_ShutdownThreads) == 0)
            {
                m_FormatEvent.Dispose();
                m_inSamples.Shutdown(); // Flush queue and free semaphore

                for (int x = 0; x < m_ThreadCount; x++)
                {
                    m_Slims[x].Dispose();
                }
            }
        }

        #endregion
    }
}
