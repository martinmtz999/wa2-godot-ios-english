using Godot;
using System.Collections.Generic;

// Screens wider than 16:9 (e.g. iPhone 15 Pro Max, ~19.5:9): the 1280x720 game stays centred and
// untouched; the side strips get a blurred, darkened extension of the current frame
// (shader/wide_fill.gdshader) and the ADV toolbar moves into them, built from the game's own art:
// the PC icon sheet (sys_00010: row 0 idle, row 1 active, row 2 pressed) and the snowflake cluster
// of the text box (sys_00000). System buttons left, reading buttons right (thumb side), grouped
// above and below the middle so the Dynamic Island never covers one. Taps on the strips count as
// taps on the game. On 16:9 or narrower screens nothing changes. Needs stretch aspect "expand".
public partial class Wa2Wide : Node
{
	private Wa2EngineMain _e;
	public static float StripBlur = 0.025f;   // user: "less of a blur" (first look used 0.06)
	private CanvasLayer _fillLayer, _railLayer;
	private ColorRect _fill;
	private Control _rails;
	private readonly List<(Control bg, int side)> _railBgs = new();
	private readonly List<TextureRect> _ornaments = new();
	private readonly List<(TextureButton btn, int col, System.Func<bool> active)> _btns = new();
	private Vector2 _off;
	private bool _wide, _built;
	private const float Btn = 72f;

	public static void Attach(Wa2EngineMain e)
	{
		var w = new Wa2Wide { _e = e, Name = "Wa2Wide" };
		e.GetTree().Root.CallDeferred(Node.MethodName.AddChild, w);
	}

	public override void _Ready()
	{
		_fillLayer = new CanvasLayer { Layer = 1 };
		_fill = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.Black };
		_fill.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shader/wide_fill.gdshader") };
		_fillLayer.AddChild(_fill);
		AddChild(_fillLayer);

