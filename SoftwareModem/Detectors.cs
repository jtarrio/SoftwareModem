using System;
using System.Numerics;

namespace SoftwareModem
{
    abstract class BaseDetector
    {
        protected int sampleRate;
        protected int frequency;
        protected int maxDev;
        private bool ignoreInversions;
        private ComplexFilter lagFilter;
        private DoubleFilter freqFilter;
        private DoubleFilter lockFilter;
        private double phase;

        public BaseDetector(int sampleRate, int frequency, int maxDev, bool ignoreInversions)
        {
            this.sampleRate = sampleRate;
            this.frequency = frequency;
            this.maxDev = Math.Abs(maxDev);
            this.ignoreInversions = ignoreInversions;
            if (ignoreInversions)
            {
                this.frequency *= 2;
                this.maxDev *= 2;
            }
            this.lagFilter = new ComplexFilter(sampleRate, maxDev);
            this.freqFilter = new DoubleFilter(sampleRate, maxDev);
            this.lockFilter = new DoubleFilter(sampleRate, 15);
            this.phase = 0;
        }

        protected abstract bool ProcessSample(double pllFreq, double pllLock, byte[] bytes, int byteCount, int i);

        public void ReceiveSamples(byte[] bytes, int byteCount)
        {
            for (int i = 0; i < byteCount; i += 4)
            {
                int sample16 = (short)(bytes[i] + 256 * bytes[i + 1]);
                double sample = (double)sample16 / 32768;
                if (ignoreInversions)
                {
                    sample *= sample;
                }
                var pllRef = Complex.FromPolarCoordinates(1, -phase);
                var pllLag = lagFilter.Add(sample * pllRef);
                var angle = pllLag.Phase;
                var correction = Math.Max(-maxDev, Math.Min(maxDev, (angle / 65) * sampleRate / (2 * Math.PI)));
                var pllFreq = freqFilter.Add(correction);
                var pllLock = lockFilter.Add(pllLag == 0 ? 1 : sample / pllLag.Magnitude);
                phase += 2 * Math.PI * (frequency + pllFreq) / sampleRate;
                if (phase > Math.PI)
                {
                    phase -= 2 * Math.PI;
                }
                if (ProcessSample(pllFreq, pllLock, bytes, byteCount, i))
                {
                    return;
                }
            }
        }
    }

    class ANSamDetector : BaseDetector
    {
        private const double DetectThreshold = 10;

        private int samplesOverThreshold = -1;

        public ANSamDetector(int sampleRate) : base(sampleRate, 2100, 30, true) { }

        protected override bool ProcessSample(double pllFreq, double pllLock, byte[] bytes, int byteCount, int i)
        {
            if (pllLock < DetectThreshold)
            {
                ++samplesOverThreshold;
                if (samplesOverThreshold > sampleRate / 2)
                {
                    byte[] buffer = new byte[byteCount - i];
                    Array.Copy(bytes, i, buffer, 0, byteCount - i);
                    DetectANSam(this, new DetectANSAmHandlerEventArgs(buffer));
                    return true;
                }
            }
            else
            {
                samplesOverThreshold = 0;
            }
            return false;
        }

        public delegate void DetectANSamHandler(object sender, DetectANSAmHandlerEventArgs e);
        public event DetectANSamHandler DetectANSam;

        public class DetectANSAmHandlerEventArgs : EventArgs
        {
            public byte[] Buffer { get; private set; }
            public DetectANSAmHandlerEventArgs(byte[] buffer)
            {
                Buffer = buffer;
            }
        }
    }

    class BiFSKDetector : BaseDetector
    {
        private const double DetectThreshold = 10;

        private int samplesPerBitNominal;
        private double samplesPerBitActual;
        private int samplesSinceTransition;
        private int samplesSinceLastBit;
        private bool lastBitSeen;

        public BiFSKDetector(int sampleRate, int freq)
            : base(sampleRate, freq, 200, false)
        {
            this.samplesPerBitNominal = sampleRate / 300;
            this.samplesPerBitActual = samplesPerBitNominal;
            this.samplesSinceTransition = -1;
            this.samplesSinceLastBit = -1;
            this.lastBitSeen = true;
        }

        protected override bool ProcessSample(double pllFreq, double pllLock, byte[] bytes, int byteCount, int i)
        {
            var isBitOne = pllFreq < 0;
            if (samplesSinceLastBit >= samplesPerBitActual)
            {
                OnDetectTone(isBitOne);
                samplesSinceLastBit = 0;
            }
            else if (samplesSinceLastBit >= 0)
            {
                ++samplesSinceLastBit;
            }

            if (isBitOne != lastBitSeen)
            {
                lastBitSeen = isBitOne;
                var numBits = Math.Round((double)samplesSinceTransition / samplesPerBitActual);
                var samplesPerBit = samplesSinceTransition / numBits;
                if (samplesSinceTransition < 0 || numBits > 10 || samplesPerBit > samplesPerBitNominal * 1.12)
                {
                    samplesSinceTransition = 0;
                    samplesSinceLastBit = (int)(samplesPerBitActual / 2);
                }
                else if (samplesPerBit > samplesPerBitNominal * 0.88)
                {
                    samplesPerBitActual = (3 * samplesPerBitActual + samplesPerBit) / 4;
                    samplesSinceTransition = 0;
                    samplesSinceLastBit = (int)(samplesPerBitActual / 2);
                }
            }
            else if (samplesSinceTransition >= 0)
            {
                ++samplesSinceTransition;
            }
            return false;
        }

        public delegate void DetectToneHandler(object sender, DetectToneEventArgs e);
        public event DetectToneHandler DetectTone;

        private void OnDetectTone(bool toneForOne)
        {
            DetectTone(this, new DetectToneEventArgs(toneForOne));
        }

        public class DetectToneEventArgs : EventArgs
        {
            public bool ToneForOne { get; private set; }
            public DetectToneEventArgs(bool toneForOne)
            {
                ToneForOne = toneForOne;
            }
        }
    }

    class DoubleFilter
    {
        private double alpha;
        private double y;
        public DoubleFilter(int sampleRate, double rc)
        {
            this.alpha = 2 * Math.PI * rc / (sampleRate + 2 * Math.PI * rc);
            this.y = 0;
        }
        public double Add(double x)
        {
            y += alpha * (x - y);
            return y;
        }
    }

    class ComplexFilter
    {
        private double alpha;
        private Complex y;
        public ComplexFilter(int sampleRate, double rc)
        {
            this.alpha = 2 * Math.PI * rc / (sampleRate + 2 * Math.PI * rc);
            this.y = Complex.Zero;
        }
        public Complex Add(Complex x)
        {
            y += alpha * (x - y);
            return y;
        }
    }

}
