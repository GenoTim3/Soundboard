using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Soundboard
{
    public partial class MainWindow : Window
    {
        private readonly List<string> _deviceIds = new();
        private string? _clipPath;
        private WasapiOut? _output;
        private AudioFileReader? _reader;

        public MainWindow()
        {
            InitializeComponent();
            LoadDevices();
        }

        private void LoadDevices()
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .ToList();

            foreach (var d in devices)
            {
                _deviceIds.Add(d.ID);
                DeviceBox.Items.Add(d.FriendlyName);
                d.Dispose();
            }

            if (DeviceBox.Items.Count > 0)
                DeviceBox.SelectedIndex = 0;
        }

        private void PickClip_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Audio files|*.mp3;*.wav;*.m4a|All files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                _clipPath = dlg.FileName;
                ClipLabel.Text = Path.GetFileName(_clipPath);
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_clipPath is null)
            {
                MessageBox.Show("Load a clip first.");
                return;
            }
            if (DeviceBox.SelectedIndex < 0) return;

            StopCurrent();

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(_deviceIds[DeviceBox.SelectedIndex]);

                _reader = new AudioFileReader(_clipPath);
                _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
                _output.Init(_reader);
                _output.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback failed: {ex.Message}");
            }
        }

        private void StopCurrent()
        {
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _reader?.Dispose();
            _reader = null;
        }

        protected override void OnClosed(EventArgs e)
        {
            StopCurrent();
            base.OnClosed(e);
        }
    }
}