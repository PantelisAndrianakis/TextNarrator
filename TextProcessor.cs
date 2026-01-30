using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
			{"Ref.", "Reference"}
		};

		/// <summary>
		/// Splits text into sentences with smart handling of paragraphs and formatting.
		/// </summary>
		public List<string> SplitIntoSentences(string text)
		{
			List<string> sentences = new List<string>();

			if (string.IsNullOrEmpty(text))
			{
				return sentences;
			}

			try
			{
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
		public string ReplaceAbbreviations(string text)
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

		private bool IsTitle(string text)
		{
			return text.StartsWith("#") || (text.Length < MAX_TITLE_LENGTH && !text.EndsWith(".") && !text.EndsWith("!") && !text.EndsWith("?"));
		}

		private List<string> SplitParagraphIntoSentences(string paragraph)
		{
			List<string> sentences = new List<string>();

			// Build negative lookbehind patterns from abbreviations dictionary.
			List<string> lookbehindPatterns = Abbreviations.Keys
				.Where(abbrev => abbrev.EndsWith("."))
				.Select(abbrev => $@"(?<!\b{Regex.Escape(abbrev.Substring(0, abbrev.Length - 1))})")
				.ToList();

			// Combine all negative lookbehinds + sentence-ending punctuation.
			string combinedLookbehinds = string.Join("", lookbehindPatterns);
			string pattern = combinedLookbehinds + @"[.!?]+\s*";

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

		private List<string> ProcessRegexParts(string[] parts, string originalParagraph)
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
					if (partEndInOriginal < originalParagraph.Length && ".!?".Contains(originalParagraph[partEndInOriginal]))
					{
						part = part.Trim() + originalParagraph[partEndInOriginal];
					}
				}
				sentences.Add(part.Trim());
			}

			return sentences;
		}

		private List<string> FallbackSplit(string paragraph)
		{
			List<string> sentences = new List<string>();
			string[] simpleParts = paragraph.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

			if (simpleParts.Length > 1)
			{
				string currentSentence = "";

				for (int i = 0; i < simpleParts.Length; i++)
				{
					string part = simpleParts[i].Trim();
					currentSentence += part;

					bool endsWithAbbreviation = Abbreviations.Keys
						.Where(abbrev => abbrev.EndsWith("."))
						.Any(abbrev => part.EndsWith(abbrev.Substring(0, abbrev.Length - 1), StringComparison.OrdinalIgnoreCase));

					if (!endsWithAbbreviation || i == simpleParts.Length - 1)
					{
						if (i < simpleParts.Length - 1)
						{
							currentSentence += ".";
						}
						sentences.Add(currentSentence.Trim());
						currentSentence = "";
					}
					else
					{
						currentSentence += ". ";
					}
				}
			}
			else
			{
				sentences.Add(paragraph);
			}

			return sentences;
		}
	}
}
