// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "FxWrapper.h"
#include <string>
#include <vector>
#include <memory>

// Include the AudioPassthru and DfxDsp headers
#include "AudioPassthru.h"
#include "DfxDsp.h"
#include "sndDevices.h"
#pragma comment(lib, "Shlwapi.lib")
// Global instances of our C++ classes
static DfxDsp* g_dfxDsp = nullptr;
static AudioPassthru* g_audioPassthru = nullptr;
static FxDeviceChangeCallback g_deviceChangeCallback = nullptr;

// Helper class for AudioPassthru callback
class DeviceChangeCallbackAdapter : public AudioPassthruCallback
{
public:
	virtual void onSoundDeviceChange(std::vector<SoundDevice> sound_devices) override
	{
		if (g_deviceChangeCallback)
		{
			// Convert to C API format
			std::vector<FxSoundDevice> fxDevices;
			fxDevices.resize(sound_devices.size());

			for (size_t i = 0; i < sound_devices.size(); i++)
			{
				const SoundDevice& src = sound_devices[i];
				FxSoundDevice& dst = fxDevices[i];

				// Allocate and copy strings
				dst.deviceId = AllocateAndCopyWString(src.pwszID);
				dst.friendlyName = AllocateAndCopyWString(src.deviceFriendlyName);
				dst.description = AllocateAndCopyWString(src.deviceDescription);

				// Copy boolean values as integers
				dst.isCaptureDevice = src.isCaptureDevice ? 1 : 0;
				dst.isPlaybackDevice = src.isPlaybackDevice ? 1 : 0;
				dst.isDefaultDevice = src.isDefaultDevice ? 1 : 0;
				dst.isRealDevice = src.isRealDevice ? 1 : 0;
				dst.isDFXDevice = src.isDFXDevice ? 1 : 0;
				dst.numChannels = src.deviceNumChannel;
			}

			// Call the C callback
			g_deviceChangeCallback(fxDevices.data(), static_cast<int>(fxDevices.size()));

			// Free allocated strings
			for (auto& device : fxDevices)
			{
				free(device.deviceId);
				free(device.friendlyName);
				free(device.description);
			}
		}
	}

private:
	// Helper function to allocate and copy a wide string
	static wchar_t* AllocateAndCopyWString(const std::wstring& str)
	{
		if (str.empty()) return nullptr;

		size_t size = (str.length() + 1) * sizeof(wchar_t);
		wchar_t* result = static_cast<wchar_t*>(malloc(size));
		if (result)
		{
			wcscpy_s(result, str.length() + 1, str.c_str());
		}
		return result;
	}
};

// The callback adapter instance
static DeviceChangeCallbackAdapter* g_callbackAdapter = nullptr;

// Helper function to allocate and copy a wide string
static wchar_t* AllocateAndCopyWString(const std::wstring& str)
{
	if (str.empty()) return nullptr;

	size_t size = (str.length() + 1) * sizeof(wchar_t);
	wchar_t* result = static_cast<wchar_t*>(malloc(size));
	if (result)
	{
		wcscpy_s(result, str.length() + 1, str.c_str());
	}
	return result;
}

BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

// ===== DfxDsp API Implementation =====

FXWRAPPER_API int FxDspInitialize()
{
	if (g_dfxDsp != nullptr)
	{
		return FX_ERROR_GENERAL;
	}

	try
	{
		g_dfxDsp = new DfxDsp();
		return FX_SUCCESS;
	}
	catch (...)
	{
		return FX_ERROR_GENERAL;
	}
}

FXWRAPPER_API void FxDspCleanup()
{
	if (g_dfxDsp)
	{
		delete g_dfxDsp;
		g_dfxDsp = nullptr;
	}
}

FXWRAPPER_API int FxDspSetSignalFormat(int bitsPerSample, int numChannels, int sampleRate, int validBits)
{
	if (!g_dfxDsp) return FX_ERROR_NOT_INITIALIZED;
	return g_dfxDsp->setSignalFormat(bitsPerSample, numChannels, sampleRate, validBits);
}

