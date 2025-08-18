using Microsoft.Win32;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FootStepBoost
{
    public partial class MainWindow : Window
    {
        private FxSoundDotNet.FxSoundApi _fxSoundApi;
        private List<FxSoundDotNet.FxSoundDevice> _devices;
        private DispatcherTimer _spectrumTimer;
        private DispatcherTimer _audioProcessTimer;
        private bool _updatingSliders = false;

        public MainWindow()
        {
            InitializeComponent();

            // 初始化音频处理定时器
            _audioProcessTimer = new DispatcherTimer();
            _audioProcessTimer.Interval = TimeSpan.FromMilliseconds(100); // 与 FxController.cpp 相同的间隔
            _audioProcessTimer.Tick += AudioProcessTimer_Tick;

            // Initialize the timer for spectrum updates
            _spectrumTimer = new DispatcherTimer();
            _spectrumTimer.Interval = TimeSpan.FromMilliseconds(50);
            _spectrumTimer.Tick += SpectrumTimer_Tick;

            // Disable the stop button initially
            StopSpectrumButton.IsEnabled = false;
        }
        private void AudioProcessTimer_Tick(object sender, EventArgs e)
        {
            if (_fxSoundApi != null)
            {
                // 调用 processTimer 处理音频
                _fxSoundApi.ProcessTimer();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 初始化 FxSound API
                _fxSoundApi = new FxSoundDotNet.FxSoundApi();

                // 设置初始信号格式 - 非常重要
                _fxSoundApi.SetSignalFormat(16, 2, 44100, 16);

                // 注册设备更改回调
                _fxSoundApi.RegisterDeviceChangeCallback(OnDeviceChanged);

                // 加载可用设备
                RefreshDeviceList();

                // 设置均衡器
                SetupEqualizer();

                // 设置默认效果值
                UpdateEffectSliders();

                // 更新电源按钮
                PowerToggle.IsChecked = _fxSoundApi.IsPowerOn();

                // 启动音频处理定时器
                _audioProcessTimer.Start();

                StatusTextBlock.Text = "FxSound initialized successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing FxSound: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop the spectrum timer if it's running
            _spectrumTimer.Stop();

            // Clean up the FxSound API
            _fxSoundApi?.Dispose();
        }

        private void RefreshDeviceList()
        {
            try
            {
                // Get available sound devices
                _devices = _fxSoundApi.GetSoundDevices();

                _devices.RemoveAll(d=>d.IsRealDevice == 0);
                // Clear the combo box
                DevicesComboBox.Items.Clear();

                // Add devices to the combo box
                foreach (var device in _devices)
                {
                    if (device.IsRealDevice == 1)
                    {
                        DevicesComboBox.Items.Add(device.FriendlyNameString);
                    }
                }

                // Select the first device if available
                if (DevicesComboBox.Items.Count > 0)
                {
                    DevicesComboBox.SelectedIndex = 0;
                }
                else
                {
                    DeviceInfoTextBlock.Text = "No audio devices found";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error getting devices: {ex.Message}";
            }
        }

        private void UpdateEffectSliders()
        {
            if (_fxSoundApi == null) return;

            _updatingSliders = true;

            // Set slider values based on current effect values
            FidelitySlider.Value = _fxSoundApi.GetEffectValue(FxSoundDotNet.FxEffect.Fidelity) * 100;
            FidelityValueText.Text = $"{FidelitySlider.Value:F0}%";

            AmbienceSlider.Value = _fxSoundApi.GetEffectValue(FxSoundDotNet.FxEffect.Ambience) * 100;
            AmbienceValueText.Text = $"{AmbienceSlider.Value:F0}%";

            SurroundSlider.Value = _fxSoundApi.GetEffectValue(FxSoundDotNet.FxEffect.Surround) * 100;
            SurroundValueText.Text = $"{SurroundSlider.Value:F0}%";

            DynamicBoostSlider.Value = _fxSoundApi.GetEffectValue(FxSoundDotNet.FxEffect.DynamicBoost) * 100;
            DynamicBoostValueText.Text = $"{DynamicBoostSlider.Value:F0}%";

            BassSlider.Value = _fxSoundApi.GetEffectValue(FxSoundDotNet.FxEffect.Bass) * 100;
            BassValueText.Text = $"{BassSlider.Value:F0}%";

            _updatingSliders = false;
        }

        private void SetupEqualizer()
        {
            if (_fxSoundApi == null) return;

            try
            {
                int numBands = _fxSoundApi.GetNumEqBands();
                if (numBands <= 0) return;

                // Set the initial state of the equalizer checkbox
                EqEnabledCheckBox.IsChecked = _fxSoundApi.IsPowerOn();

                // Clear any existing bands
                EqBandsPanel.Children.Clear();

                // Create a vertical slider for each band
                for (int i = 0; i < numBands; i++)
                {
                    float freq = _fxSoundApi.GetEqBandFrequency(i);
                    float boost = _fxSoundApi.GetEqBandBoostCut(i);

                    StackPanel bandPanel = new StackPanel();
                    bandPanel.Margin = new Thickness(5);
                    bandPanel.Width = 50;

                    // Create the slider
                    Slider slider = new Slider();
                    slider.Orientation = Orientation.Vertical;
                    slider.Minimum = -12;
                    slider.Maximum = 12;
                    slider.Value = boost;
                    slider.Height = 150;
                    slider.TickFrequency = 3;
                    slider.TickPlacement = System.Windows.Controls.Primitives.TickPlacement.Both;
                    slider.Tag = i; // Store the band number
                    slider.ValueChanged += EqBandSlider_ValueChanged;

                    // Create the frequency label
                    TextBlock freqLabel = new TextBlock();
                    freqLabel.Text = freq < 1000 ? $"{freq:F0} Hz" : $"{freq / 1000:F1} kHz";
                    freqLabel.HorizontalAlignment = HorizontalAlignment.Center;

                    // Create the boost/cut value label
                    TextBlock boostLabel = new TextBlock();
                    boostLabel.Text = $"{boost:F1} dB";
                    boostLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    boostLabel.Tag = $"eqLabel_{i}";

                    // Add controls to the panel
                    bandPanel.Children.Add(boostLabel);
                    bandPanel.Children.Add(slider);
                    bandPanel.Children.Add(freqLabel);

                    // Add the panel to the bands panel
                    EqBandsPanel.Children.Add(bandPanel);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error setting up equalizer: {ex.Message}";
            }
        }

        private void OnDeviceChanged(List<FxSoundDotNet.FxSoundDevice> devices)
        {
            // This is called from a background thread, so we need to invoke on the UI thread
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = "Audio devices changed";
                //RefreshDeviceList();
            });
        }

        private void DevicesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = DevicesComboBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _devices.Count)
            {
                try
                {
                    var device = _devices[selectedIndex];

                    // 更新设备信息
                    DeviceInfoTextBlock.Text = $"Type: {(device.IsPlaybackDevice == 1 ? "Playback" : "")} {(device.IsCaptureDevice == 1 ? "Capture" : "")}\n" +
                                               $"Default: {(device.IsDefaultDevice == 1 ? "Yes" : "No")}\n" +
                                               $"Real Device: {(device.IsRealDevice == 1 ? "Yes" : "No")}\n" +
                                               $"DFX Device: {(device.IsDFXDevice == 1 ? "Yes" : "No")}\n" +
                                               $"Channels: {device.NumChannels}";

                    // 设置所选设备为播放设备
                    _fxSoundApi.SetPlaybackDevice(device);

                    // 如果电源已开启，确保未静音
                    if (_fxSoundApi.IsPowerOn())
                    {
                        _fxSoundApi.Mute(false);
                    }

                    StatusTextBlock.Text = $"Device set to: {device.FriendlyNameString}";
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Error setting device: {ex.Message}";
                }
            }
        }

        private void PowerToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (_fxSoundApi == null) return;

            bool powerOn = PowerToggle.IsChecked == true;
            _fxSoundApi.PowerOn(powerOn);
            _fxSoundApi.Mute(false);
            StatusTextBlock.Text = powerOn ? "Power On" : "Power Off";
        }

        private void FidelitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_fxSoundApi == null || _updatingSliders) return;

            float value = (float)(FidelitySlider.Value / 100.0);
            _fxSoundApi.SetEffectValue(FxSoundDotNet.FxEffect.Fidelity, value);
            FidelityValueText.Text = $"{FidelitySlider.Value:F0}%";
            StatusTextBlock.Text = $"Fidelity set to {FidelitySlider.Value:F0}%";
        }

        private void AmbienceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_fxSoundApi == null || _updatingSliders) return;

            float value = (float)(AmbienceSlider.Value / 100.0);
            _fxSoundApi.SetEffectValue(FxSoundDotNet.FxEffect.Ambience, value);
            AmbienceValueText.Text = $"{AmbienceSlider.Value:F0}%";
            StatusTextBlock.Text = $"Ambience set to {AmbienceSlider.Value:F0}%";
        }

        private void SurroundSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_fxSoundApi == null || _updatingSliders) return;

            float value = (float)(SurroundSlider.Value / 100.0);
            _fxSoundApi.SetEffectValue(FxSoundDotNet.FxEffect.Surround, value);
            SurroundValueText.Text = $"{SurroundSlider.Value:F0}%";
            StatusTextBlock.Text = $"Surround set to {SurroundSlider.Value:F0}%";
        }

        private void DynamicBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_fxSoundApi == null || _updatingSliders) return;

            float value = (float)(DynamicBoostSlider.Value / 100.0);
            _fxSoundApi.SetEffectValue(FxSoundDotNet.FxEffect.DynamicBoost, value);
            DynamicBoostValueText.Text = $"{DynamicBoostSlider.Value:F0}%";
            StatusTextBlock.Text = $"Dynamic Boost set to {DynamicBoostSlider.Value:F0}%";
        }

        private void BassSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_fxSoundApi == null || _updatingSliders) return;

            float value = (float)(BassSlider.Value / 100.0);
            _fxSoundApi.SetEffectValue(FxSoundDotNet.FxEffect.Bass, value);
            BassValueText.Text = $"{BassSlider.Value:F0}%";
            StatusTextBlock.Text = $"Bass set to {BassSlider.Value:F0}%";
        }

        private void EqBandSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_fxSoundApi == null) return;

            Slider slider = sender as Slider;
            if (slider == null) return;

            int bandNum = (int)slider.Tag;
            float boost = (float)slider.Value;

            _fxSoundApi.SetEqBandBoostCut(bandNum, boost);

            // Update the boost/cut label
            StackPanel parent = slider.Parent as StackPanel;
            if (parent != null)
            {
                foreach (var child in parent.Children)
                {
                    if (child is TextBlock tb && tb.Tag != null && tb.Tag.ToString() == $"eqLabel_{bandNum}")
                    {
                        tb.Text = $"{boost:F1} dB";
                        break;
                    }
                }
            }

            StatusTextBlock.Text = $"EQ Band {bandNum + 1} set to {boost:F1} dB";
        }

        private void EqEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_fxSoundApi == null) return;

            bool enabled = EqEnabledCheckBox.IsChecked == true;
            _fxSoundApi.EqOn(enabled);
            StatusTextBlock.Text = enabled ? "Equalizer enabled" : "Equalizer disabled";
        }

        private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fxSoundApi == null) return;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = ".fac";
            dlg.Filter = "FxSound Config (*.fac)|*.fac|All Files (*.*)|*.*";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    int result = _fxSoundApi.LoadPreset(dlg.FileName);
                    if (result == 0)
                    {
                        StatusTextBlock.Text = "Preset loaded successfully";
                        UpdateEffectSliders();
                    }
                    else
                    {
                        StatusTextBlock.Text = $"Failed to load preset. Error code: {result}";
                    }
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Error loading preset: {ex.Message}";
                }
            }
        }

        private void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fxSoundApi == null) return;

            // 首先获取预设名称
            var inputDialog = new InputDialog("Enter a name for the preset:", "Save Preset");
            if (inputDialog.ShowDialog() == true)
            {
                string presetName = inputDialog.ResponseText;
                if (string.IsNullOrWhiteSpace(presetName))
                {
                    MessageBox.Show("Preset name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 使用FolderBrowserDialog代替SaveFileDialog来选择目录
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "Select folder to save preset";
                folderDialog.ShowNewFolderButton = true;

                // 需要添加引用: System.Windows.Forms
                System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                {
                    string exportDirectory = folderDialog.SelectedPath;

                    try
                    {
                        // 调用SavePreset，传入预设名称和目录路径
                        int saveResult = _fxSoundApi.SavePreset(presetName, exportDirectory);
                        if (saveResult == 0)
                        {
                            StatusTextBlock.Text = "Preset saved successfully";
                        }
                        else
                        {
                            StatusTextBlock.Text = $"Failed to save preset. Error code: {saveResult}";
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusTextBlock.Text = $"Error saving preset: {ex.Message}";
                    }
                }
            }
        }

        private void StartSpectrumButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fxSoundApi == null) return;

            // Configure the signal format for processing (this is just for demonstration)
            _fxSoundApi.SetSignalFormat(16, 2, 44100, 16);

            // Start the timer to update the spectrum
            _spectrumTimer.Start();

            // Update button states
            StartSpectrumButton.IsEnabled = false;
            StopSpectrumButton.IsEnabled = true;

            StatusTextBlock.Text = "Spectrum visualization started";
        }

        private void StopSpectrumButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop the timer
            _spectrumTimer.Stop();

            // Update button states
            StartSpectrumButton.IsEnabled = true;
            StopSpectrumButton.IsEnabled = false;

            StatusTextBlock.Text = "Spectrum visualization stopped";
        }

        private void SpectrumTimer_Tick(object sender, EventArgs e)
        {
            if (_fxSoundApi == null) return;

            try
            {
                // Get spectrum data (32 bands)
                float[] spectrumData = _fxSoundApi.GetSpectrumBandValues(10);

                // Clear the canvas
                SpectrumCanvas.Children.Clear();

                // Draw the spectrum
                DrawSpectrum(spectrumData);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error updating spectrum: {ex.Message}";
                _spectrumTimer.Stop();
            }
        }

        private void DrawSpectrum(float[] spectrumData)
        {
            if (spectrumData == null || spectrumData.Length == 0) return;

            double canvasWidth = SpectrumCanvas.ActualWidth;
            double canvasHeight = SpectrumCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            double barWidth = canvasWidth / spectrumData.Length;

            for (int i = 0; i < spectrumData.Length; i++)
            {
                // Normalize the value (assuming it's in the range 0-1)
                double value = Math.Min(1.0, Math.Max(0.0, spectrumData[i]));

                // Calculate bar height (inverted since Y increases downward)
                double barHeight = canvasHeight * value;

                // Create a rectangle for the bar
                Rectangle bar = new Rectangle();
                bar.Width = barWidth - 2; // Leave a small gap between bars
                bar.Height = barHeight;

                // Position the bar
                Canvas.SetLeft(bar, i * barWidth);
                Canvas.SetTop(bar, canvasHeight - barHeight);

                // Color the bar based on its height
                bar.Fill = new LinearGradientBrush(
                    GetColorFromValue(value),
                    Colors.Transparent,
                    new Point(0.5, 0),
                    new Point(0.5, 1));

                // Add the bar to the canvas
                SpectrumCanvas.Children.Add(bar);
            }
        }

        private Color GetColorFromValue(double value)
        {
            // Create a gradient from blue (low) to green (mid) to red (high)
            if (value < 0.5)
            {
                // Blend from blue to green
                double blend = value * 2; // 0-0.5 maps to 0-1
                return Color.FromRgb(
                    (byte)(0 * (1 - blend) + 0 * blend),
                    (byte)(0 * (1 - blend) + 255 * blend),
                    (byte)(255 * (1 - blend) + 0 * blend));
            }
            else
            {
                // Blend from green to red
                double blend = (value - 0.5) * 2; // 0.5-1.0 maps to 0-1
                return Color.FromRgb(
                    (byte)(0 * (1 - blend) + 255 * blend),
                    (byte)(255 * (1 - blend) + 0 * blend),
                    (byte)(0 * (1 - blend) + 0 * blend));
            }
        }
    }

    // A simple input dialog for getting the preset name
    public class InputDialog : Window
    {
        private TextBox _textBox;

        public string ResponseText
        {
            get { return _textBox.Text; }
        }

        public InputDialog(string question, string title)
        {
            Title = title;
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            Grid grid = new Grid();
            grid.Margin = new Thickness(10);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock questionText = new TextBlock { Text = question, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(questionText, 0);

            _textBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(_textBox, 1);

            StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            Button okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0) };
            okButton.Click += (s, e) => { DialogResult = true; };

            Button cancelButton = new Button { Content = "Cancel", Width = 75 };
            cancelButton.Click += (s, e) => { DialogResult = false; };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(questionText);
            grid.Children.Add(_textBox);
            grid.Children.Add(buttonPanel);

            Content = grid;

            _textBox.Focus();
        }
    }
}