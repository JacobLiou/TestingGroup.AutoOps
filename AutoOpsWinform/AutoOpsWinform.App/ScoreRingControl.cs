using System.Drawing.Drawing2D;

namespace AutoOpsWinform.App;

public sealed class ScoreRingControl : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer;
    private int _targetScore;
    private int _displayScore;

    public int Score
    {
        get => _targetScore;
        set
        {
            _targetScore = Math.Clamp(value, 0, 100);
            if (!_animationTimer.Enabled)
            {
                _animationTimer.Start();
            }
        }
    }

    public ScoreRingControl()
    {
        DoubleBuffered = true;
        Size = new Size(130, 130);
        //BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(30, 30, 30);
        _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _animationTimer.Tick += (_, _) =>
        {
            if (_displayScore == _targetScore)
            {
                _animationTimer.Stop();
                return;
            }

            _displayScore += _displayScore < _targetScore ? 1 : -1;
            Invalidate();
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(8, 8, Width - 16, Height - 16);
        using var bgPen = new Pen(Color.FromArgb(230, 230, 230), 12f);
        using var fgPen = new Pen(GetScoreColor(_displayScore), 12f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.DrawArc(bgPen, rect, -90, 360);
        g.DrawArc(fgPen, rect, -90, 360f * _displayScore / 100f);

        using var scoreFont = new Font(Font.FontFamily, 22, FontStyle.Bold);
        using var subFont = new Font(Font.FontFamily, 9, FontStyle.Regular);
        var scoreText = _displayScore.ToString();
        var scoreSize = g.MeasureString(scoreText, scoreFont);
        var subText = "SCORE";
        var subSize = g.MeasureString(subText, subFont);

        g.DrawString(scoreText, scoreFont, Brushes.Black, (Width - scoreSize.Width) / 2f, (Height - scoreSize.Height) / 2f - 8);
        g.DrawString(subText, subFont, Brushes.Gray, (Width - subSize.Width) / 2f, (Height - subSize.Height) / 2f + 22);
    }

    private static Color GetScoreColor(int score)
    {
        if (score >= 90) return Color.FromArgb(34, 139, 34);
        if (score >= 75) return Color.FromArgb(255, 165, 0);
        return Color.FromArgb(220, 20, 60);
    }
}