FXWRAPPER_API int FxDspProcessAudio(short* inputSamples, short* outputSamples, int numSampleSets, int checkForDuplicateBuffers)
{
	if (!g_dfxDsp) return FX_ERROR_NOT_INITIALIZED;
	return g_dfxDsp->processAudio(inputSamples, outputSamples, numSampleSets, checkForDuplicateBuffers);
}

FXWRAPPER_API int FxDspLoadPreset(const wchar_t* presetFilePath)
{
	if (!g_dfxDsp) return FX_ERROR_NOT_INITIALIZED;
	if (!presetFilePath) return FX_ERROR_INVALID_PARAM;

	return g_dfxDsp->loadPreset(presetFilePath);
}

FXWRAPPER_API int FxDspSavePreset(const wchar_t* presetName, const wchar_t* presetFilePath)
{
	if (!g_dfxDsp) return FX_ERROR_NOT_INITIALIZED;
	if (!presetName || !presetFilePath) return FX_ERROR_INVALID_PARAM;

	return g_dfxDsp->savePreset(presetName, presetFilePath);
}

FXWRAPPER_API int FxDspExportPreset(const wchar_t* sourceFilePath, const wchar_t* presetName, const wchar_t* exportPath)
{
	if (!g_dfxDsp) return FX_ERROR_NOT_INITIALIZED;
	if (!sourceFilePath || !presetName || !exportPath) return FX_ERROR_INVALID_PARAM;

	return g_dfxDsp->exportPreset(sourceFilePath, presetName, exportPath);
}

FXWRAPPER_API void FxDspEqOn(int on)
{
	if (!g_dfxDsp) return;
	g_dfxDsp->eqOn(on != 0);
}

FXWRAPPER_API int FxDspGetNumEqBands()
{
	if (!g_dfxDsp) return 0;
	return g_dfxDsp->getNumEqBands();
}

FXWRAPPER_API float FxDspGetEqBandFrequency(int bandNum)
{
	if (!g_dfxDsp) return 0.0f;
	return g_dfxDsp->getEqBandFrequency(bandNum);
}

FXWRAPPER_API void FxDspSetEqBandFrequency(int bandNum, float frequency)
{
	if (!g_dfxDsp) return;
	g_dfxDsp->setEqBandFrequency(bandNum, frequency);
}

FXWRAPPER_API void FxDspGetEqBandFrequencyRange(int bandNum, float* minFreq, float* maxFreq)
{
	if (!g_dfxDsp || !minFreq || !maxFreq) return;
	g_dfxDsp->getEqBandFrequencyRange(bandNum, minFreq, maxFreq);
}

FXWRAPPER_API float FxDspGetEqBandBoostCut(int bandNum)
{
	if (!g_dfxDsp) return 0.0f;
	return g_dfxDsp->getEqBandBoostCut(bandNum);
}

FXWRAPPER_API void FxDspSetEqBandBoostCut(int bandNum, float boost)
{
	if (!g_dfxDsp) return;
	g_dfxDsp->setEqBandBoostCut(bandNum, boost);
}

FXWRAPPER_API void FxDspPowerOn(int on)
{
	if (!g_dfxDsp) return;
	g_dfxDsp->powerOn(on != 0);
}

FXWRAPPER_API int FxDspIsPowerOn()
{
	if (!g_dfxDsp) return 0;
	return g_dfxDsp->isPowerOn() ? 1 : 0;
}

FXWRAPPER_API float FxDspGetEffectValue(FxEffect effect)
{
	if (!g_dfxDsp) return 0.0f;
	return g_dfxDsp->getEffectValue(static_cast<DfxDsp::Effect>(effect));
}

FXWRAPPER_API void FxDspSetEffectValue(FxEffect effect, float value)
{
	if (!g_dfxDsp) return;
	g_dfxDsp->setEffectValue(static_cast<DfxDsp::Effect>(effect), value);
}

