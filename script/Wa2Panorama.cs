using Godot;

// Panorama mode for screens wider than 16:9: the scene layer (backgrounds) is stretched toward the
// edges into the side strips, the centre third untouched (shader/panorama.gdshader). Characters are
// counter-warped (mask.gdshader prewarp) so they keep their exact shape; event CGs (v*.tga) are not
// stretched; text box, menus and movies are untouched. Strength: Wa2Phone.PanoIdx (Off/Half/Full).
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
	public static float Covered => _inst?._e ?? 0f;     // px per side currently filled by the scene

	public override void _Ready()
	{
		Texture = _e2.Viewport.GetTexture();
		Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shader/panorama.gdshader") };
		((ShaderMaterial)Material).SetShaderParameter("scene", _e2.Viewport.GetTexture());
	}

	public override void _Process(double delta)
	{
		var e = _e2;
		float strip = Wa2Wide.StripWidth;
		bool cg = e.BgInfo.Path != null && e.BgInfo.Path.StartsWith("v", System.StringComparison.OrdinalIgnoreCase);
		bool scene = e.State == Wa2EngineMain.GameState.GAME && !e.VideoPlayer.IsPlaying();
		_target = scene && !cg ? strip * Strengths[Wa2Phone.PanoIdx] : 0f;
		_e = Mathf.MoveToward(_e, _target, (float)delta * Mathf.Max(strip, 1) / 0.45f);   // ease over ~0.45 s
		bool on = _e > 0.5f;
		Visible = on;
		e.SubViewport.SelfModulate = new Color(1, 1, 1, on ? 0 : 1);
		RenderingServer.GlobalShaderParameterSet("pano", new Vector4(_e, C, L, on ? 1 : 0));
		Position = new Vector2(-_e, 0);
		Size = new Vector2(1280 + 2 * _e, 720);
		((ShaderMaterial)Material).SetShaderParameter("out_width", 1280 + 2 * _e);
		Wa2Wide.SetCovered(_e);
	}
}
