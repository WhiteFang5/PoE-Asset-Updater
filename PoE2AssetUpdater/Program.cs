using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PoEAssetReader;
using PoEAssetReader.DatFiles;
using PoEAssetReader.DatFiles.Definitions;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PoE2AssetUpdater;

/// <remarks>
/// Check & update every league:
/// * <see cref="CurrentLeagueName"/>
/// </remarks>
public partial class Program
{
	#region Properties

	private static string ApplicationName => Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
	private static string? ApplicationVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString();

	private const string CurrentLeagueName = "Standard";
	private const string CurrentMapSeries = "Ritual"; // The current map series, this isn't always the same as the League name.

	private const int TotalNumberOfStats = 6;

	private const ulong UndefinedValueDat = 18374403900871474942L;
	private static readonly UInt128 UndefinedValueDat64 = new(UndefinedValueDat, UndefinedValueDat);

	private static readonly char[] NewLineSplitter = "\r\n".ToCharArray();
	private static readonly char[] WhiteSpaceSplitter = "\t ".ToCharArray();

	private static readonly Language[] AllLanguages = Enum.GetValues<Language>();

	private const string CountryBaseURLFormat = "https://{0}.pathofexile.com/api/trade2/data/";
	private const string CountryStatsURLFormat = CountryBaseURLFormat + "stats";
	private const string CountryStaticURLFormat = CountryBaseURLFormat + "static";
	private static readonly Dictionary<Language, string> LanguageToPoETradeAPIUrlMapping = new()
	{
		[Language.English] = string.Format(CountryStatsURLFormat, "www"),
		/*[Language.Portuguese] = string.Format(CountryStatsURLFormat, "br"),
		[Language.Russian] = string.Format(CountryStatsURLFormat, "ru"),
		[Language.Thai] = string.Format(CountryStatsURLFormat, "th"),
		[Language.German] = string.Format(CountryStatsURLFormat, "de"),
		[Language.French] = string.Format(CountryStatsURLFormat, "fr"),
		[Language.Spanish] = string.Format(CountryStatsURLFormat, "es"),
		[Language.Japanese] = string.Format(CountryStatsURLFormat, "jp"),*/
		//[Language.Korean] = "https://poe.game.daum.net/api/trade2/data/stats",
		//[Language.SimplifiedChinese] = "https://poe.game.qq.com/api/trade2/data/stats",
		//[Language.TraditionalChinese] = "https://pathofexile.tw/api/trade2/data/stats",
	};

	private static readonly string PoEStaticTradeDataUrl = string.Format(CountryStaticURLFormat, "www");

	private const string CountryCachedFileNameFormat = "{0}.stats.json";
	private static readonly Dictionary<Language, string> LanguageToPoETradeAPICachedFileNameMapping = new()
	{
		[Language.English] = string.Format(CountryCachedFileNameFormat, "www"),
		[Language.Portuguese] = string.Format(CountryCachedFileNameFormat, "br"),
		[Language.Russian] = string.Format(CountryCachedFileNameFormat, "ru"),
		[Language.Thai] = string.Format(CountryCachedFileNameFormat, "th"),
		[Language.German] = string.Format(CountryCachedFileNameFormat, "de"),
		[Language.French] = string.Format(CountryCachedFileNameFormat, "fr"),
		[Language.Spanish] = string.Format(CountryCachedFileNameFormat, "es"),
		[Language.Korean] = string.Format(CountryCachedFileNameFormat, "kr"),
		//[Language.SimplifiedChinese] = string.Format(CountryCachedFileNameFormat, "ch"),
		[Language.TraditionalChinese] = string.Format(CountryCachedFileNameFormat, "tw"),
		[Language.Japanese] = string.Format(CountryCachedFileNameFormat, "jp"),
	};

	[GeneratedRegex("^lang \"(.*)\"$")]
	private static partial Regex GetLangRegex();

	private static readonly Regex StatDescriptionLangRegex = GetLangRegex();

	private static readonly Dictionary<string, string> LabelsWithSuffix = new()
	{
		["implicit"] = " (implicit)",
		["enchant"] = " (enchant)",
	};