FXWRAPPER_API int FxDspGetPresetInfo(const wchar_t* presetFilePath, FxPreset* preset)
{
	if (!g_dfxDsp || !presetFilePath || !preset) return FX_ERROR_INVALID_PARAM;

	try
	{
		DfxPreset nativePreset = g_dfxDsp->getPresetInfo(presetFilePath);

		preset->fullPath = AllocateAndCopyWString(nativePreset.full_path);
		preset->name = AllocateAndCopyWString(nativePreset.name);

		return FX_SUCCESS;
	}
	catch (...)
	{
		return FX_ERROR_GENERAL;
	}
}

FXWRAPPER_API unsigned long FxDspGetTotalAudioProcessedTime()
{
	if (!g_dfxDsp) return 0;
	return g_dfxDsp->getTotalAudioProcessedTime();
}

FXWRAPPER_API void FxDspResetTotalAudioProcessedTime()
{
	if (!g_dfxDsp) return;
	g_dfxDsp->resetTotalAudioProcessedTime();
}

FXWRAPPER_API void FxDspGetSpectrumBandValues(float* bandValues, int arraySize)
{
	if (!g_dfxDsp || !bandValues || arraySize <= 0) return;
	g_dfxDsp->getSpectrumBandValues(bandValues, arraySize);
}

FXWRAPPER_API void FxDspSetVolumeNormalization(float targetRms)
{
	if (!g_dfxDsp) return;
	g_dfxDsp->setVolumeNormalization(targetRms);
}

// ===== AudioPassthru API Implementation =====

FXWRAPPER_API int FxAudioInitialize()
{
	if (g_audioPassthru != nullptr)
	{
		return FX_ERROR_GENERAL;
	}

	try
	{
		g_audioPassthru = new AudioPassthru();
		return g_audioPassthru->init();
	}
	catch (...)
	{
		return FX_ERROR_GENERAL;
	}
}

FXWRAPPER_API void FxAudioCleanup()
{
	if (g_callbackAdapter)
	{
		delete g_callbackAdapter;
		g_callbackAdapter = nullptr;
	}

	if (g_audioPassthru)
	{
		delete g_audioPassthru;
		g_audioPassthru = nullptr;
	}
}

FXWRAPPER_API void FxAudioMute(int mute)
{
	if (!g_audioPassthru) return;
	g_audioPassthru->mute(mute != 0);
}

FXWRAPPER_API int FxAudioGetSoundDevices(FxSoundDevice** devices, int* deviceCount)
{
	if (!g_audioPassthru || !devices || !deviceCount) return FX_ERROR_INVALID_PARAM;

	try
	{
		std::vector<SoundDevice> nativeDevices = g_audioPassthru->getSoundDevices();
		*deviceCount = static_cast<int>(nativeDevices.size());

		if (*deviceCount == 0)
		{
			*devices = nullptr;
			return FX_SUCCESS;
		}

		*devices = static_cast<FxSoundDevice*>(malloc(*deviceCount * sizeof(FxSoundDevice)));
		if (!*devices) return FX_ERROR_MEMORY;

		for (int i = 0; i < *deviceCount; i++)
		{
			const SoundDevice& src = nativeDevices[i];
			FxSoundDevice& dst = (*devices)[i];

			// Allocate and copy strings
			dst.deviceId = AllocateAndCopyWString(src.pwszID);
			dst.friendlyName = AllocateAndCopyWString(src.deviceFriendlyName);
			dst.description = AllocateAndCopyWString(src.deviceDescription);

			// Copy boolean values as integers
			dst.isCaptureDevice = src.isCaptureDevice ? 1 : 0;
			dst.isPlaybackDevice = src.isPlaybackDevice ? 1 : 0;
			dst.isDefaultDevice = src.isDefaultDevice ? 1 : 0;
			dst.isRealDevice = src.isRealDevice ? 1 : 0;
			dst.isDFXDevice = src.isDFXDevice ? 1 : 0;
			dst.numChannels = src.deviceNumChannel;
		}

		return FX_SUCCESS;
	}
	catch (...)
	{
		return FX_ERROR_GENERAL;
	}
}

