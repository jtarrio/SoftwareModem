using System;
using System.Collections.Concurrent;

namespace SoftwareModem
{
    class ToneGenerator : NAudio.Wave.WaveProvider32
    {
        private int sampleRate;
        private int freqOne;
        private int freqZero;
        private ISubGenerator currentGenerator;
        private ISubGenerator nextGenerator;

        public ToneGenerator(int sampleRate, int dataFreq)
        {
            this.sampleRate = sampleRate;
            this.freqOne = dataFreq - 100;
            this.freqZero = dataFreq + 100;
            this.currentGenerator = new SilenceGenerator();
            this.nextGenerator = null;
            SetWaveFormat(sampleRate, 1);
        }

        public void Silence()
        {
            this.nextGenerator = new SilenceGenerator();
        }

        public void SendANSam()
        {
            this.nextGenerator = new ANSamGenerator(sampleRate);
        }

        public void Repeat(bool sendPause, int[] bits)
        {
            this.nextGenerator = new RepeatGenerator(sampleRate, freqOne, freqZero, sendPause ? 500 : 0, bits);
        }

        public void PrepareForData(bool sendPreamble)
        {
            this.nextGenerator = new PrepareForDataGenerator(sampleRate, freqOne, freqZero, sendPreamble);
        }

        public void EnqueueBits(int[] bits)
        {
            currentGenerator.EnqueueBits(bits);
        }

        public override int Read(float[] samples, int offset, int sampleCount)
        {
            var numRead = currentGenerator.Generate(samples, offset, sampleCount);
            if (currentGenerator.IsDone)
            {
                if (nextGenerator != null)
                {
                    currentGenerator = nextGenerator;
                    nextGenerator = null;
                }
                else if (currentGenerator.ReadyForData)
                {
                    currentGenerator = new DataGenerator(sampleRate, freqOne, freqZero, false);
                    nextGenerator = null;
                }
            }
            return numRead;
        }

    }

    internal interface ISubGenerator
    {
        bool IsDone { get; }
        bool ReadyForData { get; }
        int Generate(float[] samples, int offset, int sampleCount);
        void EnqueueBits(int[] bits);
    }

    internal class SilenceGenerator : ISubGenerator
    {
        public bool IsDone { get { return true; } }

        public bool ReadyForData { get { return false; } }

        public int Generate(float[] samples, int offset, int sampleCount)
        {
            for (int i = 0; i < sampleCount; ++i)
            {
                samples[i + offset] = 0;
            }
            return sampleCount;
        }

        public void EnqueueBits(int[] bits) { }
    }

    internal class ANSamGenerator : ISubGenerator
    {
        private double freq2100;
        private double freq15;
        private int samplesPerInversion;
        private int sampleNum;
        private bool invert;

        public bool IsDone { get { return true; } }

        public bool ReadyForData { get { return false; } }

        public ANSamGenerator(int sampleRate)
        {
            this.freq2100 = 2 * Math.PI * 2100 / sampleRate;
            this.freq15 = 2 * Math.PI * 15 / sampleRate;
            this.samplesPerInversion = sampleRate * 9 / 20;
            this.sampleNum = 0;
            this.invert = false;
        }

        public int Generate(float[] samples, int offset, int sampleCount)
        {
            for (int i = 0; i < sampleCount; ++i)
            {
                double sample = Math.Cos(freq2100 * sampleNum) * (1 + 0.2 * Math.Cos(freq15 * sampleNum)) / 1.2;
                if (invert) sample = -sample;
                samples[i + offset] = (float)sample;
                ++sampleNum;
                if (sampleNum == samplesPerInversion)
                {
                    sampleNum = 0;
                    invert = !invert;
                }
            }
            return sampleCount;
        }

        public void EnqueueBits(int[] bits) { }
    }

    internal class RepeatGenerator : ISubGenerator
    {
        private const int NumRepeats = 5;

        private SilenceGenerator silenceGenerator;
        private DataGenerator dataGenerator;
        private int samplesPerBit;
        private int pauseLen;
        private int repetitions;
        private int[] bits;

        public bool IsDone { get { return pauseLen == 0 && dataGenerator.IsDone; } }

        public bool ReadyForData { get { return false; } }

        public RepeatGenerator(int sampleRate, int freqOne, int freqZero, int pauseLen, int[] bits)
        {
            this.silenceGenerator = new SilenceGenerator();
            this.dataGenerator = new DataGenerator(sampleRate, freqOne, freqZero, true);
            this.samplesPerBit = sampleRate / 300;
            this.pauseLen = sampleRate * pauseLen / 1000;
            this.repetitions = 0;
            this.bits = bits;
        }

