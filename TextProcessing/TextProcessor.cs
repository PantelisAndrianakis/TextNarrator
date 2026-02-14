using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace TextNarrator
{
	/// <summary>
	/// Handles text processing including sentence splitting and abbreviation replacement.
	/// </summary>
	public class TextProcessor
	{
		private const int MAX_TITLE_LENGTH = 100;

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
			{"Ref.", "Reference"},
			
			// Special cases.
			{"#", ""},
			{"***", ""},
			{"**", ""}
		};

		/// <summary>
		/// Splits text into sentences with smart handling of paragraphs and formatting.
		/// </summary>
		public static List<string> SplitIntoSentences(string text)
		{
			List<string> sentences = new List<string>();

			if (string.IsNullOrEmpty(text))
			{
				return sentences;
			}

			try
			{
				// Don't preprocess ellipsis. Treat it as a sentence delimiter, then filter out standalone ellipsis sentences.
				// Split by double line endings for paragraphs and titles.
				string[] paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n", "\r\r" }, StringSplitOptions.RemoveEmptyEntries);

				foreach (string paragraph in paragraphs)
				{
					string trimmedParagraph = paragraph.Trim();
					if (string.IsNullOrEmpty(trimmedParagraph))
					{
						continue;
					}

					if (IsTitle(trimmedParagraph))
					{
						sentences.Add(trimmedParagraph);
					}
					else
					{
						sentences.AddRange(SplitParagraphIntoSentences(trimmedParagraph));
					}
				}

				// Filter out sentences that are only single punctuation marks.
				sentences = sentences.Where(s => !IsOnlySinglePunctuationMark(s)).ToList();
				
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
				if (!string.IsNullOrEmpty(text?.Trim()))
				{
					return new List<string> { text.Trim() };
				}

				return new List<string>();
			}
		}

		/// <summary>
		/// Replaces abbreviations with their full forms for better TTS pronunciation.
		/// </summary>
		public static string ReplaceAbbreviations(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			foreach (KeyValuePair<string, string> kvp in Abbreviations)
			{
				if (text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
				{
					text = text.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
				}
			}

			return text;
		}

		private static bool IsTitle(string text)
		{
			return text.StartsWith("#") || (text.Length < MAX_TITLE_LENGTH && !text.EndsWith(".") && !text.EndsWith("!") && !text.EndsWith("?"));
		}

		private static List<string> SplitParagraphIntoSentences(string paragraph)
		{
			List<string> sentences = new List<string>();

			// Build negative lookbehind patterns from abbreviations dictionary.
			List<string> lookbehindPatterns = Abbreviations.Keys
				.Where(abbrev => abbrev.EndsWith("."))
				.Select(abbrev => $@"(?<!\b{Regex.Escape(abbrev.Substring(0, abbrev.Length - 1))})")
				.ToList();

			// Combine all negative lookbehinds + sentence-ending punctuation.
			string combinedLookbehinds = string.Join("", lookbehindPatterns);
			// Handle abbreviations first, then include ellipsis and other delimiters.
			// Apply the lookbehinds to each part of the pattern to prevent splitting at abbreviations.
			string pattern =
				// Handle periods not in abbreviations or ellipsis.
				combinedLookbehinds + @"\.(?!\.|\s*[a-z])(?=\s*[A-Z0-9]|\s)" + "|" +
				// Handle ellipsis as a separate case.
				@"(?:\.\.\.|…)" + "|" +
				// Handle other punctuation.
				@"[!?]+" + "|" +
				// Handle spaced hyphens/em dashes without capital letter requirement.
				@"(\s-\s|\s—\s)";

			try
			{
				string[] parts = Regex.Split(paragraph, pattern);

				if (parts.Length > 1)
				{
					sentences.AddRange(ProcessRegexParts(parts, paragraph));
				}
				else
				{
					sentences.AddRange(FallbackSplit(paragraph));
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error in regex sentence splitting: {ex.Message}");
				sentences.Add(paragraph);
			}

			return sentences;
		}

		private static List<string> ProcessRegexParts(string[] parts, string originalParagraph)
		{
			List<string> sentences = new List<string>();

			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				if (string.IsNullOrEmpty(part))
				{
					continue;
				}

				// Find punctuation that was removed by split.
				int partStartInOriginal = originalParagraph.IndexOf(part.Trim(), StringComparison.Ordinal);
				if (partStartInOriginal >= 0)
				{
					int partEndInOriginal = partStartInOriginal + part.Trim().Length;
					bool isValidPunctuation = false;
					
					// Check for standard punctuation.
					if (partEndInOriginal < originalParagraph.Length && ".!?".Contains(originalParagraph[partEndInOriginal]))
					{
						isValidPunctuation = true;
						part = part.Trim() + originalParagraph[partEndInOriginal];
					}
					// Check for spaced hyphens and em dashes.
					else if (partEndInOriginal + 2 < originalParagraph.Length)
					{
						char currentChar = originalParagraph[partEndInOriginal];
						char nextChar = originalParagraph[partEndInOriginal + 1];
						char afterNextChar = originalParagraph[partEndInOriginal + 2];
						
						// Only if it's a space followed by hyphen/emdash followed by another space.
						if (currentChar == ' ' && (nextChar == '-' || nextChar == '—') && afterNextChar == ' ')
						{
							isValidPunctuation = true;
							// Include both the space and the punctuation.
							part = part.Trim() + " " + nextChar;
							// Skip to after the punctuation.
							partEndInOriginal += 1;
						}
					}
					
					if (isValidPunctuation)
					{
						part = part.Trim() + originalParagraph[partEndInOriginal];
					}
				}
				sentences.Add(part.Trim());
			}

			return sentences;
		}

		private static List<string> FallbackSplit(string paragraph)
		{
			List<string> sentences = new List<string>();
			
			// Use regex to split on standard punctuation and hyphens followed by capital letters.
			// Apply same pattern logic as the main regex for consistency.
			string pattern =
				// Handle periods not in abbreviations or ellipsis.
				@"(?<![A-Z][a-z]\.)\.(?!\.|\s*[a-z])(?=\s*[A-Z0-9]|\s)" + "|" +
				// Handle ellipsis as a separate case.
				@"(?:\.\.\.|…)" + "|" +
				// Handle other punctuation.
				@"[!?]+" + "|" +
				// Handle spaced hyphens/em dashes without capital letter requirement.
				@"(\s-\s|\s—\s)";
			string[] simpleParts = Regex.Split(paragraph, pattern);

			// Process the parts into sentences.
			if (simpleParts.Length > 1)
			{
				StringBuilder currentSentence = new StringBuilder();
				
				for (int i = 0; i < simpleParts.Length; i++)
				{
					string part = simpleParts[i].Trim();
					if (string.IsNullOrEmpty(part))
					{
						continue;
					}
					
					// Check if this part is a standard delimiter.
					bool isDelimiter = part == "." || part == "!" || part == "?";
					
					// Check if this part is a spaced hyphen or em dash.
					bool isHyphen = part == " - " || part == " — " || part == "-" || part == "—";
					
					if (isDelimiter || isHyphen)
					{
						// Standard punctuation.
						currentSentence.Append(part);
						
						// Check if it's a period in an abbreviation.
						if (part == "." && i > 0)
						{
							string prevText = simpleParts[i-1].Trim();
							bool isAbbreviation = Abbreviations.Keys
								.Where(abbrev => abbrev.EndsWith("."))
								.Any(abbrev => prevText.EndsWith(abbrev.Substring(0, abbrev.Length - 1), StringComparison.OrdinalIgnoreCase));
							
							if (isAbbreviation)
							{
								// This is an abbreviation, continue building the sentence.
								currentSentence.Append(" ");
								continue;
							}
						}
						
						// End of sentence.
						sentences.Add(currentSentence.ToString().Trim());
						currentSentence.Clear();
					}
					else
					{
						// Regular text, add to current sentence.
						currentSentence.Append(part);
					}
				}
				
				// Add any remaining content.
				if (currentSentence.Length > 0)
				{
					sentences.Add(currentSentence.ToString().Trim());
				}
			}
			else
			{
				// No delimiters found, use the whole paragraph.
				sentences.Add(paragraph);
			}

			return sentences;
		}
		
		/// <summary>
		/// Checks if the string contains only a single punctuation mark (possibly with whitespace).
		/// </summary>
		private static bool IsOnlySinglePunctuationMark(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			
			string trimmed = text.Trim();
			return trimmed == "-" || trimmed == "—" || trimmed == "." || trimmed == "!" || trimmed == "?" || trimmed == "..." || trimmed == "…";
		}
	}
}
