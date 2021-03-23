using System;
using System.Windows.Forms;
using WavFiles;
using WavOutLib;

namespace testWavOut
{
    public partial class Form1 : Form
    {
        WavOutPlayer waveOut;
        LoopedDataProvider dataProvider;
        //FixedLengthDataProvider dataProvider;
        const int HeaderCount = 6;

        public Form1()
        {
            InitializeComponent();
        }


        private void btnInit_Click(object sender, EventArgs e)
        {
            waveOut = new WavOutPlayer();
            listBox1.Items.Clear();
            for (uint i=0; i< waveOut.getNumDevs(); i++)
                listBox1.Items.Add(this.waveOut.getCapabilityDevice(i).ToString());
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            // D3Q: read data from wav-file; 038-d and 052-e are organ-pipes; toccata is, well... toccata.
            WavFile wavFile = new WavFile();
            // "../../sound/038-d.wav", 16 bit, used with a LoopDataProvider
            // "../../sound/052-e.wav", 24 bit, used with a LoopDataProvider
            //wavFile.ReadWavFileByteArray("../../sound/038-d.wav");
            //"../../sound/Toccata-and-fugue-in-d-minor.wav" used with a FixedLengthDataProvider
            wavFile.ReadWavFileByteArray("../../sound/Toccata-and-fugue-Short.wav");

            FrameReader frameReader = wavFile.getFrameReader();
            frameReader.startFrame(0);

            // D3Q: create a dataprovider; a loopedDataprovider repeats itself in a loop
            //dataProvider = new LoopedDataProvider(frameReader.wavBytes, frameReader.currentByte, frameReader.findFrameAfterTime(1.20747916666667f),
            //    frameReader.findFrameAfterTime(2.7106875f), 6000, frameReader.bytesInValue());
            //dataProvider = new FixedLengthDataProvider(frameReader.wavBytes, frameReader.currentByte, 2200, frameReader.bytesInValue());
            dataProvider = new LoopedDataProvider(frameReader.wavBytes, frameReader.currentByte, frameReader.findFrameAfterTime(0.0f),
                frameReader.findFrameAfterTime(120.0f), 2200, frameReader.bytesInValue());

            // D3Q: open device
            WaveFormatEx waveFormatEx = waveOut.createWaveOutFormat(frameReader.sampleRate, frameReader.bitsSample, frameReader.channelCount);
            // if deviceId==-1 the default output is used 
            int deviceId = listBox1.SelectedIndex;
            int res = waveOut.openDevice(waveFormatEx, deviceId);
            if (res != 0) { Console.WriteLine(waveOut.getWavOutError(res)); Console.ReadLine(); }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            int res = waveOut.initHeaders(dataProvider, HeaderCount);
            if (res != 0) Console.WriteLine("Start: " + waveOut.getWavOutError(res));
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            waveOut.pauseDevice();
        }

        private void btnResume_Click(object sender, EventArgs e)
        {
            waveOut.restartDevice();
        }

        private void btnReset_Click_1(object sender, EventArgs e)
        {
            waveOut.resetDevice();
            dataProvider.reset(80000);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            waveOut.closeDevice();
        }

    }



    /// <summary>
    /// Provides the data used to fill the buffers of the output device.
    /// </summary>
    class FixedLengthDataProvider : BufferDataProvider
    {
        byte[] buffer;
        // D3Q: constructor translates frame-count to byte-count
        int currentByte, countBytes;
        // D3Q: index first byte audio in buffer
        int nulByte;
        // D3Q: bytes in each frame, e.g. when 24bits, 2 channels then bytesInFrame = 6.
        int bytesInFrame;

        /// <summary>
        /// D3Q: initializes the data provider. 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="nulByte">index of first audio byte in buffer</param>
        /// <param name="frameCount">count in frames of each buffer when calling getBuffer()</param>
        /// <param name="bytesInFrame">e.g. for 24bits and 2 channels: 6 bytes</param>
        public FixedLengthDataProvider(byte[] buffer, int nulByte, int frameCount, int bytesInFrame)
        {
            this.buffer = buffer; this.currentByte = nulByte; this.nulByte = nulByte; this.countBytes = frameCount*bytesInFrame;
            this.bytesInFrame = bytesInFrame;

            this.getNextBuffer = getBuffer;
        }

        public override void reset(int atFrame)
        {
            this.currentByte = nulByte+atFrame* bytesInFrame;
        }

        private bool getBuffer(out byte[] buffer, out int start, out int count)
        {
            bool more = true;
            buffer = this.buffer;
            start = this.currentByte;
            int headerStart = this.currentByte;
            if (buffer.Length >= currentByte + countBytes) currentByte += countBytes; else { currentByte = buffer.Length; more = false; }
            count = currentByte - headerStart;
            return more;
        }
    }

    /// <summary>
    /// Provides the data used to fill the buffers of the output device.
    /// </summary>
    class LoopedDataProvider : BufferDataProvider
    {
        byte[] buffer;
        // D3Q: constructor translates frame-count to byte-count
        int currentByte, loopStartByte, loopEndByte, countBytes;
        // D3Q: index first byte audio in buffer
        int nulByte;
        // D3Q: bytes in each frame, e.g. when 24bits, 2 channels then bytesInFrame = 6.
        int bytesInFrame;

        /// <summary>
        /// D3Q: initializes the data provider. 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="nulByte">index of first audio byte in buffer</param>
        /// <param name="loopStartFrame">start of loop using frame-count</param>
        /// <param name="loopEndFrame">end of loop using frame-count</param>
        /// <param name="frameCount">optimal count in frames of each buffer when calling getBuffer()</param>
        /// <param name="bytesInFrame">e.g. for 24bits and 2 channels: 6 bytes</param>
        public LoopedDataProvider(byte[] buffer, int nulByte, int loopStartFrame, int loopEndFrame, int frameCount, int bytesInFrame)
        {
            this.buffer = buffer; this.currentByte = nulByte; this.nulByte = nulByte;
            this.countBytes = frameCount*bytesInFrame;
            this.loopStartByte = nulByte+loopStartFrame*bytesInFrame; this.loopEndByte = nulByte+loopEndFrame*bytesInFrame;
            this.bytesInFrame = bytesInFrame;

            this.getNextBuffer = getBuffer;
        }

        public override void reset(int atFrame)
        {
            this.currentByte = nulByte + atFrame * bytesInFrame;
        }

        private bool getBuffer(out byte[] buffer, out int start, out int count)
        {
            bool more = true;
            buffer = this.buffer;
            start = this.currentByte;
            int headerStart = this.currentByte;
            if (loopEndByte >= currentByte + countBytes) currentByte += countBytes; else { currentByte = loopEndByte; }
            count = currentByte - headerStart;
            if (currentByte == loopEndByte) currentByte = loopStartByte;
            //Console.WriteLine(">> " + loopStartByte + " > " + loopEndByte+ " > " + start + " > " + count);
            return more;
        }
    }

}
