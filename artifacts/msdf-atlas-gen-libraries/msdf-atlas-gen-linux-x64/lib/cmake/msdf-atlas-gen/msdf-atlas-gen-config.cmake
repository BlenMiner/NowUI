
include(CMakeFindDependencyMacro)

set(MSDF_ATLAS_STANDALONE_AVAILABLE OFF)
set(MSDF_ATLAS_NO_PNG OFF)

if(NOT MSDF_ATLAS_NO_PNG)
    find_dependency(PNG REQUIRED)
endif()
find_dependency(msdfgen REQUIRED)

include("${CMAKE_CURRENT_LIST_DIR}/msdf-atlas-gen-targets.cmake")

if(MSDF_ATLAS_STANDALONE_AVAILABLE)
    include("${CMAKE_CURRENT_LIST_DIR}/msdf-atlas-gen-binary-targets.cmake")
    if(${CMAKE_VERSION} VERSION_LESS "3.18.0")
        set_target_properties(msdf-atlas-gen-standalone::msdf-atlas-gen-standalone PROPERTIES IMPORTED_GLOBAL TRUE)
    endif()
    add_executable(msdf-atlas-gen::msdf-atlas-gen-run ALIAS msdf-atlas-gen-standalone::msdf-atlas-gen-standalone)
    set(MSDF_ATLAS_GEN_EXECUTABLE "/home/runner/work/Now-UI/Now-UI/artifacts/msdf-atlas-gen-linux-x64/bin/msdf-atlas-gen")
endif()
