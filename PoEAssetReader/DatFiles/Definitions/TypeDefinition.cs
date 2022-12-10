using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PoEAssetReader.DatFiles.Definitions
{
	public abstract class TypeDefinition
	{
		#region Consts

		static TypeDefinition()
		{
			for(int i = 1; i <= 100; i++)
			{
				string name = $"byte[{i.ToString(CultureInfo.InvariantCulture)}]";
				int bytesToRead = i; // Explicitly capture the variable!
				TypeDefinitionMapping.Add(name, new GenericTypeDefinition(name, typeof(byte[]), i, bs => new DatData(bs.ReadBytes(bytesToRead))));
			}
		}

		private const string RefDataTypeName = "ref|";
		private const string ListDataTypeName = "list|";

		private static readonly Dictionary<string, TypeDefinition> TypeDefinitionMapping = new List<TypeDefinition>()
		{
			new GenericTypeDefinition("bool", typeof(bool), sizeof(bool), bs => new DatData(bs.ReadBoolean())),
			new GenericTypeDefinition("byte", typeof(byte), sizeof(byte), bs => new DatData(bs.ReadByte())),
			new GenericTypeDefinition("short", typeof(short), sizeof(short), bs => new DatData(bs.ReadInt16())),
			new GenericTypeDefinition("ushort", typeof(ushort), sizeof(ushort), bs => new DatData(bs.ReadUInt16())),
			new GenericTypeDefinition("int", typeof(int), sizeof(int), bs => new DatData(bs.ReadInt32())),
			new GenericTypeDefinition("uint", typeof(uint), sizeof(uint), bs => new DatData(bs.ReadUInt32())),
			new GenericTypeDefinition("float", typeof(float), sizeof(float), bs => new DatData(bs.ReadSingle())),
			new GenericTypeDefinition("long", typeof(long), sizeof(long), bs => new DatData(bs.ReadInt64())),
			new GenericTypeDefinition("ulong", typeof(ulong), sizeof(ulong), bs => new DatData(bs.ReadUInt64())),
			new GenericTypeDefinition("int128", typeof(Int128), sizeof(ulong) * 2, bs =>
			{
				// Stored as little endian
				ulong first = bs.ReadUInt64();
				ulong second = bs.ReadUInt64();
				return new DatData(new Int128(second, first));
			}),
			new GenericTypeDefinition("uint128", typeof(UInt128), sizeof(ulong) * 2, bs =>
			{
				// Stored as little endian
				ulong first = bs.ReadUInt64();
				ulong second = bs.ReadUInt64();
				return new DatData(new UInt128(second, first));
			}),
			new GenericTypeDefinition("string", typeof(string), -1, bs =>
			{
				var oldPos = bs.BaseStream.Position;
				var sb = new StringBuilder();
				int ch;
				while ((ch = bs.PeekChar()) > 0)
				{
					sb.Append(bs.ReadChar());
				}
				// string should end with int(0)
				if (ch != 0)
				{
					bs.BaseStream.Seek(oldPos, SeekOrigin.Begin);
					return new DatData("[ERROR: Could not read string!]", $"pointer: {(bs.PeekChar() != -1 ? bs.ReadInt32() : -1)}");
				}
				return new DatData(sb.ToString());
			}),
			new GenericTypeDefinition("string_utf8", typeof(string), -1, bs =>
			{
				var oldPos = bs.BaseStream.Position;
				var sb = new StringBuilder();
				bool eos = false;
				while (!eos) {
					byte[] bytes = bs.ReadBytes(2);
					if(bytes.Any(x => x != 0))
					{
						sb.Append(Encoding.UTF8.GetString(bytes));
						if(bs.BaseStream.Position == bs.BaseStream.Length)
						{
							eos = true;
						}
					}
					else
					{
						break;
					}
				}
				// string should end with int(0)
				if (eos)
				{
					bs.BaseStream.Seek(oldPos, SeekOrigin.Begin);
					return new DatData("[ERROR: Could not read string!]", $"pointer: {(bs.PeekChar() != -1 ? bs.ReadInt32() : -1)}");
				}
				return new DatData(sb.ToString());
			}),
			new GenericTypeDefinition("ref|generic", typeof(int), sizeof(int), bs => new DatData(bs.ReadInt32())),
		}.ToDictionary(x => x.Name, x => x);

		private static readonly Dictionary<string, TypeDefinition> _types = new Dictionary<string, TypeDefinition>();

		#endregion

		public TypeDefinition(string name, Type dataType, int dataSize)
		{
			Name = name;
			DataType = dataType;
			DataSize = dataSize;
		}

		#region Properties

		public string Name
		{
			get;
		}

		public Type DataType
		{
			get;
		}

		public long DataSize
		{
			get;
		}

		#endregion

		#region Public Methods

		public abstract DatData ReadData(BinaryReader binaryReader, long dataSectionOffset);

		public static TypeDefinition Parse(string dataType, bool x64)
		{
			if(_types.TryGetValue(dataType, out TypeDefinition typeDefinition))
			{
				return typeDefinition;
			}

			if(!TypeDefinitionMapping.TryGetValue(dataType, out typeDefinition))
			{
				if(dataType.StartsWith(RefDataTypeName))
				{
					var subDataType = dataType[RefDataTypeName.Length..];
					if(subDataType.StartsWith(ListDataTypeName))
					{
						typeDefinition = new ListTypeDefinition(dataType, Parse(subDataType[ListDataTypeName.Length..], x64), x64);
					}
					else
					{
						typeDefinition = new RefTypeDefinition(dataType, Parse(subDataType, x64), x64);
					}
				}
				else
				{
					throw new Exception($"Missing {nameof(TypeDefinitionMapping)} for '{dataType}'");
				}
			}

			if(typeDefinition != null)
			{
				_types[dataType] = typeDefinition;
			}
			return typeDefinition;
		}

		#endregion
	}
}
