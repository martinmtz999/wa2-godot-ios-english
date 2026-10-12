using System;
using System.Security.Cryptography.X509Certificates;
using Godot;
public partial class UIConfirm : Control
{
  [Export]
  public Wa2Button ConfirmBtn;
  [Export]
  public Wa2Button CancelBtn;
  [Export]

  public Label ConfirmLabel;
  [Export]
  public Control ConfirmMessage;
  [Export]
  public Control TipMessage;
  [Export]
  public Label TipLabel;
  public Action ConfirmAction;
  public Wa2EngineMain _engine;
  public override void _Ready()
  {
    _engine = Wa2EngineMain.Engine;
    ConfirmBtn.ButtonDown += OnConfirmBtnDown;
    CancelBtn.ButtonDown += OnCancelBtnDown;
  }
  // English mode: the messages in the game's own lettering (Wa2Label, the dialogue font) instead of
  // the upstream's bundled TTF (user, 2026-10-11). Each line is centred in its box; the Godot Labels
  // keep the text (it is still read for TipLabel.Text) but are hidden.
  private readonly System.Collections.Generic.List<Wa2Label> _gameText = new();
  private void GameText(Control box, Label label, string text, float centerY)
  {
    foreach (var old in _gameText.FindAll(l => l.GetParent() == box)) { old.QueueFree(); _gameText.Remove(old); }
    if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--old-dialog-font") >= 0) { label.Visible = true; return; }   // comparisons
    if (!Wa2EngineMain.EnglishPatch || string.IsNullOrEmpty(text)) { label.Visible = !Wa2EngineMain.EnglishPatch; return; }
    label.Visible = false;
    const int size = 28, pitch = 42;                     // dialogue size and line pitch (PC)
    var rows = text.Split('\n');
    float y0 = centerY - rows.Length * pitch / 2f + (pitch - size) / 2f;
    for (int i = 0; i < rows.Length; i++)
    {
      float w = rows[i].Length * size / 2f;                // half-width letters advance size/2
      var l = new Wa2Label
      {
        FontSize = size, MaxChars = 40, MaxLines = 1, Color = new Color(1, 1, 1),
        FontTexture = _engine?.AdvMain?.TextLabel?.FontTexture,
        Position = new Vector2(Mathf.Round((box.Size.X - w) / 2), Mathf.Round(y0 + i * pitch)),
      };
      box.AddChild(l);
      l.SetText(rows[i]);
      _gameText.Add(l);
    }
  }
  public void Open(string text1, string text2, bool confirm, Action action)
  {
    Show();
    ConfirmLabel.Text = text1;
    TipLabel.Text = text2;
    GameText(ConfirmMessage, ConfirmLabel, text1, 62);   // above the Yes / No buttons (box 544x216)
    GameText(TipMessage, TipLabel, text2, 56);           // middle of the notice (box 480x112)
    ConfirmAction = action;
    if (confirm)
    {
      ConfirmMessage.Show();
    }
    else
    {
      TipMessage.Show();
      ConfirmAction();
    }

  }
  public void OnConfirmBtnDown()
  {
    ConfirmAction();
    if (TipLabel.Text != "")
    {
      TipMessage.Show();
    }

    ConfirmMessage.Hide();
  }
  public void OnCancelBtnDown()
  {
    Close();
  }
  public void Close()
  {
    ConfirmMessage.Hide();
    TipMessage.Hide();
    Hide();
    _engine.UiMgr.ReturnScene();
  }
}