// using FFmpeg.AutoGen;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
public class TextParseResult
{
	public Vector2 EndPosition = Vector2.Zero;
	public bool ParseEnd = false;
	public bool WaitKey = false;
}
public class TextTag
{
	public int Type;
	public int Value;
}
public class CharRenderData
{
	public int X;
	public int Y;
	public int Size;
	public float Alpha = 1.0f;
	public char Chr;
	public CharRenderData(char ch, int drawX, int drawY, int size, int v)
	{
		X = drawX;
		Y = drawY;
		Chr = ch;
		Size = size;
		if (v >= 0 && v < 16)
		{
			Alpha = Mathf.Pow(2, v - 16);
		}
		else if (v < 0)
		{
			Alpha = 0.0f;
		}
	}
}
[GlobalClass]
public partial class Wa2Label : Node2D
{
	[Export]
	public Color Color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	[Export]
	public string Text;
	[Export]
	public Texture2D FontTexture;
	[Export]
	public Texture2D ShadowTexture;
	[Export]
	public bool Shadow = true;
	[Export]
	public int FontSize = 28;
	[Export]
	public int Rect1Size = 40;
	// [Export]
	// public int Rect2Size = 31;
	[Export]
	public int ParagraphSpacing = 13;
	[Export]
	public int LineSpacing = 0;
	[Export]
	public int MaxLines = 4;
	[Export]
	public int MaxChars = 28;
	// > 0: re-wrap the English text by words at this many half-width columns (wide phones give the
	// text box more width). The stored Text keeps the original line breaks (saves, backlog).
	public int WrapCols = 0;
	private string _shownFor, _shown;
	private int _shownCols;
	private string Shown
	{
		get
		{
			if (WrapCols <= 0 || Text == null) return Text;
			if (!ReferenceEquals(_shownFor, Text) || _shownCols != WrapCols)
			{
				_shown = Rewrap(Text, WrapCols);
				_shownFor = Text;
				_shownCols = WrapCols;
			}
			return _shown;
		}
	}
	// Width in half-width columns of one character (ASCII 1, anything else 2, as drawn).
	private static int Cols(char ch) => ch < 0x80 ? 1 : 2;
	// Visible width of the word starting at i (up to a space, line break or end); skips markup.
	private static int WordWidth(string s, int i)
	{
		int w = 0;
		while (i < s.Length && s[i] != ' ')
		{
			if (s[i] == '\\') { if (i + 1 < s.Length && s[i + 1] == 'n') break; i += 2; continue; }
			if (s[i] < 0x20 || s[i] == '^' || s[i] == '`' || s[i] == '~') { i++; continue; }
			w += Cols(s[i]);
			i++;
		}
		return w;
	}
	// The English script is pre-wrapped at ~54 columns, mixed with deliberate breaks (poems, pauses).
	// A break counts as automatic when the next word would not have fitted in 54 columns, or nearly
	// (>= 50) and the line does not end a sentence. The page is wrapped again at `cols` only when that
	// saves at least one line, and then as evenly as possible (no lone last word, never narrower than
	// the PC's 54); otherwise the PC's own wrapping is kept. Pages built from several \\k segments and
	// text with tags are left as they are, so text already shown never moves.
	public static string Rewrap(string s, int cols)
	{
		if (s.IndexOf('<') >= 0 || s.Contains("\\k")) return s;
		var paras = new List<string>();
		var sb = new System.Text.StringBuilder(s.Length);
		int orig = 0, origLines = 1;
		char last = ' ';
		for (int i = 0; i < s.Length; i++)
		{
			char ch = s[i];
			if (ch == '\\' && i + 1 < s.Length)
			{
				if (s[i + 1] == 'n')
				{
					int next = WordWidth(s, i + 2);
					int v = next == 0 ? 0 : orig + 1 + next;
					bool sentenceEnd = ".!?\"'…)」』～~".IndexOf(last) >= 0;
					i++;
					orig = 0;
					origLines++;
					if (v >= 54 || (v >= 50 && !sentenceEnd)) { sb.Append(' '); last = ' '; }
					else { paras.Add(sb.ToString()); sb.Clear(); }
					continue;
				}
				sb.Append(ch).Append(s[i + 1]);
				i++;
				continue;
			}
			sb.Append(ch);
			if (ch >= 0x20 && ch != '^' && ch != '`' && ch != '~') { orig += Cols(ch); last = ch; }
		}
		paras.Add(sb.ToString());
		int newLines = 0;
		foreach (var p in paras) newLines += Wrap(p, cols, null);
		if (newLines >= origLines) return s;
		var outp = new System.Text.StringBuilder(s.Length + 8);
		for (int k = 0; k < paras.Count; k++)
		{
			string p = paras[k];
			int n = Wrap(p, cols, null), w = cols;
			while (w > 54 && Wrap(p, w - 1, null) <= n) w--;
			if (k > 0) outp.Append("\\n");
			Wrap(p, w, outp);
		}
		return outp.ToString();
	}
	// Greedy word wrap of one paragraph at `cols`; appends to `o` when given; returns the line count.
	private static int Wrap(string p, int cols, System.Text.StringBuilder o)
	{
		int col = 0, lines = 1;
		for (int i = 0; i < p.Length; i++)
		{
			char ch = p[i];
			if (ch == ' ')
			{
				int next = WordWidth(p, i + 1);
				if (col > 0 && next > 0 && col + 1 + next > cols) { o?.Append("\\n"); col = 0; lines++; continue; }
				o?.Append(' ');
				col++;
				continue;
			}
			if (ch == '\\' && i + 1 < p.Length) { o?.Append(ch).Append(p[i + 1]); i++; continue; }
			o?.Append(ch);
			if (ch >= 0x20 && ch != '^' && ch != '`' && ch != '~') col += Cols(ch);
		}
		return lines;
	}
	// [Export]
	// public string EllipsisChar = "…";
	[Export]
	public Color ShadowColor = new Color(0, 0, 0, 0.9f); // 阴影颜色
														 // [Export]
														 // public int ShadowSize = 8; // 阴影大小

