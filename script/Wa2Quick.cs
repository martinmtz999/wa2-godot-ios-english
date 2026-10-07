using Godot;

// Phone conveniences: quick save / quick load (the PC's Q.Save / Q.Load, hidden in this port until
// now), auto-save when the app leaves the foreground (iOS may close a backgrounded app), and a light
// haptic tick on button presses. Quick save uses slot 98 and the auto-save slot 99: page 10 of the
// save menu, so both can be seen and loaded like any save.
public static class Wa2Quick
{
	public const int QuickSlot = 98, AutoSlot = 99;
	private static ulong _lastAuto;

	// Same rule as the toolbar's Save button: only at a stable point (waiting for a click or a choice).
	private static bool CanSave(Wa2EngineMain e) =>
		e.State == Wa2EngineMain.GameState.GAME && e.Script != null && e.UiMgr.UiQueue.Count > 0
		&& e.UiMgr.UiQueue.Peek() == e.AdvMain && !e.WaitTimer.IsActive() && !e.VideoPlayer.IsPlaying()
		&& (e.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK || e.AdvMain.SelectMessageContainer.Visible)
		&& e.ReplayMode == 0 && !e.DemoMode;

	private static void CloseConfirmSoon(Wa2EngineMain e) =>
		e.GetTree().CreateTimer(1).Timeout += () => e.UiMgr.UIConfirm.Close();

	public static void QuickSave(Wa2EngineMain e)
	{
		if (!CanSave(e)) return;
		e.UiMgr.OpenConfirm("", Wa2EngineMain.Tr("存档保存成功", "File Saved"), false, () => e.GameSav.SaveData(QuickSlot));
		CloseConfirmSoon(e);
	}

	public static void QuickLoad(Wa2EngineMain e)
	{
		if (e.State != Wa2EngineMain.GameState.GAME || e.UiMgr.UiQueue.Peek() != e.AdvMain) return;
		if (!FileAccess.FileExists(e.SavPath + string.Format("sav{0:D2}.sav", QuickSlot))) return;
		e.UiMgr.OpenConfirm(Wa2EngineMain.Tr("读取快速存档。\n确定吗？", "Load Quicksave?"), Wa2EngineMain.Tr("存档读取成功", "File Loaded"),
			e.Prefs.GetConfig("yes_no") == 1, () => { e.GameSav.LoadData(QuickSlot); CloseConfirmSoon(e); });
	}

	public static void AutoSave(Wa2EngineMain e)
	{
		if (Time.GetTicksMsec() - _lastAuto < 5000 || !CanSave(e)) return;
		_lastAuto = Time.GetTicksMsec();
		e.GameSav.SaveData(AutoSlot);
		e.BootLog("AutoSave: slot " + AutoSlot);
	}

	// Resume on launch: the first time the title appears after the app starts, offer the auto-save.
	private static bool _resumeOffered;
	public static void OfferResume(Wa2EngineMain e)
	{
		if (_resumeOffered || Wa2Autoplay.Enabled) return;
		_resumeOffered = true;
		if (!FileAccess.FileExists(e.SavPath + string.Format("sav{0:D2}.sav", AutoSlot))) return;
		e.UiMgr.OpenConfirm(Wa2EngineMain.Tr("继续上次的进度？", "Resume where you left off?"), "", true,
			() => Wa2EngineMain.RunGuarded(() => ResumeAsync(e), "Wa2Quick.Resume"));
	}
	private static async System.Threading.Tasks.Task ResumeAsync(Wa2EngineMain e)
	{
		e.UiMgr.UIConfirm.Close();
		e.SoundMgr.StopBgm();
		var title = e.UiMgr.TitleMenu;
		title.AnimationPlayer.Play("close");
		await title.ToSignal(title.AnimationPlayer, AnimationMixer.SignalName.AnimationFinished);
		e.UiMgr.OpenGame();
		e.GameSav.LoadData(AutoSlot);
	}

	public static void Haptic()
	{
		if (OS.GetName() == "iOS" && Wa2Phone.Haptics) Input.VibrateHandheld(12, 0.35f);
	}
}
