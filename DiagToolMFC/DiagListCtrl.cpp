#include "stdafx.h"
#include "DiagListCtrl.h"
#include "Resource.h"

using namespace Gdiplus;
typedef Gdiplus::Font GdipFont;

IMPLEMENT_DYNAMIC(CDiagListCtrl, CWnd)

CDiagListCtrl::CDiagListCtrl()
{
}

CDiagListCtrl::~CDiagListCtrl()
{
}

BEGIN_MESSAGE_MAP(CDiagListCtrl, CWnd)
    ON_WM_PAINT()
    ON_WM_ERASEBKGND()
    ON_WM_SIZE()
    ON_WM_VSCROLL()
    ON_WM_MOUSEWHEEL()
    ON_WM_MOUSEMOVE()
    ON_WM_LBUTTONUP()
    ON_WM_MOUSELEAVE()
END_MESSAGE_MAP()

BOOL CDiagListCtrl::Create(DWORD dwStyle, const RECT& rect, CWnd* pParent, UINT nID)
{
    static CString className;
    if (className.IsEmpty())
    {
        className = AfxRegisterWndClass(CS_HREDRAW | CS_VREDRAW | CS_DBLCLKS,
            LoadCursor(nullptr, IDC_ARROW), nullptr, nullptr);
    }
    return CWnd::Create(className, _T(""), dwStyle | WS_CHILD | WS_VISIBLE | WS_VSCROLL,
                         rect, pParent, nID);
}

void CDiagListCtrl::SetItems(std::vector<DiagnosticItem>* pItems)
{
    m_pItems = pItems;
    m_scrollPos = 0;
    UpdateScrollInfo();
    InvalidateRect(nullptr, FALSE);
}

void CDiagListCtrl::RefreshItem(int /*index*/)
{
    InvalidateRect(nullptr, FALSE);
}

void CDiagListCtrl::RefreshAll()
{
    UpdateScrollInfo();
    InvalidateRect(nullptr, FALSE);
}

void CDiagListCtrl::UpdateScrollInfo()
{
    if (!m_pItems || !GetSafeHwnd()) return;

    CRect rc;
    GetClientRect(&rc);
    int totalHeight = HEADER_HEIGHT + (int)m_pItems->size() * ROW_HEIGHT;
    int visibleHeight = rc.Height();

    SCROLLINFO si = {};
    si.cbSize = sizeof(si);
    si.fMask = SIF_RANGE | SIF_PAGE | SIF_POS;
    si.nMin = 0;
    si.nMax = totalHeight - 1;
    si.nPage = visibleHeight;
    si.nPos = m_scrollPos;
    SetScrollInfo(SB_VERT, &si, TRUE);
}

BOOL CDiagListCtrl::OnEraseBkgnd(CDC*)
{
    return TRUE;
}

void CDiagListCtrl::OnSize(UINT nType, int cx, int cy)
{
    CWnd::OnSize(nType, cx, cy);
    UpdateScrollInfo();
    InvalidateRect(nullptr, FALSE);
}

void CDiagListCtrl::OnVScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar)
{
    SCROLLINFO si = {};
    si.cbSize = sizeof(si);
    si.fMask = SIF_ALL;
    GetScrollInfo(SB_VERT, &si);

    int oldPos = si.nPos;
    switch (nSBCode)
    {
    case SB_TOP:        si.nPos = si.nMin; break;
    case SB_BOTTOM:     si.nPos = si.nMax; break;
    case SB_LINEUP:     si.nPos -= ROW_HEIGHT; break;
    case SB_LINEDOWN:   si.nPos += ROW_HEIGHT; break;
    case SB_PAGEUP:     si.nPos -= si.nPage; break;
    case SB_PAGEDOWN:   si.nPos += si.nPage; break;
    case SB_THUMBTRACK: si.nPos = si.nTrackPos; break;
    }

    si.nPos = max(0, min(si.nPos, si.nMax - (int)si.nPage + 1));
    m_scrollPos = si.nPos;
    SetScrollPos(SB_VERT, m_scrollPos, TRUE);

    if (oldPos != m_scrollPos)
        InvalidateRect(nullptr, FALSE);

    CWnd::OnVScroll(nSBCode, nPos, pScrollBar);
}

