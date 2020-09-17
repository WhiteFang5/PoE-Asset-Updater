using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PoEAssetReader
{
	public class AssetBundle
	{
		#region Consts

		public const string FileExtension = ".bundle.bin";

		private const int SafeSpace = 64;

		private const int MaxChunkSize = 262144;//256k

		#endregion

		#region Variables

		private byte[] _decompressedContent;

		#endregion

		public AssetBundle(string poeDirectory, string name)
		{
			PoEDirectory = poeDirectory;
			Name = name;
		}

		#region Properties

		public string PoEDirectory
		{
			get;
		}

		public string Name
		{
			get;
		}

		public List<AssetFile> Files
		{
			get;
		} = new List<AssetFile>();

		#endregion

		#region Public Methods

		public byte[] GetFileContents(AssetFile assetFile)
		{
			if(assetFile.Bundle != this)
			{
				return assetFile.Bundle.GetFileContents(assetFile);
			}

			if(_decompressedContent == null)
			{
				_decompressedContent = GetBundleContent(Path.Combine(PoEDirectory, Name));
			}

			return _decompressedContent.Skip(assetFile.Offset).Take(assetFile.Size).ToArray();
		}

		/*
		# 010 Editor Template (for PoE bundle.bin files):
uint32 uncompressed_size;
uint32 total_payload_size;
uint32 head_payload_size;
struct head_payload_t {
	enum <uint32> {Kraken_6 = 8, Mermaid_A = 9, Leviathan_C = 13 } first_file_encode;
	uint32 unk03;
	uint64 uncompressed_size2;
	uint64 total_payload_size2;
	uint32 entry_count;
	uint32 unk28[5];
	uint32 entry_sizes[entry_count];
} head;

local int i <hidden=true>;
for (i = 0; i < head.entry_count; ++i) {
	struct entry_t {
		byte data[head.entry_sizes[i]];
	} entry;
}
			*/
		/// <summary>
		/// Returns the decompressed contents of the bundle located at the given <paramref name="bundleFilePath"/>.
		/// </summary>
		public static byte[] GetBundleContent(string bundleFilePath) => GetBundleContent(File.ReadAllBytes(bundleFilePath));

		/// <summary>
		/// Returns the decompressed contents of the bundle contained in the given <paramref name="content"/>.
		/// </summary>
		public static byte[] GetBundleContent(byte[] content)
		{
			using MemoryStream stream = new MemoryStream(content);
			using BinaryReader reader = new BinaryReader(stream);

			int uncompressedSize = reader.ReadInt32();
			int totalPayloadSize = reader.ReadInt32();
			int headPayloadSize = reader.ReadInt32();
			int encoding = reader.ReadInt32();

			// Read some unknown value.
			reader.ReadInt32();

			long uncompressedSizeL = reader.ReadInt64();
			long totalPayloadSizeL = reader.ReadInt64();
			int entryCount = reader.ReadInt32();

			// Read some unknown value.
			reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();
			reader.ReadInt32();

			// Read the entry sizes
			List<int> entrySizes = new List<int>();
			for(int i = 0; i < entryCount; i++)
			{
				entrySizes.Add(reader.ReadInt32());
			}

			// Read and decompress the entry bytes
			byte[] decompressedContent = new byte[uncompressedSize];
			byte[] decompressionBuffer = new byte[MaxChunkSize + SafeSpace];
			int lastEntry = entryCount - 1;
			int offset = 0;
			for(int i = 0; i < entryCount; i++)
			{
				byte[] compressedContent = reader.ReadBytes(entrySizes[i]);

				int decompressedSize = (i == lastEntry) ? (uncompressedSize - (lastEntry * MaxChunkSize)) : MaxChunkSize;

				LibOoz.Ooz_Decompress(compressedContent, compressedContent.Length, decompressionBuffer, decompressedSize);
				Array.Copy(decompressionBuffer, 0, decompressedContent, offset, decompressedSize);
				offset += decompressedSize;
			}
			return decompressedContent;
		}

		#endregion
	}
}
