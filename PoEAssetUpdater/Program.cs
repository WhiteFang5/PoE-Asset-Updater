using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PoEAssetReader;
using PoEAssetReader.DatFiles;
using PoEAssetReader.DatFiles.Definitions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PoEAssetUpdater
{
	public class Program
	{
		#region Properties

		private static string ApplicationName => Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
		private static string ApplicationVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();

		private const int TotalNumberOfStats = 6;

		private const ulong UndefinedValue = 18374403900871474942L;

		private static readonly char[] NewLineSplitter = "\r\n".ToCharArray();
		private static readonly char[] WhiteSpaceSplitter = "\t ".ToCharArray();

		private static readonly Language[] AllLanguages = (Language[])Enum.GetValues(typeof(Language));

		private const string CountryURLFormat = "https://{0}.pathofexile.com/api/trade/data/stats";
		private static readonly Dictionary<Language, string> LanguageToPoETradeAPIUrlMapping = new Dictionary<Language, string>()
		{
			[Language.English] = string.Format(CountryURLFormat, "www"),
			[Language.Portuguese] = string.Format(CountryURLFormat, "br"),
			[Language.Russian] = string.Format(CountryURLFormat, "ru"),
			[Language.Thai] = string.Format(CountryURLFormat, "th"),
			[Language.German] = string.Format(CountryURLFormat, "de"),
			[Language.French] = string.Format(CountryURLFormat, "fr"),
			[Language.Spanish] = string.Format(CountryURLFormat, "es"),
			[Language.Korean] = "https://poe.game.daum.net/api/trade/data/stats",
			[Language.SimplifiedChinese] = "https://poe.game.qq.com/api/trade/data/stats",
			[Language.TraditionalChinese] = "https://web.poe.garena.tw/api/trade/data/stats",
		};

		private static readonly Regex StatDescriptionLangRegex = new Regex("^lang \"(.*)\"$");

		private static readonly string[] LabelsWithSuffix = new string[] { "implicit", "crafted", "fractured", "enchant" };

		private static readonly Dictionary<string, string> BaseItemTypeInheritsFromToCategoryMapping = new Dictionary<string, string>()
		{
			// Accessories
			["AbstractRing"] = ItemCategory.AccessoryRing,
			["AbstractAmulet"] = ItemCategory.AccessoryAmulet,
			["AbstractBelt"] = ItemCategory.AccessoryBelt,
			["AbstractTrinket"] = ItemCategory.AccessoryTrinket,
			// Armors/Armours
			["AbstractShield"] = ItemCategory.ArmourShield,
			["AbstractHelmet"] = ItemCategory.ArmourHelmet,
			["AbstractBodyArmour"] = ItemCategory.ArmourChest,
			["AbstractBoots"] = ItemCategory.ArmourBoots,
			["AbstractGloves"] = ItemCategory.ArmourGloves,
			["AbstractQuiver"] = ItemCategory.ArmourQuiver,
			// Currencies
			["AbstractCurrency"] = ItemCategory.Currency,
			["StackableCurrency"] = ItemCategory.Currency,
			["DelveSocketableCurrency"] = ItemCategory.CurrencyResonator,
			["DelveStackableSocketableCurrency"] = ItemCategory.CurrencyResonator,
			["AbstractUniqueFragment"] = ItemCategory.CurrencyPiece,
			["HarvestSeed"] = ItemCategory.CurrencySeed,
			["HarvestPlantBooster"] = ItemCategory.CurrencySeedBooster,
			["HeistObjective"] = ItemCategory.CurrencyHeistTarget,
			["Incubator"] = ItemCategory.CurrencyIncubator,
			// Divination Cards
			["AbstractDivinationCard"] = ItemCategory.Card,
			// Flasks
			["AbstractLifeFlask"] = ItemCategory.Flask,
			["AbstractManaFlask"] = ItemCategory.Flask,
			["AbstractHybridFlask"] = ItemCategory.Flask,
			["CriticalUtilityFlask"] = ItemCategory.Flask,
			["AbstractUtilityFlask"] = ItemCategory.Flask,
			// Gems
			["ActiveSkillGem"] = ItemCategory.GemActivegem,
			["SupportSkillGem"] = ItemCategory.GemSupportGem,
			// Heist
			["HeistContract"] = ItemCategory.HeistContract,
			["HeistBlueprint"] = ItemCategory.HeistBlueprint,
			["AbstractHeistEquipment"] = ItemCategory.HeistEquipment,
			// Jewels
			["AbstractJewel"] = ItemCategory.Jewel,
			["AbstractAbyssJewel"] = ItemCategory.JewelAbyss,
			// Leaguestones
			["Leaguestone"] = ItemCategory.Leaguestone,
			// Maps
			["AbstractMap"] = ItemCategory.Map,
			["AbstractMapFragment"] = ItemCategory.MapFragment,
			["AbstractMiscMapItem"] = ItemCategory.MapFragment,
			["OfferingToTheGoddess"] = ItemCategory.MapFragment,
			// Metamorph Samples
			["MetamorphosisDNA"] = ItemCategory.MonsterSample,
			// Watchstones
			["AtlasRegionUpgrade"] = ItemCategory.Watchstone,
			// Weapons
			["AbstractTwoHandSword"] = ItemCategory.WeaponTwoSword,
			["AbstractWand"] = ItemCategory.WeaponWand,
			["AbstractDagger"] = ItemCategory.WeaponDagger,
			["AbstractRuneDagger"] = ItemCategory.WeaponRunedagger,
			["AbstractClaw"] = ItemCategory.WeaponClaw,
			["AbstractOneHandAxe"] = ItemCategory.WeaponOneAxe,
			["AbstractOneHandSword"] = ItemCategory.WeaponOneSword,
			["AbstractOneHandSwordThrusting"] = ItemCategory.WeaponOneSword,
			["AbstractOneHandMace"] = ItemCategory.WeaponOneMace,
			["AbstractSceptre"] = ItemCategory.WeaponSceptre,
			["AbstractBow"] = ItemCategory.WeaponBow,
			["AbstractStaff"] = ItemCategory.WeaponStaff,
			["AbstractWarstaff"] = ItemCategory.WeaponWarstaff,
			["AbstractTwoHandAxe"] = ItemCategory.WeaponTwoAxe,
			["AbstractTwoHandSword"] = ItemCategory.WeaponTwoSword,
			["AbstractTwoHandMace"] = ItemCategory.WeaponTwoMace,
			["AbstractFishingRod"] = ItemCategory.WeaponRod,
			// Ignored (i.e. not exported as they're untradable items!)
			["AbstractMicrotransaction"] = null,
			["AbstractQuestItem"] = null,
			["AbstractLabyrinthItem"] = null,
			["AbstractHideoutDoodad"] = null,
			["LabyrinthTrinket"] = null,
			["AbstactPantheonSoul"] = null,
			["HarvestInfrastructure"] = null,
			["Item"] = null,
		};

		private static readonly Dictionary<string, string> HarvestSeedPrefixToItemCategoryMapping = new Dictionary<string, string>()
		{
			["Wild"] = ItemCategory.CurrencyWildSeed,
			["Vivid"] = ItemCategory.CurrencyVividSeed,
			["Primal"] = ItemCategory.CurrencyPrimalSeed,
		};

		private static readonly Dictionary<ulong, string> TagsToItemCategoryMapping = new Dictionary<ulong, string>()
		{
			[651] = ItemCategory.HeistCloak,
			[652] = ItemCategory.HeistBrooch,
			[653] = ItemCategory.HeistGear,
			[664] = ItemCategory.HeistTool,
			[696] = ItemCategory.MapInvitation,
		};

		private static readonly string[] IgnoredItemIds = new string[]
		{
			"HeistEquipmentWeaponTest",
			"HeistEquipmentToolTest",
			"HeistEquipmentUtilityTest",
			"HeistEquipmentRewardTest",
		};

		private static readonly string[] IgnoredProphecyIds = new string[]
		{
			"MapExtraHaku",
			"MapExtraTora",
			"MapExtraCatarina",
			"MapExtraVagan",
			"MapExtraElreon",
			"MapExtraVorici",
		};

		private static readonly Dictionary<string, string> ProphecyIdToSuffixClientStringIdMapping = new Dictionary<string, string>()
		{
			["MapExtraZana"] = "MasterNameZana",
			["MapExtraEinhar"] = "MasterNameEinhar",
			["MapExtraAlva"] = "MasterNameAlva",
			["MapExtraNiko"] = "MasterNameNiko",
			["MapExtraJun"] = "MasterNameJun",
		};

		#endregion

		#region Public Methods

		public static void Main(string[] args)
		{
			// Validate args array size
			if(args.Length != 2)
			{
				Logger.WriteLine($"Invalid number of arguments. Found {args.Length}, expected 2.");
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

			try
			{
				// Read the index
				AssetIndex assetIndex = new AssetIndex(poeDirectory);
				DatDefinitions datDefinitions = DatDefinitions.ParsePyPoE();

				//assetIndex.ExportBundleTree(Path.Combine(assetIndex.PoEDirectory, "_.index.tree.json"));

				ExportBaseItemTypeCategories(assetIndex, datDefinitions, assetOutputDir);
				ExportBaseItemTypes(assetIndex, datDefinitions, assetOutputDir);
				ExportClientStrings(assetIndex, datDefinitions, assetOutputDir);
				//maps.json -> Likely created/maintained manually.
				ExportMods(assetIndex, datDefinitions, assetOutputDir);
				ExportStats(assetIndex, datDefinitions, assetOutputDir);
				//stats-local.json -> Likely/maintained created manually.
				ExportWords(assetIndex, datDefinitions, assetOutputDir);
				ExportAnnointments(assetIndex, datDefinitions, assetOutputDir);
			}
#if !DEBUG
			catch(Exception ex)
			{
				PrintError($"{ex.Message}\r\n{ex.StackTrace}");
			}
#endif
			finally
			{
				Logger.SaveLogs(Path.Combine(assetOutputDir, string.Concat(ApplicationName, ".log")));
			}

			Console.WriteLine(string.Empty);
			Console.WriteLine("Press any key to exit...");
			Console.Read();
		}

		#endregion

		#region Private Methods

		public delegate (string key, string value) GetKeyValuePairDelegate(int idx, DatRecord recordData, List<AssetFile> languageFiles);

		private static void PrintUsage()
		{
			Logger.WriteLine("Usage:");
			Logger.WriteLine($"{ApplicationName} <path-to-Content.ggpk> <asset-output-dir>");
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

		private static void ExportDataFile(AssetIndex assetIndex, string exportFilePath, Action<List<AssetFile>, JsonWriter> writeData, bool includeLanguageFolders)
		{
			Logger.WriteLine($"Exporting {Path.GetFileName(exportFilePath)}...");

			List<AssetFile> dataFiles = includeLanguageFolders ? assetIndex.FindFiles(x => x.Name.StartsWith("Data/")) : assetIndex.FindFiles(x => Path.GetDirectoryName(x.Name) == "Data");

			using(var streamWriter = new StreamWriter(exportFilePath))
			{
				// Create a JSON writer with human-readable output.
				var jsonWriter = new JsonTextWriter(streamWriter)
				{
					Formatting = Formatting.Indented,
					Indentation = 1,
					IndentChar = '\t'
				};
				jsonWriter.WriteStartObject();

				writeData(dataFiles, jsonWriter);

				jsonWriter.WriteEndObject();
			}

			Logger.WriteLine($"Exported '{exportFilePath}'.");
		}

		private static void ExportLanguageDataFile(List<AssetFile> assetFiles, DatDefinitions datDefinitions, JsonWriter jsonWriter, Dictionary<string, GetKeyValuePairDelegate> datFiles, bool mirroredRecords)
		{
			foreach(var language in AllLanguages)
			{
				Dictionary<string, string> records = new Dictionary<string, string>();

				// Determine the directory to search for the given datFile. English is the base/main language and isn't located in a sub-folder.
				var langDir = (language == Language.English ? "Data" : $"Data\\{language}").ToLowerInvariant();
				var languageFiles = assetFiles.FindAll(x => Path.GetDirectoryName(x.Name).ToLowerInvariant() == langDir);
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
							(string key, string value) = getKeyValuePair(j, datContainer.Records[j], languageFiles);
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

		private static void ExportBaseItemTypes(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
		{
			ExportDataFile(assetIndex, Path.Combine(exportDir, "base-item-types.json"), WriteRecords, true);

			void WriteRecords(List<AssetFile> assetFiles, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(assetFiles, datDefinitions, jsonWriter, new Dictionary<string, GetKeyValuePairDelegate>()
				{
					["BaseItemTypes.dat"] = GetBaseItemTypeKVP,
					["Prophecies.dat"] = GetPropheciesKVP,
					["MonsterVarieties.dat"] = GetMonsterVaritiesKVP,
				}, true);
			}

			static (string, string) GetBaseItemTypeKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				string id = recordData.GetValue<string>("Id").Split('/').Last();
				string name = Escape(recordData.GetValue<string>("Name").Trim());
				string inheritsFrom = recordData.GetValue<string>("InheritsFrom").Split('/').Last();
				if(inheritsFrom == "AbstractMicrotransaction" || inheritsFrom == "AbstractHideoutDoodad")
				{
					return (null, null);
				}
				return (id, name);
			}

			(string, string) GetPropheciesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				string id = recordData.GetValue<string>("Id");
				string name = recordData.GetValue<string>("Name").Trim();

				if(IgnoredProphecyIds.Contains(id))
				{
					return (null, null);
				}

				if(ProphecyIdToSuffixClientStringIdMapping.TryGetValue(id, out string clientStringId))
				{
					var clientStringsDatContainer = GetDatFile(languageFiles, datDefinitions, "ClientStrings.dat");
					var clientStringRecordData = clientStringsDatContainer?.Records.First(x => x.GetValue<string>("Id") == clientStringId);
					if(clientStringRecordData != null)
					{
						name += $" ({clientStringRecordData.GetValue<string>("Text")})";
					}
					else
					{
						PrintError($"Missing {nameof(clientStringId)} for '{clientStringId}'");
					}
				}

				return (id, Escape(name));
			}

			static (string, string) GetMonsterVaritiesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				string id = recordData.GetValue<string>("Id").Split('/').Last();
				string name = Escape(recordData.GetValue<string>("Name").Trim());
				return (id, name);
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

		private static void ExportClientStrings(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
		{
			ExportDataFile(assetIndex, Path.Combine(exportDir, "client-strings.json"), WriteRecords, true);

			void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(dataFiles, datDefinitions, jsonWriter, new Dictionary<string, GetKeyValuePairDelegate>()
				{
					["ClientStrings.dat"] = GetClientStringKVP,
					["AlternateQualityTypes.dat"] = GetAlternateQualityTypesKVP,
					["MetamorphosisMetaSkillTypes.dat"] = GetMetamorphosisMetaSkillTypesKVP,
					["Prophecies.dat"] = GetPropheciesKVP,
					["GrantedEffectQualityTypes.dat"] = GetAlternateGemQualityTypesKVP,
				}, false);
			}

			static (string, string) GetClientStringKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				string id = recordData.GetValue<string>("Id");
				string name = recordData.GetValue<string>("Text").Trim();

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

			static (string, string) GetAlternateQualityTypesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				var modsKey = recordData.GetValue<ulong>("ModsKey");
				string id = string.Concat("Quality", (modsKey + 1).ToString(CultureInfo.InvariantCulture));//Magic number "1" is the lowest mods key value plus the magic number; It's used to create a DESC sort.
				string name = recordData.GetValue<string>("Description");
				return (id, name);
			}

			static (string, string) GetMetamorphosisMetaSkillTypesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				int index = recordData.GetValue<int>("Unknown8");
				string id = string.Concat("MetamorphBodyPart", (index + 1).ToString(CultureInfo.InvariantCulture));
				string name = recordData.GetValue<string>("BodypartName").Trim();
				return (id, name);
			}

			static (string, string) GetPropheciesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				string id = recordData.GetValue<string>("Id");
				string name = recordData.GetValue<string>("PredictionText").Trim();
				string name2 = recordData.GetValue<string>("PredictionText2").Trim();

				if(IgnoredProphecyIds.Contains(id))
				{
					return (null, null);
				}

				return ($"Prophecy{id}", string.IsNullOrEmpty(name2) ? name : name2);
			}

			static (string, string) GetAlternateGemQualityTypesKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				int qualityNum = recordData.GetValue<int>("Id");
				string id = string.Concat("GemAlternateQuality", qualityNum.ToString(CultureInfo.InvariantCulture), "EffectName");
				string name = recordData.GetValue<string>("Text");
				return (id, name);
			}
		}

		private static void ExportWords(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
		{
			ExportDataFile(assetIndex, Path.Combine(exportDir, "words.json"), WriteRecords, true);

			void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(dataFiles, datDefinitions, jsonWriter, new Dictionary<string, GetKeyValuePairDelegate>()
				{
					["Words.dat"] = GetWordsKVP,
				}, true);
			}

			static (string, string) GetWordsKVP(int idx, DatRecord recordData, List<AssetFile> languageFiles)
			{
				string id = idx.ToString(CultureInfo.InvariantCulture);
				string name = recordData.GetValue<string>("Text2").Trim();
				return (id, name);
			}
		}

		private static DatFile GetDatFile(List<AssetFile> assetFiles, DatDefinitions datDefinitions, string datFileName)
		{
			var assetFile = assetFiles.FirstOrDefault(x => Path.GetFileName(x.Name) == datFileName);
			if(assetFile == null)
			{
				Logger.WriteLine($"\t{datFileName} not found.");
				return null;
			}

			var datFile = new DatFile(assetFile, datDefinitions);
			if(datFile.Records.Count > 0 && datFile.Records[0].TryGetValue("_Remainder", out byte[] remainder))
			{
				PrintError($"Found {remainder.Length} Remainder Bytes in {datFileName}");
			} 
			return datFile;
		}

		private static void ExportMods(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
		{
			ExportDataFile(assetIndex, Path.Combine(exportDir, "mods.json"), WriteRecords, false);

			void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
			{
				var modsDatContainer = GetDatFile(dataFiles, datDefinitions, "Mods.dat");
				var statsDatContainer = GetDatFile(dataFiles, datDefinitions, "Stats.dat");

				if(modsDatContainer == null || statsDatContainer == null)
				{
					return;
				}

				// Create the root node.
				jsonWriter.WritePropertyName("Default");
				jsonWriter.WriteStartObject();

				// Group mods
				var groupedRecords = modsDatContainer.Records.Select(RecordSelector).GroupBy(x => x.statNames);

				foreach(var recordGroup in groupedRecords)
				{
					// Write the stat names
					jsonWriter.WritePropertyName(recordGroup.Key);
					jsonWriter.WriteStartObject();
					int recordIdx = 0;
					foreach(var (recordData, statNames, lastValidStatNum) in recordGroup)
					{
						// Write the stat name excluding its group name
						jsonWriter.WritePropertyName(recordData.GetValue<string>("Id").Replace(recordData.GetValue<string>("CorrectGroup"), ""));
						jsonWriter.WriteStartArray();

						// Write all stats in the array
						for(int i = 1; i <= lastValidStatNum; i++)
						{
							WriteMinMaxValues(recordData, jsonWriter, i);
						}

						jsonWriter.WriteEndArray();
						recordIdx++;
					}
					jsonWriter.WriteEnd();
				}
				jsonWriter.WriteEndObject();

				(DatRecord recordData, string statNames, int lastValidStatNum) RecordSelector(DatRecord recordData)
				{
					List<string> statNames = new List<string>();
					int lastValidStatsKey = 0;
					for(int i = 1; i <= TotalNumberOfStats; i++)
					{
						ulong statsKey = recordData.GetValue<ulong>(string.Concat("StatsKey", i.ToString(CultureInfo.InvariantCulture)));

						if(statsKey != UndefinedValue)
						{
							statNames.Add(statsDatContainer.Records[(int)statsKey].GetValue<string>("Id"));
							lastValidStatsKey = i;
						}
					}
					return (recordData, string.Join(" ", statNames.Distinct().ToArray()), lastValidStatsKey);
				}
			}

			static void WriteMinMaxValues(DatRecord recordData, JsonWriter jsonWriter, int statNum)
			{
				string statPrefix = string.Concat("Stat", statNum.ToString(CultureInfo.InvariantCulture));
				int minValue = recordData.GetValue<int>(string.Concat(statPrefix, "Min"));
				int maxValue = recordData.GetValue<int>(string.Concat(statPrefix, "Max"));

				jsonWriter.WriteStartObject();
				jsonWriter.WritePropertyName("min");
				jsonWriter.WriteValue(minValue);
				jsonWriter.WritePropertyName("max");
				jsonWriter.WriteValue(maxValue);
				jsonWriter.WriteEndObject();
			}
		}

		private static void ExportStats(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
		{
			ExportDataFile(assetIndex, Path.Combine(exportDir, "stats.json"), WriteRecords, false);

			void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
			{
				var statsDatContainer = GetDatFile(dataFiles, datDefinitions, "Stats.dat");
				var afflictionRewardTypeVisualsDatContainer = GetDatFile(dataFiles, datDefinitions, "AfflictionRewardTypeVisuals.dat");

				List<AssetFile> statDescriptionFiles = assetIndex.FindFiles(x => x.Name.StartsWith("Metadata/StatDescriptions"));
				string[] statDescriptionsText = GetStatDescriptions("stat_descriptions.txt");
				string[] mapStatDescriptionsText = GetStatDescriptions("map_stat_descriptions.txt");
				string[] atlasStatDescriptionsText = GetStatDescriptions("atlas_stat_descriptions.txt");
				string[] heistEquipmentStatDescriptionsText = GetStatDescriptions("heist_equipment_stat_descriptions.txt");

				if(statsDatContainer == null || afflictionRewardTypeVisualsDatContainer == null || statDescriptionFiles.Count == 0 || statDescriptionsText == null || atlasStatDescriptionsText == null || heistEquipmentStatDescriptionsText == null)
				{
					return;
				}

				Logger.WriteLine($"Parsing {statsDatContainer.FileDefinition.Name}...");

				string[] localStats = statsDatContainer.Records.Where(x => x.GetValue<bool>("IsLocal")).Select(x => x.GetValue<string>("Id")).ToArray();

				Logger.WriteLine($"Parsing {afflictionRewardTypeVisualsDatContainer.FileDefinition.Name}...");

				string[] afflictionRewardTypes = afflictionRewardTypeVisualsDatContainer.Records.Select(x => x.GetValue<string>("Name")).ToArray();

				Logger.WriteLine($"Parsing Stat Description Files...");

				// Create a list of all stat descriptions
				List<StatDescription> statDescriptions = new List<StatDescription>();
				string[] lines = statDescriptionsText.Concat(mapStatDescriptionsText).Concat(atlasStatDescriptionsText).Concat(heistEquipmentStatDescriptionsText).ToArray();
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
						StatDescription statDescription = new StatDescription(ids.Skip(1).ToArray(), ids.Any(x => localStats.Contains(x)));

						// Initial (first) language is always english
						Language language = Language.English;
						while(true)
						{
							// Read the next line as it contains how many mods are added.
							line = lines[++lineIdx];
							int textCount = int.Parse(line);
							for(int i = 0; i < textCount; i++)
							{
								statDescription.ParseAndAddStatLine(language, lines[++lineIdx], afflictionRewardTypes);
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

						statDescriptions.Add(statDescription);
					}
				}

				Logger.WriteLine("Downloading PoE Trade API Stats...");

				// Download the PoE Trade Stats json
				Dictionary<Language, JObject> poeTradeStats = new Dictionary<Language, JObject>();
				foreach ((var language, var tradeAPIUrl) in LanguageToPoETradeAPIUrlMapping)
				{
					try
					{
						HttpWebRequest request = (HttpWebRequest)WebRequest.Create(tradeAPIUrl);
						request.Timeout = 5*1000;
						request.Headers[HttpRequestHeader.UserAgent] = "PoEOverlayAssetUpdater/" + ApplicationVersion;
						using var response = (HttpWebResponse)request.GetResponse();
						if(response.StatusCode == HttpStatusCode.OK)
						{
							using Stream dataStream = response.GetResponseStream();
							using StreamReader reader = new StreamReader(dataStream);
							poeTradeStats[language] = JObject.Parse(reader.ReadToEnd());
						}
					}
					catch(Exception ex)
					{
						PrintError($"Failed to connect to '{tradeAPIUrl}': {ex.Message}");
					}
					// Sleep for a short time to avoid spamming the different trade APIs
					Thread.Sleep(1000);
				}

				Logger.WriteLine("Parsing PoE Trade API Stats...");

				if(!poeTradeStats.ContainsKey(Language.English))
				{
					PrintError($"Failed to parse PoE Trade API Stats.");
					return;
				}

				// Parse the PoE Trade Stats
				foreach(var result in poeTradeStats[Language.English]["result"])
				{
					var label = GetLabel(result);
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
							optionValues = options.ToDictionary(option => option["id"].ToString(), option => option["text"].ToString());
						}

						FindAndWriteStatDescription(label, tradeId, modValue, text, optionValues);
					}
					jsonWriter.WriteEndObject();
				}

				static string GetLabel(JToken token) => ((string)token["label"]).ToLowerInvariant();

				static string GetTradeID(JToken token, string label) => ((string)token["id"])[(label.Length + 1)..];

				static (string modlessText, string modValue) GetTradeMod(string tradeAPIStatDescription)
				{
					if (tradeAPIStatDescription.EndsWith(")"))
					{
						int bracketsOpenIdx = tradeAPIStatDescription.LastIndexOf("(");
						int bracketsCloseIdx = tradeAPIStatDescription.LastIndexOf(")");
						string modValue = tradeAPIStatDescription.Substring(bracketsOpenIdx + 1, bracketsCloseIdx - bracketsOpenIdx - 1).ToLowerInvariant();
						string modlessText = tradeAPIStatDescription.Substring(0, bracketsOpenIdx).Trim();
						return (modlessText, modValue);
					}
					return (tradeAPIStatDescription, null);
				}

				string[] GetStatDescriptions(string fileName)
				{
					var statDescriptionsFile = statDescriptionFiles.FirstOrDefault(x => Path.GetFileName(x.Name) == fileName);

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

				void FindAndWriteStatDescription(string label, string tradeId, string mod, string text, Dictionary<string, string> options)
				{
					bool explicitLocal = mod == "local";
					// Lookup the stat, unless it's a pseudo stat (those arn't supposed to be linked to real stats)
					StatDescription statDescription = label == "pseudo" ? null : statDescriptions.Find(x => (!explicitLocal || x.LocalStat) && x.HasMatchingStatLine(text));

					if(statDescription == null)
					{
						PrintWarning($"Missing {nameof(StatDescription)} for Label '{label}', TradeID '{tradeId}', Desc: '{text.Replace("\n", "\\n")}'");
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
								jsonWriter.WriteStartObject();
								if (statDescription != null)
								{
									foreach (var statLine in statDescription.GetStatLines(language, text, options != null))
									{
										WriteStatLine(statLine, options, label, jsonWriter);
									}
								}
								else
								{
									var tradeIdSearch = $"{label}.{tradeId}";

									JToken otherLangStat = null;
									if (poeTradeStats.TryGetValue(language, out var otherLangTradeStats))
									{
										otherLangStat = otherLangTradeStats["result"].SelectMany(x => x["entries"]).FirstOrDefault(x => ((string)x["id"]).ToLowerInvariant() == tradeIdSearch);
									}
									string otherLangText;
									if (otherLangStat != null)
									{
										otherLangText = (string)otherLangStat["text"];
										(otherLangText, _) = GetTradeMod(otherLangText);// i.e. strips the trade mod.
									}
									else
									{
										otherLangText = text;
										PrintWarning($"Missing {language} trade ID '{tradeIdSearch}'");
									}

									var statLine = new StatDescription.StatLine("#", otherLangText.Replace("\n", "\\n"));
									WriteStatLine(statLine, options, label, jsonWriter);
								}
								jsonWriter.WriteEndObject();
							}
						}
						jsonWriter.WriteEndObject();
					}
					jsonWriter.WriteEndObject();
				}
			}

			void WriteStatLine(StatDescription.StatLine statLine, Dictionary<string, string> options, string label, JsonWriter jsonWriter)
			{
				string desc = statLine.StatDescription;
				string descSuffix;
				if(LabelsWithSuffix.Contains(label))
				{
					descSuffix = $" \\({label}\\)";
				}
				else
				{
					descSuffix = string.Empty;
				}

				if(options == null)
				{
					jsonWriter.WritePropertyName(statLine.NumberPart);
					jsonWriter.WriteValue(StatDescription.StatLine.GetStatDescriptionRegex(string.Concat(desc, descSuffix)));
				}
				else
				{
					foreach((var id, var optionValue) in options)
					{
						// Split the options into lines, replaced the placeholder with each line, and join them back together to form a single line.
						string optionDesc = string.Join("\n", optionValue.Split('\n').Select(option => desc.Replace(StatDescription.Placeholder, option)));
						jsonWriter.WritePropertyName(id);
						jsonWriter.WriteValue(StatDescription.StatLine.GetStatDescriptionRegex(string.Concat(optionDesc, descSuffix)));
					}
				}
			}
		}

		private static void ExportBaseItemTypeCategories(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
		{
			ExportDataFile(assetIndex, Path.Combine(exportDir, "base-item-type-categories.json"), WriteRecords, false);

			void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
			{
				var baseItemTypesDatContainer = GetDatFile(dataFiles, datDefinitions, "BaseItemTypes.dat");
				var propheciesDatContainer = GetDatFile(dataFiles, datDefinitions, "Prophecies.dat");
				var monsterVarietiesDatContainer = GetDatFile(dataFiles, datDefinitions, "MonsterVarieties.dat");

				if(baseItemTypesDatContainer == null)
				{
					return;
				}

				// Create the root node.
				jsonWriter.WritePropertyName("Default");
				jsonWriter.WriteStartObject();

				// Write the Base Item Types
				for(int i = 0, recordCount = baseItemTypesDatContainer.Records.Count; i < recordCount; i++)
				{
					var baseItemType = baseItemTypesDatContainer.Records[i];
					string id = baseItemType.GetValue<string>("Id").Split('/').Last();
					string inheritsFrom = baseItemType.GetValue<string>("InheritsFrom").Split('/').Last();

					if(IgnoredItemIds.Contains(id))
					{
						continue;
					}

					// Check the inheritance mapping for a matching category.
					if(!BaseItemTypeInheritsFromToCategoryMapping.TryGetValue(inheritsFrom, out string category))
					{
						PrintError($"Missing {Path.GetFileNameWithoutExtension(baseItemTypesDatContainer.FileDefinition.Name)} Category for '{id}' (InheritsFrom '{inheritsFrom}') at row {i}");
						continue;
					}

					// Special cases
					switch (category)
					{
						// Special case for Fossils
						case ItemCategory.Currency when id.StartsWith("CurrencyDelveCrafting"):
							category = ItemCategory.CurrencyFossil;
							break;

						// Special case for Scarabs
						case ItemCategory.MapFragment when id.StartsWith("Scarab"):
							category = ItemCategory.MapScarab;
							break;

						// Special case for Awakened Support Gems
						case ItemCategory.GemSupportGem when id.EndsWith("Plus"):
							category = ItemCategory.GemSupportGemplus;
							break;

						// Special case for Cluster Jewels
						case ItemCategory.Jewel when id.StartsWith("JewelPassiveTreeExpansion"):
							category = ItemCategory.JewelCluster;
							break;

						// Special case for Harvest Seeds
						case ItemCategory.CurrencySeed:
							string seedName = baseItemType.GetValue<string>("Name").Split(' ').First();
							if (!HarvestSeedPrefixToItemCategoryMapping.TryGetValue(seedName, out category))
							{
								PrintWarning($"Missing Seed Name in {nameof(HarvestSeedPrefixToItemCategoryMapping)} for '{seedName}'");
								category = ItemCategory.CurrencySeed;
							}
							break;

						// Special case of Heist Equipment & Map Fragments
						case ItemCategory.HeistEquipment:
						case ItemCategory.MapFragment:
							foreach(ulong tag in baseItemType.GetValue<List<ulong>>("TagsKeys"))
							{
								if(TagsToItemCategoryMapping.TryGetValue(tag, out string newCategory))
								{
									category = newCategory;
								}
							}
							if(category == ItemCategory.HeistEquipment)
							{
								PrintWarning($"Missing Heist Equipment Tag in {TagsToItemCategoryMapping} for '{id}' ('{baseItemType.GetValue<string>("Name")}')");
							}
							break;
					}

					// Only write to the json if an appropriate category was found.
					if(category != null)
					{
						jsonWriter.WritePropertyName(id);
						jsonWriter.WriteValue(category);
					}
				}

				// Write the Prophecies
				foreach(var prophecy in propheciesDatContainer.Records)
				{
					jsonWriter.WritePropertyName(prophecy.GetValue<string>("Id"));
					jsonWriter.WriteValue(ItemCategory.Prophecy);
				}

				// Write the Monster Varieties
				foreach(var monsterVariety in monsterVarietiesDatContainer.Records)
				{
					jsonWriter.WritePropertyName(monsterVariety.GetValue<string>("Id").Split('/').Last());
					jsonWriter.WriteValue(ItemCategory.MonsterBeast);
				}

				jsonWriter.WriteEndObject();
			}
		}

		private static void ExportAnnointments(AssetIndex assetIndex, DatDefinitions datDefinitions, string exportDir)
		{
			ExportDataFile(assetIndex, Path.Combine(exportDir, "annointments.json"), WriteRecords, false);

			void WriteRecords(List<AssetFile> dataFiles, JsonWriter jsonWriter)
			{
				var baseItemTypesDatContainer = GetDatFile(dataFiles, datDefinitions, "BaseItemTypes.dat");
				var craftingItemsDatContainer = GetDatFile(dataFiles, datDefinitions, "BlightCraftingItems.dat");
				var craftingResultsDatContainer = GetDatFile(dataFiles, datDefinitions, "BlightCraftingResults.dat");
				var craftingRecipesDatContainer = GetDatFile(dataFiles, datDefinitions, "BlightCraftingRecipes.dat");
				var passiveSkillsDatContainer = GetDatFile(dataFiles, datDefinitions, "PassiveSkills.dat");

				jsonWriter.WritePropertyName("annointments");
				jsonWriter.WriteStartObject();

				// Write the Base Item Types
				for(int i = 0, recordCount = craftingRecipesDatContainer.Records.Count; i < recordCount; i++)
				{
					var craftingRecipe = craftingRecipesDatContainer.Records[i];
					var craftingType = craftingRecipe.GetValue<ulong>("BlightCraftingTypesKey");

					if(craftingType != 0)
					{
						continue;
					}

					var craftingItemKeys = craftingRecipe.GetValue<List<ulong>>("BlightCraftingItemsKeys");

					var craftingResultsKey = (int)craftingRecipe.GetValue<ulong>("BlightCraftingResultsKey");
					var craftingResult = craftingResultsDatContainer.Records[craftingResultsKey];

					var passiveSkillsKey = (int)craftingResult.GetValue<ulong>("PassiveSkillsKey");
					var passiveSkill = passiveSkillsDatContainer.Records[passiveSkillsKey];

					var statOptionID = passiveSkill.GetValue<int>("PassiveSkillGraphId");

					jsonWriter.WritePropertyName(statOptionID.ToString(CultureInfo.InvariantCulture));
					jsonWriter.WriteStartArray();
					foreach(var craftingItemKey in craftingItemKeys)
					{
						var craftingItem = craftingItemsDatContainer.Records[(int)craftingItemKey];
						var baseItemTypeKey = (int)craftingItem.GetValue<ulong>("BaseItemTypesKey");
						var baseItemType = baseItemTypesDatContainer.Records[baseItemTypeKey];
						var id = baseItemType.GetValue<string>("Id").Split('/').Last();

						jsonWriter.WriteValue(id);
					}
					jsonWriter.WriteEndArray();
				}

				jsonWriter.WriteEndObject();
			}
		}

		#endregion
	}
}
