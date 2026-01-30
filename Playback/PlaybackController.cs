using System.Diagnostics;

namespace TextNarrator
{
	/// <summary>
	/// Orchestrates text-to-speech playback including state management, speech synthesis and UI updates.
	/// </summary>
	public class PlaybackController : IDisposable
	{
		private const int SENTENCE_PAUSE_DELAY_MS = 200;
		private const int RESPONSIVE_DELAY_MS = 20;

		private readonly PlaybackState _state;
		private readonly TextProcessor _textProcessor;
		private readonly HighlightManager _highlightManager;
		private readonly Control _parentControl;
		private readonly Action<string> _updateTitle;

		private ISpeechEngine? _currentEngine;
		private CancellationTokenSource? _cancellationTokenSource;

		public PlaybackController(Control parentControl, HighlightManager highlightManager, Action<string> updateTitle)
		{
			_parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
			_highlightManager = highlightManager ?? throw new ArgumentNullException(nameof(highlightManager));
			_updateTitle = updateTitle ?? throw new ArgumentNullException(nameof(updateTitle));

			_state = new PlaybackState();
			_textProcessor = new TextProcessor();
		}

		public PlaybackState State => _state;

		/// <summary>
		/// Sets the speech engine to use for playback.
		/// </summary>
		public void SetSpeechEngine(ISpeechEngine engine)
		{
			_currentEngine = engine ?? throw new ArgumentNullException(nameof(engine));
		}

		/// <summary>
		/// Starts or resumes playback.
		/// </summary>
		public async Task PlayAsync(string text)
		{
			if (_currentEngine == null)
			{
				throw new InvalidOperationException("Speech engine not set. Call SetSpeechEngine first.");
			}

			// If already playing (not paused), do nothing.
			if (_state.IsPlaying)
			{
				return;
			}

			// Handle resume from pause.
			if (_state.IsPaused)
			{
				await ResumeFromPauseAsync(text);
				return;
			}

			// Start fresh playback.
			await StartPlaybackAsync(text);
		}

		/// <summary>
		/// Pauses playback at the current sentence.
		/// </summary>
		public void Pause()
		{
			_state.Pause(_state.CurrentSentenceIndex);

			// Cancel ongoing operations.
			try
			{
				if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
				{
					_cancellationTokenSource.Cancel();
				}
			}
			catch (ObjectDisposedException)
			{
				// Already disposed, ignore.
			}

			// Stop speech.
			_currentEngine?.StopImmediate();

			UpdateTitleWithPause();
		}

		/// <summary>
		/// Stops playback completely and resets state.
		/// </summary>
		public void Stop()
		{
			_state.Stop();

			// Cancel ongoing operations.
			try
			{
				if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
				{
					_cancellationTokenSource.Cancel();
				}
			}
			catch (ObjectDisposedException)
			{
				// Already disposed, ignore.
			}

			// Stop speech.
			_currentEngine?.StopImmediate();

			// Clear highlighting.
			_highlightManager.ClearHighlight();
		}

		/// <summary>
		/// Restarts playback from the beginning.
		/// </summary>
		public async Task RestartAsync(string text)
		{
			Stop();
			await Task.Delay(100); // Brief delay to ensure cleanup.

			List<string> sentences = _textProcessor.SplitIntoSentences(text);
			if (sentences.Count > 0)
			{
				SentencePosition position = _highlightManager.FindAndHighlight(sentences[0], 0);
				_state.UpdateCurrentSentence(sentences[0], position.StartPosition, position.Length);
			}

			await StartPlaybackAsync(text);
		}

		private async Task StartPlaybackAsync(string text)
		{
			_state.Play();
			_highlightManager.ClearHighlight();

			List<string> sentences = _textProcessor.SplitIntoSentences(text);
			await PlaySentencesAsync(sentences, 0);
		}

		private async Task ResumeFromPauseAsync(string text)
		{
			int resumeIndex = _state.GetResumeIndex();
			List<string> sentences = _textProcessor.SplitIntoSentences(text);

			if (resumeIndex >= 0 && resumeIndex < sentences.Count)
			{
				_state.Resume();
				_state.CurrentSentenceIndex = resumeIndex;
				_state.UpdateCurrentSentence(sentences[resumeIndex], _state.CurrentSentenceStartPosition, _state.CurrentSentenceLength);

				_highlightManager.HighlightSentence(_state.CurrentSentenceStartPosition, _state.CurrentSentenceLength);

				await PlaySentencesAsync(sentences, resumeIndex);
			}
			else
			{
				// Invalid resume index, start from beginning.
				await StartPlaybackAsync(text);
			}
		}

