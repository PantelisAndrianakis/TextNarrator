using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Speech.Synthesis;

namespace TextNarrator
{
	/// <summary>
	/// Manages available voices and voice selection.
	/// </summary>
	public class VoiceManager
	{
		private readonly Dictionary<string, VoiceInfo> _voiceMap;

		public VoiceManager()
		{
			_voiceMap = new Dictionary<string, VoiceInfo>();
		}

		/// <summary>
		/// Gets all available voices from both speech systems.
		/// </summary>
		public List<VoiceInfo> GetAvailableVoices()
		{
			List<VoiceInfo> voices = new List<VoiceInfo>();

			try
			{
				// Add Windows Modern voices first.
				IEnumerable<VoiceInfo> winRtVoices = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
					.OrderBy(v => v.DisplayName)
					.Select(v => new VoiceInfo
					{
						DisplayName = v.DisplayName,
						IsSystemSpeech = false
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
								IsSystemSpeech = true
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
				Debug.WriteLine($"Error getting available voices: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// Gets the voice information for a given display name.
		/// </summary>
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

	/// <summary>
	/// Information about a voice.
	/// </summary>
	public class VoiceInfo
	{
		public string DisplayName { get; set; } = string.Empty;
		public bool IsSystemSpeech { get; set; }
	}
}
