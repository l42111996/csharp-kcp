using DotNetty.Buffers;

namespace fec
{
    public class InputOutputByteBufTableCodingLoop : ByteBufCodingLoopBase
    {
        public void codeSomeShards(byte[][] matrixRows, IByteBuffer[] inputs, int inputCount, IByteBuffer[] outputs,
            int outputCount, int offset, int byteCount)
        {
            byte[][] table = Galois.MULTIPLICATION_TABLE;

            {
                int iInput = 0;
                IByteBuffer inputShard = inputs[iInput];
                for (int iOutput = 0; iOutput < outputCount; iOutput++)
                {
                    IByteBuffer outputShard = outputs[iOutput];
                    byte[] matrixRow = matrixRows[iOutput];
                    byte[] multTableRow = table[matrixRow[iInput] & 0xFF];
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        outputShard.SetByte(iByte, multTableRow[inputShard.GetByte(iByte) & 0xFF]);
                        //outputShard[iByte] = multTableRow[inputShard[iByte] & 0xFF];
                    }
                }
            }

            for (int iInput = 1; iInput < inputCount; iInput++)
            {
                IByteBuffer inputShard = inputs[iInput];
                for (int iOutput = 0; iOutput < outputCount; iOutput++)
                {
                    IByteBuffer outputShard = outputs[iOutput];
                    byte[] matrixRow = matrixRows[iOutput];
                    byte[] multTableRow = table[matrixRow[iInput] & 0xFF];
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        byte temp = outputShard.GetByte(iByte);
                        temp ^= multTableRow[inputShard.GetByte(iByte) & 0xFF];
                        outputShard.SetByte(iByte, temp);
                        //outputShard[iByte] ^= multTableRow[inputShard[iByte] & 0xFF];
                    }
                }
            }
        }


        public bool checkSomeShards(
            byte[][] matrixRows,
            IByteBuffer[] inputs, int inputCount,
            byte[][] toCheck, int checkCount,
            int offset, int byteCount,
            byte[] tempBuffer)
        {
            if (tempBuffer == null)
            {
                return base.checkSomeShards(matrixRows, inputs, inputCount, toCheck, checkCount, offset, byteCount,
                    null);
            }

            // This is actually the code from OutputInputByteTableCodingLoop.
            // Using the loops from this class would require multiple temp
            // buffers.

            byte[][] table = Galois.MULTIPLICATION_TABLE;
            for (int iOutput = 0; iOutput < checkCount; iOutput++)
            {
                byte[] outputShard = toCheck[iOutput];
                byte[] matrixRow = matrixRows[iOutput];
                {
                    int iInput = 0;
                    IByteBuffer inputShard = inputs[iInput];
                    byte[] multTableRow = table[matrixRow[iInput] & 0xFF];
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        tempBuffer[iByte] = multTableRow[inputShard.GetByte(iByte) & 0xFF];
                    }
                }
                for (int iInput = 1; iInput < inputCount; iInput++)
                {
                    IByteBuffer inputShard = inputs[iInput];
                    byte[] multTableRow = table[matrixRow[iInput] & 0xFF];
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        tempBuffer[iByte] ^= multTableRow[inputShard.GetByte(iByte) & 0xFF];
                    }
                }

                for (int iByte = offset; iByte < offset + byteCount; iByte++)
                {
                    if (tempBuffer[iByte] != outputShard[iByte])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}