	private static readonly Dictionary<string, string?> BaseItemTypeInheritsFromToCategoryMapping = new()
	{
		// Accessories
		["AbstractRing"] = ItemCategory.AccessoryRing,
		["AbstractAmulet"] = ItemCategory.AccessoryAmulet,
		["AbstractBelt"] = ItemCategory.AccessoryBelt,
		// Armors/Armours
		["AbstractShield"] = ItemCategory.ArmourShield,
		["AbstractHelmet"] = ItemCategory.ArmourHelmet,
		["AbstractBodyArmour"] = ItemCategory.ArmourChest,
		["AbstractBoots"] = ItemCategory.ArmourBoots,
		["AbstractGloves"] = ItemCategory.ArmourGloves,
		["AbstractQuiver"] = ItemCategory.ArmourQuiver,
		["AbstractFocus"] = ItemCategory.ArmourFocus,
		// Currencies
		["AbstractCurrency"] = ItemCategory.Currency,
		["StackableCurrency"] = ItemCategory.Currency,
		["AbstractSoulCore"] = ItemCategory.CurrencySoulCore,
		["AbstractOmen"] = ItemCategory.CurrencyOmen,
		// Divination Cards
		["AbstractDivinationCard"] = ItemCategory.Card,
		// Flasks
		["AbstractLifeFlask"] = ItemCategory.FlaskLife,
		["AbstractManaFlask"] = ItemCategory.FlaskMana,
		// Gems
		["UncutSkillGem"] = ItemCategory.Gem,
		["UncutSupportGem"] = ItemCategory.Gem,
		["UncutReservationGem"] = ItemCategory.Gem,
		["ActiveSkillGem"] = ItemCategory.GemActivegem,
		["SupportSkillGem"] = ItemCategory.GemSupportGem,
		["MetaSkillGem"] = ItemCategory.GemMetaGem,
		// Jewels
		["AbstractJewel"] = ItemCategory.Jewel,
		// Sanctum
		["AbstractRelic"] = ItemCategory.SanctumRelic,
		["AbstractSpecialRelic"] = ItemCategory.SanctumRelic,
		// Maps
		["AbstractMap"] = ItemCategory.Map,
		["AbstractVaultKey"] = ItemCategory.Map,
		["AbstractMapFragment"] = ItemCategory.MapFragment,
		["ExpeditionSaga"] = ItemCategory.MapLogbook,
		["PinnacleKey"] = ItemCategory.MapBossKey,
		["UltimatumKey"] = ItemCategory.MapUltimatum,
		["SanctumFloorBase"] = ItemCategory.MapBarya,
		["TowerAugment"] = ItemCategory.MapTablet,
		// Weapons
		["AbstractClaw"] = ItemCategory.WeaponClaw,
		["AbstractDagger"] = ItemCategory.WeaponDagger,
		["AbstractOneHandSword"] = ItemCategory.WeaponOneSword,
		["AbstractOneHandAxe"] = ItemCategory.WeaponOneAxe,
		["AbstractOneHandMace"] = ItemCategory.WeaponOneMace,
		["AbstractSpear"] = ItemCategory.WeaponSpear,
		["AbstractFlail"] = ItemCategory.WeaponFlail,
		["AbstractTwoHandSword"] = ItemCategory.WeaponTwoSword,
		["AbstractTwoHandAxe"] = ItemCategory.WeaponTwoAxe,
		["AbstractTwoHandMace"] = ItemCategory.WeaponTwoMace,
		["AbstractWarstaff"] = ItemCategory.WeaponWarstaff,
		["AbstractBow"] = ItemCategory.WeaponBow,
		["AbstractCrossbow"] = ItemCategory.WeaponCrossbow,
		["AbstractWand"] = ItemCategory.WeaponWand,
		["AbstractSceptre"] = ItemCategory.WeaponSceptre,
		["AbstractStaff"] = ItemCategory.WeaponStaff,
		["AbstractFishingRod"] = ItemCategory.WeaponRod,
		// Unknown/Not implemented in PoE2 Early Access
		["AbstractTrapTool"] = null,
		["SkillGemToken"] = null,
		// Ignored (i.e. not exported as they're untradable items!)
		["AbstractMicrotransaction"] = null,
		["AbstractQuestItem"] = null,
		["AbstractHideoutDoodad"] = null,
		["Item"] = null,
		["AbstractGiftBox"] = null,
		["AbstractGold"] = null,
		["AbstractUtilityFlask"] = null,// Charms in PoE2 are not tradeable
		// PoE1 Legacy stuff that's not applicable to PoE2 (yet?)
		["DelveSocketableCurrency"] = null,
		["DelveStackableSocketableCurrency"] = null,
		["AbstractLabyrinthItem"] = null,
		["Incubator"] = null,
		["IncubatorStackable"] = null,
		["AbstactPantheonSoul"] = null,
		["AbstractUniqueFragment"] = null,
		["AtlasRegionUpgrade"] = null,
		["HeistContract"] = null,
		["HeistBlueprint"] = null,
		["AbstractHeistEquipment"] = null,
		["HeistObjective"] = null,
		["ArchnemesisMod"] = null,
		["SentinelDroneBase"] = null,
		["MemoryLineBase"] = null,
		["AbstractMiscMapItem"] = null,
	};

	private static readonly Dictionary<UInt128, string> TagsToItemCategoryMapping = new()
	{
		[1094] = ItemCategory.CurrencyRune,//soul_core
	};

	private static readonly string[] IgnoredItemIds = [];

	// Manually matched trade stat IDs to Stat IDs (because there is no generic way to match them up accordingly)
	private static readonly Dictionary<string, (string statId, bool clearOptions)> TradeStatIdManualMapping = [];

	private static readonly Dictionary<string, string> PoEStaticDataLabelToImagesMapping = [];

	#endregion

	#region Public Methods

	public static void Main(string[] args)
	{
		// Validate args array size
		if(args.Length < 2)
		{
			Logger.WriteLine($"Invalid number of arguments. Found {args.Length}, expected atleast 2.");
			PrintUsage();
			return;
		}

		// Validate arguments
		string poeDirectory = args[0];
		if(!Directory.Exists(poeDirectory))
		{
			Logger.WriteLine($"Directory '{poeDirectory}' does not exist.");
			PrintUsage();
			return;
		}
		string assetOutputDir = args[1];
		if(!Directory.Exists(assetOutputDir))
		{
			Logger.WriteLine($"Directory '{assetOutputDir}' does not exist.");
			PrintUsage();
			return;
		}
		string? tradeApiCacheDir = args.Length > 2 ? args[2] : null;
		if(!string.IsNullOrEmpty(tradeApiCacheDir) && !Directory.Exists(tradeApiCacheDir))
		{
			Directory.CreateDirectory(tradeApiCacheDir);
		}
		string? datSchemaPath = args.Length > 3 ? args[3] : null;
		if(string.IsNullOrEmpty(datSchemaPath) || (!Directory.Exists(datSchemaPath) && !File.Exists(datSchemaPath)))
		{
			Logger.WriteLine($"Dat Schema Path '{datSchemaPath}' does not exist.");
			PrintUsage();
			return;
		}

		string logPath = Path.Combine(assetOutputDir, string.Concat(ApplicationName, ".log"));

		try
		{
			// Read the index
			AssetIndex assetIndex = new(poeDirectory);
			DatDefinitions datDefinitions;
			if(datSchemaPath.EndsWith(".py"))
			{
				datDefinitions = DatDefinitions.ParseLocalPyPoE(datSchemaPath);
			}
			else if(datSchemaPath.EndsWith(".json"))
			{
				datDefinitions = DatDefinitions.ParseJson(datSchemaPath);
			}
			else if(File.GetAttributes(datSchemaPath).HasFlag(FileAttributes.Directory))
			{
				datDefinitions = DatDefinitions.ParseLocalGQLDirectory(datSchemaPath);
			}
			else
			{
				Logger.WriteLine($"Dat Schema Path '{datSchemaPath}' doesn't contain not a valid DatDefinition.");
				PrintUsage();
				return;
			}

			ExportBaseItemTypes(assetIndex, datDefinitions, assetOutputDir);
			Logger.SaveLogs(logPath);

			ExportClientStrings(assetIndex, datDefinitions, assetOutputDir);
			ExportWords(assetIndex, datDefinitions, assetOutputDir);
			Logger.SaveLogs(logPath);

			ExportStats(assetIndex, datDefinitions, assetOutputDir, tradeApiCacheDir);
			Logger.SaveLogs(logPath);
			//stats-local.json -> Likely/maintained created manually.
		}
#if !DEBUG
		catch(Exception ex)
		{
			PrintError($"{ex.Message}\r\n{ex.StackTrace}");
		}
#endif
		finally
		{
			Logger.SaveLogs(logPath);
		}

		Console.WriteLine(string.Empty);
		Console.WriteLine("Press any key to exit...");
		Console.Read();
	}

