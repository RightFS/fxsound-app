#pragma once

#ifdef FXWRAPPER_EXPORTS
#define FXWRAPPER_API __declspec(dllexport)
#else
#define FXWRAPPER_API __declspec(dllimport)
#endif

// Define C-compatible types for our API
#ifdef __cplusplus
extern "C" {
#endif

// Sound device structure for C API
typedef struct {
    wchar_t* deviceId;
    wchar_t* friendlyName;
    wchar_t* description;
    int isCaptureDevice;
    int isPlaybackDevice;
    int isDefaultDevice;
    int isRealDevice;
    int isDFXDevice;
    int numChannels;
} FxSoundDevice;

// Preset structure for C API
typedef struct {
    wchar_t* fullPath;
    wchar_t* name;
} FxPreset;

// Callback type for device change notifications
typedef void (*FxDeviceChangeCallback)(FxSoundDevice* devices, int deviceCount);

// Error codes
#define FX_SUCCESS 0
#define FX_ERROR_GENERAL -1
#define FX_ERROR_INVALID_PARAM -2
#define FX_ERROR_NOT_INITIALIZED -3
#define FX_ERROR_MEMORY -4

// Audio effects enum
typedef enum {
    FX_EFFECT_FIDELITY = 0,
    FX_EFFECT_AMBIENCE = 1,
    FX_EFFECT_SURROUND = 2,
    FX_EFFECT_DYNAMIC_BOOST = 3,
    FX_EFFECT_BASS = 4
} FxEffect;

// ===== DfxDsp API =====

// Initialize the DSP module
FXWRAPPER_API int FxDspInitialize();

// Clean up the DSP module
FXWRAPPER_API void FxDspCleanup();

// Configure signal format
FXWRAPPER_API int FxDspSetSignalFormat(int bitsPerSample, int numChannels, int sampleRate, int validBits);

// Process audio data
FXWRAPPER_API int FxDspProcessAudio(short* inputSamples, short* outputSamples, int numSampleSets, int checkForDuplicateBuffers);

// Load preset from file
FXWRAPPER_API int FxDspLoadPreset(const wchar_t* presetFilePath);

// Save preset to file
FXWRAPPER_API int FxDspSavePreset(const wchar_t* presetName, const wchar_t* presetFilePath);

// Export preset to file
FXWRAPPER_API int FxDspExportPreset(const wchar_t* sourceFilePath, const wchar_t* presetName, const wchar_t* exportPath);

// Enable/disable equalizer
FXWRAPPER_API void FxDspEqOn(int on);

// Get number of equalizer bands
FXWRAPPER_API int FxDspGetNumEqBands();

// Get equalizer band frequency
FXWRAPPER_API float FxDspGetEqBandFrequency(int bandNum);

// Set equalizer band frequency
FXWRAPPER_API void FxDspSetEqBandFrequency(int bandNum, float frequency);

// Get equalizer band frequency range
FXWRAPPER_API void FxDspGetEqBandFrequencyRange(int bandNum, float* minFreq, float* maxFreq);

// Get equalizer band boost/cut value
FXWRAPPER_API float FxDspGetEqBandBoostCut(int bandNum);

// Set equalizer band boost/cut value
FXWRAPPER_API void FxDspSetEqBandBoostCut(int bandNum, float boost);

// Enable/disable processing
FXWRAPPER_API void FxDspPowerOn(int on);

// Check if processing is enabled
FXWRAPPER_API int FxDspIsPowerOn();

// Get effect value
FXWRAPPER_API float FxDspGetEffectValue(FxEffect effect);

// Set effect value
FXWRAPPER_API void FxDspSetEffectValue(FxEffect effect, float value);

// Get preset information
FXWRAPPER_API int FxDspGetPresetInfo(const wchar_t* presetFilePath, FxPreset* preset);

// Get total audio processed time
FXWRAPPER_API unsigned long FxDspGetTotalAudioProcessedTime();

// Reset total audio processed time
FXWRAPPER_API void FxDspResetTotalAudioProcessedTime();

// Get spectrum band values
FXWRAPPER_API void FxDspGetSpectrumBandValues(float* bandValues, int arraySize);

// Set volume normalization
FXWRAPPER_API void FxDspSetVolumeNormalization(float targetRms);

// ===== AudioPassthru API =====

// Initialize the audio passthru module
FXWRAPPER_API int FxAudioInitialize();

// Clean up the audio passthru module
FXWRAPPER_API void FxAudioCleanup();

// Mute/unmute audio
FXWRAPPER_API void FxAudioMute(int mute);

// Get sound devices
FXWRAPPER_API int FxAudioGetSoundDevices(FxSoundDevice** devices, int* deviceCount);

// Free sound devices array allocated by FxAudioGetSoundDevices
FXWRAPPER_API void FxAudioFreeSoundDevices(FxSoundDevice* devices, int deviceCount);

// Set buffer length
FXWRAPPER_API int FxAudioSetBufferLength(int bufferLengthMsecs);

// Process timer
FXWRAPPER_API int FxAudioProcessTimer();

// Connect DSP module to audio module
FXWRAPPER_API void FxAudioSetDspProcessingModule();

// Set playback device
FXWRAPPER_API void FxAudioSetPlaybackDevice(const FxSoundDevice* device);

// Register callback for device change notifications
FXWRAPPER_API void FxAudioRegisterDeviceChangeCallback(FxDeviceChangeCallback callback);

// Check if playback device is available
FXWRAPPER_API int FxAudioIsPlaybackDeviceAvailable();

// Free allocated memory for strings
FXWRAPPER_API void FxFreeString(wchar_t* str);

// Free allocated memory for a preset
FXWRAPPER_API void FxFreePreset(FxPreset* preset);

#ifdef __cplusplus
}
#endif