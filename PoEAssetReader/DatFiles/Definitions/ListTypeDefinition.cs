using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class ListTypeDefinition : TypeDefinition
	{
		#region Consts

		private const int ListCutOff = 100;

		#endregion

		#region Variables

		private readonly bool _x64;

		#endregion

		public ListTypeDefinition(string name, TypeDefinition listType, bool x64 = false)
			: base(name, typeof(List<>).MakeGenericType(listType.DataType), (x64 ? sizeof(ulong) : sizeof(uint)) * 2)
		{
			ListType = listType;
			_x64 = x64;
		}

		#region Properties

		public TypeDefinition ListType
		{
			get;
		}

		#endregion

		#region Public Methods

		public override DatData ReadData(BinaryReader binaryReader, long dataSectionOffset)
		{
			string remark = null;
			var count = _x64 ? binaryReader.ReadInt64() : binaryReader.ReadUInt32();
			var pointer = _x64 ? binaryReader.ReadInt64() : binaryReader.ReadUInt32();
			IList list = (IList)Activator.CreateInstance(DataType);
			if(count > 0)
			{
				if(count > ListCutOff)
				{
					remark = $"Only showing first {ListCutOff}/{count} list items";
					count = ListCutOff;
				}
				var dataPos = dataSectionOffset + pointer;
				var streamLength = binaryReader.BaseStream.Length;
				var dataSize = Math.Max(0L, ListType.DataSize);
				if ((dataPos + (dataSize * count)) <= streamLength)
				{
					var oldPos = binaryReader.BaseStream.Position;
					binaryReader.BaseStream.Seek(dataPos, SeekOrigin.Begin);
					for(var i = 0L; i < count; i++)
					{
						DatData data = ListType.ReadData(binaryReader, dataSectionOffset);
						list.Add(data.Value);
						if (!string.IsNullOrEmpty(data.Remark))
						{
							remark = $"{(string.IsNullOrEmpty(remark) ? string.Empty : $"{remark}; ")}Item {i}: {data.Remark}";
						}
					}
					binaryReader.BaseStream.Seek(oldPos, SeekOrigin.Begin);
				}
				else
				{
					list = null;
					remark = $"Invalid data pointer: {dataPos} + ({dataSize} * {count}) (stream length: {streamLength})";
				}
			}
			return new DatData(list, remark);
		}

		#endregion
	}
}
