using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PoEAssetReader.DatFiles
{
	public class DatRecord
	{
		public DatRecord(Dictionary<string, DatData> values)
		{
			Values = values;
		}

		#region Properties

		public IReadOnlyDictionary<string, DatData> Values
		{
			get;
		}

		#endregion

		#region Public Methods

		public T GetValue<T>(string key)
		{
			if(!Values.TryGetValue(key, out DatData data))
			{
				throw new KeyNotFoundException($"Key '{key}' was not found.");
			}
			if(data.Value is T value)
			{
				return value;
			}
			throw new Exception($"Value Type Mismatch. Expected '{data.GetType().Name}', provided '{typeof(T)}'");
		}

		public bool HasValue(string key)
		{
			return Values.ContainsKey(key);
		}

		public bool TryGetValue<T>(string key, out T value)
		{
			if(Values.TryGetValue(key, out DatData data) && data.Value is T val)
			{
				value = val;
				return true;
			}
			value = default;
			return false;
		}

		public string GetStringValue(string key)
		{
			if (Values.TryGetValue(key, out DatData data))
			{
				object value = data.Value;
				if (value == null)
				{
					return "NULL";
				}
				else
				{
					var type = value.GetType();
					if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && value is IEnumerable enumerable)
					{
						return $"[{string.Join(",", enumerable.Cast<object>())}]";
					}
					else if (value is byte[] byteArray)
					{
						return string.Join(" ", byteArray);
					}
					return value.ToString();
				}
			}
			return $"Missing Key '{key}'";
		}

		public string GetRemark(string key)
		{
			if (Values.TryGetValue(key, out DatData data))
			{
				return data.Remark;
			}
			return $"Missing Key '{key}'";
		}

		#endregion
	}
}
