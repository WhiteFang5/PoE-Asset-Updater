namespace PoEAssetReader.DatFiles.Definitions
{
	public class FieldDefinition
	{
		public FieldDefinition(string id, TypeDefinition dataType)
		{
			ID = id;
			DataType = dataType;
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

		#endregion
	}
}
