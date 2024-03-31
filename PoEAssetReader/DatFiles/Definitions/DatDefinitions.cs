using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class DatDefinitions
	{
		#region Consts

		private static string ApplicationVersion => Assembly.GetAssembly(typeof(DatDefinitions)).GetName().Version.ToString();

		#endregion

		private DatDefinitions(FileDefinition[] fileDefinitions)
		{
			FileDefinitions = fileDefinitions;
		}

		#region Properties

		public FileDefinition[] FileDefinitions
		{
			get;
		}

		#endregion

		#region Public Methods

		public static DatDefinitions ParseJson(string definitionsFileName)
		{
			using StreamReader streamReader = new StreamReader(definitionsFileName);
			using JsonReader reader = new JsonTextReader(streamReader);
			List<FileDefinition> fileDefinitions = new List<FileDefinition>();
			var jsonFileDefinitions = JArray.Load(reader);
			foreach (JObject jsonFileDefinition in jsonFileDefinitions.Children())
			{
				string name = jsonFileDefinition.Value<string>("name");
				List<FieldDefinition> fields = new List<FieldDefinition>();
				foreach (JObject jsonFieldDefinition in jsonFileDefinition.Values("fields"))
				{
					string id = jsonFieldDefinition.Value<string>("id");
					string dataType = jsonFieldDefinition.Value<string>("type");
					fields.Add(new FieldDefinition(id, TypeDefinition.Parse(dataType, false), null, null));
				}
				fileDefinitions.Add(new FileDefinition(name, fields.ToArray()));
			}
			return new DatDefinitions(fileDefinitions.ToArray());
		}

		public static DatDefinitions ParseLocalPyPoE(string fileName) => ParsePyPoE(File.ReadAllText(fileName));

		public static DatDefinitions ParsePyPoE()
		{
			using HttpClient client = new();
			client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PoEOverlayAssetReader/" + ApplicationVersion));

			try
			{
				var request = client.GetAsync("https://raw.githubusercontent.com/brather1ng/PyPoE/dev/PyPoE/poe/file/specification/data/stable.py");
				request.RunSynchronously();
				using var response = request.Result;
				using var content = response.Content;
				var task = content.ReadAsStringAsync();
				task.RunSynchronously();
				return ParsePyPoE(task.Result);
			}
			catch (Exception ex)
			{
				throw new Exception($"Failed to download definitions from PyPoE's GitHub.", ex);
			}
		}

		public static DatDefinitions ParsePyPoE(string definitionsFileContent)
		{
			string[] lines = definitionsFileContent.Split("\r\n".ToCharArray());
			List<FileDefinition> fileDefinitions = new List<FileDefinition>();
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				if (line.EndsWith("': File("))
				{
					string name = line.Split("'")[1];
					List<FieldDefinition> fields = new List<FieldDefinition>();
					for (i++; i < lines.Length; i++)
					{
						line = lines[i];
						if (line.EndsWith("Field("))
						{
							bool findEnd = false;
							line = FindLine(ref lines, ref i, s => s.Trim().StartsWith("name='"));
							string id = line.Split("'")[1];

							line = FindLine(ref lines, ref i, s => s.Trim().StartsWith("type='"));
							string dataType = line.Split("'")[1];

							// Find the ref dat file or closing tag for this Field.
							line = FindLine(ref lines, ref i, s => IsRefDatFileName(s) || IsEndOfSection(s));
							string refDatFileName = null;
							string refDatFieldID = null;
							if (IsRefDatFileName(line))
							{
								refDatFileName = line.Split("'")[1];

								line = FindLine(ref lines, ref i, s => IsRefDataFieldID(s) || IsEndOfSection(s));
								if (IsRefDataFieldID(line))
								{
									refDatFieldID = line.Split("'")[1];
									findEnd = true;
								}
							}

							fields.Add(new FieldDefinition(id, TypeDefinition.Parse(dataType, false), refDatFileName, refDatFieldID));

							if (findEnd)
							{
								FindLine(ref lines, ref i, IsEndOfSection);
							}
						}
						else if (IsEndOfSection(line))
						{
							break;
						}
					}
					fileDefinitions.Add(new FileDefinition(name, fields.ToArray()));
				}
			}
			return new DatDefinitions(fileDefinitions.ToArray());

			// Nested Method(s)
			static string FindLine(ref string[] lines, ref int idx, Predicate<string> predicate)
			{
				for (; idx < lines.Length; idx++)
				{
					string line = lines[idx];
					if (predicate(line))
					{
						return line;
					}
				}
				return string.Empty;
			}

			static bool IsEndOfSection(string input)
			{
				return input.Trim().EndsWith("),");
			}

			static bool IsRefDatFileName(string input)
			{
				return input.Trim().StartsWith("key='");
			}

			static bool IsRefDataFieldID(string input)
			{
				return input.Trim().StartsWith("key_id='");
			}
		}

		public static DatDefinitions ParseLocalGQLDirectory(string directory)
		{
			bool x64 = true;
			List<FileDefinition> fileDefinitions = new List<FileDefinition>();
			List<string> enums = new List<string>();
			foreach (string filePath in Directory.GetFiles(directory).OrderBy(x => x))
			{
				string[] lines = File.ReadAllLines(filePath);
				for (int i = 0; i < lines.Length; i++)
				{
					string line = lines[i];
					if (line.StartsWith("type "))
					{
						string name = $"{line.Split(" ")[1].Trim()}.dat{(x64 ? "64" : string.Empty)}";
						List<FieldDefinition> fields = new List<FieldDefinition>();
						for (i++; i < lines.Length; i++)
						{
							line = lines[i].Trim();
							if (line.StartsWith("}"))
							{
								break;
							}
							else if (!line.StartsWith("\"") && line.Contains(":"))
							{
								string[] split = line.Split(": @".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
								string id = split[0];
								string refDatFieldID = null;
								string dataType = ConvertToPyPoeDataType(split[1], filePath, fileDefinitions, x64, out string refDatFileName);
								if (!string.IsNullOrEmpty(refDatFileName) && split.Length >= 4 && split[2].StartsWith("ref(column"))
								{
									refDatFieldID = split[3].Split("\"", StringSplitOptions.RemoveEmptyEntries)[0];
								}

								int j = 1;
								string originalId = id;
								while (fields.Exists(x => x.ID == id))
								{
									id = $"{originalId}{j}";
									j++;
								}

								fields.Add(new FieldDefinition(id, TypeDefinition.Parse(dataType, x64), refDatFileName, refDatFieldID));
							}
						}
						fileDefinitions.Add(new FileDefinition(name, fields.ToArray()));
					}
					else if (line.StartsWith("enum "))
					{
						string name = $"{line.Split(" ")[1].Trim()}.dat{(x64 ? "64" : string.Empty)}";
						enums.Add(name);
					}
				}
			}

			// Update the type of referenced fields
			for (int i = fileDefinitions.Count - 1; i >= 0; i--)
			{
				FileDefinition fileDefinition = fileDefinitions[i];
				if (!fileDefinition.Fields.Any(x => !string.IsNullOrEmpty(x.RefDatFileName)))
				{
					continue;
				}
				List<FieldDefinition> fields = new List<FieldDefinition>();
				foreach (var field in fileDefinition.Fields)
				{
					if (string.IsNullOrEmpty(field.RefDatFileName))
					{
						fields.Add(field);
						continue;
					}

					TypeDefinition dataType;
					if (enums.Contains(field.RefDatFileName))
					{
						dataType = TypeDefinition.Parse(x64 ? field.DataType.Name.Replace("uint128", "int") : field.DataType.Name.Replace("ulong", "int"), x64);
					}
					else
					{
						FileDefinition refDatFile = fileDefinitions.FirstOrDefault(x => x.Name == field.RefDatFileName);
						FieldDefinition refDatField = refDatFile?.Fields.FirstOrDefault(x => x.ID == field.RefDatFieldID);
						if (refDatField != null)
						{
							dataType = TypeDefinition.Parse(x64 ? field.DataType.Name.Replace("uint128", refDatField.DataType.Name) : field.DataType.Name.Replace("ulong", refDatField.DataType.Name), x64);
						}
						else if(refDatFile?.Name == fileDefinition.Name)
						{
							dataType = TypeDefinition.Parse(x64 ? field.DataType.Name.Replace("uint128", "ulong") : field.DataType.Name.Replace("ulong", "uint"), x64);
						}
						else
						{
							dataType = field.DataType;
						}
					}

					fields.Add(new FieldDefinition(field.ID, dataType, field.RefDatFileName, field.RefDatFieldID));
				}
				fileDefinitions[i] = new FileDefinition(fileDefinition.Name, fields.ToArray());
			}

			return new DatDefinitions(fileDefinitions.ToArray());

			// Nested Method(s)
			static string ConvertToPyPoeDataType(string input, string filePath, List<FileDefinition> fileDefinitions, bool x64, out string refDatFileName)
			{
				refDatFileName = null;

				string result = string.Empty;
				bool isList = input.StartsWith("[");
				if (isList)
				{
					result = "ref|list|";
					input = input.Replace("[", "").Replace("]", "");
				}

				string dataType;
				switch (input)
				{
					case "string":
						dataType = "ref|string";
						break;
					case "bool":
						dataType = "bool";
						break;
					case "i32":
						dataType = isList ? "uint" : "int";
						break;
					case "f32":
						dataType = "float";
						break;
					case "rid":
					case "_":
						dataType = x64 ? "uint128" : "ulong";
						break;

					default:
						if (!char.IsUpper(input[0]))
						{
							throw new InvalidDataException($"Invalid data type '{input}' found in '{filePath}'");
						}
						refDatFileName = $"{input}.dat{(x64 ? "64" : string.Empty)}";
						dataType = x64 ? "uint128" : "ulong";
						break;
				}

				result += dataType;

				return result;
			}
		}

		#endregion
	}
}