	#endregion

	#region Private Methods

	public delegate (string? key, string? value) GetKeyValuePairDelegate(int idx, DatRecord recordData, List<AssetFile> languageFiles);

	private static void PrintUsage()
	{
		Logger.WriteLine("Usage:");
		Logger.WriteLine($"{ApplicationName} <path-to-steam-bundles2-dir> <asset-output-dir> <optional:trade-api-cache-dir> <optional:py-poe-dat-definitions-file>");
		Console.Read();
	}

	private static void PrintError(string message)
	{
		Logger.WriteLine(string.Empty);
		Logger.WriteLine($"!!! ERROR !!! {message}");
		Logger.WriteLine(string.Empty);
	}

	private static void PrintWarning(string message)
	{
		Logger.WriteLine($"!! WARNING: {message}");
	}

	private static void WriteJsonFile(string exportFilePath, Action<JsonWriter> writeData)
	{
		// Create a JSON writer with human-readable output.
		using(StreamWriter streamWriter = new(exportFilePath))
		using(JsonTextWriter jsonWriter = new(streamWriter)
		{
			Formatting = Formatting.Indented,
			Indentation = 1,
			IndentChar = '\t',
		})
		{
			jsonWriter.WriteStartObject();

			writeData(jsonWriter);

			jsonWriter.WriteEndObject();
		}

		// Create a minified json.
		string? dirName = Path.GetDirectoryName(exportFilePath);
		if(string.IsNullOrEmpty(dirName))
		{
			return;
		}
		string minifiedDir = Path.Combine(dirName, "minified");
		if(!Directory.Exists(minifiedDir))
		{
			Directory.CreateDirectory(minifiedDir);
		}
		string minifiedFilePath = Path.Combine(minifiedDir, Path.GetFileName(exportFilePath));

		using(StreamReader streamReader = new(exportFilePath))
		using(StreamWriter streamWriter = new(minifiedFilePath))
		using(JsonReader jsonReader = new JsonTextReader(streamReader))
		using(JsonWriter jsonWriter = new JsonTextWriter(streamWriter)
		{
			Formatting = Formatting.None,
		})
		{
			jsonWriter.WriteToken(jsonReader);
		}
	}

	private static void ExportDataFile(AssetIndex assetIndex, string exportFilePath, Action<List<AssetFile>, JsonWriter> writeData, bool includeLanguageFolders)
	{
		Logger.WriteLine($"Exporting {Path.GetFileName(exportFilePath)}...");

		List<AssetFile> dataFiles = includeLanguageFolders ? assetIndex.FindFiles(x => x.Name.StartsWith("data/")) : assetIndex.FindFiles(x => Path.GetDirectoryName(x.Name) == "data");

		WriteJsonFile(exportFilePath, jsonWriter => writeData(dataFiles, jsonWriter));

		Logger.WriteLine($"Exported '{exportFilePath}'.");
	}

	private static Dictionary<Language, List<DatFile>> GetLanguageDataFiles(List<AssetFile> assetFiles, DatDefinitions datDefinitions, params string[] datFileNames)
	{
		Dictionary<Language, List<DatFile>> datFiles = [];
		foreach(var language in AllLanguages)
		{
			// Determine the directory to search for the given datFile. English is the base/main language and isn't located in a sub-folder.
			var langDir = (language == Language.English ? "data" : $"data\\{language}").ToLowerInvariant();
			var languageFiles = assetFiles.FindAll(x => Path.GetDirectoryName(x.Name)?.ToLowerInvariant() == langDir);
			if(languageFiles.Count > 0)
			{
				datFiles.Add(language, []);

				foreach(var datFileName in datFileNames)
				{
					var datContainer = GetDatFile(languageFiles, datDefinitions, datFileName);
					if(datContainer == null)
					{
						// An error was already logged.
						continue;
					}
					datFiles[language].Add(datContainer);
				}
			}
			else
			{
				Logger.WriteLine($"\t{language} Language folder not found.");
			}
		}
		return datFiles;
	}

	private static void ExportLanguageDataFile(List<AssetFile> assetFiles, DatDefinitions datDefinitions, JsonWriter jsonWriter, Dictionary<string, GetKeyValuePairDelegate> datFiles, bool mirroredRecords)
	{
		foreach(var language in AllLanguages)
		{
			Dictionary<string, string> records = [];

			// Determine the directory to search for the given datFile. English is the base/main language and isn't located in a sub-folder.
			var langDir = (language == Language.English ? "Data" : $"Data\\{language}").ToLowerInvariant();
			var languageFiles = assetFiles.FindAll(x => Path.GetDirectoryName(x.Name)?.ToLowerInvariant() == langDir);
			if(languageFiles.Count > 0)
			{
				// Retrieve all records
				foreach((var datFileName, var getKeyValuePair) in datFiles)
				{
					// Find the given datFile.
					var datContainer = GetDatFile(languageFiles, datDefinitions, datFileName);
					if(datContainer == null)
					{
						// An error was already logged.
						continue;
					}

					Logger.WriteLine($"\tExporting {langDir}/{datFileName}.");

					for(int j = 0, recordsLength = datContainer.Records.Count; j < recordsLength; j++)
					{
						(string? key, string? value) = getKeyValuePair(j, datContainer.Records[j], languageFiles);
						if(key == null || value == null || records.ContainsKey(key) || (mirroredRecords && records.ContainsKey(value)))
						{
							continue;
						}

						records[key] = value;
						if(mirroredRecords)
						{
							records[value] = key;
						}
					}
				}
			}
			else
			{
				Logger.WriteLine($"\t{language} Language folder not found.");
			}

			// Create a node and write the data of each record in this node.
			jsonWriter.WritePropertyName(language.ToString());
			jsonWriter.WriteStartObject();

			foreach((var key, var value) in records)
			{
				jsonWriter.WritePropertyName(key);
				jsonWriter.WriteValue(value);
			}

			jsonWriter.WriteEndObject();
		}
	}

