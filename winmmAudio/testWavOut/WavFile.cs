using System;
using System.IO;
using System.Text;

namespace WavFiles
{
    //D3Q: version 0.21;
    //D3Q: 22 maart 2021


    /// <summary>
    ///  A Frame is created by a FrameReader positioned on a WavFile.
    ///  A frame has one value for each channel (e.g. 2 for a stereo wav file).
    ///  The values created by the FrameReader can be 8, 16 or 24 bits.
    /// </summary>
    internal class Frame
    {
        public int[] values;
        public double time;

        public Frame(int n, double time)
        {
            values = new int[n];
            this.time = time;
        }

        public byte[] valuesToBytes(int bits)
        {
            int l = 0;
            if (bits == 16) l = 2;
            else if (bits == 24) l = 3;
            else if (bits == 32) l = 4;
            byte[] bytes = new byte[l * values.Length];

            int pos = 0;
            for (int i = 0; i < values.Length; i++)
            {
                byte[] src = BitConverter.GetBytes(values[i]);
                for (int j = 0; j < l; j++) bytes[pos + j] = src[j];
                pos += l;
            }
            return bytes;
        }
    }

    /// <summary>
    /// A FrameReader knows about the format of the WavFile. 
    /// It keeps a byte pointer on the wavFile in the field currentByte.
    /// The frames start at nulByte.
    /// </summary>
    internal class FrameReader
    {
        private WavFile wavFile;
        /// <summary>
        /// wavBytes are the bytes read using wavFile
        /// </summary>
        public byte[] wavBytes;
        /// <summary>
        /// index of first audio byte in wavBytes. wavBytes starts with headers.
        /// </summary>
        private int nulByte;
        /// <summary>
        /// D3Q: equals the field fmtBlockAlign in the format-header. method bytesInFrame() must give the same result. 
        /// </summary>
        private int nextBytes;
        /// <summary>
        /// number of frames per second
        /// </summary>
        public int sampleRate;
        /// <summary>
        /// at currentByte the next read will be done. In each read currentByte is updated.
        /// </summary>
        public int currentByte;
        /// <summary>
        /// D3Q: channelCount =1 for mono, =2 for stereo
        /// </summary>
        public int channelCount;
        /// <summary>
        /// bitsSample: 8bit, 16bit or 24bits
        /// </summary>
        public int bitsSample;
        /// <summary>
        /// D3Q: max value dependent on 8bit, 16bit or 24bit values
        /// </summary>
        public int maxInt;
        /// <summary>
        /// count of the frames in wavBytes
        /// </summary>
        public int frameCount;

        public FrameReader(WavFile wavFile)
        {
            this.wavFile = wavFile;
            wavBytes = wavFile.wavFileByteArray;
            channelCount = wavFile.fmtWavSubChunk.fmtNumChannels;
            nextBytes = wavFile.fmtWavSubChunk.fmtBlockAlign;
            bitsSample = wavFile.fmtWavSubChunk.fmtBitsPerSample;
            sampleRate = wavFile.fmtWavSubChunk.fmtSampleRate;
            nulByte = wavFile.dataWavSubChunk.nextIndex;
            maxInt = 0x7F; if (bitsSample == 16) maxInt = 0x7FFF; else if (bitsSample == 24) maxInt = 0x7FFFFF;
            frameCount = (wavFile.wavFileByteArray.Length - nulByte) / bytesInValue();
        }

        ///// <summary>
        ///// Sets the pointer to the requested position.
        ///// No check is done if this is a frame boundary.
        ///// </summary>
        ///// <param name="bytePosition"></param>
        //public void start(int bytePosition)
        //{
        //    currentByte = bytePosition;
        //}

        /// <summary>
        /// Sets byte pointer to the position of the frame to be read.
        /// The first frame has index = 0
        /// </summary>
        /// <param name="frame">index of the frame</param>
        public void startFrame(int frame)
        {
            currentByte = this.nulByte + frame * nextBytes;
        }

        /// <summary>
        /// Sets the byte pointer to the first position (frame number 0)
        /// </summary>
        public void reset()
        {
            currentByte = this.nulByte;
        }

        public int bytesInValue()
        {
            int biv = 0;
            if (bitsSample == 8) biv = 1;
            else if (bitsSample == 16) biv = 2;
            else if (bitsSample == 24) biv = 3;
            else if (bitsSample == 32) biv = 4;
            biv = biv * channelCount;
            return biv;
        }

