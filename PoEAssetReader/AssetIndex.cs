using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PoEAssetReader
{
	public class AssetIndex
	{
		#region Consts

		private const string IndexBinFileName = "_.index.bin";

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

		public AssetFile FindFile(Predicate<AssetFile> predicate)
		{
			return (
				from assetBundle in Bundles
				from assetFile in assetBundle.Files
				where predicate(assetFile)
				select assetFile
			).First();
		}

		public List<AssetFile> FindFiles(Predicate<AssetFile> predicate)
		{
			return (
				from assetBundle in Bundles
				from assetFile in assetBundle.Files
				where predicate(assetFile)
				select assetFile
			).ToList();
		}

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
			uint64 file_name_hash <comment="FVN1a-hash of the lower-case path name with a '++' suffix">;
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

uint32 path_rep_count;
struct path_rep_t
{
    uint32 unk[2];
    uint32 payload_offset;
    uint32 payload_size;
    uint32 unk4;
} path_rep[path_rep_count];

// The file ends in a nested compressed bundle containing
// compact representation of all possible paths.
local int bundle_start = FTell();
ubyte path_rep_bundle[FileSize() - bundle_start];
		*/
		private void ReadIndexFiles()
		{
			byte[] content = AssetBundle.GetBundleContent(Path.Combine(PoEDirectory, IndexBinFileName));

			using MemoryStream stream = new MemoryStream(content);
			using BinaryReader reader = new BinaryReader(stream);

			// Read the bundle info
			int bundleCount = reader.ReadInt32();
			for(int i = 0; i < bundleCount; i++)
			{
				int nameLength = reader.ReadInt32();
				string name = new string(reader.ReadChars(nameLength));
				int uncompressedSize = reader.ReadInt32();

				Bundles.Add(new AssetBundle(PoEDirectory, $"{name}{AssetBundle.FileExtension}"));
			}

			// Read the file info
			int fileCount = reader.ReadInt32();
			List<(int bundleIndex, int offset, int size, long fileNameHash)> files = new List<(int bundleIndex, int offset, int size, long fileNameHash)>(fileCount);
			for(int i = 0; i < fileCount; i++)
			{
				long fileNameHash = reader.ReadInt64();
				int bundleIndex = reader.ReadInt32();
				int offset = reader.ReadInt32();
				int size = reader.ReadInt32();

				//currentBundle.Files.Add(new AssetFile(currentBundle, name, offset, size));
				files.Add((bundleIndex, offset, size, fileNameHash));
			}

			int pathCount = reader.ReadInt32();
			List<(int offset, int size, int unk0, int unk1, int unk2)> pathSections = new List<(int offset, int size, int unk0, int unk1, int unk2)>(pathCount);
			for(int i = 0; i < pathCount; i++)
			{
				// Read unknown values.
				int unk0 = reader.ReadInt32();
				int unk1 = reader.ReadInt32();

				// Read known values.
				int payload_offset = reader.ReadInt32();
				int payload_size = reader.ReadInt32();

				// Read unknown values.
				int unk2 = reader.ReadInt32();

				pathSections.Add((payload_offset, payload_size, unk0, unk1, unk2));
			}

			byte[] pathBundle = AssetBundle.GetBundleContent(reader.ReadBytes((int)(content.Length - reader.BaseStream.Position)));

			List<string> pathNames = new List<string>(pathCount);

			FNV1aHash64 fnv1a = new FNV1aHash64();

			for(int i = 0; i < pathCount; i++)
			{
				var pathSection = pathSections[i];
				var generatedPaths = GeneratePaths(pathBundle.Skip(pathSection.offset).Take(pathSection.size).ToArray());
				pathNames.AddRange(generatedPaths);
			}

			Dictionary<long, string> pathHashes = pathNames.ToDictionary(x => BitConverter.ToInt64(fnv1a.ComputeHash(Encoding.UTF8.GetBytes($"{x.ToLowerInvariant()}++")), 0), x => x);

			AssetBundle currentBundle = null;
			int currentBundleIndex = -1;
			for(int i = 0; i < fileCount; i++)
			{
				var fileInfo = files[i];
				var fileName = pathHashes[fileInfo.fileNameHash];

				if(currentBundleIndex != fileInfo.bundleIndex)
				{
					currentBundle = Bundles[fileInfo.bundleIndex];
				}
				currentBundle.Files.Add(new AssetFile(currentBundle, fileName, fileInfo.offset, fileInfo.size));
			}

			static List<string> GeneratePaths(byte[] section)
			{
				using MemoryStream pathStream = new MemoryStream(section);
				using BinaryReader pathReader = new BinaryReader(pathStream);

				bool basePhase = false;
				List<string> bases = new List<string>();
				List<string> results = new List<string>();
				while(pathReader.BaseStream.Position < pathReader.BaseStream.Length)
				{
					int cmd = pathReader.ReadInt32();
					if(cmd == 0)
					{
						basePhase = !basePhase;
						if(basePhase)
						{
							bases.Clear();
						}
					}
					else
					{
						string path = string.Empty;
						while(pathReader.PeekChar() > 0)
						{
							path += pathReader.ReadChar();
						}
						// Skip the 0-termination byte
						pathReader.ReadByte();

						// the input is one-indexed
						int index = cmd - 1;
						if(index < bases.Count)
						{
							// Prepend the base string
							path = $"{bases[index]}{path}";
						}

						(basePhase ? bases : results).Add(path);
					}
				}
				return results;
			}
		}

		#endregion
	}
}
