#include <windows.h>
#include <string>

// ============================================
// Safe native shim — delegates to managed DLL
// ============================================
// The managed DLL (SwComAddin) should be registered via RegAsm
// as the primary COM server. This native shim is only loaded
// if the managed DLL is NOT properly registered.
// It safely accepts ConnectToSW without crashing.

static const CLSID CLSID_SwAddin =
    {0xB3E7D8A1, 0x4F2C, 0x4A91, {0xB5, 0xD6, 0xE8, 0xF0, 0xA1, 0xC2, 0xD3, 0xE4}};

static const IID IID_SwAddinSolidWorks =
    {0xDA306A0D, 0xEAC5, 0x4406, {0x86, 0x10, 0xB1, 0xDA, 0x80, 0x5D, 0x92, 0x70}};

static const wchar_t* ADDIN_TITLE = L"SolidWorks AI Plugin";
static const wchar_t* LOG_FILE = L"SwAddin.log";

HINSTANCE g_hInst = nullptr;
static LONG g_cRef = 0;

static void Log(const wchar_t* msg);
static std::wstring GetDesktopPath();

// ISwAddin inherits from IUnknown only (NOT IDispatch)
// Vtable: QI(0), AddRef(1), Release(2), ConnectToSW(3), DisconnectFromSW(4)
class ISwAddin : public IUnknown {
public:
    virtual HRESULT STDMETHODCALLTYPE ConnectToSW(
        IDispatch* ThisSW, LONG Cookie, VARIANT_BOOL* retval) = 0;
    virtual HRESULT STDMETHODCALLTYPE DisconnectFromSW(
        VARIANT_BOOL* retval) = 0;
};

