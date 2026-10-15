using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Godot;
public partial class BackLogMenu : BasePage
{
  [Export]
  public VBoxContainer BackLogItems;
  [Export]
  public VScrollBar ScrollBar;
  public List<VoiceInfo> VoiceInfos;
  public int VoiceIdx = 0;
  // Rows per page: 4 (PC layout). Until 2026-10-11 an enlarged dialogue size gave 3 wider rows (the
  // k > 1 branch below, now unused); Text Size now only enlarges the letters. Applied on first open: EnglishPatch is only known
  // after the paks are found, which is after _Ready.
  private int _rows = 4;
  private float _appliedK = 1f;
  private Vector4 _origBox, _origPanel; private Vector2 _origScroll, _origItem; private float _origTextY = -1;
  private void ApplyTextScale()
  {
    // Since 2026-10-11 Text Size only enlarges the letters (the PC's box and lines stay; user):
    // the backlog keeps the PC layout (4 rows) and draws its letters at the same scale.
    float g = Wa2EngineMain.EnglishPatch ? Wa2AdvMain.TextScale : 1f;
    float k = 1f;
    if (Mathf.IsEqualApprox(g, _appliedK)) return;
    var panel = GetNodeOrNull<Control>("NinePatchRect");
    if (_origTextY < 0)
    {
      _origBox = new Vector4(BackLogItems.OffsetLeft, BackLogItems.OffsetTop, BackLogItems.OffsetRight, BackLogItems.OffsetBottom);
      _origPanel = panel != null ? new Vector4(panel.OffsetLeft, 0, panel.OffsetRight, 0) : Vector4.Zero;
      _origScroll = new Vector2(ScrollBar.OffsetLeft, ScrollBar.OffsetRight);
      var it0 = BackLogItems.GetChild<BackLogItem>(0);
      _origItem = it0.CustomMinimumSize; _origTextY = it0.TextLabel.Position.Y;
    }
    _appliedK = g;
    for (int i = 0; i < BackLogItems.GetChildCount(); i++)
    {
      var it = BackLogItems.GetChild<BackLogItem>(i);
      it.NmaeLabel.GlyphScale = g; it.TextLabel.GlyphScale = g;
    }
    if (k <= 1.01f)
    {
      _rows = 4;
      BackLogItems.OffsetLeft = _origBox.X; BackLogItems.OffsetTop = _origBox.Y; BackLogItems.OffsetRight = _origBox.Z; BackLogItems.OffsetBottom = _origBox.W;
      if (panel != null) { panel.OffsetLeft = _origPanel.X; panel.OffsetRight = _origPanel.Z; }
      ScrollBar.OffsetLeft = _origScroll.X; ScrollBar.OffsetRight = _origScroll.Y;
      for (int i = 0; i < BackLogItems.GetChildCount(); i++)
      {
        var it = BackLogItems.GetChild<BackLogItem>(i);
        it.CustomMinimumSize = _origItem; it.NmaeLabel.FontSize = 28; it.TextLabel.FontSize = 28; it.TextLabel.ParagraphSpacing = 13;
        it.VoiceBtn.Scale = Vector2.One;
        it.TextLabel.Position = new Vector2(it.TextLabel.Position.X, _origTextY);
      }
      return;
    }
    _rows = 3;
    float half = Mathf.Round((62 + 55 * 14 * k + 24) / 2);          // name indent + 55 half-width chars + margin
    float rowH = Mathf.Round(4 + 38 * k + 2 * (41 * k + 1) + 28 * k + 10);   // 3 rows clear the header strip
    if (panel != null) { panel.OffsetLeft = -half; panel.OffsetRight = half; }
    BackLogItems.OffsetLeft = -half; BackLogItems.OffsetRight = half;
    BackLogItems.OffsetTop = -rowH * 1.5f; BackLogItems.OffsetBottom = rowH * 1.5f;
    BackLogItems.CustomMinimumSize = new Vector2(half * 2, 0);
    for (int i = 0; i < BackLogItems.GetChildCount(); i++)
    {
      var item = BackLogItems.GetChild<BackLogItem>(i);
      item.CustomMinimumSize = new Vector2(half * 2, rowH);
      // voice replay: icon grows with the text; a larger invisible tap area (~47x40 pt on a phone)
      var vb = item.VoiceBtn;
      vb.PivotOffset = vb.Size / 2;
      vb.Scale = new Vector2(k, k);
      if (item.GetNodeOrNull<Button>("VoiceHit") == null)
      {
        var hit = new Button { Name = "VoiceHit", Flat = true, FocusMode = FocusModeEnum.None,
          Position = vb.Position + vb.Size / 2 - new Vector2(40, 34), Size = new Vector2(80, 68) };
        foreach (var st in new[] { "normal", "hover", "pressed", "focus", "disabled" }) hit.AddThemeStyleboxOverride(st, new StyleBoxEmpty());
        hit.ButtonDown += () => { if (vb.IsVisibleInTree() && !vb.Disabled) vb.EmitSignal(BaseButton.SignalName.ButtonDown); };
        item.AddChild(hit);
      }
      item.NmaeLabel.FontSize = Mathf.RoundToInt(28 * k);
      item.TextLabel.FontSize = Mathf.RoundToInt(28 * k);
      item.TextLabel.ParagraphSpacing = Mathf.RoundToInt(13 * k);
      item.TextLabel.Position = new Vector2(item.TextLabel.Position.X, 4 + Mathf.Round(38 * k));
      if (i >= _rows) item.Hide();
    }
    ScrollBar.OffsetLeft = 640 + half + 8; ScrollBar.OffsetRight = 640 + half + 40;
  }
  public override void _Ready()
  {
    base._Ready();
    Modulate = new Color(1, 1, 1, 1);
    Scale = new Vector2(1, 1);
    ScrollBar.ValueChanged += OnScrollBarValChanged;
    _engine.SoundMgr.GetVoicePlayer(0).Finished += OnVoiceFinished;
    for (int i = 0; i < 4; i++)
    {
      BackLogItem item = BackLogItems.GetChild<BackLogItem>(i);
      item.VoiceBtn.ButtonDown += () =>
      {
        VoiceIdx = 0;
        VoiceInfos = item.VoiceInfos;
      };
    }

  }
  public void OnVoiceFinished()
  {
    if (VoiceInfos!=null && VoiceIdx < (VoiceInfos.Count-1) && _engine.UiMgr.UiQueue.Peek()==this)
    {
      VoiceIdx++;
      _engine.SoundMgr.PlayVoice(VoiceInfos[VoiceIdx].Label, VoiceInfos[VoiceIdx].Id, VoiceInfos[VoiceIdx].Chr, VoiceInfos[VoiceIdx].Volume);
    }
  }
  public override void Open()
  {
    Show();
    BgmPlayer.Show();
    _engine.AdvMain.Hide();
    ApplyTextScale();
    ScrollBar.MaxValue = Math.Max(0, _engine.Backlogs.Count - _rows);
    ScrollBar.Value = ScrollBar.MaxValue;
    OnScrollBarValChanged(ScrollBar.Value);
  }
  public override void Close()
  {
    Hide();
    BgmPlayer.Hide();
    _engine.UiMgr.ReturnScene();
    _engine.AdvMain.Show();
    VoiceInfos=null;
  }