	private static void ExportClientStrings(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
	{
		ExportDataFile(assetIndex, Path.Combine(exportDir, "client-strings.json"), WriteRecords, true);

		void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
		{
			ExportLanguageDataFile(dataFiles, datDefinitions, jsonWriter, new Dictionary<string, GetKeyValuePairDelegate>()
			{
				["ClientStrings.datc64"] = GetClientStringKVP,
				["AlternateQualityTypes.datc64"] = GetAlternateQualityTypesKVP,
				["ExpeditionFactions.datc64"] = GetExpeditionFactionsKVP,
				["Characters.datc64"] = GetCharactersKVP,
				["ItemClasses.datc64"] = GetItemClassesKVP,
			}, false);
		}

		static (string?, string?) GetClientStringKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
		{
			string id = recordData.GetValue<string>(DatSchemas.ClientStrings.Id);
			string name = recordData.GetValue<string>(DatSchemas.ClientStrings.Text).Trim();

			switch(id)
			{
				case "ItemDisplayStoredExperience" when name.EndsWith(": %0"):
					name = name[0..^4];
					break;
				case "ItemDisplayStoredExperience" when name.EndsWith(": {0}"):
					name = name[0..^5];
					break;
			}

			return (id, name);
		}

		static (string?, string?) GetAlternateQualityTypesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
		{
			string id = string.Concat("Quality", (idx + 1).ToString(CultureInfo.InvariantCulture));//Magic number "1" is the lowest mods key value plus the magic number; It's used to create a DESC sort.
			string name = recordData.GetValue<string>(DatSchemas.AlternateQualityTypes.Description);
			return (id, name);
		}

		static (string?, string?) GetExpeditionFactionsKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
		{
			string id = recordData.GetValue<string>(DatSchemas.ExpeditionFactions.Id);
			string name = recordData.GetValue<string>(DatSchemas.ExpeditionFactions.Name).Trim();
			if(string.IsNullOrEmpty(name))
			{
				return (null, null);
			}
			return ($"Expedition{id}", name);
		}

		static (string?, string?) GetCharactersKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
		{
			string name = recordData.GetValue<string>(DatSchemas.Characters.Name).Trim();
			return ($"CharacterName{idx}", name);
		}

		static (string?, string?) GetItemClassesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
		{
			string id = recordData.GetValue<string>(DatSchemas.ItemClasses.Id);
			string name = recordData.GetValue<string>(DatSchemas.ItemClasses.Name).Trim();
			return ($"ItemClass{id}", name);
		}
	}

	private static void ExportWords(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
	{
		ExportDataFile(assetIndex, Path.Combine(exportDir, "words.json"), WriteRecords, true);

		void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
		{
			ExportLanguageDataFile(dataFiles, datDefinitions, jsonWriter, new Dictionary<string, GetKeyValuePairDelegate>()
			{
				["Words.datc64"] = GetWordsKVP,
			}, true);
		}

		static (string, string) GetWordsKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
		{
			string id = idx.ToString(CultureInfo.InvariantCulture);
			string name = recordData.GetValue<string>(DatSchemas.Words.Text).Trim();
			return (id, name);
		}
	}

	private static DatFile? GetDatFile(List<AssetFile> assetFiles, DatDefinitions datDefinitions, string datFileName)
	{
		var assetFile = assetFiles.FirstOrDefault(x => datFileName.Equals(Path.GetFileName(x.Name), StringComparison.InvariantCultureIgnoreCase));
		if(assetFile == null)
		{
			Logger.WriteLine($"\t{datFileName} not found.");
			return null;
		}

		var datFile = new DatFile(assetFile, datDefinitions);
		if(datFile.Records.Count > 0)
		{
			if(datFile.Records[0].TryGetValue("_Remainder", out byte[] remainder))
			{
				PrintError($"Found {remainder.Length} Remainder Bytes in {datFileName}");
			}
			for(int i = 0; i < datFile.Records.Count; i++)
			{
				var record = datFile.Records[i];
				var remarks = string.Join("; ", record.Values.Where(x => !string.IsNullOrEmpty(x.Value.Remark)).Select(x => $"{x.Key}: {x.Value.Remark}"));
				if(!string.IsNullOrEmpty(remarks))
				{
					//PrintError($"{datFileName}[{i}] Remarks: {remarks}");
				}
			}
		}
		return datFile;
	}

	private static string GetWebContent(string url)
	{
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
		request.Timeout = 10 * 1000;
		request.Headers[HttpRequestHeader.UserAgent] = "PoEOverlayAssetUpdater/" + ApplicationVersion;
		using var response = (HttpWebResponse)request.GetResponse();
		if(response.StatusCode == HttpStatusCode.OK)
		{
			using Stream dataStream = response.GetResponseStream();
			using StreamReader reader = new(dataStream);
			return reader.ReadToEnd();
		}
		throw new Exception($"Failed to retrieve data from '{url}'. Unexpected response code {response.StatusCode} (Desc: {response.StatusDescription})");
	}

	private static void ExportStats(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir, string? tradeApiCacheDir)
	{
		ExportDataFile(assetIndex, Path.Combine(exportDir, "stats.json"), WriteRecords, true);

		void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
		{
			var statsDatContainer = GetLanguageDataFiles(dataFiles, datDefinitions, "Stats.datc64")[Language.English][0];
			var afflictionRewardTypeVisualsDatContainer = GetLanguageDataFiles(dataFiles, datDefinitions, "AfflictionRewardTypeVisuals.datc64")[Language.English][0];
			var indexableSupportGemsDatContainer = GetLanguageDataFiles(dataFiles, datDefinitions, "IndexableSupportGems.datc64")[Language.English][0];
			var modsDatContainer = GetLanguageDataFiles(dataFiles, datDefinitions, "Mods.datc64")[Language.English][0];
			var clientStringsDatContainers = GetLanguageDataFiles(dataFiles, datDefinitions, "ClientStrings.datc64");

			List<AssetFile> statDescriptionFiles = assetIndex.FindFiles(x => x.Name.StartsWith("Metadata/StatDescriptions", StringComparison.InvariantCultureIgnoreCase));
			string[]? statDescriptionsText = GetStatDescriptions("stat_descriptions.csd");
			string[]? mapStatDescriptionsText = GetStatDescriptions("map_stat_descriptions.csd");
			string[]? atlasStatDescriptionsText = GetStatDescriptions("atlas_stat_descriptions.csd");
			string[]? sanctumRelicStatDescriptionsText = GetStatDescriptions("sanctum_relic_stat_descriptions.csd");
			string[]? advancedModsStatDescriptionsText = GetStatDescriptions("advanced_mod_stat_descriptions.csd");

			if(statsDatContainer == null || afflictionRewardTypeVisualsDatContainer == null || indexableSupportGemsDatContainer == null ||
				clientStringsDatContainers == null || clientStringsDatContainers.Count == 0 || statDescriptionFiles.Count == 0 || statDescriptionsText == null ||
				mapStatDescriptionsText == null || atlasStatDescriptionsText == null || sanctumRelicStatDescriptionsText == null || advancedModsStatDescriptionsText == null)
			{
				return;
			}

			Logger.WriteLine($"Parsing {statsDatContainer.FileDefinition.Name}...");

			string[] localStats = statsDatContainer.Records.Where(x => x.GetValue<bool>(DatSchemas.Stats.IsLocal)).Select(x => x.GetValue<string>(DatSchemas.Stats.Id)).ToArray();

			Logger.WriteLine($"Parsing {indexableSupportGemsDatContainer.FileDefinition.Name}...");

			string[] indexableSupportGems = indexableSupportGemsDatContainer.Records.Select(x => x.GetValue<string>(DatSchemas.IndexableSupportGems.Name)).ToArray();

			Logger.WriteLine($"Parsing Stat Description Files...");

			// Create a list of all stat descriptions
			List<StatDescription> statDescriptions = [];
			var textDescriptions = statDescriptionsText.Concat(mapStatDescriptionsText).Concat(atlasStatDescriptionsText).Concat(sanctumRelicStatDescriptionsText);
			int advancedModDesStartIdx = textDescriptions.Count();
			string[] lines = textDescriptions.Concat(advancedModsStatDescriptionsText).ToArray();
			for(int lineIdx = 0, lastLineIdx = lines.Length - 1; lineIdx <= lastLineIdx; lineIdx++)
			{
				string line = lines[lineIdx];
				// Description found => read id(s)
				if(line.StartsWith("description"))
				{
					line = lines[++lineIdx];
					string[] ids = line.Split(WhiteSpaceSplitter, StringSplitOptions.RemoveEmptyEntries);
					int statCount = int.Parse(ids[0]);

					if(Array.Exists(ids, x => x.Contains("old_do_not_use")))
					{
						// Ignore all "old do not use" stats.
						continue;
					}

					// Strip the number indicating how many stats are present from the IDs
					ids = ids.Skip(1).ToArray();
					string fullID = string.Join(" ", ids);
					bool isLocalStat = ids.Any(x => localStats.Contains(x));
					bool isAdvancedStat = lineIdx >= advancedModDesStartIdx;

					// Find an existing stat in the list
					StatDescription? statDescription = statDescriptions.FirstOrDefault(x => x.FullIdentifier == fullID && x.LocalStat == isLocalStat);
					if(statDescription == null)
					{
						statDescription = new StatDescription(ids, isLocalStat);
						statDescriptions.Add(statDescription);
					}
					else
					{
						Logger.WriteLine($"Found existing stat description for '{fullID}'");
					}

					// Initial (first) language is always english
					Language language = Language.English;
					while(true)
					{
						// Read the next line as it contains how many mods are added.
						line = lines[++lineIdx];
						int textCount = int.Parse(line);
						for(int i = 0; i < textCount; i++)
						{
							statDescription.ParseAndAddStatLine(language, lines[++lineIdx], i, indexableSupportGems, isAdvancedStat);
						}
						if(lineIdx < lastLineIdx)
						{
							// Take a peek at the next line to check if it's a new language, or something else
							line = lines[lineIdx + 1];
							Match match = StatDescriptionLangRegex.Match(line);
							if(match.Success)
							{
								lineIdx++;
								language = Enum.Parse<Language>(match.Groups[1].Value.Replace(" ", ""), true);
							}
							else
							{
								break;
							}
						}
						else
						{
							break;
						}
					}
				}
			}

			statDescriptions.RemoveAll(x => !x.HasStatLines);

			Logger.WriteLine("Downloading PoE2 Trade API Stats...");

			// Download the PoE Trade Stats json
			Dictionary<Language, JObject> poeTradeStats = [];
			Dictionary<Language, string> poeTradeSiteContent = [];
			bool retrievedAllContent = true;
			foreach((var language, var tradeAPIUrl) in LanguageToPoETradeAPIUrlMapping)
			{
				try
				{
					string content = GetWebContent(tradeAPIUrl);
					poeTradeSiteContent[language] = content;
					poeTradeStats[language] = JObject.Parse(content);
				}
				catch(Exception ex)
				{
					retrievedAllContent = false;
					PrintError($"Failed to connect to '{tradeAPIUrl}': {ex.Message}");
					// Check if we have a cached file
					if(!string.IsNullOrEmpty(tradeApiCacheDir))
					{
						string cachedFileName = Path.Combine(tradeApiCacheDir, LanguageToPoETradeAPICachedFileNameMapping[language]);
						if(File.Exists(cachedFileName))
						{
							PrintWarning($"Using cached trade-api data for {language}");
							poeTradeStats[language] = JObject.Parse(File.ReadAllText(cachedFileName));
						}
					}
				}
				// Sleep for a short time to avoid spamming the different trade APIs
				Thread.Sleep(1000);
			}

			Logger.WriteLine("Parsing PoE Trade API Stats...");

			if(!poeTradeStats.TryGetValue(Language.English, out JObject? englishStats))
			{
				PrintError($"Failed to parse PoE2 Trade API Stats.");
				return;
			}

			// Update the trade api cached files
			if(retrievedAllContent && !string.IsNullOrEmpty(tradeApiCacheDir))
			{
				foreach((var language, string content) in poeTradeSiteContent)
				{
					if(LanguageToPoETradeAPICachedFileNameMapping.TryGetValue(language, out string? fileName))
					{
						string cachedFileName = Path.Combine(tradeApiCacheDir, fileName);
						File.WriteAllText(cachedFileName, content);
					}
				}
			}
			poeTradeSiteContent.Clear();

			var indistuingishableStats = new Dictionary<string, Dictionary<string, List<string>>>();

			// Parse the PoE Trade Stats
			var englishStatResults = englishStats["result"];
			if(englishStatResults == null)
			{
				PrintError($"Failed to obtain English PoE2 Trade API Stats.");
				return;
			}
			foreach(var result in englishStatResults)
			{
				var label = GetLabel(result);

				var tradeStatsData = new Dictionary<string, List<string>>();

				jsonWriter.WritePropertyName(label);
				jsonWriter.WriteStartObject();
				foreach(var entry in result["entries"])
				{
					string tradeId = GetTradeID(entry, label);
					string text = (string)entry["text"];
					string modValue = null;
					Dictionary<string, string> optionValues = null;

					// Check the trade text for mods
					(text, modValue) = GetTradeMod(text);

					// Check for options
					var options = entry["option"]?["options"];
					if(options != null)
					{
						optionValues = options.ToDictionary(option => option["id"]?.ToString() ?? string.Empty, option => option["text"]?.ToString() ?? string.Empty);
					}

					FindAndWriteStatDescription(label, tradeId, modValue, text, optionValues, tradeStatsData);
				}
				jsonWriter.WriteEndObject();

				indistuingishableStats[label] = tradeStatsData;
			}

			// Write the ingistinguishale stats json
			WriteJsonFile(Path.Combine(exportDir, "stats-indistinguishable.json"), jsonWriter =>
			{
				jsonWriter.WritePropertyName("indistinguishableStats");
				jsonWriter.WriteStartObject();
				foreach((var label, var tradeStatsData) in indistuingishableStats)
				{
					jsonWriter.WritePropertyName(label);
					jsonWriter.WriteStartObject();
					var usedTradeIds = new List<string>();
					foreach((string statDesc, List<string> tradeIds) in tradeStatsData)
					{
						if(tradeIds.Count > 1)
						{
#if DEBUG
							Logger.WriteLine($"[{label}] Indistinguishable Desc '{statDesc}' for Trade Stat IDs: {string.Join(", ", tradeIds.Select(x => $"'{x}'"))}");
#endif
							for(int i = 0; i < tradeIds.Count; i++)
							{
								string tradeId = tradeIds[i];
								if(usedTradeIds.Contains(tradeId))
								{
									continue;
								}
								jsonWriter.WritePropertyName(tradeId);
								jsonWriter.WriteStartArray();
								for(int j = 0; j < tradeIds.Count; j++)
								{
									if(i == j)
									{
										continue;
									}
									jsonWriter.WriteValue(tradeIds[j]);
								}
								jsonWriter.WriteEndArray();

								usedTradeIds.Add(tradeId);
							}
						}
					}
					jsonWriter.WriteEndObject();
				}
				jsonWriter.WriteEndObject();
			});

			// Nested Method(s)
			static string GetLabel(JToken token) => ((string)token["label"]).ToLowerInvariant();

			static string GetTradeID(JToken token, string label) => ((string)token["id"])[(label.Length + 1)..];

			static (string modlessText, string? modValue) GetTradeMod(string tradeAPIStatDescription)
			{
				if(tradeAPIStatDescription.EndsWith(')'))
				{
					int bracketsOpenIdx = tradeAPIStatDescription.LastIndexOf('(');
					int bracketsCloseIdx = tradeAPIStatDescription.LastIndexOf(')');
					string modValue = tradeAPIStatDescription.Substring(bracketsOpenIdx + 1, bracketsCloseIdx - bracketsOpenIdx - 1).ToLowerInvariant();
					string modlessText = tradeAPIStatDescription[..bracketsOpenIdx].Trim();
					return (modlessText, modValue);
				}
				return (tradeAPIStatDescription, null);
			}

			string[]? GetStatDescriptions(string fileName)
			{
				var statDescriptionsFile = statDescriptionFiles.FirstOrDefault(x => string.Equals(Path.GetFileName(x.Name), fileName, StringComparison.InvariantCultureIgnoreCase));

				if(statDescriptionsFile == null)
				{
					Logger.WriteLine($"\t{fileName} not found.");
					return null;
				}

				Logger.WriteLine($"Reading {statDescriptionsFile.Name}...");
				string content = Encoding.Unicode.GetString(statDescriptionsFile.GetFileContents());
				return content
					.Split(NewLineSplitter, StringSplitOptions.RemoveEmptyEntries)
					.Select(x => x.Trim())
					.Where(x => x.Length > 0).ToArray();
			}

			void FindAndWriteStatDescription(string label, string tradeId, string? mod, string text, Dictionary<string, string>? options, Dictionary<string, List<string>> tradeStatsData)
			{
				bool explicitLocal = mod == "local";
				StatDescription? statDescription = null;
				bool expandOptions = false;
				bool addTradeStatData = false;
				bool equalizedPlaceholders = false;
				if(TradeStatIdManualMapping.TryGetValue($"{label}.{tradeId}", out (string statId, bool clearOptions) mapping))
				{
					statDescription = statDescriptions.FirstOrDefault(x => x.FullIdentifier == mapping.statId);
					if(statDescription != null)
					{
						addTradeStatData = true;
						if(mapping.clearOptions)
						{
							options = null;
						}
					}
				}
				// Lookup the stat, unless it's a pseudo stat (those arn't supposed to be linked to real stats)
				if(statDescription == null && label != "pseudo")
				{
					var candidateStatDescs = statDescriptions
						.Where(x => (!explicitLocal || x.LocalStat) && x.HasMatchingStatLine(text))
						.OrderBy(x => x.GetMatchingStatLineIndex(text))
						.ToList();

					// When no regular stat descs were found, and options are present, try to split them out and find matching stat descs.
					if(candidateStatDescs.Count == 0 && options != null)
					{
						expandOptions = true;

						string[] searchTexts = options.Select(x => GetOptionStatDesc(text, x.Value)).ToArray();

						candidateStatDescs = [..statDescriptions
							.Where(x => (!explicitLocal || x.LocalStat) && searchTexts.All(y => x.HasMatchingStatLine(y)))
							.OrderBy(x => x.GetMatchingStatLineIndex(searchTexts.First(y => x.HasMatchingStatLine(y))))];
					}

					if(candidateStatDescs.Count == 0)
					{
						equalizedPlaceholders = true;
						candidateStatDescs = [..statDescriptions
							.Where(x => (!explicitLocal || x.LocalStat) && x.HasMatchingStatLine(text, equalizedPlaceholders))
							.OrderBy(x => x.GetMatchingStatLineIndex(text, equalizedPlaceholders))];
					}

					if(candidateStatDescs.Count == 0)
					{
						PrintWarning($"Missing {nameof(StatDescription)} for Label '{label}', TradeID '{tradeId}', Desc: '{text.Replace("\n", "\\n")}'");
						statDescriptions.Where(x => !explicitLocal || x.LocalStat).AsParallel().Select(x => x.GetLowestDistance(text)).OrderBy(x => x.dist).Take(2).ToList().ForEach(x =>
						{
							Logger.WriteLine($"[DIST] {x.dist} |{x.statline.StatDescription}|{text.Replace("\n", "\\n")}");
						});
					}
					else
					{
						// Only add trade data for candidates that have equal "local stat" values because instinguishable stats always occur between different "local stat" values, but are properly distinguished by the app.
						addTradeStatData = candidateStatDescs.All(x => x.LocalStat == candidateStatDescs[0].LocalStat);

						statDescription = candidateStatDescs.First();
						mod ??= statDescription.LocalStat ? "local" : null;
					}
				}

				jsonWriter.WritePropertyName(tradeId);
				jsonWriter.WriteStartObject();
				{
					if(statDescription != null)
					{
						jsonWriter.WritePropertyName("id");
						jsonWriter.WriteValue(statDescription.FullIdentifier);
						jsonWriter.WritePropertyName("negated");
						jsonWriter.WriteValue(statDescription.Negated);
					}
					if(mod != null)
					{
						jsonWriter.WritePropertyName("mod");
						jsonWriter.WriteValue(mod);
					}
					if(options != null)
					{
						jsonWriter.WritePropertyName("option");
						jsonWriter.WriteValue(true);
					}
					jsonWriter.WritePropertyName("text");
					jsonWriter.WriteStartObject();
					{
						for(int i = 0; i < AllLanguages.Length; i++)
						{
							Language language = AllLanguages[i];

							jsonWriter.WritePropertyName((i + 1).ToString(CultureInfo.InvariantCulture));
							jsonWriter.WriteStartArray();
							if(statDescription != null)
							{
								if(expandOptions)
								{
									foreach((_, var optionValue) in options!)
									{
										foreach(var statLine in statDescription.GetStatLines(language, GetOptionStatDesc(text, optionValue), true))
										{
											WriteStatLine(statLine, null, label, addTradeStatData ? tradeStatsData : null, tradeId, language, jsonWriter);
										}
									}
								}
								else
								{
									foreach(var statLine in statDescription.GetStatLines(language, text, false))
									{
										WriteStatLine(statLine, options, label, addTradeStatData ? tradeStatsData : null, tradeId, language, jsonWriter);
									}
								}
							}
							else
							{
								var tradeIdSearch = $"{label}.{tradeId}";

								JToken? otherLangStat = null;
								if(poeTradeStats.TryGetValue(language, out var otherLangTradeStats))
								{
									otherLangStat = otherLangTradeStats["result"]?.SelectMany(x => x["entries"]!)?.FirstOrDefault(x => string.Equals((string?)x["id"], tradeIdSearch, StringComparison.InvariantCultureIgnoreCase));
								}
								string otherLangText;
								if(otherLangStat != null)
								{
									otherLangText = (string)otherLangStat["text"]!;
									(otherLangText, _) = GetTradeMod(otherLangText);// i.e. strips the trade mod.
								}
								else
								{
									otherLangText = text;
									PrintWarning($"Missing {language} trade ID '{tradeIdSearch}'");
								}

								var statLine = new StatDescription.StatLine("#", otherLangText);
								WriteStatLine(statLine, options, label, addTradeStatData ? tradeStatsData : null, tradeId, language, jsonWriter);
							}
							jsonWriter.WriteEndArray();
						}
					}
					jsonWriter.WriteEndObject();
				}
				jsonWriter.WriteEndObject();
			}
		}

		void WriteStatLine(StatDescription.StatLine statLine, Dictionary<string, string>? options, string label, Dictionary<string, List<string>>? tradeStatsData, string tradeId, Language language, JsonWriter jsonWriter)
		{
			string desc = statLine.StatDescription;
			if(!LabelsWithSuffix.TryGetValue(label, out string? descSuffix))
			{
				descSuffix = null;
			}

			if(options == null)
			{
				WriteStatLine(statLine.NumberPart, StatDescription.StatLine.GetStatDescriptionRegex(AppendSuffix(desc, descSuffix)));
				if(language == Language.English)
				{
					AddTradeStatData(tradeStatsData, desc, tradeId);
				}
			}
			else
			{
				foreach((var id, var optionValue) in options)
				{
					string optionDesc = GetOptionStatDesc(desc, optionValue);
					WriteStatLine(id, StatDescription.StatLine.GetStatDescriptionRegex(AppendSuffix(optionDesc, descSuffix)));
					if(language == Language.English)
					{
						AddTradeStatData(tradeStatsData, optionDesc, tradeId);
					}
				}
			}

			static string AppendSuffix(string text, string? suffix)
			{
				if(string.IsNullOrEmpty(suffix))
				{
					return text;
				}
				return string.Join("\n", text.Split('\n').Select(x => string.Concat(x, suffix)).ToArray());
			}

			void WriteStatLine(string predicate, string regex)
			{
				jsonWriter.WriteStartObject();
				jsonWriter.WritePropertyName(predicate);
				jsonWriter.WriteValue(regex);
				jsonWriter.WriteEndObject();
			}
		}

		static string GetOptionStatDesc(string desc, string optionValue)
		{
			// Split the options into lines, replaced the placeholder with each line, and join them back together to form a single line.
			return string.Join("\n", optionValue.Split('\n').Select(option => desc.Replace(StatDescription.Placeholder, option)));
		}

		static void AddTradeStatData(Dictionary<string, List<string>>? tradeStatsData, string desc, string tradeId)
		{
			if(tradeStatsData == null)
			{
				return;
			}
			foreach((var existingDesc, var tradeData) in tradeStatsData)
			{
				if(existingDesc == desc || Regex.IsMatch(desc, StatDescription.StatLine.GetStatDescriptionRegex(existingDesc)))
				{
					if(!tradeData.Contains(tradeId))
					{
						tradeData.Add(tradeId);
					}
					return;
				}
			}
			tradeStatsData[desc] = [tradeId];
		}
	}

	private static string? GetItemCategory(DatRecord baseItemType, int rowIndex)
	{
		string id = baseItemType.GetValue<string>(DatSchemas.BaseItemTypes.Id).Split('/').Last();
		string inheritsFrom = baseItemType.GetValue<string>(DatSchemas.BaseItemTypes.InheritsFrom).Split('/').Last();

		if(IgnoredItemIds.Contains(id))
		{
			return null;
		}

		// Check the inheritance mapping for a matching category.
		if(!BaseItemTypeInheritsFromToCategoryMapping.TryGetValue(inheritsFrom, out string? category))
		{
			PrintError($"Missing BaseItemTypes Category for '{id}' (InheritsFrom '{inheritsFrom}') at row {rowIndex}");
			return null;
		}

		// Special cases
		switch(category)
		{
			// Special mapping for
			case ItemCategory.MapFragment:
			case ItemCategory.CurrencySoulCore:
				foreach(UInt128 tag in baseItemType.GetValue<List<UInt128>>(DatSchemas.BaseItemTypes.Tags))
				{
					if(TagsToItemCategoryMapping.TryGetValue(tag, out string? newCategory))
					{
						category = newCategory;
					}
				}
				break;
		}

		return category;
	}

	private static void ExportBaseItemTypes(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
	{
		JObject? staticTradeData = TryParseData(PoEStaticTradeDataUrl);

		ExportDataFile(assetIndex, Path.Combine(exportDir, "base-item-types-v2.json"), WriteRecords, true);

		JObject? TryParseData(string url)
		{
			try
			{
				return JObject.Parse(GetWebContent(url));
			}
			catch(Exception ex)
			{
				PrintError($"Failed to connect to '{url}': {ex.Message}");
			}
			return null;
		}

		void WriteRecords(List<AssetFile> assetFiles, JsonWriter jsonWriter)
		{
			var baseItemTypesDatContainers = GetLanguageDataFiles(assetFiles, datDefinitions, "BaseItemTypes.datc64");
			var clientStringsDatContainers = GetLanguageDataFiles(assetFiles, datDefinitions, "ClientStrings.datc64");
			var wordsDatContainers = GetLanguageDataFiles(assetFiles, datDefinitions, "Words.datc64");

			var itemVisualIdentityDatContainer = GetDatFile(assetFiles, datDefinitions, "ItemVisualIdentity.datc64");

			var englishBaseItemTypesDatContainer = baseItemTypesDatContainers[Language.English];
			if(englishBaseItemTypesDatContainer.Count == 0)
			{
				PrintWarning($"Couldn't find English BaseItemTypes");
				return;
			}

			var baseItemTypesDatContainer = englishBaseItemTypesDatContainer[0];

			// Write the Base Item Types
			for(int i = 0; i < baseItemTypesDatContainer.Count; i++)
			{
				var baseItemType = baseItemTypesDatContainer.Records[i];
				string inheritsFrom = baseItemType.GetValue<string>(DatSchemas.BaseItemTypes.InheritsFrom).Split('/').Last();
				if(inheritsFrom == "AbstractMicrotransaction" || inheritsFrom == "AbstractHideoutDoodad")
				{
					continue;
				}
				string id = baseItemType.GetValue<string>(DatSchemas.BaseItemTypes.Id).Split('/').Last();
				string name = Escape(baseItemType.GetValue<string>(DatSchemas.BaseItemTypes.Name).Trim());

				var category = GetItemCategory(baseItemType, i);
				if(string.IsNullOrEmpty(category))
				{
					// Ignore items without an appropriate category.
					continue;
				}

				// Explicitly exclude old maps from previous expansions.
				if(ShouldExclude(id, category, inheritsFrom))
				{
					Logger.WriteLine($"[BITsV2] Excluded: '{id}' ('{name}')");
					continue;
				}

				var names = baseItemTypesDatContainers.ToDictionary(kvp => kvp.Key, kvp => Escape(kvp.Value[0].Records[i].GetValue<string>(DatSchemas.BaseItemTypes.Name).Trim()));

				WriteRecord(id, names, GetImageByName(id, names[Language.English], category), category, baseItemType.GetValue<int>(DatSchemas.BaseItemTypes.Width), baseItemType.GetValue<int>(DatSchemas.BaseItemTypes.Height));
			}

			// Nested Method(s)
			string GetImageByName(string id, string name, string category)
			{
				if(staticTradeData != null)
				{
					foreach(var group in staticTradeData["result"]!)
					{
						string groupLabel = (string)group["label"]!;
						foreach(var entry in group["entries"]!)
						{
							string entryText = (string)entry["text"]!;
							if(entryText == name)
							{
								if(PoEStaticDataLabelToImagesMapping.TryGetValue(groupLabel, out string? groupImage))
								{
									return groupImage;
								}
								else
								{
									var imageObj = entry["image"];
									if(imageObj != null)
									{
										return (string)imageObj!;
									}
								}
							}
						}
					}
				}

				if(category.StartsWith(ItemCategory.Currency) || category.StartsWith(ItemCategory.Map))
				{
					PrintWarning($"Missing Image for '{id}' ('{name}' ; Category: '{category}')");
				}

				return string.Empty;
			}

			static bool ShouldExclude(string id, string category, string inheritsFrom)
			{
				if(category != ItemCategory.Map)
				{
					return false;
				}
				return inheritsFrom switch
				{
					"AbstractVaultKey" => false,
					"AbstractMap" => false,
					_ => true,
				};
			}

			void WriteRecord(string id, Dictionary<Language, string> names, string image, string category, int width, int height)
			{
				// Write ID
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteStartObject();

				// Write Names
				jsonWriter.WritePropertyName("names");
				jsonWriter.WriteStartObject();
				foreach((var language, var name) in names)
				{
					jsonWriter.WritePropertyName(((int)language).ToString(CultureInfo.InvariantCulture));
					jsonWriter.WriteValue(name);
				}
				jsonWriter.WriteEndObject();

				// Write Art Name
				if(!string.IsNullOrEmpty(image))
				{
					jsonWriter.WritePropertyName("image");
					jsonWriter.WriteValue(image);
				}

				// Write Category
				if(!string.IsNullOrEmpty(category))
				{
					jsonWriter.WritePropertyName("category");
					jsonWriter.WriteValue(category);
				}

				// Write Size
				jsonWriter.WritePropertyName("width");
				jsonWriter.WriteValue(width);
				jsonWriter.WritePropertyName("height");
				jsonWriter.WriteValue(height);

				jsonWriter.WriteEndObject();
			}
		}

		static string Escape(string input)
			=> input
				.Replace("[", "\\[")
				.Replace("]", "\\]")
				.Replace("(", "\\(")
				.Replace(")", "\\)")
				.Replace(".", "\\.")
				.Replace("|", "\\|");
	}

	#endregion
}