class CSwAddin : public ISwAddin {
private:
    LONG m_cRef;

public:
    CSwAddin() : m_cRef(1) {
        InterlockedIncrement(&g_cRef);
        Log(L"CSwAddin constructed (native shim)");
    }
    ~CSwAddin() {
        InterlockedDecrement(&g_cRef);
        Log(L"CSwAddin destroyed");
    }

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) {
        if (riid == IID_IUnknown || riid == IID_SwAddinSolidWorks) {
            *ppv = static_cast<ISwAddin*>(this);
            AddRef();
            Log(L"QI matched ISwAddin");
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    STDMETHODIMP_(ULONG) AddRef() { return InterlockedIncrement(&m_cRef); }
    STDMETHODIMP_(ULONG) Release() {
        LONG c = InterlockedDecrement(&m_cRef);
        if (c == 0) delete this;
        return c;
    }

    STDMETHODIMP ConnectToSW(IDispatch* ThisSW, LONG Cookie, VARIANT_BOOL* retval) {
        Log(L"*** ConnectToSW called (native shim) ***");
        Log(L"WARNING: Native shim loaded instead of managed DLL.");
        Log(L"Please register SwComAddin.dll with RegAsm.");
        if (retval) *retval = VARIANT_TRUE;
        return S_OK;
    }

    STDMETHODIMP DisconnectFromSW(VARIANT_BOOL* retval) {
        Log(L"DisconnectFromSW called (native shim)");
        if (retval) *retval = VARIANT_TRUE;
        return S_OK;
    }
};

// ============================================
// Class Factory
// ============================================

class CSwAddinFactory : public IClassFactory {
private:
    LONG m_cRef;
public:
    CSwAddinFactory() : m_cRef(1) {}

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) {
        if (riid == IID_IUnknown || riid == IID_IClassFactory) {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    STDMETHODIMP_(ULONG) AddRef() { return InterlockedIncrement(&m_cRef); }
    STDMETHODIMP_(ULONG) Release() {
        LONG c = InterlockedDecrement(&m_cRef);
        if (c == 0) delete this;
        return c;
    }

    STDMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv) {
        if (pUnkOuter) return CLASS_E_NOAGGREGATION;
        CSwAddin* p = new (std::nothrow) CSwAddin();
        if (!p) return E_OUTOFMEMORY;
        HRESULT hr = p->QueryInterface(riid, ppv);
        p->Release();
        return hr;
    }
    STDMETHODIMP LockServer(BOOL) { return S_OK; }
};

// ============================================
// COM DLL Exports
// ============================================

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv) {
    if (rclsid == CLSID_SwAddin) {
        CSwAddinFactory* f = new (std::nothrow) CSwAddinFactory();
        if (!f) return E_OUTOFMEMORY;
        HRESULT hr = f->QueryInterface(riid, ppv);
        f->Release();
        return hr;
    }
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow() { return (g_cRef == 0) ? S_OK : S_FALSE; }

// ============================================
// Registration
// ============================================

static std::wstring GuidToString(REFGUID guid) {
    wchar_t buf[64];
    swprintf_s(buf, 64, L"{%08lX-%04X-%04X-%02X%02X-%02X%02X%02X%02X%02X%02X}",
        guid.Data1, guid.Data2, guid.Data3,
        guid.Data4[0], guid.Data4[1], guid.Data4[2],
        guid.Data4[3], guid.Data4[4], guid.Data4[5],
        guid.Data4[6], guid.Data4[7]);
    return std::wstring(buf);
}

STDAPI DllRegisterServer() {
    HKEY hKey;
    DWORD val1 = 1;
    std::wstring guidStr = GuidToString(CLSID_SwAddin);
    wchar_t dllPath[MAX_PATH];
    GetModuleFileNameW(g_hInst, dllPath, MAX_PATH);

    std::wstring clsidPath = L"CLSID\\" + guidStr;
    RegCreateKeyExW(HKEY_CLASSES_ROOT, clsidPath.c_str(), 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr);
    RegSetValueExW(hKey, nullptr, 0, REG_SZ, (const BYTE*)ADDIN_TITLE,
        (DWORD)((wcslen(ADDIN_TITLE) + 1) * sizeof(wchar_t)));
    RegCloseKey(hKey);

    std::wstring inprocPath = clsidPath + L"\\InprocServer32";
    RegCreateKeyExW(HKEY_CLASSES_ROOT, inprocPath.c_str(), 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr);
    RegSetValueExW(hKey, nullptr, 0, REG_SZ, (const BYTE*)dllPath,
        (DWORD)((wcslen(dllPath) + 1) * sizeof(wchar_t)));
    const wchar_t* tm = L"Apartment";
    RegSetValueExW(hKey, L"ThreadingModel", 0, REG_SZ, (const BYTE*)tm,
        (DWORD)((wcslen(tm) + 1) * sizeof(wchar_t)));
    RegCloseKey(hKey);

    std::wstring swAddinPath = L"SOFTWARE\\SolidWorks\\Addins\\" + guidStr;
    RegCreateKeyExW(HKEY_LOCAL_MACHINE, swAddinPath.c_str(), 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr);
    RegSetValueExW(hKey, nullptr, 0, REG_DWORD, (BYTE*)&val1, sizeof(val1));
    RegSetValueExW(hKey, L"Title", 0, REG_SZ, (const BYTE*)ADDIN_TITLE,
        (DWORD)((wcslen(ADDIN_TITLE) + 1) * sizeof(wchar_t)));
    RegSetValueExW(hKey, L"dllPath", 0, REG_SZ, (const BYTE*)dllPath,
        (DWORD)((wcslen(dllPath) + 1) * sizeof(wchar_t)));
    RegCloseKey(hKey);

    std::wstring startupPath = L"SOFTWARE\\SolidWorks\\AddInsStartup\\" + guidStr;
    RegCreateKeyExW(HKEY_CURRENT_USER, startupPath.c_str(), 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hKey, nullptr);
    RegSetValueExW(hKey, nullptr, 0, REG_DWORD, (BYTE*)&val1, sizeof(val1));
    RegCloseKey(hKey);

    Log(L"DllRegisterServer SUCCESS");
    return S_OK;
}

STDAPI DllUnregisterServer() {
    std::wstring guidStr = GuidToString(CLSID_SwAddin);
    RegDeleteTreeW(HKEY_CLASSES_ROOT, (L"CLSID\\" + guidStr).c_str());
    RegDeleteTreeW(HKEY_LOCAL_MACHINE, (L"SOFTWARE\\SolidWorks\\Addins\\" + guidStr).c_str());
    RegDeleteTreeW(HKEY_CURRENT_USER, (L"SOFTWARE\\SolidWorks\\AddInsStartup\\" + guidStr).c_str());
    return S_OK;
}

// ============================================
// DLL Main + Helpers
// ============================================

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID) {
    g_hInst = (HINSTANCE)hModule;
    return TRUE;
}

static void Log(const wchar_t* msg) {
    std::wstring path = GetDesktopPath() + L"\\" + LOG_FILE;
    HANDLE hFile = CreateFileW(path.c_str(), FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hFile != INVALID_HANDLE_VALUE) {
        SYSTEMTIME st; GetLocalTime(&st);
        wchar_t line[1024];
        swprintf_s(line, 1024, L"[%02d:%02d:%02d] %s\r\n", st.wHour, st.wMinute, st.wSecond, msg);
        DWORD written;
        SetFilePointer(hFile, 0, nullptr, FILE_END);
        WriteFile(hFile, line, (DWORD)(wcslen(line) * sizeof(wchar_t)), &written, nullptr);
        CloseHandle(hFile);
    }
}

static std::wstring GetDesktopPath() {
    wchar_t path[MAX_PATH];
    SHGetFolderPathW(nullptr, CSIDL_DESKTOP, nullptr, 0, path);
    return std::wstring(path);
}
