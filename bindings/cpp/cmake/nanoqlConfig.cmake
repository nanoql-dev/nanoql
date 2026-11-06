# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
#
# nanoql's CMake package, in the C archive's lib/cmake/nanoql/: find_package(nanoql CONFIG) gives nanoql::c, the
# library and nanoql.h, and nanoql::cpp, the C++17 layer over it, nanoql.hpp (docs/bindings.md, "C and C++").
get_filename_component(_nanoql_root "${CMAKE_CURRENT_LIST_DIR}/../../.." ABSOLUTE)
if(NOT TARGET nanoql::c)
  add_library(nanoql::c SHARED IMPORTED)
  set_target_properties(nanoql::c PROPERTIES INTERFACE_INCLUDE_DIRECTORIES "${_nanoql_root}/include")
  if(WIN32)
    set_target_properties(nanoql::c PROPERTIES
      IMPORTED_LOCATION "${_nanoql_root}/lib/nanoql.dll"
      IMPORTED_IMPLIB "${_nanoql_root}/lib/nanoql.lib")
  elseif(APPLE)
    set_target_properties(nanoql::c PROPERTIES IMPORTED_LOCATION "${_nanoql_root}/lib/libnanoql.dylib")
  else()
    set_target_properties(nanoql::c PROPERTIES
      IMPORTED_LOCATION "${_nanoql_root}/lib/libnanoql.so"
      IMPORTED_SONAME "libnanoql.so")
  endif()
  add_library(nanoql::cpp INTERFACE IMPORTED)
  set_target_properties(nanoql::cpp PROPERTIES
    INTERFACE_LINK_LIBRARIES nanoql::c
    INTERFACE_COMPILE_FEATURES cxx_std_17)
endif()
unset(_nanoql_root)
