using System;

namespace D3Q.WavFile
{
    public class FrameReader
    {
        int channelCount;
        int bitsPerSample;
        int bytesInFrame;
        int bytesInValue;
        int maxInt;
        public int FrameCount;
        byte[] dataBytes;
        int currentFrame;

        public FrameReader(DataSubChunk dataChunk)
        {
            channelCount = dataChunk.ChannelCount;
            bitsPerSample = dataChunk.BitsPerSample;
            maxInt = 0x7F; if (bitsPerSample == 16) maxInt = 0x7FFF; else if (bitsPerSample == 24) maxInt = 0x7FFFFF;
            bytesInValue = bitsPerSample / 8;
            bytesInFrame = channelCount * bytesInValue;
            dataBytes = dataChunk.DataBytes;
            FrameCount = dataBytes.Length / bytesInFrame;
            currentFrame = 0;
        }

        public void Start(int frameNumber)
        {
            currentFrame = frameNumber;
        }

        /// <summary>
        /// D3Q: Converts bytes to float and adjusts currentFrame for the next read
        /// </summary>
        /// <returns>the float array of the next frame, one float fro each channel</returns>
        public float[] ReadNext()
        {
            int currentByte = currentFrame * bytesInFrame;

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

            currentFrame += 1;
            return res;
        }

        /// <summary>
        /// D3Q: Copies frameCount frames into the offered Buffer. Current is updated.
        /// </summary>
        /// <param name="Buffer">an already created byte buffer</param>
        /// <param name="frameCount">count of frames asked for</param>
        /// <returns>number of frames actually copied</returns>
        public int ReadNextFramesCopied(ref byte[] Buffer, int frameCount)
        {
            if (currentFrame + frameCount > FrameCount) frameCount = FrameCount - currentFrame;
            int byteCount = frameCount * bytesInFrame;
            int currentByte = currentFrame * bytesInFrame;
            Array.Copy(dataBytes, currentByte, Buffer, 0, byteCount);
            currentFrame += frameCount;
            return frameCount;
        }
    }
}

