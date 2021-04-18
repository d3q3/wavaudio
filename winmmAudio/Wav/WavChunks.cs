using System;
using System.IO;
using System.Text;

namespace D3Q.WavFile
{
    public class WavChunk
    {
        /// <summary>
        /// each chunk has a 4 byte name/code
        /// </summary>
        public string ChunkName;
        /// <summary>
        /// count in bytes of the content that follows this field Size.
        /// </summary>
        public int Size;

        /// <summary>
        /// use to read ChunkNames, 4-byte code
        /// </summary>
        /// <param name="wavFileStream"></param>
        /// <returns></returns>
        protected string ReadName(FileStream wavFileStream)
        {
            byte[] bn = new byte[4];
            wavFileStream.Read(bn, 0, 4);
            return "" + (char)bn[0] + (char)bn[1] + (char)bn[2] + (char)bn[3];
        }

        public WavChunk ReadNextChunk(FileStream wavFileStream)
        {
            ChunkName = ReadName(wavFileStream);
            if (ChunkName == "fmt ") return new FmtSubChunk(wavFileStream);
            //else if (ChunkName == "smpl") return new SmplSubChunk(wavFileStream);
            else if (ChunkName == "data") return new DataSubChunk(wavFileStream);
            else return new OtherChunk(wavFileStream, ChunkName);
            //{
            //    Size = ReadInt32bBytes(wavFileStream);
            //    skipBytes(wavFileStream, Size);
            //}
            //return this;
        }

        /// <summary>
        /// D3Q: Used to read chunks; Frames must be read using a FrameReader
        /// </summary>
        /// <param name="wavFileByteArray"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        protected int ReadInt16bBytes(FileStream wavFileStream)
        {
            byte[] bytes = new byte[2];
            wavFileStream.Read(bytes, 0, 2);

            return BitConverter.ToInt16(bytes, 0);
        }

        /// <summary>
        /// D3Q: Used to read chunks; Frames must be read using a FrameReader
        /// </summary>
        /// <param name="wavFileByteArray"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        protected int ReadInt32bBytes(FileStream wavFileStream)
        {
            byte[] bytes = new byte[4];
            wavFileStream.Read(bytes, 0, 4);

            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// D3Q: used for writing ChunNames
        /// </summary>
        /// <returns></returns>
        protected void WriteNameBytes(FileStream wavFileStream, string name)
        {
            byte[] bytes = new UTF8Encoding(true).GetBytes(name);
            wavFileStream.Write(bytes, 0, 4);
        }


        protected int WriteInt16bBytes(FileStream wavFileStream, short value)
        {
            byte[] bytes = new byte[2];
            wavFileStream.Read(bytes, 0, 2);

            return BitConverter.ToInt16(bytes, 0);
        }

        protected void WriteInt32bBytes(FileStream wavFileStream, int byte4)
        {
            wavFileStream.Write(BitConverter.GetBytes(byte4), 0, 4);
        }

        protected void WriteInt16bBytes(FileStream wavFileStream, int byte2)
        {
            wavFileStream.Write(BitConverter.GetBytes(byte2), 0, 2);
        }

        protected void WriteXtraBytes(FileStream wavFileStream, byte[] bytes, int count)
        {
            wavFileStream.Write(bytes, 0, count);
        }

        /// <summary>
        /// D3Q: sometimes skip bytes when a chunksize is larger than the size of the used fields
        /// </summary>
        /// <param name="wavFileStream"></param>
        /// <param name="count"></param>
        protected void skipBytes(FileStream wavFileStream, int count)
        {
            if (count < 0)
            {
                throw new InvalidDataException("RIFF structure could not be read.");
            }
            if (count > 0)
            {
                byte[] bs = new byte[count];
                wavFileStream.Read(bs, 0, count);
            }
        }

    }

    public class OtherChunk: WavChunk
    {
        public byte[] Bytes;

        public OtherChunk(string ChunkName, int Size)
        {
            this.ChunkName = ChunkName;
            this.Size = Size;
            Bytes = new byte[Size];
        }