        /// <summary>
        /// D3Q: Reads the value(s) of the next frame and adjusts the currentByte pointer.
        /// </summary>
        /// <returns></returns>
        public Frame read()
        {
            Frame frame = new Frame(channelCount, timeFrame());
            for (int i = 0; i < channelCount; i++)
            {
                if (bitsSample == 16) frame.values[i] = readInt16Bytes(wavBytes, currentByte + 2 * i);
                else if (bitsSample == 24) frame.values[i] = readInt24Bytes(wavBytes, currentByte + 3 * i);
            }

            currentByte += nextBytes;
            return frame;
        }

        private int readInt16Bytes(byte[] wavFileByteArray, int index)
        {
            byte[] bytes = new byte[2];
            bytes[0] = wavFileByteArray[index];
            bytes[1] = wavFileByteArray[index + 1];
            //if (BitConverter.IsLittleEndian) Array.Reverse(bytes);

            return BitConverter.ToInt16(bytes, 0);
        }

        private int readInt24Bytes(byte[] wavFileByteArray, int index)
        {
            byte[] bytes = new byte[4];

            bytes[0] = wavFileByteArray[index];
            bytes[1] = wavFileByteArray[index + 1];
            bytes[2] = wavFileByteArray[index + 2];
            bytes[3] = 0;
            //if (BitConverter.IsLittleEndian) Array.Reverse(bytes);

            int res24 = BitConverter.ToInt32(bytes, 0);
            if ((res24 & 0x00800000) > 0) res24 = (int)(res24 | (uint)0xFF000000);
            return res24;
        }

        /// <summary>
        /// D3Q: returns the time of the current frame.
        /// float changed to double. A time difference of 0.0002 seconds can produce clicks
        /// </summary>
        /// <returns></returns>
        double timeFrame()
        {
            return ((double)((currentByte - nulByte) / nextBytes)) / sampleRate;
            //return timeAt(currentByte);
        }

        // D3Q: changed to double. A time difference of 0.0002 seconds can produce clicks 
        public double timeAtFrame(int indexFrame)
        {
            return ((double)(indexFrame)) / sampleRate;
        }

        /// <summary>
        /// find the index of the first frame after the given time.
        ///  changed: from WavFile class: now in Frames, was in bytes, now in frames
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public int findFrameAfterTime(float time)
        {
            return (int)Math.Ceiling(time * sampleRate);
        }

        //public int findBytesAfterTime(float time)
        //{
        //    return nulByte+(int)Math.Ceiling(time * sampleRate) * nextBytes;
        //}
    }


    /// <summary>
    /// D3Q: header RiffWavChunck, first header
    /// Must start with bytes 'RIFF'
    /// </summary>
    internal class RiffWavChunk
    {
        /// <summary>
        /// format must contain the 4 bytes 'WAVE'
        /// </summary>
        public string format;
        /// <summary>
        /// count in bytes of the content that follows the header.
        /// </summary>
        public int chunkSize;
        /// <summary>
        /// nextIndex points to byteposition just after chunk. 
        /// not part of the file, only for internal use
        /// </summary>
        public int nextIndex;
    }

    /// <summary>
    /// The fmtSubChunk has the description of the format used.
    /// Must start with bytes 'fmt '.
    /// </summary>
    internal class FmtWavSubChunk
    {
        /// <summary>
        /// count in bytes of the content that follows the subchunck.
        /// </summary>
        public int fmtSubChunkSize;
        /// <summary>
        /// We only accept PCM encoded data: fmtAudioFormat = 1
        /// </summary>
        public int fmtAudioFormat;
        public int fmtNumChannels;
        public int fmtSampleRate;
        public int fmtByteRate;
        public int fmtBlockAlign;
        public int fmtBitsPerSample;
        public int fmtXtra;
        /// <summary>
        /// nextIndex points to byteposition just after subchunk. 
        /// not part of the file, only for internal use
        /// </summary>
        public int nextIndex;
    }

    /// <summary>
    /// subchunk SmplWavSubChunk
    /// Must start with bytes 'smpl'
    /// D3Q: see  https://sites.google.com/site/musicgapi/technical-documents/wav-file-format#smpl
    /// </summary>
    internal class SmplWavSubChunk
    {        
        /// <summary>
        /// count in bytes of the content that follows the subchunck.
        /// </summary>
        public int smplSubChunkSize;
        public int smplSamplePeriod;
        public int smplMIDIUnityNote;
        public int smplLoopStart;
        public int smplLoopEnd;
        /// <summary>
        /// nextIndex points to byteposition just after subchunk. 
        /// not part of the file, only for internal use
        /// </summary>
        public int nextIndex;
    }