BOOL CDiagListCtrl::OnMouseWheel(UINT nFlags, short zDelta, CPoint pt)
{
    int scroll = -zDelta / WHEEL_DELTA * ROW_HEIGHT * 3;
    SCROLLINFO si = {};
    si.cbSize = sizeof(si);
    si.fMask = SIF_ALL;
    GetScrollInfo(SB_VERT, &si);

    m_scrollPos += scroll;
    m_scrollPos = max(0, min(m_scrollPos, si.nMax - (int)si.nPage + 1));
    SetScrollPos(SB_VERT, m_scrollPos, TRUE);
    InvalidateRect(nullptr, FALSE);

    return CWnd::OnMouseWheel(nFlags, zDelta, pt);
}

void CDiagListCtrl::OnMouseMove(UINT nFlags, CPoint point)
{
    if (!m_tracking)
    {
        TRACKMOUSEEVENT tme = {};
        tme.cbSize = sizeof(tme);
        tme.dwFlags = TME_LEAVE;
        tme.hwndTrack = GetSafeHwnd();
        TrackMouseEvent(&tme);
        m_tracking = true;
    }

    int newHover = HitTestFixButton(point);
    if (newHover != m_hoverFixBtn)
    {
        m_hoverFixBtn = newHover;
        InvalidateRect(nullptr, FALSE);
    }

    CWnd::OnMouseMove(nFlags, point);
}

void CDiagListCtrl::OnLButtonUp(UINT nFlags, CPoint point)
{
    int idx = HitTestFixButton(point);
    if (idx >= 0 && m_pItems && idx < (int)m_pItems->size())
    {
        auto& item = (*m_pItems)[idx];
        if (item.status == CheckStatus::Warning || item.status == CheckStatus::Fail)
        {
            GetParent()->PostMessage(WM_DIAGLIST_FIX_ITEM, (WPARAM)idx, 0);
        }
    }
    CWnd::OnLButtonUp(nFlags, point);
}

void CDiagListCtrl::OnMouseLeave()
{
    m_tracking = false;
    if (m_hoverFixBtn >= 0)
    {
        m_hoverFixBtn = -1;
        InvalidateRect(nullptr, FALSE);
    }
    CWnd::OnMouseLeave();
}

int CDiagListCtrl::HitTestFixButton(CPoint pt)
{
    if (!m_pItems) return -1;

    CRect rc;
    GetClientRect(&rc);
    int w = rc.Width();
    int detailRight = w - COL_ACTION;

    float rowY = (float)(HEADER_HEIGHT - m_scrollPos);
    for (int i = 0; i < (int)m_pItems->size(); i++)
    {
        auto& item = (*m_pItems)[i];
        if (item.status == CheckStatus::Warning || item.status == CheckStatus::Fail)
        {
            float btnX = (float)(detailRight + 8);
            float btnY = rowY + 6.0f;
            float btnW = (float)(COL_ACTION - 16);
            float btnH = (float)(ROW_HEIGHT - 12);

            if (pt.x >= btnX && pt.x <= btnX + btnW &&
                pt.y >= btnY && pt.y <= btnY + btnH)
            {
                return i;
            }
        }
        rowY += ROW_HEIGHT;
    }
    return -1;
}

void CDiagListCtrl::OnPaint()
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

void CDiagListCtrl::DrawToBuffer(Graphics& g, int w, int h)
{
    COLORREF bgCol = ThemeColors::BgCard();
    SolidBrush bgBrush(Color(255, GetRValue(bgCol), GetGValue(bgCol), GetBValue(bgCol)));
    g.FillRectangle(&bgBrush, 0, 0, w, h);

    float y = (float)(-m_scrollPos);
    DrawHeader(g, w, y);
    y += HEADER_HEIGHT;

    if (m_pItems)
    {
        for (int i = 0; i < (int)m_pItems->size(); i++)
        {
            if (y + ROW_HEIGHT > 0 && y < h)
                DrawRow(g, w, y, i);
            y += ROW_HEIGHT;
        }
    }
}

