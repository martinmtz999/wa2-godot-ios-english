using Godot;
using System;
using System.Collections.Generic;

// Options -> OPTION 4: the phone settings as a fourth page of the game's own Options menu. The page
// art (assets/grp/phone_opt.png, lit buttons phone_opt_on.png, tab phone_tab.png) is generated from
// the original page-1 art by tools/make-phone-options.py; this file must use the same layout: boxes
// of rows 38 px apart, the first row 32 px below the box top, buttons from x 456 (2 per row: 256 px
// cells 255 apart; 3: 170; 4: 128). Like the original pages, the normal look is baked into the page
// and each button only carries its lit texture. The Details box explains the last setting touched.
public static class Wa2PhoneOptions
{
	private record Row(string Label, string[] Choices, Func<int> Get, Action<int> Set, string Details, bool Momentary = false);

	private static List<(string title, Row[] rows)> Page() => new()
	{
		("Screen Setting", new[]
		{
			new Row("Fill Screen", new[] { "Off", "Half", "Full" }, () => Wa2Phone.PanoIdx, Wa2Phone.SetPano,
				"Widens the scenery into the side bars; CGs only gently; characters keep their shape."),
			new Row("Side Bars", new[] { "Deep", "Light", "Black" }, () => Wa2Phone.FillIdx, Wa2Phone.SetFill,
				"How the bars beside the picture look: a deep blur, a light blur, or plain black."),
		}),
		("Text Setting", new[]
		{
			new Row("Text Size", Wa2Phone.TextNames, () => Wa2Phone.TextIdx, Wa2Phone.SetText,
				"Size of the dialogue text and its box."),
			new Row("Text Position", new[] { "Like PC", "Centered" }, () => Wa2Phone.CenterText ? 1 : 0, k => Wa2Phone.SetCenter(k == 1),
				"Short pages start where the PC version starts them, or sit in the middle of the box."),
		}),
		("Control Setting", new[]
		{
			new Row("Side Buttons", new[] { "Auto-Hide", "Always" }, () => Wa2Phone.AutoHide ? 0 : 1, k => Wa2Phone.SetAutoHide(k == 0),
				"Hide the side buttons while reading; tap a side bar to show them."),
			new Row("Haptics", new[] { "On", "Off" }, () => Wa2Phone.Haptics ? 0 : 1, k => Wa2Phone.SetHaptics(k == 0),
				"Light taps on button presses and a few dramatic moments."),
			new Row("Gesture Guide", new[] { "Show" }, () => -1, _ => Wa2Phone.OpenGuide(Wa2EngineMain.Engine),
				"Shows the touch gestures: tap, hold, and the four swipes.", true),
		}),
	};

	private static readonly List<(Wa2Button btn, Func<bool> on)> _buttons = new();
	private static Wa2Label _details;
	public static int PageIndex = -1;

	public static void Attach(OptionsMenu m)
	{
		if (!ResourceLoader.Exists("res://assets/grp/phone_opt.png")) return;    // art not generated
		var page = new TextureRect
		{
			Name = "Option4", Texture = GD.Load<Texture2D>("res://assets/grp/phone_opt.png"),
			Position = new Vector2(128, 128), Size = new Vector2(1024, 464), Visible = false,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
		};
		m.PageList.AddChild(page);
		PageIndex = page.GetIndex();
		var lit = GD.Load<Texture2D>("res://assets/grp/phone_opt_on.png");
		var click = m.PageVoiceYesBtn.ClickStream;
		int y = 0;
		foreach (var (title, rows) in Page())
		{
			for (int i = 0; i < rows.Length; i++)
			{
				var row = rows[i];
				int cy = y + 32 + i * 38, n = row.Choices.Length;
				int w = n <= 2 ? 256 : n == 3 ? 170 : 128, step = n <= 2 ? 255 : w;
				var group = row.Momentary ? null : new ButtonGroup();
				for (int k = 0; k < n; k++)
				{
					int idx = k;
					var b = new Wa2Button
					{
						ToggleMode = !row.Momentary, KeepPressedOutside = true, ButtonGroup = group, ClickStream = click,
						TexturePressed = new AtlasTexture { Atlas = lit, Region = new Rect2(456 + k * step, cy - 19, w, 38) },
						Position = new Vector2(456 + k * step, cy - 19), Size = new Vector2(w, 38),
						IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
					};
					b.ButtonDown += () => { row.Set(idx); ShowDetails(row.Details); Refresh(); };
					page.AddChild(b);
					if (!row.Momentary) _buttons.Add((b, () => row.Get() == idx));
				}
			}
			y += 32 + (rows.Length - 1) * 38 + 26 + 8;
		}
		_details = new Wa2Label { Position = new Vector2(150, 441), FontSize = 18, Color = new Color(0.72f, 0.8f, 0.84f), MaxChars = 46, MaxLines = 1 };
		page.AddChild(_details);
		AddTab(m);
		Refresh();
	}

	// Fourth tab: the row of tabs moves left by one tab so all four fit inside the 1280 frame.
	private static void AddTab(OptionsMenu m)
	{
		var list = m.OptionButtonList;
		var first = list.GetChild<Wa2Button>(0);
		var tabs = GD.Load<Texture2D>("res://assets/grp/phone_tab.png");
		var tab = new Wa2Button
		{
			ToggleMode = true, KeepPressedOutside = true, ButtonGroup = first.ButtonGroup, ClickStream = first.ClickStream,
			TextureNormal = new AtlasTexture { Atlas = tabs, Region = new Rect2(0, 0, 192, 32) },
			TextureHover = new AtlasTexture { Atlas = tabs, Region = new Rect2(0, 0, 192, 32) },
			TexturePressed = new AtlasTexture { Atlas = tabs, Region = new Rect2(0, 32, 192, 32) },
			CustomMinimumSize = new Vector2(192, 32),
		};
		tab.ButtonDown += () => m.UpdatePage(PageIndex);
		list.AddChild(tab);
		list.Position -= new Vector2(192, 0);
		list.Size += new Vector2(192, 0);
		// The black bar and "PAGE" line behind the tabs (two copies of the same strip) move along;
		// a copy of their right end continues the line to the frame edge.
		foreach (var name in new[] { "TextureRect2", "TextureRect3" })
		{
			var strip = m.GetNodeOrNull<TextureRect>(name);
			if (strip == null) continue;
			strip.Position -= new Vector2(192, 0);
			var end = (TextureRect)strip.Duplicate();
			end.Position = strip.Position + new Vector2(strip.Size.X, 0);
			end.Size = new Vector2(192, strip.Size.Y);
			if (strip.Texture is AtlasTexture at)
				end.Texture = new AtlasTexture { Atlas = at.Atlas, Region = new Rect2(at.Region.End.X - 192, at.Region.Position.Y, 192, at.Region.Size.Y) };
			strip.GetParent().AddChild(end);
			strip.GetParent().MoveChild(end, strip.GetIndex() + 1);
		}
	}

	private static void ShowDetails(string s) => _details?.SetText(s);

	public static void Refresh()
	{
		foreach (var (b, on) in _buttons) if (GodotObject.IsInstanceValid(b)) b.SetPressedNoSignal(on());
	}
}
