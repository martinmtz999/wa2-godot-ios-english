using Godot;
using System.Collections.Generic;

// Touch assist for menus: a tap that lands just outside a button (within Reach game px, about 22 pt
// on a 6.7" iPhone) counts as a tap on the nearest button. Menu art and layouts are unchanged. Only
// when a menu/dialog/panel is on top; story taps are untouched. Touch input only (desktop mouse as is).
public partial class Wa2TouchAssist : Node
{
	public const float Reach = 36f;
	private static Wa2TouchAssist _inst;
	private BaseButton _pending;

	public static void Attach(Wa2EngineMain e)
	{
		_inst = new Wa2TouchAssist { Name = "Wa2TouchAssist" };
		e.GetTree().Root.CallDeferred(Node.MethodName.AddChild, _inst);
	}

	// Buttons a tap could mean right now: those under the top menu, the confirm dialog, the phone panel.
	public static List<BaseButton> Candidates()
	{
		var e = Wa2EngineMain.Engine;
		var list = new List<BaseButton>();
		if (e == null || e.UiMgr.UiQueue.Count == 0) return list;
		var top = e.UiMgr.UiQueue.Peek();
		if (top == e.UiMgr.AdvMain && e.State == Wa2EngineMain.GameState.GAME) return list;
		void Walk(Node n)
		{
			if (n is CanvasItem ci && !ci.IsVisibleInTree()) return;
			if (n is BaseButton b && !b.Disabled && b.MouseFilter != Control.MouseFilterEnum.Ignore) list.Add(b);
			foreach (Node c in n.GetChildren()) Walk(c);
		}
		Walk(top);
		return list;
	}

	public static BaseButton Resolve(Vector2 p, out bool direct)
	{
		direct = false;
		BaseButton best = null; float bestD = Reach;
		foreach (var b in Candidates())
		{
			var r = b.GetGlobalRect();
			if (r.HasPoint(p)) { direct = true; return b; }
			float dx = Mathf.Max(Mathf.Max(r.Position.X - p.X, 0), p.X - r.End.X);
			float dy = Mathf.Max(Mathf.Max(r.Position.Y - p.Y, 0), p.Y - r.End.Y);
			float d = Mathf.Sqrt(dx * dx + dy * dy);
			if (d < bestD) { bestD = d; best = b; }
		}
		return best;
	}

	public override void _Input(InputEvent ev)
	{
		if (ev is not InputEventScreenTouch t || t.Index != 0) return;
		if (t.Pressed)
		{
			_pending = null;
			var b = Resolve(t.Position, out bool direct);
			if (b == null || direct) return;      // on a button, or nothing near: normal handling
			_pending = b;
			GetViewport().SetInputAsHandled();
			b.EmitSignal(BaseButton.SignalName.ButtonDown);
		}
		else if (_pending != null)
		{
			var b = _pending; _pending = null;
			GetViewport().SetInputAsHandled();
			if (b.ToggleMode) b.ButtonPressed = true;
			b.EmitSignal(BaseButton.SignalName.Pressed);
			b.EmitSignal(BaseButton.SignalName.ButtonUp);
		}
	}

	// Test aid: every visible button in the current menu with its on-screen size in game px.
	public static IEnumerable<(string name, Vector2 size)> Audit()
	{
		foreach (var b in Candidates())
			yield return ($"{b.GetParent()?.Name}/{b.Name}", b.GetGlobalRect().Size);
	}
}
