using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.Wave;

namespace TextNarrator
{
	/// <summary>
	/// Piper TTS with progress notifications and sentence-by-sentence playback.
	/// Text preprocessing handled by TextProcessor (shared with all engines).
	/// </summary>
	public class PiperSpeechEngine : ISpeechEngine
	{
		private string _piperExePath = "";
		private string _modelPath = "";
		private string _currentVoiceName = "";
		private bool _isSpeaking = false;
		private WaveOutEvent? _waveOut;
		private CancellationTokenSource? _playbackCts;
		private int _sampleRate = 22050;

		public string CurrentVoiceName => _currentVoiceName;
		public bool IsSpeaking => _isSpeaking;

		// Progress events for UI notifications.
		public event EventHandler<string>? StatusChanged;
		public event EventHandler<int>? SentenceChanged;
		public event EventHandler? PlaybackCompleted;

		public async Task InitializeAsync(string? modelKey = null)
		{
			try
			{
				modelKey ??= "en_US-ljspeech-high";
				_currentVoiceName = modelKey;

				string cwd = Directory.GetCurrentDirectory();
				string piperDir = Path.Combine(cwd, "piper");
				_piperExePath = Path.Combine(piperDir, "piper.exe");

				// Download piper.exe if needed.
				if (!File.Exists(_piperExePath))
				{
					OnStatusChanged("Downloading piper.exe...");
					await DownloadPiperAsync(piperDir);
				}

				// Download model if needed.
				string modelsDir = Path.Combine(cwd, "models");
				_modelPath = await DownloadModelIfNeededAsync(modelKey, modelsDir);

				// Read sample rate.
				string configPath = _modelPath + ".json";
				if (File.Exists(configPath))
				{
					string json = await File.ReadAllTextAsync(configPath);
					Match match = Regex.Match(json, @"""sample_rate"":\s*(\d+)");
					if (match.Success)
					{
						_sampleRate = int.Parse(match.Groups[1].Value);
					}
				}

				OnStatusChanged("Ready");
				Debug.WriteLine($"Piper initialized: {_currentVoiceName} @ {_sampleRate}Hz");
			}
			catch (Exception ex)
			{
				OnStatusChanged($"Error: {ex.Message}");
				Debug.WriteLine($"Error initializing Piper: {ex.Message}");
				throw;
			}
		}

		private void OnStatusChanged(string status)
		{
			StatusChanged?.Invoke(this, status);
		}

		private void OnSentenceChanged(int sentenceIndex)
		{
			SentenceChanged?.Invoke(this, sentenceIndex);
		}

		private void OnPlaybackCompleted()
		{
			PlaybackCompleted?.Invoke(this, EventArgs.Empty);
		}

		private async Task DownloadPiperAsync(string piperDir)
		{
			Directory.CreateDirectory(piperDir);

			string piperExe = Path.Combine(piperDir, "piper.exe");
			string espeakData = Path.Combine(piperDir, "espeak-ng-data");

			if (File.Exists(piperExe) && Directory.Exists(espeakData))
			{
				Debug.WriteLine("Piper already installed");
				return;
			}

			// Just attempt download without internet pre-check.
			string downloadUrl = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip";
			string zipPath = Path.Combine(piperDir, "piper.zip");

			try
			{
				OnStatusChanged("Downloading piper.exe (40MB)...");
				Debug.WriteLine("Downloading piper.exe (~40MB)...");

				using var client = new HttpClient();
				client.Timeout = TimeSpan.FromMinutes(5);

				HttpResponseMessage response = await client.GetAsync(downloadUrl);
				response.EnsureSuccessStatusCode();

				await using (FileStream fileStream = File.Create(zipPath))
				{
					await response.Content.CopyToAsync(fileStream);
					await fileStream.FlushAsync();
				}

				OnStatusChanged("Extracting piper.exe...");
				Debug.WriteLine("Extracting...");

				string tempDir = Path.Combine(Path.GetTempPath(), "piper_extract_" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(tempDir);

				System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);

				string[] subDirs = Directory.GetDirectories(tempDir);
				string sourceDir = subDirs.Length > 0 ? subDirs[0] : tempDir;

				CopyDirectory(sourceDir, piperDir);

				try
				{
					Directory.Delete(tempDir, recursive: true);
					File.Delete(zipPath);
				}
				catch
				{
					// Cleanup failure is non-critical.
				}

				OnStatusChanged("Piper installed");
				Debug.WriteLine("Piper installed successfully");
			}
			catch (Exception ex)
			{
				OnStatusChanged($"Error downloading piper: {ex.Message}");
				throw new Exception($"Failed to install piper: {ex.Message}", ex);
			}
		}

		private void CopyDirectory(string sourceDir, string destDir)
		{
			Directory.CreateDirectory(destDir);

			foreach (string file in Directory.GetFiles(sourceDir))
			{
				string destFile = Path.Combine(destDir, Path.GetFileName(file));
				File.Copy(file, destFile, overwrite: true);
			}

			foreach (string dir in Directory.GetDirectories(sourceDir))
			{
				string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
				CopyDirectory(dir, destSubDir);
			}
		}

		private async Task<string> DownloadModelIfNeededAsync(string modelKey, string modelsDir)
		{
			Directory.CreateDirectory(modelsDir);

			string modelPath = Path.Combine(modelsDir, $"{modelKey}.onnx");
			string configPath = modelPath + ".json";

			if (File.Exists(modelPath) && File.Exists(configPath))
			{
				return modelPath;
			}

			// Just attempt download without internet pre-check.
			OnStatusChanged($"Downloading voice model: {modelKey}");

			string[] parts = modelKey.Split('-');
			if (parts.Length < 2)
			{
				throw new ArgumentException("Invalid model key");
			}

			string locale = parts[0];
			string voice = parts[1];
			string quality = parts.Length > 2 ? parts[2] : "medium";
			string language = locale.Split('_')[0];

			string baseUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0";
			string modelUrl = $"{baseUrl}/{language}/{locale}/{voice}/{quality}/{modelKey}.onnx";
			string configUrl = $"{baseUrl}/{language}/{locale}/{voice}/{quality}/{modelKey}.onnx.json";

			try
			{
				Debug.WriteLine($"Downloading model: {modelKey}");
				using var client = new HttpClient();
				client.Timeout = TimeSpan.FromMinutes(10);

				byte[] modelData = await client.GetByteArrayAsync(modelUrl);
				await File.WriteAllBytesAsync(modelPath, modelData);

				byte[] configData = await client.GetByteArrayAsync(configUrl);
				await File.WriteAllBytesAsync(configPath, configData);

				OnStatusChanged("Voice model downloaded");
				Debug.WriteLine($"Model downloaded: {modelKey}");
			}
			catch (Exception ex)
			{
				OnStatusChanged($"Error downloading model: {ex.Message}");
				throw new Exception($"Failed to download model: {ex.Message}", ex);
			}

			return modelPath;
		}

		public void SelectVoice(string voiceName)
		{
			_ = Task.Run(async () => await InitializeAsync(voiceName));
		}

		private async Task<byte[]?> GenerateSentenceAudioAsync(string sentence, CancellationToken cancellationToken)
		{
			try
			{
				var startInfo = new ProcessStartInfo
				{
					FileName = _piperExePath,
					Arguments = $"--model \"{_modelPath}\" --output-raw --quiet",
					UseShellExecute = false,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					StandardInputEncoding = Encoding.UTF8
				};

				using var process = new Process { StartInfo = startInfo };
				process.Start();

				await process.StandardInput.WriteLineAsync(sentence);
				await process.StandardInput.FlushAsync();
				process.StandardInput.Close();

				Task<byte[]> outputTask = Task.Run(async () =>
				{
					var ms = new MemoryStream();
					await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
					return ms.ToArray();
				}, cancellationToken);

				await process.WaitForExitAsync(cancellationToken);

				return await outputTask;
			}
			catch (OperationCanceledException)
			{
				return null;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error generating audio: {ex.Message}");
				return null;
			}
		}

		public async Task<bool> SpeakAsync(string text, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(_piperExePath) || string.IsNullOrEmpty(_modelPath))
			{
				Debug.WriteLine("Piper not initialized");
				return false;
			}

			_playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			try
			{
				_isSpeaking = true;

				// Text is already preprocessed by TextProcessor in MainForm.
				List<string> sentences = TextProcessor.SplitIntoSentences(text);
				Debug.WriteLine($"Split into {sentences.Count} sentences");

				if (sentences.Count == 0)
				{
					OnStatusChanged("No text to read");
					return false;
				}

				// Notify: converting first sentence.
				OnStatusChanged($"Converting sentence 1/{sentences.Count}...");

				// Pre-generate first sentence.
				int currentSentenceIndex = 0;
				byte[]? currentAudio = await GenerateSentenceAudioAsync(sentences[0], _playbackCts.Token);

				if (currentAudio == null || currentAudio.Length == 0)
				{
					Debug.WriteLine("Failed to generate audio for first sentence");
					OnStatusChanged("Error: Failed to convert text");
					return false;
				}

				// Process sentences one by one.
				while (currentSentenceIndex < sentences.Count && !_playbackCts.Token.IsCancellationRequested)
				{
					// Start generating next sentence while playing current.
					Task<byte[]?>? nextAudioTask = null;
					if (currentSentenceIndex + 1 < sentences.Count)
					{
						int nextIndex = currentSentenceIndex + 1;
						nextAudioTask = Task.Run(async () =>
						{
							OnStatusChanged($"Converting sentence {nextIndex + 1}/{sentences.Count}...");
							return await GenerateSentenceAudioAsync(sentences[nextIndex], _playbackCts.Token);
						});
					}

					// Notify UI which sentence is playing.
					OnSentenceChanged(currentSentenceIndex);
					OnStatusChanged($"Reading sentence {currentSentenceIndex + 1}/{sentences.Count}");

					// Play current sentence.
					Debug.WriteLine($"Playing sentence {currentSentenceIndex + 1}/{sentences.Count}");
					bool playSuccess = await PlayAudioAsync(currentAudio, _playbackCts.Token);

					if (!playSuccess || _playbackCts.Token.IsCancellationRequested)
					{
						nextAudioTask?.Wait(100);
						OnStatusChanged("Stopped");
						return false;
					}

					// Move to next sentence.
					currentSentenceIndex++;

					// Get the pre-generated next audio.
					if (nextAudioTask != null)
					{
						currentAudio = await nextAudioTask;
						if (currentAudio == null || currentAudio.Length == 0)
						{
							Debug.WriteLine($"Failed to generate audio for sentence {currentSentenceIndex + 1}");
							OnStatusChanged("Error: Failed to convert text");
							return false;
						}
					}
				}

				// Completed successfully.
				OnStatusChanged("Ready");
				OnPlaybackCompleted();
				return true;
			}
			catch (OperationCanceledException)
			{
				OnStatusChanged("Stopped");
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error in speak: {ex.Message}");
				OnStatusChanged($"Error: {ex.Message}");
				return false;
			}
			finally
			{
				_isSpeaking = false;
			}
		}

		private async Task<bool> PlayAudioAsync(byte[] audioData, CancellationToken cancellationToken)
		{
			try
			{
				using var ms = new MemoryStream(audioData);
				var waveFormat = new WaveFormat(_sampleRate, 16, 1);
				using var rawSource = new RawSourceWaveStream(ms, waveFormat);

				_waveOut = new WaveOutEvent();
				_waveOut.Init(rawSource);

				var playbackComplete = new TaskCompletionSource<bool>();
				_waveOut.PlaybackStopped += (s, e) => playbackComplete.TrySetResult(true);

				_waveOut.Play();

				while (_waveOut.PlaybackState == NAudio.Wave.PlaybackState.Playing)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						_waveOut.Stop();
						return false;
					}
					await Task.Delay(50, cancellationToken);
				}

				await playbackComplete.Task;
				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Playback error: {ex.Message}");
				return false;
			}
			finally
			{
				_waveOut?.Dispose();
				_waveOut = null;
			}
		}

		public void StopImmediate()
		{
			try
			{
				_playbackCts?.Cancel();
				_waveOut?.Stop();
				_isSpeaking = false;
				OnStatusChanged("Ready");
			}
			catch
			{
				// Stop errors are non-critical.
			}
		}

		public void Dispose()
		{
			StopImmediate();
			_waveOut?.Dispose();
			_playbackCts?.Dispose();
		}
	}
}
