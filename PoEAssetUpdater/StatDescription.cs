using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PoEAssetUpdater
{
	internal class StatDescription
	{
		#region Consts

		private const string ValuePlaceholder = "(\\S+)";
		private const string TradeAPIPlaceholder = "#";

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

		#endregion

		#region Lifecycle

		public StatDescription(string[] ids)
		{
			_identifiers = ids;
			FullIdentifier = string.Join(" ", _identifiers);
		}

		#endregion

		#region Public Methods

		public void ParseAndAddStatLine(string language, string line)
		{
			int openQuoteIdx = line.IndexOf('"');
			int closeQuoteIdx = line.IndexOf('"', openQuoteIdx + 1);

			string numberPart = line.Substring(0, openQuoteIdx);
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
			statDescription = statDescription.Replace("%%", "%");

			if(!_statLines.TryGetValue(language, out List<StatLine> statLines))
			{
				_statLines[language] = statLines = new List<StatLine>();
			}

			statLines.Add(new StatLine(numberPart, statDescription));
		}

		public StatLine[] GetStatLines(string language)
		{
			if(!_statLines.TryGetValue(language, out List<StatLine> statLines))
			{
				return new StatLine[0];
			}
			return statLines.ToArray();
		}

		public bool HasMatchingStatLine(string englishStatDescription)
		{
			if(!_statLines.TryGetValue(Language.English, out List<StatLine> statLines))
			{
				return false;
			}
			return statLines.Exists(x => x.TradeAPIStatDescription == englishStatDescription);
		}

		public bool HasMatchingIdentifier(string identifier) => _identifiers.Contains(identifier);

		#endregion

		#region Nested

		public readonly struct StatLine
		{
			public readonly string NumberPart;
			public readonly string StatDescription;
			public readonly string TradeAPIStatDescription;

			public StatLine(string numberPart, string statDescription)
			{
				NumberPart = numberPart;
				StatDescription = $"^{statDescription}$";
				TradeAPIStatDescription = statDescription.Replace(ValuePlaceholder, TradeAPIPlaceholder).Replace("\\n", "\n").Split('\n').First();
			}
		}

		#endregion
	}
}