void CDiagListCtrl::DrawHeader(Graphics& g, int w, float y)
{
    COLORREF hdrBg = ThemeColors::BgHeader();
    SolidBrush hdrBrush(Color(255, GetRValue(hdrBg), GetGValue(hdrBg), GetBValue(hdrBg)));
    g.FillRectangle(&hdrBrush, 0.0f, y, (float)w, (float)HEADER_HEIGHT);

    COLORREF borderCol = ThemeColors::Border();
    Pen borderPen(Color(255, GetRValue(borderCol), GetGValue(borderCol), GetBValue(borderCol)), 1.0f);
    g.DrawLine(&borderPen, 0.0f, y + HEADER_HEIGHT - 1, (float)w, y + HEADER_HEIGHT - 1);

    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont hdrFont(&ff, 12.0f, FontStyleBold, UnitPixel);
    COLORREF txtCol = ThemeColors::TextSecondary();
    SolidBrush txtBrush(Color(255, GetRValue(txtCol), GetGValue(txtCol), GetBValue(txtCol)));
    StringFormat sf;
    sf.SetAlignment(StringAlignmentNear);
    sf.SetLineAlignment(StringAlignmentCenter);

    int detailCol = COL_CATEGORY + COL_NAME + COL_STATUS;
    int detailWidth = w - detailCol - COL_ACTION;

    struct { LPCWSTR text; float x; float w; } cols[] = {
        { L"类别",   16.0f,             (float)COL_CATEGORY - 16 },
        { L"检查项", (float)COL_CATEGORY, (float)COL_NAME },
        { L"状态",   (float)(COL_CATEGORY + COL_NAME), (float)COL_STATUS },
        { L"详情",   (float)detailCol,   (float)detailWidth },
        { L"操作",   (float)(w - COL_ACTION), (float)COL_ACTION },
    };

    for (auto& c : cols)
    {
        RectF r(c.x, y, c.w, (float)HEADER_HEIGHT);
        g.DrawString(c.text, -1, &hdrFont, r, &sf, &txtBrush);
    }
}

