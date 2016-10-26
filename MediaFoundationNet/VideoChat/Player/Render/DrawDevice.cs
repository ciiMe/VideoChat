using System;
using System.Runtime.InteropServices;
using System.Drawing;

using MediaFoundation;
using MediaFoundation.Misc;

using SlimDX.Direct3D9;
using SlimDX;
using VideoPlayer.WindowsExtern;

namespace VideoPlayer.Render
{
    public class DrawDevice : COMBase
    {
        #region Definitions

        private const int NUM_BACK_BUFFERS = 2;

        /// <summary>
        /// A struct that describes a YUYV pixel
        /// </summary>
        private struct YUYV
        {
            public byte Y;
            public byte U;
            public byte Y2;
            public byte V;

            public YUYV(byte y, byte u, byte y2, byte v)
            {
                Y = y;
                U = u;
                Y2 = y2;
                V = v;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RGBQUAD
        {
            public byte B;
            public byte G;
            public byte R;
            public byte A;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RGB24
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
        }

        #endregion

        private IntPtr m_hwnd;
        private Device m_pDevice;
        private SwapChain m_pSwapChain;

        private PresentParameters[] m_d3dpp;

        // Format information 
        private int m_width;
        private int m_height;
        private int m_lDefaultStride;
        private MFRatio m_PixelAR;
        // Destination rectangle
        private Rectangle m_rcDest;

        public DrawDevice()
        {
            m_hwnd = IntPtr.Zero;
            m_pDevice = null;
            m_pSwapChain = null;

            m_d3dpp = null;

            m_width = 0;
            m_height = 0;
            m_lDefaultStride = 0;
            m_PixelAR.Denominator = m_PixelAR.Numerator = 1;
            m_rcDest = Rectangle.Empty;
        }

        private HResult TestCooperativeLevel()
        {
            if (m_pDevice == null)
            {
                return HResult.E_FAIL;
            }

            HResult hr = HResult.S_OK;

            // Check the current status of D3D9 device.
            SlimDX.Result r = m_pDevice.TestCooperativeLevel();

            hr = (HResult)r.Code;

            return hr;
        }

        private HResult CreateSwapChains()
        {
            HResult hr = HResult.S_OK;

            PresentParameters pp = new PresentParameters();

            if (m_pSwapChain != null)
            {
                m_pSwapChain.Dispose();
                m_pSwapChain = null;
            }

            pp.EnableAutoDepthStencil = false;
            pp.BackBufferWidth = m_width;
            pp.BackBufferHeight = m_height;
            pp.Windowed = true;
            pp.SwapEffect = SwapEffect.Flip;
            pp.DeviceWindowHandle = m_hwnd;
            pp.BackBufferFormat = Format.X8R8G8B8;
            pp.PresentFlags = PresentFlags.DeviceClip | PresentFlags.LockableBackBuffer;
            pp.PresentationInterval = PresentInterval.Immediate;
            pp.BackBufferCount = NUM_BACK_BUFFERS;

            m_pSwapChain = new SwapChain(m_pDevice, pp);

            return hr;
        }

        //-------------------------------------------------------------------
        //  UpdateDestinationRect
        //
        //  Update the destination rectangle for the current window size.
        //  The destination rectangle is letterboxed to preserve the
        //  aspect ratio of the video image.
        //-------------------------------------------------------------------
        private void UpdateDestinationRect()
        {
            Rectangle rcSrc = new Rectangle(0, 0, m_width, m_height);
            Rectangle rcClient = GetClientRect(m_hwnd);
            Rectangle rectanClient = new Rectangle(rcClient.Left, rcClient.Top, rcClient.Right - rcClient.Left, rcClient.Bottom - rcClient.Top);

            rcSrc = CorrectAspectRatio(rcSrc, m_PixelAR);

            m_rcDest = LetterBoxRect(rcSrc, rectanClient);
        }

        #region Public Methods

        public HResult CreateDevice(IntPtr hwnd)
        {
            if (m_pDevice != null)
            {
                return HResult.S_OK;
            }

            PresentParameters[] pp = new PresentParameters[1];

            pp[0] = new PresentParameters();
            pp[0].BackBufferFormat = Format.X8R8G8B8;
            pp[0].SwapEffect = SwapEffect.Copy;
            pp[0].PresentationInterval = PresentInterval.Immediate;
            pp[0].Windowed = true;
            pp[0].DeviceWindowHandle = hwnd;
            pp[0].BackBufferHeight = 0;
            pp[0].BackBufferWidth = 0;
            pp[0].EnableAutoDepthStencil = false;

            using (Direct3D d = new Direct3D())
            {
                m_pDevice = new Device(d, 0, DeviceType.Hardware, hwnd, CreateFlags.HardwareVertexProcessing | CreateFlags.FpuPreserve | CreateFlags.Multithreaded, pp);
            }

            m_hwnd = hwnd;
            m_d3dpp = pp;

            return HResult.S_OK;
        }

        public HResult ResetDevice()
        {
            HResult hr = HResult.S_OK;

            if (m_pDevice != null)
            {
                PresentParameters[] d3dpp = (PresentParameters[])m_d3dpp.Clone();

                try
                {
                    if (m_pSwapChain != null)
                    {
                        m_pSwapChain.Dispose();
                        m_pSwapChain = null;
                    }
                    d3dpp[0].BackBufferHeight = 0;
                    d3dpp[0].BackBufferWidth = 0;
                    Result r = m_pDevice.Reset(d3dpp);

                    if (r.IsFailure)
                    {
                        DestroyDevice();
                    }
                }
                catch
                {
                    DestroyDevice();
                }
            }

            if (m_pDevice == null)
            {
                hr = CreateDevice(m_hwnd);

                if (Failed(hr))
                {
                    return hr;
                }
            }

            if ((m_pSwapChain == null))
            {
                hr = CreateSwapChains();
                if (Failed(hr)) { return hr; }

                UpdateDestinationRect();
            }

            return hr;
        }

        public void DestroyDevice()
        {
            if (m_pSwapChain != null)
            {
                m_pSwapChain.Dispose();
                m_pSwapChain = null;
            }
            if (m_pDevice != null)
            {
                m_pDevice.Dispose();
                m_pDevice = null;
            }
        }

        public HResult InitializeSetVideoSize(int width, int height, MFRatio ratio)
        {
            HResult hr = HResult.S_OK;

            m_width = width;
            m_height = height;
            m_PixelAR = ratio;

            FourCC f = new FourCC(MFMediaType.YUY2);
            // Get the image stride. 
            hr = MFExtern.MFGetStrideForBitmapInfoHeader(f.ToInt32(), width, out m_lDefaultStride);

            hr = CreateSwapChains();
            if (Failed(hr)) { goto done; }

            UpdateDestinationRect();

            done:
            return hr;
        }

        public HResult DrawFrame(IMFMediaBuffer pCaptureDeviceBuffer)
        {
            HResult hr = HResult.S_OK;
            IntPtr pbScanline0;
            int lStride = 0;
            Result res;

            Surface pSurf = null;
            Surface pBB = null;

            if (m_pDevice == null || m_pSwapChain == null)
            {
                return HResult.S_OK;
            }

            using (VideoBufferLock locker = new VideoBufferLock(pCaptureDeviceBuffer))
            {
                hr = TestCooperativeLevel();
                if (Failed(hr)) { goto done; }

                // Lock the video buffer. This method returns a pointer to the first scan
                // line in the image, and the stride in bytes.
                hr = locker.LockBuffer(m_lDefaultStride, m_height, out pbScanline0, out lStride);
                if (Failed(hr)) { goto done; }

                // Get the swap-chain surface.
                pSurf = m_pSwapChain.GetBackBuffer(0);

                // Lock the swap-chain surface and get Graphic stream object.
                DataRectangle dr = pSurf.LockRectangle(LockFlags.NoSystemLock);

                try
                {
                    using (dr.Data)
                    {
                        ApplyToD3d(dr.Data.DataPointer, dr.Pitch, pbScanline0, lStride, m_width, m_height);
                    }
                }
                finally
                {
                    res = pSurf.UnlockRectangle();
                    MFError.ThrowExceptionForHR(res.Code);
                }
            }

            // Color fill the back buffer.
            pBB = m_pDevice.GetBackBuffer(0, 0);

            m_pDevice.ColorFill(pBB, Color.FromArgb(0, 0, 0x80));

            // Blit the frame.
            Rectangle r = new Rectangle(0, 0, m_width, m_height);

            res = m_pDevice.StretchRectangle(pSurf, r, pBB, m_rcDest, TextureFilter.Linear);
            hr = (HResult)res.Code;

            if (res.IsSuccess)
            {
                res = m_pDevice.Present();
                hr = (HResult)res.Code;
            }

            done:
            SafeRelease(pBB);
            SafeRelease(pSurf);

            return hr;
        }

        #endregion

        #region Static Methods
         
        private static byte Clip(int clr)
        {
            return (byte)(clr < 0 ? 0 : (clr > 255 ? 255 : clr));
        }

        private static RGBQUAD ConvertYCrCbToRGB(byte y, byte cr, byte cb)
        {
            RGBQUAD rgbq = new RGBQUAD();

            int c = y - 16;
            int d = cb - 128;
            int e = cr - 128;

            rgbq.R = Clip((298 * c + 409 * e + 128) >> 8);
            rgbq.G = Clip((298 * c - 100 * d - 208 * e + 128) >> 8);
            rgbq.B = Clip((298 * c + 516 * d + 128) >> 8);

            return rgbq;
        }

        /// <summary>
        /// Convert the frame. This also copies it to the Direct3D surface.
        /// </summary> 
        unsafe private static void ApplyToD3d(IntPtr pDest, int lDestStride, IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels)
        {
            YUYV* pSrcPel = (YUYV*)pSrc;
            RGBQUAD* pDestPel = (RGBQUAD*)pDest;

            lSrcStride /= 4; // convert lSrcStride to YUYV
            lDestStride /= 4; // convert lDestStride to RGBQUAD

            for (int y = 0; y < dwHeightInPixels; y++)
            {
                for (int x = 0; x < dwWidthInPixels / 2; x++)
                {
                    pDestPel[x * 2] = ConvertYCrCbToRGB(pSrcPel[x].Y, pSrcPel[x].V, pSrcPel[x].U);
                    pDestPel[(x * 2) + 1] = ConvertYCrCbToRGB(pSrcPel[x].Y2, pSrcPel[x].V, pSrcPel[x].U);
                }

                pSrcPel += lSrcStride;
                pDestPel += lDestStride;
            }
        }

        //-------------------------------------------------------------------
        // LetterBoxDstRect
        //
        // Takes a src rectangle and constructs the largest possible
        // destination rectangle within the specifed destination rectangle
        // such that the video maintains its current shape.
        //
        // This function assumes that pels are the same shape within both the
        // source and destination rectangles. 
        //-------------------------------------------------------------------
        private static Rectangle LetterBoxRect(Rectangle rcSrc, Rectangle rcDst)
        {
            int iDstLBWidth;
            int iDstLBHeight;

            if (Kernal32.MulDiv(rcSrc.Width, rcDst.Height, rcSrc.Height) <= rcDst.Width)
            {
                // Column letter boxing ("pillar box")
                iDstLBWidth = Kernal32.MulDiv(rcDst.Height, rcSrc.Width, rcSrc.Height);
                iDstLBHeight = rcDst.Height;
            }
            else
            {
                // Row letter boxing.
                iDstLBWidth = rcDst.Width;
                iDstLBHeight = Kernal32.MulDiv(rcDst.Width, rcSrc.Height, rcSrc.Width);
            }

            // Create a centered rectangle within the current destination rect
            int left = rcDst.Left + ((rcDst.Width - iDstLBWidth) / 2);
            int top = rcDst.Top + ((rcDst.Height - iDstLBHeight) / 2);

            Rectangle rc = new Rectangle(left, top, iDstLBWidth, iDstLBHeight);

            return rc;
        }

        //-----------------------------------------------------------------------------
        // CorrectAspectRatio
        //
        // Converts a rectangle from the source's pixel aspect ratio (PAR) to 1:1 PAR.
        // Returns the corrected rectangle.
        //
        // For example, a 720 x 486 rect with a PAR of 9:10, when converted to 1x1 PAR,
        // is stretched to 720 x 540.
        //-----------------------------------------------------------------------------
        private static Rectangle CorrectAspectRatio(Rectangle src, MFRatio srcPAR)
        {
            // Start with a rectangle the same size as src, but offset to the origin (0,0).
            Rectangle rc = new Rectangle(0, 0, src.Right - src.Left, src.Bottom - src.Top);
            int rcNewWidth = rc.Right;
            int rcNewHeight = rc.Bottom;

            if ((srcPAR.Numerator != 1) || (srcPAR.Denominator != 1))
            {
                // Correct for the source's PAR.
                if (srcPAR.Numerator > srcPAR.Denominator)
                {
                    // The source has "wide" pixels, so stretch the width.
                    rcNewWidth = Kernal32.MulDiv(rc.Right, srcPAR.Numerator, srcPAR.Denominator);
                }
                else if (srcPAR.Numerator < srcPAR.Denominator)
                {
                    // The source has "tall" pixels, so stretch the height.
                    rcNewHeight = Kernal32.MulDiv(rc.Bottom, srcPAR.Denominator, srcPAR.Numerator);
                }
                // else: PAR is 1:1, which is a no-op.
            }

            rc = new Rectangle(0, 0, rcNewWidth, rcNewHeight);
            return rc;
        }

        public static Rectangle GetClientRect(IntPtr hWnd)
        {
            Rectangle result = new Rectangle();
            Win32.GetClientRect(hWnd, out result);
            return result;
        }

        #endregion
    }
}
