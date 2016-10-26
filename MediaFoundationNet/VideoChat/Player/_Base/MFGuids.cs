using System;

namespace VideoPlayer
{
    public static class MFGuids
    {
        public static Guid MF_MT_MAJOR_TYPE = Guid.Parse("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        public static Guid MF_MT_SUBTYPE = Guid.Parse("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"); 

        public static Guid MF_MT_FRAME_SIZE = new Guid(0x1652c33d, 0xd6b2, 0x4012, 0xb8, 0x34, 0x72, 0x03, 0x08, 0x49, 0xa3, 0x7d);
        public static Guid MF_MT_FRAME_RATE = new Guid(0xc459a2e8, 0x3d2c, 0x4e44, 0xb1, 0x32, 0xfe, 0xe5, 0x15, 0x6c, 0x7b, 0xb0);
        public static Guid MF_MT_PIXEL_ASPECT_RATIO = new Guid(0xc6376a1e, 0x8d0a, 0x4027, 0xbe, 0x45, 0x6d, 0x9a, 0x0a, 0xd3, 0x9b, 0xb6);
        public static Guid MF_MT_INTERLACE_MODE = new Guid(0xe2724bb8, 0xe676, 0x4806, 0xb4, 0xb2, 0xa8, 0xd6, 0xef, 0xb4, 0x4c, 0xcd);

        public static Guid CLSID_CMSH264DecoderMFT = new Guid("62CE7E72-4C71-4d20-B15D-452831A87D9D");
    }
}
