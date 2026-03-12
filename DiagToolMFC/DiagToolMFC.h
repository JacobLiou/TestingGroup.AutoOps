#pragma once

#ifndef __AFXWIN_H__
    #error "include 'stdafx.h' before including this file for PCH"
#endif

#include "Resource.h"
#include "GdiplusHelper.h"

class CDiagToolApp : public CWinApp
{
public:
    CDiagToolApp();

    virtual BOOL InitInstance() override;

    DECLARE_MESSAGE_MAP()

private:
    CGdiplusHelper m_gdiplusHelper;
};

extern CDiagToolApp theApp;
