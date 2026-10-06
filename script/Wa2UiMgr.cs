using System.Linq;
using Godot;
using System;
using System.Collections.Generic;
[GlobalClass]
public partial class Wa2UiMgr : Control
{
	public Stack<Control> UiQueue = new();
	[Export]
	public UICalender UICalender;
	[Export]
	public UIConfirm UIConfirm;
	[Export]
	public OptionsMenu OptionsMenu;
	[Export]
	public BgmModeMenu BgmModeMenu;
	[Export]
	public Wa2AdvMain AdvMain;
	[Export]
	public TitleMenu TitleMenu;
	[Export]
	public LoadSaveMenu LoadSaveMenu;
	[Export]
	public CGModeMenu CGModeMenu;
	[Export]
	public BackLogMenu BackLogMenu;
	[Export]
	public NovelBackLogMenu NovelBackLogMenu;
	[Export]
	public SceneReplayMenu SceneReplayMenu;
	[Export]
	public VoiceMessageMenu VoiceMessageMenu;
	private Wa2EngineMain _engine;
	public override void _Ready()
	{

		TitleMenu.Visible = false;
		_engine = Wa2EngineMain.Engine;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	// public override void _Process(double delta)
	// {
	// }
	public void OpenNovelBackLog()
	{
		NovelBackLogMenu.Open();
		UiQueue.Push(NovelBackLogMenu);
	}
	public void OpenBackLog()
	{
		BackLogMenu.Open();
		UiQueue.Push(BackLogMenu);
	}
	public void OpenGame()
	{
		try
		{
			_engine.SubViewport.Show();
			_engine.State = Wa2EngineMain.GameState.GAME;
			JumpScene(AdvMain);
			Callable.From(() => Wa2Phone.ShowIfFirstTime(_engine)).CallDeferred();
			AdvMain.Hide();
		}
		catch (System.Exception e)
		{
			_engine.BootLog("OpenGame:CRASH: " + e);
			_engine.OpenErrorMessage(Wa2EngineMain.Tr("进入游戏失败:\n", "Could not enter the game:\n") + e.Message);
		}
	}
	// Menus opened over the story hide the dialogue box (as the backlog already did) and restore it
	// on close, but only if it was showing: a box the player hid stays hidden.
	private readonly System.Collections.Generic.HashSet<Control> _hidAdv = new();
	private void HideAdvFor(Control page)
	{
		if (_engine.State == Wa2EngineMain.GameState.GAME && AdvMain.Visible)
		{
			AdvMain.Hide();
			_hidAdv.Add(page);
		}
	}
	public void ReturnScene()
	{
		if (UiQueue.Count > 0)
		{
			Control ui = UiQueue.Pop();
			ui.Hide();
			if (_hidAdv.Remove(ui)) AdvMain.Show();
		}
	}
	public void OpenOptionsMenu()
	{
		HideAdvFor(OptionsMenu);
		OptionsMenu.Open();
		UiQueue.Push(OptionsMenu);
	}
	public void OpenVoiceMessageMenu()
	{
		VoiceMessageMenu.Open();
		UiQueue.Push(VoiceMessageMenu);
	}
	public void OpenSaveMenu()
	{
		HideAdvFor(LoadSaveMenu);
		LoadSaveMenu.Open(DataMode.Save);
		UiQueue.Push(LoadSaveMenu);
	}
	public void OpenCGModeMenu()
	{
		CGModeMenu.Open();
		UiQueue.Push(CGModeMenu);
	}
	public void OpenSceneReplayMenu()
	{
		SceneReplayMenu.Open();
		UiQueue.Push(SceneReplayMenu);
	}
	public void OpenBgmModeMenu()
	{
		BgmModeMenu.Open();
		UiQueue.Push(BgmModeMenu);
	}
	public void OpenLoadMenu()
	{
		HideAdvFor(LoadSaveMenu);
		LoadSaveMenu.Open(DataMode.Load);
		UiQueue.Push(LoadSaveMenu);
	}
	public void OpenConfirm(string text1, string text2, bool confirm, Action action)
	{
		UiQueue.Push(UIConfirm);
		UIConfirm.Open(text1, text2, confirm, action);

	}
	// Developer options: `godot --path . -- --start=1002 [--skip-to=100]` skips the title once,
	// starts that script, and optionally fast-forwards until that message index.
	private static string _devStartScript = OS.GetCmdlineUserArgs()
		.FirstOrDefault(a => a.StartsWith("--start="))?["--start=".Length..];
	public static int DevSkipTo = int.TryParse(OS.GetCmdlineUserArgs()
		.FirstOrDefault(a => a.StartsWith("--skip-to="))?["--skip-to=".Length..], out int n) ? n : 0;
	public void OpenTitleMenu()
	{
		if (_devStartScript != null)
		{
			string name = _devStartScript;
			_devStartScript = null;
			_engine.StartScript(name);
			// `--import-flags=v0,v1,...`: game flags from a PC save (tools/pc-save-flags.py)
			string fl = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--import-flags="))?["--import-flags=".Length..];
			if (fl != null)
			{
				var vals = fl.Split(',');
				for (int i = 0; i < vals.Length && i < _engine.GameFlags.Length; i++) _engine.GameFlags[i] = int.Parse(vals[i]);
				GD.Print("import: game flags set " + fl);
			}
			OpenGame();
			_engine.SkipMode = DevSkipTo > 0;
			return;
		}
		_engine.State = Wa2EngineMain.GameState.TITLE;
		// _engine.ReplayMode = 0;
		_engine.ScriptStack.Clear();
		_engine.Script = null;
		_engine.Reset();
		_engine.SubViewport.Hide();

		TitleMenu.Open();
		JumpScene(TitleMenu);
	}
	public void OpenUICalender()
	{
		UiQueue.Push(UICalender);
		UICalender.Open();
	}
	public void JumpScene(Control scene)
	{
		if (UiQueue.Count > 0)
		{
			Control ui = UiQueue.Pop();
			ui.Hide();

		}
		scene.Show();
		UiQueue.Push(scene);
	}
}
