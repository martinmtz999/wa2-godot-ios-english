using Godot;
using System.Linq;
using System;
//对话框状态
public partial class Wa2AdvMain : Control
{

	// public bool NovelMode = false;
	[Export]
	public Wa2Button BackLogButton;
	[Export]
	public TextureRect IsReadTexture;
	[Export]
	public TextureRect AutoModeTexture;
	[Export]
	public TextureRect SkipModeTexture;
	[Export]
	public Wa2Button SaveButton;
	[Export]
	public Wa2Button LoadButton;
	[Export]
	public Wa2Button OptionButton;
	[Export]
	public Wa2Button AutoButton;
	[Export]
	public Wa2Button SkipButton;
	[Export]
	public Wa2Button OffButton;
	[Export]
	public VBoxContainer SelectMessageContainer;
	[Export]
	public Wa2Label NameLabel;
	[Export]
	public Wa2Label TextLabel;
	[Export]
	public AnimatedSprite2D WaitSprite;
	[Export]
	public TextureRect Window;
	[Export]
	public Control MessageBox;
	[Export]
	public ColorRect Mask;
	public int PageAnime = 2;
	public bool WaitKey;
	// [Export]
	// public Control DebugUi;
	// 	[Export]
	// public bool Debug;
	private Wa2EngineMain _engine;
	public string OriginText = "";
	public string CurName = "";
	public int TextProgress = 0;
	public int ParseMode = 2;
	public enum AdvState
	{
		PARSE_TEXT = 0,
		FADE_IN = 1,
		FADE_OUT = 2,
		WAIT_CLICK = 3,
		END = 4,
		NONE = 5,
		HIDE = 6
	}
	public AdvState State = AdvState.END;
	public void UpdateWindowAlpha()
	{
		if (_engine.BgInfo.Type == 1)
		{
			SetWindowAlpha(_engine.Prefs.GetConfig("win_alpha_vis"));
		}
		else
		{
			if (_engine.NovelMode)
			{
				Mask.SelfModulate = new Color(1, 1, 1, _engine.Prefs.GetConfig("win_alpha_novel") / 256f);
			}
			else
			{
				SetWindowAlpha(_engine.Prefs.GetConfig("win_alpha"));
			}
		}
	}
	// public bool Active;
	public void SetNovelMode(bool flag)
	{
		_engine.NovelMode = flag;
		if (flag)
		{
			MessageBox.Hide();
			TextLabel.Position = new Vector2(80, 40);
			Mask.Show();
			TextLabel.MaxChars = 39;
		}
		else
		{
			MessageBox.Show();
			Mask.Hide();
			TextLabel.FontSize = 28;
			TextLabel.MaxChars = 28;
			TextLabel.Position = new Vector2(278, 562);
			ApplyTextScale();
		}
	}
	// Larger dialogue for small phone screens (English mode). Scales the text, line spacing, name
	// and the message box together about the box's bottom centre (640, 704), so the layout keeps
	// its proportions and the text stays inside the box; the box grows upward. 1 = PC layout;
	// default 1.35, chosen by the user for a 6.1" phone (2026-10-05). --text-scale=K overrides.
	// The English lines are pre-wrapped at <= 55 half-width chars and pages hold <= 3 lines
	// (99.97% of pages), which is what the box was sized for.
	public static float TextScale = float.TryParse(OS.GetCmdlineUserArgs()
		.FirstOrDefault(a => a.StartsWith("--text-scale="))?["--text-scale=".Length..],
		System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ts) ? ts : 1.35f;
	// Wide screens move the toolbar into the side strips (Wa2Wide), freeing the bottom of the box:
	// the box, name and text then sit this much lower. Set by Wa2Wide.
	public static float WideDrop = 0;
	private static Vector2 ScaleAboutBoxBottom(Vector2 p, float k) => new Vector2(640, 704 + WideDrop) + (p - new Vector2(640, 704)) * k;
	public void ApplyTextScale()
	{
		if (!Wa2EngineMain.EnglishPatch || _engine.NovelMode) return;
		float k = TextScale;
		TextLabel.FontSize = Mathf.RoundToInt(28 * k);
		TextLabel.ParagraphSpacing = Mathf.RoundToInt(13 * k);
		TextLabel.Position = ScaleAboutBoxBottom(new Vector2(278, 562), k);
		NameLabel.FontSize = Mathf.RoundToInt(28 * k);
		NameLabel.Position = ScaleAboutBoxBottom(new Vector2(278, 521), k);
		// The box art is an oval ring (sys_00000) over a soft fill (sys_00001). Its height follows the
		// text, but its width is capped at 1.2 so the whole oval, ends included, stays on screen
		// (opaque part 1061px wide x 1.2 = 1273 of 1280); scaled uniformly it ran off both sides.
		MessageBox.PivotOffset = new Vector2(624, 208);
		MessageBox.Scale = new Vector2(Mathf.Min(k, 1.2f), k);
		MessageBox.Position = new Vector2(16, 496 + WideDrop);
		// Toolbar: slightly larger for touch when the text is enlarged, about its bottom-right corner.
		var bar = GetNodeOrNull<Control>("HBoxContainer");
		if (bar != null)
		{
			float b = k > 1f ? 1.15f : 1f;
			bar.PivotOffset = new Vector2(bar.Size.X, 48);
			bar.Scale = new Vector2(b, b);
		}
	}
	public void Init(Wa2EngineMain e)
	{
		_engine = e;
		Visible = false;
		WaitSprite.Hide();
		// Measured against the PC English patch: names are 1px wider-spaced and 3px higher.
		if (Wa2EngineMain.EnglishPatch)
		{
			NameLabel.LineSpacing = 1;
			NameLabel.Position = new Vector2(278, 521);
		}
		Modulate = new Color(1, 1, 1, 0);
		LoadButton.ButtonDown += OnLoadButtonDown;
		SaveButton.ButtonDown += OnSaveButtonDown;
		AutoButton.ButtonDown += OnAutoButtonDown;
		SkipButton.ButtonDown += OnSkipButtonDown;
		OffButton.ButtonDown += OnOffButtonDown;
		OptionButton.ButtonDown += OnOptionButtonDown;
		BackLogButton.ButtonDown += OnBackLogButtonDown;
		for (int i = 0; i < SelectMessageContainer.GetChildCount(); i++)
		{
			int idx = i;
			SelectMessageContainer.GetChild<SelectMessage>(i).ButtonDown += () => OnSelectMessageButtonDown(idx);
		}
	}
	public void OnOptionButtonDown()
	{
		_engine.UiMgr.OpenOptionsMenu();
	}
	public void OnAutoButtonDown()
	{
		_engine.AutoMode = !_engine.AutoMode;
		if (_engine.AutoMode)
		{
			_engine.StopSkip();
			_engine.AutoModeStart();
		}
	}
	public void OnBackLogButtonDown()
	{
		if (_engine.Backlogs.Count <= 0)
		{
			return;
		}
		if (_engine.NovelMode)
		{
			_engine.UiMgr.OpenNovelBackLog();
		}
		else
		{
			_engine.UiMgr.OpenBackLog();
		}

	}
	public void OnSkipButtonDown()
	{
		_engine.AutoTimer.DeActive();
		if (!_engine.HasReadMessage && _engine.Prefs.GetConfig("msg_cut_optin") == 0)
		{
			_engine.SkipMode = false;
		}
		else
		{
			_engine.SkipMode = !_engine.SkipMode;
		}
	}
	public void OnSelectMessageButtonDown(int idx)
	{

		for (int i = 0; i < SelectMessageContainer.GetChildCount(); i++)
		{
			SelectMessageContainer.GetChild<SelectMessage>(i).Hide();
		}
		_engine.Script.Args[^1].Set(idx);
		_engine.WirtSysFlag(_engine.SelectIdx, (1 << idx) | _engine.ReadSysFlag(_engine.SelectIdx));
		_engine.SelectItems.Clear();
		SelectMessageContainer.Hide();
		State = AdvState.END;
	}

