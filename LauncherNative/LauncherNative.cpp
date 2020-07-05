/*
	Copyright (C) 2016-2020 Hajin Jang
	Licensed under MIT License.

	MIT License

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

// Constants
#include "Var.h"

// Windows SDK Headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <shlwapi.h>
#include <shellapi.h>

// C Runtime Headers
#include <stdio.h>
#include <stdlib.h>
#include <stdbool.h>
#include <stdint.h>

// Resource Headers
#include "resource.h"

// Local Headers
#include "Helper.h"
#include "Version.h"
#include "NetDetector.h"

// These buffers are too large to go in local stack
WCHAR AbsPath[MAX_PATH_LONG] = { 0 };
WCHAR PEBakeryPath[MAX_PATH_LONG] = { 0 };

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
	_In_opt_ HINSTANCE hPrevInstance,
	_In_ LPWSTR    lpCmdLine,
	_In_ int       nCmdShow)
{
	UNREFERENCED_PARAMETER(hPrevInstance);
	UNREFERENCED_PARAMETER(lpCmdLine);
	int hRes = 0;

	// Get absolute path of PEBakery.exe in absPath
	const DWORD absPathLen = GetModuleFileNameW(NULL, AbsPath, MAX_PATH_LONG);
	if (absPathLen == 0)
		Helper::PrintError(ERR_MSG_UNABLE_TO_GET_ABSPATH);

	AbsPath[MAX_PATH_LONG - 1] = '\0'; // NULL guard for Windows XP
	const PWSTR posDir = StrRChrW(AbsPath, NULL, L'\\');
	if (posDir == NULL)
		exit(1);
	posDir[0] = '\0';
	StringCchCopyW(PEBakeryPath, MAX_PATH_LONG, AbsPath);
	StringCchCatW(PEBakeryPath, MAX_PATH_LONG, L"\\Binary\\PEBakery.exe");

#ifdef CHECK_NETFX
	// Check if required version of .NET Framework is installed
	NetFxDetector fxDetector = NetFxDetector();
	if (!fxDetector.IsInstalled())
		fxDetector.ExitAndDownload();
#endif

#ifdef CHECK_NETCORE
	// Check if required version of .NET Core is installed
	Version coreVer = Version(NETCORE_VER_MAJOR, NETCORE_VER_MINOR, NETCORE_VER_PATCH);
	NetCoreDetector coreDetector = NetCoreDetector(coreVer);
	if (!coreDetector.IsInstalled())
		coreDetector.ExitAndDownload();
#endif

	// Check if PEBakery.exe exists
	if (!PathFileExistsW(PEBakeryPath))
		Helper::PrintError(ERR_MSG_UNABLE_TO_FIND_BINARY);

	// According to MSDN, ShellExecute's return value can be casted only to int.
	// In mingw, size_t casting should be used to evade [-Wpointer-to-int-cast] warning.
	WCHAR* params = Helper::GetParameters(GetCommandLineW());
	hRes = (int)(size_t)ShellExecuteW(NULL, NULL, PEBakeryPath, params, AbsPath, SW_SHOWNORMAL);
	if (hRes <= 32)
		Helper::PrintError(ERR_MSG_UNABLE_TO_LAUNCH_BINARY);

	return 0;
}
