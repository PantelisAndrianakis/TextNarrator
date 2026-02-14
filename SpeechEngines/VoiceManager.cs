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
			"en_US-amy-medium",
			"en_US-amy-low", 
			"en_US-lessac-medium",
			"en_US-lessac-low",
			"en_GB-alan-medium",
			"en_GB-alba-medium"
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
				bool hasDownloadedModels = false;
				bool hasPiperExe = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "piper.exe"));

				// Check if any models are already downloaded.
				if (Directory.Exists(modelsDir))
				{
					string[] modelFiles = Directory.GetFiles(modelsDir, "*.onnx");
					hasDownloadedModels = modelFiles.Length > 0;
					
					if (hasDownloadedModels)
					{
						Debug.WriteLine($"Found {modelFiles.Length} downloaded Piper voice(s)");
					}
				}

				// If no models and no Piper exe, check internet availability.
				bool shouldAddPiperVoices = hasDownloadedModels || hasPiperExe;
				
				if (!shouldAddPiperVoices)
				{
					bool internetAvailable = await IsInternetAvailableAsync();
					
					if (internetAvailable)
					{
						Debug.WriteLine("No Piper models found, but internet is available - showing voices for download");
						shouldAddPiperVoices = true;
					}
					else
					{
						Debug.WriteLine("No Piper models found and no internet available - skipping Piper voices");
						return;
					}
				}

				// Add Piper voices to the list.
				foreach (string modelKey in _availablePiperModels)
				{
					string friendlyName = FormatPiperVoiceName(modelKey);
					
					voices.Add(new VoiceInfo
					{
						DisplayName = friendlyName,
						EngineType = SpeechEngineType.Piper,
						PiperModelKey = modelKey
					});
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
