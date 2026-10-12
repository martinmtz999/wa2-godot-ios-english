using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Unattended test mode (research/05-pc-comparison.md). Combine with --start=<script>:
//   godot --path . -- --start=1001 --autoplay=20 --shots=/abs/dir [--settle=0.4]
// Advances by itself (no mouse, no focus needed), saves a frame from the renderer for every line
// as <script>_<idx>[_k].png, prints one progress line per step, and quits. Fails loudly: if no
// new line appears within 20 s it saves stuck.png, prints the engine state and exits with code 3.
public static class Wa2Autoplay
{
	static readonly string[] Args = OS.GetCmdlineUserArgs();
	static string Arg(string k) => Args.FirstOrDefault(a => a.StartsWith(k))?[k.Length..];
	public static readonly int Lines = int.TryParse(Arg("--autoplay="), out int n) ? n : 0;
	static readonly string Dir = Arg("--shots=");
	static readonly double Settle = double.TryParse(Arg("--settle="), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double s) ? s : 0.4;
	public static bool Enabled => Lines > 0;

	static int _done;
	static readonly int FilmFrames = int.TryParse(Arg("--film="), out int ff) ? ff : 0;
	// `--toggle-pictures-at=N`: after line N flip Options -> Pictures (Sharper/Original) and save the
	// same line again (<key>_2), to test the live reload. Writes phone.cfg: use a scratch XDG_DATA_HOME.
	static readonly int ToggleAt = int.TryParse(Arg("--toggle-pictures-at="), out int ta) ? ta : 0;
	static int _importRetries;
	static int _film; static string _filmKey;
	static double _settled, _idle, _nudge;
	static readonly HashSet<string> _keys = new();

	static void Out(string msg) { GD.Print("autoplay: " + msg); Wa2Trace.Log("autoplay", msg); }

	static void Finish(int code, string why)
	{
		Out($"{why} ({_done} lines)");
		Out($"image cache: {Wa2Resource.ImageCacheCount} images, {Wa2Resource.ImageCacheBytes / 1048576} MB");
		Wa2EngineMain.Engine.GetTree().Quit(code);
	}

	static void Save(string name)
	{
		if (string.IsNullOrEmpty(Dir)) return;
		DirAccess.MakeDirRecursiveAbsolute(Dir);
		Wa2EngineMain.Engine.GetViewport().GetTexture().GetImage().SavePng(Dir.PathJoin(name + ".png"));
	}

	// `--title-shot=FILE`: save the title screen once it has settled (4 s), then quit. For comparisons.
	static readonly string TitleShot = Arg("--title-shot=");
	static double _titleTime;
	static bool _opened;
	public static void TitleTick(double delta)
	{
		if (string.IsNullOrEmpty(TitleShot)) return;
		if (_opened && Args.Contains("--title-open=resume")) { _titleTime += delta; goto resume; }
		if (_opened && Args.Contains("--title-open=newgame"))
		{
			_titleTime += delta;
			if (_titleTime < 8) return;
			var e0 = Wa2EngineMain.Engine;
			e0.GetViewport().GetTexture().GetImage().SavePng(TitleShot);
			GD.Print($"autoplay: newgame saved state={e0.State} specials={e0.UiMgr.TitleMenu.SpecialButton.Visible} cc={e0.UiMgr.TitleMenu.CcButton.Visible} coda={e0.UiMgr.TitleMenu.CodeaButton.Visible}");
			e0.GetTree().Quit(0); return;
		}
		if (Wa2EngineMain.Engine.State != Wa2EngineMain.GameState.TITLE) return;
		_titleTime += delta;
		if (_titleTime < 4) return;
		resume:
		if (Args.Contains("--title-open=resume"))
		{
			var e1 = Wa2EngineMain.Engine;
			if (!_opened) { _opened = true; GD.Print($"autoplay: prompt shown = {e1.UiMgr.UiQueue.Peek() == e1.UiMgr.UIConfirm}"); e1.UiMgr.UIConfirm.OnConfirmBtnDown(); return; }
			if (_titleTime < 12) return;
			e1.GetViewport().GetTexture().GetImage().SavePng(TitleShot);
			GD.Print($"autoplay: resumed state={e1.State} script={e1.Script?.ScriptName} msg={e1.CurMessageIdx}");
			e1.GetTree().Quit(0); return;
		}
		if (Args.Contains("--title-open=newgame") && !_opened)
		{
			_opened = true;
			GD.Print($"autoplay: title specials={Wa2EngineMain.Engine.UiMgr.TitleMenu.SpecialButton.Visible}");
			Wa2EngineMain.Engine.UiMgr.TitleMenu.OnStartButtonDown();
			return;
		}
		// `--title-open=cg` / `--title-open=load`: open the CG gallery or the load menu, capture 3 s later
		if (Args.Contains("--title-open=cg") || Args.Contains("--title-open=load"))
		{
			if (!_opened)
			{
				_opened = true;
				if (Args.Contains("--title-open=load")) Wa2EngineMain.Engine.UiMgr.OpenLoadMenu();
				else Wa2EngineMain.Engine.UiMgr.OpenCGModeMenu();
				return;
			}
			if (_titleTime < 7) return;
		}
		Wa2EngineMain.Engine.GetViewport().GetTexture().GetImage().SavePng(TitleShot);
		GD.Print("autoplay: title saved");
		Wa2EngineMain.Engine.GetTree().Quit(0);
	}

