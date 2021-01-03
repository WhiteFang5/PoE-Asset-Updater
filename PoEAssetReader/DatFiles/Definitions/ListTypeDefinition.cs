using System.Collections.Generic;
using System.IO;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class ListTypeDefinition : TypeDefinition
	{
		public ListTypeDefinition(string name, TypeDefinition listType)
			: base(name)
		{
			ListType = listType;
		}

		#region Properties

		public TypeDefinition ListType
		{
			get;
		}

		#endregion

		#region Public Methods

		public override object ReadData(BinaryReader binaryReader, long dataSectionOffset)
		{
			var count = binaryReader.ReadUInt32();
			var pointer = binaryReader.ReadUInt32();
			List<object> list = new List<object>();
			if(count > 0)
			{
				var oldPos = binaryReader.BaseStream.Position;
				binaryReader.BaseStream.Seek(dataSectionOffset + pointer, SeekOrigin.Begin);
				for(int i = 0; i < count; i++)
				{
					list.Add(ListType.ReadData(binaryReader, dataSectionOffset));
				}
				binaryReader.BaseStream.Seek(oldPos, SeekOrigin.Begin);
			}
			return list;
		}

		#endregion
	}
}
