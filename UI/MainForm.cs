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
		private PiperSpeechEngine? _piperSpeechEngine; // Created on-demand due to async initialization.

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

			// Note: Piper engine will be created on-demand when first Piper voice is selected.
		}

		/// <summary>
		/// Form load event - initializes voices asynchronously.
		/// </summary>
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			
			// Initialize voice selection asynchronously.
			_ = InitializeVoiceSelectionAsync();
		}

		/// <summary>
		/// Initializes voice selection dropdown asynchronously.
		/// </summary>
		private async Task InitializeVoiceSelectionAsync()
		{
			comboVoices.Items.Clear();
			comboVoices.DropDown += ComboVoices_DropDown;

			try
			{
				// Show loading indicator.
				comboVoices.Enabled = false;
				comboVoices.Items.Add("Loading voices...");
				comboVoices.SelectedIndex = 0;

				// Load voices asynchronously (includes Piper voices from HuggingFace).
				List<VoiceInfo> voices = await _voiceManager.GetAvailableVoicesAsync();

				// Clear loading indicator.
				comboVoices.Items.Clear();
				comboVoices.Enabled = true;

				// Populate voice dropdown.
				foreach (VoiceInfo voice in voices)
				{
					comboVoices.Items.Add(voice.DisplayName);
				}

				if (comboVoices.Items.Count > 0)
				{
					// Try to select preferred voice (Piper voices first, then fallback).
					string? preferredVoice = _voiceManager.FindPreferredVoice("Piper: Alan", "Piper: Semaine", "Zira", "James");

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
				// Clear loading state.
				comboVoices.Items.Clear();
				comboVoices.Enabled = true;

				// Show error but continue with fallback.
				MessageBox.Show($"Error loading voices: {ex.Message}\n\nFalling back to system voices only.", "Voice Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

				// Fallback: Load system voices only.
				try
				{
					using (System.Speech.Synthesis.SpeechSynthesizer systemSynth = new System.Speech.Synthesis.SpeechSynthesizer())
					{
						System.Collections.ObjectModel.ReadOnlyCollection<System.Speech.Synthesis.InstalledVoice> systemVoices = systemSynth.GetInstalledVoices();
						foreach (System.Speech.Synthesis.InstalledVoice voice in systemVoices.Where(v => v.Enabled))
						{
							comboVoices.Items.Add(voice.VoiceInfo.Name);
						}
					}

					if (comboVoices.Items.Count > 0)
					{
						comboVoices.SelectedIndex = 0;
					}
				}
				catch (Exception fallbackEx)
				{
					MessageBox.Show($"Critical error: Could not load any voices.\n\n{fallbackEx.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
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
				// Show loading cursor for Piper voices (first time initialization).
				if (voiceInfo.EngineType == SpeechEngineType.Piper && _piperSpeechEngine == null)
				{
					Cursor = Cursors.WaitCursor;
					comboVoices.Enabled = false;
					Text = "TextNarrator - Downloading voice model...";
				}

				// Create appropriate engine based on voice type.
				ISpeechEngine newEngine = await CreateEngineForVoiceAsync(voiceInfo);

				// Select voice in the engine.
				newEngine.SelectVoice(voiceInfo.PiperModelKey ?? selectedVoice);

				// Update current engine.
				_currentEngine = newEngine;
				_playbackController.SetSpeechEngine(_currentEngine);

				// Restore UI state.
				Text = "TextNarrator";
				Cursor = Cursors.Default;
				comboVoices.Enabled = true;

				// If currently playing, pause and resume with new voice.
				if (_playbackController.State.IsPlaying)
				{
					_playbackController.Pause();
					await Task.Delay(200);
					await _playbackController.PlayAsync(richTextBox.Text ?? string.Empty);
				}
			}
			catch (Exception ex)
			{
				// Restore UI state.
				Text = "TextNarrator";
				Cursor = Cursors.Default;
				comboVoices.Enabled = true;

				MessageBox.Show($"Error selecting voice '{selectedVoice}': {ex.Message}\n\nFalling back to previous voice.", "Voice Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

				// Fall back to system speech engine.
				_currentEngine = _systemSpeechEngine;
				_playbackController.SetSpeechEngine(_currentEngine);
			}
		}

		/// <summary>
		/// Creates the appropriate speech engine based on voice type.
		/// </summary>
		private async Task<ISpeechEngine> CreateEngineForVoiceAsync(VoiceInfo voiceInfo)
		{
			switch (voiceInfo.EngineType)
			{
				case SpeechEngineType.Piper:
					// Create Piper engine on first use.
					if (_piperSpeechEngine == null)
					{
						_piperSpeechEngine = new PiperSpeechEngine();
						await _piperSpeechEngine.InitializeAsync(voiceInfo.PiperModelKey);
					}
					else
					{
						// Engine already exists, SelectVoice will handle model switching.
						// (The SelectVoice method in PiperSpeechEngine re-initializes with new model).
					}
					return _piperSpeechEngine;

				case SpeechEngineType.WinRT:
					return _winRtSpeechEngine;

				case SpeechEngineType.SystemSpeech:
				default:
					return _systemSpeechEngine;
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

		/// <summary>
		/// Form closing event - cleanup Piper resources.
		/// </summary>
		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			// Dispose Piper engine if it was created.
			_piperSpeechEngine?.Dispose();
			
			base.OnFormClosing(e);
		}
	}
}