		_railLayer = new CanvasLayer { Layer = 2 };
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_railLayer.AddChild(root);
		for (int side = 0; side < 2; side++)
		{
			var bg = new RailInput { E = _e, MouseFilter = Control.MouseFilterEnum.Stop };
			root.AddChild(bg);
			_railBgs.Add((bg, side));
		}
		_rails = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(_rails);
		var deco = GD.Load<Texture2D>("res://assets/grp/sys_00000.png");
		for (int side = 0; side < 2; side++)
		{
			var o = new TextureRect
			{
				Texture = new AtlasTexture { Atlas = deco, Region = new Rect2(100, 52, 220, 125) },
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				FlipH = side == 1, MouseFilter = Control.MouseFilterEnum.Ignore,
				Modulate = new Color(1, 1, 1, 0.55f),
			};
			_rails.AddChild(o);
			_ornaments.Add(o);
		}
		AddChild(_railLayer);
		// `--strip-blur=R` overrides the blur radius (fraction of screen height) for comparisons.
		var rb = System.Linq.Enumerable.FirstOrDefault(OS.GetCmdlineUserArgs(), a => a.StartsWith("--strip-blur="));
		if (rb != null && float.TryParse(rb["--strip-blur=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r))
			StripBlur = r;
		((ShaderMaterial)_fill.Material).SetShaderParameter("radius", StripBlur);
		GetViewport().SizeChanged += Layout;
		Layout();
	}

	private void BuildButtons()
	{
		if (_built) return;
		_built = true;
		var adv = _e.AdvMain;
		var sheet = GD.Load<Texture2D>("res://assets/grp/sys_00010.png");
		AtlasTexture Icon(int col, int row) => new() { Atlas = sheet, Region = new Rect2(col * 40, row * 40, 40, 40) };
		void Take(TextureButton b, int col, System.Func<bool> active = null)
		{
			b.GetParent()?.RemoveChild(b);
			_rails.AddChild(b);
			b.IgnoreTextureSize = true;
			b.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
			b.TextureNormal = Icon(col, 0);
			b.TextureHover = Icon(col, 0);
			b.TexturePressed = Icon(col, 2);
			b.TextureDisabled = Icon(col, 0);
			b.TextureFocused = null;
			b.Size = new Vector2(Btn, Btn);
			_btns.Add((b, col, active));
		}
		// PC sheet columns: 0 q.save, 1 q.load, 2 config, 3 save, 4 load, 5 backlog, 6 auto, 7 skip, 8 close
		var qs = adv.GetNodeOrNull<TextureButton>("HBoxContainer/QSave");
		var ql = adv.GetNodeOrNull<TextureButton>("HBoxContainer/QLoad");
		Take(adv.OptionButton, 2);
		Take(adv.SaveButton, 3);
		Take(adv.LoadButton, 4);
		Take(adv.BackLogButton, 5);
		Take(adv.AutoButton, 6, () => _e.AutoMode);
		Take(adv.SkipButton, 7, () => _e.SkipMode || _e.Skipping);
		Take(adv.OffButton, 8);
		// Quick save / quick load stay hidden: the user does not use them and they crowded the strip.
		// Auto-save on leaving the app (Wa2Quick) needs no button.
	}

	private void Layout()
	{
		Vector2 vs = GetViewport().GetVisibleRect().Size;
		_off = ((vs - new Vector2(1280, 720)) / 2).Floor();
		_off = new Vector2(Mathf.Max(_off.X, 0), Mathf.Max(_off.Y, 0));
		_e.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_e.Position = _off;
		_e.Size = new Vector2(1280, 720);
		bool bars = _off.X > 0 || _off.Y > 0;
		_fill.Visible = bars;
		_fill.Position = Vector2.Zero;
		_fill.Size = vs;
		var sm = (ShaderMaterial)_fill.Material;
		sm.SetShaderParameter("center", new Vector4(_off.X / vs.X, _off.Y / vs.Y, 1280 / vs.X, 720 / vs.Y));
		sm.SetShaderParameter("aspect", vs.X / vs.Y);
		_wide = _off.X >= Btn * 0.75f;
		foreach (var (bg, side) in _railBgs)
		{
			bg.Visible = _off.X > 0;
			bg.Position = new Vector2(side == 0 ? 0 : _off.X + 1280, 0);
			bg.Size = new Vector2(vs.X - 1280 - _off.X, vs.Y);
			if (side == 0) bg.Size = new Vector2(_off.X, vs.Y);
		}
		float drop = _wide ? 40f : 0f;
		if (Wa2AdvMain.WideDrop != drop) { Wa2AdvMain.WideDrop = drop; _e.AdvMain.ApplyTextScale(); }
		if (!_wide) { _rails.Visible = false; return; }
		BuildButtons();
		float rw = _off.X, cl = rw / 2, cr = _off.X + 1280 + rw / 2, top = _off.Y;
		float ow = Mathf.Min(rw * 0.92f, 200), oh = ow * 125 / 220;
		_ornaments[0].Position = new Vector2(cl - ow / 2, top + 14); _ornaments[0].Size = new Vector2(ow, oh);
		_ornaments[1].Position = new Vector2(cr - ow / 2, top + 14); _ornaments[1].Size = new Vector2(ow, oh);
		// Two groups per strip, mirrored, with roomy spacing, clear of the vertical middle (Dynamic
		// Island) and of the rounded corners. Left = system: Save, Load above; Options below.
		// Right = reading (thumb): Backlog, Auto above; Skip, Hide below.
		float step = Btn + 30, mid = top + 720 * 0.5f;
		float a1 = mid - 70 - Btn - step, a2 = mid - 70 - Btn, b1 = mid + 70, b2 = mid + 70 + step;
		void At(int i, float x, float y) => _btns[i].btn.Position = new Vector2(x - Btn / 2, y);
		At(1, cl, a1);   // save
		At(2, cl, a2);   // load
		At(0, cl, b1);   // options
		At(3, cr, a1);   // backlog
		At(4, cr, a2);   // auto
		At(5, cr, b1);   // skip
		At(6, cr, b2);   // hide text box
	}

	private bool _lowApplied;
	private double _powerCheck;
	public override void _Process(double delta)
	{
		// Low Power Mode: 30 fps and a cheap 8-tap blur in the strips; back to 60 fps / 64 taps after.
		_powerCheck -= delta;
		if (_powerCheck <= 0)
		{
			_powerCheck = 3;
			bool low = Wa2Feel.LowPower;
			if (low != _lowApplied)
			{
				_lowApplied = low;
				Engine.MaxFps = low ? 30 : 60;
				((ShaderMaterial)_fill.Material).SetShaderParameter("taps", low ? 12 : 64);
				((ShaderMaterial)_fill.Material).SetShaderParameter("radius", low ? Mathf.Min(0.016f, StripBlur) : StripBlur);   // few taps need a small radius or they ghost
				Wa2Trace.Log("lowpower", low ? 1 : 0);
				GD.Print("Wa2Wide: low power " + low);
			}
		}
		if (!_wide || _e == null) return;
		var adv = _e.AdvMain;
		bool show = _e.State == Wa2EngineMain.GameState.GAME && adv.IsVisibleInTree()
			&& _e.UiMgr.UiQueue.Count > 0 && _e.UiMgr.UiQueue.Peek() == adv && !_e.VideoPlayer.IsPlaying();
		_rails.Visible = show;
		if (!show) return;
		_rails.Modulate = new Color(1, 1, 1, adv.Modulate.A);
		foreach (var (btn, col, active) in _btns)
		{
			int row = active != null && active() ? 1 : 0;
			if (btn.TextureNormal is AtlasTexture at && (int)(at.Region.Position.Y / 40) != row)
				at.Region = new Rect2(col * 40, row * 40, 40, 40);
		}
	}

	// Taps and swipes on the strips go to the game, as if they landed on its nearest edge.
	private partial class RailInput : Control
	{
		public Wa2EngineMain E;
		public override void _GuiInput(InputEvent ev)
		{
			if (E == null) return;
			var x = (InputEvent)ev.Duplicate();
			Vector2 shift = Position - E.Position;
			switch (x)
			{
				case InputEventScreenTouch t: t.Position += shift; break;
				case InputEventScreenDrag d: d.Position += shift; break;
				case InputEventMouseButton m: m.Position += shift; break;
				case InputEventMouseMotion mm: mm.Position += shift; break;
			}
			E._GuiInput(x);
			AcceptEvent();
		}
	}
}
