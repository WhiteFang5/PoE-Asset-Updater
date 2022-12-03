using PoEAssetReader.DatFiles.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PoEAssetReader.DatFiles
{
	public class DatFile
	{
		#region Consts

		private const ulong MagicNumber = 0xBBbbBBbbBBbbBBbb;

		#endregion

		public DatFile(AssetFile assetFile, DatDefinitions datDefinitions)
			: this(assetFile.GetFileContents(), datDefinitions.FileDefinitions.FirstOrDefault(x => x.Name == Path.GetFileName(assetFile.Name)) ?? new FileDefinition($"[DUMMY] {assetFile.Name}"))
		{
		}

		public DatFile(byte[] fileContents, FileDefinition fileDefinition)
		{
			FileDefinition = fileDefinition;
			try
			{
				using MemoryStream memoryStream = new MemoryStream(fileContents);
				using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.Unicode);

				Count = binaryReader.ReadInt32();

				var recordLength = FindRecordLength(binaryReader, Count);
				var dataSectionOffset = 4 + (Count * recordLength);

				binaryReader.BaseStream.Seek(dataSectionOffset, SeekOrigin.Begin);
				if (binaryReader.ReadUInt64() != MagicNumber)
				{
					throw new Exception($"Missing magic number after records.");
				}

				binaryReader.BaseStream.Seek(4, SeekOrigin.Begin);
				var records = new List<DatRecord>(Count);
				for (int i = 0; i < Count; i++)
				{
					Dictionary<string, DatData> values = new Dictionary<string, DatData>();
					for (int j = 0; j < fileDefinition.Fields.Length; j++)
					{
						FieldDefinition fieldDefinition = fileDefinition.Fields[j];
						try
						{
							values[fieldDefinition.ID] = fieldDefinition.DataType.ReadData(binaryReader, dataSectionOffset);
						}
						catch (Exception ex)
						{
							throw new Exception($"Error: Row '{i}' FieldID '{fieldDefinition.ID}' DataType '{fieldDefinition.DataType.Name}',\n Message:{ex.Message}\n Stacktrace: {ex.StackTrace}");
						}
					}
					int remainder = (int)(((4 + (i * recordLength)) + recordLength) - binaryReader.BaseStream.Position);
					if (remainder != 0)
					{
						var pos = binaryReader.BaseStream.Position;
						if (remainder > 0)
						{
							values["_Remainder"] = new DatData(binaryReader.ReadBytes(remainder));

							TryRead("bool", "_RemainderBool");
							TryRead("byte", "_RemainderByte");
							TryRead("int", "_RemainderInt");
							TryRead("uint", "_RemainderUInt");
							TryRead("long", "_RemainderLong");
							TryRead("ulong", "_RemainderULong");
							TryRead("float", "_RemainderFloat");
							TryRead("string_utf8", "_RemainderString");
							TryRead("ref|string_utf8", "_RemainderRefString");
							TryRead("ref|list|ulong", "_RemainderListULong");
							TryRead("ref|list|int", "_RemainderListInt");

							void TryRead(string dataType, string valuesKey)
							{
								binaryReader.BaseStream.Seek(pos, SeekOrigin.Begin);
								try
								{
									values[valuesKey] = TypeDefinition.Parse(dataType, fileDefinition.X64).ReadData(binaryReader, dataSectionOffset);
								}
								catch (Exception)
								{
								}
							}

						}
						binaryReader.BaseStream.Seek(pos + remainder, SeekOrigin.Begin);
					}
					records.Add(new DatRecord(values));
				}

				Records = records;
			}
			catch (Exception ex)
			{
				throw new Exception($"Failed to read {fileDefinition.Name}.", ex);
			}
		}

		private static long FindRecordLength(BinaryReader binaryReader, int entryCount)
		{
			if (entryCount == 0)
			{
				return 0;
			}

			var streamLength = binaryReader.BaseStream.Length;
			for (long i = 0L, offset = binaryReader.BaseStream.Position; binaryReader.BaseStream.Position - offset <= streamLength - 8; i++)
			{
				if (binaryReader.ReadUInt64() == MagicNumber)
				{
					return i;
				}
				binaryReader.BaseStream.Seek(-8 + entryCount, SeekOrigin.Current);
			}
			return 0;
		}

		#region Properties

		public FileDefinition FileDefinition
		{
			get;
		}

		public int Count
		{
			get;
		}

		public IReadOnlyList<DatRecord> Records
		{
			get;
		}

		#endregion
	}
}
