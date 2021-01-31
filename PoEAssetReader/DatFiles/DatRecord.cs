using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoEAssetReader.DatFiles
{
	public class DatRecord
	{
		public DatRecord(Dictionary<string, object> values)
		{
			Values = values;
		}

		#region Properties

		public IReadOnlyDictionary<string, object> Values
		{
			get;
		}

		#endregion

		#region Public Methods

		public T GetValue<T>(string key)
		{
			if(!Values.TryGetValue(key, out object objValue))
			{
				throw new KeyNotFoundException($"Key '{key}' was not found.");
			}
			if(objValue is T value)
			{
				return value;
			}
			throw new Exception($"Value Type Mismatch. Expected '{objValue.GetType().Name}', provided '{typeof(T)}'");
		}

		public bool TryGetValue<T>(string key, out T value)
		{
			if(Values.TryGetValue(key, out object objValue) && objValue is T val)
			{
				value = val;
				return true;
			}
			value = default;
			return false;
		}

		public string GetStringValue(string key)
		{
			if(Values.TryGetValue(key, out object objValue))
			{
				var type = objValue.GetType();
				if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && objValue is IEnumerable enumerable)
				{
					return $"[{string.Join(",", enumerable.Cast<object>())}]";
				}
				else if(objValue is byte[] byteArray)
				{
					return string.Join(" ", byteArray);
				}
				return objValue.ToString();
			}
			return $"Missing Key '{key}'";
		}

		#endregion
	}
}
