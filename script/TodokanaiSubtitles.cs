// English subtitles for voice-only audio, ported from the Todokanai TL subtitle patch
// (https://github.com/TodokanaiTL/subtitles, MIT, Copyright (c) 2021 Ben <Avuxo>): its d3d9.dll
// draws these on the PC. Reads the user's own `todokanai/subtitles` and `todokanai/font.png`
// from the resource folder. Behaviour follows SubParser.cpp, SubContext.cpp and TextRenderer.cpp.
using System.Collections.Generic;
using System.Globalization;
using Godot;

public partial class TodokanaiSubtitles : Node2D
{
	private record struct Glyph(int X, int Y, int W, int H, int TopAdjust);
	private class Line
	{
		public ulong Start, End;
		public string Text;
	}
	private class Track
	{
		public int TriggerFile, Trigger, EndFile, EndLine;
		public List<Line> Lines = new();
	}

	private const char LineBreak = '^';
	private const int ScreenWidth = 1280;
	private const int CharWidth = 20;

	private readonly List<Track> _tracks = new();
	private Texture2D _font;
	private Track _playing;
	private int _lineIndex;
	private ulong _startTicks;

	public bool Loaded => _font != null && _tracks.Count > 0;

	public void Load(string dir)
	{
		Image img = Image.LoadFromFile(dir.PathJoin("font.png"));
		if (img != null)
			_font = ImageTexture.CreateFromImage(img);
		using var file = FileAccess.Open(dir.PathJoin("subtitles"), FileAccess.ModeFlags.Read);
		if (file == null)
			return;
		string[] lines = file.GetAsText().Replace("\r", "").Split('\n');
		for (int i = 0; i < lines.Length; i++)
		{
			if (lines[i].StartsWith('{'))
				_tracks.Add(ParseTrack(lines, ref i));
		}
	}

	private static Track ParseTrack(string[] lines, ref int i)
	{
		i++;
		var head = lines[i].Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		Track t = new()
		{
			TriggerFile = int.Parse(head[0], CultureInfo.InvariantCulture),
			Trigger = int.Parse(head[1], CultureInfo.InvariantCulture),
			EndFile = int.Parse(head[2], CultureInfo.InvariantCulture),
			EndLine = int.Parse(head[3], CultureInfo.InvariantCulture),
		};
		for (i++; i < lines.Length && !lines[i].StartsWith('}'); i++)
		{
			var parts = lines[i].Split(' ', 3);
			if (parts.Length < 3)
				continue;
			ulong start = ulong.Parse(parts[0], CultureInfo.InvariantCulture);
			ulong length = ulong.Parse(parts[1], CultureInfo.InvariantCulture);
			t.Lines.Add(new Line { Start = start, End = start + length, Text = parts[2] });
		}
		return t;
	}

	// The PC hooks report the script as a number; split English parts like "1006_2" count as 1006.
	private static int CurrentFile()
	{
		string name = Wa2EngineMain.Engine.Script?.ScriptName ?? "";
		int n = 0;
		foreach (char c in name)
		{
			if (!char.IsDigit(c))
				break;
			n = n * 10 + (c - '0');
		}
		return n;
	}

	public void OnSoundEffect(int id) => CheckForTrigger(id, false);
	public void OnVoice(int id) => CheckForTrigger(id, true);

	// Called on every new message line (the PC's setLineHook).
	public void CheckForCutoff()
	{
		if (_playing != null && _playing.EndFile == CurrentFile() && _playing.EndLine <= Wa2EngineMain.Engine.CurMessageIdx)
			Stop();
	}

	private void CheckForTrigger(int id, bool voice)
	{
		if (!Loaded)
			return;
		CheckForCutoff();
		if (_playing != null)
			return;
		int file = CurrentFile();
		foreach (Track t in _tracks)
		{
			bool match = voice
				? t.TriggerFile == file && t.Trigger == id
				: t.Trigger == id && (t.TriggerFile == 0 || t.TriggerFile == file);
			if (match)
			{
				_playing = t;
				_lineIndex = 0;
				_startTicks = Time.GetTicksMsec();
				return;
			}
		}
	}

