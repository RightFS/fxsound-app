using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FxSoundDotNet
{
    // Sound device structure for interop
    [StructLayout(LayoutKind.Sequential)]
    public struct FxSoundDevice
    {
        public IntPtr DeviceId;
        public IntPtr FriendlyName;
        public IntPtr Description;
        public int IsCaptureDevice;
        public int IsPlaybackDevice;
        public int IsDefaultDevice;
        public int IsRealDevice;
        public int IsDFXDevice;
        public int NumChannels;

        // Helper properties to convert from unmanaged to managed strings
        public string DeviceIdString => Marshal.PtrToStringUni(DeviceId);
        public string FriendlyNameString => Marshal.PtrToStringUni(FriendlyName);
        public string DescriptionString => Marshal.PtrToStringUni(Description);
    }

    // Preset structure for interop
    [StructLayout(LayoutKind.Sequential)]
    public struct FxPreset
    {
        public IntPtr FullPath;
        public IntPtr Name;

        // Helper properties to convert from unmanaged to managed strings
        public string FullPathString => Marshal.PtrToStringUni(FullPath);
        public string NameString => Marshal.PtrToStringUni(Name);
    }

    // Audio effects enum
    public enum FxEffect
    {
        Fidelity = 0,
        Ambience = 1,
        Surround = 2,
        DynamicBoost = 3,
        Bass = 4
    }

    // Callback delegate for device change notifications
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FxDeviceChangeCallback(IntPtr devices, int deviceCount);

    // Main wrapper class for the FxWrapper DLL
    public class FxSoundApi : IDisposable
    {
        // Constants
        private const int FX_SUCCESS = 0;
        private const int FX_ERROR_GENERAL = -1;
        private const int FX_ERROR_INVALID_PARAM = -2;
        private const int FX_ERROR_NOT_INITIALIZED = -3;
        private const int FX_ERROR_MEMORY = -4;

        // Flag to track whether we've initialized the library
        private bool _initialized = false;
        private bool _disposed = false;

        // Keep device change callback alive to prevent garbage collection
        private FxDeviceChangeCallback _deviceChangeCallback;
        private Action<List<FxSoundDevice>> _managedDeviceChangeCallback;

        // Constructor - initialize the library
        public FxSoundApi()
        {
            Initialize();
        }

        // Initialize the DSP and Audio modules
        public void Initialize()
        {
            if (_initialized) return;

            int dspResult = FxDspInitialize();
            if (dspResult != FX_SUCCESS)
            {
                throw new Exception($"Failed to initialize DSP module. Error code: {dspResult}");
            }

            int audioResult = FxAudioInitialize();
            if (audioResult != FX_SUCCESS)
            {
                FxDspCleanup();
                throw new Exception($"Failed to initialize Audio module. Error code: {audioResult}");
            }

            // Connect DSP to Audio
            FxAudioSetDspProcessingModule();

            _initialized = true;
        }

        // Clean up resources
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Dispose pattern implementation
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            // Free unmanaged resources
            FxAudioCleanup();
            FxDspCleanup();

            _disposed = true;
        }

        // Finalizer
        ~FxSoundApi()
        {
            Dispose(false);
        }

        // ===== Audio Module Methods =====

        // Mute/unmute audio
        public void Mute(bool mute)
        {
            FxAudioMute(mute ? 1 : 0);
        }

        // Get available sound devices
        public List<FxSoundDevice> GetSoundDevices()
        {
            IntPtr devicesPtr = IntPtr.Zero;
            int deviceCount = 0;

            int result = FxAudioGetSoundDevices(ref devicesPtr, ref deviceCount);
            if (result != FX_SUCCESS)
            {
                throw new Exception($"Failed to get sound devices. Error code: {result}");
            }

            List<FxSoundDevice> devices = new List<FxSoundDevice>();
            if (deviceCount > 0 && devicesPtr != IntPtr.Zero)
            {
                try
                {
                    // Marshal the array of structures
                    int structSize = Marshal.SizeOf<FxSoundDevice>();
                    for (int i = 0; i < deviceCount; i++)
                    {
                        IntPtr elementPtr = new IntPtr(devicesPtr.ToInt64() + i * structSize);
                        FxSoundDevice device = Marshal.PtrToStructure<FxSoundDevice>(elementPtr);

                        // 创建一个新的设备结构，复制所有字符串数据
                        FxSoundDevice copiedDevice = new FxSoundDevice
                        {
                            // 深拷贝字符串数据到托管内存
                            DeviceId = CopyStringToManagedMemory(device.DeviceId),
                            FriendlyName = CopyStringToManagedMemory(device.FriendlyName),
                            Description = CopyStringToManagedMemory(device.Description),

                            // 复制值类型数据
                            IsCaptureDevice = device.IsCaptureDevice,
                            IsPlaybackDevice = device.IsPlaybackDevice,
                            IsDefaultDevice = device.IsDefaultDevice,
                            IsRealDevice = device.IsRealDevice,
                            IsDFXDevice = device.IsDFXDevice,
                            NumChannels = device.NumChannels
                        };

                        devices.Add(copiedDevice);
                    }
                }
                finally
                {
                    // Free the memory allocated by the C API
                    FxAudioFreeSoundDevices(devicesPtr, deviceCount);
                }
            }

            return devices;
        }
        // 帮助方法：将非托管字符串复制到托管内存
        private IntPtr CopyStringToManagedMemory(IntPtr sourcePtr)
        {
            if (sourcePtr == IntPtr.Zero)
                return IntPtr.Zero;

            string managedString = Marshal.PtrToStringUni(sourcePtr);
            if (string.IsNullOrEmpty(managedString))
                return IntPtr.Zero;

            // 分配新的非托管内存并复制字符串
            return Marshal.StringToHGlobalUni(managedString);
        }

        // Set buffer length
        public int SetBufferLength(int bufferLengthMsecs)
        {
            return FxAudioSetBufferLength(bufferLengthMsecs);
        }

        // Process timer
        public int ProcessTimer()
        {
            return FxAudioProcessTimer();
        }

        // Set playback device
        public void SetPlaybackDevice(FxSoundDevice device)
        {
            // Create a copy of the device with marshaled strings
            FxSoundDevice marshaledDevice = new FxSoundDevice
            {
                DeviceId = Marshal.StringToHGlobalUni(device.DeviceIdString),
                FriendlyName = Marshal.StringToHGlobalUni(device.FriendlyNameString),
                Description = Marshal.StringToHGlobalUni(device.DescriptionString),
                IsCaptureDevice = device.IsCaptureDevice,
                IsPlaybackDevice = device.IsPlaybackDevice,
                IsDefaultDevice = device.IsDefaultDevice,
                IsRealDevice = device.IsRealDevice,
                IsDFXDevice = device.IsDFXDevice,
                NumChannels = device.NumChannels
            };

            try
            {
                FxAudioSetPlaybackDevice(ref marshaledDevice);
            }
            finally
            {
                // Free allocated strings
                Marshal.FreeHGlobal(marshaledDevice.DeviceId);
                Marshal.FreeHGlobal(marshaledDevice.FriendlyName);
                Marshal.FreeHGlobal(marshaledDevice.Description);
            }
        }

        // Register callback for device change notifications
        public void RegisterDeviceChangeCallback(Action<List<FxSoundDevice>> callback)
        {
            _managedDeviceChangeCallback = callback;

            if (callback == null)
            {
                FxAudioRegisterDeviceChangeCallback(null);
                _deviceChangeCallback = null;
            }
            else
            {
                _deviceChangeCallback = (devicesPtr, deviceCount) =>
                {
                    List<FxSoundDevice> devices = new List<FxSoundDevice>();
                    if (deviceCount > 0 && devicesPtr != IntPtr.Zero)
                    {
                        // Marshal the array of structures
                        int structSize = Marshal.SizeOf<FxSoundDevice>();
                        for (int i = 0; i < deviceCount; i++)
                        {
                            IntPtr elementPtr = new IntPtr(devicesPtr.ToInt64() + i * structSize);
                            FxSoundDevice device = Marshal.PtrToStructure<FxSoundDevice>(elementPtr);

                            // 创建一个新的设备结构，复制所有字符串数据
                            FxSoundDevice copiedDevice = new FxSoundDevice
                            {
                                // 深拷贝字符串数据到托管内存
                                DeviceId = CopyStringToManagedMemory(device.DeviceId),
                                FriendlyName = CopyStringToManagedMemory(device.FriendlyName),
                                Description = CopyStringToManagedMemory(device.Description),

                                // 复制值类型数据
                                IsCaptureDevice = device.IsCaptureDevice,
                                IsPlaybackDevice = device.IsPlaybackDevice,
                                IsDefaultDevice = device.IsDefaultDevice,
                                IsRealDevice = device.IsRealDevice,
                                IsDFXDevice = device.IsDFXDevice,
                                NumChannels = device.NumChannels
                            };

                            devices.Add(copiedDevice);
                        }
                    }

                    // Call the managed callback
                    _managedDeviceChangeCallback(devices);
                };

                FxAudioRegisterDeviceChangeCallback(_deviceChangeCallback);
            }
        }
        // 在 FxSoundApi 类中添加
        public void FreeDeviceStrings(FxSoundDevice device)
        {
            if (device.DeviceId != IntPtr.Zero)
                Marshal.FreeHGlobal(device.DeviceId);

            if (device.FriendlyName != IntPtr.Zero)
                Marshal.FreeHGlobal(device.FriendlyName);

            if (device.Description != IntPtr.Zero)
                Marshal.FreeHGlobal(device.Description);
        }

        // 当不再需要设备列表时调用
        public void FreeDeviceList(List<FxSoundDevice> devices)
        {
            if (devices == null)
                return;

            foreach (var device in devices)
            {
                FreeDeviceStrings(device);
            }
        }
        // Check if playback device is available
        public bool IsPlaybackDeviceAvailable()
        {
            return FxAudioIsPlaybackDeviceAvailable() != 0;
        }

        // ===== DSP Module Methods =====

        // Set signal format
        public int SetSignalFormat(int bitsPerSample, int numChannels, int sampleRate, int validBits)
        {
            return FxDspSetSignalFormat(bitsPerSample, numChannels, sampleRate, validBits);
        }

        // Process audio data
        public int ProcessAudio(short[] inputSamples, short[] outputSamples, int numSampleSets, bool checkForDuplicateBuffers)
        {
            GCHandle inputHandle = GCHandle.Alloc(inputSamples, GCHandleType.Pinned);
            GCHandle outputHandle = GCHandle.Alloc(outputSamples, GCHandleType.Pinned);

            try
            {
                return FxDspProcessAudio(
                    inputHandle.AddrOfPinnedObject(),
                    outputHandle.AddrOfPinnedObject(),
                    numSampleSets,
                    checkForDuplicateBuffers ? 1 : 0);
            }
            finally
            {
                inputHandle.Free();
                outputHandle.Free();
            }
        }

        // Load preset
        public int LoadPreset(string presetFilePath)
        {
            return FxDspLoadPreset(presetFilePath);
        }

        // Save preset
        public int SavePreset(string presetName, string presetFilePath)
        {
            return FxDspSavePreset(presetName, presetFilePath);
        }

        // Export preset
        public int ExportPreset(string sourceFilePath, string presetName, string exportPath)
        {
            return FxDspExportPreset(sourceFilePath, presetName, exportPath);
        }

        // Enable/disable equalizer
        public void EqOn(bool on)
        {
            FxDspEqOn(on ? 1 : 0);
        }

        // Get number of equalizer bands
        public int GetNumEqBands()
        {
            return FxDspGetNumEqBands();
        }

        // Get equalizer band frequency
        public float GetEqBandFrequency(int bandNum)
        {
            return FxDspGetEqBandFrequency(bandNum);
        }

        // Set equalizer band frequency
        public void SetEqBandFrequency(int bandNum, float frequency)
        {
            FxDspSetEqBandFrequency(bandNum, frequency);
        }

        // Get equalizer band frequency range
        public (float Min, float Max) GetEqBandFrequencyRange(int bandNum)
        {
            float min = 0, max = 0;
            FxDspGetEqBandFrequencyRange(bandNum, ref min, ref max);
            return (min, max);
        }

        // Get equalizer band boost/cut value
        public float GetEqBandBoostCut(int bandNum)
        {
            return FxDspGetEqBandBoostCut(bandNum);
        }

        // Set equalizer band boost/cut value
        public void SetEqBandBoostCut(int bandNum, float boost)
        {
            FxDspSetEqBandBoostCut(bandNum, boost);
        }

        // Enable/disable processing
        public void PowerOn(bool on)
        {
            FxDspPowerOn(on ? 1 : 0);
        }

        // Check if processing is enabled
        public bool IsPowerOn()
        {
            return FxDspIsPowerOn() != 0;
        }

        // Get effect value
        public float GetEffectValue(FxEffect effect)
        {
            return FxDspGetEffectValue(effect);
        }

        // Set effect value
        public void SetEffectValue(FxEffect effect, float value)
        {
            FxDspSetEffectValue(effect, value * 10);
        }

        // Get preset information
        public FxPreset GetPresetInfo(string presetFilePath)
        {
            FxPreset preset = new FxPreset();
            int result = FxDspGetPresetInfo(presetFilePath, ref preset);
            if (result != FX_SUCCESS)
            {
                throw new Exception($"Failed to get preset info. Error code: {result}");
            }
            return preset;
        }

        // Get total audio processed time
        public uint GetTotalAudioProcessedTime()
        {
            return FxDspGetTotalAudioProcessedTime();
        }

        // Reset total audio processed time
        public void ResetTotalAudioProcessedTime()
        {
            FxDspResetTotalAudioProcessedTime();
        }

        // Get spectrum band values
        public float[] GetSpectrumBandValues(int arraySize)
        {
            float[] bandValues = new float[arraySize];
            GCHandle handle = GCHandle.Alloc(bandValues, GCHandleType.Pinned);
            try
            {
                FxDspGetSpectrumBandValues(handle.AddrOfPinnedObject(), arraySize);
                return bandValues;
            }
            finally
            {
                handle.Free();
            }
        }

        // Set volume normalization
        public void SetVolumeNormalization(float targetRms)
        {
            FxDspSetVolumeNormalization(targetRms);
        }

        // ===== P/Invoke Declarations =====

        // DfxDsp functions
        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxDspInitialize();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspCleanup();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxDspSetSignalFormat(int bitsPerSample, int numChannels, int sampleRate, int validBits);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxDspProcessAudio(IntPtr inputSamples, IntPtr outputSamples, int numSampleSets, int checkForDuplicateBuffers);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int FxDspLoadPreset(string presetFilePath);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int FxDspSavePreset(string presetName, string presetFilePath);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int FxDspExportPreset(string sourceFilePath, string presetName, string exportPath);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspEqOn(int on);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxDspGetNumEqBands();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern float FxDspGetEqBandFrequency(int bandNum);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspSetEqBandFrequency(int bandNum, float frequency);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspGetEqBandFrequencyRange(int bandNum, ref float minFreq, ref float maxFreq);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern float FxDspGetEqBandBoostCut(int bandNum);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspSetEqBandBoostCut(int bandNum, float boost);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspPowerOn(int on);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxDspIsPowerOn();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern float FxDspGetEffectValue(FxEffect effect);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspSetEffectValue(FxEffect effect, float value);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int FxDspGetPresetInfo(string presetFilePath, ref FxPreset preset);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint FxDspGetTotalAudioProcessedTime();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspResetTotalAudioProcessedTime();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspGetSpectrumBandValues(IntPtr bandValues, int arraySize);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxDspSetVolumeNormalization(float targetRms);

        // AudioPassthru functions
        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxAudioInitialize();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxAudioCleanup();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxAudioMute(int mute);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxAudioGetSoundDevices(ref IntPtr devices, ref int deviceCount);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxAudioFreeSoundDevices(IntPtr devices, int deviceCount);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxAudioSetBufferLength(int bufferLengthMsecs);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxAudioProcessTimer();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxAudioSetDspProcessingModule();

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxAudioSetPlaybackDevice(ref FxSoundDevice device);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxAudioRegisterDeviceChangeCallback(FxDeviceChangeCallback callback);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FxAudioIsPlaybackDeviceAvailable();

        // Utility functions
        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxFreeString(IntPtr str);

        [DllImport("FxWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FxFreePreset(ref FxPreset preset);
    }
}