using LibDat;
using LibGGPK;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PoEAssetUpdater
{
	public class Program
	{
		#region Properties

		private static string ApplicationName => Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);

		private const string EnglishLanguage = "English";
		private static readonly string[] Languages = new string[] { EnglishLanguage, "French", "German", "Korean", "Portuguese", "Russian", "SimplifiedChinese", "Spanish", "Thai", "TraditionalChinese" };

		private const int TotalNumberOfStats = 6;

		private const ulong UndefinedValue = 18374403900871474942L;

		#endregion

		#region Public Methods

		public static void Main(string[] args)
		{
			// Validate args array size
			if(args.Length != 2)
			{
				Console.WriteLine("Invalid number of arguments.");
				PrintUsage();
				return;
			}

			// Validate arguments
			string contentFilePath = args[0];
			if(!File.Exists(contentFilePath))
			{
				Console.WriteLine($"File '{contentFilePath}' does not exist.");
				PrintUsage();
				return;
			}
			string assetOutputDir = args[1];
			if(!Directory.Exists(assetOutputDir))
			{
				Console.WriteLine($"Directory '{assetOutputDir}' does not exist.");
				PrintUsage();
				return;
			}

			// Read the GGPKG file
			GrindingGearsPackageContainer container = new GrindingGearsPackageContainer();
			container.Read(contentFilePath, Console.Write);

			//base-item-type-categories.json
			ExportBaseItemTypes(contentFilePath, assetOutputDir, container);
			ExportClientStrings(contentFilePath, assetOutputDir, container);
			//maps.json
			ExportMods(contentFilePath, assetOutputDir, container);
			ExportStats(contentFilePath, assetOutputDir, container);
			//stats-local.json -> Likely created manually.
			ExportWords(contentFilePath, assetOutputDir, container);

			Console.Read();
		}

		#endregion

		#region Private Methods

		private static void PrintUsage()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine($"{ApplicationName} <path-to-Content.ggpk> <asset-output-dir>");
			Console.Read();
		}

		private static void ExportDataFile(GrindingGearsPackageContainer container, string contentFilePath, string exportFilePath, Action<string, DirectoryTreeNode, JsonWriter> writeData)
		{
			Console.WriteLine($"Exporting {Path.GetFileName(exportFilePath)}...");

			var dataDir = container.DirectoryRoot.Children.Find(x => x.Name == "Data");

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

				writeData(contentFilePath, dataDir, jsonWriter);

				jsonWriter.WriteEndObject();
			}

			Console.WriteLine($"Exported '{exportFilePath}'.");
		}

		private static void ExportLanguageDataFile(string contentFilePath, DirectoryTreeNode dataDir, JsonWriter jsonWriter, string datFileName, Action<int, RecordData, JsonWriter> writeRecordData)
		{
			foreach(var language in Languages)
			{
				// Determine the directory to search for the given datFile. English is the base/main language and isn't located in a sub-folder.
				var searchDir = language == EnglishLanguage ? dataDir : dataDir.Children.FirstOrDefault(x => x.Name == language);
				if(searchDir == null)
				{
					Console.WriteLine($"\t{language} Language folder not found.");
					continue;
				}

				// Find the given datFile.
				var datContainer = GetDatContainer(searchDir, contentFilePath, datFileName);
				if(datContainer == null)
				{
					// An error was already logged.
					continue;
				}

				Console.WriteLine($"\tExporting {language}.");

				// Create a node and write the data of each record in this node.
				jsonWriter.WritePropertyName(language);
				jsonWriter.WriteStartObject();

				for(int j = 0, recordsLength = datContainer.Records.Count; j < recordsLength; j++)
				{
					writeRecordData(j, datContainer.Records[j], jsonWriter);
				}

				jsonWriter.WriteEndObject();
			}
		}

		private static void ExportBaseItemTypes(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "base-item-types.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "BaseItemTypes.dat", WriteRecord);
			}

			void WriteRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = recordData.GetDataValueStringByFieldId("Id").Split('/').Last();
				string name = recordData.GetDataValueStringByFieldId("Name");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
				jsonWriter.WritePropertyName(name);
				jsonWriter.WriteValue(id);
			}
		}

		private static void ExportClientStrings(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "client-strings.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "ClientStrings.dat", WriteRecord);
			}

			void WriteRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = recordData.GetDataValueStringByFieldId("Id");
				string name = recordData.GetDataValueStringByFieldId("Text");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
			}
		}

		private static void ExportWords(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "words.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "Words.dat", WriteRecord);
			}

			void WriteRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = idx.ToString(CultureInfo.InvariantCulture);
				string name = recordData.GetDataValueStringByFieldId("Text");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
				jsonWriter.WritePropertyName(name);
				jsonWriter.WriteValue(id);
			}
		}

		private static DatContainer GetDatContainer(DirectoryTreeNode dataDir, string contentFilePath, string datFileName)
		{
			var dataFile = dataDir.Files.FirstOrDefault(x => x.Name == datFileName);
			if(dataFile == null)
			{
				Console.WriteLine($"\t{datFileName} not found in '{dataDir.Name}'.");
				return null;
			}

			var data = dataFile.ReadFileContent(contentFilePath);
			using(var dataStream = new MemoryStream(data))
			{
				// Read the MemoryStream and create a DatContainer
				return new DatContainer(dataStream, dataFile.Name);
			}
		}

		private static void ExportMods(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "mods.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				var modsDatContainer = GetDatContainer(dataDir, contentFilePath, "Mods.dat");
				var statsDatContainer = GetDatContainer(dataDir, contentFilePath, "Stats.dat");

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
						jsonWriter.WritePropertyName(recordData.GetDataValueStringByFieldId("Id").Replace(recordData.GetDataValueStringByFieldId("CorrectGroup"), ""));
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

				(RecordData recordData, string statNames, int lastValidStatNum) RecordSelector(RecordData recordData)
				{
					List<string> statNames = new List<string>();
					int lastValidStatsKey = 0;
					for(int i = 1; i <= TotalNumberOfStats; i++)
					{
						ulong statsKey = ulong.Parse(recordData.GetDataValueStringByFieldId(string.Concat("StatsKey", i.ToString(CultureInfo.InvariantCulture))));

						if(statsKey != UndefinedValue)
						{
							statNames.Add(statsDatContainer.Records[(int)statsKey].GetDataValueStringByFieldId("Id"));
							lastValidStatsKey = i;
						}
					}
					return (recordData, string.Join(" ", statNames.Distinct().ToArray()), lastValidStatsKey);
				}
			}

			void WriteMinMaxValues(RecordData recordData, JsonWriter jsonWriter, int statNum)
			{
				string statPrefix = string.Concat("Stat", statNum.ToString(CultureInfo.InvariantCulture));
				int minValue = int.Parse(recordData.GetDataValueStringByFieldId(string.Concat(statPrefix, "Min")));
				int maxValue = int.Parse(recordData.GetDataValueStringByFieldId(string.Concat(statPrefix, "Max")));

				jsonWriter.WriteStartObject();
				jsonWriter.WritePropertyName("min");
				jsonWriter.WriteValue(minValue);
				jsonWriter.WritePropertyName("max");
				jsonWriter.WriteValue(maxValue);
				jsonWriter.WriteEndObject();
			}
		}

		private static void ExportStats(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "stats.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				var modsDatContainer = GetDatContainer(dataDir, contentFilePath, "Mods.dat");
				var statsDatContainer = GetDatContainer(dataDir, contentFilePath, "Stats.dat");

				if(modsDatContainer == null || statsDatContainer == null)
				{
					return;
				}
			}
		}

		#endregion
	}
}
