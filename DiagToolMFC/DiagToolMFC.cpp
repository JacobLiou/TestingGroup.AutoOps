#include "stdafx.h"
#include "DiagToolMFC.h"
#include "DiagToolDlg.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif

BEGIN_MESSAGE_MAP(CDiagToolApp, CWinApp)
END_MESSAGE_MAP()

CDiagToolApp theApp;

CDiagToolApp::CDiagToolApp()
{
    m_dwRestartManagerSupportFlags = AFX_RESTART_MANAGER_SUPPORT_RESTART;
}

BOOL CDiagToolApp::InitInstance()
{
    INITCOMMONCONTROLSEX icc;
    icc.dwSize = sizeof(icc);
    icc.dwICC = ICC_WIN95_CLASSES;
    InitCommonControlsEx(&icc);

    CWinApp::InitInstance();

    AfxEnableControlContainer();

    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr))
        CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    SetRegistryKey(_T("AutoOps-DiagTool"));

    CDiagToolDlg dlg;
    m_pMainWnd = &dlg;

    dlg.DoModal();

    CoUninitialize();

    return FALSE;
}
