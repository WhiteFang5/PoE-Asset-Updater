using System.Text;

namespace PoE2AssetUpdater;

internal static class Logger
{
	#region Variables

	private static readonly StringBuilder _logs = new();

	#endregion

	#region Public Methods

	public static void Write(string message)
	{
		_logs.Append(message);
		Console.Write(message);
	}

	public static void WriteLine(string message)
	{
		_logs.AppendLine(message);
		Console.WriteLine(message);
	}

	public static void SaveLogs(string logFilePath)
	{
		File.WriteAllText(logFilePath, _logs.ToString());
	}

	#endregion
}
