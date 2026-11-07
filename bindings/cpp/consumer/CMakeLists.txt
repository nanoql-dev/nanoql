# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
#
# A user's first project over the C archive's CMake package (docs/cpp.md), the unpacked archive's folder given by
# CMAKE_PREFIX_PATH:
#   cmake -S bindings/cpp/consumer -B <build> -DCMAKE_PREFIX_PATH=<the archive's folder>
#   cmake --build <build> --config Release
#   ctest --test-dir <build> -C Release
# CI builds it through each platform's archive, as a user unpacks it.
cmake_minimum_required(VERSION 3.21)
project(nanoql_consumer CXX)

find_package(nanoql 2.2 CONFIG REQUIRED)
message(STATUS "nanoql ${nanoql_VERSION} from ${nanoql_DIR}")

add_executable(consumer main.cpp)
target_link_libraries(consumer PRIVATE nanoql::cpp)
if(WIN32)
  # Windows looks for the DLL beside the program first.
  add_custom_command(TARGET consumer POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different $<TARGET_RUNTIME_DLLS:consumer> $<TARGET_FILE_DIR:consumer>
    COMMAND_EXPAND_LISTS)
endif()

enable_testing()
add_test(NAME consumer COMMAND consumer)