		private async Task PlaySentencesAsync(List<string> sentences, int startIndex)
		{
			if (_currentEngine == null)
			{
				return;
			}

			int sentenceCount = sentences.Count;
			int searchStartIndex = CalculateSearchStartIndex(sentences, startIndex);

			_cancellationTokenSource?.Cancel();
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = new CancellationTokenSource();

			try
			{
				for (int i = startIndex; i < sentences.Count; i++)
				{
					// Check for pause/stop.
					if (_state.IsStopped || _cancellationTokenSource.Token.IsCancellationRequested)
					{
						break;
					}

					// Wait while paused.
					while (_state.IsPaused)
					{
						await Task.Delay(RESPONSIVE_DELAY_MS);
						if (_state.IsStopped || _cancellationTokenSource.Token.IsCancellationRequested)
						{
							break;
						}
					}

					if (_state.IsStopped || _cancellationTokenSource.Token.IsCancellationRequested)
					{
						break;
					}

					// Update state and UI.
					_state.CurrentSentenceIndex = i;
					string originalSentence = sentences[i];
					string processedSentence = _textProcessor.ReplaceAbbreviations(originalSentence);

					UpdateTitle(i + 1, sentenceCount);

					// Speak the sentence.
					searchStartIndex = await SpeakSentenceAsync(processedSentence, originalSentence, searchStartIndex, _cancellationTokenSource.Token);

					// Check if paused during speech.
					if (_state.IsPaused)
					{
						UpdateTitleWithPause();
						break;
					}

					// Reset to beginning after last sentence.
					if (i == sentences.Count - 1 && !_state.IsPaused && !_state.IsStopped)
					{
						_state.CurrentSentenceIndex = 0;
					}

					// Delay between sentences.
					if (SENTENCE_PAUSE_DELAY_MS > 0 && !_state.IsStopped && !_state.IsPaused)
					{
						await Task.Delay(SENTENCE_PAUSE_DELAY_MS, _cancellationTokenSource.Token);

						if (_state.IsStopped || _state.IsPaused)
						{
							break;
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				Debug.WriteLine("Playback cancelled");
			}
			catch (Exception ex)
			{
				ShowError($"Error during playback: {ex.Message}", "Playback Error");
			}
			finally
			{
				if (_state.IsStopped)
				{
					_highlightManager.ClearHighlight();
				}
			}
		}

		private async Task<int> SpeakSentenceAsync(string processedSentence, string originalSentence, int searchStartIndex, CancellationToken cancellationToken)
		{
			if (_currentEngine == null)
			{
				return searchStartIndex;
			}

			// Find and highlight the sentence.
			SentencePosition position = _highlightManager.FindAndHighlight(originalSentence, searchStartIndex);
			_state.UpdateCurrentSentence(originalSentence, position.StartPosition, position.Length);

			// Speak the sentence.
			try
			{
				await _currentEngine.SpeakAsync(processedSentence, cancellationToken);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error during speech synthesis: {ex.Message}");
			}

			return position.NextSearchIndex;
		}

		private int CalculateSearchStartIndex(List<string> sentences, int startIndex)
		{
			string text = _highlightManager.GetText();
			int searchStartIndex = 0;

			for (int i = 0; i < startIndex && i < sentences.Count; i++)
			{
				int foundPos = text.IndexOf(sentences[i], searchStartIndex, StringComparison.OrdinalIgnoreCase);
				if (foundPos >= 0)
				{
					searchStartIndex = foundPos + sentences[i].Length;
				}
			}

			return searchStartIndex;
		}

		private void StopCurrentSpeech()
		{
			try
			{
				_cancellationTokenSource?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				// Token source already disposed, ignore.
			}

			_currentEngine?.StopImmediate();
		}

		private void UpdateTitle(int currentSentence, int totalSentences)
		{
			ExecuteOnUIThread(() =>
			{
				_updateTitle($"TextNarrator - Reading sentence {currentSentence} of {totalSentences}");
			});
		}

		private void UpdateTitleWithPause()
		{
			ExecuteOnUIThread(() =>
			{
				_updateTitle($"TextNarrator - PAUSED at sentence {_state.CurrentSentenceIndex + 1}");
			});
		}

		private void ShowError(string message, string title)
		{
			ExecuteOnUIThread(() =>
			{
				if (!_parentControl.IsDisposed)
				{
					MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			});
		}

		private void ExecuteOnUIThread(Action action)
		{
			if (_parentControl.IsDisposed) return;

			if (_parentControl.InvokeRequired)
			{
				_parentControl.Invoke(action);
			}
			else
			{
				action();
			}
		}

		public void Dispose()
		{
			try
			{
				// Cancel any ongoing operations first.
				if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
				{
					try
					{
						_cancellationTokenSource.Cancel();
					}
					catch (ObjectDisposedException)
					{
						// Already disposed, ignore.
					}
				}

				// Stop speech engine.
				_currentEngine?.StopImmediate();

				// Dispose cancellation token.
				_cancellationTokenSource?.Dispose();
				_cancellationTokenSource = null;

				// Don't dispose engines here, they're disposed by MainForm.
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error during PlaybackController disposal: {ex.Message}");
			}
		}
	}
}
