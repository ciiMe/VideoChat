using MediaFoundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaFoundation.Misc;

namespace VideoPlayer.MediaSource
{
    public class CBaseAttributes : IMFAttributes
    {
        public HResult Compare(IMFAttributes pTheirs, MFAttributesMatchType MatchType, out bool pbResult)
        {
            throw new NotImplementedException();
        }

        public HResult CompareItem(Guid guidKey, ConstPropVariant Value, out bool pbResult)
        {
            throw new NotImplementedException();
        }

        public HResult CopyAllItems(IMFAttributes pDest)
        {
            throw new NotImplementedException();
        }

        public HResult DeleteAllItems()
        {
            throw new NotImplementedException();
        }

        public HResult DeleteItem(Guid guidKey)
        {
            throw new NotImplementedException();
        }

        public HResult GetAllocatedBlob(Guid guidKey, out IntPtr ip, out int pcbSize)
        {
            throw new NotImplementedException();
        }

        public HResult GetAllocatedString(Guid guidKey, out string ppwszValue, out int pcchLength)
        {
            throw new NotImplementedException();
        }

        public HResult GetBlob(Guid guidKey, byte[] pBuf, int cbBufSize, out int pcbBlobSize)
        {
            throw new NotImplementedException();
        }

        public HResult GetBlobSize(Guid guidKey, out int pcbBlobSize)
        {
            throw new NotImplementedException();
        }

        public HResult GetCount(out int pcItems)
        {
            throw new NotImplementedException();
        }

        public HResult GetDouble(Guid guidKey, out double pfValue)
        {
            throw new NotImplementedException();
        }

        public HResult GetGUID(Guid guidKey, out Guid pguidValue)
        {
            throw new NotImplementedException();
        }

        public HResult GetItem(Guid guidKey, PropVariant pValue)
        {
            throw new NotImplementedException();
        }

        public HResult GetItemByIndex(int unIndex, out Guid pguidKey, PropVariant pValue)
        {
            throw new NotImplementedException();
        }

        public HResult GetItemType(Guid guidKey, out MFAttributeType pType)
        {
            throw new NotImplementedException();
        }

        public HResult GetString(Guid guidKey, StringBuilder pwszValue, int cchBufSize, out int pcchLength)
        {
            throw new NotImplementedException();
        }

        public HResult GetStringLength(Guid guidKey, out int pcchLength)
        {
            throw new NotImplementedException();
        }

        public HResult GetUINT32(Guid guidKey, out int punValue)
        {
            throw new NotImplementedException();
        }

        public HResult GetUINT64(Guid guidKey, out long punValue)
        {
            throw new NotImplementedException();
        }

        public HResult GetUnknown(Guid guidKey, Guid riid, out object ppv)
        {
            throw new NotImplementedException();
        }

        public HResult LockStore()
        {
            throw new NotImplementedException();
        }

        public HResult SetBlob(Guid guidKey, byte[] pBuf, int cbBufSize)
        {
            throw new NotImplementedException();
        }

        public HResult SetDouble(Guid guidKey, double fValue)
        {
            throw new NotImplementedException();
        }

        public HResult SetGUID(Guid guidKey, Guid guidValue)
        {
            throw new NotImplementedException();
        }

        public HResult SetItem(Guid guidKey, ConstPropVariant Value)
        {
            throw new NotImplementedException();
        }

        public HResult SetString(Guid guidKey, string wszValue)
        {
            throw new NotImplementedException();
        }

        public HResult SetUINT32(Guid guidKey, int unValue)
        {
            throw new NotImplementedException();
        }

        public HResult SetUINT64(Guid guidKey, long unValue)
        {
            throw new NotImplementedException();
        }

        public HResult SetUnknown(Guid guidKey, object pUnknown)
        {
            throw new NotImplementedException();
        }

        public HResult UnlockStore()
        {
            throw new NotImplementedException();
        }
    }
}
