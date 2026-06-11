cmake_minimum_required(VERSION 3.21)

project(nowui_vg LANGUAGES CXX)

set(NOWUI_VG_LIBRARY_TYPE SHARED)
if(EMSCRIPTEN)
    set(NOWUI_VG_LIBRARY_TYPE STATIC)
endif()
if(IOS)
    # Unity links iOS plugins statically into the player (DllImport "__Internal").
    set(NOWUI_VG_LIBRARY_TYPE STATIC)
endif()

add_library(nowui-vg ${NOWUI_VG_LIBRARY_TYPE}
    nowui_vg.cpp
    nowui_vg.h
)

target_compile_features(nowui-vg PRIVATE cxx_std_17)

if(MSVC)
    target_compile_options(nowui-vg PRIVATE /W4 /permissive- /O2)
else()
    target_compile_options(nowui-vg PRIVATE -Wall -Wextra -Wpedantic -O2)
endif()

set_target_properties(nowui-vg PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN YES
    OUTPUT_NAME "nowui-vg"
)

if(APPLE)
    set_target_properties(nowui-vg PROPERTIES
        BUILD_RPATH "@loader_path"
        INSTALL_RPATH "@loader_path"
    )
elseif(UNIX)
    set_target_properties(nowui-vg PROPERTIES
        BUILD_RPATH "$ORIGIN"
        INSTALL_RPATH "$ORIGIN"
    )
endif()

install(TARGETS nowui-vg
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

install(FILES nowui_vg.h DESTINATION include/nowui-vg)
