using System;
using System.Text;
using DotNetty.Buffers;

namespace dotNetty_kcp
{
    /// <summary>
    /// Computes a CRC32 checksum.
    /// </summary>
    /// <remarks>Based on <see cref="http://sanity-free.org/12/crc32_implementation_in_csharp.html"/></remarks>
    public static class Crc32
    {
        readonly static uint[] Table = CreateTable();

        static Crc32()
        {
        }

//        /// <summary>
//        /// Compute the checksum of a UTF8 text.
//        /// </summary>
//        /// <param name="text">Text to calculate</param>
//        /// <returns>Checksum</returns>
//        public static int ComputeChecksum(string text)
//        {
//            return ComputeChecksum(text, Encoding.UTF8);
//        }
//
//        /// <summary>
//        /// Compute the checksum of a text using a specific encoding.
//        /// </summary>
//        /// <param name="text">Text to calculate</param>
//        /// <param name="encoding">Text encoding</param>
//        /// <returns>Checksum</returns>
//        public static int ComputeChecksum(string text, Encoding encoding)
//        {
//            if (string.IsNullOrEmpty(text)) return 0;
//            byte[] bytes = encoding.GetBytes(text);
//            return ComputeChecksum(bytes);
//        }

        /// <summary>
        /// Compute the checksum of a binary buffer.
        /// </summary>
        /// <param name="bytes">Buffer to calculate</param>
        /// <returns></returns>
        public static int ComputeChecksum(sbyte[] bytes)
        {
            uint crc = 0xffffffff;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte index = (byte) (((crc) & 0xff) ^ bytes[i]);
                crc = (crc >> 8) ^ Table[index];
            }

            return unchecked((int) ~crc);
        }



        /// <summary>
        /// Compute the checksum of a binary buffer.
        /// </summary>
        /// <param name="bytes">Buffer to calculate</param>
        /// <returns></returns>
        public static uint ComputeChecksum(IByteBuffer byteBuffer,int offset,int lenth)
        {
            var crc = 0xffffffff;
            lenth += offset;
            for (var i = offset; i < lenth; i++)
            {
                var index = (byte) (((crc) & 0xff) ^ byteBuffer.GetByte(i));
                crc = (crc >> 8) ^ Table[index];
            }
            return ~crc;
        }

        static uint[] CreateTable()
        {
            const uint poly = 0xedb88320;
            var table = new uint[256];
            uint temp = 0;
            for (uint i = 0; i < table.Length; ++i)
            {
                temp = i;
                for (int j = 8; j > 0; --j)
                {
                    if ((temp & 1) == 1)
                    {
                        temp = (uint) ((temp >> 1) ^ poly);
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }

                table[i] = temp;
            }

            return table;
        }
    }
}