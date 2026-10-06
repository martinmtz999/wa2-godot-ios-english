using Godot;

// The save slots' number font. sys_01013.png is an ASCII grid from ' ' (0x20): 16 cells per row,
// 12x16 px each ('/' at row 0 col 15, "0123456789:" at the start of row 1; measured 2026-10-06).
// The generated sys_01013.fontdata mapped characters to the wrong cells (dates showed as wrong
// digits and dots), so the font is built from the grid instead.
public static class Wa2NumberFont
{
	private static FontFile _font;
	public static FontFile Get()
	{
		if (_font != null) return _font;
		var tex = ResourceLoader.Load<Texture2D>("res://assets/grp/sys_01013.png");
		var img = tex?.GetImage();
		if (img == null) return null;
		if (img.IsCompressed()) img.Decompress();
		var f = new FontFile { FixedSize = 16, Antialiasing = TextServer.FontAntialiasing.Gray };
		var size = new Vector2I(16, 0);
		f.SetTextureImage(0, size, 0, img);
		f.SetCacheAscent(0, 16, 14);
		f.SetCacheDescent(0, 16, 2);
		int scale = img.GetWidth() / 192;   // 1, or 2 for an x2 copy
		for (int c = 0x20; c < 0x40; c++)
		{
			int i = c - 0x20, col = i % 16, row = i / 16;
			int adv = c switch { ' ' => 6, ':' => 6, '/' => 10, _ => 11 };   // ink widths: digits 9-11, ':' 5, '/' 10
			f.SetGlyphAdvance(0, 16, c, new Vector2(adv, 0));
			f.SetGlyphOffset(0, size, c, new Vector2(0, -14));
			f.SetGlyphSize(0, size, c, new Vector2(12, 16));
			f.SetGlyphUVRect(0, size, c, new Rect2(col * 12 * scale, row * 16 * scale, 12 * scale, 16 * scale));
			f.SetGlyphTextureIdx(0, size, c, 0);
		}
		_font = f;
		return f;
	}
	public static void Apply(Label l)
	{
		var f = Get();
		if (l == null || f == null) return;
		if (l.LabelSettings != null) { l.LabelSettings = (LabelSettings)l.LabelSettings.Duplicate(); l.LabelSettings.Font = f; }
		l.AddThemeFontOverride("font", f);
	}
}
