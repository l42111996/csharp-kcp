using System;
using DotNetty.Buffers;

namespace fec
{
    public static class FecExpansion
    {
        private static readonly InputOutputByteBufTableCodingLoop LOOP = new InputOutputByteBufTableCodingLoop();


        public static void encodeParity(this ReedSolomon reedSolomon, IByteBuffer[] shards, int offset, int byteCount)
        {
            // Check arguments.
            reedSolomon.checkBuffersAndSizes(shards, offset, byteCount);

            // Build the array of output buffers.
            IByteBuffer[] outputs = new IByteBuffer [reedSolomon.ParityShardCount];
            Array.Copy(shards, reedSolomon.DataShardCount, outputs, 0, reedSolomon.ParityShardCount);
//        System.arraycopy(shards, dataShardCount, outputs, 0, parityShardCount);

            // Do the coding.
            LOOP.codeSomeShards(
                reedSolomon.ParityRows,
                shards, reedSolomon.DataShardCount,
                outputs, reedSolomon.ParityShardCount,
                offset, byteCount);
        }


        /**
       * Checks the consistency of arguments passed to public methods.
       */
        private static void checkBuffersAndSizes(this ReedSolomon reedSolomon, IByteBuffer[] shards, int offset,
            int byteCount)
        {
            // The number of buffers should be equal to the number of
            // data shards plus the number of parity shards.
            if (shards.Length != reedSolomon.TotalShardCount)
            {
                throw new Exception("wrong number of shards: " + shards.Length);
            }

            // All of the shard buffers should be the same length.
            int shardLength = shards[0].ReadableBytes;
            for (int i = 1; i < shards.Length; i++)
            {
                if (shards[i].ReadableBytes != shardLength)
                {
                    throw new Exception("Shards are different sizes");
                }
            }

            // The offset and byteCount must be non-negative and fit in the buffers.
            if (offset < 0)
            {
                throw new Exception("offset is negative: " + offset);
            }

            if (byteCount < 0)
            {
                throw new Exception("byteCount is negative: " + byteCount);
            }

            if (shardLength < offset + byteCount)
            {
                throw new Exception("buffers to small: " + byteCount + offset);
            }
        }


        public static void decodeMissing(this ReedSolomon reedSolomon, IByteBuffer[] shards,
            bool[] shardPresent,
            int offset,
            int byteCount)
        {
            // Check arguments.
            reedSolomon.checkBuffersAndSizes(shards, offset, byteCount);

            // Quick check: are all of the shards present?  If so, there's
            // nothing to do.
            int numberPresent = 0;
            for (int i = 0; i < reedSolomon.TotalShardCount; i++)
            {
                if (shardPresent[i])
                {
                    numberPresent += 1;
                }
            }

            if (numberPresent == reedSolomon.TotalShardCount)
            {
                // Cool.  All of the shards data data.  We don't
                // need to do anything.
                return;
            }

            // More complete sanity check
            if (numberPresent < reedSolomon.DataShardCount)
            {
                throw new Exception("Not enough shards present");
            }

            // Pull out the rows of the matrix that correspond to the
            // shards that we have and build a square matrix.  This
            // matrix could be used to generate the shards that we have
            // from the original data.
            //
            // Also, pull out an array holding just the shards that
            // correspond to the rows of the submatrix.  These shards
            // will be the input to the decoding process that re-creates
            // the missing data shards.
            Matrix subMatrix = new Matrix(reedSolomon.DataShardCount, reedSolomon.DataShardCount);
            IByteBuffer[] subShards = new IByteBuffer[reedSolomon.DataShardCount];
            {
                int subMatrixRow = 0;
                for (int matrixRow = 0;
                    matrixRow < reedSolomon.TotalShardCount && subMatrixRow < reedSolomon.DataShardCount;
                    matrixRow++)
                {
                    if (shardPresent[matrixRow])
                    {
                        for (int c = 0; c < reedSolomon.DataShardCount; c++)
                        {
                            subMatrix.set(subMatrixRow, c, reedSolomon.Matrix.get(matrixRow, c));
                        }

                        subShards[subMatrixRow] = shards[matrixRow];
                        subMatrixRow += 1;
                    }
                }
            }

            // Invert the matrix, so we can go from the encoded shards
            // back to the original data.  Then pull out the row that
            // generates the shard that we want to decode.  Note that
            // since this matrix maps back to the orginal data, it can
            // be used to create a data shard, but not a parity shard.
            Matrix dataDecodeMatrix = subMatrix.invert();

            // Re-create any data shards that were missing.
            //
            // The input to the coding is all of the shards we actually
            // have, and the output is the missing data shards.  The computation
            // is done using the special decode matrix we just built.
            IByteBuffer[] outputs = new IByteBuffer[reedSolomon.ParityShardCount];
            byte[][] matrixRows = new byte [reedSolomon.ParityShardCount][];
            int outputCount = 0;
            for (int iShard = 0; iShard < reedSolomon.DataShardCount; iShard++)
            {
                if (!shardPresent[iShard])
                {
                    outputs[outputCount] = shards[iShard];
                    matrixRows[outputCount] = dataDecodeMatrix.getRow(iShard);
                    outputCount += 1;
                }
            }

            LOOP.codeSomeShards(
                matrixRows,
                subShards, reedSolomon.DataShardCount,
                outputs, outputCount,
                offset, byteCount);

            // Now that we have all of the data shards intact, we can
            // compute any of the parity that is missing.
            //
            // The input to the coding is ALL of the data shards, including
            // any that we just calculated.  The output is whichever of the
            // data shards were missing.
            outputCount = 0;
            for (int iShard = reedSolomon.DataShardCount; iShard < reedSolomon.TotalShardCount; iShard++)
            {
                if (!shardPresent[iShard])
                {
                    outputs[outputCount] = shards[iShard];
                    matrixRows[outputCount] = reedSolomon.ParityRows[iShard - reedSolomon.DataShardCount];
                    outputCount += 1;
                }
            }

            LOOP.codeSomeShards(
                matrixRows,
                shards, reedSolomon.DataShardCount,
                outputs, outputCount,
                offset, byteCount);
        }
    }
}