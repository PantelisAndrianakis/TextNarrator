using System;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace TextNarrator
{
	/// <summary>
	/// Speech engine implementation using System.Speech.Synthesis.
	/// </summary>
	public class SystemSpeechEngine : ISpeechEngine
	{
		private readonly SpeechSynthesizer _synthesizer;

		public SystemSpeechEngine()
		{
			_synthesizer = new SpeechSynthesizer();
		}

		public string CurrentVoiceName => _synthesizer.Voice?.Name ?? string.Empty;

		public bool IsSpeaking => _synthesizer.State == SynthesizerState.Speaking;

		public void SelectVoice(string voiceName)
		{
			try
			{
				_synthesizer.SelectVoice(voiceName);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error selecting System.Speech voice '{voiceName}': {ex.Message}");
				throw;
			}
		}

		public async Task<bool> SpeakAsync(string text, CancellationToken cancellationToken)
		{
			TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();

			EventHandler<SpeakCompletedEventArgs>? completedHandler = null;
			completedHandler = (s, e) =>
			{
				_synthesizer.SpeakCompleted -= completedHandler;
				completionSource.TrySetResult(!e.Cancelled && e.Error == null);
			};

			_synthesizer.SpeakCompleted += completedHandler;

			try
			{
				Prompt prompt = new Prompt(text);
				_synthesizer.SpeakAsync(prompt);

				// Monitor for cancellation.
				while (_synthesizer.State == SynthesizerState.Speaking)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						_synthesizer.SpeakAsyncCancelAll();
						completionSource.TrySetResult(false);
						break;
					}

					await Task.Delay(20, cancellationToken);
				}

				return await completionSource.Task;
			}
			catch (OperationCanceledException)
			{
				_synthesizer.SpeakAsyncCancelAll();
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error in System.Speech playback: {ex.Message}");
				return false;
			}
		}

		public void StopImmediate()
		{
			if (_synthesizer.State == SynthesizerState.Speaking)
			{
				_synthesizer.SpeakAsyncCancelAll();
			}
		}

		public void Dispose()
		{
			_synthesizer?.Dispose();
		}
	}
}
