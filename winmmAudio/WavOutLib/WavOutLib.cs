using System;
using System.Collections.Generic;

// D3Q: A lib for playback of wav-files using the wavout audio subset in winmm.dll
// For the functions and structures: a subset of audio see: https://docs.microsoft.com/en-us/windows/win32/api/_multimedia/
// Using these functions see: https://docs.microsoft.com/en-us/windows/win32/multimedia/multimedia-audio,
//       in particular the topics in https://docs.microsoft.com/en-us/windows/win32/multimedia/audio-data-blocks

using System.Runtime.InteropServices;
using System.Threading;

namespace WavOutLib
{
    /// <summary>
    /// D3Q: A player is initialized to play data of this format
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class WaveFormatEx
    {
        /// <summary>
        /// if PCM == 1.
        /// </summary>
        public short wFormatTag;
        /// <summary>
        /// mono or stereo.
        /// </summary>
        public short nChannels;
        /// <summary>
        /// samples per second (hertz).
        /// </summary>
        public int nSamplesPerSec;
        /// <summary>
        /// if PCM nAvgBytesPerSec should be equal to the product of nSamplesPerSec and nBlockAlign.
        /// </summary>
        public int nAvgBytesPerSec;
        /// <summary>
        /// if PCM nBlockAlign must be equal to the product of nChannels and wBitsPerSample divided by 8 
        /// </summary>
        public short nBlockAlign;
        /// <summary>
        /// if PCM, then wBitsPerSample should be equal to 8 or 16. (D3Q: 24 not permitted; experimenting shows 
        /// that there is no problem...)
        /// </summary>
        public short wBitsPerSample;
        /// <summary>
        /// Size, in bytes, of extra format information appended to the end of the WAVEFORMATEX structure.
        /// </summary>
        public short cbSize;
    }

    /// <summary>
    /// D3Q: a block of data sent to the player has this header; its field lpData has the unmanaged buffer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WaveHdr
    {
        /// <summary>
        /// Pointer to the waveform buffer.
        /// </summary>
        public IntPtr lpData;
        /// <summary>
        /// Length, in bytes, of the buffer.
        /// </summary>
        public int dwBufferLength;
        /// <summary>
        /// When the header is used in input, specifies how much data is in the buffer.
        /// </summary>
        public int dwBytesRecorded;
        /// <summary>
        /// User data.
        /// </summary>
        public IntPtr dwUser;
        /// <summary>
        /// A bitwise OR of zero of more flags.
        /// </summary>
        public int dwFlags;
        /// <summary>
        /// Number of times to play the loop.
        /// </summary>
        public int dwLoops;
        /// <summary>
        /// reserved
        /// </summary>
        public IntPtr lpNext;
        /// <summary>
        /// reserved
        /// </summary>
        public int reserved;
    }

