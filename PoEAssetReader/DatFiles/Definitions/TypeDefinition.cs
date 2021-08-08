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
				TypeDefinitionMapping.Add(name, new GenericTypeDefinition(name, typeof(byte[]), bs => new DatData(bs.ReadBytes(bytesToRead))));
			}
		}

		private const string RefDataTypeName = "ref|";
		private const string ListDataTypeName = "list|";

		private static readonly Dictionary<string, TypeDefinition> TypeDefinitionMapping = (new List<TypeDefinition>()
		{
			new GenericTypeDefinition("bool", typeof(bool), bs => new DatData(bs.ReadBoolean())),
			new GenericTypeDefinition("byte", typeof(byte), bs => new DatData(bs.ReadByte())),
			new GenericTypeDefinition("short", typeof(short), bs => new DatData(bs.ReadInt16())),
			new GenericTypeDefinition("ushort", typeof(ushort), bs => new DatData(bs.ReadUInt16())),
			new GenericTypeDefinition("int", typeof(int), bs => new DatData(bs.ReadInt32())),
			new GenericTypeDefinition("uint", typeof(uint), bs => new DatData(bs.ReadUInt32())),
			new GenericTypeDefinition("float", typeof(float), bs => new DatData(bs.ReadSingle())),
			new GenericTypeDefinition("long", typeof(long), bs => new DatData(bs.ReadInt64())),
			new GenericTypeDefinition("ulong", typeof(ulong), bs => new DatData(bs.ReadUInt64())),
			new GenericTypeDefinition("string", typeof(string), bs => {
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
			new GenericTypeDefinition("ref|generic", typeof(int), bs => new DatData(bs.ReadInt32())),
		}).ToDictionary(x => x.Name, x => x);

		private static readonly Dictionary<string, TypeDefinition> _types = new Dictionary<string, TypeDefinition>();

		#endregion

		public TypeDefinition(string name, Type dataType)
		{
			Name = name;
			DataType = dataType;
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

		#endregion

		#region Public Methods

		public abstract DatData ReadData(BinaryReader binaryReader, long dataSectionOffset);

		public static TypeDefinition Parse(string dataType)
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
						typeDefinition = new ListTypeDefinition(dataType, Parse(subDataType[ListDataTypeName.Length..]));
					}
					else
					{
						typeDefinition = new RefTypeDefinition(dataType, Parse(subDataType));
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
