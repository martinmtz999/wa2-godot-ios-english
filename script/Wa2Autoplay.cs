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
	static double _settled, _idle, _nudge;
	static readonly HashSet<string> _keys = new();

	static void Out(string msg) { GD.Print("autoplay: " + msg); Wa2Trace.Log("autoplay", msg); }

	static void Finish(int code, string why)
	{
		Out($"{why} ({_done} lines)");
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
	public static void TitleTick(double delta)
	{
		if (string.IsNullOrEmpty(TitleShot) || Wa2EngineMain.Engine.State != Wa2EngineMain.GameState.TITLE) return;
		_titleTime += delta;
		if (_titleTime < 4) return;
		Wa2EngineMain.Engine.GetViewport().GetTexture().GetImage().SavePng(TitleShot);
		GD.Print("autoplay: title saved");
		Wa2EngineMain.Engine.GetTree().Quit(0);
	}

	// `--gesture-test`: after 3 lines, inject touch gestures (as the phone would send them) and log
	// what happened: swipe down -> backlog, two-finger tap -> text box hidden, tap -> shown, tap -> next line.
	static readonly bool GestureTest = Args.Contains("--gesture-test");
	static int _gStep = -1; static double _gTime;
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
				Save("g4-rail-tap"); Out($"RAIL tap on the strip -> message {_gIdx} -> {e.CurMessageIdx} (want it to advance)");
				var b = e.AdvMain.BackLogButton;
				// window pixels = viewport final transform * canvas position (buttons sit on a CanvasLayer)
				Vector2 c = b.GetViewport().GetFinalTransform() * (b.GetGlobalTransformWithCanvas() * (b.Size / 2));
				Touch(0, true, c); Touch(0, false, c);
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
				Finish(0, "gesture test done"); _gStep = 99; return true;
		}
		return true;
	}
	static int _gIdx, _qsIdx;
	static void TapNamed(Wa2EngineMain e, string name)
	{
		Control b = null;
		foreach (Node n in e.GetTree().Root.FindChildren(name, "TextureButton", true, false)) b = n as Control;
		if (b == null) { Out("no button " + name); return; }
		Vector2 c = b.GetViewport().GetFinalTransform() * (b.GetGlobalTransformWithCanvas() * (b.Size / 2));
		Touch(0, true, c); Touch(0, false, c);
	}

	public static void Tick(double delta)
	{
		if (!Enabled) return;
		var e = Wa2EngineMain.Engine;
		if (GestureTick(delta, e)) return;
		if (e.State != Wa2EngineMain.GameState.GAME || e.Script == null) return;
		var adv = e.AdvMain;
		_idle += delta;
		if (_idle > 20)
		{
			Save("stuck");
			Out($"STUCK: no new line for 20 s; script={e.Script.ScriptName} idx={e.CurMessageIdx} adv={adv.State} " +
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
			Save(k);
			_done++;
			Out($"{k} saved");
			_settled = 0; _idle = 0; _nudge = 0;
			if (_done >= Lines && !GestureTest) { Finish(0, "done"); return; }
			if (GestureTest && _done >= 3) return;
			e.ClickAdv(true);
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