        public OtherChunk(FileStream wavFileStream, string ChunkName)
        {
            this.ChunkName = ChunkName;
            Size = ReadInt32bBytes(wavFileStream);
            Bytes = new byte[Size];
            wavFileStream.Read(Bytes, 0, Size);
        }

        public void Write(FileStream wavFileStream)
        {
            WriteNameBytes(wavFileStream, ChunkName);
            WriteInt32bBytes(wavFileStream, Size);
            WriteXtraBytes(wavFileStream, Bytes, Bytes.Length);
        }
    }

    /// <summary>
    /// D3Q: header RiffWavChunck, first header
    /// Must start with bytes 'RIFF'
    /// </summary>
    internal class RiffChunk : WavChunk
    {
        /// <summary>
        /// format must contain the 4 bytes 'WAVE'
        /// </summary>
        public string format;


        public RiffChunk()
        {
            format = "WAVE";
            Size = 12;
        }

        public RiffChunk(FileStream wavFileStream)
        {
            ChunkName = ReadName(wavFileStream);
            if (ChunkName != "RIFF")
            {
                throw new InvalidDataException("RIFF header was not found at the start of the file.");
            }
            Size = ReadInt32bBytes(wavFileStream);

            format = ReadName(wavFileStream);
            if (format != "WAVE")
            {
                throw new InvalidDataException("WAVE header was not found at the expected offset.");
            }
        }

        public void Write(FileStream wavFileStream)
        {
            WriteNameBytes(wavFileStream, "RIFF");
            WriteInt32bBytes(wavFileStream, Size);
            WriteNameBytes(wavFileStream, "WAVE");
        }
    }

    /// <summary>
    /// The fmtSubChunk has the description of the format used.
    /// Must start with bytes 'fmt '.
    /// </summary>
    public sealed class FmtSubChunk : WavChunk
    {
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
        public byte[] fmtBytes;

        public FmtSubChunk(int numChannels, int sampleRate, int bitsPerSample)
        {
            this.ChunkName = "fmt ";
            Size = 20;

            if (bitsPerSample == 32) fmtAudioFormat = 3; else fmtAudioFormat = 1;
            fmtNumChannels = numChannels;
            fmtSampleRate = sampleRate;
            fmtBitsPerSample = bitsPerSample;
            fmtBlockAlign = numChannels * bitsPerSample / 8;
            fmtByteRate = sampleRate * fmtBlockAlign;
            fmtXtra = 0;
            fmtBytes = new byte[fmtXtra];
        }

        public FmtSubChunk(FileStream wavFileStream)
        {
            ChunkName = "fmt ";

            Size = ReadInt32bBytes(wavFileStream);
            fmtAudioFormat = ReadInt16bBytes(wavFileStream); //1: PCM
            fmtNumChannels = ReadInt16bBytes(wavFileStream);
            fmtSampleRate = ReadInt32bBytes(wavFileStream);
            fmtByteRate = ReadInt32bBytes(wavFileStream);
            fmtBlockAlign = ReadInt16bBytes(wavFileStream);
            fmtBitsPerSample = ReadInt16bBytes(wavFileStream);
            if (Size == 16)
            {
                fmtXtra = 0;
                Size = 20;
                fmtBytes = new byte[fmtXtra];
            }
            else
            {
                // D3Q: not tested...
                fmtXtra = ReadInt32bBytes(wavFileStream);
                if (fmtXtra != Size - 4 * 2 - 4 * 4)
                {
                    throw new InvalidDataException("Wave file format Size and fmtXtra not corresponding.");
                }
                fmtBytes = new byte[fmtXtra];
                wavFileStream.Read(fmtBytes, 0, fmtXtra);
            }
        }

        public void Write(FileStream wavFileStream)
        {
            WriteNameBytes(wavFileStream, ChunkName);
            WriteInt32bBytes(wavFileStream, Size);

            WriteInt16bBytes(wavFileStream, fmtAudioFormat);
            WriteInt16bBytes(wavFileStream, fmtNumChannels);
            WriteInt32bBytes(wavFileStream, fmtSampleRate);
            WriteInt32bBytes(wavFileStream, fmtByteRate);
            WriteInt16bBytes(wavFileStream, fmtBlockAlign);
            WriteInt16bBytes(wavFileStream, fmtBitsPerSample);
            WriteInt32bBytes(wavFileStream, fmtXtra);
        }

