namespace PoEAssetReader
{
	public class AssetFile
	{
		public AssetFile(AssetBundle bundle, string name, int offset, int size)
		{
			Bundle = bundle;
			Name = name;
			Offset = offset;
			Size = size;
		}

		#region Properties

		public AssetBundle Bundle
		{
			get;
		}

		/// <summary>
		/// The full file name, including directory and extension.
		/// </summary>
		public string Name
		{
			get;
		}

		public int Offset
		{
			get;
		}

		public int Size
		{
			get;
		}

		#endregion

		#region Public Methods

		public byte[] GetFileContents() => Bundle.GetFileContents(this);

		#endregion
	}
}
