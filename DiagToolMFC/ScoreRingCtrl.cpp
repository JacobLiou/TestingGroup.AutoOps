#include "stdafx.h"
#include "ScoreRingCtrl.h"

using namespace Gdiplus;
typedef Gdiplus::Font GdipFont;

IMPLEMENT_DYNAMIC(CScoreRingCtrl, CStatic)

CScoreRingCtrl::CScoreRingCtrl()
{
}

CScoreRingCtrl::~CScoreRingCtrl()
{
    if (GetSafeHwnd())
    {
        KillTimer(TIMER_SPIN);
        KillTimer(TIMER_GLOW);
        KillTimer(TIMER_SCORE);
    }
}

BEGIN_MESSAGE_MAP(CScoreRingCtrl, CStatic)
    ON_WM_PAINT()
    ON_WM_ERASEBKGND()
    ON_WM_TIMER()
    ON_WM_DESTROY()
    ON_WM_SIZE()
END_MESSAGE_MAP()

void CScoreRingCtrl::SetScore(int score)
{
    m_score = score;
}

void CScoreRingCtrl::SetDisplayScore(int score)
{
    m_displayScore = score;
    InvalidateRect(nullptr, FALSE);
}

void CScoreRingCtrl::SetScanning(bool scanning)
{
    m_bScanning = scanning;
    if (scanning)
    {
        SetTimer(TIMER_SPIN, 30, nullptr);
        SetTimer(TIMER_GLOW, 50, nullptr);
    }
    else
    {
        KillTimer(TIMER_SPIN);
        KillTimer(TIMER_GLOW);
        m_rotationAngle = 0.0f;
        m_glowAlpha = 0.05f;
    }
    InvalidateRect(nullptr, FALSE);
}

void CScoreRingCtrl::StartScoreAnimation(int from, int to)
{
    m_displayScore = from;
    m_targetScore = to;
    SetTimer(TIMER_SCORE, 15, nullptr);
}

void CScoreRingCtrl::OnTimer(UINT_PTR nIDEvent)
{
    if (nIDEvent == TIMER_SPIN)
    {
        m_rotationAngle += 3.0f;
        if (m_rotationAngle >= 360.0f)
            m_rotationAngle -= 360.0f;
        InvalidateRect(nullptr, FALSE);
    }
    else if (nIDEvent == TIMER_GLOW)
    {
        if (m_glowIncreasing)
        {
            m_glowAlpha += 0.01f;
            if (m_glowAlpha >= 0.3f)
            {
                m_glowAlpha = 0.3f;
                m_glowIncreasing = false;
            }
        }
        else
        {
            m_glowAlpha -= 0.01f;
            if (m_glowAlpha <= 0.05f)
            {
                m_glowAlpha = 0.05f;
                m_glowIncreasing = true;
            }
        }
    }
    else if (nIDEvent == TIMER_SCORE)
    {
        if (m_displayScore < m_targetScore)
            m_displayScore++;
        else if (m_displayScore > m_targetScore)
            m_displayScore--;
        else
        {
            KillTimer(TIMER_SCORE);
        }
        InvalidateRect(nullptr, FALSE);
    }
    CStatic::OnTimer(nIDEvent);
}

void CScoreRingCtrl::OnDestroy()
{
    KillTimer(TIMER_SPIN);
    KillTimer(TIMER_GLOW);
    KillTimer(TIMER_SCORE);
    CStatic::OnDestroy();
}

BOOL CScoreRingCtrl::OnEraseBkgnd(CDC* /*pDC*/)
{
    return TRUE;
}

void CScoreRingCtrl::OnSize(UINT nType, int cx, int cy)
{
    CStatic::OnSize(nType, cx, cy);
    InvalidateRect(nullptr, FALSE);
}

void CScoreRingCtrl::OnPaint()
{
    CPaintDC dc(this);
    CRect rc;
    GetClientRect(&rc);
    int w = rc.Width(), h = rc.Height();
    if (w <= 0 || h <= 0) return;

    Bitmap bmp(w, h, PixelFormat32bppARGB);
    Graphics g(&bmp);
    g.SetSmoothingMode(SmoothingModeAntiAlias);
    g.SetTextRenderingHint(TextRenderingHintClearTypeGridFit);

    DrawToBuffer(g, w, h);

    Graphics dcG(dc.GetSafeHdc());
    dcG.DrawImage(&bmp, 0, 0);
}

