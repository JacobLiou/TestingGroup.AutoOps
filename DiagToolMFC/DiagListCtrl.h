#pragma once
#include "DiagnosticItem.h"
#include <vector>

#define WM_DIAGLIST_FIX_ITEM (WM_USER + 200)

class CDiagListCtrl : public CWnd
{
    DECLARE_DYNAMIC(CDiagListCtrl)

public:
    CDiagListCtrl();
    virtual ~CDiagListCtrl();

    BOOL Create(DWORD dwStyle, const RECT& rect, CWnd* pParent, UINT nID);

    void SetItems(std::vector<DiagnosticItem>* pItems);
    void RefreshItem(int index);
    void RefreshAll();
    void EnsureVisible(int index);

    int GetHoverFixButton() const { return m_hoverFixBtn; }

protected:
    DECLARE_MESSAGE_MAP()
    afx_msg void OnPaint();
    afx_msg BOOL OnEraseBkgnd(CDC* pDC);
    afx_msg void OnSize(UINT nType, int cx, int cy);
    afx_msg void OnVScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar);
    afx_msg BOOL OnMouseWheel(UINT nFlags, short zDelta, CPoint pt);
    afx_msg void OnMouseMove(UINT nFlags, CPoint point);
    afx_msg void OnLButtonUp(UINT nFlags, CPoint point);
    afx_msg void OnMouseLeave();

private:
    void DrawToBuffer(Gdiplus::Graphics& g, int w, int h);
    void DrawHeader(Gdiplus::Graphics& g, int w, float y);
    void DrawRow(Gdiplus::Graphics& g, int w, float y, int index);
    void DrawFixButton(Gdiplus::Graphics& g, Gdiplus::RectF btnRect, bool hover);
    void UpdateScrollInfo();
    int HitTestFixButton(CPoint pt);

    std::vector<DiagnosticItem>* m_pItems = nullptr;

    static const int ROW_HEIGHT = 40;
    static const int HEADER_HEIGHT = 36;
    int m_scrollPos = 0;
    int m_hoverFixBtn = -1;
    bool m_tracking = false;

    static const int COL_CATEGORY = 60;
    static const int COL_NAME = 180;
    static const int COL_STATUS = 120;
    static const int COL_ACTION = 80;
};