        public int Generate(float[] samples, int offset, int sampleCount)
        {
            int sent = 0;
            if (pauseLen > 0)
            {
                sent = silenceGenerator.Generate(samples, offset, Math.Min(sampleCount, pauseLen));
                pauseLen -= sent;
                if (sent == sampleCount)
                {
                    return sent;
                }
            }

            while (sent < sampleCount && repetitions < NumRepeats)
            {
                if (dataGenerator.IsDone)
                {
                    dataGenerator.EnqueueBits(bits);
                }
                sent += dataGenerator.Generate(samples, offset + sent, sampleCount - sent);
                if (dataGenerator.IsDone)
                {
                    ++repetitions;
                }
            }
            repetitions %= NumRepeats;
            return sent;
        }

        public void EnqueueBits(int[] bits) { }
    }

    internal class PrepareForDataGenerator : ISubGenerator
    {
        private int sampleRate;
        private int freqOne;
        private int freqZero;
        private DataGenerator dataGenerator;
        private SilenceGenerator silenceGenerator;
        private int pauseLen;

        public bool IsDone { get { return pauseLen == 0; } }

        public bool ReadyForData { get { return IsDone; } }

        public PrepareForDataGenerator(int sampleRate, int freqOne, int freqZero, bool sendPreamble)
        {
            this.sampleRate = sampleRate;
            this.freqOne = freqOne;
            this.freqZero = freqZero;
            if (sendPreamble)
            {
                this.dataGenerator = new DataGenerator(sampleRate, freqOne, freqZero, true);
                this.dataGenerator.EnqueueBits(new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
            }
            else
            {
                this.dataGenerator = null;
            }
            this.silenceGenerator = new SilenceGenerator();
            this.pauseLen = sampleRate * 75 / 1000;
        }

        public int Generate(float[] samples, int offset, int sampleCount)
        {
            int preambleSent = 0;
            if (dataGenerator != null)
            {
                preambleSent = dataGenerator.Generate(samples, offset, sampleCount);
                if (dataGenerator.IsDone)
                {
                    dataGenerator = null;
                }
            }
            int toSend = Math.Min(sampleCount - preambleSent, pauseLen);
            int silenceSent = silenceGenerator.Generate(samples, offset + preambleSent, toSend);
            pauseLen -= silenceSent;
            return preambleSent + silenceSent;
        }

        public void EnqueueBits(int[] bits) { }

    }

    internal class DataGenerator : ISubGenerator
    {
        private int sampleRate;
        private double phaseDeltaOne;
        private double phaseDeltaZero;
        private double phase;
        private bool stopOnEnd;
        private int samplesPerBit;
        private int remainingSamples;
        private ConcurrentQueue<int> bitQueue;
        private int currentBit;

        public bool IsDone { get { return bitQueue.IsEmpty && remainingSamples == 0; } }

        public bool ReadyForData { get { return false; } }

        public DataGenerator(int sampleRate, int freqOne, int freqZero, bool stopOnEnd)
        {
            this.sampleRate = sampleRate;
            this.phaseDeltaOne = 2 * Math.PI * freqOne / sampleRate;
            this.phaseDeltaZero = 2 * Math.PI * freqZero / sampleRate;
            this.phase = 0;
            this.stopOnEnd = stopOnEnd;
            this.samplesPerBit = sampleRate / 300;
            this.remainingSamples = 0;
            this.bitQueue = new ConcurrentQueue<int>();
            this.currentBit = 1;
        }

        public int Generate(float[] samples, int offset, int sampleCount)
        {
            int sent = 0;
            while (sent < sampleCount)
            {
                if (remainingSamples == 0)
                {
                    if (bitQueue.IsEmpty && stopOnEnd)
                    {
                        return sent;
                    }
                    int readBit;
                    if (bitQueue.TryDequeue(out readBit))
                    {
                        currentBit = readBit;
                    }
                    remainingSamples = samplesPerBit;
                }
                double sample = Math.Cos(phase);
                samples[sent + offset] = (float)sample;
                phase += currentBit == 1 ? phaseDeltaOne : phaseDeltaZero;
                if (phase > Math.PI)
                {
                    phase -= 2 * Math.PI;
                }
                --remainingSamples;
                ++sent;
            }
            return sent;
        }

        public void EnqueueBits(int[] bits)
        {
            foreach (var b in bits)
            {
                bitQueue.Enqueue(b);
            }
        }
    }
}
