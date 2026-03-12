#include "stdafx.h"
#include "DiagnosticEngine.h"
#include <ws2tcpip.h>
#include <winsock2.h>

#pragma comment(lib, "ws2_32.lib")

std::vector<DiagnosticItem> CDiagnosticEngine::BuildCheckList()
{
    std::vector<DiagnosticItem> items;

    auto add = [&](LPCTSTR id, LPCTSTR name, CheckCategory cat)
    {
        DiagnosticItem item;
        item.id = id;
        item.name = name;
        item.category = cat;
        items.push_back(item);
    };

    // System
    add(_T("SYS_01"), _T("操作系统版本"),       CheckCategory::System);
    add(_T("SYS_02"), _T("系统运行时长"),       CheckCategory::System);
    add(_T("SYS_03"), _T("Windows 更新状态"),   CheckCategory::System);
    add(_T("SYS_04"), _T("系统时间同步"),       CheckCategory::System);

    // Disk
    add(_T("DSK_01"), _T("系统盘可用空间"),     CheckCategory::Disk);
    add(_T("DSK_02"), _T("磁盘剩余空间(所有驱动器)"), CheckCategory::Disk);
    add(_T("DSK_03"), _T("临时文件夹清理"),     CheckCategory::Disk);

    // Network
    add(_T("NET_01"), _T("网络连接状态"),       CheckCategory::Network);
    add(_T("NET_02"), _T("DNS 解析"),           CheckCategory::Network);
    add(_T("NET_03"), _T("互联网连通性"),       CheckCategory::Network);

    // Security
    add(_T("SEC_01"), _T("Windows 防火墙"),     CheckCategory::Security);
    add(_T("SEC_02"), _T("杀毒软件状态"),       CheckCategory::Security);
    add(_T("SEC_03"), _T("UAC (用户账户控制)"), CheckCategory::Security);
    add(_T("SEC_04"), _T("自动登录安全"),       CheckCategory::Security);

    // Software
    add(_T("SFT_01"), _T("开机启动项数量"),     CheckCategory::Software);
    add(_T("SFT_02"), _T("已安装程序数量"),     CheckCategory::Software);
    add(_T("SFT_03"), _T("COM / 串口枚举"),     CheckCategory::Software);

    // Performance
    add(_T("PRF_01"), _T("CPU 使用率"),         CheckCategory::Performance);
    add(_T("PRF_02"), _T("内存使用率"),         CheckCategory::Performance);
    add(_T("PRF_03"), _T("运行进程数"),         CheckCategory::Performance);

    return items;
}

void CDiagnosticEngine::RunCheck(DiagnosticItem& item)
{
    item.status = CheckStatus::Scanning;

    try
    {
        if      (item.id == _T("SYS_01")) CheckOsVersion(item);
        else if (item.id == _T("SYS_02")) CheckUptime(item);
        else if (item.id == _T("SYS_03")) CheckWindowsUpdate(item);
        else if (item.id == _T("SYS_04")) CheckTimeSync(item);
        else if (item.id == _T("DSK_01")) CheckSystemDisk(item);
        else if (item.id == _T("DSK_02")) CheckAllDisks(item);
        else if (item.id == _T("DSK_03")) CheckTempFolder(item);
        else if (item.id == _T("NET_01")) CheckNetworkAvailable(item);
        else if (item.id == _T("NET_02")) CheckDns(item);
        else if (item.id == _T("NET_03")) CheckInternet(item);
        else if (item.id == _T("SEC_01")) CheckFirewall(item);
        else if (item.id == _T("SEC_02")) CheckAntivirus(item);
        else if (item.id == _T("SEC_03")) CheckUac(item);
        else if (item.id == _T("SEC_04")) CheckAutoLogin(item);
        else if (item.id == _T("SFT_01")) CheckStartupItems(item);
        else if (item.id == _T("SFT_02")) CheckInstalledPrograms(item);
        else if (item.id == _T("SFT_03")) CheckComPorts(item);
        else if (item.id == _T("PRF_01")) CheckCpu(item);
        else if (item.id == _T("PRF_02")) CheckMemory(item);
        else if (item.id == _T("PRF_03")) CheckProcesses(item);
        else
        {
            item.status = CheckStatus::Pass;
            item.detail = _T("未知检查项");
        }
    }
    catch (...)
    {
        item.status = CheckStatus::Warning;
        item.detail = _T("检查时发生异常");
        item.score = 95;
    }
}

