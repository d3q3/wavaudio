using System;
using System.Windows.Forms;
//using WavFiles;
using D3Q.WavFile;
using WavOutLib;

namespace testWavOut
{
    public partial class Form1 : Form
    {
        WavOutPlayer waveOut;
        
        FixedLengthDataProvider dataProvider; //another option: LoopedDataProvider dataProvider;
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
            // D3Q: read data from wav-files; 038-d and 052-e are organ-pipes; toccata is, well... toccata.
            // data read from Wav-file using class Wav in Wav-project
            Wav WavFile = new Wav("../../sound/Toccata-and-fugue-Short.wav");

            FrameReader frameReader = WavFile.getFrameReader();
            frameReader.Start(0);

            //loopedProvider = new LoopedDataProvider(frameReader, 200000, frameReader.FrameCount, 2500);
            dataProvider = new FixedLengthDataProvider(frameReader, 2500);

            // D3Q: open device
            FmtSubChunk fmt = WavFile.FmtChunk;
            WaveFormatEx waveFormatEx = waveOut.createWaveOutFormat(fmt.SampleRate, fmt.BitsPerSample, fmt.NumChannels);
            // if deviceId == -1 the default output is used 
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
        FrameReader frameReader;
        int frameNextCount;

        public FixedLengthDataProvider(FrameReader frameReader, int frameNextCount)
        {
            this.frameReader = frameReader;
            this.frameNextCount = frameNextCount;

            this.getNextBuffer = getBuffer;
        }

        public override void reset(int atFrame)
        {
            frameReader.Start(atFrame);
        }

        private bool getBuffer(out byte[] buffer, out int start, out int count)
        {
            bool more = true;
            int toCopy = frameNextCount;
            if (frameReader.CurrentFrame + frameNextCount > frameReader.FrameCount)
            {
                toCopy = frameReader.FrameCount - frameReader.CurrentFrame;
                more = false;
            }
            buffer = frameReader.ReadNextFrames(toCopy, out int framesCopied, out int firstByte, out int byteCount);
            start = firstByte;
            count = byteCount;
            return more;
        }
    }

    /// <summary>
    /// D3Q: Provides the data used to fill the buffers of the output device. The data loops from loopStartTime to loopEndTime.
    /// </summary>
    class LoopedDataProvider : BufferDataProvider
    {
        int loopStartFrame, loopEndFrame, frameNextCount;
        FrameReader frameReader;
        public LoopedDataProvider(FrameReader frameReader, int loopStartFrame, int loopEndFrame, int frameNextCount)
        {
            this.frameReader = frameReader;
            this.loopStartFrame = loopStartFrame;
            this.loopEndFrame = loopEndFrame-1; // endframe is included, so -1
            this.frameNextCount = frameNextCount;

            this.getNextBuffer = getBuffer;
        }

        public override void reset(int atFrame) { 
            frameReader.Start(atFrame);
        }

        private bool getBuffer(out byte[] buffer, out int start, out int count)
        {
            bool more = true;
            int toCopy = frameNextCount;
            if (frameReader.CurrentFrame + frameNextCount > frameReader.FrameCount) toCopy = frameReader.FrameCount - frameReader.CurrentFrame;
            buffer = frameReader.ReadNextFrames(toCopy, out int framesCopied, out int firstByte, out int byteCount);
            start = firstByte;
            count = byteCount;

            if ( frameReader.CurrentFrame>=loopEndFrame)
                frameReader.Start(loopStartFrame);
            return more;
        }
    }

}
