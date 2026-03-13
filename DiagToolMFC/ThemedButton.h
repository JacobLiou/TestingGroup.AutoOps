#pragma once
#include "DiagnosticItem.h"

class CThemedButton : public CButton
{
    DECLARE_DYNAMIC(CThemedButton)

public:
    CThemedButton();

    void SetColors(COLORREF bg, COLORREF bgHover, COLORREF text = RGB(0,0,0));

protected:
    DECLARE_MESSAGE_MAP()
    virtual void DrawItem(LPDRAWITEMSTRUCT lpDIS) override;
    afx_msg void OnMouseMove(UINT nFlags, CPoint point);
    afx_msg void OnMouseLeave();

private:
    COLORREF m_bgColor;
    COLORREF m_bgHoverColor;
    COLORREF m_textColor;
    bool m_bHover = false;
    bool m_bTracking = false;
};
