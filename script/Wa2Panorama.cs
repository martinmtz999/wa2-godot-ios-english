using Godot;

// Panorama mode for screens wider than 16:9: the scene layer (backgrounds) is stretched toward the
// edges into the side strips, the centre third untouched (shader/panorama.gdshader). Characters are
// counter-warped (mask.gdshader prewarp) so they keep their exact shape; event CGs (v*.tga) get the
// same fill, except a few held back (below); text box, menus and movies are untouched. Strength: Wa2Phone.PanoIdx.
public partial class Wa2Panorama : TextureRect
{
	private static Wa2Panorama _inst;
	public static bool HiResCanvas;
	public static readonly float[] Strengths = { 0f, 0.5f, 1f };
	private const float C = 200f, L = 440f;
	private float _e, _target;
	private Wa2EngineMain _e2;

	public static void Attach(Wa2EngineMain e)
	{
		var p = new Wa2Panorama { Name = "Panorama", _e2 = e, MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = ExpandModeEnum.IgnoreSize, StretchMode = StretchModeEnum.Scale };
		var vpc = e.SubViewport;
		vpc.AddSibling(p);
		_inst = p;
		// Scene canvas at the screen's own resolution (up to 2x) for the sharper backgrounds and CGs
		// (Wa2Resource.HiRes), layout still 1280x720. At exactly screen size nothing is resampled a
		// second time: a fixed 2x canvas shrunk to the phone's 1290 px softened everything ~5%
		// (measured 2026-10-08). This layer then always shows the canvas (the container would draw
		// it at its pixel size), with no stretch when the fill is off.
		if (Wa2Resource.HiResAvailable)
		{
			var win = DisplayServer.WindowGetSize();   // game area pixels per layout pixel (height-limited on wide phones)
			float sc = Mathf.Clamp(Mathf.Min(win.X / 1280f, win.Y / 720f), 1f, 2f);
			if (sc > 1.05f)
			{
				HiResCanvas = true;
				var vp = (SubViewport)e.Viewport;
				vp.Size2DOverride = new Vector2I(1280, 720);
				vp.Size2DOverrideStretch = true;
				vp.Size = new Vector2I(Mathf.RoundToInt(1280 * sc), Mathf.RoundToInt(720 * sc));
			}
		}
		foreach (var ch in e.Chars)
			if (ch?.Material is ShaderMaterial sm) sm.SetShaderParameter("prewarp", true);
	}
	public static float Covered => _inst == null ? 0f : 640f * (_inst._k - 1f) + _inst._e;   // px per side filled by the scene

	public override void _Ready()
	{
		Texture = _e2.Viewport.GetTexture();
		Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shader/panorama.gdshader") };
		((ShaderMaterial)Material).SetShaderParameter("scene", _e2.Viewport.GetTexture());
	}

	// CGs with a face near the edge (tools/cgfill/faceguard.py, anime face detector) get only a 5%
	// even widening and keep the side bars, so no face is widened visibly.
	private const float CgWiden = 0.05f;
	private static readonly System.Collections.Generic.HashSet<string> HeldBack = new()
	{
		"v102000", "v102001", "v202301", "v205501", "v205600", "v205601", "v206801", "v206802",
		"v208602", "v208700", "v208701", "v208702", "v208800", "v209200", "v211000", "v211001",
	};
	private float _k = 1f, _kTarget = 1f;

	public override void _Process(double delta)
	{
		var e = _e2;
		float strip = Wa2Wide.StripWidth, s = Strengths[Wa2Phone.PanoIdx];
		string path = e.BgInfo.Path;
		bool cg = path != null && path.StartsWith("v", System.StringComparison.OrdinalIgnoreCase);
		bool scene = e.State == Wa2EngineMain.GameState.GAME && !e.VideoPlayer.IsPlaying();
		float kT = 1f, eT = 0f;
		if (scene && strip > 0)
		{
			// CGs use the same stretch as backgrounds (user, 2026-10-08): the panorama warps the whole
			// scene at once, so different settings for backgrounds and CGs had to ease across every
			// background <-> CG cross-fade, a visible re-stretch. Only the held-back CGs differ.
			if (!cg || !HeldBack.Contains(System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant()))
				eT = strip * s;
			else
				kT = 1f + CgWiden * s;
		}
		if (eT != _target || kT != _kTarget) Wa2Trace.Log("pano", eT, kT, path);
		_target = eT; _kTarget = kT;
		_e = Mathf.MoveToward(_e, eT, (float)delta * Mathf.Max(strip, 1) / 0.45f);   // ease over ~0.45 s
		_k = Mathf.MoveToward(_k, kT, (float)delta * CgWiden / 0.45f);
		float cov = 640f * (_k - 1f) + _e;          // px per side the picture reaches past 1280
		bool on = cov > 0.5f || HiResCanvas;
		Visible = on;
		e.SubViewport.SelfModulate = new Color(1, 1, 1, on ? 0 : 1);
		// Characters are counter-warped only for the plain background stretch (pre_k = 1); on a CG
		// (rarely with characters) they are not.
		bool plain = _k < 1.0005f;
		RenderingServer.GlobalShaderParameterSet("pano", new Vector4(plain ? _e : 0f, C, L, on && plain ? 1 : 0));
		Position = new Vector2(-cov, 0);
		Size = new Vector2(1280 + 2 * cov, 720);
		var m = (ShaderMaterial)Material;
		m.SetShaderParameter("out_width", 1280 + 2 * cov);
		m.SetShaderParameter("pre_k", _k);
		m.SetShaderParameter("edge", _e);
		Wa2Wide.SetCovered(cov);
	}
}
