﻿using MediaFoundation;
using MediaFoundation.Misc;
using System;

namespace VideoPlayer.Render
{
    class VideoBufferLock : COMBase, IDisposable
    {
        private IMFMediaBuffer m_pBuffer;
        private IMF2DBuffer m_p2DBuffer;

        private bool m_bLocked;

        // Constructor
        public VideoBufferLock(IMFMediaBuffer pBuffer)
        {
            m_p2DBuffer = null;
            m_bLocked = false;
            m_pBuffer = pBuffer;
            
            m_p2DBuffer = pBuffer as IMF2DBuffer;
        }

        //-------------------------------------------------------------------
        // LockBuffer
        //
        // Locks the buffer. Returns a pointer to scan line 0 and returns the stride.
        //
        // The caller must provide the default stride as an input parameter, in case
        // the buffer does not expose IMF2DBuffer. You can calculate the default stride
        // from the media type.
        //-------------------------------------------------------------------
        public HResult LockBuffer(
            int lDefaultStride,    // Minimum stride (with no padding).
            int dwHeightInPixels,  // Height of the image, in pixels.
            out IntPtr ppbScanLine0,    // Receives a pointer to the start of scan line 0.
            out int plStride          // Receives the actual stride.
            )
        {
            HResult hr = HResult.S_OK;
            ppbScanLine0 = IntPtr.Zero;
            plStride = 0;

            // Use the 2-D version if available.
            if (m_p2DBuffer != null)
            {
                hr = m_p2DBuffer.Lock2D(out ppbScanLine0, out plStride);
            }
            else
            {
                // Use non-2D version.
                IntPtr pData;
                int pcbMaxLength;
                int pcbCurrentLength;

                hr = m_pBuffer.Lock(out pData, out pcbMaxLength, out pcbCurrentLength);
                if (Succeeded(hr))
                {
                    plStride = lDefaultStride;
                    if (lDefaultStride < 0)
                    {
                        // Bottom-up orientation. Return a pointer to the start of the
                        // last row *in memory* which is the top row of the image.
                        ppbScanLine0 += lDefaultStride * (dwHeightInPixels - 1);
                    }
                    else
                    {
                        // Top-down orientation. Return a pointer to the start of the buffer.
                        ppbScanLine0 = pData;
                    }
                }
            }

            m_bLocked = (Succeeded(hr));

            return hr;
        }

        //-------------------------------------------------------------------
        // UnlockBuffer
        //
        // Unlocks the buffer. Called automatically by the destructor.
        //-------------------------------------------------------------------
        public void UnlockBuffer()
        {
            if (m_bLocked)
            {
                if (m_p2DBuffer != null)
                {
                    m_p2DBuffer.Unlock2D();
                }
                else
                {
                    m_pBuffer.Unlock();
                }

                m_bLocked = false;
            }
        }

        public void Dispose()
        {
            UnlockBuffer();
            SafeRelease(m_pBuffer);
            SafeRelease(m_p2DBuffer);

            m_pBuffer = null;
            m_p2DBuffer = null;

            GC.SuppressFinalize(this);
        }
    }
}
