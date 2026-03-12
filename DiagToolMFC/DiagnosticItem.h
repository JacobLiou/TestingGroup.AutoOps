#pragma once
#include <afxstr.h>

enum class CheckStatus
{
    Pending,
    Scanning,
    Pass,
    Warning,
    Fail,
    Fixed
};

enum class CheckCategory
{
    System,
    Disk,
    Network,
    Security,
    Software,
    Performance
};

struct DiagnosticItem
{
    CString id;
    CString name;
    CheckCategory category = CheckCategory::System;
    CheckStatus status = CheckStatus::Pending;
    CString detail;
    CString fixSuggestion;
    int score = 100;

    CString GetCategoryIcon() const
    {
        switch (category)
        {
        case CheckCategory::System:      return _T("[SYS]");
        case CheckCategory::Disk:        return _T("[DSK]");
        case CheckCategory::Network:     return _T("[NET]");
        case CheckCategory::Security:    return _T("[SEC]");
        case CheckCategory::Software:    return _T("[SFT]");
        case CheckCategory::Performance: return _T("[PRF]");
        default:                         return _T("[?]");
        }
    }

    CString GetCategoryText() const
    {
        switch (category)
        {
        case CheckCategory::System:      return _T("系统");
        case CheckCategory::Disk:        return _T("磁盘");
        case CheckCategory::Network:     return _T("网络");
        case CheckCategory::Security:    return _T("安全");
        case CheckCategory::Software:    return _T("软件");
        case CheckCategory::Performance: return _T("性能");
        default:                         return _T("?");
        }
    }

    CString GetStatusText() const
    {
        switch (status)
        {
        case CheckStatus::Pending:  return _T("等待检测");
        case CheckStatus::Scanning: return _T("正在扫描...");
        case CheckStatus::Pass:     return _T("正常");
        case CheckStatus::Warning:  return _T("存在风险");
        case CheckStatus::Fail:     return _T("异常");
        case CheckStatus::Fixed:    return _T("已修复");
        default:                    return _T("");
        }
    }
};

namespace ThemeColors
{
    inline COLORREF BgMain()        { return RGB(0x1A, 0x1A, 0x2E); }
    inline COLORREF BgHeader()      { return RGB(0x16, 0x21, 0x3E); }
    inline COLORREF BgDarker()      { return RGB(0x0D, 0x1B, 0x2A); }
    inline COLORREF BgCard()        { return RGB(0x1E, 0x25, 0x40); }
    inline COLORREF BgRow()         { return RGB(0x14, 0x1C, 0x33); }
    inline COLORREF BgRowAlt()      { return RGB(0x18, 0x22, 0x3A); }
    inline COLORREF Border()        { return RGB(0x2A, 0x35, 0x55); }

    inline COLORREF TextPrimary()   { return RGB(0xE8, 0xEA, 0xED); }
    inline COLORREF TextSecondary() { return RGB(0x8A, 0x92, 0xA8); }

    inline COLORREF AccentGreen()   { return RGB(0x00, 0xC8, 0x53); }
    inline COLORREF AccentOrange()  { return RGB(0xFF, 0x98, 0x00); }
    inline COLORREF AccentRed()     { return RGB(0xF4, 0x43, 0x36); }
    inline COLORREF AccentBlue()    { return RGB(0x42, 0xA5, 0xF5); }

    inline COLORREF BtnPrimary()    { return RGB(0x00, 0xC8, 0x53); }
    inline COLORREF BtnPrimaryHov() { return RGB(0x00, 0xE6, 0x76); }
    inline COLORREF BtnFix()        { return RGB(0xFF, 0x98, 0x00); }
    inline COLORREF BtnFixHov()     { return RGB(0xFF, 0xB3, 0x00); }
    inline COLORREF BtnStop()       { return RGB(0xF4, 0x43, 0x36); }
    inline COLORREF BtnStopHov()    { return RGB(0xEF, 0x53, 0x50); }

    inline COLORREF PassBg()        { return RGB(0x0A, 0x2E, 0x1A); }
    inline COLORREF WarnBg()        { return RGB(0x2E, 0x24, 0x0A); }
    inline COLORREF FailBg()        { return RGB(0x2E, 0x0A, 0x0A); }
    inline COLORREF FixedBg()       { return RGB(0x0A, 0x1E, 0x2E); }

    inline COLORREF StatusColor(CheckStatus s)
    {
        switch (s)
        {
        case CheckStatus::Pass:    return AccentGreen();
        case CheckStatus::Warning: return AccentOrange();
        case CheckStatus::Fail:    return AccentRed();
        case CheckStatus::Fixed:   return AccentBlue();
        case CheckStatus::Scanning:return RGB(0x64, 0xB5, 0xF6);
        default:                   return TextSecondary();
        }
    }

    inline COLORREF RowBgForStatus(CheckStatus s)
    {
        switch (s)
        {
        case CheckStatus::Warning: return WarnBg();
        case CheckStatus::Fail:    return FailBg();
        case CheckStatus::Fixed:   return FixedBg();
        default:                   return BgRow();
        }
    }

    inline COLORREF ScoreColor(int score)
    {
        if (score >= 80) return AccentGreen();
        if (score >= 60) return AccentOrange();
        return AccentRed();
    }
}