	private void Stop()
	{
		_playing = null;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_playing == null)
			return;
		ulong ticks = Time.GetTicksMsec() - _startTicks;
		var lines = _playing.Lines;
		if (_lineIndex < lines.Count && ticks > lines[_lineIndex].End)
		{
			if (_lineIndex < lines.Count - 1)
				_lineIndex++;
			else
				_playing = null;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_playing == null || _font == null || _lineIndex >= _playing.Lines.Count)
			return;
		Line line = _playing.Lines[_lineIndex];
		ulong ticks = Time.GetTicksMsec() - _startTicks;
		if (ticks > line.Start && ticks < line.End)
			RenderText(line.Text);
	}

	private static int XOffset(string s, int from)
	{
		int len = 0;
		while (from + len < s.Length && s[from + len] != LineBreak)
			len++;
		return (ScreenWidth - len * CharWidth) / 2;
	}

	private void RenderText(string s)
	{
		float x = XOffset(s, 0);
		int lineOffset = 0;
		for (int i = 0; i < s.Length; i++)
		{
			if (s[i] < 0x20 || s[i] >= 0x7f)
				continue;
			if (s[i] == LineBreak)
			{
				i++;
				lineOffset += 35;
				x = XOffset(s, i);
				if (i >= s.Length)
					break;
			}
			if (!Glyphs.TryGetValue(s[i], out Glyph g))
				continue;
			DrawTextureRectRegion(_font, new Rect2(x, 50 + g.TopAdjust + lineOffset, g.W, g.H), new Rect2(g.X, g.Y, g.W, g.H));
			x += g.W - 2;
		}
	}

	// font_atlas.h from the Todokanai subtitle patch (x, y, width, height, top adjust). Its '{' and '}'
	// slots hold padding copies of '[' and ']' that the PC skips, so those two characters are not drawn.
	private static readonly Dictionary<char, Glyph> Glyphs = new()
	{
		{ ' ', new(17, 150, 19, 3, 0) },
		{ '!', new(19, 68, 15, 30, 0) },
		{ '"', new(355, 127, 17, 15, 0) },
		{ '#', new(252, 98, 23, 26, 0) },
		{ '$', new(209, 0, 24, 35, 0) },
		{ '%', new(75, 38, 32, 30, 0) },
		{ '&', new(138, 38, 30, 30, 0) },
		{ '\'', new(372, 127, 11, 15, 0) },
		{ '(', new(169, 0, 20, 36, 0) },
		{ ')', new(189, 0, 20, 36, 0) },
		{ '*', new(278, 127, 19, 18, 0) },
		{ '+', new(48, 127, 23, 23, 0) },
		{ ',', new(319, 127, 13, 16, 20) },
		{ '-', new(0, 150, 17, 9, 12) },
		{ '.', new(419, 127, 11, 11, 20) },
		{ '/', new(28, 0, 25, 37, 0) },
		{ '0', new(275, 98, 24, 25, 6) },
		{ '1', new(364, 98, 18, 24, 6) },
		{ '2', new(299, 98, 23, 24, 6) },
		{ '3', new(336, 38, 24, 30, 6) },
		{ '4', new(360, 38, 24, 30, 6) },
		{ '5', new(80, 98, 25, 29, 6) },
		{ '6', new(408, 38, 23, 30, 6) },
		{ '7', new(105, 98, 25, 29, 6) },
		{ '8', new(384, 38, 24, 30, 6) },
		{ '9', new(431, 38, 23, 30, 6) },
		{ ':', new(222, 127, 14, 23, 6) },
		{ ';', new(220, 98, 15, 28, 6) },
		{ '<', new(322, 98, 21, 24, 0) },
		{ '=', new(332, 127, 23, 15, 0) },
		{ '>', new(343, 98, 21, 24, 0) },
		{ '?', new(0, 68, 19, 30, 0) },
		{ '@', new(276, 0, 31, 33, 0) },
		{ 'A', new(206, 68, 30, 29, 0) },
		{ 'B', new(353, 68, 28, 29, 0) },
		{ 'C', new(256, 38, 28, 30, 0) },
		{ 'D', new(175, 68, 31, 29, 0) },
		{ 'E', new(266, 68, 29, 29, 0) },
		{ 'F', new(295, 68, 29, 29, 0) },
		{ 'G', new(198, 38, 29, 30, 0) },
		{ 'H', new(74, 68, 35, 29, 0) },
		{ 'I', new(176, 98, 22, 29, 2) },
		{ 'J', new(284, 38, 26, 30, 0) },
		{ 'K', new(143, 68, 32, 29, 0) },
		{ 'L', new(54, 98, 26, 29, 0) },
		{ 'M', new(34, 68, 40, 29, 0) },
		{ 'N', new(39, 38, 36, 30, 0) },
		{ 'O', new(227, 38, 29, 30, 0) },
		{ 'P', new(381, 68, 28, 29, 0) },
		{ 'Q', new(96, 0, 29, 36, 0) },
		{ 'R', new(324, 68, 29, 29, 0) },
		{ 'S', new(310, 38, 26, 30, 0) },
		{ 'T', new(0, 98, 27, 29, 0) },
		{ 'U', new(107, 38, 31, 30, 0) },
		{ 'V', new(168, 38, 30, 30, 0) },
		{ 'W', new(0, 38, 39, 30, 0) },
		{ 'X', new(109, 68, 34, 29, 0) },
		{ 'Y', new(409, 68, 28, 29, 0) },
		{ 'Z', new(236, 68, 30, 29, 0) },
		{ '[', new(233, 0, 22, 35, 0) },
		{ '\\', new(75, 0, 12, 37, 0) },
		{ ']', new(255, 0, 21, 35, 0) },
		{ '^', new(297, 127, 22, 17, 0) },
		{ '_', new(430, 127, 28, 9, 0) },
		{ '`', new(383, 127, 13, 14, 0) },
		{ 'a', new(71, 127, 23, 23, 8) },
		{ 'b', new(378, 0, 22, 32, 0) },
		{ 'c', new(162, 127, 20, 23, 8) },
		{ 'd', new(307, 0, 24, 32, -1) },
		{ 'e', new(182, 127, 20, 23, 8) },
		{ 'f', new(5, 0, 20, 38, 0) },
		{ 'g', new(130, 98, 23, 29, 8) },
		{ 'h', new(331, 0, 24, 32, -2) },
		{ 'i', new(415, 0, 15, 31, 0) },
		{ 'j', new(53, 0, 22, 37, 0) },
		{ 'k', new(355, 0, 23, 32, 0) },
		{ 'l', new(400, 0, 15, 32, -1) },
		{ 'm', new(382, 98, 33, 23, 8) },
		{ 'n', new(94, 127, 23, 23, 8) },
		{ 'o', new(140, 127, 22, 23, 8) },
		{ 'p', new(27, 98, 27, 29, 8) },
		{ 'q', new(198, 98, 22, 29, 8) },
		{ 'r', new(258, 127, 20, 22, 8) },
		{ 's', new(202, 127, 20, 23, 8) },
		{ 't', new(235, 98, 17, 27, 3) },
		{ 'u', new(0, 127, 24, 23, 8) },
		{ 'v', new(117, 127, 23, 23, 8) },
		{ 'w', new(415, 98, 32, 23, 8) },
		{ 'x', new(24, 127, 24, 23, 8) },
		{ 'y', new(153, 98, 23, 29, 8) },
		{ 'z', new(236, 127, 22, 22, 8) },
		{ '|', new(87, 0, 9, 37, 0) },
		{ '~', new(396, 127, 23, 12, 0) },
	};
}
