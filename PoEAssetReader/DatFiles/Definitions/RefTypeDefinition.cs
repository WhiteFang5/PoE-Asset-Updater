using System;
using System.IO;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class RefTypeDefinition : TypeDefinition
	{
		public RefTypeDefinition(string name, TypeDefinition refType)
			: base(name, refType.DataType)
		{
			RefType = refType;
		}

		#region Properties

		public TypeDefinition RefType
		{
			get;
		}

		#endregion

		#region Public Methods

		public override object ReadData(BinaryReader binaryReader, long dataSectionOffset)
		{
			var pointer = binaryReader.ReadUInt32();
			var oldPos = binaryReader.BaseStream.Position;
			binaryReader.BaseStream.Seek(Math.Min(binaryReader.BaseStream.Length, dataSectionOffset + pointer), SeekOrigin.Begin);
			var value = RefType.ReadData(binaryReader, dataSectionOffset);
			binaryReader.BaseStream.Seek(oldPos, SeekOrigin.Begin);
			return value;
		}

		#endregion
	}
}
