using Godot;

// Autoload: runs before the main scene. Exported apps built without game files (the GitHub build)
// load the player's `wa2-data.pck` from Wa2Res. That pack is the full project export made locally
// with the game-derived UI images and fonts, so it replaces the app's own scenes and resources.
// Both carry the engine commit they were built from: `build_commit.txt` (written by the CI
// workflow) and `data_commit.txt` (written by tools/ios-data-pack.sh).
public partial class Wa2DataPack : Node
{
	public enum PackState { NotNeeded, Loaded, Missing, Failed }
	public static PackState State = PackState.NotNeeded;
	public static string AppCommit = "";
	public static string DataCommit = "";

	public static string PackPath => OS.GetUserDataDir().PathJoin("Wa2Res").PathJoin("wa2-data.pck");
	public static bool CommitMismatch => State == PackState.Loaded && AppCommit != "" && DataCommit != AppCommit;

	public Wa2DataPack()
	{
		LoadPack();
		UseHiResUi();
	}

	private static void LoadPack()
	{
		if (!OS.HasFeature("template") || OS.GetName() != "iOS")
			return;
		AppCommit = ReadText("res://build_commit.txt");
		if (!FileAccess.FileExists(PackPath))
		{
			State = PackState.Missing;
			return;
		}
		State = ProjectSettings.LoadResourcePack(PackPath, true) ? PackState.Loaded : PackState.Failed;
		DataCommit = ReadText("res://data_commit.txt");
	}

	// Sharper UI on high-resolution screens: assets/grp2x/ holds x2 upscales of the UI images
	// (tools/upscale-ui.sh). Before any scene loads, each one replaces res://assets/grp/<name> in
	// the resource cache as an ImageTexture whose size override is the original size, so layouts
	// and AtlasTexture regions keep their 1280x720 coordinates while drawing from the x2 pixels.
	// Off when the screen is not notably larger than 720p, or with `--ui-hires=0`.
	// Runs from the constructor: the main scene (and every texture it uses) is loaded before any
	// autoload's _Ready, which was too late (verified: the swap had no effect there).
	private static bool _hiResDone;
	private static readonly System.Collections.Generic.Dictionary<string, ImageTexture> HiRes = new();

	// The cache swap covers everything loaded later; scenes already loaded at startup keep their
	// original textures (verified), so once the main scene is ready, swap them in the live tree.
	public override void _Ready()
	{
		if (HiRes.Count > 0) Callable.From(() => SwapTree(GetTree().Root)).CallDeferred();
	}
	private static Texture2D Hi(Texture2D t)
	{
		if (t is AtlasTexture at && at.Atlas != null && HiRes.TryGetValue(at.Atlas.ResourcePath, out var a)) { at.Atlas = a; return t; }
		return t != null && t is not ImageTexture && HiRes.TryGetValue(t.ResourcePath, out var h) ? h : t;
	}
	public static void SwapTree(Node n)
	{
		int swapped = 0;
		void Visit(Node node)
		{
			foreach (var prop in node.GetPropertyList())
			{
				if ((Variant.Type)(int)prop["type"] != Variant.Type.Object) continue;
				string name = (string)prop["name"];
				var v = node.Get(name);
				if (v.Obj is Texture2D t)
				{
					var h = Hi(t);
					if (!ReferenceEquals(h, t)) { node.Set(name, h); swapped++; }
					else if (t is AtlasTexture) swapped++;
				}
				else if (v.Obj is SpriteFrames sf)
				{
					foreach (string anim in sf.GetAnimationNames())
						for (int i = 0; i < sf.GetFrameCount(anim); i++)
						{
							var ft = sf.GetFrameTexture(anim, i);
							var h = Hi(ft);
							if (!ReferenceEquals(h, ft)) { sf.SetFrame(anim, i, h, sf.GetFrameDuration(anim, i)); swapped++; }
						}
				}
			}
			foreach (Node c in node.GetChildren()) Visit(c);
		}
		ulong t0 = Time.GetTicksMsec();
		Visit(n);
		GD.Print($"Wa2DataPack: live UI swap touched {swapped} textures in {Time.GetTicksMsec() - t0} ms");
	}
	private static void UseHiResUi()
	{
		if (_hiResDone) return;
		_hiResDone = true;
		var args = OS.GetCmdlineUserArgs();
		bool off = System.Array.IndexOf(args, "--ui-hires=0") >= 0;
		bool force = System.Array.IndexOf(args, "--ui-hires=1") >= 0;
		float scale = DisplayServer.ScreenGetSize().Y / 720f;
		if (OS.GetName() != "iOS" && OS.GetName() != "Android")
			scale = DisplayServer.WindowGetSize().Y / 720f;
		if (off || (!force && scale < 1.2f)) return;
		using var dir = DirAccess.Open("res://assets/grp2x");
		if (dir == null) return;
		ulong t0 = Time.GetTicksMsec();
		int n = 0;
		// Exported packs list "x.png.import"/".remap" instead of "x.png": reduce to unique names.
		var names = new System.Collections.Generic.SortedSet<string>();
		foreach (string file in dir.GetFiles())
			names.Add(file.Replace(".import", "").Replace(".remap", ""));
		foreach (string name in names)
		{
			if (!name.EndsWith(".png")) continue;
			var hi = ResourceLoader.Load<Texture2D>("res://assets/grp2x/" + name);
			var img = hi?.GetImage();
			if (img == null) continue;
			var tex = ImageTexture.CreateFromImage(img);
			tex.SetSizeOverride(new Vector2I(img.GetWidth() / 2, img.GetHeight() / 2));
			tex.TakeOverPath("res://assets/grp/" + name);
			HiRes["res://assets/grp/" + name] = tex;
			n++;
		}
		GD.Print($"Wa2DataPack: {n} x2 UI images in {Time.GetTicksMsec() - t0} ms (screen x{scale:0.00})");
	}

	private static string ReadText(string path) =>
		FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path).StripEdges() : "";
}
