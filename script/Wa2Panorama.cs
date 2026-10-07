using Godot;

// Panorama mode for screens wider than 16:9: the scene layer (backgrounds) is stretched toward the
// edges into the side strips, the centre third untouched (shader/panorama.gdshader). Characters are
// counter-warped (mask.gdshader prewarp) so they keep their exact shape; event CGs (v*.tga) get a
// gentler fill (below); text box, menus and movies are untouched. Strength: Wa2Phone.PanoIdx.
public partial class Wa2Panorama : TextureRect
{
	private static Wa2Panorama _inst;
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

	// Event CGs get the 'soft' fill: 5% even widening, then a gentler edge stretch for the rest. CGs
	// with a face near the edge (tools/cgfill/faceguard.py, anime face detector) get only the 5% and
	// keep the side bars, so no face is widened visibly.
	private const float CgWiden = 0.05f;
	private static readonly System.Collections.Generic.HashSet<string> HeldBack = new()
	{
		"v102000", "v102001", "v202301", "v205501", "v205600", "v205601", "v206801", "v206802",
		"v208602", "v208700", "v208701", "v208702", "v208800", "v209200", "v211000", "v211001",
	};
	private float _k = 1f;

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
			if (!cg) eT = strip * s;
			else
			{
				kT = 1f + CgWiden * s;
				if (!HeldBack.Contains(System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant()))
					eT = Mathf.Max(strip * s - 640f * (kT - 1f), 0f);
			}
		}
		_target = eT;
		_e = Mathf.MoveToward(_e, eT, (float)delta * Mathf.Max(strip, 1) / 0.45f);   // ease over ~0.45 s
		_k = Mathf.MoveToward(_k, kT, (float)delta * CgWiden / 0.45f);
		float cov = 640f * (_k - 1f) + _e;          // px per side the picture reaches past 1280
		bool on = cov > 0.5f;
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
