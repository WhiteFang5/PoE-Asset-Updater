using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PoEAssetReader
{
	public class AssetIndex
	{
		#region Consts

		private const string IndexBinFileName = "_.index.bin";
		private const string IndexTxtFileName = "_.index.txt";

		#endregion

		public AssetIndex(string poeDirectory)
		{
			PoEDirectory = poeDirectory;
			ReadIndexFiles();
		}

		#region Properties

		public string PoEDirectory
		{
			get;
		}

		public List<AssetBundle> Bundles
		{
			get;
		} = new List<AssetBundle>();

		#endregion

		#region Public Methods

		public byte[] GetFileContents(AssetFile assetFile) => assetFile.Bundle.GetFileContents(assetFile);

		#endregion

		#region Private Methods

		/*
		# 010 Editor Template for PoE index.bin:
uint32 bundle_count;
struct bundles_t
{
local int i;
for (i = 0; i < bundle_count; ++i) {
	struct {
		uint32 name_length;
		char name[name_length];
		uint32 bundle_uncompressed_size;
	} bundle_info;
}
} bundles;

uint32 file_count;
struct files_t
{
local int i;
for (i = 0; i < file_count; ++i) {
	struct {
		uint32 unk[2];
		uint32 bundle_index <comment=BundleIndexComment>;
		uint32 file_offset;
		uint32 file_size;
	} file_info;
}
} files;

string BundleIndexComment(int bundle_index)
{
return bundles.bundle_info[bundle_index].name;
}
		*/
		private void ReadIndexFiles()
		{
			string[] indexTxt = File.ReadAllLines(Path.Combine(PoEDirectory, IndexTxtFileName));

			byte[] content = AssetBundle.GetBundleContent(Path.Combine(PoEDirectory, IndexBinFileName));

			using MemoryStream stream = new MemoryStream(content);
			using BinaryReader reader = new BinaryReader(stream);

			// Read the bundle info
			int bundleCount = reader.ReadInt32();
			HashSet<string> bundleNames = new HashSet<string>(bundleCount);
			for(int i = 0; i < bundleCount; i++)
			{
				int nameLength = reader.ReadInt32();
				string name = new string(reader.ReadChars(nameLength));
				int uncompressedSize = reader.ReadInt32();

				Bundles.Add(new AssetBundle(PoEDirectory, $"{name}{AssetBundle.FileExtension}"));
				bundleNames.Add(name);
			}

			// Map the files to their respective bundles
			Dictionary<string, List<string>> bundleFiles = new Dictionary<string, List<string>>();
			string lastBundleName = null;
			for(int i = 0; i < indexTxt.Length; i++)
			{
				string name = indexTxt[i];
				if(bundleNames.Contains(name))
				{
					lastBundleName = $"{name}{AssetBundle.FileExtension}";
					bundleFiles.Add(lastBundleName, new List<string>());
				}
				else
				{
					bundleFiles[lastBundleName].Add(name);
				}
			}

			// Read the file info
			int fileCount = reader.ReadInt32();
			int lastBundleIndex = -1;
			AssetBundle currentBundle = null;
			List<string> currentBundleFilesList = null;
			for(int i = 0; i < fileCount; i++)
			{
				// Read unknown values.
				reader.ReadInt32();
				reader.ReadInt32();

				// Read known values.
				int bundleIndex = reader.ReadInt32();
				int offset = reader.ReadInt32();
				int size = reader.ReadInt32();

				// Find the file name
				if(lastBundleIndex != bundleIndex)
				{
					currentBundle = Bundles[bundleIndex];
					currentBundleFilesList = bundleFiles[currentBundle.Name];
					lastBundleIndex = bundleIndex;
				}
				string name = currentBundleFilesList.Count > currentBundle.Files.Count ? currentBundleFilesList[currentBundle.Files.Count] : $"--MISSING FILE NAME FOR INDEX {currentBundle.Files.Count}";

				currentBundle.Files.Add(new AssetFile(currentBundle, name, offset, size));
			}
		}

		#endregion
	}
}
