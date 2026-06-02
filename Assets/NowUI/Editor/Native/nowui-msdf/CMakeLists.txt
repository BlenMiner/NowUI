cmake_minimum_required(VERSION 3.21)

project(nowui_msdf LANGUAGES CXX)

find_package(msdf-atlas-gen CONFIG REQUIRED)

add_library(nowui-msdf SHARED
    nowui_msdf.cpp
    nowui_msdf.h
)

target_compile_features(nowui-msdf PRIVATE cxx_std_11)
target_link_libraries(nowui-msdf PRIVATE msdf-atlas-gen::msdf-atlas-gen)

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
