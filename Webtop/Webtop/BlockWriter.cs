using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Gosub.Webtop
{
    /// <summary>
    /// Generate a bitmap block by block, copying source blocks to target blocks when necessary
    /// </summary>
    class BlockWriter
    {
        StringBuilder mSb = new StringBuilder();
        Bitmap32Bits mSource;
        Bitmap32Bits mTarget;
        int mBlockSize;
        int mSourceBlocksX;
        int mTargetBlocksX;
        Dictionary<int, int> mDuplicates;
        Dictionary<int, int> mDuplicatedSourceToTarget;

        int mSourceIndex;
        int mTargetIndex;

        int mSkipsInARow;
        int mSolidColor;
        int mSolidsInARow;
        int mCopiesInARow;
        int mDuplicateCursor;
        int mDuplicatesInARow;

        public BlockWriter(Bitmap32Bits source, 
                           Bitmap32Bits target, 
                           int blockSize, 
                           Dictionary<int, int> duplicates)
        {
            mSource = source;
            mTarget = target;
            mBlockSize = blockSize;

            // The duplicates map from source index to source index, 
            // but we want to map directly to target index.
            mDuplicates = duplicates;
            mDuplicatedSourceToTarget = new Dictionary<int, int>();
            foreach (var duplicate in mDuplicates)
                mDuplicatedSourceToTarget[duplicate.Value] = -1;

            // Number of blocks visible (even if partially visible)
            mSourceBlocksX = (source.Width + mBlockSize - 1) / mBlockSize;
            mTargetBlocksX = (target.Width + mBlockSize - 1) / mBlockSize;

            // Write bitmap header info - screen resolution and block size
            mSb.Append('X');
            mSb.Append(source.Width);
            mSb.Append('Y');
            mSb.Append(source.Height);
            mSb.Append('B');
            mSb.Append(mBlockSize);
        }

        public void Write(FrameCompressor.Block block)
        {
            int sourceX = (block.Index % mSourceBlocksX) * mBlockSize;
            int sourceY = (block.Index / mSourceBlocksX) * mBlockSize;

            if (block.Index != mSourceIndex)
            {
                // Skip clear blocks
                if (mSkipsInARow == 0)
                    Flush();
                mSkipsInARow += block.Index - mSourceIndex;
                mSourceIndex = block.Index;
            }
            if (block.Score.HasFlag(FrameCompressor.Score.Solid))
            {
                // Set solid color if different from previous color
                if (mSource[sourceX, sourceY] != mSolidColor)
                {
                    Flush();
                    mSolidColor = mSource[sourceX, sourceY];
                    mSb.Append('s');
                    mSb.Append(mSolidColor & 0xFFFFFF);
                }
                // Cache solid color
                if (mSolidsInARow == 0)
                    Flush();
                mSolidsInARow++;
                mSourceIndex++;
                return;
            }
            if (block.Score.HasFlag(FrameCompressor.Score.Duplicate))
            {
                // Set duplicate cursor position if different from previous location
                int duplicateCursor = mDuplicatedSourceToTarget[mDuplicates[block.Index]];
                if (duplicateCursor < 0)
                {
                    // This duplicate was not written to the target
                    return;
                }
                if (duplicateCursor != mDuplicateCursor)
                {
                    Flush();
                    mDuplicateCursor = duplicateCursor;
                    mSb.Append('d');
                    mSb.Append(mDuplicateCursor);
                }
                // Cache duplicate block
                if (mDuplicatesInARow == 0)
                    Flush();
                mDuplicatesInARow++;
                mSourceIndex++;
                return;
            }
            // Create duplicate source to target map as we go 
            if (mDuplicatedSourceToTarget.ContainsKey(mSourceIndex))
            {
                mDuplicatedSourceToTarget[mSourceIndex] = mTargetIndex;
            }
            // Cache copy to target
            if (mCopiesInARow == 0)
                Flush();
            mCopiesInARow++;
            mSourceIndex++;
            int targetX = (mTargetIndex % mTargetBlocksX) * mBlockSize;
            int targetY = (mTargetIndex / mTargetBlocksX) * mBlockSize;
            mTargetIndex++;


            // Copy source to target (source can be non block size multiple, but not target)
            mSource.Copy(sourceX, sourceY, Math.Min(mBlockSize, mSource.Width - sourceX),
                                            Math.Min(mBlockSize, mSource.Height - sourceY), mTarget, targetX, targetY);
        }

        void Flush()
        {
            if (mSkipsInARow != 0)
            {
                // Skips
                mSb.Append('K');
                if (mSkipsInARow != 1)
                    mSb.Append(mSkipsInARow);
                mSkipsInARow = 0;
            }
            if (mSolidsInARow != 0)
            {
                // Solid
                mSb.Append('S');
                if (mSolidsInARow != 1)
                    mSb.Append(mSolidsInARow);
                mSolidsInARow = 0;
            }
            if (mCopiesInARow != 0)
            {
                // Copy
                mSb.Append('C');
                if (mCopiesInARow != 1)
                    mSb.Append(mCopiesInARow);
                mCopiesInARow = 0;
            }
            if (mDuplicatesInARow != 0)
            {
                mSb.Append('D');
                if (mDuplicatesInARow != 1)
                    mSb.Append(mDuplicatesInARow);
                mDuplicatesInARow = 0;
            }
        }

        public string GetDrawString()
        {
            Flush();
            return mSb.ToString();
        }
    }
}
