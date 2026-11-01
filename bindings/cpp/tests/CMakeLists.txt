# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
#
# The C++ conformance suite on the test build, C++17, with NANOQL_EXPERIMENTAL, and stable.cpp without it: nanoql-bind
# build --rid <rid> --out <folder> --test-exports, then
#   cmake -S bindings/cpp/tests -B <build> -DNANOQL_DIR=<folder>
#   cmake --build <build> --config Release
#   ctest --test-dir <build> -C Release
cmake_minimum_required(VERSION 3.20)
project(nanoql_cpp_conformance CXX)

set(NANOQL_DIR "" CACHE PATH "The test build's folder, with include/ and lib/")
if(NOT NANOQL_DIR)
  message(FATAL_ERROR "NANOQL_DIR names the test build's folder (nanoql-bind build ... --test-exports)")
endif()

if(NOT MSVC)
  find_library(NANOQL_LIBRARY nanoql PATHS "${NANOQL_DIR}/lib" NO_DEFAULT_PATH REQUIRED)
  find_package(Threads REQUIRED)
endif()

function(nanoql_test target)
  add_executable(${target} ${target}.cpp)
  target_include_directories(${target} PRIVATE "${NANOQL_DIR}/include")
  set_target_properties(${target} PROPERTIES CXX_STANDARD 17 CXX_STANDARD_REQUIRED ON CXX_EXTENSIONS OFF)
  if(MSVC)
    target_compile_options(${target} PRIVATE /W4 /permissive- /utf-8 /bigobj /Zc:__cplusplus)
    target_link_libraries(${target} PRIVATE "${NANOQL_DIR}/lib/nanoql.lib")
    add_custom_command(TARGET ${target} POST_BUILD
      COMMAND ${CMAKE_COMMAND} -E copy_if_different "${NANOQL_DIR}/lib/nanoql.dll" $<TARGET_FILE_DIR:${target}>)
  else()
    target_compile_options(${target} PRIVATE -Wall -Wextra)
    target_link_libraries(${target} PRIVATE "${NANOQL_LIBRARY}" Threads::Threads ${CMAKE_DL_LIBS})
    set_target_properties(${target} PROPERTIES BUILD_RPATH "${NANOQL_DIR}/lib")
  endif()
endfunction()

nanoql_test(conformance)
target_compile_definitions(conformance PRIVATE NANOQL_EXPERIMENTAL)
nanoql_test(stable)

enable_testing()
foreach(section kinds arrays codes handles generators overloads defaults misc conventions engines calibrations experimental)
  add_test(NAME ${section} COMMAND conformance ${section} WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR})
endforeach()
add_test(NAME stable COMMAND stable WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR})
