using System;
using System.Security.Cryptography;

namespace PoEAssetReader
{
	internal abstract class FNVHash : HashAlgorithm
	{
		protected const uint FNV32_PRIME = 16777619;
		protected const uint FNV32_OFFSETBASIS = 2166136261;

		protected const ulong FNV64_PRIME = 1099511628211;
		protected const ulong FNV64_OFFSETBASIS = 14695981039346656037;

		public FNVHash(int hashSize)
		{
			this.HashSizeValue = hashSize;
			this.Initialize();
		}
	}

	internal class FNV1Hash32 : FNVHash
	{
		private uint _hash;

		public FNV1Hash32()
			: base(32) { }

		public override void Initialize()
		{
			_hash = FNV32_OFFSETBASIS;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize)
		{
			for(int i = 0; i < cbSize; i++)
				_hash = (_hash * FNV32_PRIME) ^ array[ibStart + i];
		}

		protected override byte[] HashFinal()
		{
			return BitConverter.GetBytes(_hash);
		}
	}

	internal class FNV1Hash64 : FNVHash
	{
		private ulong _hash;

		public FNV1Hash64()
			: base(64) { }

		public override void Initialize()
		{
			_hash = FNV64_OFFSETBASIS;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize)
		{
			for(int i = 0; i < cbSize; i++)
				_hash = (_hash * FNV64_PRIME) ^ array[ibStart + i];
		}

		protected override byte[] HashFinal()
		{
			return BitConverter.GetBytes(_hash);
		}
	}

	internal class FNV1aHash32 : FNVHash
	{
		private uint _hash;

		public FNV1aHash32()
			: base(32) { }

		public override void Initialize()
		{
			_hash = FNV32_OFFSETBASIS;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize)
		{
			for(int i = 0; i < cbSize; i++)
				_hash = (_hash ^ array[ibStart + i]) * FNV32_PRIME;
		}

		protected override byte[] HashFinal()
		{
			return BitConverter.GetBytes(_hash);
		}
	}

	internal class FNV1aHash64 : FNVHash
	{
		private ulong _hash;

		public FNV1aHash64()
			: base(64) { }

		public override void Initialize()
		{
			_hash = FNV64_OFFSETBASIS;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize)
		{
			for(int i = 0; i < cbSize; i++)
				_hash = (_hash ^ array[ibStart + i]) * FNV64_PRIME;
		}

		protected override byte[] HashFinal()
		{
			return BitConverter.GetBytes(_hash);
		}
	}
}
