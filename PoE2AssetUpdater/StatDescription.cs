using System.Globalization;
using System.Text.RegularExpressions;

namespace PoE2AssetUpdater;

internal class StatDescription
{
	#region Consts

	private const string RegexPlaceholder = @"(\S+)";
	public const string Placeholder = "#";
	public const string PlaceholderWithPlus = "+#";

	#endregion

	#region Variables

	private readonly Dictionary<Language, List<StatLine>> _statLines = [];

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

	public bool ContainsIndexableSupportGem
	{
		get; private set;
	}

	public bool ContainsAdvancedStatDescriptions
	{
		get; private set;
	}

	public bool HasStatLines => _statLines.Count > 0;

	#endregion

	#region Lifecycle

	public StatDescription(string[] ids, bool localStat)
	{
		_identifiers = ids;
		LocalStat = localStat;
		FullIdentifier = string.Join(" ", _identifiers);
	}

	public StatDescription(StatDescription toCopy, string[] ids)
		: this(ids, toCopy.LocalStat)
	{
		foreach(var kvp in toCopy._statLines)
		{
			_statLines.Add(kvp.Key, [..kvp.Value]);
		}
		Negated = toCopy.Negated;
		ContainsIndexableSupportGem = toCopy.ContainsIndexableSupportGem;
		ContainsAdvancedStatDescriptions = toCopy.ContainsAdvancedStatDescriptions;
	}

	#endregion

	#region Public Methods

	public void ParseAndAddStatLine(Language language, string line, int lineIdx, string[] indexableSupportGems, bool isAdvancedStat)
	{
		int openQuoteIdx = line.IndexOf('"');
		int closeQuoteIdx = line.IndexOf('"', openQuoteIdx + 1);

		if(closeQuoteIdx == -1)
		{
			// Invalid stat line detected -> do nothing & return.
			return;
		}

		ContainsAdvancedStatDescriptions |= isAdvancedStat;

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
		else if(additionalData.Contains("tempest_mod_text"))
		{
			// Explicitly ignored because the description is '# #',  which doesn't help at all
			return;
		}

		var numberParts = numberPart.Split(' ');

		// Replace all placeholders & remove redundant number parts
		for(int i = 0; i < numberParts.Length; i++)
		{
			string num = i.ToString(CultureInfo.InvariantCulture);
			var numPart = numberParts[i];
			if(numPart.Contains('#') && !Regex.IsMatch(statDescription, string.Concat(@"\{", (i == 0 ? @"(0(:\+?d)?|:\+?d)?" : $@"{num}(:\+?d)?"), @"\}")))
			{
				numberParts[i] = numPart.Replace("#", numPart.Contains('|') ? numPart.Split('|')[0] : string.Empty);
			}

			statDescription = statDescription
				.Replace($"{{{num}}}", Placeholder)
				.Replace($"{{{num}:d}}", Placeholder)
				.Replace($"{{{num}:+d}}", PlaceholderWithPlus);
		}
		numberPart = string.Join(' ', numberParts).Trim();

		statDescription = statDescription
			.Replace("{}", Placeholder)
			.Replace("{:d}", Placeholder)
			.Replace("{:+d}", PlaceholderWithPlus)
			.Replace("%%", "%")
			.Replace(@"\n", "\n");

		if(!_statLines.TryGetValue(language, out List<StatLine>? statLines))
		{
			_statLines[language] = statLines = [];
		}

		if(additionalData.Contains("display_indexable_support"))
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
		if(!_statLines.TryGetValue(language, out List<StatLine>? statLines))
		{
			return [];
		}

		// Check if an english stat description is provided, if so, the matching index should be returned as only result.
		if(singleMatchOnly || ContainsIndexableSupportGem)
		{
			if(!_statLines.TryGetValue(Language.English, out List<StatLine>? englishStatLines))
			{
				return [];
			}
			int statLineIdx = englishStatLines.FindIndex(x => x.IsMatchingTradeAPIStatDescription(englishStatDescription));
			var statLine = statLines[statLineIdx];
			if(ContainsAdvancedStatDescriptions)
			{
				return statLines.FindAll(x => x.NumberPart == statLine.NumberPart).DistinctBy(x => x.StatDescription).ToArray();
			}
			return [statLine];
		}

		return [..statLines];
	}

	public bool HasMatchingStatLine(string englishStatDescription, bool equalizePlaceholders = false)
	{
		if(!_statLines.TryGetValue(Language.English, out List<StatLine>? statLines))
		{
			return false;
		}
		return statLines.Exists(x => x.IsMatchingTradeAPIStatDescription(englishStatDescription, equalizePlaceholders));
	}

	public int GetMatchingStatLineIndex(string englishStatDescription, bool equalizePlaceholders = false)
	{
		if(!_statLines.TryGetValue(Language.English, out List<StatLine>? statLines))
		{
			return -1;
		}
		return statLines.FindLastIndex(x => x.IsMatchingTradeAPIStatDescription(englishStatDescription, equalizePlaceholders));
	}

