using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PoEAssetReader.DatFiles.Definitions
{
	public abstract class TypeDefinition
	{
		#region Consts

		private const string RefDataTypeName = "ref|";
		private const string ListDataTypeName = "list|";

		private static readonly Dictionary<string, TypeDefinition> TypeDefinitionMapping = (new List<TypeDefinition>()
		{
			new GenericTypeDefinition("bool", bs => bs.ReadBoolean()),
			new GenericTypeDefinition("byte", bs => bs.ReadByte()),
			new GenericTypeDefinition("short", bs => bs.ReadInt16()),
			new GenericTypeDefinition("ushort", bs => bs.ReadUInt16()),
			new GenericTypeDefinition("int", bs => bs.ReadInt32()),
			new GenericTypeDefinition("uint", bs => bs.ReadUInt32()),
			new GenericTypeDefinition("float", bs => bs.ReadSingle()),
			new GenericTypeDefinition("long", bs => bs.ReadInt64()),
			new GenericTypeDefinition("ulong", bs => bs.ReadUInt64()),
			new GenericTypeDefinition("string", bs => {
				var sb = new StringBuilder();
				char ch;
				while ((ch = bs.ReadChar()) != 0)
				{
					sb.Append(ch);
				}
				ch = bs.ReadChar();
				// string should end with int(0)
				if (ch != 0)
				{
					throw new Exception("Not found int(0) value at the end of the string");
				}
				return sb.ToString();
			}),
			new GenericTypeDefinition("ref|generic", bs => bs.ReadInt32()),
		}).ToDictionary(x => x.Name, x => x);

		private static readonly Dictionary<string, TypeDefinition> _types = new Dictionary<string, TypeDefinition>();

		#endregion

		public TypeDefinition(string name)
		{
			Name = name;
		}

		#region Properties

		public string Name
		{
			get;
		}

		#endregion

		#region Public Methods

		public abstract object ReadData(BinaryReader binaryReader, long dataSectionOffset);

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

		#region Private Methods

		private static TypeDefinition ParseValueType(string dataType)
		{
			if(TypeDefinitionMapping.TryGetValue(dataType, out var typeDefinition))
			{
				return typeDefinition;
			}
			throw new Exception($"Missing {nameof(TypeDefinitionMapping)} for '{dataType}'");
		}

		#endregion
	}
}