	// `--gesture-test`: after 3 lines, inject touch gestures (as the phone would send them) and log
	// what happened: swipe down -> backlog, two-finger tap -> text box hidden, tap -> shown, tap -> next line.
	static readonly bool GestureTest = Args.Contains("--gesture-test");
	static int _gStep = -1; static double _gTime;
	static bool _skipPressed, _autoPressed;
	static int _optTextIdx, _savedCut;
	static void Touch(int index, bool down, Vector2 pos) =>
		Input.ParseInputEvent(new InputEventScreenTouch { Index = index, Pressed = down, Position = pos });
	static void Drag(Vector2 pos) => Input.ParseInputEvent(new InputEventScreenDrag { Index = 0, Position = pos });
	static bool GestureTick(double delta, Wa2EngineMain e)
	{
		if (!GestureTest || _done < 3) return false;
		if (_gStep < 0) { _gStep = 0; _gTime = 0; }
		_gTime += delta;
		var top = e.UiMgr.UiQueue.Peek()?.Name.ToString();
		switch (_gStep)
		{
			case 0:   // swipe down
				Touch(0, true, new Vector2(640, 250));
				for (int y = 270; y <= 430; y += 40) Drag(new Vector2(645, y));
				Touch(0, false, new Vector2(645, 430));
				_gStep = 1; _gTime = 0; return true;
			case 1:
				if (_gTime < 1.5) return true;
				Save("g1-swipe-down"); Out($"GESTURE swipe down -> top ui = {top} (want BackLog)");
				e.Back(); _gStep = 2; _gTime = 0; return true;
			case 2:
				if (_gTime < 1.5) return true;
				Out($"GESTURE back -> top ui = {e.UiMgr.UiQueue.Peek()?.Name}");
				Touch(0, true, new Vector2(500, 300)); Touch(1, true, new Vector2(760, 300));
				Touch(1, false, new Vector2(760, 300)); Touch(0, false, new Vector2(500, 300));
				_gStep = 3; _gTime = 0; return true;
			case 3:
				if (_gTime < 1.0) return true;
				Save("g2-two-finger"); Out($"GESTURE two-finger tap -> text box visible = {e.AdvMain.Visible} (want False)");
				Touch(0, true, new Vector2(640, 300)); Touch(0, false, new Vector2(640, 300));
				_gStep = 4; _gTime = 0; return true;
			case 4:
				if (_gTime < 1.0) return true;
				Save("g3-tap-show"); Out($"GESTURE tap -> text box visible = {e.AdvMain.Visible} (want True)");
				_gIdx = e.CurMessageIdx;
				Touch(0, true, new Vector2(640, 300)); Touch(0, false, new Vector2(640, 300));
				_gStep = 5; _gTime = 0; return true;
			case 5:
				if (_gTime < 2.5) return true;
				Out($"GESTURE tap -> message {_gIdx} -> {e.CurMessageIdx} (want it to advance)");
				if (e.AdvMain.BackLogButton.GetParent() == e.AdvMain.GetNode("HBoxContainer")) { Finish(0, "gesture test done"); _gStep = 9; return true; }
				// Side-strip checks (wide screens): tap the blurred strip, then the backlog button.
				_gIdx = e.CurMessageIdx;
				var strip = new Vector2(20, DisplayServer.WindowGetSize().Y * 0.5f);
				Touch(0, true, strip); Touch(0, false, strip);
				_gStep = 6; _gTime = 0; return true;
			case 6:
				if (_gTime < 2.5) return true;
				Save("g4-rail-tap");
				Out(Wa2Phone.AutoHide
					? $"RAIL tap on the strip -> message {_gIdx} -> {e.CurMessageIdx} (want same); buttons shown {Wa2Wide.ButtonsShown}"
					: $"RAIL tap on the strip -> message {_gIdx} -> {e.CurMessageIdx} (want it to advance)");
				Wa2Wide.ShowButtons();
				var b = e.AdvMain.BackLogButton;
				Vector2 c = b.GetViewport().GetFinalTransform() * (b.GetGlobalTransformWithCanvas() * (b.Size / 2));
				_gPending = c;
				_gStep = 61; _gTime = 0; return true;
			case 61:
				if (_gTime < 0.4) return true;
				Touch(0, true, _gPending); Touch(0, false, _gPending);
				_gStep = 7; _gTime = 0; return true;
			case 7:
				if (_gTime < 1.5) return true;
				Save("g5-rail-backlog"); Out($"RAIL backlog button -> top ui = {top} (want BackLogMenu)");
				e.Back(); _gStep = 20; _gTime = 0; return true;
			case 20:   // quick save via its side button
				if (_gTime < 1.5) return true;
				_qsIdx = e.CurMessageIdx;
				Wa2Quick.QuickSave(e);
				_gStep = 21; _gTime = 0; return true;
			case 21:
				if (_gTime < 2.0) return true;
				Out($"QUICK save at message {_qsIdx} -> slot 98 exists = {FileAccess.FileExists(e.SavPath + "sav98.sav")} (want True)");
				var strip2 = new Vector2(20, DisplayServer.WindowGetSize().Y * 0.5f);
				Touch(0, true, strip2); Touch(0, false, strip2);
				_gStep = 22; _gTime = 0; return true;
			case 22:
				if (_gTime < 2.5) return true;
				Out($"QUICK advanced to message {e.CurMessageIdx}");
				Wa2Quick.QuickLoad(e);
				_gStep = 23; _gTime = 0; return true;
			case 23:
				if (_gTime < 1.0) return true;
				Save("g6-quickload-confirm");
				if (e.UiMgr.UiQueue.Peek() == e.UiMgr.UIConfirm) e.UiMgr.UIConfirm.OnConfirmBtnDown();
				_gStep = 24; _gTime = 0; return true;
			case 24:
				if (_gTime < 3.0) return true;
				Out($"QUICK load -> message {e.CurMessageIdx} (want {_qsIdx}); top ui = {e.UiMgr.UiQueue.Peek()?.Name}");
				DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(e.SavPath + "sav99.sav"));
				e.Notification((int)Node.NotificationApplicationPaused);
				Out($"AUTO save on app pause -> slot 99 exists = {FileAccess.FileExists(e.SavPath + "sav99.sav")} (want True)");
				if (Wa2Feel.Force)
				{
					int f0 = Wa2Feel.Fired;
					Wa2Feel.OnSe(2210); int a = Wa2Feel.Fired - f0;          // everyday sound: no pulse
					Wa2Feel.OnSe(8271); int b2 = Wa2Feel.Fired - f0;         // impact: pulse
					Wa2Feel.OnSe(8405); int c2 = Wa2Feel.Fired - f0;         // second impact within 10 s: no pulse
					Out($"HAPTIC everyday={a} impact={b2} second-within-10s={c2} (want 0 1 1)");
				}
				_gStep = 30; _gTime = 0; return true;
			case 30:   // menu: open Save, tap an empty spot
				_gIdx = e.CurMessageIdx;
				e.UiMgr.OpenSaveMenu();
				_gStep = 31; _gTime = 0; return true;
			case 31:
				if (_gTime < 1.5) return true;
				var spot = e.GetViewport().GetFinalTransform() * (e.GetGlobalTransformWithCanvas() * new Vector2(640, 40));
				Touch(0, true, spot); Touch(0, false, spot);
				var strip3 = new Vector2(20, DisplayServer.WindowGetSize().Y * 0.5f);
				Touch(0, true, strip3); Touch(0, false, strip3);
				_gStep = 32; _gTime = 0; return true;
			case 32:
				if (_gTime < 2.5) return true;
				Save("g7-menu");
				Out($"MENU taps while Save is open -> message {_gIdx} -> {e.CurMessageIdx} (want same); text box visible = {e.AdvMain.Visible} (want False)");
				e.Back();
				_gStep = 33; _gTime = 0; return true;
			case 33:
				if (_gTime < 2.0) return true;
				Out($"MENU closed -> top ui = {e.UiMgr.UiQueue.Peek()?.Name}; text box visible = {e.AdvMain.Visible} (want True)");
				_gStep = 40; _gTime = 0; return true;
			case 40:   // hold 0.8 s -> Auto on
				Touch(0, true, new Vector2(640, 300)); _gStep = 41; _gTime = 0; return true;
			case 41:
				if (_gTime < 0.8) return true;
				Touch(0, false, new Vector2(640, 300)); _gStep = 42; _gTime = 0; return true;
			case 42:
				if (_gTime < 0.5) return true;
				Out($"HOLD -> auto = {e.AutoMode} (want True)");
				Touch(0, true, new Vector2(640, 300)); _gStep = 43; _gTime = 0; return true;
			case 43:
				if (_gTime < 0.8) return true;
				Touch(0, false, new Vector2(640, 300)); _gStep = 44; _gTime = 0; return true;
			case 44:
				if (_gTime < 0.5) return true;
				Out($"HOLD again -> auto = {e.AutoMode} (want False)");
				Swipe(new Vector2(400, 300), new Vector2(700, 300));
				_gStep = 45; _gTime = 0; return true;
			case 45:
				Out($"SWIPE right -> skip = {e.SkipMode} (want False: swipes no longer skip)");
				Swipe(new Vector2(700, 300), new Vector2(400, 300));
				_gStep = 46; _gTime = 0; return true;
			case 46:
				Out($"SWIPE left -> skip = {e.SkipMode} (want False)");
				_savedCut = e.Prefs.GetConfig("msg_cut_optin");
				e.Prefs.SetConfig("msg_cut_optin", 1);   // Skip Mode "All", so the button works on any line
				e.AdvMain.SkipButton.EmitSignal(BaseButton.SignalName.ButtonDown);   // hold the skip button
				_gStep = 460; _gTime = 0; return true;
			case 460:
				if (_gTime < 0.6) return true;
				Out($"SKIP button held 0.6 s -> skip = {e.SkipMode} (want True; line read = {e.HasReadMessage})");
				e.AdvMain.SkipButton.EmitSignal(BaseButton.SignalName.ButtonUp);
				Out($"SKIP button released -> skip = {e.SkipMode} (want False)");
				e.AdvMain.SkipButton.EmitSignal(BaseButton.SignalName.ButtonDown);   // quick tap
				e.AdvMain.SkipButton.EmitSignal(BaseButton.SignalName.ButtonUp);
				Out($"SKIP button tapped -> skip = {e.SkipMode} (want True)");
				e.AdvMain.SkipButton.EmitSignal(BaseButton.SignalName.ButtonDown);
				e.AdvMain.SkipButton.EmitSignal(BaseButton.SignalName.ButtonUp);
				Out($"SKIP button tapped again -> skip = {e.SkipMode} (want False)");
				e.StopSkip();
				e.Prefs.SetConfig("msg_cut_optin", _savedCut);
				_gStep = 47; _gTime = 0; return true;
			case 47:
				if (_gTime < 1.0) return true;
				Swipe(new Vector2(640, 450), new Vector2(640, 250));
				_gStep = 48; _gTime = 0; return true;
			case 48:
				if (_gTime < 0.5) return true;
				Out($"SWIPE up -> text box visible = {e.AdvMain.Visible} (want False)");
				Finish(0, "gesture test done"); _gStep = 99; return true;
		}
		return true;
	}
	static int _gIdx, _qsIdx;
	static Vector2 _gPending;
	static void Swipe(Vector2 a, Vector2 b)
	{
		Touch(0, true, a);
		for (int i = 1; i <= 5; i++) Drag(a + (b - a) * i / 5f);
		Touch(0, false, b);
	}
	static void TapNamed(Wa2EngineMain e, string name)
	{
		Control b = null;
		foreach (Node n in e.GetTree().Root.FindChildren(name, "TextureButton", true, false)) b = n as Control;
		if (b == null) { Out("no button " + name); return; }
		Vector2 c = b.GetViewport().GetFinalTransform() * (b.GetGlobalTransformWithCanvas() * (b.Size / 2));
		Touch(0, true, c); Touch(0, false, c);
	}

	// `--backlog-voice-test`: after 3 voiced lines open the backlog by swipe, tap each visible voice
	// button with a touch, and check the same voice (label/id) is played again.
	static readonly List<string> _voices = new();
	public static void OnVoice(int label, int id) { _voices.Add($"{label}:{id}"); }
	static int _bvStep; static double _bvTime; static int _bvItem;
	static bool BacklogVoiceTick(double delta, Wa2EngineMain e)
	{
		if (!Args.Contains("--backlog-voice-test") || _done < 3) return false;
		_bvTime += delta;
		switch (_bvStep)
		{
			case 0:
				Out($"BLVOICE voices played while reading: {string.Join(" ", _voices)}");
				Swipe(new Vector2(640, 250), new Vector2(645, 430));
				_bvStep = 1; _bvTime = 0; return true;
			case 1:
				if (_bvTime < 1.5) return true;
				Save("bl-open");
				var m = e.UiMgr.BackLogMenu;
				Out($"BLVOICE backlog open = {e.UiMgr.UiQueue.Peek() == m}");
				_bvStep = 2; _bvTime = 0; _bvItem = 0; return true;
			case 2:
				if (_bvTime < 0.6) return true;
				var menu = e.UiMgr.BackLogMenu;
				while (_bvItem < menu.BackLogItems.GetChildCount())
				{
					var it = menu.BackLogItems.GetChild<BackLogItem>(_bvItem);
					if (it.Visible && it.VoiceBtn.IsVisibleInTree())
					{
						var b = it.VoiceBtn;
						// tap 24 px right of the icon's centre: outside the original 28 px button
						Vector2 c = b.GetViewport().GetFinalTransform() * (b.GetGlobalTransformWithCanvas() * (b.Size / 2) + new Vector2(24, 0));
						_voices.Clear();
						Touch(0, true, c); Touch(0, false, c);
						Out($"BLVOICE tap entry {_bvItem} (expects {it.VoiceInfos[0].Label}:{it.VoiceInfos[0].Id}, disabled={b.Disabled}, size {b.Size * b.GetGlobalTransformWithCanvas().Scale} game px)");
						_bvItem++; _bvStep = 3; _bvTime = 0; return true;
					}
					_bvItem++;
				}
				Finish(0, "backlog voice test done"); _bvStep = 9; return true;
			case 3:
				if (_bvTime < 0.8) return true;
				Out($"BLVOICE   -> played {(_voices.Count == 0 ? "nothing" : string.Join(" ", _voices))}");
				_bvStep = 2; _bvTime = 0; return true;
		}
		return true;
	}

	// `--assist-test`: open each menu, list buttons under 74 game px (~44 pt), tap ~26 px outside its
	// exit button with a touch, and check the menu closed.
	static int _asStep, _asMenu; static double _asTime;
	static readonly string[] AsMenus = { "panel", "options", "save", "backlog" };
	static bool AssistTick(double delta, Wa2EngineMain e)
	{
		if (!Args.Contains("--assist-test") || _done < 2) return false;
		_asTime += delta;
		if (_asMenu >= AsMenus.Length) { Finish(0, "assist test done"); return true; }
		string m = AsMenus[_asMenu];
		switch (_asStep)
		{
			case 0:
				if (_asTime < 1.0) return true;
				if (m == "panel") Wa2Phone.Open(e);
				else if (m == "options") e.UiMgr.OpenOptionsMenu();
				else if (m == "save") e.UiMgr.OpenSaveMenu();
				else e.AdvMain.OnBackLogButtonDown();
				_asStep = 1; _asTime = 0; return true;
			case 1:
				if (_asTime < 2.0) return true;
				Save("assist-" + m);
				var small = new List<string>();
				foreach (var (n, sz) in Wa2TouchAssist.Audit()) if (Mathf.Min(sz.X, sz.Y) < 74) small.Add($"{n} {sz.X:0}x{sz.Y:0}");
				Out($"ASSIST {m}: {small.Count} buttons under 74 px: {string.Join(", ", small)}");
				Control exit = m == "panel" ? FindClose(e) : (e.UiMgr.UiQueue.Peek() as BasePage)?.ExitBtn;
				if (exit == null) { Out($"ASSIST {m}: no exit button found"); _asMenu++; _asStep = 0; return true; }
				var r = exit.GetGlobalRect();
				Vector2 canvas = new Vector2(r.GetCenter().X, r.End.Y + 26);   // 26 px below the button
				Vector2 win = e.GetViewport().GetFinalTransform() * canvas;
				Out($"ASSIST {m}: exit {r.Size.X:0}x{r.Size.Y:0} at {r.Position}; tapping 26 px below it");
				Touch(0, true, win); Touch(0, false, win);
				_asStep = 2; _asTime = 0; return true;
			case 2:
				if (_asTime < 2.5) return true;
				Out($"ASSIST {m}: after tap top ui = {e.UiMgr.UiQueue.Peek()?.Name} (want AdvMain)");
				if (e.UiMgr.UiQueue.Peek() != e.AdvMain) e.Back();
				_asMenu++; _asStep = 0; _asTime = 0; return true;
		}
		return true;
	}
	static Control FindClose(Wa2EngineMain e)
	{
		Control best = null; float bestY = -1;
		foreach (var b in Wa2TouchAssist.Candidates()) { var y = b.GetGlobalRect().Position.Y; if (y > bestY) { bestY = y; best = b; } }
		return best;   // the panel's Close is its lowest button
	}

	// `--switch-test`: leaving the app mid-touch must not leave the game skipping (user report
	// 2026-10-11). After 3 lines: A) Skip button held, app switch -> skip off, no lines advance;
	// B) finger held with long-press skip on, app switch -> not skipping, the late lift is ignored;
	// C) a held touch that iOS cancels does not toggle Auto; D) control: a normal hold + lift still does.
	static readonly bool SwitchTest = Args.Contains("--switch-test");
	static int _swStep = -1, _swIdx; static double _swTime;
	static Vector2 SwPos(Wa2EngineMain e) => e.GetViewport().GetFinalTransform() * new Vector2(640, 300);
	static void AppSwitch(Wa2EngineMain e)
	{
		e.Notification((int)Node.NotificationApplicationFocusOut);
		e.Notification((int)Node.NotificationApplicationPaused);
	}
	static bool SwitchTick(double delta, Wa2EngineMain e)
	{
		if (!SwitchTest || _done < 3) return false;
		if (_swStep < 0) { _swStep = 0; _swTime = 0; e.Prefs.SetConfig("msg_cut_optin", 1); }   // Skip Mode: All (scratch profile)
		_swTime += delta;
		switch (_swStep)
		{
			case 0:
				if (_swTime < 1.0) return true;
				e.AdvMain.OnSkipPress();                     // finger down on Skip, never lifted
				_swStep = 1; _swTime = 0; return true;
			case 1:
				if (_swTime < 0.3) return true;
				Out($"SWITCH A: holding Skip -> SkipMode {e.SkipMode} (want True)");
				AppSwitch(e); _swIdx = e.CurMessageIdx;
				Out($"SWITCH A: after app switch SkipMode {e.SkipMode} Skipping {e.Skipping} (want False False)");
				_swStep = 20; _swTime = 0; return true;
			case 20:
				if (_swTime < 0.3) return true;              // a line the skip had already requested may still land
				Out($"SWITCH A: lines in the first 0.3 s: {e.CurMessageIdx - _swIdx} (at most 1)");
				_swIdx = e.CurMessageIdx;
				_swStep = 2; _swTime = 0; return true;
			case 2:
				if (_swTime < 1.5) return true;
				Out($"SWITCH A: lines advanced in the next 1.5 s: {e.CurMessageIdx - _swIdx} (want 0)");
				e.Prefs.SetConfig("checkskip", 1);
				Touch(0, true, SwPos(e));                    // finger held on the story screen
				_swStep = 3; _swTime = 0; return true;
			case 3:
				if (_swTime < 1.0) return true;
				Out($"SWITCH B: finger held 1 s with long-press skip on -> Skipping {e.Skipping} (want True)");
				AppSwitch(e); _swIdx = e.CurMessageIdx;
				_swStep = 4; _swTime = 0; return true;
			case 4:
				if (_swTime < 1.5) return true;
				Out($"SWITCH B: after app switch Skipping {e.Skipping} IsPressed {e.IsPressed}, lines advanced {e.CurMessageIdx - _swIdx} (want False False 0)");
				_swIdx = e.CurMessageIdx;
				Touch(0, false, SwPos(e));                   // the lift arrives late
				_swStep = 5; _swTime = 0; return true;
			case 5:
				if (_swTime < 1.0) return true;
				Out($"SWITCH B: late lift -> lines advanced {e.CurMessageIdx - _swIdx} (want 0)");
				e.Prefs.SetConfig("checkskip", 0);
				e.StopAutoMode();
				Touch(0, true, SwPos(e));
				_swStep = 6; _swTime = 0; return true;
			case 6:
				if (_swTime < 0.9) return true;              // a hold (>= 0.6 s) would toggle Auto on lift
				Input.ParseInputEvent(new InputEventScreenTouch { Index = 0, Pressed = false, Canceled = true, Position = SwPos(e) });
				_swStep = 7; _swTime = 0; return true;
			case 7:
				if (_swTime < 0.5) return true;
				Out($"SWITCH C: held touch cancelled by iOS -> AutoMode {e.AutoMode} (want False)");
				Touch(0, true, SwPos(e));
				_swStep = 8; _swTime = 0; return true;
			case 8:
				if (_swTime < 0.9) return true;
				Touch(0, false, SwPos(e));
				_swStep = 9; _swTime = 0; return true;
			case 9:
				if (_swTime < 0.5) return true;
				Out($"SWITCH D: normal hold + lift -> AutoMode {e.AutoMode} (want True)");
				e.StopAutoMode();
				Finish(0, "switch test done");
				return true;
		}
		return true;
	}

	public static void Tick(double delta)
	{
		if (!Enabled) return;
		var e = Wa2EngineMain.Engine;
		if (AssistTick(delta, e)) return;
		if (BacklogVoiceTick(delta, e)) return;
		if (Args.Contains("--show-panel") && _done >= 2)
		{
			if (!Wa2Phone.IsOpen && _gTime == 0) { Wa2Phone.Open(e); _gTime = 0.001; return; }
			_gTime += delta;
			if (_gTime < 1.5) return;
			Save("panel"); Finish(0, "panel saved"); return;
		}
		// `--show-options`: open the original Options menu and save each of its three pages.
		if (Args.Any(a => a.StartsWith("--show-options")) && _done >= 2)
		{
			_gTime += delta;
			bool phone = Args.Contains("--show-options=4");
			if (_gStep < 0) { if (_gTime < 1.0) return; if (phone) e.UiMgr.OpenPhoneOptions(); else e.UiMgr.OpenOptionsMenu(); _gStep = phone ? 2 : 0; _gTime = 0; return; }
			if (_gTime < 2.0) return;
			Save($"options{_gStep}");
			_gStep++; _gTime = 0;
			if (phone && _gStep == 3)
			{
				// tap "Larger" (Text Size) on OPTION 4 and check the setting and the lit button
				var page = e.UiMgr.OptionsMenu.PageList.GetChild(Wa2PhoneOptions.PageIndex);
				var btns = page.GetChildren().OfType<Wa2Button>().ToList();
				_optTextIdx = Wa2Phone.TextIdx;
				// buttons in page order: Fill Screen 0-2, Side Bars 3-5, Pictures 6-7, Text Size 8-11
				btns[11].EmitSignal(BaseButton.SignalName.ButtonDown);
				Out($"OPTION4 tap Larger -> TextIdx = {Wa2Phone.TextIdx} (want 3); lit {btns[11].ButtonPressed}, Small lit {btns[8].ButtonPressed}");
				return;
			}
			if (phone && _gStep == 4) { Wa2Phone.SetText(_optTextIdx); Finish(0, "options saved"); return; }
			if (_gStep >= 3) { Finish(0, "options saved"); return; }
			e.UiMgr.OptionsMenu.UpdatePage(_gStep);
			return;
		}
		if (GestureTick(delta, e)) return;
		if (SwitchTick(delta, e)) return;
		// `--film=N`: after each click, save every 2nd frame for N frames (to see transitions).
		if (_film > 0)
		{
			if (_film % 2 == 0) Save($"{_filmKey}_f{(FilmFrames - _film) / 2:D3}");
			_film--;
		}
		if (e.State != Wa2EngineMain.GameState.GAME || e.Script == null) return;
		var adv = e.AdvMain;
		_idle += delta;
		if (_idle > 180)
		{
			Save("stuck");
			Out($"STUCK: no new line for 180 s; script={e.Script.ScriptName} idx={e.CurMessageIdx} adv={adv.State} " +
				$"wait={e.WaitTimer.IsActive()} anim={e.AnimatorMgr.WaitAnimation()} video={e.VideoPlayer.IsPlaying()} " +
				$"select={adv.SelectMessageContainer.Visible} ui={e.UiMgr.UiQueue.Peek()?.Name}");
			Finish(3, "stopped");
			return;
		}
		if (adv.SelectMessageContainer.Visible)
		{
			Save("choice");
			Finish(4, $"stopped at a choice in {e.Script.ScriptName} idx {e.CurMessageIdx} (choices not automated yet)");
			return;
		}
		if (adv.State == Wa2AdvMain.AdvState.WAIT_CLICK && !e.AnimatorMgr.WaitAnimation())
		{
			_settled += delta;
			if (_settled < Settle) return;
			string key = $"{e.Script.ScriptName}_{e.CurMessageIdx}", k = key;
			for (int j = 2; _keys.Contains(k); j++) k = $"{key}_{j}";
			_keys.Add(k);
			// `--char-left`: move every shown character to the left standing position (test aid)
			if (Args.Contains("--char-left"))
				foreach (var ch in e.Chars) if (ch != null && ch.Visible) { ch.SetCurOffset(new Vector2(288, 0)); ch.SetNextOffset(new Vector2(288, 0)); }
			// `--import-slot=N`: rebuild a PC save by replay; save the port slot at the --skip-to line.
			string slot = Arg("--import-slot=");
			if (slot != null)
			{
				int want = int.TryParse(Arg("--skip-to="), out int w) ? w : -1;
				// One slow frame (e.g. the first decode of a 2x picture) can "settle" a line while still
				// fast-forwarding: keep going while still in the start scene (or its English split parts,
				// "2402" -> "2402_2", which keep the scene's message numbers) and short of the target.
				string start = Arg("--start="), cur = e.Script.ScriptName;
				if (e.CurMessageIdx != want && want > 0 && e.CurMessageIdx < want
					&& (cur == start || cur.StartsWith(start + "_")) && ++_importRetries <= 300)
				{
					_keys.Remove(k); _settled = 0;
					e.SkipMode = true; e.ClickAdv(true);
					return;
				}
				if (e.CurMessageIdx != want) { Finish(5, $"import: stopped at {e.Script.ScriptName} {e.CurMessageIdx}, wanted {want} (retries {_importRetries})"); return; }
				e.GameSav.SaveData(int.Parse(slot));
				Save("import");
				Out($"import: saved {e.Script.ScriptName} message {e.CurMessageIdx} to slot {slot}; flags {string.Join(",", e.GameFlags)}");
				Finish(0, "import done");
				return;
			}
			// `--auto-on`: switch Auto on at the first line (as the side-bar button does), for shots of
			// the Auto indicator and button state.
			if (Args.Contains("--auto-on") && !_autoPressed)
			{
				_autoPressed = true; _settled = 0;
				e.AdvMain.OnAutoButtonDown();
				Out($"auto-on: AutoMode={e.AutoMode}");
				return;
			}
			// `--skip-test`: press the skip button on the first line, then report where skipping stops.
			if (Args.Contains("--skip-test"))
			{
				if (!_skipPressed)
				{
					_skipPressed = true;
					Out($"skiptest: msg_cut_optin={e.Prefs.GetConfig("msg_cut_optin")} first line {k} read={e.HasReadMessage}");
					e.AdvMain.OnSkipButtonDown();
					Out($"skiptest: skip button -> SkipMode={e.SkipMode}");
					_settled = 0;
					if (e.SkipMode) return;
					Finish(0, "skiptest: skip refused on an unread line");
					return;
				}
				Out($"skiptest: stopped at {k} read={e.HasReadMessage} SkipMode={e.SkipMode}");
				Finish(0, "skiptest done");
				return;
			}
			Save(k);
			_done++;
			Out($"{k} saved");
			_settled = 0; _idle = 0; _nudge = 0;
			if (_done == ToggleAt)
			{
				Wa2Phone.SetSharper(!Wa2Phone.Sharper);
				Out($"pictures -> {(Wa2Phone.Sharper ? "Sharper" : "Original")}");
				return;    // no click: the same line settles and is saved again as <key>_2
			}
			if (_done >= Lines && !GestureTest && !SwitchTest && !Args.Contains("--backlog-voice-test") && !Args.Contains("--assist-test")) { Finish(0, "done"); return; }
			if ((GestureTest || SwitchTest || Args.Contains("--backlog-voice-test")) && _done >= 3) return;
			if (Args.Contains("--assist-test") && _done >= 2) return;
			e.ClickAdv(true);
			if (FilmFrames > 0) { _film = FilmFrames; _filmKey = k; }
			return;
		}
		_settled = 0;
		// Movies, eyecatches and click-to-continue waits: nudge once a second, like a player would.
		_nudge += delta;
		if (_nudge > 1.0)
		{
			_nudge = 0;
			if (e.VideoPlayer.IsPlaying()) e.HideVideo();
			else e.ClickAdv(true);
		}
	}
}
