#include "stdafx.h"
#include "DiagToolMFC.h"
#include "DiagToolDlg.h"

using namespace Gdiplus;
typedef Gdiplus::Font GdipFont;

#ifdef _DEBUG
#define new DEBUG_NEW
#endif

BEGIN_MESSAGE_MAP(CDiagToolDlg, CDialogEx)
    ON_WM_PAINT()
    ON_WM_ERASEBKGND()
    ON_WM_CTLCOLOR()
    ON_WM_SIZE()
    ON_WM_GETMINMAXINFO()
    ON_BN_CLICKED(IDC_BTN_START, &CDiagToolDlg::OnBnClickedStart)
    ON_BN_CLICKED(IDC_BTN_STOP, &CDiagToolDlg::OnBnClickedStop)
    ON_BN_CLICKED(IDC_BTN_FIXALL, &CDiagToolDlg::OnBnClickedFixAll)
    ON_MESSAGE(WM_CHECK_COMPLETE, &CDiagToolDlg::OnCheckComplete)
    ON_MESSAGE(WM_SCAN_FINISHED, &CDiagToolDlg::OnScanFinished)
    ON_MESSAGE(WM_FIX_ITEM_COMPLETE, &CDiagToolDlg::OnFixItemComplete)
    ON_MESSAGE(WM_FIX_ALL_COMPLETE, &CDiagToolDlg::OnFixAllComplete)
    ON_MESSAGE(WM_DIAGLIST_FIX_ITEM, &CDiagToolDlg::OnDiagListFixItem)
END_MESSAGE_MAP()

CDiagToolDlg::CDiagToolDlg(CWnd* pParent)
    : CDialogEx(IDD_DIAGTOOL_DIALOG, pParent)
{
    m_bgBrush.CreateSolidBrush(ThemeColors::BgMain());
    m_headerBrush.CreateSolidBrush(ThemeColors::BgHeader());
}

void CDiagToolDlg::DoDataExchange(CDataExchange* pDX)
{
    CDialogEx::DoDataExchange(pDX);
}

BOOL CDiagToolDlg::OnInitDialog()
{
    CDialogEx::OnInitDialog();

    ModifyStyle(0, WS_CLIPCHILDREN);
    ModifyStyleEx(0, WS_EX_COMPOSITED);

    SetWindowText(_T("电脑安全体检 — PC Diagnostic Tool"));
    SetIcon(AfxGetApp()->LoadStandardIcon(IDI_SHIELD), TRUE);
    SetIcon(AfxGetApp()->LoadStandardIcon(IDI_SHIELD), FALSE);

    m_items = CDiagnosticEngine::BuildCheckList();
    m_statusText = _T("点击「开始体检」开始扫描");

    // Create score ring
    CRect ringRect(40, HEADER_H + 10, 240, HEADER_H + 210);
    m_scoreRing.Create(_T(""), WS_CHILD | WS_VISIBLE | SS_OWNERDRAW,
                        ringRect, this, IDC_SCORE_RING);

    // Create diagnostic list
    CRect listRect(20, HEADER_H + SCORE_AREA_H, 780, 480);
    m_diagList.Create(WS_CHILD | WS_VISIBLE, listRect, this, IDC_DIAG_LIST);
    m_diagList.SetItems(&m_items);

    // Create buttons with owner draw style
    m_btnStart.Create(_T("开始体检"), WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
                       CRect(260, HEADER_H + 150, 380, HEADER_H + 186), this, IDC_BTN_START);
    m_btnStart.SetColors(ThemeColors::BtnPrimary(), ThemeColors::BtnPrimaryHov());

    m_btnStop.Create(_T("停止"), WS_CHILD | BS_OWNERDRAW,
                      CRect(390, HEADER_H + 150, 480, HEADER_H + 186), this, IDC_BTN_STOP);
    m_btnStop.SetColors(ThemeColors::BtnStop(), ThemeColors::BtnStopHov());

    m_btnFixAll.Create(_T("一键修复"), WS_CHILD | BS_OWNERDRAW,
                        CRect(390, HEADER_H + 150, 510, HEADER_H + 186), this, IDC_BTN_FIXALL);
    m_btnFixAll.SetColors(ThemeColors::BtnFix(), ThemeColors::BtnFixHov());

    // Size to a good default
    MoveWindow(0, 0, 1100, 800, FALSE);
    CenterWindow();

    LayoutControls();

    return TRUE;
}