	public bool HasMatchingIdentifier(string identifier) => _identifiers.Contains(identifier);

	public bool HasMatchingIdentifier(string[] identifiers) => identifiers.Any(HasMatchingIdentifier);

	public int GetMatchingIdentifierCount(string[] identifiers) => identifiers.Count(HasMatchingIdentifier);

	public (StatLine statline, int dist) GetLowestDistance(string englishStatDescription)
	{
		if(!_statLines.TryGetValue(Language.English, out List<StatLine>? statLines) || statLines.Count == 0)
		{
			return (new StatLine("#", string.Empty), int.MaxValue);
		}
		return _statLines[Language.English].Select(x => (x, x.CalcDist(englishStatDescription))).OrderBy(x => x.Item2).First();
	}

	public void ApplyPresenceText(Dictionary<Language, string> presenceTexts)
	{
		foreach(var kvp in _statLines)
		{
			if(presenceTexts.TryGetValue(kvp.Key, out string? presenceText))
			{
				List<StatLine> statLines = kvp.Value;
				for(int i = 0; i < statLines.Count; i++)
				{
					StatLine statLine = statLines[i];
					statLines[i] = new StatLine(statLine.NumberPart, string.Format(presenceText, statLine.StatDescription));
				}
			}
		} 
	}

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
		{
			// Replace all "[Resistance|Fire Resistance]"-like (without quotes) translation stuff with their actual text
			int searchIdx = 0;
			do
			{
				var startIdx = statDescription.IndexOf('[', searchIdx);
				var endIdx = statDescription.IndexOf(']', searchIdx);
				if(startIdx == -1 || endIdx == -1)
				{
					break;
				}
				else if(startIdx > endIdx)
				{
					searchIdx = endIdx + 1;
					continue;
				}
				var content = statDescription.Substring(startIdx, endIdx - startIdx + 1);
				var split = content.Split("[|]".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
				statDescription = $"{statDescription[..startIdx]}{split.Last()}{statDescription[(endIdx + 1)..]}";
			} while(true);

			string regex = statDescription
				//.Replace(PlaceholderWithPlus, Placeholder)
				.Replace("+", @"\+")
				.Replace("(", @"\(")
				.Replace(")", @"\)")
				.Replace(Placeholder, RegexPlaceholder);
			return $"^{regex}$";
		}

		public bool IsMatchingTradeAPIStatDescription(string statDescription, bool equalizePlaceholders = false)
		{
			string aStatDescription;
			string bStatDescription = statDescription.Trim();

			if(statDescription.Contains('\n'))
			{
				aStatDescription = StatDescription;
			}
			else
			{
				aStatDescription = _strippedTradeAPIStatDescription;
			}

			if(equalizePlaceholders)
			{
				aStatDescription = aStatDescription.Replace(PlaceholderWithPlus, Placeholder);
				bStatDescription = bStatDescription.Replace(PlaceholderWithPlus, Placeholder);
			}

			return aStatDescription == bStatDescription;
		}

		public int CalcDist(string statDescription)
		{
			string[] splitted1 = _strippedTradeAPIStatDescription.Split(' ');
			string[] splitted2 = statDescription.Split(' ');
			if(splitted1.Length == splitted2.Length)
			{
				int dist = 0;
				for(int i = 0; i < splitted1.Length; i++)
				{
					string left = splitted1[i].Trim();
					string right = splitted2[i].Trim();
					if(left == right || left == Placeholder || right == Placeholder || left == PlaceholderWithPlus || right == PlaceholderWithPlus)
					{
						continue;
					}
					dist += LevenshteinDistance.Compute(left, right);
				}
				return dist;
			}
			return LevenshteinDistance.Compute(_strippedTradeAPIStatDescription, statDescription);
		}

		#endregion
	}

	#endregion

	/// <summary>
	/// Contains approximate string matching
	/// </summary>
	static class LevenshteinDistance
	{
		/// <summary>
		/// Compute the distance between two strings.
		/// </summary>
		public static int Compute(string s, string t)
		{
			return 100;

			int n = s.Length;
			int m = t.Length;
			int[,] d = new int[n + 1, m + 1];

			// Step 1
			if(n == 0)
			{
				return m;
			}

			if(m == 0)
			{
				return n;
			}

			// Step 2
			for(int i = 0; i <= n; d[i, 0] = i++)
			{
			}

			for(int j = 0; j <= m; d[0, j] = j++)
			{
			}

			// Step 3
			for(int i = 1; i <= n; i++)
			{
				//Step 4
				for(int j = 1; j <= m; j++)
				{
					// Step 5
					int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

					// Step 6
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost);
				}
			}
			// Step 7
			return d[n, m];
		}
	}
}