FXWRAPPER_API void FxAudioFreeSoundDevices(FxSoundDevice* devices, int deviceCount)
{
	if (!devices) return;

	for (int i = 0; i < deviceCount; i++)
	{
		free(devices[i].deviceId);
		free(devices[i].friendlyName);
		free(devices[i].description);
	}

	free(devices);
}

FXWRAPPER_API int FxAudioSetBufferLength(int bufferLengthMsecs)
{
	if (!g_audioPassthru) return FX_ERROR_NOT_INITIALIZED;
	return g_audioPassthru->setBufferLength(bufferLengthMsecs);
}

FXWRAPPER_API int FxAudioProcessTimer()
{
	if (!g_audioPassthru) return FX_ERROR_NOT_INITIALIZED;
	return g_audioPassthru->processTimer();
}

FXWRAPPER_API void FxAudioSetDspProcessingModule()
{
	if (!g_audioPassthru || !g_dfxDsp) return;
	g_audioPassthru->setDspProcessingModule(g_dfxDsp);
}

FXWRAPPER_API void FxAudioSetPlaybackDevice(const FxSoundDevice* device)
{
	if (!g_audioPassthru || !device) return;

	SoundDevice nativeDevice;
	nativeDevice.pwszID = device->deviceId;
	nativeDevice.deviceFriendlyName = device->friendlyName;
	nativeDevice.deviceDescription = device->description;
	nativeDevice.isCaptureDevice = device->isCaptureDevice != 0;
	nativeDevice.isPlaybackDevice = device->isPlaybackDevice != 0;
	nativeDevice.isDefaultDevice = device->isDefaultDevice != 0;
	nativeDevice.isRealDevice = device->isRealDevice != 0;
	nativeDevice.isDFXDevice = device->isDFXDevice != 0;
	nativeDevice.deviceNumChannel = device->numChannels;

	g_audioPassthru->setAsPlaybackDevice(nativeDevice);
}

FXWRAPPER_API void FxAudioRegisterDeviceChangeCallback(FxDeviceChangeCallback callback)
{
	if (!g_audioPassthru) return;

	g_deviceChangeCallback = callback;

	if (g_callbackAdapter)
	{
		delete g_callbackAdapter;
		g_callbackAdapter = nullptr;
	}

	if (callback)
	{
		g_callbackAdapter = new DeviceChangeCallbackAdapter();
		g_audioPassthru->registerCallback(g_callbackAdapter);
	}
}

FXWRAPPER_API int FxAudioIsPlaybackDeviceAvailable()
{
	if (!g_audioPassthru) return 0;
	return g_audioPassthru->isPlaybackDeviceAvailable() ? 1 : 0;
}

// ===== Utility functions =====

FXWRAPPER_API void FxFreeString(wchar_t* str)
{
	free(str);
}

FXWRAPPER_API void FxFreePreset(FxPreset* preset)
{
	if (!preset) return;

	free(preset->fullPath);
	free(preset->name);
}
#include "DfxInstall.h"
std::string output;
auto const version = L"14.1.0.0";
const wchar_t* fxvad_id = L"Root\\FXVAD";

FXWRAPPER_API int FxInstallDriverWin10(wchar_t* work_dir) {
	std::wstring inf(work_dir);
	inf.append(L"\\Drivers\\win10\\x64\\fxvad.inf");

	DfxInstall dfx_install(work_dir, version);
	if (!dfx_install.FindDFXDriver(fxvad_id, version)) {
		return cmdInstall(NULL, NULL, 0, inf.c_str(), fxvad_id);
	}
	return 0;
}

FXWRAPPER_API int FxUninstallDriverWin10(wchar_t* work_dir) {
	DfxInstall dfx_install(work_dir, version);
	if (dfx_install.FindDFXDriver(fxvad_id, version)) {
		return cmdRemove(NULL, NULL, fxvad_id);
	}
	return 0;
}

FXWRAPPER_API const char* FxGetInstallLog() {
	return output.c_str();
}