	public int Segment = 0;
	public int ProgressStep = 0;
	private List<CharRenderData> _renderDatas = new();
	// iOS 适配：字体图集（本体80.png）是上游未提交的本地资产，仓库内仅有源 TTF。
	// 图集缺失时回退到 TTF 渲染，避免 DrawChar 因空图集而无效/崩溃，且无需外部素材。
	private static FontFile _ttfFont;
	// English text from a vector font (Fira Mono Medium, OFL, fonts_en/) instead of the 40px bitmap
	// atlas, so it stays sharp when the 1280x720 game is shown at phone resolution. Sized to the PC
	// glyphs' cap height, which makes its natural advance exactly the PC's 14px at size 28.
	// Off by default: the user wants the PC's own letter shapes. `--text-font=vector` to compare.
	private static FontFile _enFont;
	private static readonly bool UseVectorText = OS.GetCmdlineUserArgs().Contains("--text-font=vector");
	// Default: the PC's own English glyphs, sharpened. assets/fonts2x/en_rows.png is atlas rows 0-3
	// upscaled x4 (tools/upscale-ui.sh); a size override keeps the original 3200x160 coordinates, and
	// mipmaps keep it smooth when drawn smaller; cells are repacked 20 per row (see the tool). Used on screens >= 1.2x 720p unless --text-font=bitmap.
	private static Texture2D _enHi;
	private static bool _enHiTried;
	private static Texture2D EnglishGlyphs(Texture2D fallback)
	{
		if (!_enHiTried)
		{
			_enHiTried = true;
			var args = OS.GetCmdlineUserArgs();
			float scale = DisplayServer.WindowGetSize().Y / 720f;
			bool force = args.Contains("--ui-hires=1"), off = args.Contains("--text-font=bitmap") || args.Contains("--ui-hires=0");
			if (!off && (force || scale >= 1.2f) && ResourceLoader.Exists("res://assets/fonts2x/en_rows.png"))
			{
				var img = ResourceLoader.Load<Texture2D>("res://assets/fonts2x/en_rows.png")?.GetImage();
				if (img != null)
				{
					img.GenerateMipmaps();
					var t = ImageTexture.CreateFromImage(img);
					t.SetSizeOverride(new Vector2I(800, 640));   // 20 x 16 cells of 40px
					_enHi = t;
				}
			}
		}
		return _enHi ?? fallback;
	}
	// Dark rim like the PC atlas glyphs have (keeps text readable over bright CGs); `--text-outline=N`.
	private static readonly float TextOutline = float.TryParse(OS.GetCmdlineUserArgs()
		.FirstOrDefault(a => a.StartsWith("--text-outline="))?["--text-outline=".Length..],
		System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float o) ? o : 3f;
	public override void _Ready()
	{
		if (Wa2EngineMain.Engine.Lang == Wa2EngineMain.Language.JP)
		{
			
			FontTexture = ResourceLoader.Load<Texture2D>("res://assets/fonts/jp/本体80.png");
			ShadowTexture = ResourceLoader.Load<Texture2D>("res://assets/fonts/jp/袋影80.png");
		}
		else
		{
			FontTexture = ResourceLoader.Load<Texture2D>("res://assets/fonts/cn/本体80.png");
			ShadowTexture = ResourceLoader.Load<Texture2D>("res://assets/fonts/cn/袋影80.png");
		}
		// 图集缺失（上游未提交的本地资产）→ 回退到仓库内源 TTF（AlibabaPuHuiTi）
		if (FontTexture == null)
		{
			_ttfFont = ResourceLoader.Load<FontFile>("res://assets/fonts/AlibabaPuHuiTi-3-65-Medium.ttf");
		}
	}