void CScoreRingCtrl::DrawToBuffer(Graphics& g, int w, int h)
{
    COLORREF bgCol = ThemeColors::BgMain();
    SolidBrush bgBrush(Color(255, GetRValue(bgCol), GetGValue(bgCol), GetBValue(bgCol)));
    g.FillRectangle(&bgBrush, 0, 0, w, h);

    float ringSize = (float)min(w, h) - 20.0f;
    if (ringSize < 50.0f) ringSize = 50.0f;
    float ringX = (w - ringSize) / 2.0f;
    float ringY = (h - ringSize) / 2.0f;
    RectF ringRect(ringX, ringY, ringSize, ringSize);

    DrawGlow(g, ringRect);
    DrawBackgroundRing(g, ringRect);

    if (m_bScanning)
        DrawDashedRing(g, ringRect);
    else
        DrawScoreArc(g, ringRect);

    DrawScoreText(g, ringRect);
}

void CScoreRingCtrl::DrawGlow(Graphics& g, RectF ringRect)
{
    COLORREF col = ThemeColors::ScoreColor(m_displayScore);
    float alpha = m_bScanning ? m_glowAlpha : 0.10f;
    BYTE a = (BYTE)(alpha * 255);
    Color glowColor(a, GetRValue(col), GetGValue(col), GetBValue(col));

    float expand = 30.0f;
    RectF glowRect(ringRect.X - expand, ringRect.Y - expand,
                   ringRect.Width + expand * 2, ringRect.Height + expand * 2);
    SolidBrush glowBrush(glowColor);
    g.FillEllipse(&glowBrush, glowRect);
}

void CScoreRingCtrl::DrawBackgroundRing(Graphics& g, RectF ringRect)
{
    COLORREF col = ThemeColors::Border();
    Pen ringPen(Color(80, GetRValue(col), GetGValue(col), GetBValue(col)), 10.0f);
    float inset = 5.0f;
    RectF r(ringRect.X + inset, ringRect.Y + inset,
            ringRect.Width - inset * 2, ringRect.Height - inset * 2);
    g.DrawEllipse(&ringPen, r);
}

void CScoreRingCtrl::DrawScoreArc(Graphics& g, RectF ringRect)
{
    COLORREF col = ThemeColors::ScoreColor(m_displayScore);
    Pen arcPen(Color(220, GetRValue(col), GetGValue(col), GetBValue(col)), 10.0f);
    arcPen.SetLineCap(LineCapRound, LineCapRound, DashCapRound);

    float inset = 5.0f;
    RectF r(ringRect.X + inset, ringRect.Y + inset,
            ringRect.Width - inset * 2, ringRect.Height - inset * 2);

    float sweepAngle = (m_displayScore / 100.0f) * 360.0f;
    if (sweepAngle > 0.0f)
        g.DrawArc(&arcPen, r, -90.0f, sweepAngle);
}

void CScoreRingCtrl::DrawDashedRing(Graphics& g, RectF ringRect)
{
    COLORREF col = ThemeColors::ScoreColor(m_displayScore);
    Pen dashPen(Color(180, GetRValue(col), GetGValue(col), GetBValue(col)), 10.0f);
    REAL dashPattern[] = { 4.0f, 1.0f };
    dashPen.SetDashPattern(dashPattern, 2);

    float inset = 5.0f;
    RectF r(ringRect.X + inset, ringRect.Y + inset,
            ringRect.Width - inset * 2, ringRect.Height - inset * 2);

    GraphicsState state = g.Save();
    float cx = ringRect.X + ringRect.Width / 2.0f;
    float cy = ringRect.Y + ringRect.Height / 2.0f;
    g.TranslateTransform(cx, cy);
    g.RotateTransform(m_rotationAngle);
    g.TranslateTransform(-cx, -cy);
    g.DrawEllipse(&dashPen, r);
    g.Restore(state);
}

void CScoreRingCtrl::DrawScoreText(Graphics& g, RectF ringRect)
{
    COLORREF scoreCol = ThemeColors::ScoreColor(m_displayScore);

    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont scoreFont(&ff, 48.0f, FontStyleBold, UnitPixel);

    CString scoreStr;
    scoreStr.Format(_T("%d"), m_displayScore);

    StringFormat sf;
    sf.SetAlignment(StringAlignmentCenter);
    sf.SetLineAlignment(StringAlignmentCenter);

    RectF textRect(ringRect.X, ringRect.Y - 10.0f, ringRect.Width, ringRect.Height);
    SolidBrush scoreBrush(Color(255, GetRValue(scoreCol), GetGValue(scoreCol), GetBValue(scoreCol)));
    g.DrawString(CT2W(scoreStr), -1, &scoreFont, textRect, &sf, &scoreBrush);

    GdipFont subFont(&ff, 13.0f, FontStyleRegular, UnitPixel);
    COLORREF subCol = ThemeColors::TextSecondary();
    SolidBrush subBrush(Color(255, GetRValue(subCol), GetGValue(subCol), GetBValue(subCol)));
    RectF subRect(ringRect.X, ringRect.Y + ringRect.Height * 0.55f,
                  ringRect.Width, 30.0f);
    g.DrawString(CT2W(m_subText), -1, &subFont, subRect, &sf, &subBrush);
}
