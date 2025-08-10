using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Media;
using System.Speech.Synthesis;
using Windows.Media.SpeechSynthesis;
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.Media.Core;

namespace TextNarrator
{
    public sealed partial class MainForm : Form
    {
        /// <summary>
        /// Delay after each sentence in milliseconds.
        /// 0 = no delay, 1000 = 1 second
        /// </summary>
        /// <summary>
        /// Delay between sentences during playback in milliseconds.
        /// </summary>
        private const int SENTENCE_PAUSE_DELAY_MS = 200;
        
        /// <summary>
        /// Maximum length of text to be considered a title when no ending punctuation.
        /// </summary>
        private const int MAX_TITLE_LENGTH = 100;

        /// <summary>
        /// Dictionary of common abbreviations and their full forms for better TTS pronunciation.<br>
        /// Used to improve TTS quality by replacing short forms with their full text equivalents.
        /// </summary>
        private static readonly Dictionary<string, string> Abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Titles.
            {"Dr.", "Doctor"},
            {"Mr.", "Mister"},
            {"Mrs.", "Missus"},
            {"Ms.", "Miss"},
            {"Prof.", "Professor"},
            {"Rev.", "Reverend"},
            {"Fr.", "Father"},
            {"Sr.", "Senior"},
            {"Jr.", "Junior"},
            
            // Common abbreviations.
            {"etc.", "etcetera"},
            {"vs.", "versus"},
            {"e.g.", "for example"},
            {"i.e.", "that is"},
            {"a.m.", "A M"},
            {"p.m.", "P M"},
            {"Inc.", "Incorporated"},
            {"Corp.", "Corporation"},
            {"Ltd.", "Limited"},
            {"Co.", "Company"},
            
            // Geographic.
            {"St.", "Saint"},
            {"Ave.", "Avenue"},
            {"Blvd.", "Boulevard"},
            {"Rd.", "Road"},
            {"Ln.", "Lane"},
            {"Ct.", "Court"},
            {"Pl.", "Place"},
            
            // Units and measurements.
            {"ft.", "feet"},
            {"in.", "inches"},
            {"lb.", "pounds"},
            {"oz.", "ounces"},
            {"min.", "minutes"},
            {"sec.", "seconds"},
            {"hrs.", "hours"},
            
            // Academic and professional.
            {"Ph.D.", "PhD"},
            {"M.D.", "MD"},
            {"B.A.", "BA"},
            {"M.A.", "MA"},
            {"B.S.", "BS"},
            {"M.S.", "MS"},
            
            // Other common ones.
            {"No.", "Number"},
            {"Vol.", "Volume"},
            {"Ch.", "Chapter"},
            {"Fig.", "Figure"},
            {"Ref.", "Reference"}
        };

        // TTS synthesizer instances for speech generation.
        private Windows.Media.SpeechSynthesis.SpeechSynthesizer? winRtSynth; // Windows Runtime synthesizer.
        private System.Speech.Synthesis.SpeechSynthesizer? systemSynth; // System.Speech synthesizer.
        private bool useSystemSpeech = false; // Tracks which speech system is active.
        private bool isPaused = false; // Indicates if playback is currently paused.
        private bool stopRequested = false; // Indicates if stop was requested.
        private int pausedSentenceIndex = -1; // Index of sentence where pause occurred.
        private bool isResumingFromPause = false; // Indicates resumption from paused state.

        // Media playback control components.
        private MediaPlayer? mediaPlayer; // Windows Runtime media player.
        private SoundPlayer? currentPlayer = null; // Fallback player for legacy support.
        private bool isPlayingWinRt = false; // Indicates if WinRT playback is active.
        private CancellationTokenSource? playbackCancellation = null; // For instant cancellation.

        // Sentence tracking and position management.
        private int currentSentenceIndex = 0; // Index of currently speaking sentence.
        private string currentSentence = ""; // Text of current sentence being spoken.
        private int currentSentenceStartPosition = 0; // Start position of current sentence in text.
        private int currentSentenceLength = 0; // Length of current sentence in characters.