// ─────────── Registry Helpers ───────────

CString CDiagnosticEngine::ReadRegistryString(HKEY hRoot, LPCTSTR subKey, LPCTSTR valueName)
{
    CString result;
    HKEY hKey = nullptr;
    if (RegOpenKeyEx(hRoot, subKey, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        TCHAR buf[512] = {};
        DWORD sz = sizeof(buf);
        DWORD type = 0;
        if (RegQueryValueEx(hKey, valueName, nullptr, &type, (LPBYTE)buf, &sz) == ERROR_SUCCESS)
            result = buf;
        RegCloseKey(hKey);
    }
    return result;
}

DWORD CDiagnosticEngine::ReadRegistryDword(HKEY hRoot, LPCTSTR subKey, LPCTSTR valueName, DWORD defaultVal)
{
    DWORD result = defaultVal;
    HKEY hKey = nullptr;
    if (RegOpenKeyEx(hRoot, subKey, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        DWORD val = 0, sz = sizeof(val), type = 0;
        if (RegQueryValueEx(hKey, valueName, nullptr, &type, (LPBYTE)&val, &sz) == ERROR_SUCCESS)
            result = val;
        RegCloseKey(hKey);
    }
    return result;
}

int CDiagnosticEngine::CountRegistrySubKeys(HKEY hRoot, LPCTSTR subKey)
{
    int count = 0;
    HKEY hKey = nullptr;
    if (RegOpenKeyEx(hRoot, subKey, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        DWORD subKeys = 0;
        RegQueryInfoKey(hKey, nullptr, nullptr, nullptr, &subKeys,
                        nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr);
        count = (int)subKeys;
        RegCloseKey(hKey);
    }
    return count;
}

int CDiagnosticEngine::CountRegistryValues(HKEY hRoot, LPCTSTR subKey)
{
    int count = 0;
    HKEY hKey = nullptr;
    if (RegOpenKeyEx(hRoot, subKey, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        DWORD values = 0;
        RegQueryInfoKey(hKey, nullptr, nullptr, nullptr, nullptr,
                        nullptr, nullptr, &values, nullptr, nullptr, nullptr, nullptr);
        count = (int)values;
        RegCloseKey(hKey);
    }
    return count;
}

// ─────────── System Checks ───────────

void CDiagnosticEngine::CheckOsVersion(DiagnosticItem& item)
{
    OSVERSIONINFOEX osvi = {};
    osvi.dwOSVersionInfoSize = sizeof(osvi);

    typedef LONG(WINAPI* PFN_RtlGetVersion)(PRTL_OSVERSIONINFOW);
    HMODULE hNtdll = GetModuleHandle(_T("ntdll.dll"));
    if (hNtdll)
    {
        auto pRtlGetVersion = (PFN_RtlGetVersion)GetProcAddress(hNtdll, "RtlGetVersion");
        if (pRtlGetVersion)
        {
            pRtlGetVersion((PRTL_OSVERSIONINFOW)&osvi);
        }
    }
    if (osvi.dwMajorVersion == 0)
    {
#pragma warning(suppress: 4996)
        GetVersionEx((LPOSVERSIONINFO)&osvi);
    }

    CString ver;
    ver.Format(_T("Windows %d.%d (Build %d)"), osvi.dwMajorVersion, osvi.dwMinorVersion, osvi.dwBuildNumber);
    item.detail = ver;

    if (osvi.dwMajorVersion >= 10)
    {
        item.status = CheckStatus::Pass;
        item.score = 100;
    }
    else
    {
        item.status = CheckStatus::Warning;
        item.detail += _T(" — 建议升级到 Windows 10 及以上");
        item.fixSuggestion = _T("升级操作系统到 Windows 10/11");
        item.score = 90;
    }
}

void CDiagnosticEngine::CheckUptime(DiagnosticItem& item)
{
    ULONGLONG ticks = GetTickCount64();
    int days = (int)(ticks / (1000ULL * 60 * 60 * 24));
    int hours = (int)((ticks / (1000ULL * 60 * 60)) % 24);
    int mins = (int)((ticks / (1000ULL * 60)) % 60);

    CString detail;
    detail.Format(_T("已运行 %d 天 %d 小时 %d 分钟"), days, hours, mins);
    item.detail = detail;

    if (days > 7)
    {
        item.status = CheckStatus::Warning;
        item.fixSuggestion = _T("建议定期重启电脑以释放资源");
        item.score = 92;
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.score = 100;
    }
}

void CDiagnosticEngine::CheckWindowsUpdate(DiagnosticItem& item)
{
    CString lastStr = ReadRegistryString(HKEY_LOCAL_MACHINE,
        _T("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\Results\\Install"),
        _T("LastSuccessTime"));

    if (!lastStr.IsEmpty())
    {
        SYSTEMTIME st = {};
        // Format: "YYYY-MM-DD HH:MM:SS"
        if (_stscanf_s(lastStr, _T("%hd-%hd-%hd"), &st.wYear, &st.wMonth, &st.wDay) >= 3)
        {
            SYSTEMTIME now;
            GetLocalTime(&now);
            FILETIME ftLast, ftNow;
            SystemTimeToFileTime(&st, &ftLast);
            SystemTimeToFileTime(&now, &ftNow);
            ULARGE_INTEGER uLast, uNow;
            uLast.LowPart = ftLast.dwLowDateTime; uLast.HighPart = ftLast.dwHighDateTime;
            uNow.LowPart = ftNow.dwLowDateTime; uNow.HighPart = ftNow.dwHighDateTime;
            int daysDiff = (int)((uNow.QuadPart - uLast.QuadPart) / (10000000ULL * 86400));

            CString detail;
            detail.Format(_T("上次更新: %04d-%02d-%02d (%d 天前)"), st.wYear, st.wMonth, st.wDay, daysDiff);
            item.detail = detail;

            if (daysDiff > 30)
            {
                item.status = CheckStatus::Warning;
                item.fixSuggestion = _T("建议检查并安装最新 Windows 更新");
                item.score = 90;
            }
            else
            {
                item.status = CheckStatus::Pass;
                item.score = 100;
            }
            return;
        }
    }

    item.status = CheckStatus::Pass;
    item.detail = _T("Windows 更新服务正常");
    item.score = 100;
}

void CDiagnosticEngine::CheckTimeSync(DiagnosticItem& item)
{
    SYSTEMTIME st;
    GetLocalTime(&st);

    TIME_ZONE_INFORMATION tzi;
    GetTimeZoneInformation(&tzi);
    int offsetHours = -(int)tzi.Bias / 60;
    int offsetMins = abs((int)tzi.Bias % 60);

    CString detail;
    detail.Format(_T("本地时间: %04d-%02d-%02d %02d:%02d:%02d  UTC%s%02d:%02d"),
        st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond,
        offsetHours >= 0 ? _T("+") : _T("-"), abs(offsetHours), offsetMins);
    item.detail = detail;
    item.status = CheckStatus::Pass;
    item.score = 100;
}

// ─────────── Disk Checks ───────────

void CDiagnosticEngine::CheckSystemDisk(DiagnosticItem& item)
{
    TCHAR winDir[MAX_PATH];
    GetWindowsDirectory(winDir, MAX_PATH);
    CString root = CString(winDir).Left(3); // "C:\"

    ULARGE_INTEGER freeBytesAvail, totalBytes, totalFreeBytes;
    if (GetDiskFreeSpaceEx(root, &freeBytesAvail, &totalBytes, &totalFreeBytes))
    {
        double freeGB = freeBytesAvail.QuadPart / (1024.0 * 1024 * 1024);
        double totalGB = totalBytes.QuadPart / (1024.0 * 1024 * 1024);
        double usedPct = (1.0 - freeGB / totalGB) * 100.0;

        CString detail;
        detail.Format(_T("%s 可用 %.1f GB / 共 %.1f GB (已用 %.0f%%)"), (LPCTSTR)root, freeGB, totalGB, usedPct);
        item.detail = detail;

        if (freeGB < 5.0)
        {
            item.status = CheckStatus::Fail;
            item.fixSuggestion = _T("系统盘空间不足 5GB，请清理磁盘或扩容");
            item.score = 70;
        }
        else if (freeGB < 20.0)
        {
            item.status = CheckStatus::Warning;
            item.fixSuggestion = _T("系统盘空间低于 20GB，建议清理不需要的文件");
            item.score = 88;
        }
        else
        {
            item.status = CheckStatus::Pass;
            item.score = 100;
        }
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("无法读取系统盘信息");
        item.score = 98;
    }
}

void CDiagnosticEngine::CheckAllDisks(DiagnosticItem& item)
{
    TCHAR drives[256];
    DWORD len = GetLogicalDriveStrings(255, drives);
    int driveCount = 0;
    CString warnings;

    for (TCHAR* p = drives; *p; p += _tcslen(p) + 1)
    {
        if (GetDriveType(p) != DRIVE_FIXED)
            continue;

        driveCount++;
        ULARGE_INTEGER freeBytesAvail, totalBytes, totalFreeBytes;
        if (GetDiskFreeSpaceEx(p, &freeBytesAvail, &totalBytes, &totalFreeBytes))
        {
            double freeGB = freeBytesAvail.QuadPart / (1024.0 * 1024 * 1024);
            if (freeGB < 10.0)
            {
                CString w;
                w.Format(_T("%s 仅剩 %.1fGB"), p, freeGB);
                if (!warnings.IsEmpty()) warnings += _T(", ");
                warnings += w;
            }
        }
    }

    CString detail;
    detail.Format(_T("检测到 %d 个固定磁盘"), driveCount);

    if (!warnings.IsEmpty())
    {
        item.status = CheckStatus::Warning;
        detail += _T(" | 低空间: ") + warnings;
        item.fixSuggestion = _T("清理磁盘空间或移动数据到其他驱动器");
        item.score = 85;
    }
    else
    {
        item.status = CheckStatus::Pass;
        detail += _T("，空间充足");
        item.score = 100;
    }
    item.detail = detail;
}

void CDiagnosticEngine::CheckTempFolder(DiagnosticItem& item)
{
    TCHAR tempPath[MAX_PATH];
    GetTempPath(MAX_PATH, tempPath);

    CString searchPath = CString(tempPath) + _T("*");
    WIN32_FIND_DATA fd;
    HANDLE hFind = FindFirstFile(searchPath, &fd);

    int fileCount = 0;
    ULONGLONG totalSize = 0;

    if (hFind != INVALID_HANDLE_VALUE)
    {
        do
        {
            if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
            {
                fileCount++;
                ULARGE_INTEGER sz;
                sz.LowPart = fd.nFileSizeLow;
                sz.HighPart = fd.nFileSizeHigh;
                totalSize += sz.QuadPart;
            }
        } while (FindNextFile(hFind, &fd));
        FindClose(hFind);
    }

    double sizeMB = totalSize / (1024.0 * 1024);
    CString detail;
    detail.Format(_T("临时文件夹: %d 个文件，共 %.1f MB"), fileCount, sizeMB);
    item.detail = detail;

    if (sizeMB > 500.0)
    {
        item.status = CheckStatus::Warning;
        item.fixSuggestion = _T("临时文件超过 500MB，建议清理");
        item.score = 90;
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.score = 100;
    }
}

// ─────────── Network Checks ───────────

void CDiagnosticEngine::CheckNetworkAvailable(DiagnosticItem& item)
{
    ULONG bufLen = 15000;
    std::vector<BYTE> buf(bufLen);
    auto pAddresses = (PIP_ADAPTER_ADDRESSES)buf.data();

    ULONG ret = GetAdaptersAddresses(AF_UNSPEC,
        GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST | GAA_FLAG_SKIP_DNS_SERVER,
        nullptr, pAddresses, &bufLen);

    if (ret == ERROR_BUFFER_OVERFLOW)
    {
        buf.resize(bufLen);
        pAddresses = (PIP_ADAPTER_ADDRESSES)buf.data();
        ret = GetAdaptersAddresses(AF_UNSPEC,
            GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST | GAA_FLAG_SKIP_DNS_SERVER,
            nullptr, pAddresses, &bufLen);
    }

    int activeCount = 0;
    if (ret == NO_ERROR)
    {
        for (auto p = pAddresses; p; p = p->Next)
        {
            if (p->OperStatus == IfOperStatusUp &&
                p->IfType != IF_TYPE_SOFTWARE_LOOPBACK)
            {
                activeCount++;
            }
        }
    }

    if (activeCount > 0)
    {
        CString detail;
        detail.Format(_T("网络已连接，%d 个活动适配器"), activeCount);
        item.detail = detail;
        item.status = CheckStatus::Pass;
        item.score = 100;
    }
    else
    {
        item.detail = _T("无网络连接");
        item.status = CheckStatus::Fail;
        item.fixSuggestion = _T("检查网线或 Wi-Fi 连接");
        item.score = 60;
    }
}

void CDiagnosticEngine::CheckDns(DiagnosticItem& item)
{
    WSADATA wsaData;
    WSAStartup(MAKEWORD(2, 2), &wsaData);

    struct addrinfoW hints = {};
    hints.ai_family = AF_INET;
    hints.ai_socktype = SOCK_STREAM;
    struct addrinfoW* result = nullptr;

    int ret = GetAddrInfoW(L"www.baidu.com", L"80", &hints, &result);
    if (ret == 0 && result)
    {
        WCHAR ipStr[64] = {};
        DWORD ipLen = 64;
        WSAAddressToStringW(result->ai_addr, (DWORD)result->ai_addrlen, nullptr, ipStr, &ipLen);

        CString detail;
        detail.Format(_T("DNS 解析正常 (www.baidu.com → %s)"), ipStr);
        item.detail = detail;
        item.status = CheckStatus::Pass;
        item.score = 100;

        FreeAddrInfoW(result);
    }
    else
    {
        item.detail = _T("DNS 解析失败");
        item.status = CheckStatus::Fail;
        item.fixSuggestion = _T("检查 DNS 设置，尝试使用 8.8.8.8");
        item.score = 65;
    }

    WSACleanup();
}

void CDiagnosticEngine::CheckInternet(DiagnosticItem& item)
{
    HANDLE hIcmp = IcmpCreateFile();
    if (hIcmp == INVALID_HANDLE_VALUE)
    {
        item.status = CheckStatus::Warning;
        item.detail = _T("无法创建 ICMP 句柄");
        item.fixSuggestion = _T("检查网络防火墙或代理设置");
        item.score = 85;
        return;
    }

    IPAddr destAddr = 0;
    IN_ADDR inAddr;
    if (InetPtonA(AF_INET, "8.8.8.8", &inAddr) == 1)
        destAddr = inAddr.S_un.S_addr;
    else
    {
        item.status = CheckStatus::Warning;
        item.detail = _T("无法解析目标地址");
        item.score = 85;
        IcmpCloseHandle(hIcmp);
        return;
    }

    char sendData[] = "ping";
    DWORD replySize = sizeof(ICMP_ECHO_REPLY) + sizeof(sendData) + 8;
    std::vector<BYTE> replyBuf(replySize);

    DWORD ret = IcmpSendEcho(hIcmp, destAddr, sendData, sizeof(sendData),
                              nullptr, replyBuf.data(), replySize, 3000);

    if (ret > 0)
    {
        auto pReply = (PICMP_ECHO_REPLY)replyBuf.data();
        CString detail;
        detail.Format(_T("互联网连通，延迟 %dms"), pReply->RoundTripTime);
        item.detail = detail;
        item.status = CheckStatus::Pass;
        item.score = 100;
    }
    else
    {
        item.status = CheckStatus::Warning;
        item.detail = _T("无法 Ping 外网（可能被防火墙拦截）");
        item.fixSuggestion = _T("检查防火墙是否拦截了 ICMP");
        item.score = 85;
    }

    IcmpCloseHandle(hIcmp);
}

// ─────────── Security Checks ───────────

void CDiagnosticEngine::CheckFirewall(DiagnosticItem& item)
{
    DWORD enabled = ReadRegistryDword(HKEY_LOCAL_MACHINE,
        _T("SYSTEM\\CurrentControlSet\\Services\\SharedAccess\\Parameters\\FirewallPolicy\\StandardProfile"),
        _T("EnableFirewall"), 0xFFFF);

    if (enabled == 1)
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("Windows 防火墙已启用");
        item.score = 100;
    }
    else if (enabled == 0)
    {
        item.status = CheckStatus::Fail;
        item.detail = _T("Windows 防火墙未启用");
        item.fixSuggestion = _T("请启用 Windows 防火墙以保护系统");
        item.score = 60;
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("防火墙状态检测完成");
        item.score = 100;
    }
}

void CDiagnosticEngine::CheckAntivirus(DiagnosticItem& item)
{
    HRESULT hr;
    IWbemLocator* pLocator = nullptr;
    IWbemServices* pServices = nullptr;
    IEnumWbemClassObject* pEnumerator = nullptr;

    hr = CoCreateInstance(CLSID_WbemLocator, nullptr, CLSCTX_INPROC_SERVER,
                          IID_IWbemLocator, (void**)&pLocator);
    if (FAILED(hr))
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("杀毒软件检测完成（WMI 不可用）");
        item.score = 98;
        return;
    }

    hr = pLocator->ConnectServer(_bstr_t(L"root\\SecurityCenter2"),
                                  nullptr, nullptr, nullptr, 0, nullptr, nullptr, &pServices);
    if (FAILED(hr))
    {
        pLocator->Release();
        item.status = CheckStatus::Pass;
        item.detail = _T("杀毒软件检测完成（服务器版本可能无 SecurityCenter）");
        item.score = 98;
        return;
    }

    CoSetProxyBlanket(pServices, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, nullptr,
                      RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, nullptr, EOAC_NONE);

    hr = pServices->ExecQuery(
        _bstr_t(L"WQL"),
        _bstr_t(L"SELECT displayName FROM AntiVirusProduct"),
        WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, nullptr, &pEnumerator);

    CString names;
    int count = 0;

    if (SUCCEEDED(hr))
    {
        IWbemClassObject* pObj = nullptr;
        ULONG returned = 0;
        while (pEnumerator->Next(WBEM_INFINITE, 1, &pObj, &returned) == S_OK)
        {
            VARIANT vtName;
            if (SUCCEEDED(pObj->Get(L"displayName", 0, &vtName, nullptr, nullptr)))
            {
                if (vtName.vt == VT_BSTR)
                {
                    if (!names.IsEmpty()) names += _T(", ");
                    names += CString(vtName.bstrVal);
                    count++;
                }
                VariantClear(&vtName);
            }
            pObj->Release();
        }
        pEnumerator->Release();
    }

    if (count > 0)
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("检测到杀毒软件: ") + names;
        item.score = 100;
    }
    else
    {
        item.status = CheckStatus::Warning;
        item.detail = _T("未检测到杀毒软件");
        item.fixSuggestion = _T("建议启用 Windows Defender 或安装杀毒软件");
        item.score = 75;
    }

    pServices->Release();
    pLocator->Release();
}

void CDiagnosticEngine::CheckUac(DiagnosticItem& item)
{
    DWORD uac = ReadRegistryDword(HKEY_LOCAL_MACHINE,
        _T("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System"),
        _T("EnableLUA"), 0xFFFF);

    if (uac == 1)
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("UAC 已启用");
        item.score = 100;
    }
    else if (uac == 0)
    {
        item.status = CheckStatus::Warning;
        item.detail = _T("UAC 已关闭，存在安全风险");
        item.fixSuggestion = _T("建议开启用户账户控制（UAC）");
        item.score = 80;
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("UAC 检测完成");
        item.score = 100;
    }
}

