namespace TextNarrator
{
	/// <summary>
	/// Manages text highlighting in the RichTextBox with proper UI thread marshalling.
	/// </summary>
	public class HighlightManager
	{
		private readonly RichTextBox _richTextBox;
		private readonly Control _parentControl;

		public HighlightManager(RichTextBox richTextBox, Control parentControl)
		{
			_richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
			_parentControl = parentControl ?? throw new ArgumentNullException(nameof(parentControl));
		}

		/// <summary>
		/// Highlights a specific sentence in the text.
		/// </summary>
		public void HighlightSentence(int startPosition, int length)
		{
			ExecuteOnUIThread(() =>
			{
				_richTextBox.SelectAll();
				_richTextBox.SelectionBackColor = Color.White;
				_richTextBox.Select(startPosition, length);
				_richTextBox.SelectionBackColor = Color.Yellow;
				_richTextBox.ScrollToCaret();
			});
		}

		/// <summary>
		/// Clears all highlighting from the text.
		/// </summary>
		public void ClearHighlight()
		{
			ExecuteOnUIThread(() =>
			{
				_richTextBox.SelectAll();
				_richTextBox.SelectionBackColor = Color.White;
				_richTextBox.SelectionLength = 0;
			});
		}

		/// <summary>
		/// Finds and highlights the first occurrence of a sentence in the text.
		/// </summary>
		/// <param name="sentence">The sentence to highlight.</param>
		/// <param name="searchStartIndex">Position to start searching from.</param>
		/// <returns>The position information of the highlighted sentence.</returns>
		public SentencePosition FindAndHighlight(string sentence, int searchStartIndex = 0)
		{
			string text = GetText();
			int sentenceIndex = text.IndexOf(sentence, searchStartIndex, StringComparison.OrdinalIgnoreCase);

			if (sentenceIndex < 0)
			{
				// Try from beginning.
				sentenceIndex = text.IndexOf(sentence, 0, StringComparison.OrdinalIgnoreCase);
			}

			if (sentenceIndex < 0)
			{
				// Fallback to search start index.
				sentenceIndex = searchStartIndex;
			}

			HighlightSentence(sentenceIndex, sentence.Length);

			return new SentencePosition
			{
				StartPosition = sentenceIndex,
				Length = sentence.Length,
				NextSearchIndex = sentenceIndex >= 0 ? sentenceIndex + sentence.Length : searchStartIndex + 1
			};
		}

		/// <summary>
		/// Gets the current text from the RichTextBox.
		/// </summary>
		public string GetText()
		{
			if (_richTextBox.InvokeRequired)
			{
				return (string)_richTextBox.Invoke(new Func<string>(() => _richTextBox.Text ?? string.Empty));
			}

			return _richTextBox.Text ?? string.Empty;
		}

		private void ExecuteOnUIThread(Action action)
		{
			if (_parentControl.IsDisposed)
			{
				return;
			}

			if (_parentControl.InvokeRequired)
			{
				_parentControl.Invoke(action);
			}
			else
			{
				action();
			}
		}
	}

	/// <summary>
	/// Information about a sentence's position in the text.
	/// </summary>
	public class SentencePosition
	{
		public int StartPosition { get; set; }
		public int Length { get; set; }
		public int NextSearchIndex { get; set; }
	}
}