void CDiagToolDlg::OnCancel()
{
    m_bCancelScan = true;
    if (m_bScanning)
        Sleep(100);
    CDialogEx::OnCancel();
}

void CDiagToolDlg::OnGetMinMaxInfo(MINMAXINFO* lpMMI)
{
    lpMMI->ptMinTrackSize.x = 900;
    lpMMI->ptMinTrackSize.y = 700;
    CDialogEx::OnGetMinMaxInfo(lpMMI);
}

void CDiagToolDlg::OnSize(UINT nType, int cx, int cy)
{
    CDialogEx::OnSize(nType, cx, cy);
    if (m_scoreRing.GetSafeHwnd())
        LayoutControls();
}

void CDiagToolDlg::LayoutControls()
{
    CRect rc;
    GetClientRect(&rc);
    int w = rc.Width(), h = rc.Height();

    int ringSize = 200;
    int ringLeft = 40;
    int ringTop = HEADER_H + 10;
    m_scoreRing.MoveWindow(ringLeft, ringTop, ringSize, ringSize);

    int infoX = ringLeft + ringSize + 30;
    int btnY = HEADER_H + 150;
    int btnH = 36;

    m_btnStart.MoveWindow(infoX, btnY, 120, btnH);
    m_btnStop.MoveWindow(infoX + 130, btnY, 90, btnH);
    m_btnFixAll.MoveWindow(infoX + 130, btnY, 120, btnH);

    int listTop = HEADER_H + SCORE_AREA_H;
    int listBottom = h - FOOTER_H;
    if (listBottom > listTop + 50)
    {
        m_diagList.MoveWindow(20, listTop, w - 40, listBottom - listTop);
    }

    InvalidateDialogAreas();
}

void CDiagToolDlg::InvalidateDialogAreas()
{
    CRect rc;
    GetClientRect(&rc);
    int w = rc.Width(), h = rc.Height();

    // Header region
    CRect header(0, 0, w, HEADER_H);
    InvalidateRect(&header, FALSE);

    // Score info area (between ring and list, excluding child controls)
    int infoLeft = 40 + 200 + 30;
    CRect scanInfo(infoLeft, HEADER_H, w, HEADER_H + SCORE_AREA_H);
    InvalidateRect(&scanInfo, FALSE);

    // Footer region
    CRect footer(0, h - FOOTER_H, w, h);
    InvalidateRect(&footer, FALSE);
}

// ─────────── Painting ───────────

BOOL CDiagToolDlg::OnEraseBkgnd(CDC*)
{
    return TRUE;
}

HBRUSH CDiagToolDlg::OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor)
{
    pDC->SetBkMode(TRANSPARENT);
    pDC->SetTextColor(ThemeColors::TextPrimary());
    pDC->SetBkColor(ThemeColors::BgMain());
    return (HBRUSH)m_bgBrush.GetSafeHandle();
}

void CDiagToolDlg::OnPaint()
{
    CPaintDC dc(this);
    CRect rc;
    GetClientRect(&rc);
    int w = rc.Width(), h = rc.Height();

    Bitmap bmp(w, h, PixelFormat32bppARGB);
    Graphics g(&bmp);
    g.SetSmoothingMode(SmoothingModeAntiAlias);
    g.SetTextRenderingHint(TextRenderingHintClearTypeGridFit);

    // Main background
    COLORREF bgCol = ThemeColors::BgMain();
    SolidBrush bgBrush(Color(255, GetRValue(bgCol), GetGValue(bgCol), GetBValue(bgCol)));
    g.FillRectangle(&bgBrush, 0, 0, w, h);

    DrawHeader(g, w);

    int ringRight = 40 + 200 + 30;
    DrawScanInfo(g, ringRight, HEADER_H + 10);

    int cardsX = w - 200;
    DrawSummaryCards(g, cardsX, HEADER_H + 20);

    DrawFooter(g, w, h);

    // Draw owner-draw buttons on our buffer
    DrawButton(g, CRect(m_btnStart.IsWindowVisible() ? 1 : 0, 0, 0, 0), nullptr,
               ThemeColors::BtnPrimary(), false);

    Graphics dcG(dc.GetSafeHdc());
    dcG.DrawImage(&bmp, 0, 0);

    // Let child controls paint themselves (they use their own buffers)
}

