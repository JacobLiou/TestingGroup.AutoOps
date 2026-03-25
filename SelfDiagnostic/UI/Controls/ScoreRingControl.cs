using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SelfDiagnostic.UI.Controls
{
    public sealed class ScoreRingControl : Control
    {
        private readonly Timer _animationTimer;
        private int _targetScore;
        private int _displayScore;
        private string _subtitle = "整体评分";

        public int Score
        {
            get => _targetScore;
            set
            {
                _targetScore = Math.Max(0, Math.Min(value, 100));
                if (!_animationTimer.Enabled)
                {
                    _animationTimer.Start();
                }
            }
        }

        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value ?? string.Empty; Invalidate(); }
        }

        public ScoreRingControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(130, 130);
            ForeColor = Color.FromArgb(30, 30, 30);
            _animationTimer = new Timer { Interval = 15 };
            _animationTimer.Tick += (s, e) =>
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
            using (var bgPen = new Pen(Color.FromArgb(230, 230, 230), 12f))
            using (var fgPen = new Pen(GetScoreColor(_displayScore), 12f))
            {
                fgPen.StartCap = LineCap.Round;
                fgPen.EndCap = LineCap.Round;

                g.DrawArc(bgPen, rect, -90, 360);
                g.DrawArc(fgPen, rect, -90, 360f * _displayScore / 100f);
            }

            using (var scoreFont = new Font(Font.FontFamily, 22, FontStyle.Bold))
            using (var subFont = new Font(Font.FontFamily, 8, FontStyle.Regular))
            {
                var scoreText = _displayScore.ToString();
                var scoreSize = g.MeasureString(scoreText, scoreFont);
                var subText = _subtitle;
                var subSize = g.MeasureString(subText, subFont);

                g.DrawString(scoreText, scoreFont, Brushes.Black,
                    (Width - scoreSize.Width) / 2f, (Height - scoreSize.Height) / 2f - 10);
                g.DrawString(subText, subFont, Brushes.Gray,
                    (Width - subSize.Width) / 2f, (Height - subSize.Height) / 2f + 20);
            }
        }

        private static Color GetScoreColor(int score)
        {
            if (score >= 90) return Color.FromArgb(34, 139, 34);
            if (score >= 75) return Color.FromArgb(255, 165, 0);
            return Color.FromArgb(220, 20, 60);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
