cmake_minimum_required(VERSION 3.21)

project(nowui_msdf LANGUAGES CXX)

find_package(Threads REQUIRED)
find_package(msdf-atlas-gen CONFIG REQUIRED)
find_package(Freetype REQUIRED)

option(NOWUI_MSDF_DISABLE_FILE_API "Disable path/PNG/JSON based compiler entry point." ON)

# HarfBuzz is embedded via its officially supported amalgamated build (a single
# translation unit) instead of a package dependency: identical on every
# platform, pinned by git tag in CI, and HB_NO_MT keeps it free of the atomics
# requirements that break Emscripten linking. Leave the directory empty to
# build without shaping support (the exports then return a clean error).
set(NOWUI_HARFBUZZ_DIR "" CACHE PATH "Path to a HarfBuzz source checkout for the amalgamated build.")

set(NOWUI_MSDF_LIBRARY_TYPE SHARED)
if(EMSCRIPTEN)
    set(NOWUI_MSDF_LIBRARY_TYPE STATIC)
    set(NOWUI_MSDF_DISABLE_FILE_API ON CACHE BOOL "Disable file API for WebGL" FORCE)
endif()
if(IOS)
    # Unity links iOS plugins statically into the player (DllImport "__Internal").
    set(NOWUI_MSDF_LIBRARY_TYPE STATIC)
endif()

set(NOWUI_MSDF_SOURCES
    nowui_msdf.cpp
    nowui_msdf.h
)

if(NOWUI_HARFBUZZ_DIR)
    list(APPEND NOWUI_MSDF_SOURCES "${NOWUI_HARFBUZZ_DIR}/src/harfbuzz.cc")
endif()

add_library(nowui-msdf ${NOWUI_MSDF_LIBRARY_TYPE} ${NOWUI_MSDF_SOURCES})

target_compile_features(nowui-msdf PRIVATE cxx_std_11)
target_link_libraries(nowui-msdf PRIVATE msdf-atlas-gen::msdf-atlas-gen Freetype::Freetype)

if(NOWUI_HARFBUZZ_DIR)
    target_include_directories(nowui-msdf PRIVATE "${NOWUI_HARFBUZZ_DIR}/src")
    target_compile_definitions(nowui-msdf PRIVATE HB_NO_MT)
else()
    target_compile_definitions(nowui-msdf PRIVATE NOWUI_MSDF_NO_SHAPING)
endif()

if(NOWUI_MSDF_DISABLE_FILE_API)
    target_compile_definitions(nowui-msdf PRIVATE NOWUI_MSDF_DISABLE_FILE_API)
endif()

if(MSVC)
    target_compile_options(nowui-msdf PRIVATE /W4 /permissive-)
else()
    target_compile_options(nowui-msdf PRIVATE -Wall -Wextra -Wpedantic)
endif()

set_target_properties(nowui-msdf PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN YES
    OUTPUT_NAME "nowui-msdf"
)

if(APPLE)
    set_target_properties(nowui-msdf PROPERTIES
        BUILD_RPATH "@loader_path"
        INSTALL_RPATH "@loader_path"
    )
elseif(UNIX)
    set_target_properties(nowui-msdf PROPERTIES
        BUILD_RPATH "$ORIGIN"
        INSTALL_RPATH "$ORIGIN"
    )
endif()

install(TARGETS nowui-msdf
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

install(FILES nowui_msdf.h DESTINATION include/nowui-msdf)
