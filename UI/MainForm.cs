using System.Diagnostics;

namespace TextNarrator
{
	/// <summary>
	/// Main form for the TextNarrator application.
	/// </summary>
	public sealed partial class MainForm : Form
	{
		// Core components.
		private readonly VoiceManager _voiceManager;
		private readonly PlaybackController _playbackController;
		private readonly HighlightManager _highlightManager;

		// Speech engines.
		private readonly SystemSpeechEngine _systemSpeechEngine;
		private readonly WinRtSpeechEngine _winRtSpeechEngine;

		// Current state.
		private ISpeechEngine _currentEngine;

		public MainForm()
		{
			InitializeComponent();

			// Initialize components.
			_voiceManager = new VoiceManager();
			_highlightManager = new HighlightManager(richTextBox, this);
			_playbackController = new PlaybackController(this, _highlightManager, title => Text = title);

			// Initialize speech engines.
			_systemSpeechEngine = new SystemSpeechEngine();
			_winRtSpeechEngine = new WinRtSpeechEngine();
			_currentEngine = _winRtSpeechEngine; // Default to WinRT.

			InitializeVoiceSelection();
		}

		/// <summary>
		/// Initializes voice selection dropdown.
		/// </summary>
		private void InitializeVoiceSelection()
		{
			comboVoices.Items.Clear();
			comboVoices.DropDown += ComboVoices_DropDown;

			try
			{
				List<VoiceInfo> voices = _voiceManager.GetAvailableVoices();

				foreach (VoiceInfo voice in voices)
				{
					comboVoices.Items.Add(voice.DisplayName);
				}

				if (comboVoices.Items.Count > 0)
				{
					// Try to select preferred voice.
					string? preferredVoice = _voiceManager.FindPreferredVoice("Guy");

					if (preferredVoice != null)
					{
						int index = comboVoices.Items.IndexOf(preferredVoice);
						if (index >= 0)
						{
							comboVoices.SelectedIndex = index;
						}
					}
					else
					{
						// Select first voice.
						comboVoices.SelectedIndex = 0;
					}
				}
				else
				{
					MessageBox.Show("No text-to-speech voices found on this system.", "Voice Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error initializing text-to-speech voices: {ex.Message}\n\nPlease ensure your system has speech voices installed.", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Handles voice selection changes.
		/// </summary>
		private async void ComboVoices_SelectedIndexChanged(object? sender, EventArgs e)
		{
			if (comboVoices.SelectedIndex < 0 || comboVoices.SelectedItem == null)
			{
				return;
			}

			string selectedVoice = comboVoices.SelectedItem.ToString() ?? string.Empty;

			if (string.IsNullOrEmpty(selectedVoice))
			{
				return;
			}

			VoiceInfo? voiceInfo = _voiceManager.GetVoiceInfo(selectedVoice);
			if (voiceInfo == null)
			{
				Debug.WriteLine($"Voice not found: {selectedVoice}");
				return;
			}

			try
			{
				// Switch speech engine based on voice type.
				ISpeechEngine newEngine = voiceInfo.IsSystemSpeech ? (ISpeechEngine)_systemSpeechEngine : _winRtSpeechEngine;

				newEngine.SelectVoice(selectedVoice);
				_currentEngine = newEngine;
				_playbackController.SetSpeechEngine(_currentEngine);

				// If currently playing, pause and resume with new voice.
				if (_playbackController.State.IsPlaying)
				{
					_playbackController.Pause();
					await System.Threading.Tasks.Task.Delay(200);
					await _playbackController.PlayAsync(richTextBox.Text ?? string.Empty);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error selecting voice: {ex.Message}", "Voice Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		/// <summary>
		/// Handles dropdown opening - pauses playback.
		/// </summary>
		private void ComboVoices_DropDown(object? sender, EventArgs e)
		{
			try
			{
				if (_currentEngine.IsSpeaking)
				{
					_playbackController.Pause();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error pausing on dropdown: {ex.Message}");
			}
		}

		/// <summary>
		/// Handles play button click.
		/// </summary>
		private async void btnPlay_Click(object? sender, EventArgs e)
		{
			try
			{
				_playbackController.SetSpeechEngine(_currentEngine);
				await _playbackController.PlayAsync(richTextBox.Text ?? string.Empty);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error during playback: {ex.Message}", "Playback Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		/// <summary>
		/// Handles pause button click.
		/// </summary>
		private void btnPause_Click(object? sender, EventArgs e)
		{
			try
			{
				_playbackController.Pause();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error pausing playback: {ex.Message}", "Pause Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		/// <summary>
		/// Handles stop button click.
		/// </summary>
		private void btnStop_Click(object? sender, EventArgs e)
		{
			try
			{
				_playbackController.Stop();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error stopping playback: {ex.Message}", "Stop Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		/// <summary>
		/// Handles restart button click.
		/// </summary>
		private async void btnRestart_Click(object? sender, EventArgs e)
		{
			try
			{
				await _playbackController.RestartAsync(richTextBox.Text ?? string.Empty);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error restarting playback: {ex.Message}", "Restart Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}
	}
}
