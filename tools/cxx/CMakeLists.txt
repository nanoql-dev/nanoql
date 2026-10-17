# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
#
# The C++ harness (docs/porting-playbook.md §7.1): QuantLib built from its pinned sources, called through JSON-RPC
# 2.0 over standard input and output. tools/cxx/build.py configures and builds it against the QuantLib it installs.
cmake_minimum_required(VERSION 3.25)
project(nanoql_cxx_harness LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
# QuantLib's library is built with the static runtime, as the wheel's is.
set(CMAKE_MSVC_RUNTIME_LIBRARY MultiThreaded)

find_package(QuantLib 1.44 EXACT CONFIG REQUIRED)

# The stale guard: a SHA-256 over this file and every file under src/, each as "<relative path>:<its SHA-256>" in
# sorted order, which tools/vectors/cxx.py recomputes from the working tree. CMake reruns this script, and so
# recomputes the digest, whenever one of them changes.
file(GLOB_RECURSE HARNESS_FILES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/src/*")
set(HARNESS_DIGESTED "CMakeLists.txt")
foreach(f IN LISTS HARNESS_FILES)
  file(RELATIVE_PATH rel "${CMAKE_CURRENT_SOURCE_DIR}" "${f}")
  list(APPEND HARNESS_DIGESTED "${rel}")
endforeach()
list(SORT HARNESS_DIGESTED)
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/CMakeLists.txt"
             ${HARNESS_FILES})
set(HARNESS_DIGEST_INPUT "")
foreach(rel IN LISTS HARNESS_DIGESTED)
  file(SHA256 "${CMAKE_CURRENT_SOURCE_DIR}/${rel}" h)
  string(APPEND HARNESS_DIGEST_INPUT "${rel}:${h}\n")
endforeach()
string(SHA256 HARNESS_SOURCE_DIGEST "${HARNESS_DIGEST_INPUT}")
# Only this generated file carries the digest, so a source change recompiles the changed file and this one.
file(CONFIGURE OUTPUT "${CMAKE_CURRENT_BINARY_DIR}/digest.cpp"
     CONTENT "const char* harnessSourceDigest() { return \"${HARNESS_SOURCE_DIGEST}\"; }\n")

list(FILTER HARNESS_FILES INCLUDE REGEX "\\.cpp$")
add_executable(cxx-harness ${HARNESS_FILES} "${CMAKE_CURRENT_BINARY_DIR}/digest.cpp")
target_include_directories(cxx-harness PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src")
target_link_libraries(cxx-harness PRIVATE QuantLib::QuantLib)
# Boost.JSON is compiled header-only into json.cpp; nothing auto-links.
target_compile_definitions(cxx-harness PRIVATE BOOST_ALL_NO_LIB NOMINMAX)
# The harness's own code builds warning-free; QuantLib's and Boost's headers are external.
target_compile_options(cxx-harness PRIVATE /utf-8 /bigobj /W4 /WX /external:W0)
