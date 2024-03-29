using System;
using System.IO;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class GenericTypeDefinition : TypeDefinition
	{
		#region Variables

		private readonly ReadDataDelegate _readData;

		#endregion

		public GenericTypeDefinition(string name, Type dataType, int dataSize, ReadDataDelegate readData)
			: base(name, dataType, dataSize)
		{
			_readData = readData;
		}

		#region Public Methods

		public delegate DatData ReadDataDelegate(BinaryReader binaryReader);

		public override DatData ReadData(BinaryReader binaryReader, long dataSectionOffset) => _readData(binaryReader);

		#endregion
	}
}
