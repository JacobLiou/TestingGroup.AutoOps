#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN

#include <afxwin.h>
#include <afxext.h>
#include <afxcmn.h>
#include <afxcontrolbars.h>

#include <gdiplus.h>
#pragma comment(lib, "gdiplus.lib")

#include <wbemidl.h>
#pragma comment(lib, "wbemuuid.lib")

#include <iphlpapi.h>
#pragma comment(lib, "iphlpapi.lib")

#include <icmpapi.h>

#include <windns.h>
#pragma comment(lib, "dnsapi.lib")

#include <setupapi.h>
#pragma comment(lib, "setupapi.lib")

#include <tlhelp32.h>

#include <comdef.h>

#include <vector>
#include <string>
#include <atomic>
#include <functional>
#include <algorithm>
#include <cmath>

#ifdef _UNICODE
#if defined _M_IX86
#pragma comment(linker,"/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='x86' publicKeyToken='6595b64144ccf1df' language='*'\"")
#elif defined _M_X64
#pragma comment(linker,"/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='amd64' publicKeyToken='6595b64144ccf1df' language='*'\"")
#endif
#endif
