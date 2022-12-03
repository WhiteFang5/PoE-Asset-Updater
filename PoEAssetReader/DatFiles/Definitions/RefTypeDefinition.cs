using System;
using System.IO;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class RefTypeDefinition : TypeDefinition
	{
		#region Variables

		private readonly bool _x64;

		#endregion

		public RefTypeDefinition(string name, TypeDefinition refType, bool x64 = false)
			: base(name, refType.DataType, sizeof(uint))
		{
			RefType = refType;
			_x64 = x64;
		}

		#region Properties

		public TypeDefinition RefType
		{
			get;
		}

		#endregion

		#region Public Methods

		public override DatData ReadData(BinaryReader binaryReader, long dataSectionOffset)
		{
			var pointer = _x64 ? binaryReader.ReadInt64() : binaryReader.ReadUInt32();
			var oldPos = binaryReader.BaseStream.Position;
			binaryReader.BaseStream.Seek(Math.Min(binaryReader.BaseStream.Length, dataSectionOffset + pointer), SeekOrigin.Begin);
			var value = RefType.ReadData(binaryReader, dataSectionOffset);
			binaryReader.BaseStream.Seek(oldPos, SeekOrigin.Begin);
			return value;
		}

		#endregion
	}
}
