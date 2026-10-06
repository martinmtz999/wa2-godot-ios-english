using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

// Phone feel: haptics on a few dramatic sound effects, and Low Power Mode awareness.
public static class Wa2Feel
{
	// Sound effects chosen by measurement, not by scene (no spoilers): 0.15-3 s long, loud, sharp
	// attack, mostly bass, and used at most 12 times in the whole game. 18 effects, 65 plays in all.
	// Pulse length and strength follow each effect's duration and loudness.
	private static readonly Dictionary<int, (int ms, float amp)> Impacts = new()
	{
		{ 8271, (126, 1.0f) },
		{ 8405, (124, 0.78f) },
		{ 7, (75, 0.7f) },
		{ 8261, (108, 0.67f) },
		{ 8229, (97, 0.65f) },
		{ 8451, (88, 0.64f) },
		{ 8165, (71, 0.63f) },
		{ 8232, (64, 0.6f) },
		{ 10, (103, 0.59f) },
		{ 1906, (99, 0.58f) },
		{ 8456, (107, 0.57f) },
		{ 8404, (144, 0.57f) },
		{ 8044, (72, 0.57f) },
		{ 8237, (260, 0.57f) },
		{ 8, (122, 0.57f) },
		{ 8460, (120, 0.56f) },
		{ 2006, (260, 0.56f) },
		{ 8457, (217, 0.53f) }
	};
	private const ulong MinGapMs = 10000;   // never more than one pulse per 10 s
	private static ulong _last;
	public static int Fired;                 // for tests
	public static readonly bool Force = OS.GetCmdlineUserArgs().Contains("--feel-test");

	public static void OnSe(int id)
	{
		if (OS.GetName() != "iOS" && !Force) return;
		var e = Wa2EngineMain.Engine;
		if (e == null || e.State != Wa2EngineMain.GameState.GAME || e.SkipMode || e.Skipping) return;
		if (!Impacts.TryGetValue(id, out var h)) return;
		ulong now = Time.GetTicksMsec();
		if (_last != 0 && now - _last < MinGapMs) return;
		_last = now;
		Fired++;
		Wa2Trace.Log("haptic", id, h.ms, h.amp);
		if (OS.GetName() == "iOS") Input.VibrateHandheld(h.ms, h.amp);
	}

	// Low Power Mode: Godot has no power API, so ask iOS directly (NSProcessInfo
	// isLowPowerModeEnabled) through the Objective-C runtime. Any failure disables the check.
	[DllImport("/usr/lib/libobjc.A.dylib")] private static extern IntPtr objc_getClass(string name);
	[DllImport("/usr/lib/libobjc.A.dylib")] private static extern IntPtr sel_registerName(string name);
	[DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr MsgSend(IntPtr r, IntPtr s);
	[DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern byte MsgSendBool(IntPtr r, IntPtr s);
	private static bool _powerFailed, _low;
	private static ulong _lastPoll;
	public static readonly bool ForceLow = OS.GetCmdlineUserArgs().Contains("--low-power");

	public static bool LowPower
	{
		get
		{
			if (ForceLow) return true;
			if (OS.GetName() != "iOS" || _powerFailed) return false;
			ulong now = Time.GetTicksMsec();
			if (_lastPoll != 0 && now - _lastPoll < 3000) return _low;
			_lastPoll = now;
			try
			{
				IntPtr info = MsgSend(objc_getClass("NSProcessInfo"), sel_registerName("processInfo"));
				_low = info != IntPtr.Zero && MsgSendBool(info, sel_registerName("isLowPowerModeEnabled")) != 0;
			}
			catch (Exception ex)
			{
				_powerFailed = true;
				_low = false;
				Wa2EngineMain.Engine?.BootLog("LowPower check disabled: " + ex.Message);
			}
			return _low;
		}
	}
}
