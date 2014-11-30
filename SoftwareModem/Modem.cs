using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoftwareModem
{
    class Modem
    {
        private const int Channel1Freq = 1080;
        private const int Channel2Freq = 1750;

        private readonly int[] CallMenu = new int[] {
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
                0, 1, 0, 0, 0, 0, 0, 1, 1, 1,
                0, 1, 0, 1, 0, 0, 0, 0, 0, 1,
                0, 0, 0, 0, 0, 1, 0, 0, 0, 1,
                0, 0, 0, 0, 0, 1, 0, 0, 1, 1
            };

        private CallState state;
        private ANSamDetector ansamDetector;
        private BiFSKDetector toneDetector;
        private ToneGenerator toneGenerator;
        private List<byte> receivedMenu;
        private List<byte> recvData;
        private int currentDatum = 0;
        private int currentBit = 0;

        public void Reset()
        {
            ansamDetector = null;
            toneDetector = null;
            toneGenerator = null;
            receivedMenu = null;
            currentDatum = 0;
            currentBit = 0;
            recvData = new List<byte>();
        }

        public IWaveProvider Call(IWaveIn waveIn)
        {
            Reset();
            var sampleRate = waveIn.WaveFormat.SampleRate;
            state = CallState.WaitForAnswer;
            toneDetector = new BiFSKDetector(sampleRate, Channel2Freq);
            toneDetector.DetectTone += ToneDetected;
            toneGenerator = new ToneGenerator(sampleRate, Channel1Freq);
            ansamDetector = new ANSamDetector(sampleRate);
            ansamDetector.DetectANSam += ANSamDetected;
            waveIn.DataAvailable += WaveInDataAvailable;
            return toneGenerator;
        }

        public IWaveProvider Answer(IWaveIn waveIn)
        {
            Reset();
            var sampleRate = waveIn.WaveFormat.SampleRate;
            state = CallState.WaitForCallMenu;
            toneDetector = new BiFSKDetector(sampleRate, Channel1Freq);
            toneDetector.DetectTone += ToneDetected;
            toneGenerator = new ToneGenerator(sampleRate, Channel2Freq);
            toneGenerator.SendANSam();
            waveIn.DataAvailable += WaveInDataAvailable;
            return toneGenerator;
        }

        public void Hangup()
        {
            state = CallState.Hangup;
            Reset();
        }

        public void SendData(byte[] data)
        {
            int[] bits = new int[data.Length * 10];
            for (int i = 0; i < data.Length; ++i) {
                bits[i * 10] = 0;
                bits[i * 10 + 9] = 1;
                for (int j = 0; j < 8; ++j) {
                    bits[i * 10 + j + 1] = (data[i] >> j) & 0x01;
                }
            }
            toneGenerator.EnqueueBits(bits);
        }

        public delegate void ByteReceivedHandler(object sender, ByteReceivedEventArgs e);
        public event ByteReceivedHandler ByteReceived;

        private void WaveInDataAvailable(object sender, WaveInEventArgs e)
        {
            if (ansamDetector != null)
            {
                ansamDetector.ReceiveSamples(e.Buffer, e.BytesRecorded);
            }
            else if (toneDetector != null)
            {
                toneDetector.ReceiveSamples(e.Buffer, e.BytesRecorded);
            }
        }

        private void ANSamDetected(object sender, ANSamDetector.DetectANSAmHandlerEventArgs e)
        {
            ansamDetector = null;
            state = CallState.SendCallMenu;
            toneGenerator.Repeat(true, CallMenu);
            toneDetector.ReceiveSamples(e.Buffer, e.Buffer.Length);
        }

        private void ToneDetected(object sender, BiFSKDetector.DetectToneEventArgs e)
        {
            currentDatum = ((e.ToneForOne ? 1 : 0) << 9) | (currentDatum >> 1);
            currentBit = currentBit + 1;
            if (currentDatum == 0x3ff)
            {
                recvData.Clear();
                currentBit = 0;
            }
            else if ((currentDatum & 0x201) == 0x200 && currentBit >= 10)
            {
                currentBit = 0;
                int recvByte = (currentDatum >> 1) & 0xff;
                recvData.Add((byte)recvByte);
                ByteDetected();
            }
        }

        private void ByteDetected()
        {
            switch (state)
            {
                case CallState.SendCallMenu:
                    if (recvData.Count == 5 && recvData[0] == 0xe0 && recvData[1] == 0xc1 && (recvData[4] & 0x80) == 0x80)
                    {
                        if (receivedMenu != null && recvData.SequenceEqual(receivedMenu)) {
                            toneGenerator.PrepareForData(true);
                            recvData.Clear();
                            state = CallState.Data;
                        }
                        else
                        {
                            receivedMenu = new List<byte>(recvData);
                            recvData.Clear();
                        }
                    }
                    break;
                case CallState.WaitForCallMenu:
                    if (recvData.Count == 5 && recvData[0] == 0xe0 && recvData[1] == 0xc1 && (recvData[4] & 0x80) == 0x80)
                    {
                        if (receivedMenu != null && recvData.SequenceEqual(receivedMenu))
                        {
                            recvData.Clear();
                            state = CallState.SendJointMenu;
                            toneGenerator.Repeat(false, CallMenu);
                        }
                        else
                        {
                            receivedMenu = new List<byte>(recvData);
                            recvData.Clear();
                        }
                    }
                    break;
                case CallState.SendJointMenu:
                    if (recvData.Count >= 3 && recvData[recvData.Count - 3] == 0 && recvData[recvData.Count - 2] == 0 && recvData[recvData.Count - 1] == 0)
                    {
                        toneGenerator.PrepareForData(false);
                        recvData.Clear();
                        state = CallState.Data;
                    }
                    break;
                case CallState.Data:
                    foreach (var b in recvData)
                    {
                        ByteReceived(this, new ByteReceivedEventArgs(b));
                    }
                    recvData.Clear();
                    break;
            }
        }

        private enum CallState
        {
            WaitForAnswer, SendCallMenu, WaitForCallMenu, ReceiveCallMenu, SendJointMenu, WaitForData, Data, Hangup
        }

        private class RecvData
        {
            private List<byte> bytes = new List<byte>();
            public void AddByte(int b)
            {
                bytes.Add((byte)b);
            }

            public List<byte> Data { get { return bytes; } }
        }
    }

    public class ByteReceivedEventArgs : EventArgs
    {
        public byte Byte { get; private set; }

        public ByteReceivedEventArgs(byte b)
        {
            this.Byte = b;
        }
    }

}
