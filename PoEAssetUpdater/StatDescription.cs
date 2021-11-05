using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PoEAssetUpdater
{
	internal class StatDescription
	{
		#region Consts

		private const string RegexPlaceholder = "(\\S+)";
		public const string Placeholder = "#";

		#endregion

		#region Variables

		private readonly Dictionary<Language, List<StatLine>> _statLines = new Dictionary<Language, List<StatLine>>();

		private readonly string[] _identifiers;

		#endregion

		#region Properties

		public string FullIdentifier
		{
			get; private set;
		}

		public bool LocalStat
		{
			get; private set;
		}

		public bool Negated
		{
			get; private set;
		}

		public bool ContainsAfflictionRewardType
		{
			get; private set;
		}

		public bool ContainsIndexableSupportGem
		{
			get; private set;
		}

		public bool ContainsConqueredPassivesText
		{
			get; private set;
		}

		#endregion

		#region Lifecycle

		public StatDescription(string[] ids, bool localStat)
		{
			_identifiers = ids;
			LocalStat = localStat;
			FullIdentifier = string.Join(" ", _identifiers);
		}

		#endregion

		#region Public Methods

		public void ParseAndAddStatLine(Language language, string line, int lineIdx, string[] afflictionRewardTypes, string[] indexableSupportGems)
		{
			int openQuoteIdx = line.IndexOf('"');
			int closeQuoteIdx = line.IndexOf('"', openQuoteIdx + 1);

			if(closeQuoteIdx == -1)
			{
				// Invalid stat line detected -> do nothing & return.
				return;
			}

			string numberPart = line[..openQuoteIdx].Trim();
			string statDescription = line[(openQuoteIdx + 1)..closeQuoteIdx];
			string additionalData = line[(closeQuoteIdx + 1)..];
			if(additionalData.Contains("negate 1"))
			{
				numberPart = string.Concat("N", numberPart);
				if(additionalData.Contains("canonical_line"))
				{
					// Only mark this stat desc as negated when the 'canonical_line' occurs after the 'negate'.
					Negated = additionalData.IndexOf("negate") < additionalData.IndexOf("canonical_line");
				}
				// The numberPart is negated, but expressed explicitly positive => mark the stat as negated.
				else if(additionalData.Contains("ReminderTextPhysReductionNotNegative"))
				{
					Negated = true;
				}
				// The negated stat desc is at the first index, which means it is 'canonical' and should be negated.
				else if(lineIdx == 0)
				{
					Negated = true;
				}
			}

			ContainsConqueredPassivesText = additionalData.Contains("ReminderTextConqueredPassives");

			var numberParts = numberPart.Split(' ');

			// Replace all placeholders & remove redundant number parts
			for(int i = 0; i < numberParts.Length; i++)
			{
				string num = i.ToString(CultureInfo.InvariantCulture);
				var numPart = numberParts[i];
				if(numPart.Contains('#') && !Regex.IsMatch(statDescription, string.Concat("\\{", (i == 0 ? "(0(:\\+?d)?|:\\+?d)?" : $"{num}(:\\+?d)?"), "\\}")))
				{
					numberParts[i] = numPart.Replace("#", numPart.Contains('|') ? numPart.Split('|')[0] : string.Empty);
				}

				statDescription = statDescription
					.Replace($"{{{num}}}", Placeholder)
					.Replace($"{{{num}:d}}", Placeholder)
					.Replace($"{{{num}:+d}}", Placeholder);
			}
			numberPart = string.Join(' ', numberParts).Trim();

			statDescription = statDescription
				.Replace("{}", Placeholder)
				.Replace("{:d}", Placeholder)
				.Replace("{:+d}", Placeholder)
				.Replace("%%", "%")
				.Replace("\\n", "\n");

			if(!_statLines.TryGetValue(language, out List<StatLine> statLines))
			{
				_statLines[language] = statLines = new List<StatLine>();
			}

			if(additionalData.Contains("affliction_reward_type"))
			{
				ContainsAfflictionRewardType = true;
				for(int i = 0; i < afflictionRewardTypes.Length; i++)
				{
					CreateAndAddStatLine(new StatLine(i.ToString(CultureInfo.InvariantCulture), statDescription.Replace(Placeholder, afflictionRewardTypes[i])));
				}
			}
			else if(additionalData.Contains("display_indexable_support"))
			{
				ContainsIndexableSupportGem = true;
				var splittedAdditionalData = additionalData.Split(' ').ToList();
				int indexableSupportNameIdx = int.Parse(splittedAdditionalData[splittedAdditionalData.IndexOf("display_indexable_support") + 1]);
				var placeholderIdx = statDescription.IndexOf(Placeholder);
				for(int i = 1; i < indexableSupportNameIdx; i++)
				{
					placeholderIdx = statDescription.IndexOf(Placeholder, placeholderIdx + Placeholder.Length);
				}
				for (int i = 0; i < indexableSupportGems.Length; i++)
				{
					CreateAndAddStatLine(new StatLine(Placeholder, string.Concat(statDescription[..placeholderIdx], indexableSupportGems[i], statDescription[(placeholderIdx + Placeholder.Length)..])));
				}
			}
			else
			{
				CreateAndAddStatLine(new StatLine(numberPart, statDescription));
			}

			void CreateAndAddStatLine(StatLine statLine)
			{
#if DEBUG
				if(language == Language.English)
				{
					Logger.WriteLine($"ID '{FullIdentifier}' | Local: {LocalStat} | Desc: '{statLine.StatDescription}'");
				}
#endif
				statLines.Add(statLine);
			}
		}

		public StatLine[] GetStatLines(Language language, string englishStatDescription, bool singleMatchOnly)
		{
			if(!_statLines.TryGetValue(language, out List<StatLine> statLines))
			{
				return new StatLine[0];
			}

			// Check if an english stat description is provided, if so, the matching index should be returned as only result.
			if(singleMatchOnly || ContainsAfflictionRewardType || ContainsIndexableSupportGem || ContainsConqueredPassivesText)
			{
				if(!_statLines.TryGetValue(Language.English, out List<StatLine> englishStatLines))
				{
					return new StatLine[0];
				}
				return new StatLine[] { statLines[englishStatLines.FindIndex(x => x.IsMatchingTradeAPIStatDescription(englishStatDescription))] };
			}

			return statLines.ToArray();
		}

		public bool HasMatchingStatLine(string englishStatDescription)
		{
			if(!_statLines.TryGetValue(Language.English, out List<StatLine> statLines))
			{
				return false;
			}
			return statLines.Exists(x => x.IsMatchingTradeAPIStatDescription(englishStatDescription));
		}

		public int GetMatchingStatLineIndex(string englishStatDescription)
		{
			if(!_statLines.TryGetValue(Language.English, out List<StatLine> statLines))
			{
				return -1;
			}
			return statLines.FindLastIndex(x => x.IsMatchingTradeAPIStatDescription(englishStatDescription));
		}

		public bool HasMatchingIdentifier(string identifier) => _identifiers.Contains(identifier);

		#endregion

		#region Nested

		public readonly struct StatLine
		{
			#region Variables

			public readonly string NumberPart;
			public readonly string StatDescription;

			private readonly string _strippedTradeAPIStatDescription;

			#endregion

			public StatLine(string numberPart, string statDescription)
			{
				NumberPart = numberPart;
				StatDescription = statDescription.Trim();
				_strippedTradeAPIStatDescription = StatDescription.Split('\n').First().Trim();
			}

			#region Public Methods

			public static string GetStatDescriptionRegex(string statDescription)
				=> $"^{statDescription.Replace("+", "\\+").Replace(Placeholder, RegexPlaceholder)}$";

			public bool IsMatchingTradeAPIStatDescription(string statDescription)
			{
				if(statDescription.Contains("\n"))
				{
					return StatDescription == statDescription.Trim();
				}
				else
				{
					return _strippedTradeAPIStatDescription == statDescription.Trim();
				}
			}

			#endregion
		}

		#endregion
	}
}