	public override void _Draw()
	{
		foreach (CharRenderData r in _renderDatas)
		{
			DrawChar(r);
		}
	}
	// public override void _PhysicsProcess(double delta)
	// {

	// }
	public void Clear()
	{
		_renderDatas.Clear();
	}
	public TextParseResult Update(int progress)
	{
		//遍历所有字符
		_renderDatas.Clear();
		int Speed = 10;
		int curprogress = 0;
		int waitprogress = 0;
		int curSegment = 0;
		int modFontSize = FontSize;
		Stack<TextTag> tagList = new();
		int drawX = 0;
		int drawY = 0;
		int lastDrawX = 0;
		bool endedWithBreak = false;
		TextParseResult r = new();
		string Text = Shown;
		// int drawX = 0;
		// int drawY = 0;
		for (int i = 0; i < Text.Length; i++)
		{
			//当前进度超过目标进度时
			if (r.WaitKey)
			{
				break;
			}
			if (curprogress >= progress && progress != -1)
			{
				if (curSegment >= Segment)
				{
					// r.ParseEnd = true;

					break;
				}
			}
			if (waitprogress > 0)
			{
				curprogress += waitprogress;
				waitprogress = 0;
			}
			switch (Text[i])
			{
				case (char)0:
					break;
				case (char)0xa:
					break;
				case '<':
					if (tagList.Count < 15)
					{
						TextTag tag = new();
						switch (Text[++i])
						{
							case 'A':
							case 'a':
								tag.Type = 6;
								tagList.Push(tag);
								break;
							case 'B':
							case 'b':
								tag.Type = 2;
								tagList.Push(tag);
								break;
							case 'C':
							case 'c':
								tag.Type = 1;
								tagList.Push(tag);
								break;
							case 'D':
							case 'd':
								tag.Type = 0;
								tagList.Push(tag);
								break;
							case 'E':
							case 'G':
							case 'H':
							case 'I':
							case 'J':
							case 'L':
							case 'M':
							case 'N':
							case 'O':
							case 'P':
							case 'Q':
							case 'T':
							case 'U':
							case 'V':
							case 'X':
							case 'Y':
							case 'Z':
							case '[':
							case '\\':
							case ']':
							case '^':
							case '_':
							case '`':
							case 'e':
							case 'g':
							case 'h':
							case 'i':
							case 'j':
							case 'l':
							case 'm':
							case 'n':
							case 'o':
							case 'p':
							case 'q':
							case 't':
							case 'u':
							case 'v':
								break;
							case 'F':
							case 'f':
								tag.Type = 4;
								tag.Value = FontSize;
								i++;
								modFontSize = ParseDecimalDigits(Text, ref i);
								tagList.Push(tag);
								break;
							case 'K':
							case 'k':
								tag.Type = 3;
								tagList.Push(tag);
								break;
							case 'R':
							case 'r':
								//记录当前绘制的位置方便下次遇到|符号时绘制注音字符
								tag.Type = 5;
								tag.Value = drawX;
								tagList.Push(tag);
								break;
							case 'S':
							case 's':
								tag.Type = 7;
								tag.Value = Speed;
								i++;
								switch (ParseDecimalDigits(Text, ref i))
								{
									case 0:
										Speed = 100;
										break;
									case 1:
										Speed = 80;
										break;
									case 2:
										Speed = 60;
										break;
									case 3:
										Speed = 40;
										break;
									case 4:
										Speed = 20;
										break;
									case 5:
										Speed = 10;
										break;
									case 6:
										Speed = 6;
										break;
									case 7:
										Speed = 4;
										break;
									case 8:
										Speed = 2;
										break;
									case 9:
										Speed = 1;
										break;
									case 10:
										Speed = 0;
										break;
								}
								tagList.Push(tag);
								break;
							case 'W':
							case 'w':
								tag.Type = 8;
								i++;
								waitprogress = ParseDecimalDigits(Text, ref i);
								tagList.Push(tag);
								break;
						}
					}
					break;
				case '>':
					if (tagList.Count > 0)
					{
						TextTag tag = tagList.Pop();
						switch (tag.Type)
						{
							case 1:
							case 2:
								break;
							case 3:
							case 5:
								break;
							case 4:
								modFontSize = tag.Value;
								break;
							case 6:
								break;
							case 7:
								Speed = tag.Value;
								break;
						}
					}
					break;
				case '\\':
					switch (Text[++i])
					{
						case '<':
						case '>':
						case '\\':
						case '^':
						case '|':
						case '~':
							break;
						case '=':
						case '?':
						case '@':
						case 'A':
						case 'B':
						case 'C':
						case 'D':
						case 'E':
						case 'F':
						case 'G':
						case 'H':
						case 'I':
						case 'J':
						case 'K':
						case 'L':
						case 'M':
						case 'N':
						case 'O':
						case 'P':
						case 'Q':
						case 'R':
						case 'S':
						case 'T':
						case 'U':
						case 'V':
						case 'W':
						case 'X':
						case 'Y':
						case 'Z':
						case '[':
						case ']':
						case '_':
						case '`':
						case 'a':
						case 'b':
						case 'c':
						case 'd':
						case 'e':
						case 'f':
						case 'g':
						case 'h':
						case 'i':
						case 'j':
						case 'l':
						case 'm':
						case 'o':
						case 'p':
						case 'q':
						case 'r':
						case 's':
						case 't':
						case 'u':
						case 'v':
						case 'w':
						case 'x':
						case 'y':
						case 'z':
						case '{':
						case '}':
							break;
						case 'k':
							if (curSegment >= Segment && Segment != -1)
							{
								// if (progress != -1)
								// {
								r.WaitKey = true;
								// }

								// break;
								// r.ParseEnd = true;
							}
							curSegment++;
							break;
						case 'n':
							lastDrawX = drawX;
							endedWithBreak = true;
							if (drawX > 0)
							{
								drawY += LinePitch;
								drawX = 0;
							}

							break;
					}
					break;
				case '^':
					break;
				case '`':
					break;
				case '|':
					//标签类型为5时绘制注音字符
					if (tagList.Count > 0 && tagList.Peek().Type == 5)
					{
						int x = tagList.Peek().Value;
						int y = drawY - 13;
						i++;
						while (Text[i] != '>')
						{

							if (curSegment >= Segment)
							{
								_renderDatas.Add(new CharRenderData(Text[i], x + (FontSize - FontSize / 2) / 2, y, FontSize / 2, progress - curprogress));

							}
							else
							{
								_renderDatas.Add(new CharRenderData(Text[i], x + (FontSize - FontSize / 2) / 2, y, FontSize / 2, 16));
							}
							x += FontSize + LineSpacing;
							i++;
						}
					}
					break;
				case '~':
					break;
				default:
					int advance = modFontSize + LineSpacing;
					int wrapAt = (MaxChars - 1) * (FontSize + LineSpacing);
					if (IsHalfWidth(Text[i]))
					{
						advance = modFontSize / 2 + LineSpacing;
						wrapAt = MaxChars * (FontSize + LineSpacing) - advance;
					}
					if (curSegment >= Segment)
					{
						curprogress += Speed / 10;
						int v = 16;
						if (progress >= 0)
						{
							v = progress - curprogress;
						}
						_renderDatas.Add(new CharRenderData(Text[i], drawX, drawY, modFontSize, v));
					}
					else
					{
						_renderDatas.Add(new CharRenderData(Text[i], drawX, drawY, modFontSize, 16));
					}
					lastDrawX = drawX;
					endedWithBreak = false;
					if (drawX >= wrapAt)
					{
						drawY += LinePitch;
						drawX = 0;
					}
					else
					{
						drawX += advance;
					}
					break;
			}
		}

		if (drawX != 0 || drawY == 0 || drawX > (MaxChars + 1) * (FontSize + LineSpacing))
		{
			r.EndPosition = new Vector2(drawX, drawY);

		}
		else
		{
			// After an explicit line break the PC puts the icon right after the last character
			// (measured: 28px further left than lastDrawX + FontSize, 1001 line 2).
			int gap = Wa2EngineMain.EnglishPatch && endedWithBreak ? 0 : FontSize;
			r.EndPosition = new Vector2(lastDrawX + gap, drawY - LinePitch);

		}

		// The PC English patch places the click-wait icon 3px right and 9px up from here.
		if (Wa2EngineMain.EnglishPatch)
			r.EndPosition += new Vector2(3, -9);
		r.ParseEnd = curprogress <= (progress - 16);
		QueueRedraw();
		return r;
	}
	// English patch, matched against the PC game (research/02-english-text.md): single-byte
	// (ASCII) characters are half-width, 14px at size 28; lines are 1px further apart.
	private static bool IsHalfWidth(char ch) => Wa2EngineMain.EnglishPatch && ch < 0x80;
	private int LinePitch => FontSize + ParagraphSpacing + (Wa2EngineMain.EnglishPatch ? 1 : 0);

