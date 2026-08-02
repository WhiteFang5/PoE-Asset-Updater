using System;

namespace PoEAssetReader
{
	/// <summary>
	/// A MurmurHash32 implementation based on:
	/// <para>
	/// * https://github.com/SlideWave/Mumurhash264A/blob/master/Murmurhash264A/Murmurhash264A/Murmur2.cs
	/// </para>
	/// <para>
	/// * https://github.com/zao/ooz/blob/6307b387e45c6b2283d622d82d2713a38e25e503/bun.cpp#L311-L337
	/// </para>
	/// <para>
	/// * https://github.com/Project-Path-of-Exile-Wiki/PyPoE/pull/113/files#diff-82b1fe47bff7d588880afb974703f2073841fca9c79a558667af5528aaed5c44
	/// </para>
	/// </summary>
	public sealed class MurmurHash32
	{
		private const uint m = 0x5bd1e995;
		private const int r = 24;

		private readonly uint _seed;

		public MurmurHash32(uint seed)
		{
			_seed = seed;
		}

		public uint ComputeHash(byte[] data)
		{
			uint length = (uint)data.Length;
			int currentIndex = 0;
			uint h = _seed ^ length;

			while(length >= 4)
			{
				uint k = BitConverter.ToUInt32(data, currentIndex);

				k *= m;
				k ^= k >> r;
				k *= m;

				h *= m;
				h ^= k;

				currentIndex += 4;
				length -= 4;
			}

			switch(length)
			{
				case 3:
					h ^= (uint)data[currentIndex + 2] << 16;
					goto case 2;
				case 2:
					h ^= (uint)data[currentIndex + 1] << 8;
					goto case 1;
				case 1:
					h ^= (uint)data[currentIndex];
					h *= m;
					break;
			}

			h ^= h >> 13;
			h *= m;
			h ^= h >> 15;

			return h;
		}
	}
}
