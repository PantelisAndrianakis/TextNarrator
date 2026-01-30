namespace TextNarrator
{
	/// <summary>
	/// Centralized state management for text-to-speech playback.
	/// Replaces scattered boolean flags with a clear state machine.
	/// </summary>
	public class PlaybackState
	{
		public enum State
		{
			Stopped,
			Playing,
			Paused
		}

		private State _currentState = State.Stopped;
		private int _currentSentenceIndex = 0;
		private int _pausedSentenceIndex = -1;

		/// <summary>
		/// Current sentence tracking for highlighting.
		/// </summary>
		public string CurrentSentence { get; set; } = string.Empty;
		public int CurrentSentenceStartPosition { get; set; } = 0;
		public int CurrentSentenceLength { get; set; } = 0;

		public State CurrentState
		{
			get => _currentState;
			private set => _currentState = value;
		}

		public int CurrentSentenceIndex
		{
			get => _currentSentenceIndex;
			set => _currentSentenceIndex = value;
		}

		public bool IsPlaying => CurrentState == State.Playing;
		public bool IsPaused => CurrentState == State.Paused;
		public bool IsStopped => CurrentState == State.Stopped;

		public void Play()
		{
			CurrentState = State.Playing;
		}

		public void Pause(int sentenceIndex)
		{
			CurrentState = State.Paused;
			_pausedSentenceIndex = sentenceIndex;
		}

		public void Stop()
		{
			CurrentState = State.Stopped;
			_currentSentenceIndex = 0;
			_pausedSentenceIndex = -1;
			CurrentSentence = string.Empty;
			CurrentSentenceStartPosition = 0;
			CurrentSentenceLength = 0;
		}

		public void Resume()
		{
			if (CurrentState == State.Paused && _pausedSentenceIndex >= 0)
			{
				_currentSentenceIndex = _pausedSentenceIndex;
				CurrentState = State.Playing;
			}
		}

		public int GetResumeIndex()
		{
			return _pausedSentenceIndex >= 0 ? _pausedSentenceIndex : _currentSentenceIndex;
		}

		public void UpdateCurrentSentence(string sentence, int startPosition, int length)
		{
			CurrentSentence = sentence;
			CurrentSentenceStartPosition = startPosition;
			CurrentSentenceLength = length;
		}
	}
}
