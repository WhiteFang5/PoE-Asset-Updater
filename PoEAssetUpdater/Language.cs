namespace PoEAssetUpdater
{
	internal static class Language
	{
		public const string English = "English";

		// Must match the Language enum in PoE Overlay's language.type.ts
		public static readonly string[] All = new string[] {
			English,
			"Portuguese",
			"Russian",
			"Thai",
			"German",
			"French",
			"Spanish",
			"Korean",
			"SimplifiedChinese",
			"TraditionalChinese"
		};
	}
}
