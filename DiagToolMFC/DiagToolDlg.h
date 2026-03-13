#pragma once

#include "Resource.h"
#include "DiagnosticItem.h"
#include "DiagnosticEngine.h"
#include "ScoreRingCtrl.h"
#include "DiagListCtrl.h"
#include "ThemedButton.h"

#include <vector>
#include <atomic>

class CDiagToolDlg : public CDialogEx
{
public:
    CDiagToolDlg(CWnd* pParent = nullptr);

    enum { IDD = IDD_DIAGTOOL_DIALOG };

protected:
    virtual void DoDataExchange(CDataExchange* pDX) override;
    virtual BOOL OnInitDialog() override;
    virtual void OnOK() override {}
    virtual void OnCancel() override;

    DECLARE_MESSAGE_MAP()
    afx_msg void OnPaint();
    afx_msg BOOL OnEraseBkgnd(CDC* pDC);
    afx_msg HBRUSH OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor);
    afx_msg void OnSize(UINT nType, int cx, int cy);
    afx_msg void OnGetMinMaxInfo(MINMAXINFO* lpMMI);
    afx_msg void OnBnClickedStart();
    afx_msg void OnBnClickedStop();
    afx_msg void OnBnClickedFixAll();
    afx_msg LRESULT OnCheckComplete(WPARAM wParam, LPARAM lParam);
    afx_msg LRESULT OnScanFinished(WPARAM wParam, LPARAM lParam);
    afx_msg LRESULT OnFixItemComplete(WPARAM wParam, LPARAM lParam);
    afx_msg LRESULT OnFixAllComplete(WPARAM wParam, LPARAM lParam);
    afx_msg LRESULT OnDiagListFixItem(WPARAM wParam, LPARAM lParam);

private:
    void LayoutControls();
    void InvalidateDialogAreas();
    void UpdateStatusText();
    void UpdateCounts();
    int  CalculateTotalScore();
    void AnimateScore(int targetScore);

    void DrawHeader(Gdiplus::Graphics& g, int w);
    void DrawSummaryCards(Gdiplus::Graphics& g, int x, int y);
    void DrawScanInfo(Gdiplus::Graphics& g, int x, int y);
    void DrawFooter(Gdiplus::Graphics& g, int w, int h);

    static UINT ScanThreadProc(LPVOID pParam);
    static UINT FixAllThreadProc(LPVOID pParam);
    static UINT FixItemThreadProc(LPVOID pParam);

    std::vector<DiagnosticItem> m_items;
    CScoreRingCtrl m_scoreRing;
    CDiagListCtrl m_diagList;

    CThemedButton m_btnStart;
    CThemedButton m_btnStop;
    CThemedButton m_btnFixAll;

    bool m_bScanning = false;
    bool m_bScanComplete = false;
    std::atomic<bool> m_bCancelScan{ false };

    int m_passCount = 0;
    int m_warnCount = 0;
    int m_failCount = 0;
    int m_scannedItems = 0;
    CString m_currentScanItem;
    CString m_statusText;

    CBrush m_bgBrush;
    CBrush m_headerBrush;

    struct FixItemParam
    {
        CDiagToolDlg* pDlg;
        int index;
    };

    static const int HEADER_H = 56;
    static const int SCORE_AREA_H = 220;
    static const int FOOTER_H = 40;
};
