using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace TextNarrator
{
	/// <summary>
	/// Speech engine implementation using Windows.Media.SpeechSynthesis.
	/// </summary>
	public class WinRtSpeechEngine : ISpeechEngine
	{
		private readonly SpeechSynthesizer _synthesizer;
		private MediaPlayer? _mediaPlayer;
		private bool _isPlaying = false;

		public WinRtSpeechEngine()
		{
			_synthesizer = new SpeechSynthesizer();
		}

		public string CurrentVoiceName => _synthesizer.Voice?.DisplayName ?? string.Empty;

		public bool IsSpeaking => _isPlaying;

		public void SelectVoice(string voiceName)
		{
			try
			{
				VoiceInformation? voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.DisplayName == voiceName);
				if (voice != null)
				{
					_synthesizer.Voice = voice;
				}
				else
				{
					Debug.WriteLine($"WinRT voice '{voiceName}' not found.");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error selecting WinRT voice '{voiceName}': {ex.Message}");
				throw;
			}
		}

		public async Task<bool> SpeakAsync(string text, CancellationToken cancellationToken)
		{
			TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();

			try
			{
				using (SpeechSynthesisStream stream = await _synthesizer.SynthesizeTextToStreamAsync(text))
				{
					if (cancellationToken.IsCancellationRequested)
					{
						return false;
					}

					// Dispose of previous player.
					_mediaPlayer?.Dispose();

					_mediaPlayer = new MediaPlayer();
					_isPlaying = true;

					// Set up event handlers.
					TypedEventHandler<MediaPlayer, object>? endedHandler = null;
					TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs>? failedHandler = null;

					endedHandler = (s, e) =>
					{
						if (_mediaPlayer != null)
						{
							_mediaPlayer.MediaEnded -= endedHandler;
							_mediaPlayer.MediaFailed -= failedHandler;
						}

						_isPlaying = false;
						completionSource.TrySetResult(true);
					};

					failedHandler = (s, e) =>
					{
						if (_mediaPlayer != null)
						{
							_mediaPlayer.MediaEnded -= endedHandler;
							_mediaPlayer.MediaFailed -= failedHandler;
						}

						_isPlaying = false;
						Debug.WriteLine($"WinRT media playback failed: {e.ErrorMessage}");
						completionSource.TrySetResult(false);
					};

					_mediaPlayer.MediaEnded += endedHandler;
					_mediaPlayer.MediaFailed += failedHandler;

					// Create and play media.
					IRandomAccessStream randomAccessStream = stream.CloneStream();
					IMediaPlaybackSource mediaSource = MediaSource.CreateFromStream(randomAccessStream, "audio/wav");
					_mediaPlayer.Source = mediaSource;
					_mediaPlayer.Play();

					// Wait for completion or cancellation.
					while (_isPlaying)
					{
						if (cancellationToken.IsCancellationRequested)
						{
							_mediaPlayer.Pause();
							_isPlaying = false;

							_mediaPlayer.MediaEnded -= endedHandler;
							_mediaPlayer.MediaFailed -= failedHandler;

							return false;
						}

						await Task.Delay(20, cancellationToken);
					}

					return await completionSource.Task;
				}
			}
			catch (OperationCanceledException)
			{
				Debug.WriteLine("WinRT playback cancelled.");
				_isPlaying = false;
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error in WinRT playback: {ex.Message}");
				_isPlaying = false;
				return false;
			}
		}

		public void StopImmediate()
		{
			if (_mediaPlayer != null && _isPlaying)
			{
				_mediaPlayer.Pause();
				_isPlaying = false;
			}
		}

		public void Dispose()
		{
			_mediaPlayer?.Dispose();
			_mediaPlayer = null;
		}
	}
}
