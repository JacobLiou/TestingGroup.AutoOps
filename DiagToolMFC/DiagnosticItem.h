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
    // Light Theme Colors
    inline COLORREF BgMain()        { return RGB(0xF5, 0xF7, 0xFA); } // ThemeBg
    inline COLORREF BgHeader()      { return RGB(0xFF, 0xFF, 0xFF); } // ThemeHeaderBg
    inline COLORREF BgDarker()      { return RGB(0xE4, 0xE7, 0xEB); } // ThemeCardBorder
    inline COLORREF BgCard()        { return RGB(0xFF, 0xFF, 0xFF); } // ThemeCardBg
    inline COLORREF BgRow()         { return RGB(0xFF, 0xFF, 0xFF); }
    inline COLORREF BgRowAlt()      { return RGB(0xF7, 0xFA, 0xFC); }
    inline COLORREF Border()        { return RGB(0xE4, 0xE7, 0xEB); } // ThemeCardBorder

    inline COLORREF TextPrimary()   { return RGB(0x2D, 0x37, 0x48); } // ThemeTextPrimary
    inline COLORREF TextSecondary() { return RGB(0x71, 0x80, 0x96); } // ThemeTextSecondary

    inline COLORREF AccentGreen()   { return RGB(0x38, 0xA1, 0x69); } // ThemeAccentGreen
    inline COLORREF AccentOrange()  { return RGB(0xDD, 0x6B, 0x20); } // ThemeAccentOrange
    inline COLORREF AccentRed()     { return RGB(0xE5, 0x3E, 0x3E); } // ThemeAccentRed
    inline COLORREF AccentBlue()    { return RGB(0x31, 0x82, 0xCE); } 

    inline COLORREF BtnPrimary()    { return RGB(0x38, 0xA1, 0x69); } // ThemeAccentGreen
    inline COLORREF BtnPrimaryHov() { return RGB(0x2F, 0x85, 0x5A); }
    inline COLORREF BtnFix()        { return RGB(0xDD, 0x6B, 0x20); } // ThemeAccentOrange
    inline COLORREF BtnFixHov()     { return RGB(0xC0, 0x56, 0x21); }
    inline COLORREF BtnStop()       { return RGB(0xE5, 0x3E, 0x3E); } // ThemeAccentRed
    inline COLORREF BtnStopHov()    { return RGB(0xC5, 0x30, 0x30); }

    inline COLORREF PassBg()        { return RGB(0xE6, 0xFF, 0xED); } // ThemePassCardBg
    inline COLORREF WarnBg()        { return RGB(0xFF, 0xF8, 0xD6); } // ThemeWarnCardBg
    inline COLORREF FailBg()        { return RGB(0xFF, 0xEB, 0xEB); } // ThemeFailCardBg
    inline COLORREF FixedBg()       { return RGB(0xE1, 0xF5, 0xFE); }

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
