using LibDat;
using System.Linq;

namespace PoEAssetUpdater
{
	internal static class RecordDataExtensionMethods
	{
		#region Public Methods

		public static string GetDataValueStringByFieldId(this RecordData recordData, string fieldId) => recordData.FieldsData.First(x => x.FieldInfo.Id == fieldId).Data.GetValueString();

		#endregion
	}
}