    /// <summary>
    /// D3Q: WaveOut capabilities: see https://www.pinvoke.net/default.aspx/winmm.waveoutgetdevcaps
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Auto)]
    public struct WAVEOUTCAPS
    {
        /// <summary>
        /// Manufacturer identifier for the device driver for the device.
        /// </summary>
        public ushort wMid;
        /// <summary>
        /// Product identifier for the device.
        /// </summary>
        public ushort wPid;
        /// <summary>
        /// Version number of the device driver for the device.
        /// </summary>
        public uint vDriverVersion;
        /// <summary>
        /// Name of the device. 
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string name;
        /// <summary>
        /// Standard formats that are supported. Can be a combination of the following: (see documentation; 
        /// no 24 bit format mentioned)
        /// </summary>
        public uint dwFormats;
        /// <summary>
        /// Number specifying whether the device supports mono (1) or stereo (2) output.
        /// </summary>
        public ushort wChannels;
        /// <summary>
        /// Packing.
        /// </summary>
        public ushort wReserved;
        /// <summary>
        /// Optional functionality supported by the device. The following values are defined: (see documentation;
        /// no mentioning of 24 bits optional support)
        /// </summary>
        public uint dwSupport;

        public override string ToString()
        {
            return string.Format("{0} >>>> properties: dwFormats {1} | wChannels {2} |  dwSupport {3}", new object[] { name, dwFormats, wChannels, dwSupport });
        }
    }


    /// <summary>
    /// D3Q: signature NextBuffer, used in a BufferDataProvider.
    /// used to fill the next buffer in WaveOutDone.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="start">start of audio bytes in buffer. MUST be aligned with used bitsInSample, e.g. start
    /// after first audio-byte must be divisible with 6 in case of 24bits, 2 channel audio.</param>
    /// <param name="count">count of bytes in next buffer. MUST be aligned with used bitsInSample, e.g. count
    /// must be divisible with 6 in case of 24bits, 2 channel audio.</param>
    public delegate bool NextBufferDelegate(out byte[] buffer, out int start, out int count);

    /// <summary>
    /// Class contains the getNextBuffer() function called in WaveOutDone() when filling the header_buffers for output.
    /// The subclasses must implement that function.
    /// It is advised to use in the constructors of subclasses frames instead of bytes and also set the bitsInSample,
    /// the reason is given in the fields start and count of the NextBufferDelegate delegate.
    /// </summary>
    public abstract class BufferDataProvider
    {
        public NextBufferDelegate getNextBuffer;
        public abstract void reset(int atFrame);
    }

    /// <summary>
    /// D3Q: A translation from WavOutXXX functions in "winmm.dll" into a class.
    /// First Open() a device, then Start() the device filling header_buffers. 
    /// The playing can be interrupted (Pause, followed by Restart().
    /// The playing can also be Reset(): the output is stopped, the header_buffers are cleared and after Start() 
    /// we start at the first audio byte again (no need to re-open the device).
    /// The device can be closed. After this the header_buffers are cleared and the device is closed. Nothing
    /// can be done without a new call to Open().
    /// 
    /// Filling of the header_buffers is done with classes that are subclasses of BufferDataProvider.
    /// </summary>
    public class WavOutPlayer
    {
        public const int CALLBACK_FUNCTION = 0x00030000;

        /// <summary>
        /// MM_WOM_OPEN 
        /// wParam = (WPARAM) hOutputDev
        /// lParam = reserved
        /// </summary>
        public const int MM_WOM_OPEN = 0x3BB; // =955
        /// <summary>
        /// MM_WOM_DONE 
        /// wParam = (WPARAM) hOutputDev
        /// lParam = (LONG) lpwvhdr
        /// </summary>
        public const int MM_WOM_DONE = 0x3BD;
        /// <summary>
        /// MM_WOM_CLOSE 
        /// wParam = (WPARAM) hOutputDev
        /// lParam = reserved
        /// </summary>
        public const int MM_WOM_CLOSE = 0x3BC;

        /// <summary>
        /// signature waveOutProc: https://docs.microsoft.com/en-us/previous-versions/dd743869(v=vs.85)
        /// used to process Header+buffer after buffer has been processed by output device.
        /// </summary>
        /// <param name="hwo">Handle to the waveform-audio device associated with the callback.</param>
        /// <param name="uMsg">WOM_OPEN, WOM_CLOSE or WOM_DONE</param>
        /// <param name="dwUser">User-instance data specified with waveOutOpen.</param>
        /// <param name="dwParam1">Message parameter; for MM_WOM_DONE the WaveHdr</param>
        /// <param name="dwParam2">Message parameter.</param>
        private delegate void WaveOutDelegate(IntPtr hwo, int uMsg, int dwUser, ref WaveHdr wavhdr, int dwParam2);
        private WaveOutDelegate afterWaveOutDone;


        /// <summary>
        /// The waveOutGetNumDevs function retrieves the number of waveform-audio output devices present in the system.
        /// </summary>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern uint waveOutGetNumDevs();

        /// <summary>
        /// D3Q: The waveOutGetDevCaps function retrieves the capabilities of a given waveform-audio output device.
        /// </summary>
        /// <param name="uDeviceID">Identifier of the waveform-audio output device. It can be either a device identifier or a handle of an 
        /// open waveform-audio output device.</param>
        /// <param name="pwoc">Pointer to a WAVEOUTCAPS structure to be filled with information about the capabilities of the device.</param>
        /// <param name="cbwoc">Size, in bytes, of the WAVEOUTCAPS structure.</param>
        /// <returns></returns>
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int waveOutGetDevCaps(uint uDeviceID, ref WAVEOUTCAPS pwoc, uint cbwoc);

        /// <summary>
        /// The waveOutOpen function opens the given waveform-audio output device for playback.
        /// </summary>
        /// <param name="hWaveOut"></param>
        /// <param name="uDeviceID"></param>
        /// <param name="lpFormat"></param>
        /// <param name="dwCallback"></param>
        /// <param name="dwInstance"></param>
        /// <param name="dwFlags"></param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, WaveFormatEx lpFormat,
            WaveOutDelegate dwCallback, IntPtr dwInstance, int dwFlags);

        /// <summary>
        /// The waveOutClose function closes the given waveform-audio output device.
        /// The close operation fails if the device is still playing a waveform-audio buffer that was previously 
        /// sent by calling waveOutWrite. Before calling waveOutClose, the application must wait for all buffers 
        /// to finish playing or call the waveOutReset function to terminate playback.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutClose(IntPtr hWaveOut);

        [DllImport("winmm.dll")]
        public static extern int waveOutPause(IntPtr hWaveOut);

        /// <summary>
        /// The waveOutRestart function resumes playback on a paused waveform-audio output device.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutRestart(IntPtr hWaveOut);

        /// <summary>
        /// The waveOutReset function stops playback on the given waveform-audio output device and resets 
        /// the current position to zero. All pending playback buffers are marked as done (WHDR_DONE) and
        /// returned to the application.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutReset(IntPtr hWaveOut);

        /// <summary>
        /// The waveOutPrepareHeader function prepares a waveform-audio data block for playback.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <param name="lpWaveOutHdr">Pointer to a WAVEHDR structure that identifies the data block to be prepared.</param>
        /// <param name="uSize">Size, in bytes, of the WAVEHDR structure.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, uint uSize);

        /// <summary>
        /// The waveOutUnprepareHeader function cleans up the preparation performed by the waveOutPrepareHeader 
        /// function. This function must be called after the device driver is finished with a data block. You 
        /// must call this function before freeing the buffer.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <param name="lpWaveOutHdr">Pointer to a WAVEHDR structure identifying the data block to be cleaned up.</param>
        /// <param name="uSize">in bytes, of the WAVEHDR structure.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, uint uSize);

        /// <summary>
        /// The waveOutWrite function sends a data block to the given waveform-audio output device.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <param name="lpWaveOutHdr">Pointer to a WAVEHDR structure containing information about the data block.</param>
        /// <param name="uSize">Size, in bytes, of the WAVEHDR structure.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutWrite(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, uint uSize);

        /// <summary>
        /// The waveOutGetPosition function retrieves the current playback position of the given waveform-audio output device.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <param name="lpMmTime">Pointer to an MMTIME structure.</param>
        /// <param name="uSize">Size, in bytes, of the MMTIME structure.</param>
        /// <returns>Returns MMSYSERR_NOERROR if successful or an error otherwise.</returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutGetPosition(IntPtr hWaveOut, out int lpMmTime, uint uSize);

        /// <summary>
        /// The waveOutSetVolume function sets the volume level of the specified waveform-audio output device.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <param name="dwVolume">New volume setting. The low-order word contains the left-channel volume setting, 
        /// and the high-order word contains the right-channel setting.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutSetVolume(IntPtr hWaveOut, int dwVolume);

        /// <summary>
        /// The waveOutGetVolume function retrieves the current volume level of the specified waveform-audio output device.
        /// </summary>
        /// <param name="hWaveOut">Handle to the waveform-audio output device.</param>
        /// <param name="dwVolume">New volume setting. The low-order word contains the left-channel volume setting, 
        /// and the high-order word contains the right-channel setting.</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutGetVolume(IntPtr hWaveOut, out int dwVolume);

        /// <summary>
        /// Get an error-string from returned error integer
        /// </summary>
        /// <param name="mmrError">returned integer from a waveOut function</param>
        /// <param name="pszText">the error text</param>
        /// <param name="cchText">size of returned text</param>
        /// <returns></returns>
        [DllImport("winmm.dll")]
        public static extern int waveOutGetErrorText(int mmrError, IntPtr pszText, uint cchText);


        // D3Q: handle of player, used in many functions of winmm
        private IntPtr handlePlayer = (IntPtr)0;
        // D3Q: code in waveOutDone() get locked
        // used in resetting: we unprepare the header_buffers when resetting
        private bool resetting = false;
        // D3Q: can be used as a very rough indication how far the input is processed. Easier than the built in waveOutGetPosition()
        public int bytesProcessed;
        // D3Q: when unpreparing header_buffers we decrement headersInUse. When it reaches 0 then resetting is false.
        private int headersInUse;
        // D3Q: the application has to write its own dataproviders for filling the header_buffers
        public BufferDataProvider bufferDataProvider;
        // D3Q: used as a constant
        private uint sizeOfWaveHdr = (uint)Marshal.SizeOf<WaveHdr>();

        /// <summary>
        /// D3Q: returns the error string corresponding to return value mmError
        /// </summary>
        /// <param name="mmrError"></param>
        /// <returns></returns>
        public string getWavOutError(int mmError)
        {
            IntPtr pszText = Marshal.AllocHGlobal(256);
            waveOutGetErrorText(mmError, pszText, 256);
            string res = Marshal.PtrToStringAnsi(pszText);
            Marshal.FreeHGlobal(pszText);
            return res;
        }


        /// <summary>
        /// D3Q: creates HeaderCount Header_buffers. Filling the buffers is done with the dataprovider.
        /// </summary>
        /// <param name="dataProvider">dataProvider.getNextBuffer returns the buffer used to fill a header_buffer</param>
        /// <param name="HeaderCount">the number of headerBuffers that will be used.</param>
        /// <returns></returns>
        public int initHeaders(BufferDataProvider dataProvider, int HeaderCount)
        {
            this.bufferDataProvider = dataProvider;
            int res = 0;
            WaveHdr[] waveOutHdr = new WaveHdr[HeaderCount];
            for (int i = 0; i < HeaderCount; i++)
            {
                byte[] buffer; int startHeader, countHeader;
                dataProvider.getNextBuffer(out buffer, out startHeader, out countHeader);
                res = prepareAndSend(ref waveOutHdr[i], buffer, startHeader, countHeader);
                if (res != 0) return res;

                headersInUse = HeaderCount;
                bytesProcessed = 0;
            }
            return res;
        }

        /// <summary>
        /// D3Q: callback called by output system after opening and closing the device, and after a header_buffer
        /// has been processed (message MM_WOM_DONE)
        /// </summary>
        /// <param name="hdrvr">the output device</param>
        /// <param name="uMsg"></param>
        /// <param name="dwUser"></param>
        /// <param name="wavhdr">parameter that has the processed header_buffer when message isMM_WOM_DONE </param>
        /// <param name="dwParam2"></param>
        private void waveOutDone(IntPtr hdrvr, int uMsg, int dwUser, ref WaveHdr wavhdr, int dwParam2)
        {
            int res;

            if (uMsg == MM_WOM_DONE && bufferDataProvider != null)
            {
                bytesProcessed += wavhdr.dwBufferLength;

                byte[] buffer; int start; int count;
                bufferDataProvider.getNextBuffer(out buffer, out start, out count);

                if (count > 0 && !resetting)
                {
                    if (headersInUse == 0)
                    {
                        // D3Q: we never want to see this
                        Console.WriteLine("waveOutDone: no header buffers left, use more headers or larger buffers");
                        closeDevice();
                    }
                    res = prepareAndSend(ref wavhdr, buffer, start, count);
                    if (res != 0)
                    {
                        // D3Q: we never want to see this
                        Console.WriteLine("waveOutDone: " + getWavOutError(res));
                        closeDevice();
                    }
                }
                else
                {
                    res = waveOutUnprepareHeader(handlePlayer, ref wavhdr, (uint)0);
                    headersInUse--;
                    if (headersInUse == 0) resetting = false;
                }
            }
        }

        /// <summary>
        /// D3Q: get number of audio output devices
        /// </summary>
        /// <returns></returns>
        public uint getNumDevs()
        {
            return waveOutGetNumDevs();
        }

        /// <summary>
        /// D3Q: returns the name and capabilities of output device
        /// </summary>
        public WAVEOUTCAPS getCapabilityDevice(uint deviceId)
        {
            WAVEOUTCAPS caps = new WAVEOUTCAPS();
            waveOutGetDevCaps(deviceId, ref caps, (uint)Marshal.SizeOf(typeof(WAVEOUTCAPS)));

            return caps;
        }

        /// <summary>
        /// initializes the device for playing a certain format
        /// </summary>
        /// <param name="waveFormat"></param>
        /// <param name="deviceId">deviceId=-1 uses the default output device</param>
        public int openDevice(WaveFormatEx waveFormat, int deviceId = -1)
        {
            if (handlePlayer != (IntPtr)0) closeDevice();

            afterWaveOutDone = new WaveOutDelegate(waveOutDone);
            resetting = false;
            headersInUse = 0;

            IntPtr dwInstance = (IntPtr)0;
            int res = waveOutOpen(out handlePlayer, deviceId, waveFormat, afterWaveOutDone, dwInstance, CALLBACK_FUNCTION);
            return res;
        }

        /// <summary>
        /// Closes the device
        /// </summary>
        /// <returns></returns>
        public int closeDevice()
        {
            // D3Q: first reset device to stop playing: see waveOutClose comments
            resetDevice();
            return waveOutClose(handlePlayer);
        }


        /// <summary>
        /// Pauses the device. Use RestartDevice to continue
        /// </summary>
        /// <returns></returns>
        public int pauseDevice()
        {
            return waveOutPause(handlePlayer);
        }

        /// <summary>
        /// Restarts the device after a pause
        /// </summary>
        /// <returns></returns>
        public int restartDevice()
        {
            return waveOutRestart(handlePlayer);
        }

        /// <summary>
        /// The waveOutReset function stops playback on the given waveform-audio output device and resets 
        /// the current position to zero. All pending playback buffers are marked as done (WHDR_DONE) and
        /// returned to the application.
        /// The pending playback buffers are unprepared in waveOutDone because we set resetting = true.
        /// </summary>
        /// <returns></returns>
        public int resetDevice()
        {
            if (headersInUse > 0) resetting = true;
            return waveOutReset(handlePlayer);
        }

        /// <summary>
        /// A player is created to play data of this format.
        /// </summary>
        /// <param name="sampleFrequency">For instance 44100 frames/sec</param>
        /// <param name="bitsPerSample">For PCM 8, 16 (and 24)</param>
        /// <param name="channels">mono=1, stereo=2</param>
        /// <returns></returns>
        public WaveFormatEx createWaveOutFormat(int sampleFrequency, int bitsPerSample, int channels)
        {
            WaveFormatEx waveFormat = new WaveFormatEx();
            waveFormat.wFormatTag = (short)1;
            waveFormat.nAvgBytesPerSec = (int)(sampleFrequency * (bitsPerSample / 8) * channels);
            waveFormat.nBlockAlign = (short)(channels * (bitsPerSample / 8));
            waveFormat.nChannels = (short)channels;
            waveFormat.nSamplesPerSec = (int)sampleFrequency;
            waveFormat.wBitsPerSample = (short)bitsPerSample;
            waveFormat.cbSize = 0;
            return waveFormat;
        }

        /// <summary>
        /// Prepares the header and sends the header_buffer to the output device
        /// </summary>
        /// <param name="hdr"></param>
        /// <param name="data"></param>
        /// <param name="from"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int prepareAndSend(ref WaveHdr hdr, byte[] data, int from, int count)
        {
            int res = 0;

            hdr.dwBufferLength = count;
            hdr.dwFlags = 0;
            hdr.dwLoops = 0;

            // copy buffer to unmanaged buffer locked in memory
            //if (hdr.lpData != (IntPtr)0) Marshal.FreeHGlobal(hdr.lpData); //should I use unPrepare??
            hdr.lpData = Marshal.AllocHGlobal(count);
            // public static void Copy(byte[] source, int startIndex, IntPtr destination, int length);
            Marshal.Copy(data, from, hdr.lpData, count);

            uint wavhdrsize = sizeOfWaveHdr;

            res = waveOutPrepareHeader(handlePlayer, ref hdr, wavhdrsize);
            if (res != 0)
            {
                return res;
            }
            res = waveOutWrite(handlePlayer, ref hdr, wavhdrsize);
            if (res != 0)
            {
                return res;
            }
            return res;
        }

    }

}