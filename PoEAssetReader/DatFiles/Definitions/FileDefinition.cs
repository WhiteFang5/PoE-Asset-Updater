namespace PoEAssetReader.DatFiles.Definitions
{
	public class FileDefinition
	{
		public FileDefinition(string name, params FieldDefinition[] fields)
		{
			Name = name;
			Fields = fields;
		}

		#region Properties

		public string Name
		{
			get;
		}

		public FieldDefinition[] Fields
		{
			get;
		} 

		#endregion
	}
}
