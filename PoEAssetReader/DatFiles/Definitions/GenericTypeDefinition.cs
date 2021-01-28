using System;
using System.IO;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class GenericTypeDefinition : TypeDefinition
	{
		#region Variables

		private readonly ReadDataDelegate _readData;

		#endregion

		public GenericTypeDefinition(string name, Type dataType, ReadDataDelegate readData)
			: base(name, dataType)
		{
			_readData = readData;
		}

		#region Public Methods

		public delegate object ReadDataDelegate(BinaryReader binaryReader);

		public override object ReadData(BinaryReader binaryReader, long dataSectionOffset) => _readData(binaryReader);

		#endregion
	}
}
