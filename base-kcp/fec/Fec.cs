namespace fec.fec
{
    public class Fec
    {
        public const int fecHeaderSize = 6,
            fecDataSize = 2,
            fecHeaderSizePlus2 = fecHeaderSize + fecDataSize, // plus 2B data size
            typeData = 0xf1,
            typeParity = 0xf2;
    }
}