	public void OnOffButtonDown()
	{
		if (State == AdvState.WAIT_CLICK)
		{
			HideAdv();
		}

	}
	public void HideAdv()
	{
		State = AdvState.HIDE;
		_engine.StopSkip();
		_engine.StopAutoMode();
		_engine.AdvMain.Hide();
	}
	public void ClearText()
	{
		TextLabel.SetText("");
		NameLabel.SetText("");
		TextProgress = 0;
		TextLabel.Segment = 0;
	}
	public void SetDemoMode(bool b)
	{
		if (b)
		{
			LoadButton.Visible = false;
			SaveButton.Visible = false;
			AutoButton.Visible = false;
			OffButton.Visible = false;
			BackLogButton.Visible = false;
			OptionButton.Visible = false;
		}
		else
		{
			if (_engine.ReplayMode == 0)
			{
				LoadButton.Visible = true;
				SaveButton.Visible = true;
			}

			AutoButton.Visible = true;
			OffButton.Visible = true;
			BackLogButton.Visible = true;
			OptionButton.Visible = true;

		}
	}

	public void Update()
	{
		if (State == AdvState.WAIT_CLICK && !SelectMessageContainer.Visible && !_engine.CanSkip() && !_engine.AutoMode && !_engine.DemoMode)
		{
			WaitSprite.Show();
		}
		else
		{
			WaitSprite.Hide();
		}
		switch (State)
		{
			case AdvState.PARSE_TEXT:
				TextParseResult r;
				int magWait = _engine.Prefs.GetMsgWait();
				if (_engine.CanSkip() || _engine.ClickedInWait || magWait == 0)
				{
					r = TextLabel.Update(9999);
				}
				else
				{
					if (_engine.DemoMode)
					{
						TextProgress += 2;
					}
					else
					{
						TextProgress += magWait;
						// GD.Print(TextProgress);
					}
					r = TextLabel.Update(TextProgress);
				}
				if (r.ParseEnd)
				{
					if (r.WaitKey)
					{
						TextLabel.Segment++;
						TextProgress = 0;
						State = AdvState.WAIT_CLICK;
						WaitKey = true;

					}
					else if (ParseMode == 2)
					{
						TextLabel.Segment++;
						TextProgress = 0;
						State = AdvState.WAIT_CLICK;

						WaitKey = false;
					}
					else if (ParseMode == 1)
					{
						TextLabel.Segment++;
						TextProgress = 0;
						State = AdvState.END;
						WaitKey = false;
					}
					if (_engine.Backlogs.Count > 0)
					{
						_engine.Backlogs[^1].Segment = Math.Max(0, TextLabel.Segment - 1);
					}
				}

				break;
			case AdvState.FADE_IN:
				break;
			case AdvState.FADE_OUT:
				break;
			case AdvState.WAIT_CLICK:
				r = TextLabel.Update(0);
				WaitSprite.Position = TextLabel.Position + r.EndPosition;
				if (!WaitKey)
				{
					WaitSprite.Play("page2");
				}
				else
				{
					WaitSprite.Play("page1");
				}
				break;
			case AdvState.END:

				break;
			case AdvState.HIDE:
				break;
		}

		AutoModeTexture.Visible = _engine.AutoMode;
		SkipModeTexture.Visible = _engine.SkipMode;
		IsReadTexture.Visible = _engine.HasReadMessage;
	}
	public void Clear()
	{
		ClearText();
		// TextLabel.Segment = 0;
		Modulate = new Color(1, 1, 1, 0);
	}
	public void AdvShow(bool fade = true)
	{

		if (!Visible || Modulate.A < 1)
		{
			// TextLabel.Clear();
			if (fade)
			{
				AdvFade(0.2f, true);
			}
			else
			{
				Visible = true;
				Modulate = new Color(1, 1, 1, 1);
				State = AdvState.WAIT_CLICK;
			}
		}
		else
		{
			State = AdvState.PARSE_TEXT;
			NameLabel.Update(-1);

		}
		if (_engine.ReplayMode > 0 ||_engine.DemoMode)
		{
			SaveButton.Hide();
			LoadButton.Hide();
		}
		else
		{
			SaveButton.Show();
			LoadButton.Show();
		}
	}
	public void AdvHide(float time = 0.2f)
	{
		AdvFade(time, false);
	}
	public void NeveHide(float time)
	{
		_engine.AnimatorMgr.AddFeadAnimation(Mask, time, 0.0f);
	}
	public void NeveShow(float time)
	{
		_engine.AnimatorMgr.AddFeadAnimation(Mask, time, 1.0f);
	}
	public void AdvFade(float time, bool fadein)
	{
		_engine.AnimatorMgr.AddAdvFeadAnimation(this, time, fadein);
	}
	public void NovelHide(float time)
	{
		_engine.AnimatorMgr.AddFeadAnimation(Mask, time, 0f);
		_engine.AnimatorMgr.AddFeadAnimation(TextLabel, time, 0f);
	}
	public void NovelShow(float time)
	{
		_engine.AnimatorMgr.AddFeadAnimation(Mask, time, 1f);
		_engine.AnimatorMgr.AddFeadAnimation(TextLabel, time, 1f);
	}
	public void ShowText(bool fade = true)
	{


		// ClearText();
		// TextLabel.Modulate=new Color(1, 1, 1, 1);
		// Mask.Modulate = new Color(1, 1, 1, 1);
		if (_engine.NovelMode && Mask.Modulate.A < 1)
		{
			NovelShow(0.2f);
		}
		if (!fade)
		{
			NameLabel.Update(-1);
		}
		AdvShow(fade);
		if (_engine.GetReadMessage(_engine.CurMessageIdx))
		{
			_engine.HasReadMessage = true;
		}
		else
		{
			_engine.HasReadMessage = false;
			_engine.SetReadMessage(_engine.CurMessageIdx);
			if (_engine.Prefs.GetConfig("msg_cut_optin") == 0)
			{
				_engine.StopSkip();
			}
		}
		UpdateWindowAlpha();
	}
	public void OnSaveButtonDown()
	{
		if ((State != AdvState.WAIT_CLICK && !_engine.AdvMain.SelectMessageContainer.Visible) || _engine.WaitTimer.IsActive())
		{
			return;
		}
		_engine.UiMgr.OpenSaveMenu();
	}
	public void OnLoadButtonDown()
	{
		if ((State != AdvState.WAIT_CLICK && !_engine.AdvMain.SelectMessageContainer.Visible) || _engine.WaitTimer.IsActive())
		{
			return;
		}
		_engine.UiMgr.OpenLoadMenu();
	}
	public void SetWindowAlpha(int alpha)
	{
		Window.Modulate = new Color(1, 1, 1, alpha / 256f);
	}
}