        public void SetBitsPerSample(int bits)
        {
            fmtBitsPerSample = bits;
            fmtBlockAlign = fmtNumChannels * fmtBitsPerSample / 8;
            fmtByteRate = fmtSampleRate * fmtBlockAlign;
            if (bits == 32) fmtAudioFormat = 3; else fmtAudioFormat = 1;
        }

    }


    public sealed class DataSubChunk : WavChunk
    {
        // D3Q: these two values are set after the FmtSubChunk has been read.
        public int BitsPerSample;
        public int ChannelCount;

        internal byte[] DataBytes;

        public DataSubChunk()
        {
            ChunkName = "data";
            Size = 0;

            BitsPerSample = 0;
            ChannelCount = 0;
            DataBytes = null;
            
        }


        /// </summary>
        /// <param name="wavFileStream"></param>
        public DataSubChunk(FileStream wavFileStream)
        {
            ChunkName = "data";
            Size = ReadInt32bBytes(wavFileStream);

            DataBytes = new byte[Size];
            wavFileStream.Read(DataBytes, 0, Size);
        }

        //public short[] DataShorts()
        //{
        //    if (BitsPerSample == 16) return (short[])data;
        //    return null;
        //}

        //public int[] DataInts()
        //{
        //    if (BitsPerSample == 24) return (int[])data;
        //    return null;
        //}

        //public float[] DataFloats()
        //{
        //    if (BitsPerSample==32) return (float[])data;
        //    return null;
        //}




        public void ConvertBytes( int toBitsPerSample)
        {
            FrameReader reader = new FrameReader(this);
            int toBytesPerValue = toBitsPerSample / 8;
            int maxInt = 0x7F; if (toBitsPerSample == 16) maxInt = 0x7FFF; else if (toBitsPerSample == 24) maxInt = 0x7FFFFF;


            int frame = 0;
            reader.Start(frame);
            byte[] newData = new byte[reader.FrameCount * toBytesPerValue * ChannelCount];

            int currentNew = 0;
            while (frame < reader.FrameCount)
            {
                float[] floats = reader.ReadNext();
                for (int i = 0; i < floats.Length; i++)
                {
                    if (toBitsPerSample == 16)
                    {
                        short value = (short)(floats[i]*maxInt);
                        BitConverter.GetBytes(value).CopyTo(newData, currentNew);
                    }
                    else if (toBitsPerSample == 24)
                    {
                        int value = (int)(floats[i]*maxInt);
                        // D3Q: CopyTo has no count option, so...
                        byte[] bytes4 = new byte[4]; BitConverter.GetBytes(value).CopyTo(bytes4, 0);
                        for (int j = 0; j < 3; j++) newData[currentNew + j] = bytes4[j];
                    }
                    else if (toBitsPerSample == 32)
                    {
                        float value = floats[i];
                        BitConverter.GetBytes(value).CopyTo(newData, currentNew);
                    }
                    currentNew += toBytesPerValue;
                }
                frame++;
            }

            BitsPerSample = toBitsPerSample;
            DataBytes = newData;
            Size = DataBytes.Length;
        }

        public void Write(FileStream wavFileStream)
        {
            WriteNameBytes(wavFileStream, ChunkName);
            WriteInt32bBytes(wavFileStream, Size);

            wavFileStream.Write(DataBytes, 0, DataBytes.Length);
        }

        public void DeleteAfterFrame(int frameNumber)
        {
            int bytesPerSample = ChannelCount * BitsPerSample / 8;
            Size = frameNumber * bytesPerSample;

            byte[] newData = new byte[frameNumber * bytesPerSample];

            Buffer.BlockCopy(DataBytes, 0, newData, 0, Size);
            DataBytes = newData;
        }
    }

}
