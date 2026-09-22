using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Soundboard
{
    public partial class MainWindow : Window
    {
        private const string ClipFolder = @"C:\SoundEffects";
        private static readonly string[] Extensions =
            { ".mp3", ".wav", ".m4a", ".AAC", ".ogg", ".flac" };

        private readonly List<string> _deviceIds = new();
        private readonly ObservableCollection<Clip> _clips = new();

        private WasapiOut? _output;
        private AudioFileReader? _reader;
        private HotkeyManager? _hotkeys;

        public MainWindow()
        {
            InitializeComponent();
            ClipList.ItemsSource = _clips;
            LoadDevices();
            LoadClips();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                _hotkeys = new HotkeyManager(this);
                BindHotkeys();
            }
            catch (Exception ex)
            {
                HotkeyText.Text = $"Hotkey init failed: {ex.Message}";
            }
        }

        private void LoadDevices()
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var d in enumerator.EnumerateAudioEndPoints(
                         DataFlow.Render, DeviceState.Active))
            {
                _deviceIds.Add(d.ID);
                DeviceBox.Items.Add(d.FriendlyName);
                d.Dispose();
            }

            var names = DeviceBox.Items.Cast<string>().ToList();

            var vm = names.FindIndex(n =>
                n.StartsWith("VoiceMeeter Input", StringComparison.OrdinalIgnoreCase));

            if (vm < 0)
                vm = names.FindIndex(n =>
                    n.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase));

            DeviceBox.SelectedIndex = vm >= 0 ? vm : 0;
        }

        private void LoadClips()
        {
            _clips.Clear();

            if (!Directory.Exists(ClipFolder))
            {
                StatusText.Text = $"Folder not found: {ClipFolder}";
                return;
            }

            var files = Directory.EnumerateFiles(ClipFolder)
                .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f);

            foreach (var f in files)
                _clips.Add(new Clip(f));

            StatusText.Text = $"{_clips.Count} clips loaded from {ClipFolder}";
        }

        private void BindHotkeys()
        {
            if (_hotkeys is null)
            {
                HotkeyText.Text = "Hotkey manager not initialized.";
                return;
            }

            _hotkeys.Clear();

            var bound = new List<string>();
            var failed = new List<string>();

            foreach (var clip in _clips)
            {
                if (clip.Hotkey is null) continue;
                var captured = clip;

                if (_hotkeys.Register(clip.Hotkey, () => Play(captured)))
                    bound.Add($"{clip.Hotkey}\u2192{clip.Name}");
                else
                    failed.Add(clip.Hotkey);
            }

            var parts = new List<string>();
            if (bound.Count > 0)
                parts.Add($"Bound: {string.Join("  ", bound)}");
            if (failed.Count > 0)
                parts.Add($"UNAVAILABLE: {string.Join(", ", failed)}");
            if (bound.Count == 0 && failed.Count == 0)
                parts.Add($"No hotkeys parsed from {_clips.Count} filenames.");

            HotkeyText.Text = string.Join("   |   ", parts);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadClips();
            BindHotkeys();
        }

        private void Clip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.DataContext is not Clip clip) return;
            Play(clip);
        }

        private void Play(Clip clip)
        {
            StopCurrent();

            if (DeviceBox.SelectedIndex < 0) return;

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(_deviceIds[DeviceBox.SelectedIndex]);

                _reader = new AudioFileReader(clip.Path);
                _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 200);
                _output.Init(_reader);
                _output.Play();

                StatusText.Text = $"Playing: {clip.Name}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed: {ex.Message}";
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
            _hotkeys?.Dispose();
            StopCurrent();
            base.OnClosed(e);
        }
    }
}