namespace PoEAssetReader.DatFiles
{
	public readonly struct DatData
	{
		#region Variables

		public readonly object Value;
		public readonly string Remark;

		#endregion

		public DatData(object value) => (Value, Remark) = (value, null);

		public DatData(object value, string remark) => (Value, Remark) = (value, remark);
	}
}