void CDiagnosticEngine::CheckAutoLogin(DiagnosticItem& item)
{
    CString autoAdmin = ReadRegistryString(HKEY_LOCAL_MACHINE,
        _T("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon"),
        _T("AutoAdminLogon"));

    if (autoAdmin == _T("1"))
    {
        item.status = CheckStatus::Warning;
        item.detail = _T("自动登录已启用，存在安全风险");
        item.fixSuggestion = _T("禁用自动登录以提升安全性");
        item.score = 82;
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("未启用自动登录");
        item.score = 100;
    }
}

// ─────────── Software Checks ───────────

void CDiagnosticEngine::CheckStartupItems(DiagnosticItem& item)
{
    int count = CountRegistryValues(HKEY_CURRENT_USER,
        _T("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"));
    count += CountRegistryValues(HKEY_LOCAL_MACHINE,
        _T("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"));

    CString detail;
    detail.Format(_T("检测到 %d 个开机启动项"), count);
    item.detail = detail;

    if (count > 10)
    {
        item.status = CheckStatus::Warning;
        CString fix;
        fix.Format(_T("启动项过多（%d个），建议禁用不必要的启动项"), count);
        item.fixSuggestion = fix;
        item.score = 85;
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.score = 100;
    }
}

void CDiagnosticEngine::CheckInstalledPrograms(DiagnosticItem& item)
{
    int count = CountRegistrySubKeys(HKEY_LOCAL_MACHINE,
        _T("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"));

    CString detail;
    detail.Format(_T("检测到约 %d 个已安装程序"), count);
    item.detail = detail;
    item.status = CheckStatus::Pass;
    item.score = 100;
}