  // Finger scrolling (user, 2026-10-15): drag the entries up or down like a web page, and a flick
  // keeps coasting and slows down. The list moves one entry per row height of finger travel; the
  // scroll bar still works. A drag that starts on a voice button may also play that voice.
  private bool _dragging;
  private Vector2 _dragStart;
  private float _dragAcc, _vel;
  private ulong _lastDragUs;
  public override void _Input(InputEvent @event)
  {
    if (!Visible || _engine == null || _engine.UiMgr.UiQueue.Count == 0 || _engine.UiMgr.UiQueue.Peek() != this) return;
    if (@event is InputEventScreenTouch t && t.Index == 0)
    {
      if (t.Pressed) { _dragStart = t.Position; _dragging = false; _dragAcc = 0; _vel = 0; }
      else if (_dragging)
      {
        _dragging = false;
        if ((Time.GetTicksUsec() - _lastDragUs) / 1e6f > 0.08f) _vel = 0;   // finger stopped before lifting: no coasting
        GetViewport().SetInputAsHandled();
      }
      return;
    }
    if (@event is InputEventScreenDrag d && d.Index == 0)
    {
      if (!_dragging && (d.Position - _dragStart).Length() > 16) _dragging = true;
      if (!_dragging) return;
      ulong now = Time.GetTicksUsec();
      float dt = Mathf.Max((now - _lastDragUs) / 1e6f, 1f / 120f);
      _lastDragUs = now;
      _vel = dt > 0.1f ? 0 : Mathf.Lerp(_vel, d.Relative.Y / dt, 0.6f);
      MoveBy(d.Relative.Y);
      GetViewport().SetInputAsHandled();
      return;
    }
    if (@event is InputEventMouseButton mb && mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
      Step(mb.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
  }
  public override void _Process(double delta)
  {
    if (_dragging || Mathf.Abs(_vel) < 40f) { if (!_dragging) _vel = 0; return; }
    MoveBy(_vel * (float)delta);
    _vel *= Mathf.Pow(0.04f, (float)delta);   // a flick fades out in about a second
  }
  private float RowHeight()
  {
    if (BackLogItems.GetChildCount() < 2) return 120f;
    float a = BackLogItems.GetChild<Control>(0).GetGlobalTransformWithCanvas().Origin.Y;
    float b = BackLogItems.GetChild<Control>(1).GetGlobalTransformWithCanvas().Origin.Y;
    return Mathf.Abs(b - a) > 8 ? Mathf.Abs(b - a) : 120f;
  }
  // Finger moving down shows older entries (the content follows the finger).
  private void MoveBy(float dy)
  {
    float rh = RowHeight();
    _dragAcc += dy;
    while (_dragAcc >= rh) { _dragAcc -= rh; if (!Step(-1)) { _dragAcc = 0; _vel = 0; } }
    while (_dragAcc <= -rh) { _dragAcc += rh; if (!Step(1)) { _dragAcc = 0; _vel = 0; } }
  }
  private bool Step(int n)
  {
    double v = Math.Clamp(ScrollBar.Value + n, ScrollBar.MinValue, ScrollBar.MaxValue);
    if (v == ScrollBar.Value) return false;
    ScrollBar.Value = v;   // ValueChanged redraws the rows
    return true;
  }

  public void OnScrollBarValChanged(double val)
  {
    int pos = (int)val;
    for (int i = 0; i < _rows; i++)
    {
      BackLogItem item = BackLogItems.GetChild<BackLogItem>(i);
      if (pos + i >= _engine.Backlogs.Count)
      {
        item.Hide();
      }
      else
      {
        item.Show();
        item.SetInfo(_engine.Backlogs[pos + i]);
      }

    }
  }
}