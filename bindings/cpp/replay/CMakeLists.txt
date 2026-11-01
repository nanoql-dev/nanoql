# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
#
# The C++ replayer: replay.cpp with the dispatch nanoql-bind generates from the surface, through nanoql.hpp, C++17.
#   nanoql-bind replay-cpp --out <dispatch folder>
#   cmake -S bindings/cpp/replay -B <build> -DNANOQL_DIR=<the library's folder> -DNANOQL_DISPATCH=<dispatch folder>
#   cmake --build <build> --config Release
#   <build>/replay <trace.jsonl> [<facts report.tsv>]
cmake_minimum_required(VERSION 3.20)
project(nanoql_cpp_replay CXX)

set(NANOQL_DIR "" CACHE PATH "The library's folder, with include/ and lib/ (nanoql-bind build)")
set(NANOQL_DISPATCH "" CACHE PATH "The folder holding nanoql-bind's generated dispatch")
if(NOT NANOQL_DIR OR NOT NANOQL_DISPATCH)
  message(FATAL_ERROR "NANOQL_DIR names the library's folder and NANOQL_DISPATCH the generated dispatch's")
endif()

file(GLOB dispatch "${NANOQL_DISPATCH}/dispatch_*.cpp")
add_executable(replay replay.cpp ${dispatch})
target_include_directories(replay PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}" "${NANOQL_DIR}/include")
target_compile_definitions(replay PRIVATE NANOQL_EXPERIMENTAL)
set_target_properties(replay PROPERTIES CXX_STANDARD 17 CXX_STANDARD_REQUIRED ON CXX_EXTENSIONS OFF)
if(MSVC)
  target_compile_options(replay PRIVATE /W3 /permissive- /utf-8 /bigobj /MP /Zc:__cplusplus)
  target_compile_definitions(replay PRIVATE _CRT_SECURE_NO_WARNINGS)
  target_link_libraries(replay PRIVATE "${NANOQL_DIR}/lib/nanoql.lib")
  add_custom_command(TARGET replay POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${NANOQL_DIR}/lib/nanoql.dll" $<TARGET_FILE_DIR:replay>)
else()
  find_library(NANOQL_LIBRARY nanoql PATHS "${NANOQL_DIR}/lib" NO_DEFAULT_PATH REQUIRED)
  target_link_libraries(replay PRIVATE "${NANOQL_LIBRARY}")
  set_target_properties(replay PROPERTIES BUILD_RPATH "${NANOQL_DIR}/lib")
endif()
