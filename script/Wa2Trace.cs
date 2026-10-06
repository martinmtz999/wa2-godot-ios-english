using Godot;
using System;
using System.Globalization;
using System.IO;

// Event trace for comparing the port with the PC game (research/05-pc-comparison.md).
// Off unless WA2_TRACE=<file> is set or the game is run with `-- --trace=<file>`.
// One event per line: <ms since start>\t<event>\t<fields...>.
public static class Wa2Trace
{
	private static StreamWriter _w;
	private static bool _init;
	public static bool Enabled => _w != null;

	public static void Init()
	{
		if (_init) return;
		_init = true;
		string path = OS.GetEnvironment("WA2_TRACE");
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("--trace=")) path = a["--trace=".Length..];
		if (string.IsNullOrEmpty(path)) return;
		try
		{
			_w = new StreamWriter(path, false) { AutoFlush = true };
			Log("start", OS.GetName(), Engine.GetVersionInfo()["string"]);
		}
		catch (Exception e)
		{
			GD.PrintErr("Wa2Trace: " + e.Message);
		}
	}

	public static void Log(string ev, params object[] fields)
	{
		if (_w == null) return;
		_w.Write(Time.GetTicksMsec());
		_w.Write('\t');
		_w.Write(ev);
		foreach (object f in fields)
		{
			_w.Write('\t');
			string s = f switch
			{
				float x => x.ToString(CultureInfo.InvariantCulture),
				double x => x.ToString(CultureInfo.InvariantCulture),
				string x => x.Replace('\t', ' ').Replace("\n", "\\n"),
				null => "",
				_ => f.ToString()
			};
			_w.Write(s);
		}
		_w.Write('\n');
	}
}
