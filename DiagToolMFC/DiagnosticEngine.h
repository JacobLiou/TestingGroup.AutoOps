#pragma once
#include "DiagnosticItem.h"
#include <vector>

class CDiagnosticEngine
{
public:
    static std::vector<DiagnosticItem> BuildCheckList();
    static void RunCheck(DiagnosticItem& item);

private:
    // System
    static void CheckOsVersion(DiagnosticItem& item);
    static void CheckUptime(DiagnosticItem& item);
    static void CheckWindowsUpdate(DiagnosticItem& item);
    static void CheckTimeSync(DiagnosticItem& item);

    // Disk
    static void CheckSystemDisk(DiagnosticItem& item);
    static void CheckAllDisks(DiagnosticItem& item);
    static void CheckTempFolder(DiagnosticItem& item);

    // Network
    static void CheckNetworkAvailable(DiagnosticItem& item);
    static void CheckDns(DiagnosticItem& item);
    static void CheckInternet(DiagnosticItem& item);

    // Security
    static void CheckFirewall(DiagnosticItem& item);
    static void CheckAntivirus(DiagnosticItem& item);
    static void CheckUac(DiagnosticItem& item);
    static void CheckAutoLogin(DiagnosticItem& item);

    // Software
    static void CheckStartupItems(DiagnosticItem& item);
    static void CheckInstalledPrograms(DiagnosticItem& item);
    static void CheckComPorts(DiagnosticItem& item);

    // Performance
    static void CheckCpu(DiagnosticItem& item);
    static void CheckMemory(DiagnosticItem& item);
    static void CheckProcesses(DiagnosticItem& item);

    // Helpers
    static CString ReadRegistryString(HKEY hRoot, LPCTSTR subKey, LPCTSTR valueName);
    static DWORD ReadRegistryDword(HKEY hRoot, LPCTSTR subKey, LPCTSTR valueName, DWORD defaultVal = 0);
    static int CountRegistrySubKeys(HKEY hRoot, LPCTSTR subKey);
    static int CountRegistryValues(HKEY hRoot, LPCTSTR subKey);
};
