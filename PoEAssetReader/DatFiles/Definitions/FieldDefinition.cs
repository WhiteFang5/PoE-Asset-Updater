namespace PoEAssetReader.DatFiles.Definitions
{
	public class FieldDefinition
	{
		public FieldDefinition(string id, TypeDefinition dataType, string refDatFileName)
		{
			ID = id;
			DataType = dataType;
			RefDatFileName = refDatFileName;
		}

		#region Properties

		public string ID
		{
			get;
		}

		public TypeDefinition DataType
		{
			get;
		}

		public string RefDatFileName
		{
			get;
		}

		#endregion
	}
}
