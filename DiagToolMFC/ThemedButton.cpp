#include "stdafx.h"
#include "ThemedButton.h"

using namespace Gdiplus;
typedef Gdiplus::Font GdipFont;

IMPLEMENT_DYNAMIC(CThemedButton, CButton)

CThemedButton::CThemedButton()
    : m_bgColor(ThemeColors::BtnPrimary())
    , m_bgHoverColor(ThemeColors::BtnPrimaryHov())
    , m_textColor(RGB(0, 0, 0))
{
}

BEGIN_MESSAGE_MAP(CThemedButton, CButton)
    ON_WM_MOUSEMOVE()
    ON_WM_MOUSELEAVE()
END_MESSAGE_MAP()

void CThemedButton::SetColors(COLORREF bg, COLORREF bgHover, COLORREF text)
{
    m_bgColor = bg;
    m_bgHoverColor = bgHover;
    m_textColor = text;
    if (GetSafeHwnd())
        InvalidateRect(nullptr, FALSE);
}

void CThemedButton::OnMouseMove(UINT nFlags, CPoint point)
{
    if (!m_bTracking)
    {
        TRACKMOUSEEVENT tme = {};
        tme.cbSize = sizeof(tme);
        tme.dwFlags = TME_LEAVE;
        tme.hwndTrack = GetSafeHwnd();
        TrackMouseEvent(&tme);
        m_bTracking = true;
    }
    if (!m_bHover)
    {
        m_bHover = true;
        InvalidateRect(nullptr, FALSE);
    }
    CButton::OnMouseMove(nFlags, point);
}

void CThemedButton::OnMouseLeave()
{
    m_bTracking = false;
    m_bHover = false;
    InvalidateRect(nullptr, FALSE);
    CButton::OnMouseLeave();
}

void CThemedButton::DrawItem(LPDRAWITEMSTRUCT lpDIS)
{
    CDC* pDC = CDC::FromHandle(lpDIS->hDC);
    CRect rc = lpDIS->rcItem;
    int w = rc.Width(), h = rc.Height();

    Bitmap bmp(w, h, PixelFormat32bppARGB);
    Graphics g(&bmp);
    g.SetSmoothingMode(SmoothingModeAntiAlias);
    g.SetTextRenderingHint(TextRenderingHintClearTypeGridFit);

    // Background: parent dialog bg
    COLORREF parentBg = ThemeColors::BgMain();
    SolidBrush bgClear(Color(255, GetRValue(parentBg), GetGValue(parentBg), GetBValue(parentBg)));
    g.FillRectangle(&bgClear, 0, 0, w, h);

    COLORREF bgCol = m_bHover ? m_bgHoverColor : m_bgColor;
    bool pressed = (lpDIS->itemState & ODS_SELECTED) != 0;
    if (pressed)
    {
        int r = max(0, GetRValue(bgCol) - 20);
        int gv = max(0, GetGValue(bgCol) - 20);
        int b = max(0, GetBValue(bgCol) - 20);
        bgCol = RGB(r, gv, b);
    }

    SolidBrush btnBrush(Color(255, GetRValue(bgCol), GetGValue(bgCol), GetBValue(bgCol)));

    // Rounded rect
    float radius = 6.0f;
    GraphicsPath path;
    RectF btnRect(0.0f, 0.0f, (float)w, (float)h);
    path.AddArc(btnRect.X, btnRect.Y, radius * 2, radius * 2, 180, 90);
    path.AddArc(btnRect.GetRight() - radius * 2, btnRect.Y, radius * 2, radius * 2, 270, 90);
    path.AddArc(btnRect.GetRight() - radius * 2, btnRect.GetBottom() - radius * 2, radius * 2, radius * 2, 0, 90);
    path.AddArc(btnRect.X, btnRect.GetBottom() - radius * 2, radius * 2, radius * 2, 90, 90);
    path.CloseFigure();
    g.FillPath(&btnBrush, &path);

    // Text
    CString btnText;
    GetWindowText(btnText);
    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont font(&ff, 13.0f, FontStyleBold, UnitPixel);
    SolidBrush txtBrush(Color(255, GetRValue(m_textColor), GetGValue(m_textColor), GetBValue(m_textColor)));
    StringFormat sf;
    sf.SetAlignment(StringAlignmentCenter);
    sf.SetLineAlignment(StringAlignmentCenter);
    CStringW textW(btnText);
    g.DrawString(textW, -1, &font, btnRect, &sf, &txtBrush);

    Graphics dcG(pDC->GetSafeHdc());
    dcG.DrawImage(&bmp, rc.left, rc.top);
}
