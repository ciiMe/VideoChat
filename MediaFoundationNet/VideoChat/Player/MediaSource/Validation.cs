using MediaFoundation;
using System;

namespace VideoPlayer.MediaSource
{
    // Function validating just a type of the data. 
    //bool ValidateDataType<T>(PROPVARIANT pValue)
    //{
    //    return (pValue.vt == T);
    //}

    // Function validating a BLOB apart from the type it checks the size as well.
    //bool ValidateBlob(PROPVARIANT pValue)
    //{
    //    return true;// ValidateDataType<MFAttributeTypeMF_ATTRIBUTE_BLOB>(pValue) && pValue->caub.cElems < 128;
    //}

    // Description of an attribute for validation
    public struct AttributeValidationDescriptor
    {
        Guid guidKey;
        bool IsValid() { return true; }//(*IsValid)(PROPVARIANT* pValue);
    };

    // Description of an media type for validation
    public struct MediaTypeValidationDescriptor
    {
        Guid guidSubtype;
        bool fVideo;
        int cAttributes;
        AttributeValidationDescriptor attributes;
    };

    public static class Validation
    {
        // Used to validate media type after receiving it from the network.
        public static void ValidateInputMediaType(Guid guidMajorType, Guid guidSubtype, IMFMediaType pMediaType)
        {
            /*
            var MediaTypeValidationDescriptor*typeDescriptor = FindMediaTypeDescriptor(guidMajorType, guidSubtype);

            if (typeDescriptor == nullptr)
            {
                Throw(MF_E_INVALIDMEDIATYPE);
            }

            UINT32 cAttributes = 0;
            pMediaType->GetCount(&cAttributes);

            for (UINT32 nIndex = 0; nIndex < cAttributes; ++nIndex)
            {
                Exception ^ error = nullptr;
                GUID guidKey;
                PROPVARIANT val;
                PropVariantInit(&val);

                ThrowIfError(pMediaType->GetItemByIndex(nIndex, &guidKey, &val));
                try
                {
                    if (!IsAttributeValid(typeDescriptor, guidKey, &val))
                    {
                        ThrowIfError(MF_E_INVALIDMEDIATYPE);
                    }
                }
                catch (Exception ^ exc)
        {
                error = exc;
            }

            PropVariantClear(&val);
            if (error != nullptr)
            {
                throw error;
            }*/
        }
    }
}
