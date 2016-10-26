using System;
using System.Runtime.InteropServices;
using System.Drawing;

using MediaFoundation;
using MediaFoundation.Misc;

using SlimDX.Direct3D9;
using SlimDX;
using VideoPlayer.Utils;

namespace VideoPlayer.Render
{
    struct YUYV
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
    struct RGBQUAD
    {
        public byte B;
        public byte G;
        public byte R;
        public byte A;
    }

    public class DrawDevice : COMBase
    {
        private const int NUM_BACK_BUFFERS = 2;

        private IntPtr _videoHwnd;
        private Device _device;
        private SwapChain _swapChain;

        private PresentParameters[] _d3dpp;

        // Format information 
        private int _width;
        private int _height;
        private int _defaultStride;
        private MFRatio _pixelAR;
        // Destination rectangle
        private Rectangle _destRect;

        public DrawDevice()
        {
            _videoHwnd = IntPtr.Zero;
            _device = null;
            _swapChain = null;

            _d3dpp = null;

            _width = 0;
            _height = 0;
            _defaultStride = 0;
            _pixelAR.Denominator = _pixelAR.Numerator = 1;
            _destRect = Rectangle.Empty;
        }

        private HResult TestCooperativeLevel()
        {
            if (_device == null)
            {
                return HResult.E_FAIL;
            }

            HResult hr = HResult.S_OK;

            // Check the current status of D3D9 device.
            SlimDX.Result r = _device.TestCooperativeLevel();

            hr = (HResult)r.Code;

            return hr;
        }

        private HResult CreateSwapChains()
        {
            HResult hr = HResult.S_OK;

            PresentParameters pp = new PresentParameters();

            if (_swapChain != null)
            {
                _swapChain.Dispose();
                _swapChain = null;
            }

            pp.EnableAutoDepthStencil = false;
            pp.BackBufferWidth = _width;
            pp.BackBufferHeight = _height;
            pp.Windowed = true;
            pp.SwapEffect = SwapEffect.Flip;
            pp.DeviceWindowHandle = _videoHwnd;
            pp.BackBufferFormat = Format.X8R8G8B8;
            pp.PresentFlags = PresentFlags.DeviceClip | PresentFlags.LockableBackBuffer;
            pp.PresentationInterval = PresentInterval.Immediate;
            pp.BackBufferCount = NUM_BACK_BUFFERS;

            _swapChain = new SwapChain(_device, pp);

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
            Rectangle rcSrc = new Rectangle(0, 0, _width, _height);
            Rectangle rcClient;
            WinExtern.GetClientRect(_videoHwnd, out rcClient);
            Rectangle rectanClient = new Rectangle(rcClient.Left, rcClient.Top, rcClient.Right - rcClient.Left, rcClient.Bottom - rcClient.Top);

            rcSrc = CorrectAspectRatio(rcSrc, _pixelAR);

            _destRect = LetterBoxRect(rcSrc, rectanClient);
        }

        #region Public Methods

        public HResult CreateDevice(IntPtr videoHwnd)
        {
            if (_device != null)
            {
                return HResult.S_OK;
            }

            PresentParameters[] pp = new PresentParameters[1];

            pp[0] = new PresentParameters();
            pp[0].BackBufferFormat = Format.X8R8G8B8;
            pp[0].SwapEffect = SwapEffect.Copy;
            pp[0].PresentationInterval = PresentInterval.Immediate;
            pp[0].Windowed = true;
            pp[0].DeviceWindowHandle = videoHwnd;
            pp[0].BackBufferHeight = 0;
            pp[0].BackBufferWidth = 0;
            pp[0].EnableAutoDepthStencil = false;

            using (Direct3D d = new Direct3D())
            {
                _device = new Device(d, 0, DeviceType.Hardware, videoHwnd, CreateFlags.HardwareVertexProcessing | CreateFlags.FpuPreserve | CreateFlags.Multithreaded, pp);
            }

            _videoHwnd = videoHwnd;
            _d3dpp = pp;

            return HResult.S_OK;
        }

