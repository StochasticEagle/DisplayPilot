cmake_minimum_required(VERSION 3.21)
project(DisplayPilot LANGUAGES CXX RC)
if(NOT WIN32 OR NOT MSVC)
  message(FATAL_ERROR "DisplayPilot requires MSVC and the Windows SDK.")
endif()
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
if(CMAKE_SIZEOF_VOID_P EQUAL 4)
  set(APP_ARCH "32")
else()
  set(APP_ARCH "64")
endif()
set(APP_ICON "${CMAKE_CURRENT_SOURCE_DIR}/assets/displaypilot-primary.ico")
configure_file(src/native/app.rc.in "${CMAKE_CURRENT_BINARY_DIR}/app.rc" @ONLY)
configure_file(src/native/app.manifest "${CMAKE_CURRENT_BINARY_DIR}/app.manifest" COPYONLY)
add_executable(DisplayPilot WIN32
  src/native/main.cpp
  src/native/flyout.cpp
  src/native/display.cpp
  "${CMAKE_CURRENT_BINARY_DIR}/app.rc"
)
target_include_directories(DisplayPilot PRIVATE src/native)
target_compile_definitions(DisplayPilot PRIVATE
  UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX
  _WIN32_WINNT=0x0601 WINVER=0x0601 NTDDI_VERSION=0x06010000)
target_compile_options(DisplayPilot PRIVATE /W4 /permissive- /EHsc /utf-8
  $<$<CONFIG:Release>:/O1> $<$<CONFIG:Release>:/Gy> $<$<CONFIG:Release>:/GL>)
target_link_options(DisplayPilot PRIVATE /SUBSYSTEM:WINDOWS,6.01 /MANIFEST:EMBED
  $<$<CONFIG:Release>:/LTCG> $<$<CONFIG:Release>:/OPT:REF>
  $<$<CONFIG:Release>:/OPT:ICF>)
target_link_libraries(DisplayPilot PRIVATE user32 gdi32 shell32 ole32 advapi32 dxva2 d2d1 dwrite)
set_target_properties(DisplayPilot PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/dist/${APP_ARCH}"
  RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_CURRENT_SOURCE_DIR}/dist/${APP_ARCH}"
  RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_CURRENT_SOURCE_DIR}/dist/${APP_ARCH}"
  PDB_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/symbols"
  PDB_OUTPUT_DIRECTORY_DEBUG "${CMAKE_CURRENT_BINARY_DIR}/symbols"
  PDB_OUTPUT_DIRECTORY_RELEASE "${CMAKE_CURRENT_BINARY_DIR}/symbols")
