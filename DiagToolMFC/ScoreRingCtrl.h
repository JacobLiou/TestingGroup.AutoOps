#pragma once
#include "DiagnosticItem.h"

class CScoreRingCtrl : public CStatic
{
    DECLARE_DYNAMIC(CScoreRingCtrl)

public:
    CScoreRingCtrl();
    virtual ~CScoreRingCtrl();

    void SetScore(int score);
    void SetDisplayScore(int score);
    int  GetDisplayScore() const { return m_displayScore; }
    void SetScanning(bool scanning);
    bool IsScanning() const { return m_bScanning; }

    void StartScoreAnimation(int from, int to);
    void SetSubText(const CString& text) { m_subText = text; InvalidateRect(nullptr, FALSE); }

protected:
    DECLARE_MESSAGE_MAP()
    afx_msg void OnPaint();
    afx_msg BOOL OnEraseBkgnd(CDC* pDC);
    afx_msg void OnTimer(UINT_PTR nIDEvent);
    afx_msg void OnDestroy();
    afx_msg void OnSize(UINT nType, int cx, int cy);

private:
    void DrawToBuffer(Gdiplus::Graphics& g, int w, int h);
    void DrawGlow(Gdiplus::Graphics& g, Gdiplus::RectF ringRect);
    void DrawBackgroundRing(Gdiplus::Graphics& g, Gdiplus::RectF ringRect);
    void DrawScoreArc(Gdiplus::Graphics& g, Gdiplus::RectF ringRect);
    void DrawDashedRing(Gdiplus::Graphics& g, Gdiplus::RectF ringRect);
    void DrawScoreText(Gdiplus::Graphics& g, Gdiplus::RectF ringRect);

    int m_score = 0;
    int m_displayScore = 0;
    int m_targetScore = 0;
    bool m_bScanning = false;
    float m_rotationAngle = 0.0f;
    float m_glowAlpha = 0.05f;
    bool m_glowIncreasing = true;
    CString m_subText = _T("健康评分");

    static const UINT_PTR TIMER_SPIN = 1;
    static const UINT_PTR TIMER_GLOW = 2;
    static const UINT_PTR TIMER_SCORE = 3;
};
