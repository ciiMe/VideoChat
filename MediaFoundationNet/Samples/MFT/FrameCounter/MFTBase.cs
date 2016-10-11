/* Copyright - Released to public domain
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE.
*****************************************************************************/

/* File Version: 2015-06-26 from http://mfnet.sf.net */

/* What this file is for and how to use it:
 *
 * The goal of this file is to handle all the plumbing for writing a
 * synchronous MFT, leaving just a few abstract routines that must be 
 * implemented by a subclass to have a fully working c# MFT.
 *
 * To read about synchronous MFTs, visit:
 *
 * https://msdn.microsoft.com/en-us/library/windows/desktop/dd940441%28v=vs.85%29.aspx
 *
 * While IMFTransform allows for variations, this code assumes that there
 * will be exactly 1 input stream, and 1 output stream.
 * 
 * Unless you have specific needs, no changes to this file should be required.
 * 
 * For detailed instructions on how to use this file, see HowTo.txt.
 */

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using MediaFoundation;
using MediaFoundation.Misc;

namespace MediaFoundation.Transform
{
    // Fudge on the name to avoid conflicts with AsyncMFTBase.
    [ComImport, System.Security.SuppressUnmanagedCodeSecurity,
    Guid("0000010c-0000-0000-C000-000000000046"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistAlt
    {
        [PreserveSig]
        HResult GetClassID(out Guid pClassID);
    }

    abstract public class MFTBase : COMBase, IMFTransform, IPersistAlt
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
        /// The most recent sample received by ProcessInput, or null if no sample is pending.
        /// </summary>
        private IMFSample m_pSample;

        #endregion

        #region Overrides

        /// <summary>
        /// Report whether a proposed input type is accepted by the MFT.
        /// </summary>
        /// <param name="pmt">The type to check.  Should never be null.</param>
        /// <returns>S_Ok if the type is valid or MF_E_INVALIDTYPE.</returns>
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
        /// The routine that usually performs the transform.
        /// </summary>
        /// <param name="pOutputSamples">The structure to populate with output values.</param>
        /// <returns>S_Ok unless error.</returns>
        /// <remarks>
        /// The input sample is in InputSample.  Process it into the pOutputSamples struct.
        /// Depending on what you set in On*StreamInfo, you can either perform
        /// in-place processing by modifying the input sample (which still must
        /// set inout the struct), or create a new IMFSample and FULLY populate
        /// it from the input.  If the input sample has been fully processed, 
        /// set InputSample to null.
        /// </remarks>
        abstract protected HResult OnProcessOutput(ref MFTOutputDataBuffer pOutputSamples);

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
        /// <remarks>The new type is in InputType, and can be null.</remarks>
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
        /// Override to allow the client to retrieve the MFT's list of supported Input Types.
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
        /// A new input sample has been received.
        /// </summary>
        /// <returns>S_Ok unless error.</returns>
        /// <remarks>The sample is in InputSample.  Typically nothing is done
        /// here.  The processing is done in OnProcessOutput, when we have
        /// the output buffer.</remarks>
        virtual protected HResult OnProcessInput()
        {
            return HResult.S_OK;
        }

        /// <summary>
        /// Called at end of stream (and start of new stream).
        /// </summary>
        virtual protected void OnReset()
        {
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
        #endregion

        protected MFTBase()
        {
            Trace("MFTImplementation");

            m_TransformLockObject = new object();

            m_pInputType = null;
            m_pOutputType = null;

            m_pSample = null;
        }

        ~MFTBase()
        {
            Trace("~MFTImplementation");

            if (m_TransformLockObject != null)
            {
                SafeRelease(m_pInputType);
                SafeRelease(m_pOutputType);
                SafeRelease(m_pSample);

                m_TransformLockObject = null;
            }
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
                // Trace("GetStreamCount"); not interesting

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

                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    // The implementation can override the defaults,
                    // and must set cbSize
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

                CheckValidStream(dwOutputStreamID);

                lock (m_TransformLockObject)
                {
                    // The implementation can override the defaults,
                    // and must set cbSize.
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

        public HResult GetInputStreamAttributes(
            int dwInputStreamID,
            out IMFAttributes pAttributes
        )
        {
            pAttributes = null;

            HResult hr = HResult.S_OK;

            try
            {
                // Trace("GetInputStreamAttributes"); Not interesting

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

                CheckValidStream(dwInputStreamID);

                lock (m_TransformLockObject)
                {
                    // If we have an input sample, the client cannot change the type now.
                    if (!HasPendingOutput())
                    {
                        // Allow input type to be cleared.
                        if (pType != null)
                        {
                            // Validate non-null types.
                            hr = OnCheckInputType(pType);
                        }

                        if (Succeeded(hr))
                        {
                            // Does the caller want to set the type?  Or just test it?
                            bool bReallySet = ((dwFlags & MFTSetTypeFlags.TestOnly) == 0);

                            if (bReallySet)
                            {
                                // Make a copy of the IMFMediaType.
                                InputType = CloneMediaType(pType);

                                OnSetInputType();
                            }
                        }
                    }
                    else
                    {
                        // Can't change type while samples are pending
                        hr = HResult.MF_E_TRANSFORM_CANNOT_CHANGE_MEDIATYPE_WHILE_PROCESSING;
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

                CheckValidStream(dwOutputStreamID);

                lock (m_TransformLockObject)
                {
                    // If we have an input sample, the client cannot change the type now.
                    if (!HasPendingOutput())
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

                                OnSetOutputType();
                            }
                        }
                    }
                    else
                    {
                        // Cannot change type while samples are pending
                        hr = HResult.MF_E_TRANSFORM_CANNOT_CHANGE_MEDIATYPE_WHILE_PROCESSING;
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

                lock (m_TransformLockObject)
                {
                    switch (eMessage)
                    {
                        case MFTMessageType.NotifyStartOfStream:
                            // Optional message for non-async MFTs.
                            reset();
                            break;

                        case MFTMessageType.CommandFlush:
                            reset();
                            break;

                        case MFTMessageType.CommandDrain:
                            // Drain: Tells the MFT not to accept any more input until
                            // all of the pending output has been processed. That is our
                            // default behavior already, so there is nothing to do.
                            break;

                        case MFTMessageType.CommandMarker:
                            hr = HResult.E_NOTIMPL;
                            break;

                        case MFTMessageType.NotifyEndOfStream:
                            break;

                        case MFTMessageType.NotifyBeginStreaming:
                            break;

                        case MFTMessageType.NotifyEndStreaming:
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
                            if (AllowInput())
                            {
                                // Cache the sample. We usually do the actual
                                // work in ProcessOutput, since that's when we
                                // have the output buffer.
                                m_pSample = pSample;

                                // Call the virtual function.
                                hr = OnProcessInput();
                            }
                            else
                            {
                                // Already have input sample
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

                // Check input parameters.

                if (dwFlags != MFTProcessOutputFlags.None || cOutputBufferCount != 1)
                {
                    hr = HResult.E_INVALIDARG;
                }

                if (Succeeded(hr) && pOutputSamples == null)
                {
                    hr = HResult.E_POINTER;
                }

                // In theory, we should check pOutputSamples[0].pSample,
                // but it may be null or not depending on how the derived
                // set MFTOutputStreamInfoFlags, so we leave the checking
                // for OnProcessOutput.

                if (Succeeded(hr))
                {
                    lock (m_TransformLockObject)
                    {
                        hr = AllTypesSet();
                        if (Succeeded(hr))
                        {
                            // If we don't have an input sample, we need some input before
                            // we can generate any output.
                            if (HasPendingOutput())
                            {
                                hr = OnProcessOutput(ref pOutputSamples[0]);
                            }
                            else
                            {
                                // No input sample
                                hr = HResult.MF_E_TRANSFORM_NEED_MORE_INPUT;
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

        #region IPersistAlt methods

        public HResult GetClassID(out Guid pClassID)
        {
            pClassID = this.GetType().GUID;
            return HResult.S_OK;
        }

        #endregion

        #region Private methods (Only expected to be used by template)

        /// <summary>
        /// Allow for reset between NotifyStartOfStream calls.
        /// </summary>
        private void reset()
        {
            InputSample = null;

            // Call the virtual function
            OnReset();
        }

        /// <summary>
        /// Are inputs allowed at the current time?
        /// </summary>
        /// <returns></returns>
        private bool AllowInput()
        {
            // If we already have an input sample, we don't accept
            // another one until the client calls ProcessOutput or Flush.
            return m_pSample == null;
        }

        /// <summary>
        /// Do we have data for ProcessOutput?
        /// </summary>
        /// <returns></returns>
        private bool HasPendingOutput()
        {
            return m_pSample != null;
        }

        /// <summary>
        /// Check for valid stream number.
        /// </summary>
        /// <param name="dwStreamID">Stream to check.</param>
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
        /// <returns></returns>
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
        /// All the public interface methods use this to wrap their returns.
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

        #endregion

        #region Utility methods (possibly useful to derived classes).

        /// <summary>Debug.Writeline all attributes.</summary>
        /// <param name="ia">The attributes to print</param>
        /// <remarks>Useful for examining attributes on IMFMediaTypes, IMFSamples, etc</remarks>
        [Conditional("DEBUG")]
        protected static void TraceAttributes(IMFAttributes ia)
        {
            string s = MFDump.DumpAttribs(ia);
            Debug.Write(s);
        }

        /// <summary>
        /// Check to see if two IMFMediaTypes are identical.
        /// </summary>
        /// <param name="a">First media type.</param>
        /// <param name="b">Second media type.</param>
        /// <returns>S_Ok if identical, else MF_E_INVALIDTYPE.</returns>
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
        /// Check Major type and Subtype
        /// </summary>
        /// <param name="pmt">IMFMediaType to check</param>
        /// <param name="gMajorType">MajorType to check for.</param>
        /// <param name="gSubtype">SubType to check for.</param>
        /// <returns>S_Ok if match, else MF_E_INVALIDTYPE.</returns>
        protected static HResult CheckMediaType(IMFMediaType pmt, Guid gMajorType, Guid gSubtype)
        {
            Guid major_type;

            // Major type must be video.
            HResult hr = HResult.S_OK;
            MFError throwonhr;

            throwonhr = pmt.GetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, out major_type);

            if (major_type == gMajorType)
            {
                Guid subtype;

                // Get the subtype GUID.
                throwonhr = pmt.GetGUID(MFAttributesClsid.MF_MT_SUBTYPE, out subtype);

                if (subtype != gSubtype)
                {
                    hr = HResult.MF_E_INVALIDTYPE;
                }
            }
            else
            {
                hr = HResult.MF_E_INVALIDTYPE;
            }

            return hr;
        }

        /// <summary>
        /// Check Major type and Subtype
        /// </summary>
        /// <param name="pmt">IMFMediaType to check</param>
        /// <param name="gMajorType">MajorType to check for.</param>
        /// <param name="gSubtypes">Array of subTypes to check for.</param>
        /// <returns>S_Ok if match, else MF_E_INVALIDTYPE.</returns>
        protected static HResult CheckMediaType(IMFMediaType pmt, Guid gMajorType, Guid[] gSubTypes)
        {
            Guid major_type;

            // Major type must be video.
            HResult hr = HResult.S_OK;
            MFError throwonhr;

            throwonhr = pmt.GetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, out major_type);

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
        /// Create a partial media type from a Major and Sub type.
        /// </summary>
        /// <param name="gMajorType">Major type.</param>
        /// <param name="gSubtype">Sub type.</param>
        /// <returns>Newly created media type.</returns>
        protected static IMFMediaType CreatePartialType(Guid gMajorType, Guid gSubtype)
        {
            IMFMediaType ppmt;
            MFError throwonhr;

            throwonhr = MFExtern.MFCreateMediaType(out ppmt);
            throwonhr = ppmt.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, gMajorType);
            throwonhr = ppmt.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, gSubtype);

            return ppmt;
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
            HResult hr;

            if (dwTypeIndex < gSubTypes.Length)
            {
                ppmt = CreatePartialType(gMajorType, gSubTypes[dwTypeIndex]);
                hr = HResult.S_OK;
            }
            else
            {
                ppmt = null;
                hr = HResult.MF_E_NO_MORE_TYPES;
            }

            return hr;
        }

        /// <summary>
        /// Return a duplicate of a media type. Null input gives null output.
        /// </summary>
        /// <param name="inType">The IMFMediaType to clone or null.</param>
        /// <returns>Duplicate IMFMediaType or null.</returns>
        protected static IMFMediaType CloneMediaType(IMFMediaType inType)
        {
            IMFMediaType outType = null;

            if (inType != null)
            {
                MFError throwonhr;

                throwonhr = MFExtern.MFCreateMediaType(out outType);
                throwonhr = inType.CopyAllItems(outType);
            }

            return outType;
        }

        [Conditional("DEBUG")]
        protected static void Trace(string s)
        {
            Debug.WriteLine(string.Format("{0}:{1}", DateTime.Now.ToString("HH:mm:ss.ffff"), s));
        }

        // Accessors.  Mostly derived classes shouldn't need to access our 
        // members, but if they do, here you go.

        protected IMFMediaType InputType
        {
            get { return m_pInputType; }
            set { SafeRelease(m_pInputType);  m_pInputType = value; }
        }
        protected IMFMediaType OutputType
        {
            get { return m_pOutputType; }
            set { SafeRelease(m_pOutputType); m_pOutputType = value; }
        }

        protected IMFSample InputSample
        {
            get { return m_pSample; }
            set { SafeRelease(m_pSample); m_pSample = value; }
        }

        #endregion
    }
}
