using System;

namespace D3Q.WavFile
{
    /// <summary>
    /// D3Q: Reader to simplify reading frames
    /// </summary>
    public class FrameReader
    {
        int channelCount;
        int bitsPerSample;
        int bytesInFrame;
        int bytesInValue;
        int maxInt;
        public int FrameCount;
        byte[] dataBytes;
        public int CurrentFrame;

        public FrameReader(DataSubChunk dataChunk)
        {
            channelCount = dataChunk.ChannelCount;
            bitsPerSample = dataChunk.BitsPerSample;
            maxInt = 0x7F; if (bitsPerSample == 16) maxInt = 0x7FFF; else if (bitsPerSample == 24) maxInt = 0x7FFFFF;
            bytesInValue = bitsPerSample / 8;
            bytesInFrame = channelCount * bytesInValue;
            dataBytes = dataChunk.DataBytes;
            FrameCount = dataBytes.Length / bytesInFrame;
            CurrentFrame = 0;
        }

        public void Start(int frameNumber)
        {
            CurrentFrame = frameNumber;
        }

        /// <summary>
        /// D3Q: Converts bytes to float and adjusts currentFrame for the next read
        /// </summary>
        /// <returns>the float array of the next frame, one float fro each channel</returns>
        public float[] ReadNext()
        {
            int currentByte = CurrentFrame * bytesInFrame;

            float[] res = new float[channelCount];

            for (int i = 0; i < channelCount; i++)
            {
                if (bitsPerSample == 16)
                {
                    
                    res[i] = (float)BitConverter.ToInt16(dataBytes, currentByte)/maxInt ; 
                }
                else if (bitsPerSample == 24)
                {
                    // ToInt32 has no count option, so...
                    byte[] byte4 = new byte[4];
                    for (int j = 0; j < 3; j++) byte4[j] = dataBytes[currentByte + j]; byte4[3] = 0;
                    res[i] = (float)BitConverter.ToInt32(byte4, 0)/maxInt;
                }
                else if (bitsPerSample == 32)
                {
                    res[i] = BitConverter.ToSingle(dataBytes, currentByte);
                }

                currentByte += bytesInValue;
            }

            CurrentFrame += 1;
            return res;
        }

        /// <summary>
        /// D3Q: Copies the frameRequested frames after currentFrame. Current is updated.
        /// The result is in byte[], so no conversion to floats
        /// </summary>
        /// <param name="framesRequested"></param>
        /// <param name="framesCopied">When reaching the end of frames this will be less than framesRequested</param>
        /// <returns>buffer with the copied bytes of the requested frames</returns>
        public byte[] ReadNextFramesCopied(int frameRequested, out int framesCopied)
        {
            framesCopied = frameRequested;
            if (CurrentFrame + frameRequested > FrameCount) framesCopied = FrameCount - CurrentFrame;
            
            int byteCount = framesCopied * bytesInFrame;
            byte[] buffer = new byte[byteCount];

            int currentByte = CurrentFrame * bytesInFrame;
            Array.Copy(dataBytes, currentByte, buffer, 0, byteCount);
            CurrentFrame += frameRequested;
            return buffer;
        }

        /// <summary>
        /// D3Q: gets the frameRequested frames after currentFrame. Current is updated.
        /// The result is in byte[], so no conversion to floats
        /// </summary>
        /// <param name="frameRequested"></param>
        /// <param name="framesCopied">When reaching the end of frames this will be less than framesRequested</param>
        /// <param name="firstByte">start byte for the frame series</param>
        /// <param name="byteCount">count of bytes in the frame series</param>
        /// <returns>source buffer in DataChunk holding the series of frames</returns>
        public byte[] ReadNextFrames(int frameRequested, out int framesCopied, out int firstByte, out int byteCount)
        {
            framesCopied = frameRequested;
            if (CurrentFrame + frameRequested > FrameCount) framesCopied = FrameCount - CurrentFrame;

            firstByte = CurrentFrame * bytesInFrame;
            byteCount = framesCopied * bytesInFrame;

            CurrentFrame += frameRequested;
            return dataBytes;
        }

        //    /// <summary>
        //    /// D3Q: gets all the frames from the beginning. Current is updated!
        //    /// The result is as byte[], so no conversion to floats
        //    /// </summary>
        //    /// <param name="frameRequested"></param>
        //    /// <param name="framesCopied">When reaching the end of frames this will be less than framesRequested</param>
        //    /// <param name="firstByte">start byte for the frame series</param>
        //    /// <param name="byteCount">count of bytes in the frame series</param>
        //    /// <returns>source buffer in DataChunk holding the series of frames</returns>
        //    public byte[] ReadAllFrames(out int byteCount)
        //    {
        //        byteCount = this.FrameCount * bytesInFrame;

        //        currentFrame += FrameCount;
        //        return dataBytes;
        //    }

    }
}