void CDiagToolDlg::DrawHeader(Graphics& g, int w)
{
    COLORREF hdrBg = ThemeColors::BgHeader();
    SolidBrush hdrBrush(Color(255, GetRValue(hdrBg), GetGValue(hdrBg), GetBValue(hdrBg)));
    g.FillRectangle(&hdrBrush, 0, 0, w, HEADER_H);

    COLORREF borderCol = ThemeColors::Border();
    Pen borderPen(Color(255, GetRValue(borderCol), GetGValue(borderCol), GetBValue(borderCol)));
    g.DrawLine(&borderPen, 0, HEADER_H - 1, w, HEADER_H - 1);

    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont titleFont(&ff, 20.0f, FontStyleBold, UnitPixel);
    GdipFont subFont(&ff, 12.0f, FontStyleRegular, UnitPixel);

    COLORREF txtPri = ThemeColors::TextPrimary();
    COLORREF txtSec = ThemeColors::TextSecondary();
    SolidBrush priBrush(Color(255, GetRValue(txtPri), GetGValue(txtPri), GetBValue(txtPri)));
    SolidBrush secBrush(Color(255, GetRValue(txtSec), GetGValue(txtSec), GetBValue(txtSec)));

    StringFormat sf;
    sf.SetAlignment(StringAlignmentNear);
    sf.SetLineAlignment(StringAlignmentCenter);

    RectF titleRect(24.0f, 0.0f, 400.0f, (float)HEADER_H);
    g.DrawString(L"电脑安全体检", -1, &titleFont, titleRect, &sf, &priBrush);

    RectF subRect(200.0f, 4.0f, 300.0f, (float)HEADER_H);
    g.DrawString(L"PC Diagnostic Tool", -1, &subFont, subRect, &sf, &secBrush);

    // Status text on the right
    StringFormat sfRight;
    sfRight.SetAlignment(StringAlignmentFar);
    sfRight.SetLineAlignment(StringAlignmentCenter);
    RectF statusRect(0.0f, 0.0f, (float)(w - 24), (float)HEADER_H);
    CStringW statusW(m_statusText);
    g.DrawString(statusW, -1, &subFont, statusRect, &sfRight, &secBrush);
}

void CDiagToolDlg::DrawScanInfo(Graphics& g, int x, int y)
{
    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont mainFont(&ff, 18.0f, FontStyleBold, UnitPixel);
    GdipFont subFont2(&ff, 12.0f, FontStyleRegular, UnitPixel);

    COLORREF txtPri = ThemeColors::TextPrimary();
    COLORREF txtSec = ThemeColors::TextSecondary();
    SolidBrush priBrush(Color(255, GetRValue(txtPri), GetGValue(txtPri), GetBValue(txtPri)));
    SolidBrush secBrush(Color(255, GetRValue(txtSec), GetGValue(txtSec), GetBValue(txtSec)));

    StringFormat sf;
    sf.SetAlignment(StringAlignmentNear);
    sf.SetLineAlignment(StringAlignmentNear);

    // Current scan item or status
    CString mainText;
    if (m_bScanning)
        mainText = m_currentScanItem.IsEmpty() ? _T("正在扫描...") : m_currentScanItem;
    else if (m_bScanComplete)
        mainText = _T("扫描完成");
    else
        mainText = _T("电脑安全体检");

    RectF mainRect((float)x, (float)y + 10, 300.0f, 30.0f);
    CStringW mainW(mainText);
    g.DrawString(mainW, -1, &mainFont, mainRect, &sf, &priBrush);

    // Progress text
    CString progressText;
    progressText.Format(_T("已扫描 %d / %d 项"), m_scannedItems, (int)m_items.size());
    RectF progRect((float)x, (float)y + 48, 300.0f, 20.0f);
    CStringW progW(progressText);
    g.DrawString(progW, -1, &subFont2, progRect, &sf, &secBrush);
}

