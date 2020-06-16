using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PoEAssetUpdater
{
	internal class StatDescription
	{
		#region Consts

		private const string ValuePlaceholder = "(\\S+)";
		public const string TradeAPIPlaceholder = "#";

		#endregion

		#region Variables

		private readonly Dictionary<string, List<StatLine>> _statLines = new Dictionary<string, List<StatLine>>();

		private readonly string[] _identifiers;

		#endregion

		#region Properties

		public string FullIdentifier
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

		#endregion

		#region Lifecycle

		public StatDescription(string[] ids)
		{
			_identifiers = ids;
			FullIdentifier = string.Join(" ", _identifiers);
		}

		#endregion

		#region Public Methods

		public void ParseAndAddStatLine(string language, string line, string[] afflictionRewardTypes)
		{
			int openQuoteIdx = line.IndexOf('"');
			int closeQuoteIdx = line.IndexOf('"', openQuoteIdx + 1);

			string numberPart = line.Substring(0, openQuoteIdx).Trim();
			string statDescription = line.Substring(openQuoteIdx + 1,closeQuoteIdx - openQuoteIdx - 1);
			string additionalData = line.Substring(closeQuoteIdx + 1);
			if(additionalData.Contains("negate 1"))
			{
				numberPart = string.Concat("N", numberPart);
				if(additionalData.Contains("canonical_line"))
				{
					Negated = true;
				}
			}

			// Replace all placeholders
			for(int i = 1; i <= 9; i++)
			{
				string num = i.ToString(CultureInfo.InvariantCulture);
				statDescription = statDescription
					.Replace($"%{num}%", ValuePlaceholder)
					.Replace($"%{num}$d", ValuePlaceholder)
					.Replace($"%{num}$+d", ValuePlaceholder);
			}
			statDescription = statDescription.Replace("%d", ValuePlaceholder);
			statDescription = statDescription.Replace("%%", "%");

			if(!_statLines.TryGetValue(language, out List<StatLine> statLines))
			{
				_statLines[language] = statLines = new List<StatLine>();
			}

			if(additionalData.Contains("affliction_reward_type"))
			{
				ContainsAfflictionRewardType = true;
				for(int i = 0; i < afflictionRewardTypes.Length; i++)
				{
					CreateAndAddStatLine(new StatLine(i.ToString(CultureInfo.InvariantCulture), statDescription.Replace(ValuePlaceholder, afflictionRewardTypes[i])));
				}
			}
			CreateAndAddStatLine(new StatLine(numberPart, statDescription));

			void CreateAndAddStatLine(StatLine statLine)
			{
/*#if DEBUG
				if(language == Language.English)
				{
					Logger.WriteLine($"ID '{FullIdentifier}' | Desc: '{statLine.StatDescription}' | Trade Desc: '{statLine.TradeAPIStatDescription}'");
				}
#endif*/

				statLines.Add(statLine);
			}
		}

		public StatLine[] GetStatLines(string language, string englishStatDescription, bool singleMatchOnly)
		{
			if(!_statLines.TryGetValue(language, out List<StatLine> statLines))
			{
				return new StatLine[0];
			}

			// Check if an english stat description is provided, if so, the matching index should be returned as only result.
			if(singleMatchOnly || ContainsAfflictionRewardType)
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

		public bool HasMatchingIdentifier(string identifier) => _identifiers.Contains(identifier);

		#endregion

		#region Nested

		public readonly struct StatLine
		{
			public readonly string NumberPart;
			public readonly string StatDescription;
			public readonly string TradeAPIStatDescription;

			private readonly string _strippedTradeAPIStatDescription;

			public StatLine(string numberPart, string statDescription)
			{
				NumberPart = numberPart;
				StatDescription = $"^{statDescription}$";
				TradeAPIStatDescription = statDescription.Replace(ValuePlaceholder, TradeAPIPlaceholder).Replace("\\n", "\n");
				_strippedTradeAPIStatDescription = TradeAPIStatDescription.Split('\n').First();
			}

			public bool IsMatchingTradeAPIStatDescription(string statDescription)
			{
				if(statDescription.Contains("\n"))
				{
					return TradeAPIStatDescription == statDescription;
				}
				else
				{
					return _strippedTradeAPIStatDescription == statDescription;
				}
			}
		}

		#endregion
	}
}