        public HResult ResetDevice()
        {
            HResult hr = HResult.S_OK;

            if (_device != null)
            {
                PresentParameters[] d3dpp = (PresentParameters[])_d3dpp.Clone();

                try
                {
                    if (_swapChain != null)
                    {
                        _swapChain.Dispose();
                        _swapChain = null;
                    }
                    d3dpp[0].BackBufferHeight = 0;
                    d3dpp[0].BackBufferWidth = 0;
                    Result r = _device.Reset(d3dpp);

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

            if (_device == null)
            {
                hr = CreateDevice(_videoHwnd);

                if (Failed(hr))
                {
                    return hr;
                }
            }

            if ((_swapChain == null))
            {
                hr = CreateSwapChains();
                if (Failed(hr)) { return hr; }

                UpdateDestinationRect();
            }

            return hr;
        }

        public void DestroyDevice()
        {
            if (_swapChain != null)
            {
                _swapChain.Dispose();
                _swapChain = null;
            }
            if (_device != null)
            {
                _device.Dispose();
                _device = null;
            }
        }

        public HResult Initialize(int videoWidth, int videoHeight, MFRatio videoRatio)
        {
            HResult hr = HResult.S_OK;

            _width = videoWidth;
            _height = videoHeight;
            _pixelAR = videoRatio;

            FourCC f = new FourCC(MFMediaType.YUY2);

            hr = MFExtern.MFGetStrideForBitmapInfoHeader(f.ToInt32(), videoWidth, out _defaultStride);

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

            if (_device == null || _swapChain == null)
            {
                return HResult.S_OK;
            }

            using (VideoBufferLock locker = new VideoBufferLock(pCaptureDeviceBuffer))
            {
                hr = TestCooperativeLevel();
                if (Failed(hr)) { goto done; }

                // Lock the video buffer. This method returns a pointer to the first scan
                // line in the image, and the stride in bytes.
                hr = locker.LockBuffer(_defaultStride, _height, out pbScanline0, out lStride);
                if (Failed(hr)) { goto done; }

                // Get the swap-chain surface.
                pSurf = _swapChain.GetBackBuffer(0);

                // Lock the swap-chain surface and get Graphic stream object.
                DataRectangle dr = pSurf.LockRectangle(LockFlags.NoSystemLock);

                try
                {
                    using (dr.Data)
                    {
                        ApplyToD3d(dr.Data.DataPointer, dr.Pitch, pbScanline0, lStride, _width, _height);
                    }
                }
                finally
                {
                    res = pSurf.UnlockRectangle();
                    MFError.ThrowExceptionForHR(res.Code);
                }
            }

            // Color fill the back buffer.
            pBB = _device.GetBackBuffer(0, 0);

            _device.ColorFill(pBB, Color.FromArgb(0, 0, 0x80));

            // Blit the frame.
            Rectangle r = new Rectangle(0, 0, _width, _height);

            res = _device.StretchRectangle(pSurf, r, pBB, _destRect, TextureFilter.Linear);
            hr = (HResult)res.Code;

            if (res.IsSuccess)
            {
                res = _device.Present();
                hr = (HResult)res.Code;
            }

            done:
            SafeRelease(pBB);
            SafeRelease(pSurf);

            return hr;
        }

        #endregion

        private byte clip(int clr)
        {
            return (byte)(clr < 0 ? 0 : (clr > 255 ? 255 : clr));
        }

        private RGBQUAD ConvertYCrCbToRGB(byte y, byte cr, byte cb)
        {
            RGBQUAD rgbq = new RGBQUAD();

            int c = y - 16;
            int d = cb - 128;
            int e = cr - 128;

            rgbq.R = clip((298 * c + 409 * e + 128) >> 8);
            rgbq.G = clip((298 * c - 100 * d - 208 * e + 128) >> 8);
            rgbq.B = clip((298 * c + 516 * d + 128) >> 8);

            return rgbq;
        }

        /// <summary>
        /// Convert the frame. This also copies it to the Direct3D surface.
        /// </summary> 
        unsafe private void ApplyToD3d(IntPtr pDest, int lDestStride, IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels)
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
        private Rectangle LetterBoxRect(Rectangle rcSrc, Rectangle rcDst)
        {
            int iDstLBWidth;
            int iDstLBHeight;

            if (WinExtern.MulDiv(rcSrc.Width, rcDst.Height, rcSrc.Height) <= rcDst.Width)
            {
                // Column letter boxing ("pillar box")
                iDstLBWidth = WinExtern.MulDiv(rcDst.Height, rcSrc.Width, rcSrc.Height);
                iDstLBHeight = rcDst.Height;
            }
            else
            {
                // Row letter boxing.
                iDstLBWidth = rcDst.Width;
                iDstLBHeight = WinExtern.MulDiv(rcDst.Width, rcSrc.Height, rcSrc.Width);
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
        private Rectangle CorrectAspectRatio(Rectangle src, MFRatio srcPAR)
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
                    rcNewWidth = WinExtern.MulDiv(rc.Right, srcPAR.Numerator, srcPAR.Denominator);
                }
                else if (srcPAR.Numerator < srcPAR.Denominator)
                {
                    // The source has "tall" pixels, so stretch the height.
                    rcNewHeight = WinExtern.MulDiv(rc.Bottom, srcPAR.Denominator, srcPAR.Numerator);
                }
                // else: PAR is 1:1, which is a no-op.
            }

            rc = new Rectangle(0, 0, rcNewWidth, rcNewHeight);
            return rc;
        }
    }
}