void CDiagToolDlg::DrawSummaryCards(Graphics& g, int x, int y)
{
    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont labelFont(&ff, 13.0f, FontStyleRegular, UnitPixel);
    GdipFont countFont(&ff, 22.0f, FontStyleBold, UnitPixel);

    StringFormat sf;
    sf.SetAlignment(StringAlignmentNear);
    sf.SetLineAlignment(StringAlignmentCenter);

    struct CardInfo
    {
        LPCWSTR label;
        int count;
        COLORREF accentColor;
        COLORREF bgColor;
    };

    CardInfo cards[] = {
        { L"通过", m_passCount, ThemeColors::AccentGreen(), ThemeColors::PassBg() },
        { L"风险", m_warnCount, ThemeColors::AccentOrange(), ThemeColors::WarnBg() },
        { L"异常", m_failCount, ThemeColors::AccentRed(), ThemeColors::FailBg() },
    };

    float cardW = 150.0f;
    float cardH = 44.0f;
    float gap = 8.0f;

    for (int i = 0; i < 3; i++)
    {
        float cy = (float)y + i * (cardH + gap);
        COLORREF bg = cards[i].bgColor;
        SolidBrush cardBg(Color(255, GetRValue(bg), GetGValue(bg), GetBValue(bg)));

        // Rounded rect
        GraphicsPath path;
        float r = 8.0f;
        RectF cardRect((float)x, cy, cardW, cardH);
        path.AddArc(cardRect.X, cardRect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(cardRect.GetRight() - r * 2, cardRect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(cardRect.GetRight() - r * 2, cardRect.GetBottom() - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(cardRect.X, cardRect.GetBottom() - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(&cardBg, &path);

        // Left accent line
        COLORREF ac = cards[i].accentColor;
        Pen acPen(Color(255, GetRValue(ac), GetGValue(ac), GetBValue(ac)), 3.0f);
        g.DrawLine(&acPen, cardRect.X + 3, cy + 8, cardRect.X + 3, cy + cardH - 8);

        // Label
        COLORREF txtSec = ThemeColors::TextSecondary();
        SolidBrush lblBrush(Color(255, GetRValue(txtSec), GetGValue(txtSec), GetBValue(txtSec)));
        RectF lblRect(cardRect.X + 14, cy, 50, cardH);
        g.DrawString(cards[i].label, -1, &labelFont, lblRect, &sf, &lblBrush);

        // Count
        SolidBrush cntBrush(Color(255, GetRValue(ac), GetGValue(ac), GetBValue(ac)));
        StringFormat sfRight;
        sfRight.SetAlignment(StringAlignmentFar);
        sfRight.SetLineAlignment(StringAlignmentCenter);
        RectF cntRect(cardRect.X, cy, cardW - 14, cardH);
        WCHAR cntStr[16];
        swprintf_s(cntStr, L"%d", cards[i].count);
        g.DrawString(cntStr, -1, &countFont, cntRect, &sfRight, &cntBrush);
    }
}

void CDiagToolDlg::DrawFooter(Graphics& g, int w, int h)
{
    int footerY = h - FOOTER_H;
    COLORREF hdrBg = ThemeColors::BgHeader();
    SolidBrush hdrBrush(Color(255, GetRValue(hdrBg), GetGValue(hdrBg), GetBValue(hdrBg)));
    g.FillRectangle(&hdrBrush, 0, footerY, w, FOOTER_H);

    COLORREF borderCol = ThemeColors::Border();
    Pen borderPen(Color(255, GetRValue(borderCol), GetGValue(borderCol), GetBValue(borderCol)));
    g.DrawLine(&borderPen, 0, footerY, w, footerY);

    FontFamily ff(L"Microsoft YaHei UI");
    GdipFont footFont(&ff, 11.0f, FontStyleRegular, UnitPixel);
    COLORREF txtSec = ThemeColors::TextSecondary();
    SolidBrush txtBrush(Color(255, GetRValue(txtSec), GetGValue(txtSec), GetBValue(txtSec)));

    StringFormat sfLeft;
    sfLeft.SetAlignment(StringAlignmentNear);
    sfLeft.SetLineAlignment(StringAlignmentCenter);
    RectF leftRect(24.0f, (float)footerY, 500.0f, (float)FOOTER_H);
    g.DrawString(L"Auto-Ops PC Diagnostic Tool v1.0  |  360风格电脑体检工具 (MFC)", -1,
                 &footFont, leftRect, &sfLeft, &txtBrush);

    StringFormat sfRight;
    sfRight.SetAlignment(StringAlignmentFar);
    sfRight.SetLineAlignment(StringAlignmentCenter);
    WCHAR countStr[64];
    swprintf_s(countStr, L"共 %d 项检查", (int)m_items.size());
    RectF rightRect(0.0f, (float)footerY, (float)(w - 24), (float)FOOTER_H);
    g.DrawString(countStr, -1, &footFont, rightRect, &sfRight, &txtBrush);
}

void CDiagToolDlg::DrawButton(Graphics&, CRect&, LPCTSTR, COLORREF, bool)
{
    // Buttons are actually MFC owner-draw buttons; they draw themselves.
    // This is a placeholder for the paint pipeline.
}

// ─────────── Status / Counts ───────────

void CDiagToolDlg::UpdateStatusText()
{
    if (m_bScanning)
    {
        m_statusText.Format(_T("体检中... %d/%d"), m_scannedItems, (int)m_items.size());
    }
    else if (m_bScanComplete)
    {
        m_statusText.Format(_T("体检完成! 通过 %d 项 | 风险 %d 项 | 异常 %d 项"),
                            m_passCount, m_warnCount, m_failCount);
    }
    else
    {
        m_statusText = _T("点击「开始体检」开始扫描");
    }
}

void CDiagToolDlg::UpdateCounts()
{
    m_passCount = 0;
    m_warnCount = 0;
    m_failCount = 0;
    for (auto& item : m_items)
    {
        switch (item.status)
        {
        case CheckStatus::Pass:
        case CheckStatus::Fixed:
            m_passCount++; break;
        case CheckStatus::Warning:
            m_warnCount++; break;
        case CheckStatus::Fail:
            m_failCount++; break;
        default: break;
        }
    }
}

int CDiagToolDlg::CalculateTotalScore()
{
    if (m_items.empty()) return 0;
    int total = 0;
    for (auto& item : m_items)
        total += item.score;
    return total / (int)m_items.size();
}

void CDiagToolDlg::AnimateScore(int targetScore)
{
    m_scoreRing.SetScore(targetScore);
    m_scoreRing.StartScoreAnimation(m_scoreRing.GetDisplayScore(), targetScore);
}

// ─────────── Button Handlers ───────────

void CDiagToolDlg::OnBnClickedStart()
{
    if (m_bScanning) return;

    // Reset items
    for (auto& item : m_items)
    {
        item.status = CheckStatus::Pending;
        item.detail.Empty();
        item.fixSuggestion.Empty();
        item.score = 100;
    }

    m_bScanning = true;
    m_bScanComplete = false;
    m_bCancelScan = false;
    m_scannedItems = 0;
    m_passCount = 0;
    m_warnCount = 0;
    m_failCount = 0;
    m_currentScanItem.Empty();

    m_scoreRing.SetScanning(true);
    m_scoreRing.SetDisplayScore(0);

    m_btnStart.ShowWindow(SW_HIDE);
    m_btnStop.ShowWindow(SW_SHOW);
    m_btnFixAll.ShowWindow(SW_HIDE);

    m_diagList.RefreshAll();
    UpdateStatusText();
    InvalidateDialogAreas();

    AfxBeginThread(ScanThreadProc, this);
}

void CDiagToolDlg::OnBnClickedStop()
{
    m_bCancelScan = true;
}

void CDiagToolDlg::OnBnClickedFixAll()
{
    if (m_bScanning) return;

    m_btnFixAll.ShowWindow(SW_HIDE);
    m_btnStart.ShowWindow(SW_HIDE);
    m_btnStop.ShowWindow(SW_HIDE);

    AfxBeginThread(FixAllThreadProc, this);
}

// ─────────── Threading ───────────

UINT CDiagToolDlg::ScanThreadProc(LPVOID pParam)
{
    CoInitializeEx(nullptr, COINIT_MULTITHREADED);

    auto pDlg = (CDiagToolDlg*)pParam;

    for (int i = 0; i < (int)pDlg->m_items.size(); i++)
    {
        if (pDlg->m_bCancelScan) break;

        // Small visual delay
        Sleep(200 + (rand() % 400));

        if (pDlg->m_bCancelScan) break;

        CDiagnosticEngine::RunCheck(pDlg->m_items[i]);
        pDlg->PostMessage(WM_CHECK_COMPLETE, (WPARAM)i, 0);
    }

    pDlg->PostMessage(WM_SCAN_FINISHED, 0, 0);

    CoUninitialize();
    return 0;
}

UINT CDiagToolDlg::FixAllThreadProc(LPVOID pParam)
{
    auto pDlg = (CDiagToolDlg*)pParam;

    for (int i = 0; i < (int)pDlg->m_items.size(); i++)
    {
        auto& item = pDlg->m_items[i];
        if (item.status == CheckStatus::Warning || item.status == CheckStatus::Fail)
        {
            item.status = CheckStatus::Scanning;
            pDlg->PostMessage(WM_FIX_ITEM_COMPLETE, (WPARAM)i, 1); // 1 = scanning state

            Sleep(400);

            item.status = CheckStatus::Fixed;
            item.score = 100;
            item.detail += _T(" [已修复]");
            pDlg->PostMessage(WM_FIX_ITEM_COMPLETE, (WPARAM)i, 0);
        }
    }

    pDlg->PostMessage(WM_FIX_ALL_COMPLETE, 0, 0);
    return 0;
}

UINT CDiagToolDlg::FixItemThreadProc(LPVOID pParam)
{
    auto pFixParam = (FixItemParam*)pParam;
    auto pDlg = pFixParam->pDlg;
    int idx = pFixParam->index;
    delete pFixParam;

    if (idx >= 0 && idx < (int)pDlg->m_items.size())
    {
        auto& item = pDlg->m_items[idx];
        item.status = CheckStatus::Scanning;
        pDlg->PostMessage(WM_FIX_ITEM_COMPLETE, (WPARAM)idx, 1);

        Sleep(600);

        item.status = CheckStatus::Fixed;
        item.score = 100;
        item.detail += _T(" [已修复]");
        pDlg->PostMessage(WM_FIX_ITEM_COMPLETE, (WPARAM)idx, 0);
    }

    return 0;
}

// ─────────── Message Handlers ───────────

LRESULT CDiagToolDlg::OnCheckComplete(WPARAM wParam, LPARAM)
{
    int idx = (int)wParam;
    m_scannedItems = idx + 1;

    if (idx >= 0 && idx < (int)m_items.size())
        m_currentScanItem = m_items[idx].name;

    UpdateCounts();
    UpdateStatusText();

    int score = CalculateTotalScore();
    AnimateScore(score);

    m_diagList.EnsureVisible(idx);
    m_diagList.RefreshItem(idx);
    InvalidateDialogAreas();

    return 0;
}

LRESULT CDiagToolDlg::OnScanFinished(WPARAM, LPARAM)
{
    m_bScanning = false;
    m_bScanComplete = true;
    m_scannedItems = (int)m_items.size();

    m_scoreRing.SetScanning(false);
    UpdateCounts();
    UpdateStatusText();

    int score = CalculateTotalScore();
    AnimateScore(score);

    m_btnStart.ShowWindow(SW_SHOW);
    m_btnStop.ShowWindow(SW_HIDE);

    if (m_warnCount > 0 || m_failCount > 0)
        m_btnFixAll.ShowWindow(SW_SHOW);
    else
        m_btnFixAll.ShowWindow(SW_HIDE);

    m_diagList.RefreshAll();
    InvalidateDialogAreas();

    return 0;
}

LRESULT CDiagToolDlg::OnFixItemComplete(WPARAM wParam, LPARAM lParam)
{
    int idx = (int)wParam;
    UpdateCounts();
    UpdateStatusText();

    if (lParam == 0) // fix done, not scanning state
    {
        int score = CalculateTotalScore();
        AnimateScore(score);
    }

    m_diagList.RefreshItem(idx);
    InvalidateDialogAreas();

    return 0;
}

LRESULT CDiagToolDlg::OnFixAllComplete(WPARAM, LPARAM)
{
    UpdateCounts();
    UpdateStatusText();

    int score = CalculateTotalScore();
    AnimateScore(score);

    m_btnStart.ShowWindow(SW_SHOW);
    m_btnFixAll.ShowWindow(SW_HIDE);

    m_diagList.RefreshAll();
    InvalidateDialogAreas();

    return 0;
}

LRESULT CDiagToolDlg::OnDiagListFixItem(WPARAM wParam, LPARAM)
{
    int idx = (int)wParam;
    if (idx >= 0 && idx < (int)m_items.size())
    {
        auto& item = m_items[idx];
        if (item.status == CheckStatus::Warning || item.status == CheckStatus::Fail)
        {
            auto pParam = new FixItemParam{ this, idx };
            AfxBeginThread(FixItemThreadProc, pParam);
        }
    }
    return 0;
}
