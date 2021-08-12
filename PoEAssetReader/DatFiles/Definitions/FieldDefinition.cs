namespace PoEAssetReader.DatFiles.Definitions
{
	public class FieldDefinition
	{
		public FieldDefinition(string id, TypeDefinition dataType, string refDatFileName, string refDatFieldID)
		{
			ID = id;
			DataType = dataType;
			RefDatFileName = refDatFileName;
			RefDatFieldID = refDatFieldID;
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

		public string RefDatFieldID
		{
			get;
		}

		#endregion
	}
}
