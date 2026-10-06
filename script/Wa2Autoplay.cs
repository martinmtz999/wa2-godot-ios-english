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

	public static void Tick(double delta)
	{
		if (!Enabled) return;
		var e = Wa2EngineMain.Engine;
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
			if (_done >= Lines) { Finish(0, "done"); return; }
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
