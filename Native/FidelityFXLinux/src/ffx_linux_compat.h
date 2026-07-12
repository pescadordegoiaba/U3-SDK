#pragma once

#include <cstdio>
#include <cwchar>
#include <locale>

#if !defined(_countof)
#define _countof(value) (sizeof(value) / sizeof((value)[0]))
#endif

#if !defined(_MSC_VER)
#define sprintf_s std::snprintf

template <size_t Size>
inline int wcscpy_s(wchar_t (&destination)[Size], const wchar_t* source)
{
	if (source == nullptr || Size == 0)
		return 1;
	std::wcsncpy(destination, source, Size - 1);
	destination[Size - 1] = L'\0';
	return 0;
}
#endif