        /// <summary>
        /// Initializes the main form.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            InitializeSpeechSynthesizer();
        }

        /// <summary>
        /// Stops all speech immediately, cutting off any ongoing playback.
        /// </summary>
        private void InstantStopCurrentSpeech()
        {
            try
            {
                // Cancel any ongoing playback.
                if (playbackCancellation != null)
                {
                    playbackCancellation.Cancel();
                    playbackCancellation.Dispose();
                    playbackCancellation = null;
                }

                // System.Speech instant stop.
                if (systemSynth != null && systemSynth.State == SynthesizerState.Speaking)
                {
                    systemSynth.SpeakAsyncCancelAll();
                }

                // WinRT MediaPlayer instant stop.
                if (mediaPlayer != null && isPlayingWinRt)
                {
                    mediaPlayer.Pause();
                    isPlayingWinRt = false;
                }

                // Stop legacy SoundPlayer if active.
                if (currentPlayer != null && isPlayingWinRt)
                {
                    currentPlayer.Stop();
                    isPlayingWinRt = false;
                    currentPlayer.Dispose();
                    currentPlayer = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in instant stop: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores highlighting for the current sentence being spoken.
        /// </summary>
        private void RestoreCurrentSentenceHighlight()
        {
            try
            {
                if (currentSentenceStartPosition >= 0 && currentSentenceLength > 0 && !string.IsNullOrEmpty(currentSentence))
                {
                    if (richTextBox.InvokeRequired)
                    {
                        richTextBox.Invoke(new Action(() =>
                        {
                            if (!IsDisposed)
                            {
                                richTextBox.SelectAll();
                                richTextBox.SelectionBackColor = Color.White;
                                richTextBox.Select(currentSentenceStartPosition, currentSentenceLength);
                                richTextBox.SelectionBackColor = Color.Yellow;
                                richTextBox.ScrollToCaret();
                            }
                        }));
                    }
                    else if (!IsDisposed)
                    {
                        richTextBox.SelectAll();
                        richTextBox.SelectionBackColor = Color.White;
                        richTextBox.Select(currentSentenceStartPosition, currentSentenceLength);
                        richTextBox.SelectionBackColor = Color.Yellow;
                        richTextBox.ScrollToCaret();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring sentence highlight: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles voice selection changes in the dropdown.
        /// </summary>
        private void ComboVoices_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (comboVoices.SelectedIndex < 0)
            {
                return;  // No selection.
            }

            if (comboVoices.SelectedItem == null)
            {
                return;
            }

            string selectedVoice = comboVoices.SelectedItem.ToString() ?? string.Empty;

            if (selectedVoice.StartsWith("---", StringComparison.Ordinal))
            {
                return;
            }

            Dictionary<string, bool>? voiceToSystem = Tag as Dictionary<string, bool>;

            if (voiceToSystem != null && voiceToSystem.TryGetValue(selectedVoice, out bool isSystemSpeech))
            {
                useSystemSpeech = isSystemSpeech;

                if (useSystemSpeech)
                {
                    // System.Speech voice.
                    try
                    {
                        systemSynth?.SelectVoice(selectedVoice);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error selecting voice: {ex.Message}", "Voice Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    // Windows.Media voice.
                    try
                    {
                        VoiceInformation? voice = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.DisplayName == selectedVoice);
                        if (voice != null)
                        {
                            if (winRtSynth != null)
                            {
                                winRtSynth.Voice = voice;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error selecting voice: {ex.Message}", "Voice Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            else
            {
                Debug.WriteLine($"Voice not found in mapping: {selectedVoice}");
            }

            bool systemSpeechActive = useSystemSpeech && systemSynth != null && systemSynth.State == SynthesizerState.Speaking;
            bool winRtActive = !useSystemSpeech && isPlayingWinRt;

            if (systemSpeechActive || winRtActive)
            {
                isPaused = true;

                InstantStopCurrentSpeech();

                UpdatePauseButtonState();

                _ = Task.Delay(SENTENCE_PAUSE_DELAY_MS).ContinueWith(_ =>
                {
                    try
                    {
                        if (IsHandleCreated && !IsDisposed)
                        {
                            if (InvokeRequired)
                            {
                                Invoke(new Action(() =>
                                {
                                    if (!IsDisposed)
                                    {
                                        btnPlay_Click(this, EventArgs.Empty);
                                    }
                                }));
                            }
                            else if (!IsDisposed)
                            {
                                btnPlay_Click(this, EventArgs.Empty);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (InvokeRequired)
                        {
                            Invoke(new Action(() =>
                            {
                                if (!IsDisposed)
                                {
                                    MessageBox.Show($"Error resuming with new voice: {ex.Message}", "Voice Change Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }));
                        }
                        else if (!IsDisposed)
                        {
                            MessageBox.Show($"Error resuming with new voice: {ex.Message}", "Voice Change Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Initializes the speech synthesizers and populates the voice dropdown.
        /// </summary>
        private void InitializeSpeechSynthesizer()
        {
            // Initialize both speech synthesizers.
            winRtSynth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            systemSynth = new System.Speech.Synthesis.SpeechSynthesizer();

            comboVoices.Items.Clear();

            // Subscribe to the DropDown event to pause speech when dropdown is opened.
            comboVoices.DropDown += ComboVoices_DropDown;

            try
            {
                // Create a dictionary to store voice type mapping.
                Dictionary<string, bool> voiceSystemMap = new Dictionary<string, bool>();

                // Add Windows Modern voices first.
                List<VoiceInformation> winRtVoices = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices.OrderBy(v => v.DisplayName).ToList();

                foreach (VoiceInformation voice in winRtVoices)
                {
                    string voiceName = voice.DisplayName;
                    voiceSystemMap[voiceName] = false; // false = Windows.Media.
                    comboVoices.Items.Add(voiceName);
                }

                // Then add System.Speech voices.
                ReadOnlyCollection<InstalledVoice> systemVoices = systemSynth.GetInstalledVoices();

                foreach (InstalledVoice voice in systemVoices)
                {
                    if (voice.Enabled)
                    {
                        VoiceInfo info = voice.VoiceInfo;
                        string voiceName = info.Name;

                        // Skip duplicates that are already added as Windows.Media voices.
                        if (!voiceSystemMap.ContainsKey(voiceName))
                        {
                            voiceSystemMap[voiceName] = true; // true = System.Speech.
                            comboVoices.Items.Add(voiceName);
                        }
                    }
                }

                // Store the voice system mapping in the form's tag for later use.
                Tag = voiceSystemMap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing text-to-speech voices: {ex.Message}\n\nPlease ensure your system has speech voices installed.", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (comboVoices.Items.Count > 0)
            {
                // Try to find modern voices first, then fallback to narrator voices.
                string[] preferredVoices = new[] { "Guy" };

                foreach (string voiceName in preferredVoices)
                {
                    for (int i = 0; i < comboVoices.Items.Count; i++)
                    {
                        string? item = comboVoices.Items[i]?.ToString();
                        if (item == null)
                        {
                            continue;
                        }

                        if (item.Contains(voiceName, StringComparison.OrdinalIgnoreCase))
                        {
                            comboVoices.SelectedIndex = i;
                            return;
                        }
                    }
                }

                // If no preferred voice found, select the first actual voice (not a header).
                if (comboVoices.Items.Count > 0)
                {
                    // Find the first non-header item.
                    for (int i = 0; i < comboVoices.Items.Count; i++)
                    {
                        string? item = comboVoices.Items[i]?.ToString();
                        if (item != null && !item.StartsWith("---", StringComparison.Ordinal))
                        {
                            comboVoices.SelectedIndex = i;
                            return;
                        }
                    }

                    // If all items are headers (unlikely), select the first one.
                    comboVoices.SelectedIndex = 0;
                }
            }
            else
            {
                MessageBox.Show("No text-to-speech voices found on this system.", "Voice Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Handles the play button click event.
        /// </summary>
        private async void btnPlay_Click(object? sender, EventArgs e)
        {
            stopRequested = false;

            // Check if a voice is already playing (and not paused) - return early.
            bool systemSpeechActive = useSystemSpeech && systemSynth != null && systemSynth.State == SynthesizerState.Speaking;
            bool winRtActive = !useSystemSpeech && isPlayingWinRt;
            if ((systemSpeechActive || winRtActive) && !isPaused)
            {
                return;
            }

            if (isPaused)
            {
                isPaused = false;
                isResumingFromPause = true; // Set flag to indicate we're resuming.
                UpdatePauseButtonState();

                if (pausedSentenceIndex >= 0) // Continue with the same sentence that was interrupted.
                {

                    // Split text into sentences to get the full list.
                    List<string> allSentences = SplitIntoSentences(richTextBox.Text ?? string.Empty);

                    if (pausedSentenceIndex < allSentences.Count)
                    {
                        // Force reset the current sentence index to the paused index.
                        currentSentenceIndex = pausedSentenceIndex;
                        
                        // Update current sentence info to ensure consistency.
                        currentSentence = allSentences[pausedSentenceIndex];
                        
                        // Restore highlighting for the current sentence.
                        RestoreCurrentSentenceHighlight();

                        await ContinueFromSentence(allSentences, pausedSentenceIndex);
                        
                        isResumingFromPause = false;
                        return;
                    }
                    else
                    {
                        // Reset to beginning if index is invalid.
                        currentSentenceIndex = 0;
                        currentSentence = "";
                        currentSentenceStartPosition = 0;
                        currentSentenceLength = 0;
                    }
                }
                else
                {
                    // Reset state if no valid index.
                    currentSentenceIndex = 0;
                    currentSentence = "";
                    currentSentenceStartPosition = 0;
                    currentSentenceLength = 0;
                }
            }

            isPaused = false;
            UpdatePauseButtonState();
            
            if (currentSentenceIndex == 0)
            {
                currentSentence = "";
                currentSentenceStartPosition = 0;
                currentSentenceLength = 0;
            }

            // Get selected voice and determine which system to use.
            if (comboVoices.SelectedIndex >= 0 && comboVoices.SelectedItem != null)
            {
                string selectedVoice = comboVoices.SelectedItem.ToString() ?? string.Empty;

                // Skip headers in dropdown.
                if (selectedVoice.StartsWith("---", StringComparison.Ordinal))
                {
                    // Try to select the next item in the list that's not a header.
                    for (int i = comboVoices.SelectedIndex + 1; i < comboVoices.Items.Count; i++)
                    {
                        string nextVoice = comboVoices.Items[i]?.ToString() ?? string.Empty;
                        if (!nextVoice.StartsWith("---", StringComparison.Ordinal))
                        {
                            comboVoices.SelectedIndex = i;
                            return; // Will trigger this method again with the new selection.
                        }
                    }

                    // If we couldn't find a voice after this header, try before.
                    for (int i = comboVoices.SelectedIndex - 1; i >= 0; i--)
                    {
                        string prevVoice = comboVoices.Items[i]?.ToString() ?? string.Empty;
                        if (!prevVoice.StartsWith("---", StringComparison.Ordinal))
                        {
                            comboVoices.SelectedIndex = i;
                            return; // Will trigger this method again with the new selection.
                        }
                    }

                    // If we get here, there are no valid voices, only headers.
                    return;
                }

                // Set voice in synthesizers.
                Dictionary<string, bool>? voiceToSystem = Tag as Dictionary<string, bool>;

                if (voiceToSystem != null && voiceToSystem.TryGetValue(selectedVoice, out bool isSystemSpeech))
                {
                    useSystemSpeech = isSystemSpeech;

                    if (useSystemSpeech)
                    {
                        try
                        {
                            systemSynth?.SelectVoice(selectedVoice);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error selecting System.Speech voice: {ex.Message}", "Voice Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        try
                        {
                            VoiceInformation? voice = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.DisplayName == selectedVoice);
                            if (voice != null)
                            {
                                if (winRtSynth != null)
                                {
                                    winRtSynth.Voice = voice;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error selecting Windows.Media voice: {ex.Message}", "Voice Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }

            // Reset highlight.
            richTextBox.SelectAll();
            richTextBox.SelectionBackColor = Color.White;

            string text = richTextBox.Text ?? string.Empty;
            List<string> sentences;
            int sentenceCount = 0;

            // Split text into sentences with loading cursor.
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                sentences = SplitIntoSentences(text);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }

            sentenceCount = sentences.Count;

            int searchStartIndex = 0;
            // Don't reset currentSentenceIndex if we're continuing from a pause.

            if (sentenceCount > 0)
            {
                try
                {
                    // Process sentences in order, starting from currentSentenceIndex.
                    for (int i = currentSentenceIndex; i < sentences.Count; i++)
                    {
                        if (stopRequested)
                        {
                            break;
                        }

                        // Check for pause with shorter delay for better responsiveness.
                        while (isPaused)
                        {
                            await Task.Delay(20);
                            if (stopRequested)
                            {
                                break;
                            }
                        }

                        if (stopRequested)
                        {
                            break;
                        }

                        // Update UI.
                        currentSentenceIndex = i;
                        currentSentence = sentences[i];

                        // Apply abbreviation replacement only for speech.
                        string processedSentence = ReplaceAbbreviations(sentences[i]);

                        // Set status in window title.
                        if (InvokeRequired)
                        {
                            Invoke(new Action(() =>
                            {
                                if (!IsDisposed)
                                {
                                    Text = $"TextNarrator - Reading sentence {i + 1} of {sentenceCount}";
                                }
                            }));
                        }
                        else if (!IsDisposed)
                        {
                            Text = $"TextNarrator - Reading sentence {i + 1} of {sentenceCount}";
                        }

                        // Speak the sentence and wait for completion.
                        int nextStartIndex = await SpeakSentenceWithHighlight(processedSentence, sentences[i], searchStartIndex);
                        searchStartIndex = nextStartIndex;
                        
                        // If paused during or after speaking, we already captured the index in btnPause_Click.
                        if (isPaused && !isResumingFromPause)
                        {
                            break; // Exit the loop.
                        }
                        
                        // Check if this was the last sentence.
                        if (i == sentences.Count - 1 && !isPaused && !stopRequested)
                        {
                            currentSentenceIndex = 0; // Reset for next time.
                        }

                        // Add configurable delay after sentence completion.
                        if (SENTENCE_PAUSE_DELAY_MS > 0 && !stopRequested && !isPaused)
                        {
                            await Task.Delay(SENTENCE_PAUSE_DELAY_MS);

                            // Check again after delay in case user pressed stop/pause during the delay.
                            if (stopRequested || isPaused)
                            {
                                break;
                            }
                        }

                        if (stopRequested)
                        {
                            break;
                        }
                        
                        // pausedSentenceIndex is only updated when the pause button is clicked.
                        
                        // If we've successfully spoken a sentence after resuming from pause, reset the resuming state.
                        if (isResumingFromPause)
                        {
                            isResumingFromPause = false;
                        }
                        // If we've successfully spoken a sentence after resuming from pause, make sure the pausedSentenceIndex moves forward with us.
                        if (isResumingFromPause)
                        {
                            isResumingFromPause = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during playback: {ex.Message}", "Playback Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // Clear highlighting when finished.
            richTextBox.SelectAll();
            richTextBox.SelectionBackColor = Color.White;
        }

        /// <summary>
        /// Continues speech playback from a specific sentence index.
        /// </summary>
        /// <param name="allSentences">The complete list of sentences</param>
        /// <param name="startIndex">The index to start from</param>
        private async Task ContinueFromSentence(List<string> allSentences, int startIndex)
        {
            int sentenceCount = allSentences.Count;

            // Ensure we're using the right sentence index when resuming.
            if (startIndex != currentSentenceIndex)
            {
                // Force synchronization with the requested start index.
                currentSentenceIndex = startIndex;
            }

            int searchStartIndex = 0; // Track position for sentence highlighting.
            for (int j = 0; j < startIndex && j < allSentences.Count; j++)
            {
                int foundPos = (richTextBox.Text ?? "").IndexOf(allSentences[j], searchStartIndex, StringComparison.OrdinalIgnoreCase);
                if (foundPos >= 0)
                {
                    searchStartIndex = foundPos + allSentences[j].Length;
                }
            }

            try
            {
                // Process sentences from the start index (INCLUDING the start index sentence).
                for (int i = startIndex; i < allSentences.Count; i++)
                {
                    if (stopRequested)
                    {
                        break;
                    }

                    // Check for pause with shorter delay for better responsiveness.
                    while (isPaused)
                    {
                        await Task.Delay(20);
                        if (stopRequested)
                        {
                            break;
                        }
                    }

                    if (stopRequested) break;

                    // Update UI.
                    currentSentenceIndex = i;
                    currentSentence = allSentences[i];

                    // Apply abbreviation replacement only for speech.
                    string processedSentence = ReplaceAbbreviations(allSentences[i]);

                    // Set status in window title.
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            if (!IsDisposed)
                            {
                                Text = $"TextNarrator - Reading sentence {i + 1} of {sentenceCount}";
                            }
                        }));
                    }
                    else if (!IsDisposed)
                    {
                        Text = $"TextNarrator - Reading sentence {i + 1} of {sentenceCount}";
                    }
                    
                    // Check if we're paused before speaking.
                    if (isPaused)
                    {
                        // Store the current index for later resumption (already set in btnPause_Click).
                        break; // Exit the loop.
                    }
                    
                    // Speak and highlight the current sentence.
                    int nextStartIndex = await SpeakSentenceWithHighlight(processedSentence, allSentences[i], searchStartIndex);
                    searchStartIndex = nextStartIndex;
                    
                    // Check if we got paused during or after speaking.
                    if (isPaused)
                    {
                        // Update the UI before exiting the loop.
                        if (IsHandleCreated && !IsDisposed)
                        {
                            if (InvokeRequired)
                            {
                                Invoke(new Action(() =>
                                {
                                    if (!IsDisposed)
                                    {
                                        Text = $"TextNarrator - PAUSED at sentence {i + 1} of {sentenceCount}";
                                    }
                                }));
                            }
                            else if (!IsDisposed)
                            {
                                Text = $"TextNarrator - PAUSED at sentence {i + 1} of {sentenceCount}";
                            }
                        }
                        break; // Exit the loop.
                    }

                    // Add configurable delay after sentence completion.
                    if (SENTENCE_PAUSE_DELAY_MS > 0 && !stopRequested && !isPaused)
                    {
                        await Task.Delay(SENTENCE_PAUSE_DELAY_MS);

                        // Check again after delay in case user pressed stop/pause during the delay.
                        if (stopRequested || isPaused)
                        {
                            break;
                        }
                    }

                    if (stopRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during playback: {ex.Message}", "Playback Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Clear highlighting when finished.
            richTextBox.SelectAll();
            richTextBox.SelectionBackColor = Color.White;
        }

        /// <summary>
        /// Speaks a sentence while highlighting it in the text.
        /// </summary>
        /// <param name="processedSentence">The sentence with abbreviations replaced</param>
        /// <param name="originalSentence">The original unmodified sentence</param>
        /// <param name="searchStartIndex">The position to start searching from</param>
        /// <returns>The index position after the spoken sentence</returns>
        private async Task<int> SpeakSentenceWithHighlight(string processedSentence, string originalSentence, int searchStartIndex)
        {
            // Find the original sentence in the text and highlight it entirely.
            int sentenceIndex = (richTextBox.Text ?? "").IndexOf(originalSentence, searchStartIndex, StringComparison.OrdinalIgnoreCase);
            if (sentenceIndex >= 0)
            {
                // Store position and length for better pause/resume tracking.
                currentSentenceStartPosition = sentenceIndex;
                currentSentenceLength = originalSentence.Length;

                // Update the UI to highlight the entire original sentence.
                if (richTextBox.InvokeRequired)
                {
                    richTextBox.Invoke(new Action(() =>
                    {
                        if (!IsDisposed)
                        {
                            richTextBox.SelectAll();
                            richTextBox.SelectionBackColor = Color.White;
                            richTextBox.Select(sentenceIndex, originalSentence.Length);
                            richTextBox.SelectionBackColor = Color.Yellow;
                            richTextBox.ScrollToCaret();
                        }
                    }));
                }
                else if (!IsDisposed)
                {
                    richTextBox.SelectAll();
                    richTextBox.SelectionBackColor = Color.White;
                    richTextBox.Select(sentenceIndex, originalSentence.Length);
                    richTextBox.SelectionBackColor = Color.Yellow;
                    richTextBox.ScrollToCaret();
                }
            }
            else
            {
                // Try searching from the beginning if not found.
                sentenceIndex = (richTextBox.Text ?? "").IndexOf(originalSentence, 0, StringComparison.OrdinalIgnoreCase);
                if (sentenceIndex >= 0)
                {
                    currentSentenceStartPosition = sentenceIndex;
                    currentSentenceLength = originalSentence.Length;

                    // Update the UI to highlight the entire original sentence.
                    if (richTextBox.InvokeRequired)
                    {
                        richTextBox.Invoke(new Action(() =>
                        {
                            if (!IsDisposed)
                            {
                                richTextBox.SelectAll();
                                richTextBox.SelectionBackColor = Color.White;
                                richTextBox.Select(sentenceIndex, originalSentence.Length);
                                richTextBox.SelectionBackColor = Color.Yellow;
                                richTextBox.ScrollToCaret();
                            }
                        }));
                    }
                    else if (!IsDisposed)
                    {
                        richTextBox.SelectAll();
                        richTextBox.SelectionBackColor = Color.White;
                        richTextBox.Select(sentenceIndex, originalSentence.Length);
                        richTextBox.SelectionBackColor = Color.Yellow;
                        richTextBox.ScrollToCaret();
                    }
                }
                else
                {
                    // Use the search start index as fallback.
                    sentenceIndex = searchStartIndex;
                    currentSentenceStartPosition = searchStartIndex;
                    currentSentenceLength = originalSentence.Length;
                }
            }

            // Create completion source for speech tracking.
            TaskCompletionSource<bool> speechCompletionSource = new TaskCompletionSource<bool>();

            // Setup cancellation token.
            playbackCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = playbackCancellation.Token;

            // Begin speech synthesis.
            try
            {
                if (useSystemSpeech)
                {
                    // Use System.Speech API with proper event handling.
                    if (systemSynth != null)
                    {
                        // Set up event handler for completion.
                        EventHandler<SpeakCompletedEventArgs>? completedHandler = null;
                        completedHandler = (s, e) =>
                        {
                            systemSynth.SpeakCompleted -= completedHandler; // Remove handler to prevent memory leaks.
                            speechCompletionSource.TrySetResult(!e.Cancelled && e.Error == null);
                        };
                        systemSynth.SpeakCompleted += completedHandler;

                        Prompt prompt = new Prompt(processedSentence);
                        systemSynth.SpeakAsync(prompt);

                        // Wait for completion or cancellation with instant response.
                        while (systemSynth.State == System.Speech.Synthesis.SynthesizerState.Speaking)
                        {
                            if (stopRequested || cancellationToken.IsCancellationRequested)
                            {
                                systemSynth.SpeakAsyncCancelAll();
                                speechCompletionSource.TrySetResult(false);
                                break;
                            }
                            if (isPaused)
                            {
                                systemSynth.SpeakAsyncCancelAll();
                                speechCompletionSource.TrySetResult(false);
                                break;
                            }
                            await Task.Delay(20, cancellationToken); // Shorter delay for better responsiveness.
                        }
                    }
                    else
                    {
                        speechCompletionSource.TrySetResult(false);
                    }
                }
                else
                {
                    // Use event-based WinRT playback for instant cancellation.
                    if (winRtSynth == null)
                    {
                        speechCompletionSource.TrySetResult(false);
                        return sentenceIndex >= 0 ? sentenceIndex + originalSentence.Length : searchStartIndex + 1;
                    }

                    // Force voice selection before synthesis.
                    if (comboVoices.SelectedIndex >= 0 && comboVoices.SelectedItem != null)
                    {
                        string selectedVoice = comboVoices.SelectedItem.ToString() ?? string.Empty;
                        if (!selectedVoice.StartsWith("---", StringComparison.Ordinal))
                        {
                            VoiceInformation? voice = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.DisplayName == selectedVoice);
                            if (voice != null)
                            {
                                winRtSynth.Voice = voice;
                            }
                        }
                    }

                    try
                    {
                        using (SpeechSynthesisStream stream = await winRtSynth.SynthesizeTextToStreamAsync(processedSentence))
                        {
                            // Dispose of previous player.
                            if (mediaPlayer != null)
                            {
                                mediaPlayer.Dispose();
                                mediaPlayer = null;
                            }

                            if (!stopRequested && !isPaused && !cancellationToken.IsCancellationRequested)
                            {
                                // Create MediaPlayer for event-based playback.
                                mediaPlayer = new MediaPlayer();
                                isPlayingWinRt = true;

                                // Set up completion event handler.
                                TypedEventHandler<MediaPlayer, object>? endedHandler = null;
                                endedHandler = (s, e) =>
                                {
                                    if (mediaPlayer != null)
                                    {
                                        mediaPlayer.MediaEnded -= endedHandler;
                                    }
                                    isPlayingWinRt = false;
                                    speechCompletionSource.TrySetResult(true);
                                };

                                // Set up error event handler.
                                TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs>? failedHandler = null;
                                failedHandler = (s, e) =>
                                {
                                    if (mediaPlayer != null)
                                    {
                                        mediaPlayer.MediaFailed -= failedHandler;
                                        mediaPlayer.MediaEnded -= endedHandler;
                                    }
                                    isPlayingWinRt = false;
                                    speechCompletionSource.TrySetResult(false);
                                };

                                mediaPlayer.MediaEnded += endedHandler;
                                mediaPlayer.MediaFailed += failedHandler;

                                // Create MediaSource from stream.
                                IRandomAccessStream randomAccessStream = stream.CloneStream();
                                IMediaPlaybackSource mediaSource = MediaSource.CreateFromStream(randomAccessStream, "audio/wav");
                                mediaPlayer.Source = mediaSource;

                                mediaPlayer.Play();

                                // Wait for completion or cancellation with responsive checking.
                                while (isPlayingWinRt)
                                {
                                    await Task.Delay(20, cancellationToken);

                                    if (stopRequested || isPaused || cancellationToken.IsCancellationRequested)
                                    {
                                        mediaPlayer.Pause();
                                        isPlayingWinRt = false;

                                        // Clean up event handlers.
                                        mediaPlayer.MediaEnded -= endedHandler;
                                        mediaPlayer.MediaFailed -= failedHandler;

                                        if (isPaused)
                                        {
                                            speechCompletionSource.TrySetResult(false);
                                            return sentenceIndex >= 0 ? sentenceIndex + originalSentence.Length : searchStartIndex + 1;
                                        }

                                        speechCompletionSource.TrySetResult(false);
                                        return sentenceIndex >= 0 ? sentenceIndex + originalSentence.Length : searchStartIndex + 1;
                                    }
                                }
                            }
                            else
                            {
                                isPlayingWinRt = false;
                                speechCompletionSource.TrySetResult(false);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("WinRT playback cancelled");
                        isPlayingWinRt = false;
                        speechCompletionSource.TrySetResult(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in WinRT event-based playback: {ex.Message}");
                        isPlayingWinRt = false;
                        speechCompletionSource.TrySetResult(false);
                    }
                }

                // Wait for speech completion.
                await speechCompletionSource.Task;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Speech synthesis cancelled");
            }
            catch (Exception ex)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        if (!IsDisposed)
                        {
                            MessageBox.Show($"Error during speech synthesis: {ex.Message}", "Speech Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }));
                }
                else if (!IsDisposed)
                {
                    MessageBox.Show($"Error during speech synthesis: {ex.Message}", "Speech Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                // Clean up cancellation token.
                if (playbackCancellation != null)
                {
                    playbackCancellation.Dispose();
                    playbackCancellation = null;
                }
            }

            // Return position for next search.
            try
            {
                return sentenceIndex >= 0 ? sentenceIndex + originalSentence.Length : searchStartIndex + 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating next sentence position: {ex.Message}");
                return searchStartIndex + 1;
            }
        }

        /// <summary>
        /// Splits text into sentences with smart handling of paragraphs and formatting.
        /// </summary>
        /// <param name="text">The text to split</param>
        /// <returns>A list of individual sentences</returns>
        private List<string> SplitIntoSentences(string text)
        {
            try
            {
                List<string> sentences = new List<string>();

                if (string.IsNullOrEmpty(text))
                {
                    return sentences;
                }

                // Keep original text structure - abbreviation replacement is only for speech.

                // Split by double line endings for paragraphs and titles.
                string[] paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n", "\r\r" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string paragraph in paragraphs)
                {
                    string trimmedParagraph = paragraph.Trim();
                    if (string.IsNullOrEmpty(trimmedParagraph))
                    {
                        continue;
                    }

                    // Detect titles (markdown style or short text without ending punctuation).
                    bool isTitle = trimmedParagraph.StartsWith("#") || (trimmedParagraph.Length < MAX_TITLE_LENGTH && !trimmedParagraph.EndsWith(".") && !trimmedParagraph.EndsWith("!") && !trimmedParagraph.EndsWith("?"));

                    if (isTitle)
                    {
                        // Treat titles as separate sentences.
                        sentences.Add(trimmedParagraph);
                    }
                    else
                    {
                        // Process regular paragraphs.
                        List<string> paragraphSentences = SplitParagraphIntoSentences(trimmedParagraph);
                        sentences.AddRange(paragraphSentences);
                    }
                }

                // Fallback: treat whole text as one sentence if no sentences found.
                if (sentences.Count == 0 && !string.IsNullOrEmpty(text.Trim()))
                {
                    sentences.Add(text.Trim());
                }

                return sentences;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error splitting text into sentences: {ex.Message}");

                // Emergency fallback.
                List<string> fallbackList = new List<string>();
                if (!string.IsNullOrEmpty(text?.Trim() ?? string.Empty))
                {
                    fallbackList.Add(text?.Trim() ?? string.Empty);
                }
                return fallbackList;
            }
        }

        /// <summary>
        /// Splits a paragraph into individual sentences with special handling for abbreviations.
        /// </summary>
        /// <param name="paragraph">The paragraph text to process</param>
        /// <returns>A list of sentences from the paragraph</returns>
        private List<string> SplitParagraphIntoSentences(string paragraph)
        {
            List<string> sentences = new List<string>();

            // Create a pattern that matches sentence endings but excludes abbreviations from our dictionary.
            // Build negative lookbehind patterns from our Abbreviations dictionary.
            List<string> lookbehindPatterns = new List<string>();

            foreach (string abbreviation in Abbreviations.Keys)
            {
                if (abbreviation.EndsWith("."))
                {
                    // Remove the period and escape special regex characters.
                    string abbrevWithoutPeriod = abbreviation.Substring(0, abbreviation.Length - 1);
                    string escapedAbbrev = Regex.Escape(abbrevWithoutPeriod);
                    lookbehindPatterns.Add($@"(?<!\b{escapedAbbrev})");
                }
            }

            // Combine all negative lookbehinds into one pattern.
            string combinedLookbehinds = string.Join("", lookbehindPatterns);

            // The final pattern: all lookbehinds + sentence-ending punctuation + optional whitespace.
            // Made whitespace optional to catch sentences that end at line breaks or end of text.
            string pattern = combinedLookbehinds + @"[.!?]+\s*";

            try
            {
                // Split paragraph using our regex pattern to identify sentence boundaries.
                string[] parts = Regex.Split(paragraph, pattern);

                if (parts.Length > 1)
                {
                    // Regex split worked, process the parts.
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string part = parts[i].Trim();
                        if (!string.IsNullOrEmpty(part))
                        {
                            // Add the sentence (the split already removed the punctuation, so we need to add it back).
                            // Find where this part ends in the original text to determine what punctuation to add.
                            int partStartInOriginal = paragraph.IndexOf(part.Trim());
                            if (partStartInOriginal >= 0)
                            {
                                int partEndInOriginal = partStartInOriginal + part.Trim().Length;
                                // Look for punctuation immediately after this part.
                                if (partEndInOriginal < paragraph.Length && ".!?".Contains(paragraph[partEndInOriginal]))
                                {
                                    part = part.Trim() + paragraph[partEndInOriginal];
                                }
                            }
                            sentences.Add(part.Trim());
                        }
                    }
                }
                else
                {
                    // Regex split didn't work, fall back to simple split but try to be smarter.
                    string[] simpleParts = paragraph.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                    if (simpleParts.Length > 1)
                    {
                        // Check each part to see if it looks like an abbreviation.
                        List<string> combinedParts = new List<string>();
                        string currentSentence = "";

                        for (int i = 0; i < simpleParts.Length; i++)
                        {
                            string part = simpleParts[i].Trim();
                            currentSentence += part;

                            // Check if this part ends with a known abbreviation.
                            bool endsWithAbbreviation = false;
                            foreach (string abbrev in Abbreviations.Keys)
                            {
                                if (abbrev.EndsWith(".") && part.EndsWith(abbrev.Substring(0, abbrev.Length - 1), StringComparison.OrdinalIgnoreCase))
                                {
                                    endsWithAbbreviation = true;
                                    currentSentence += "."; // Add back the period.
                                    break;
                                }
                            }

                            if (!endsWithAbbreviation || i == simpleParts.Length - 1)
                            {
                                // This is the end of a real sentence.
                                if (i < simpleParts.Length - 1) currentSentence += "."; // Add period if not last.
                                combinedParts.Add(currentSentence.Trim());
                                currentSentence = "";
                            }
                            else
                            {
                                // This was an abbreviation, continue building the sentence.
                                currentSentence += " ";
                            }
                        }

                        sentences.AddRange(combinedParts);
                    }
                    else
                    {
                        // No sentence-ending punctuation found, treat as one sentence.
                        sentences.Add(paragraph);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in regex sentence splitting: {ex.Message}");
                // Ultimate fallback - just return the whole paragraph.
                sentences.Add(paragraph);
            }

            return sentences;
        }

        /// <summary>
        /// Replaces common abbreviations with their full forms to prevent incorrect sentence splitting and improve text-to-speech pronunciation.
        /// </summary>
        private string ReplaceAbbreviations(string text)
        {
            if (string.IsNullOrEmpty(text))
            { 
                return text;
            }

            string originalText = text;

            // Replace each abbreviation with its full form using simple string replacement.
            foreach (KeyValuePair<string, string> kvp in Abbreviations)
            {
                if (text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            return text;
        }

        /// <summary>
        /// Handles the pause button click event.
        /// </summary>
        private void btnPause_Click(object? sender, EventArgs e)
        {
            isPaused = true;
            pausedSentenceIndex = currentSentenceIndex; // Store for resuming later.
            
            InstantStopCurrentSpeech();
            
            UpdatePauseButtonState();
            
            if (IsHandleCreated && !IsDisposed)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        if (!IsDisposed)
                        {
                            Text = $"TextNarrator - PAUSED at sentence {pausedSentenceIndex + 1}";
                        }
                    }));
                }
                else if (!IsDisposed)
                {
                    Text = $"TextNarrator - PAUSED at sentence {pausedSentenceIndex + 1}";
                }
            }
        }

        /// <summary>
        /// Updates the pause button text and appearance.
        /// </summary>
        private void UpdatePauseButtonState()
        {
            // Update on UI thread in case called from background thread.
            if (btnPause.InvokeRequired)
            {
                btnPause.Invoke(new Action(UpdatePauseButtonState));
                return;
            }

            // Always keep as "Pause" with pause symbol, use Play button to resume.
            btnPause.Text = "⏸ Pause";
        }

        /// <summary>
        /// Handles the stop button click event.
        /// </summary>
        private void btnStop_Click(object? sender, EventArgs e)
        {
            stopRequested = true;

            InstantStopCurrentSpeech();

            Action clearHighlighting = () =>
            {
                if (!IsDisposed)
                {
                    richTextBox.SelectAll();
                    richTextBox.SelectionBackColor = Color.White;
                    richTextBox.SelectionLength = 0; // Clear selection.
                }
            };

            if (richTextBox.InvokeRequired)
            {
                richTextBox.Invoke(clearHighlighting);
            }
            else if (!IsDisposed)
            {
                clearHighlighting();
            }

            isPaused = false;
            currentSentence = "";
            currentSentenceStartPosition = 0;
            currentSentenceLength = 0;
            currentSentenceIndex = 0;

            UpdatePauseButtonState();
        }

        /// <summary>
        /// Event handler for when the voice dropdown is opened.
        /// Pauses speech playback when the user opens the dropdown menu.
        /// </summary>
        private void ComboVoices_DropDown(object? sender, EventArgs e)
        {
            try
            {
                // Check if speech is currently playing.
                bool systemSpeechActive = false;
                try
                {
                    systemSpeechActive = useSystemSpeech && systemSynth?.State == SynthesizerState.Speaking;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking System.Speech state: {ex.Message}");
                }

                bool winRtActive = !useSystemSpeech && isPlayingWinRt;

                if (systemSpeechActive || winRtActive)
                {
                    // Pause the speech.
                    isPaused = true;

                    // Use instant cutoff method.
                    InstantStopCurrentSpeech();

                    // Update the pause button to reflect paused state.
                    UpdatePauseButtonState();
                }
            }
            catch (Exception ex)
            {
                // Only show message box for significant errors, not routine dropdown issues.
                if (ex is not NullReferenceException && ex.Message.Contains("critical") || ex.Message.Contains("fatal"))
                {
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            if (!IsDisposed)
                            {
                                MessageBox.Show($"Error changing voice: {ex.Message}", "Voice Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }));
                    }
                    else if (!IsDisposed)
                    {
                        MessageBox.Show($"Error changing voice: {ex.Message}", "Voice Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        // Helper method to find and highlight the first sentence.
        /// <summary>
        /// Highlights the first sentence in the text.
        /// </summary>
        private void HighlightFirstSentence()
        {
            if (string.IsNullOrEmpty(richTextBox.Text))
            {
                return;
            }

            try
            {
                // Split text into sentences (no pre-processing, so original = processed).
                List<string> sentences = SplitIntoSentences(richTextBox.Text);

                if (sentences.Count == 0)
                {
                    return;
                }

                // Get the first sentence.
                string firstSentence = sentences[0];

                // Find it in the text.
                int firstSentenceIndex = (richTextBox.Text ?? "").IndexOf(firstSentence);
                if (firstSentenceIndex < 0)
                {
                    return;
                }

                // Clear all highlighting.
                richTextBox.SelectAll();
                richTextBox.SelectionBackColor = Color.White;

                // Highlight the first sentence.
                richTextBox.Select(firstSentenceIndex, firstSentence.Length);
                richTextBox.SelectionBackColor = Color.Yellow;
                richTextBox.ScrollToCaret();

                // Set current sentence tracking for proper highlighting.
                currentSentence = firstSentence;
                currentSentenceStartPosition = firstSentenceIndex;
                currentSentenceLength = firstSentence.Length;
                currentSentenceIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error highlighting first sentence: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the restart button click event.
        /// </summary>
        private void btnRestart_Click(object? sender, EventArgs e)
        {
            try
            {
                btnStop_Click(this, EventArgs.Empty);
                
                Action playAction = () =>
                {
                    if (!IsDisposed)
                    {
                        // Highlight first sentence.
                        HighlightFirstSentence();
                        
                        // Start playback by calling the play button's click handler.
                        btnPlay_Click(this, EventArgs.Empty);
                    }
                };
                
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        if (!IsDisposed)
                        {
                            Task.Delay(100).ContinueWith(_ =>
                            {
                                if (IsHandleCreated && !IsDisposed)
                                {
                                    Invoke(playAction);
                                }
                            });
                        }
                    }));
                }
                else
                {
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        if (IsHandleCreated && !IsDisposed)
                        {
                            if (InvokeRequired)
                            {
                                Invoke(playAction);
                            }
                            else
                            {
                                playAction();
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        if (!IsDisposed)
                        {
                            MessageBox.Show($"Error restarting playback: {ex.Message}", "Restart Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }));
                }
                else if (!IsDisposed)
                {
                    MessageBox.Show($"Error restarting playback: {ex.Message}", "Restart Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