	// en.pak redrew atlas rows 0-3 (ASCII, punctuation, '…') and left their shadow-atlas cells
	// empty; rows 4+ are the original Japanese glyphs. The PC draws those English cells whole at
	// 34px (size 28), offset (-3,-10), with a solid gray copy 2px down-right as the shadow. We use
	// 1px: the user preferred the lighter look over an exact match.
	private const int EnglishAtlasRows = 4;
	// English shadow: offset in px at size 28 and opacity; `--shadow=OFFSET,ALPHA` overrides.
	private static readonly float[] EnShadow = ParseShadow();
	private static float[] ParseShadow()
	{
		string a = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--shadow="))?["--shadow=".Length..];
		var p = a?.Split(',');
		var ci = System.Globalization.CultureInfo.InvariantCulture;
		if (p?.Length == 2 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, ci, out float o) && float.TryParse(p[1], System.Globalization.NumberStyles.Float, ci, out float al))
			return [o, al];
		return [0.5f, 1f];   // half the earlier offset: matches the PC's thin edge (user, 2026-10-05)
	}
	private void DrawEnglishCell(CharRenderData r, int cellX, int cellY)
	{
		float k = r.Size / 28f;
		Rect2 src = new(new Vector2(cellX, cellY) * Rect1Size, new Vector2(Rect1Size, Rect1Size));
		Rect2 dst = new(new Vector2(r.X - 3 * k, r.Y - 10 * k), new Vector2(34 * k, 34 * k));
		// '…' is stretched to 40px wide at the usual 34px height, offset x -4: measured from the PC's
		// dots (9px apart as in the atlas, same size and height as other English glyphs; at 34x34
		// they were 7.6 apart, at 40x40 too big and 2-3px low). Other full-width characters in rows
		// 0-3 are not measured yet and keep the 34px rule.
		if (r.Chr == '\u2026')
			dst = new(new Vector2(r.X - 4 * k, r.Y - 10 * k), new Vector2(40 * k, 34 * k));
		Texture2D glyphs = EnglishGlyphs(FontTexture);
		if (!ReferenceEquals(glyphs, FontTexture))
		{
			int i = cellY * 80 + cellX;                  // repacked: 20 cells per row
			src = new(new Vector2(i % 20, i / 20) * Rect1Size, new Vector2(Rect1Size, Rect1Size));
		}
		if (!ReferenceEquals(glyphs, FontTexture)) TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		if (Shadow)
		{
			Rect2 shadow = new(dst.Position + new Vector2(1, 1) * EnShadow[0] * k, dst.Size);
			DrawTextureRectRegion(glyphs, shadow, src, new Color(68 / 255f, 68 / 255f, 68 / 255f, r.Alpha * EnShadow[1]));
		}
		DrawTextureRectRegion(glyphs, dst, src, new Color(Color.R, Color.G, Color.B, r.Alpha));
	}

	private void DrawEnglishVector(CharRenderData r)
	{
		float k = r.Size / 28f;
		int size = Mathf.RoundToInt(r.Size * 0.78f);   // cap height of the PC glyphs without their soft edge
		float baseline = r.Y + 16 * k;                       // atlas baseline: row 30.5 of the 40px cell at 34px, from y-10
		Color fg = new(Color.R, Color.G, Color.B, r.Alpha);
		Color sh = new(68 / 255f, 68 / 255f, 68 / 255f, r.Alpha);
		void Glyph(string ch, float centerX)
		{
			float w = _enFont.GetStringSize(ch, HorizontalAlignment.Left, -1, size).X;
			var pos = new Vector2(centerX - w / 2, baseline);
			if (Shadow) DrawString(_enFont, pos + new Vector2(1, 1) * k, ch, HorizontalAlignment.Left, -1, size, sh);
			int ol = Mathf.RoundToInt(TextOutline * k);
			if (ol > 0) DrawStringOutline(_enFont, pos, ch, HorizontalAlignment.Left, -1, size, ol, new Color(0.16f, 0.16f, 0.16f, 0.85f * r.Alpha));
			DrawString(_enFont, pos, ch, HorizontalAlignment.Left, -1, size, fg);
		}
		if (r.Chr == '\u2026')
		{
			// PC '…' spans the full-width cell with dots 9px apart (x+7, +16, +25 at size 28).
			foreach (float dx in new[] { 7f, 16f, 25f }) Glyph(".", r.X + dx * k);
			return;
		}
		float advance = r.Chr < 0x80 ? 14 * k : 28 * k;
		Glyph(r.Chr.ToString(), r.X + advance / 2);
	}

	public int ParseDecimalDigits(string input, ref int index)
	{
		int result = 0;

		while (index < input.Length && char.IsDigit(input[index]))
		{
			result = result * 10 + (input[index] - '0');
			index++;
		}
		if (index >= input.Length || input[index] != ':')
			index = Math.Max(0, index - 1);

		return result;
	}
	public void DrawChar(CharRenderData r)
	{

		if (!Wa2Def.FontMap.ContainsKey(r.Chr))
		{
			GD.Print($"未知字符 '{r.Chr}'");
			return;
		}
		int pos = Wa2Def.FontMap[r.Chr];
		if (pos >= 0)
		{
			// iOS 回退：字体图集缺失时用仓库内 TTF 渲染（源字体 AlibabaPuHuiTi）
			if (FontTexture == null || ShadowTexture == null)
			{
				if (_ttfFont != null)
				{
					float baseline = r.Y + r.Size * 0.85f;
					string ch = r.Chr.ToString();
					if (Shadow)
						DrawString(_ttfFont, new Vector2(r.X + r.Size * 0.06f, baseline + r.Size * 0.06f), ch, HorizontalAlignment.Left, -1, r.Size, new Color(ShadowColor.R, ShadowColor.G, ShadowColor.B, ShadowColor.A * r.Alpha));
					DrawString(_ttfFont, new Vector2(r.X, baseline), ch, HorizontalAlignment.Left, -1, r.Size, new Color(Color.R, Color.G, Color.B, r.Alpha));
				}
				return;
			}

			int x = pos % 80;
			int y = pos / 80;
			if (Wa2EngineMain.EnglishPatch && y < EnglishAtlasRows)
			{
				// Loaded here, not at init: EnglishPatch is only known after the paks are found.
				if (UseVectorText && _enFont == null)
					_enFont = ResourceLoader.Load<FontFile>("res://fonts_en/FiraMono-Medium.ttf");
				if (_enFont != null) { DrawEnglishVector(r); return; }
				DrawEnglishCell(r, x, y);
				return;
			}
			Rect2 rect = new(new Vector2(r.X, r.Y), new Vector2(r.Size, r.Size));
			Rect2 rect2 = new(new Vector2(r.X - r.Size / 28f * 2, r.Y - r.Size / 28f * 2), new Vector2(r.Size / 28f * 32, r.Size / 28f * 32));
			Rect2 srcRect = new(new Vector2(x, y) * Rect1Size + new Vector2(4, 4), new Vector2(28, 28));
			Rect2 shadowRect = new(new Vector2(x, y) * Rect1Size + new Vector2(2, 2), new Vector2(32, 32));
			if (Shadow)
			{
				DrawTextureRectRegion(ShadowTexture, rect2, shadowRect, new Color(0.15f, 0.15f, 0.15f, r.Alpha));
				DrawTextureRectRegion(FontTexture, rect, srcRect, new Color(Color.R, Color.G, Color.B, r.Alpha));
			}
			else
			{
				DrawTextureRectRegion(FontTexture, rect, srcRect, new Color(Color.R, Color.G, Color.B, r.Alpha));
			}



		}
	}
	public TextParseResult SetText(string text, int progress = -1)
	{
		Text = text;
		return Update(progress);
	}
}