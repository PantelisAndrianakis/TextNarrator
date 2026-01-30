namespace TextNarrator
{
	/// <summary>
	/// Interface for speech synthesis engines.
	/// Abstracts the differences between System.Speech and Windows.Media.SpeechSynthesis.
	/// </summary>
	public interface ISpeechEngine
	{
		/// <summary>
		/// Gets the display name of the currently selected voice.
		/// </summary>
		string CurrentVoiceName { get; }

		/// <summary>
		/// Selects a voice by its display name.
		/// </summary>
		/// <param name="voiceName">The name of the voice to select.</param>
		void SelectVoice(string voiceName);

		/// <summary>
		/// Speaks the given text asynchronously.
		/// </summary>
		/// <param name="text">The text to speak.</param>
		/// <param name="cancellationToken">Token to cancel the speech.</param>
		/// <returns>True if speech completed successfully, false if cancelled or failed.</returns>
		Task<bool> SpeakAsync(string text, CancellationToken cancellationToken);

		/// <summary>
		/// Immediately stops any ongoing speech.
		/// </summary>
		void StopImmediate();

		/// <summary>
		/// Checks if the engine is currently speaking.
		/// </summary>
		bool IsSpeaking { get; }

		/// <summary>
		/// Disposes of resources used by the speech engine.
		/// </summary>
		void Dispose();
	}
}
