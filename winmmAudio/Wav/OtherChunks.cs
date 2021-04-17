using System;
using System.IO;
using System.Text;

namespace D3Q.WavFile
{
    class OtherChunks
    {
    }
    /// <summary>
    /// subchunk SmplWavSubChunk
    /// Must start with bytes 'smpl'
    /// D3Q: see  https://sites.google.com/site/musicgapi/technical-documents/wav-file-format#smpl
    /// </summary>
    public sealed class SmplSubChunk: WavChunk
    {
        public int smplManufacturer;
        public int smplProduct;
        public int smplSamplePeriod;
        public int smplMIDIUnityNote;
        public int smplMIDIPitchFraction;

        public SmplSubChunk(OtherChunk chunk)
        {
            if (chunk.ChunkName != "smpl")
            {
                throw new ArgumentException("arument in SmplSubChunk is not a Smpl chunk");
            }
            ChunkName = "smpl";
            Size = chunk.Size;

            smplManufacturer = BitConverter.ToInt32(chunk.Bytes, 0); //ReadInt32bBytes(wavFileStream);
            smplProduct = BitConverter.ToInt32(chunk.Bytes, 4); //ReadInt32bBytes(wavFileStream);
            smplSamplePeriod = BitConverter.ToInt32(chunk.Bytes, 8); //ReadInt32bBytes(wavFileStream);
            smplMIDIUnityNote = BitConverter.ToInt32(chunk.Bytes, 12);  //ReadInt32bBytes(wavFileStream);
            smplMIDIPitchFraction = BitConverter.ToInt32(chunk.Bytes, 16); //ReadInt32bBytes(wavFileStream);
        }

        public void Write(FileStream wavFileStream)
        {
            WriteNameBytes(wavFileStream, ChunkName);
            WriteInt32bBytes(wavFileStream, Size);

            WriteInt32bBytes(wavFileStream, smplManufacturer);
            WriteInt32bBytes(wavFileStream, smplProduct);
            WriteInt32bBytes(wavFileStream, smplSamplePeriod);
            WriteInt32bBytes(wavFileStream, smplMIDIUnityNote);
            WriteInt32bBytes(wavFileStream, smplMIDIPitchFraction);
            WriteXtraBytes(wavFileStream, new byte[Size - 20], Size-20);
        }
    }

}