void CDiagListCtrl::DrawRow(Graphics& g, int w, float y, int index)
{
    auto& item = (*m_pItems)[index];

    COLORREF rowBg = ThemeColors::RowBgForStatus(item.status);
    if (index % 2 == 1 && item.status == CheckStatus::Pass)
        rowBg = ThemeColors::BgRowAlt();
    SolidBrush rowBrush(Color(255, GetRValue(rowBg), GetGValue(rowBg), GetBValue(rowBg)));
    g.FillRectangle(&rowBrush, 0.0f, y, (float)w, (float)ROW_HEIGHT);

    COLORREF borderCol = ThemeColors::Border();
    Pen borderPen(Color(40, GetRValue(borderCol), GetGValue(borderCol), GetBValue(borderCol)), 1.0f);
    g.DrawLine(&borderPen, 0.0f, y + ROW_HEIGHT - 1, (float)w, y + ROW_HEIGHT - 1);

    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont textFont(&ff, 12.0f, FontStyleRegular, UnitPixel);
    GdipFont smallFont(&ff, 11.0f, FontStyleRegular, UnitPixel);
    StringFormat sf;
    sf.SetAlignment(StringAlignmentNear);
    sf.SetLineAlignment(StringAlignmentCenter);
    sf.SetTrimming(StringTrimmingEllipsisCharacter);
    sf.SetFormatFlags(StringFormatFlagsNoWrap);

    COLORREF txtPrimary = ThemeColors::TextPrimary();
    COLORREF txtSecondary = ThemeColors::TextSecondary();
    SolidBrush primaryBrush(Color(255, GetRValue(txtPrimary), GetGValue(txtPrimary), GetBValue(txtPrimary)));
    SolidBrush secondaryBrush(Color(255, GetRValue(txtSecondary), GetGValue(txtSecondary), GetBValue(txtSecondary)));

    // Category
    RectF catRect(16.0f, y, (float)COL_CATEGORY - 16, (float)ROW_HEIGHT);
    CStringW catText(item.GetCategoryText());
    g.DrawString(catText, -1, &textFont, catRect, &sf, &secondaryBrush);

    // Name
    RectF nameRect((float)COL_CATEGORY, y, (float)COL_NAME - 8, (float)ROW_HEIGHT);
    CStringW nameText(item.name);
    g.DrawString(nameText, -1, &textFont, nameRect, &sf, &primaryBrush);

    // Status
    COLORREF statusCol = ThemeColors::StatusColor(item.status);
    SolidBrush statusBrush(Color(255, GetRValue(statusCol), GetGValue(statusCol), GetBValue(statusCol)));

    CString statusPrefix;
    switch (item.status)
    {
    case CheckStatus::Pass:    statusPrefix = _T("[ OK ] "); break;
    case CheckStatus::Warning: statusPrefix = _T("[WARN] "); break;
    case CheckStatus::Fail:    statusPrefix = _T("[FAIL] "); break;
    case CheckStatus::Fixed:   statusPrefix = _T("[FIX ] "); break;
    case CheckStatus::Scanning:statusPrefix = _T("[....] "); break;
    default:                   statusPrefix = _T("[    ] "); break;
    }

    RectF statusRect((float)(COL_CATEGORY + COL_NAME), y, (float)COL_STATUS - 8, (float)ROW_HEIGHT);
    CStringW statusText(statusPrefix + item.GetStatusText());
    g.DrawString(statusText, -1, &smallFont, statusRect, &sf, &statusBrush);

    // Detail
    int detailX = COL_CATEGORY + COL_NAME + COL_STATUS;
    int detailW = w - detailX - COL_ACTION;
    RectF detailRect((float)detailX, y, (float)detailW - 8, (float)ROW_HEIGHT);
    CStringW detailText(item.detail);
    g.DrawString(detailText, -1, &smallFont, detailRect, &sf, &secondaryBrush);

    // Fix button
    if (item.status == CheckStatus::Warning || item.status == CheckStatus::Fail)
    {
        float btnX = (float)(w - COL_ACTION + 8);
        float btnY = y + 6.0f;
        float btnW = (float)(COL_ACTION - 16);
        float btnH = (float)(ROW_HEIGHT - 12);
        RectF btnRect(btnX, btnY, btnW, btnH);
        DrawFixButton(g, btnRect, m_hoverFixBtn == index);
    }
}

void CDiagListCtrl::DrawFixButton(Graphics& g, RectF btnRect, bool hover)
{
    COLORREF btnCol = hover ? ThemeColors::BtnFixHov() : ThemeColors::BtnFix();
    SolidBrush btnBrush(Color(255, GetRValue(btnCol), GetGValue(btnCol), GetBValue(btnCol)));

    GraphicsPath path;
    float r = 4.0f;
    path.AddArc(btnRect.X, btnRect.Y, r * 2, r * 2, 180, 90);
    path.AddArc(btnRect.GetRight() - r * 2, btnRect.Y, r * 2, r * 2, 270, 90);
    path.AddArc(btnRect.GetRight() - r * 2, btnRect.GetBottom() - r * 2, r * 2, r * 2, 0, 90);
    path.AddArc(btnRect.X, btnRect.GetBottom() - r * 2, r * 2, r * 2, 90, 90);
    path.CloseFigure();
    g.FillPath(&btnBrush, &path);

    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont btnFont(&ff, 11.0f, FontStyleBold, UnitPixel);
    SolidBrush txtBrush(Color(255, 255, 255, 255));
    StringFormat sf;
    sf.SetAlignment(StringAlignmentCenter);
    sf.SetLineAlignment(StringAlignmentCenter);
    g.DrawString(L"修复", -1, &btnFont, btnRect, &sf, &txtBrush);
}
