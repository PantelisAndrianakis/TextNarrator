using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Text.Json;

namespace TextNarrator
{
	/// <summary>
	/// Manages available voices - shows Piper voices only if downloaded or internet available.
	/// </summary>
	public class VoiceManager
	{
		private readonly Dictionary<string, VoiceInfo> _voiceMap;
		private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

		// Popular Piper models available for download.
		private readonly string[] _availablePiperModels = new[]
		{
			"en_US-libritts-high",
			"en_US-ljspeech-high", 
			"en_US-libritts-high",
			"en_US-kusal-medium",
			"en_US-joe-medium",
			"en_US-libritts-high",
			"en_GB-semaine-medium",
			"en_GB-alan-medium"
		};

		public VoiceManager()
		{
			_voiceMap = new Dictionary<string, VoiceInfo>();
		}

		/// <summary>
		/// Gets all available voices (always shows default Piper voices for download).
		/// </summary>
		public async Task<List<VoiceInfo>> GetAvailableVoicesAsync()
		{
			List<VoiceInfo> voices = new List<VoiceInfo>();

			try
			{
				// Add Piper voices (always shows defaults, even if not downloaded).
				await AddPiperVoicesAsync(voices);

				// Add Windows Modern voices.
				IEnumerable<VoiceInfo> winRtVoices = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
					.OrderBy(v => v.DisplayName)
					.Select(v => new VoiceInfo
					{
						DisplayName = v.DisplayName,
						EngineType = SpeechEngineType.WinRT
					});

				voices.AddRange(winRtVoices);

				// Add System.Speech voices (avoiding duplicates).
				using (System.Speech.Synthesis.SpeechSynthesizer systemSynth = new System.Speech.Synthesis.SpeechSynthesizer())
				{
					ReadOnlyCollection<InstalledVoice> systemVoices = systemSynth.GetInstalledVoices();

					foreach (InstalledVoice voice in systemVoices.Where(v => v.Enabled))
					{
						string voiceName = voice.VoiceInfo.Name;

						if (!voices.Any(v => v.DisplayName == voiceName))
						{
							voices.Add(new VoiceInfo
							{
								DisplayName = voiceName,
								EngineType = SpeechEngineType.SystemSpeech
							});
						}
					}
				}

				// Build lookup map.
				_voiceMap.Clear();
				foreach (VoiceInfo voice in voices)
				{
					_voiceMap[voice.DisplayName] = voice;
				}

				return voices;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error getting voices: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// Checks if internet is available by attempting to connect to AWS checkip service.
		/// </summary>
		private async Task<bool> IsInternetAvailableAsync()
		{
			try
			{
				using (HttpResponseMessage response = await _httpClient.GetAsync("http://checkip.amazonaws.com"))
				{
					return response.IsSuccessStatusCode;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Internet check failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Adds Piper voices - only if models exist locally OR internet is available for download.
		/// </summary>
		private async Task AddPiperVoicesAsync(List<VoiceInfo> voices)
		{
			try
			{
				string modelsDir = Path.Combine(Directory.GetCurrentDirectory(), "models");
				bool hasPiperExe = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "piper.exe"));
				
				// Always show existing models in the models folder.
				if (Directory.Exists(modelsDir))
				{
					string[] modelFiles = Directory.GetFiles(modelsDir, "*.onnx");
					
					if (modelFiles.Length > 0)
					{
						Debug.WriteLine($"Found {modelFiles.Length} downloaded Piper voice(s)");
						
						foreach (string modelFile in modelFiles)
						{
							string fileName = Path.GetFileNameWithoutExtension(modelFile);
							
							// Add to voices list with proper formatting.
							string friendlyName = FormatPiperVoiceName(fileName);
							
							voices.Add(new VoiceInfo
							{
								DisplayName = friendlyName,
								EngineType = SpeechEngineType.Piper,
								PiperModelKey = fileName
							});
						}
					}
				}
				
				// Determine if we should show additional models for download.
				bool shouldShowDownloadableModels = hasPiperExe;
				
				if (!shouldShowDownloadableModels)
				{
					bool internetAvailable = await IsInternetAvailableAsync();
					shouldShowDownloadableModels = internetAvailable;
				}
				
				// Add downloadable models from the predefined list.
				if (shouldShowDownloadableModels)
				{
					// Track keys we already added from folder scan.
					HashSet<string> existingModelKeys = new HashSet<string>(voices.Where(v => v.EngineType == SpeechEngineType.Piper).Select(v => v.PiperModelKey!));
					
					foreach (string modelKey in _availablePiperModels)
					{
						// Skip if already added from folder scan.
						if (existingModelKeys.Contains(modelKey))
						{
							continue;
						}
						
						string friendlyName = FormatPiperVoiceName(modelKey);
						
						voices.Add(new VoiceInfo
						{
							DisplayName = friendlyName,
							EngineType = SpeechEngineType.Piper,
							PiperModelKey = modelKey
						});
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error adding Piper voices: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets list of Piper models available for download (but not yet downloaded).
		/// </summary>
		public List<string> GetAvailableModelsForDownload()
		{
			string modelsDir = Path.Combine(Directory.GetCurrentDirectory(), "models");
			var availableForDownload = new List<string>();

			foreach (string modelKey in _availablePiperModels)
			{
				string modelPath = Path.Combine(modelsDir, $"{modelKey}.onnx");
				
				if (!File.Exists(modelPath))
				{
					availableForDownload.Add(FormatPiperVoiceName(modelKey));
				}
			}

			return availableForDownload;
		}

		/// <summary>
		/// Formats a Piper model key into friendly name.
		/// </summary>
		private string FormatPiperVoiceName(string modelKey)
		{
			string[] parts = modelKey.Split('-');

			if (parts.Length < 2)
			{
				return $"Piper: {modelKey}";
			}

			string locale = parts[0].Replace("_", "-");
			string voiceName = char.ToUpper(parts[1][0]) + parts[1].Substring(1);
			string quality = parts.Length > 2 ? $", {char.ToUpper(parts[2][0]) + parts[2].Substring(1)}" : "";

			return $"Piper: {voiceName} ({locale}{quality})";
		}

		public VoiceInfo? GetVoiceInfo(string displayName)
		{
			return _voiceMap.TryGetValue(displayName, out VoiceInfo? voiceInfo) ? voiceInfo : null;
		}

		/// <summary>
		/// Finds a preferred voice from the available voices.
		/// </summary>
		/// <param name="preferredNames">List of preferred voice names in priority order.</param>
		/// <returns>The display name of the first matching voice, or null if none found.</returns>
		public string? FindPreferredVoice(params string[] preferredNames)
		{
			foreach (string preferred in preferredNames)
			{
				string? voice = _voiceMap.Keys.FirstOrDefault(k => k.Contains(preferred, StringComparison.OrdinalIgnoreCase));

				if (voice != null)
				{
					return voice;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the first non-header voice from the available voices.
		/// </summary>
		public string? GetFirstVoice()
		{
			return _voiceMap.Keys.FirstOrDefault();
		}
	}

	public enum SpeechEngineType
	{
		SystemSpeech,
		WinRT,
		Piper
	}
	
	/// <summary>
	/// Information about a voice.
	/// </summary>
	public class VoiceInfo
	{
		public string DisplayName { get; set; } = string.Empty;
		public SpeechEngineType EngineType { get; set; }
		public string? PiperModelKey { get; set; }
	}
}