    internal class DataWavSubChunk
    {
        /// <summary>
        /// count in bytes of the content that follows the subchunck.
        /// </summary>
        public int dataSubChunkSize;
        /// <summary>
        /// nextIndex points to byteposition just after subchunk. 
        /// not part of the file, only for internal use
        /// </summary>
        public int nextIndex;
    }

    /// <summary>
    /// D3Q: Reads a bytearray from file and has methods to find a (sub)chunk
    /// </summary>
    internal class WavFile
    {
        public string fileLocation;

        public byte[] wavFileByteArray;
        public long wavArrayLenght;

        public RiffWavChunk riffWafChunk;
        public FmtWavSubChunk fmtWavSubChunk;
        public SmplWavSubChunk smplWavSubChunk;
        public DataWavSubChunk dataWavSubChunk;

        public void ReadWavFileByteArray(string WavFilePath)
        {
            this.fileLocation = WavFilePath;

            using (FileStream WavFileStream = new FileStream(WavFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                wavFileByteArray = new byte[WavFileStream.Length];
                wavArrayLenght = WavFileStream.Length;
                WavFileStream.Read(wavFileByteArray, 0, (int)WavFileStream.Length);
            }

            riffWafChunk = getRIFFChunk();
            if (riffWafChunk == null) { Console.WriteLine("No RIFF chunk found!"); }
            if (riffWafChunk.format != "WAVE") { Console.WriteLine("RIFF file is not a wav file!"); }

            fmtWavSubChunk = getFmtSubChunk(riffWafChunk.nextIndex);
            if (fmtWavSubChunk == null) { Console.WriteLine("No Format subchunk found!"); }
            if (fmtWavSubChunk.fmtAudioFormat != 1) { Console.WriteLine("Wav file not in PCM format!"); }

            smplWavSubChunk = getSmplSubChunk(riffWafChunk.nextIndex);
            if (smplWavSubChunk == null)
            {
                Console.WriteLine("in file " + WavFilePath + ": No Sample subchunk found!");
            }

            dataWavSubChunk = getDataSubChunk(riffWafChunk.nextIndex);
            if (dataWavSubChunk == null) { Console.WriteLine("No data subchunk found!"); }

        }

        public RiffWavChunk getRIFFChunk()
        {
            RiffWavChunk riffWavChunk = new RiffWavChunk();
            int index = 0;
            if (getChunk4CharCode(wavFileByteArray, index) != "RIFF") return null;

            //int wavChunkSize = getIntChunkSize(wavFileByteArray, 4);
            int wavChunkSize = readInt32Bytes(wavFileByteArray, 4);

            if (getChunk4CharCode(wavFileByteArray, 8) != "WAVE") return null;
            riffWavChunk.format = getChunk4CharCode(wavFileByteArray, 8);
            riffWavChunk.chunkSize = wavChunkSize;
            riffWavChunk.nextIndex = 12;
            return riffWavChunk;
        }

        public FmtWavSubChunk getFmtSubChunk(int after)
        {
            FmtWavSubChunk fmtwavSubChunk = new FmtWavSubChunk();
            int index = after;

            while (getSubChunck4CharCode(wavFileByteArray, index) != "fmt ")
            {
                index = index + 8 + readInt32Bytes(wavFileByteArray, index + 4);
                if (index >= wavArrayLenght) return null;
            }
            fmtwavSubChunk.fmtSubChunkSize = 20; // readInt32Bytes(wavFileByteArray, index + 4);
            fmtwavSubChunk.fmtAudioFormat = readInt16Bytes(wavFileByteArray, index + 8); //1: PCM
            fmtwavSubChunk.fmtNumChannels = readInt16Bytes(wavFileByteArray, index + 10);
            fmtwavSubChunk.fmtSampleRate = readInt32Bytes(wavFileByteArray, index + 12);
            fmtwavSubChunk.fmtByteRate = readInt32Bytes(wavFileByteArray, index + 16);
            fmtwavSubChunk.fmtBlockAlign = readInt16Bytes(wavFileByteArray, index + 20);
            fmtwavSubChunk.fmtBitsPerSample = readInt16Bytes(wavFileByteArray, index + 22);
            fmtwavSubChunk.fmtXtra = 0;

            fmtwavSubChunk.nextIndex = index + 8 + fmtwavSubChunk.fmtSubChunkSize;

            return fmtwavSubChunk;
        }

        public SmplWavSubChunk getSmplSubChunk(int after)
        {
            SmplWavSubChunk smplWavSubChunk = new SmplWavSubChunk();
            int index = after;

            while (getSubChunck4CharCode(wavFileByteArray, index) != "smpl")
            {
                index = index + 8 + readInt32Bytes(wavFileByteArray, index + 4);
                if (index >= wavArrayLenght) return null;
            }

            smplWavSubChunk.smplSubChunkSize = readInt32Bytes(wavFileByteArray, index + 4);
            smplWavSubChunk.smplSamplePeriod = readInt32Bytes(wavFileByteArray, index + 16);
            smplWavSubChunk.smplMIDIUnityNote = readInt32Bytes(wavFileByteArray, index + 20);
            smplWavSubChunk.smplLoopStart = readInt32Bytes(wavFileByteArray, index + 40 + 8);
            smplWavSubChunk.smplLoopEnd = readInt32Bytes(wavFileByteArray, index + 40 + 12);

            smplWavSubChunk.nextIndex = index + 8 + smplWavSubChunk.smplSubChunkSize;

            return smplWavSubChunk;
        }

        /// <summary>
        /// D3q: gets header of data chsubchunk; nextIndex points to the data after the header.
        /// </summary>
        /// <param name="after"></param>
        /// <returns></returns>
        public DataWavSubChunk getDataSubChunk(int after)
        {
            DataWavSubChunk dataWavSubChunk = new DataWavSubChunk();
            int index = after;

            while (getSubChunck4CharCode(wavFileByteArray, index) != "data")
            {
                index = index + 8 + readInt32Bytes(wavFileByteArray, index + 4);
                if (index >= wavArrayLenght) return null;
            }

            dataWavSubChunk.dataSubChunkSize = readInt32Bytes(wavFileByteArray, index + 4);

            dataWavSubChunk.nextIndex = index + 8;
            return dataWavSubChunk;
        }

        ///// <summary>
        ///// Returns byte index where the frame starts
        ///// Obsolete: now a function of frameReader
        ///// </summary>
        ///// <param name="time"></param>
        ///// <param name="dataWavSubChunk"></param>
        ///// <param name="fmtWavSubChunk"></param>
        ///// <returns></returns>
        //public int findFrameAfterTime(float time)
        //{
        //    // D3Q: evry frame takes fmtWavSubChunk.fmtBlockAlign bytes;
        //    // evry frame takes 1/fmtwavSubChunk.fmtSampleRate seconds;
        //    // In time time we have time*fmtwavSubChunk.fmtSampleRate frames = time*fmtwavSubChunk.fmtSampleRate * fmtWavSubChunk.fmtBlockAlign bytes.

        //    int bytes = (int)Math.Ceiling(time * fmtWavSubChunk.fmtSampleRate) * fmtWavSubChunk.fmtBlockAlign;

        //    return dataWavSubChunk.nextIndex + bytes;//(int)(wavArrayLenght - dataWavSubChunk.nextIndex) / fmtWavSubChunk.fmtBlockAlign;
        //}

        /// <summary>
        /// returns a FrameReader that points to the first data byte
        /// </summary>
        /// <returns></returns>
        public FrameReader getFrameReader()
        {
            return new FrameReader(this);
        }

        /// <summary>
        /// sets the chunk sizes so that it seems the bytes are deleted
        /// </summary>
        /// <param name="time"></param>
        public void deleteAfterFrame(int  frame)
        {
            int dataBytes = frame * fmtWavSubChunk.fmtBlockAlign + dataWavSubChunk.nextIndex;
            //update chunksizes
            dataWavSubChunk.dataSubChunkSize = dataBytes - dataWavSubChunk.nextIndex;
            riffWafChunk.chunkSize = (8 + dataWavSubChunk.dataSubChunkSize + fmtWavSubChunk.fmtSubChunkSize + 8 + 4);
        }

        ///// <summary>
        ///// sets the chunk sizes so that it seems the bytes are deleted
        ///// Obsolete: replaced with deleteAfterFrame
        ///// </summary>
        ///// <param name="time"></param>
        //public void deleteAfter(float time)
        //{
        //    int dataBytes = findFrameAfterTime(time);
        //    //update chunksizes
        //    dataWavSubChunk.dataSubChunkSize = dataBytes - dataWavSubChunk.nextIndex;
        //    riffWafChunk.chunkSize = (8 + dataWavSubChunk.dataSubChunkSize + fmtWavSubChunk.fmtSubChunkSize + 8 + 4);
        //}

        /// <summary>
        /// D3Q: Used to read chunks; Frames must be read using a FrameReader
        /// TODO: integrate wit frameReader, same for the next methods; HeaderReader subclass of FrameReader?
        /// </summary>
        /// <param name="wavFileByteArray"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private string getChunk4CharCode(byte[] wavFileByteArray, int index)
        {
            string result = "";
            result += ((char)wavFileByteArray[index + 0]);
            result += ((char)wavFileByteArray[index + 1]);
            result += ((char)wavFileByteArray[index + 2]);
            result += ((char)wavFileByteArray[index + 3]);

            return result;
        }

        /// <summary>
        /// D3Q: Used to read chunks; Frames must be read using a FrameReader
        /// </summary>
        /// <param name="wavFileByteArray"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private string getSubChunck4CharCode(byte[] wavFileByteArray, int index)
        {
            string result = "";
            result += ((char)wavFileByteArray[index + 0]);
            result += ((char)wavFileByteArray[index + 1]);
            result += ((char)wavFileByteArray[index + 2]);
            result += ((char)wavFileByteArray[index + 3]);

            return result;
        }

        /// <summary>
        /// D3Q: Used to read chunks; Frames must be read using a FrameReader
        /// </summary>
        /// <param name="wavFileByteArray"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static int readInt32Bytes(byte[] wavFileByteArray, int index)
        {
            byte[] bytes = new byte[4];
            bytes[0] = wavFileByteArray[index];
            bytes[1] = wavFileByteArray[index + 1];
            bytes[2] = wavFileByteArray[index + 2];
            bytes[3] = wavFileByteArray[index + 3];
            //if (BitConverter.IsLittleEndian) Array.Reverse(bytes);

            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// D3Q: Used to read chunks; Frames must be read using a FrameReader
        /// </summary>
        /// <param name="wavFileByteArray"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static int readInt16Bytes(byte[] wavFileByteArray, int index)
        {
            byte[] bytes = new byte[2];
            bytes[0] = wavFileByteArray[index];
            bytes[1] = wavFileByteArray[index + 1];
            //if (BitConverter.IsLittleEndian) Array.Reverse(bytes);

            return BitConverter.ToInt16(bytes, 0);
        }

    }

    /// <summary>
    /// D3Q: to write the contents of a WavFile back to disk
    /// </summary>
    internal class WavWriter
    {
        FileStream wavStream;

        public WavWriter(string url)
        {
            //if (File.Exists(url)) return;
            this.wavStream = File.Create(url);
        }

        public void writeWavFile(WavFile wavFile)
        {
            writeRiffWavChunk(wavFile.riffWafChunk);
            writeFmtWavSubChunk(wavFile.fmtWavSubChunk);
            writeDataWavSubChunk(wavFile.dataWavSubChunk);
            writeDataBytes(wavFile.wavFileByteArray, wavFile.dataWavSubChunk.nextIndex, wavFile.dataWavSubChunk.dataSubChunkSize);
        }

        public void writeRiffWavChunk(RiffWavChunk chunk)
        {
            addString("RIFF");
            add4ByteValue(chunk.chunkSize);
            addString("WAVE");
        }

        public void writeFmtWavSubChunk(FmtWavSubChunk fmtwavSubChunk)
        {
            addString("fmt ");
            add4ByteValue(fmtwavSubChunk.fmtSubChunkSize);
            add2ByteValue(fmtwavSubChunk.fmtAudioFormat);
            add2ByteValue(fmtwavSubChunk.fmtNumChannels);
            add4ByteValue(fmtwavSubChunk.fmtSampleRate);
            add4ByteValue(fmtwavSubChunk.fmtByteRate);
            add2ByteValue(fmtwavSubChunk.fmtBlockAlign);
            add2ByteValue(fmtwavSubChunk.fmtBitsPerSample);
            add4ByteValue(fmtwavSubChunk.fmtXtra);
        }

        public void writeDataWavSubChunk(DataWavSubChunk datawavSubChunk)
        {
            addString("data");
            add4ByteValue(datawavSubChunk.dataSubChunkSize);
        }

        public void writeDataBytes(byte[] buffer, int offset, int count)
        {
            wavStream.Write(buffer, offset, count);
        }

        public void dispose()
        {
            wavStream.Flush();
            wavStream.Close();
            wavStream.Dispose();
        }

        private void addString(string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            this.wavStream.Write(info, 0, info.Length);
        }

        private void add4ByteValue(int byte4)
        {
            this.wavStream.Write(BitConverter.GetBytes(byte4), 0, 4);
        }

        private void add2ByteValue(int byte2)
        {
            this.wavStream.Write(BitConverter.GetBytes(byte2), 0, 2);
        }

    }

}
