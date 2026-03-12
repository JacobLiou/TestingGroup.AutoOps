#pragma once
#include <gdiplus.h>

class CGdiplusHelper
{
public:
    CGdiplusHelper()
    {
        Gdiplus::GdiplusStartupInput input;
        Gdiplus::GdiplusStartup(&m_token, &input, nullptr);
    }
    ~CGdiplusHelper()
    {
        Gdiplus::GdiplusShutdown(m_token);
    }
    CGdiplusHelper(const CGdiplusHelper&) = delete;
    CGdiplusHelper& operator=(const CGdiplusHelper&) = delete;

private:
    ULONG_PTR m_token = 0;
};
