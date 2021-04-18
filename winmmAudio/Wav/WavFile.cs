using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace D3Q.WavFile
{
    //D3Q: version 0.21;

    //D3Q: 22 maart 2021
    //D3Q: version 0.80;

    //D3Q: 16 april 2021;
    //D3Q: version 0.85: complete new

    //D3Q: 17 april 2021
    //D3Q: version 0.86: added OtherChunks


    public sealed class Wav
    {
        private RiffChunk riffChunk;
        public DataSubChunk DataChunk { get; }
        public FmtSubChunk FmtChunk { get; }
        public SmplSubChunk SmplChunk { get; }
        public List<OtherChunk> OtherChunks { get; }


        /// <summary>For creating empty Wav file</summary>
        public Wav(int numChannels, int sampleRate, int bitsPerSample)
        {
            riffChunk = new RiffChunk();
            DataChunk = new DataSubChunk();
            FmtChunk = new FmtSubChunk(numChannels, sampleRate, bitsPerSample);
            //SmplChunk = new SmplSubChunk();
            OtherChunks = new List<OtherChunk>();
            updateWithOthers();
        }

        /// <summary>
        /// reading wav from file.
        /// </summary>
        /// <param name="path"></param>
        public Wav(string path)
        {
            using (FileStream WavFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                OtherChunks = new List<OtherChunk>();

                riffChunk = new RiffChunk(WavFileStream);
                int totalBytesInChunks = riffChunk.Size - 4;
                int bytesInChunks = 0;
                while (bytesInChunks < totalBytesInChunks) {
                    WavChunk chunk = new WavChunk().ReadNextChunk(WavFileStream);
                    bytesInChunks += (chunk.Size + 8);
                    if (chunk is DataSubChunk) DataChunk = (DataSubChunk)chunk;
                    else if (chunk is FmtSubChunk) FmtChunk = (FmtSubChunk)chunk;
                    else if (chunk is SmplSubChunk) SmplChunk = (SmplSubChunk)chunk;
                    else OtherChunks.Add((OtherChunk)chunk);
                }
                if (FmtChunk==null)
                {
                    throw new InvalidDataException("Wave file cannot exist without Format chunk.");
                }
                DataChunk.BitsPerSample = FmtChunk.fmtBitsPerSample;
                DataChunk.ChannelCount = FmtChunk.fmtNumChannels;
            }
        }

        public void Save(string path)
        {
            Save(path, false);
        }

        public void Save(string path, bool skipOthers)
        {
            using (FileStream wavFileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                if (skipOthers) updateRiffSize();
                riffChunk.Write(wavFileStream);
                if (FmtChunk == null)
                {
                    throw new InvalidDataException("Wave file cannot exist without Format chunk.");
                }
                FmtChunk.Write(wavFileStream);
                if (SmplChunk!=null) SmplChunk.Write(wavFileStream);
                if (DataChunk!=null) DataChunk.Write(wavFileStream);
                if (!skipOthers)
                {
                    OtherChunks.ForEach((chunk) => {
                        chunk.Write(wavFileStream);
                    });
                }
            }
        }

        /// <summary>
        /// D3Q: updates the Size field in the RIFF chunk using the Size of the FmtChunk and the DataChunk only.
        /// Used when a Wav file is saved with only these two subchunks.
        /// </summary>
        private void updateRiffSize()
        {
            riffChunk.Size = 4;
            if (FmtChunk != null) riffChunk.Size += 8 + FmtChunk.Size;
            //if (SmplChunk != null) riffChunk.Size += 8 + SmplChunk.Size;
            if (DataChunk != null) riffChunk.Size += 8 + DataChunk.Size;
        }

        /// <summary>
        /// D3Q: updates the Size field in the RIFF chunk using the size of all chunks
        /// </summary>
        private void updateWithOthers()
        {
            updateRiffSize();
            OtherChunks.ForEach((chunk) => { riffChunk.Size += chunk.Size + 8; });
        }

        /// <summary>
        /// deletes all frames after a given frame and updates the Size field in the RIFF chunk
        /// </summary>
        public void deleteAfterFrame(int frame)
        {
            DataChunk.DeleteAfterFrame(frame);
            updateWithOthers();
        }

        /// <summary>
        /// Converts the contents in the DataCunk to a new series of samples having toBitsPerChannel bits.
        /// </summary>
        /// <param name="toBitsPerChannel">A value of 16, 24 or 32. When 32 the channels have float values</param>
        public void Convert(int toBitsPerChannel)
        {
            DataChunk.ConvertBytes(toBitsPerChannel);
            FmtChunk.SetBitsPerSample(toBitsPerChannel);
            updateWithOthers();
        }

        /// <summary>
        /// returns a FrameReader that points to the first frame
        /// </summary>
        /// <returns></returns>
        public FrameReader getFrameReader()
        {
            return new FrameReader(DataChunk);
        }

        public int FrameRate { get { return FmtChunk.fmtSampleRate; } }
        public int BitsPerChannel { get { return FmtChunk.fmtBitsPerSample; } }

        // TODO: adding/inserting to a wavfile

    }


}
