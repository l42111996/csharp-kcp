using DotNetty.Buffers;

namespace fec
{
    public interface ByteBufCodingLoop
    {
        /**
     * Multiplies a subset of rows from a coding matrix by a full set of
     * input shards to produce some output shards.
     *
     * @param matrixRows The rows from the matrix to use.
     * @param inputs An array of byte arrays, each of which is one input shard.
     *               The inputs array may have extra buffers after the ones
     *               that are used.  They will be ignored.  The number of
     *               inputs used is determined by the length of the
     *               each matrix row.
     * @param inputCount The number of input byte arrays.
     * @param outputs Byte arrays where the computed shards are stored.  The
     *                outputs array may also have extra, unused, elements
     *                at the end.  The number of outputs computed, and the
     *                number of matrix rows used, is determined by
     *                outputCount.
     * @param outputCount The number of outputs to compute.
     * @param offset The index in the inputs and output of the first byte
     *               to process.
     * @param byteCount The number of bytes to process.
     */
        void codeSomeShards(byte[][] matrixRows,
            IByteBuffer[] inputs,
            int inputCount,
            IByteBuffer[] outputs,
            int outputCount,
            int offset,
            int byteCount);

        /**
         * Multiplies a subset of rows from a coding matrix by a full set of
         * input shards to produce some output shards, and checks that the
         * the data is those shards matches what's expected.
         *
         * @param matrixRows The rows from the matrix to use.
         * @param inputs An array of byte arrays, each of which is one input shard.
         *               The inputs array may have extra buffers after the ones
         *               that are used.  They will be ignored.  The number of
         *               inputs used is determined by the length of the
         *               each matrix row.
         * @param inputCount THe number of input byte arrays.
         * @param toCheck Byte arrays where the computed shards are stored.  The
         *                outputs array may also have extra, unused, elements
         *                at the end.  The number of outputs computed, and the
         *                number of matrix rows used, is determined by
         *                outputCount.
         * @param checkCount The number of outputs to compute.
         * @param offset The index in the inputs and output of the first byte
         *               to process.
         * @param byteCount The number of bytes to process.
         * @param tempBuffer A place to store temporary results.  May be null.
         */
        bool checkSomeShards(byte[][] matrixRows,
            IByteBuffer[] inputs,
            int inputCount,
            byte[][] toCheck,
            int checkCount,
            int offset,
            int byteCount,
            byte[] tempBuffer);
    }
}