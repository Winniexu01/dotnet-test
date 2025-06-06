// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef _WIN_PATH_APIS_WRAPPER_
#define _WIN_PATH_APIS_WRAPPER_
class SString;

#ifdef HOST_WINDOWS

HMODULE
LoadLibraryExWrapper(
    _In_ LPCWSTR lpLibFileName,
    _Reserved_ HANDLE hFile = NULL,
    _In_ DWORD dwFlags = 0
    );

HANDLE
CreateFileWrapper(
    _In_ LPCWSTR lpFileName,
    _In_ DWORD dwDesiredAccess,
    _In_ DWORD dwShareMode,
    _In_opt_ LPSECURITY_ATTRIBUTES lpSecurityAttributes,
    _In_ DWORD dwCreationDisposition,
    _In_ DWORD dwFlagsAndAttributes,
    _In_opt_ HANDLE hTemplateFile
    );

BOOL
CopyFileExWrapper(
    _In_        LPCWSTR lpExistingFileName,
    _In_        LPCWSTR lpNewFileName,
    _In_opt_    LPPROGRESS_ROUTINE lpProgressRoutine,
    _In_opt_    LPVOID lpData,
    _When_(pbCancel != NULL, _Pre_satisfies_(*pbCancel == FALSE))
    _Inout_opt_ LPBOOL pbCancel,
    _In_        DWORD dwCopyFlags
    );
#endif //HOST_WINDOWS

DWORD
SearchPathWrapper(
    _In_opt_ LPCWSTR lpPath,
    _In_ LPCWSTR lpFileName,
    _In_opt_ LPCWSTR lpExtension,
    _In_ BOOL getPath,
    SString& lpBuffer,
    _Out_opt_ LPWSTR * lpFilePart
    );

DWORD
GetModuleFileNameWrapper(
    _In_opt_ HMODULE hModule,
    SString& buffer
    );

DWORD WINAPI GetEnvironmentVariableWrapper(
    _In_opt_  LPCTSTR lpName,
    _Out_opt_ SString&  lpBuffer
    );

#endif //_WIN_PATH_APIS_WRAPPER_

