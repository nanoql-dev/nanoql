<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->
# C++

nanoql's C++ layer, `nanoql.hpp`, is C++17 over the C interface: objects that release their handles, refusals thrown
as exceptions, nanoql's type names and QuantLib's member names (`curve.discount(5.0)`). It ships in each platform's C
archive, for Windows x64 and Linux x64, with a CMake package. [The bindings guide](bindings.md)'s "C and C++" section
gives its rules: names, objects, values, defaults and overloads, errors.

## Install

Unpack the archive for your platform from [the release](https://github.com/nanoql-dev/nanoql/releases/latest),
`nanoql-<version>-win-x64-c-api.zip` or `nanoql-<version>-linux-x64-c-api.tar.xz`. It holds:

- `include/`: `nanoql.h`, the C interface, and `nanoql.hpp`, the C++ layer over it;
- `lib/`: the library, `nanoql.dll` with its import library `nanoql.lib` on Windows or `libnanoql.so` on Linux, and
  `cmake/nanoql/`, the CMake package;
- the notices: `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md` and the .NET runtime's two, which the library carries.

## CMake

Give CMake the archive's folder and link `nanoql::cpp`:

```cmake
cmake_minimum_required(VERSION 3.21)
project(app CXX)
find_package(nanoql 2.1 CONFIG REQUIRED)
add_executable(app main.cpp)
target_link_libraries(app PRIVATE nanoql::cpp)
```

```shell
cmake -S . -B build -DCMAKE_PREFIX_PATH=<the archive's folder>
cmake --build build --config Release
```

`nanoql::cpp` brings C++17 and `nanoql::c`, the library and `include/`; a C program links `nanoql::c` alone. A
request for a version takes this release or an earlier one of its major version, as CMake's SameMajorVersion rule
has it, version ranges included.

- **Windows:** a program finds `nanoql.dll` beside it. A copy after the build puts it there:

  ```cmake
  add_custom_command(TARGET app POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different $<TARGET_RUNTIME_DLLS:app> $<TARGET_FILE_DIR:app>
    COMMAND_EXPAND_LISTS)
  ```

- **Linux:** a program run in its build tree finds `libnanoql.so` by the run path CMake gives it; one installed or
  copied elsewhere finds it by `LD_LIBRARY_PATH` or its own run path (`$ORIGIN`).

`bindings/cpp/consumer/` in the repository is this project, with the program below; CI builds it through each
platform's archive with MSVC on Windows, and g++ and clang on Linux.

## A first program

```cpp
#include <cstdio>
#include <exception>

#include "nanoql.hpp"

using namespace nanoql;

int main()
{
    FlatForward curve(Date(15, Month::March, 2023), 0.03, ConventionSet::quantLib().dayCounter("Actual365Fixed"));
    std::printf("discount(1) = %.6f\n", curve.discount(1.0));  // 0.970446
    try {
        curve.discount(-1.0);
    } catch (const std::exception& e) {
        std::printf("refused: %s\n", e.what());  // refused: negative time (-1) given
    }
}
```

Each object holds a handle to its nanoql object and releases it when it goes out of scope; `liveHandles()` counts the
handles still held. A refusal is `nanoql::error`, a `std::runtime_error` with the C interface's code, the parameter it
names and its type, and under it `argument_error` and `handle_error`.

## Compile time

`nanoql.hpp` declares the whole library, about 98,600 lines. A translation unit that includes it takes about 5 s and
600 MB with g++ at `-O2`, so include it in few files, or precompile it:

```cmake
target_precompile_headers(app PRIVATE <nanoql.hpp>)
```

## Versions

A release's header works with that release's library. A program that loads a library installed apart from its header
checks `nql_abi_version() == NQL_ABI_VERSION` first. A minor release adds to the stable API and removes only what was
deprecated two releases and a year before, which the header marks `[[deprecated]]` meanwhile; the guide's
[Versions](bindings.md#versions) gives the rule. A new overload can make a call that compiled ambiguous, as in any C++
library; naming the argument's type settles it.

## The experimental API

Define `NANOQL_EXPERIMENTAL` before including the header, or with `target_compile_definitions(app PRIVATE
NANOQL_EXPERIMENTAL)`, to declare the experimental API beside the stable one. It may change in any release.

## A library of your own

[`nanoql-bind`](https://www.nuget.org/packages/NanoQL.Bind) builds a library holding nanoql's functions and an assembly
of yours, and writes its headers beside nanoql's: `acme.h` and `acme.hpp`, whose classes extend nanoql's
(`acme::ExponentialCurve` is a `nanoql::YieldTermStructure`). Its README gives the commands.

## Without the C++ layer

C++ can include `nanoql.h` as it is, its declarations in `extern "C"`, and call the C interface directly: the guide's
"The C ABI" gives its handles, status codes and values.