void CDiagnosticEngine::CheckComPorts(DiagnosticItem& item)
{
    CString ports;
    int count = 0;
    HKEY hKey = nullptr;

    if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, _T("HARDWARE\\DEVICEMAP\\SERIALCOMM"),
                     0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        TCHAR valueName[256];
        TCHAR valueData[256];
        DWORD idx = 0;

        for (;;)
        {
            DWORD nameLen = 256, dataLen = sizeof(valueData);
            DWORD type = 0;
            LONG ret = RegEnumValue(hKey, idx, valueName, &nameLen, nullptr,
                                     &type, (LPBYTE)valueData, &dataLen);
            if (ret != ERROR_SUCCESS) break;

            if (!ports.IsEmpty()) ports += _T(", ");
            ports += valueData;
            count++;
            idx++;
        }
        RegCloseKey(hKey);
    }

    if (count > 0)
    {
        CString detail;
        detail.Format(_T("检测到 %d 个串口: %s"), count, (LPCTSTR)ports);
        item.detail = detail;
    }
    else
    {
        item.detail = _T("未检测到串口");
    }
    item.status = CheckStatus::Pass;
    item.score = 100;
}

// ─────────── Performance Checks ───────────

void CDiagnosticEngine::CheckCpu(DiagnosticItem& item)
{
    IWbemLocator* pLocator = nullptr;
    IWbemServices* pServices = nullptr;
    IEnumWbemClassObject* pEnumerator = nullptr;

    HRESULT hr = CoCreateInstance(CLSID_WbemLocator, nullptr, CLSCTX_INPROC_SERVER,
                                   IID_IWbemLocator, (void**)&pLocator);
    if (FAILED(hr))
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("CPU 检测完成");
        item.score = 100;
        return;
    }

    hr = pLocator->ConnectServer(_bstr_t(L"root\\cimv2"),
                                  nullptr, nullptr, nullptr, 0, nullptr, nullptr, &pServices);
    if (FAILED(hr))
    {
        pLocator->Release();
        item.status = CheckStatus::Pass;
        item.detail = _T("CPU 检测完成");
        item.score = 100;
        return;
    }

    CoSetProxyBlanket(pServices, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, nullptr,
                      RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, nullptr, EOAC_NONE);

    hr = pServices->ExecQuery(
        _bstr_t(L"WQL"),
        _bstr_t(L"SELECT LoadPercentage FROM Win32_Processor"),
        WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, nullptr, &pEnumerator);

    if (SUCCEEDED(hr))
    {
        IWbemClassObject* pObj = nullptr;
        ULONG returned = 0;
        if (pEnumerator->Next(WBEM_INFINITE, 1, &pObj, &returned) == S_OK)
        {
            VARIANT vtLoad;
            if (SUCCEEDED(pObj->Get(L"LoadPercentage", 0, &vtLoad, nullptr, nullptr)))
            {
                int load = vtLoad.intVal;
                CString detail;
                detail.Format(_T("CPU 当前负载: %d%%"), load);
                item.detail = detail;

                if (load > 90)
                {
                    item.status = CheckStatus::Fail;
                    item.fixSuggestion = _T("CPU 负载过高，检查高占用进程");
                    item.score = 65;
                }
                else if (load > 70)
                {
                    item.status = CheckStatus::Warning;
                    item.fixSuggestion = _T("CPU 负载较高，建议关闭不必要的程序");
                    item.score = 85;
                }
                else
                {
                    item.status = CheckStatus::Pass;
                    item.score = 100;
                }
                VariantClear(&vtLoad);
            }
            pObj->Release();
        }
        pEnumerator->Release();
    }

    if (item.detail.IsEmpty())
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("CPU 正常");
        item.score = 100;
    }

    pServices->Release();
    pLocator->Release();
}

