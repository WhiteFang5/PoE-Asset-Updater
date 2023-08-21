using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PoEAssetReader
{
	/// <summary>
	/// A MurmurHash64A implementation based on:
	/// * https://github.com/SlideWave/Mumurhash264A/blob/master/Murmurhash264A/Murmurhash264A/Murmur2.cs
	/// * https://github.com/zao/ooz/blob/6307b387e45c6b2283d622d82d2713a38e25e503/bun.cpp#L311-L337
	/// * https://github.com/Project-Path-of-Exile-Wiki/PyPoE/pull/113/files#diff-82b1fe47bff7d588880afb974703f2073841fca9c79a558667af5528aaed5c44
	/// </summary>
	internal class MurmurHash
	{
		private const ulong m = 0xc6a4a7935bd1e995;
		private const int r = 47;

		private readonly ulong _seed;

		public MurmurHash(ulong seed)
		{
			_seed = seed;
		}

		public ulong ComputeHash(byte[] data)
		{
			int length = data.Length;
			int currentIndex = 0;
			ulong h = _seed ^ ((ulong)length * m);

			while(length >= 8)
			{
				ulong k = BitConverter.ToUInt64(data, currentIndex);

				k *= m;
				k ^= k >> r;
				k *= m;

				h ^= k;
				h *= m;

				currentIndex += 8;
				length -= 8;
			}

			switch(length)
			{
				case 7:
					h ^= (ulong)data[currentIndex + 6] << 48;
					goto case 6;
				case 6:
					h ^= (ulong)data[currentIndex + 5] << 40;
					goto case 5;
				case 5:
					h ^= (ulong)data[currentIndex + 4] << 32;
					goto case 4;
				case 4:
					h ^= (ulong)data[currentIndex + 3] << 24;
					goto case 3;
				case 3:
					h ^= (ulong)data[currentIndex + 2] << 16;
					goto case 2;
				case 2:
					h ^= (ulong)data[currentIndex + 1] << 8;
					goto case 1;
				case 1:
					h ^= (ulong)data[currentIndex];
					h *= m;
					break;
			}

			h ^= h >> r;
			h *= m;
			h ^= h >> r;

			return h;
		}
	}
}
