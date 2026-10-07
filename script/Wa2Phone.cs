using Godot;
using System;
using System.Collections.Generic;

// Phone settings (user://phone.cfg; shown as Options -> OPTION 4, Wa2PhoneOptions) and the gesture
// guide: a panel in the game's own look (its letters via Wa2Label, the text box's cyan ring colour,
// its snowflake art). The guide opens once on first play and from OPTION 4 -> Gesture Guide.
public partial class Wa2Phone : Control
{
	private const string Cfg = "user://phone.cfg";
	public static readonly float[] TextSizes = { 1.0f, 1.2f, 1.35f, 1.5f };
	public static readonly string[] TextNames = { "Small", "Medium", "Large", "Larger" };
	public static readonly string[] FillNames = { "Deep", "Light", "Black" };
	public static int TextIdx = 2, FillIdx = 0;
	public static bool AutoHide = true;
	public static int PanoIdx = 2;
	public static readonly string[] PanoNames = { "Off", "Half", "Full" };
	public static bool GuideSeen;
	public static bool CenterText;          // Text Position: false = like PC, true = centred
	public static bool Haptics = true;
	private static Wa2Phone _panel;
	private readonly List<(Wa2Label lab, Func<bool> on)> _opts = new();
	private static readonly Color Cyan = new(0.55f, 0.88f, 1f), Dim = new(0.72f, 0.76f, 0.8f);

	public static void LoadSettings()
	{
		var c = new ConfigFile();
		if (c.Load(Cfg) == Error.Ok)
		{
			TextIdx = Math.Clamp((int)c.GetValue("phone", "text", 2), 0, TextSizes.Length - 1);
			FillIdx = Math.Clamp((int)c.GetValue("phone", "fill", 0), 0, FillNames.Length - 1);
			GuideSeen = (bool)c.GetValue("phone", "guide_seen", false);
			AutoHide = (bool)c.GetValue("phone", "autohide", true);
			PanoIdx = Math.Clamp((int)c.GetValue("phone", "pano", 2), 0, 2);
			CenterText = (bool)c.GetValue("phone", "center", false);
			Haptics = (bool)c.GetValue("phone", "haptics", true);
		}
		if (!Wa2AdvMain.TextScaleFromArgs) Wa2AdvMain.TextScale = TextSizes[TextIdx];
		foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--pano=")) PanoIdx = int.Parse(a["--pano=".Length..]);
	}
	public static void Save()
	{
		var c = new ConfigFile();
		c.SetValue("phone", "center", CenterText);
		c.SetValue("phone", "haptics", Haptics);
		c.SetValue("phone", "text", TextIdx);
		c.SetValue("phone", "fill", FillIdx);
		c.SetValue("phone", "guide_seen", GuideSeen);
		c.SetValue("phone", "autohide", AutoHide);
		c.SetValue("phone", "pano", PanoIdx);
		c.Save(Cfg);
	}

	public static void ShowIfFirstTime(Wa2EngineMain e)
	{
		if (!GuideSeen && !Wa2Autoplay.Enabled) Open(e);
	}
	public static void Open(Wa2EngineMain e)
	{
		if (_panel != null) return;
		_panel = new Wa2Phone();
		e.AddChild(_panel);
		e.UiMgr.UiQueue.Push(_panel);
		e.StopSkip();
	}
	public static bool IsOpen => _panel != null;

	public override void _Ready()
	{
		var e = Wa2EngineMain.Engine;
		Position = Vector2.Zero; Size = new Vector2(1280, 720);
		MouseFilter = MouseFilterEnum.Stop;
		// backdrop wider than the 1280 game area so a widened (Fill screen) picture is dimmed too
		AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), Position = new Vector2(-600, 0), Size = new Vector2(2480, 720), MouseFilter = MouseFilterEnum.Ignore });
		var box = new Panel { Position = new Vector2(170, 46), Size = new Vector2(940, 500), MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.10f, 0.14f, 0.93f), BorderColor = new Color(Cyan, 0.85f),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			ShadowColor = new Color(Cyan, 0.25f), ShadowSize = 10,
		});
		AddChild(box);
		var deco = GD.Load<Texture2D>("res://assets/grp/sys_00000.png");
		foreach (var (pos, flip) in new[] { (new Vector2(178, 54), false), (new Vector2(902, 54), true) })
			AddChild(new TextureRect { Texture = new AtlasTexture { Atlas = deco, Region = new Rect2(100, 52, 220, 125) }, Position = pos, Size = new Vector2(200, 114),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, FlipH = flip, Modulate = new Color(1, 1, 1, 0.45f), MouseFilter = MouseFilterEnum.Ignore });

		Text("How to play", 230, 76, 30, Cyan);
		var rows = new (string g, string what)[]
		{
			("Tap", "next line  (stops Skip)"),
			("Hold", "Auto mode on / off"),
			("Swipe down", "backlog"),
			("Swipe up", "hide the text box  (also: two-finger tap)"),
			("Swipe right", "skip text you have read"),
			("Swipe left", "skip everything"),
		};
		for (int i = 0; i < rows.Length; i++)
		{
			Text(rows[i].g, 250, 128 + i * 38, 24, Cyan);
			Text(rows[i].what, 450, 128 + i * 38, 24, new Color(1, 1, 1));
		}
		Text("Settings: Options -> OPTION 4, or the ? button.", 230, 400, 22, Dim);
		Option("Close", 590, 480, () => true, Close, 30);
		RefreshOptions();
	}

	private Wa2Label Text(string s, float x, float y, int size, Color col)
	{
		var l = new Wa2Label { Position = new Vector2(x, y), FontSize = size, Color = col, MaxChars = 60, MaxLines = 2 };
		AddChild(l);
		l.SetText(s);
		return l;
	}
	private void Option(string s, float x, float y, Func<bool> on, Action act, int size = 26)
	{
		var l = Text(s, x, y, size, Dim);
		_opts.Add((l, on));
		var b = new Button { Flat = true, Position = new Vector2(x - 14, y - 12), Size = new Vector2(s.Length * size / 2f + 28, size + 24), FocusMode = FocusModeEnum.None };
		b.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); b.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		b.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty()); b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		b.Pressed += () => { Wa2Quick.Haptic(); act(); RefreshOptions(); };
		AddChild(b);
	}
	private void RefreshOptions()
	{
		foreach (var (lab, on) in _opts) { lab.Color = on() ? Cyan : Dim; lab.QueueRedraw(); lab.SetText(lab.Text); }
	}
	public static void SetPano(int k) { PanoIdx = k; Save(); }
	public static void SetCenter(bool on) { CenterText = on; Save(); Wa2EngineMain.Engine.AdvMain.ApplyTextScale(); }
	public static void SetAutoHide(bool on) { AutoHide = on; Save(); }
	public static void SetHaptics(bool on) { Haptics = on; Save(); }
	public static void OpenGuide(Wa2EngineMain e) => Open(e);
	public static void SetText(int k)
	{
		TextIdx = k; Save();
		Wa2AdvMain.TextScale = TextSizes[k];
		Wa2EngineMain.Engine.AdvMain.ApplyTextScale();
	}
	public static void SetFill(int k) { FillIdx = k; Save(); Wa2Wide.ApplyFill(); }
	private void Close()
	{
		GuideSeen = true; Save();
		var e = Wa2EngineMain.Engine;
		if (e.UiMgr.UiQueue.Count > 0 && e.UiMgr.UiQueue.Peek() == this) e.UiMgr.UiQueue.Pop();
		_panel = null;
		QueueFree();
	}
}