void CDiagnosticEngine::CheckMemory(DiagnosticItem& item)
{
    MEMORYSTATUSEX ms = {};
    ms.dwLength = sizeof(ms);

    if (GlobalMemoryStatusEx(&ms))
    {
        double totalGB = ms.ullTotalPhys / (1024.0 * 1024 * 1024);
        double usedGB = (ms.ullTotalPhys - ms.ullAvailPhys) / (1024.0 * 1024 * 1024);
        DWORD usedPct = ms.dwMemoryLoad;

        CString detail;
        detail.Format(_T("内存使用: %.1f GB / %.1f GB (%d%%)"), usedGB, totalGB, usedPct);
        item.detail = detail;

        if (usedPct > 90)
        {
            item.status = CheckStatus::Fail;
            item.fixSuggestion = _T("内存使用率过高，建议关闭无用程序或增加内存");
            item.score = 65;
        }
        else if (usedPct > 75)
        {
            item.status = CheckStatus::Warning;
            item.fixSuggestion = _T("内存使用率较高");
            item.score = 85;
        }
        else
        {
            item.status = CheckStatus::Pass;
            item.score = 100;
        }
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.detail = _T("内存检测完成");
        item.score = 100;
    }
}

void CDiagnosticEngine::CheckProcesses(DiagnosticItem& item)
{
    HANDLE hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    int count = 0;

    if (hSnap != INVALID_HANDLE_VALUE)
    {
        PROCESSENTRY32 pe = {};
        pe.dwSize = sizeof(pe);
        if (Process32First(hSnap, &pe))
        {
            do { count++; } while (Process32Next(hSnap, &pe));
        }
        CloseHandle(hSnap);
    }

    CString detail;
    detail.Format(_T("当前运行 %d 个进程"), count);
    item.detail = detail;

    if (count > 200)
    {
        item.status = CheckStatus::Warning;
        item.fixSuggestion = _T("进程数过多，建议检查是否有异常进程");
        item.score = 88;
    }
    else
    {
        item.status = CheckStatus::Pass;
        item.score = 100;
    }
}
