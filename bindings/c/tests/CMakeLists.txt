# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
#
# The C conformance suite on the test build: nanoql-bind build --rid <rid> --out <folder> --test-exports, then
#   cmake -S bindings/c/tests -B <build> -DNANOQL_DIR=<folder>
#   cmake --build <build> --config Release
#   ctest --test-dir <build> -C Release
cmake_minimum_required(VERSION 3.20)
project(nanoql_c_conformance C)

set(NANOQL_DIR "" CACHE PATH "The test build's folder, with include/ and lib/")
if(NOT NANOQL_DIR)
  message(FATAL_ERROR "NANOQL_DIR names the test build's folder (nanoql-bind build ... --test-exports)")
endif()

add_executable(conformance conformance.c)
target_include_directories(conformance PRIVATE "${NANOQL_DIR}/include")
target_compile_definitions(conformance PRIVATE NANOQL_EXPERIMENTAL "NQLT_HEADER=\"${NANOQL_DIR}/include/nqlt.h\"")
set_property(TARGET conformance PROPERTY C_STANDARD 11)
if(MSVC)
  target_compile_options(conformance PRIVATE /W4 /utf-8)
  target_link_libraries(conformance PRIVATE "${NANOQL_DIR}/lib/nanoql.lib")
  # The library and its symbols beside the suite, so a debugger stepping in from it finds nanoql's source lines.
  add_custom_command(TARGET conformance POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${NANOQL_DIR}/lib/nanoql.dll" "${NANOQL_DIR}/lib/nanoql.pdb" $<TARGET_FILE_DIR:conformance>)
else()
  target_compile_options(conformance PRIVATE -Wall -Wextra)
  find_library(NANOQL_LIBRARY nanoql PATHS "${NANOQL_DIR}/lib" NO_DEFAULT_PATH REQUIRED)
  find_package(Threads REQUIRED)
  target_link_libraries(conformance PRIVATE "${NANOQL_LIBRARY}" Threads::Threads ${CMAKE_DL_LIBS} m)
  set_target_properties(conformance PROPERTIES BUILD_RPATH "${NANOQL_DIR}/lib")
endif()

enable_testing()
foreach(section kinds arrays codes handles generators misc conventions defaults engines calibrations)
  add_test(NAME ${section} COMMAND conformance ${section} WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR})
endforeach()
