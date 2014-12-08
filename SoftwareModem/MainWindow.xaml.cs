// Copyright 2014 Jacobo Tarrío Barreiro. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Windows;

namespace SoftwareModem
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Modem modem;
        private WasapiCapture capture;
        private WaveOutEvent waveOut;

        public MainWindow()
        {
            InitializeComponent();
            ScanDevices();
        }

        private void CallButton_Click(object sender, RoutedEventArgs e)
        {
            modem = new Modem();
            capture = GetCaptureDevice();
            waveOut = new WaveOutEvent();
            var playProvider = modem.Call(capture);
            modem.ByteReceived += modem_ByteReceived;
            waveOut.Init(playProvider);
            waveOut.Play();
            capture.StartRecording();
            HangupButton.IsEnabled = true;
            CallButton.IsEnabled = false;
            AnswerButton.IsEnabled = false;
        }

        private void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            modem = new Modem();
            capture = GetCaptureDevice();
            waveOut = new WaveOutEvent();
            var playProvider = modem.Answer(capture);
            modem.ByteReceived += modem_ByteReceived;
            waveOut.Init(playProvider);
            waveOut.Play();
            capture.StartRecording();
            HangupButton.IsEnabled = true;
            CallButton.IsEnabled = false;
            AnswerButton.IsEnabled = false;
        }

        private void HangupButton_Click(object sender, RoutedEventArgs e)
        {
            modem.Hangup();
            capture.StopRecording();
            waveOut.Stop();
            capture.Dispose();
            waveOut.Dispose();
            capture = null;
            waveOut = null;
            HangupButton.IsEnabled = false;
            CallButton.IsEnabled = true;
            AnswerButton.IsEnabled = true;
        }

        private void modem_ByteReceived(object sender, ByteReceivedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => AddCharacter(e.Byte)));
        }

        private void AddCharacter(byte b)
        {
            OutputBox.Text += Char.ConvertFromUtf32(b);
        }

        private void ScanDevices()
        {
            var enumerator = new MMDeviceEnumerator();
            lineInBox.ItemsSource = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            var selectedInput = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            for (var i = 0; i < lineInBox.Items.Count; ++i)
            {
                if ((lineInBox.Items[i] as MMDevice).ID == selectedInput.ID)
                {
                    lineInBox.SelectedIndex = i;
                }
            }
        }

        private WasapiCapture GetCaptureDevice()
        {
            var device = lineInBox.SelectedItem as MMDevice;
            var capture = new NAudio.CoreAudioApi.WasapiCapture(device);
            capture.ShareMode = AudioClientShareMode.Shared;
            capture.WaveFormat = new WaveFormat();
            return capture;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var bytes = new byte[InputBox.Text.Length];
            for (int i = 0; i < InputBox.Text.Length; ++i)
            {
                var chr = Char.ConvertToUtf32(InputBox.Text, i);
                if (chr < 256)
                {
                    bytes[i] = (byte)chr;
                }
                else
                {
                    bytes[i] = (byte)'?';
                }
            }
            modem.SendData(bytes);
        }
    }
}
