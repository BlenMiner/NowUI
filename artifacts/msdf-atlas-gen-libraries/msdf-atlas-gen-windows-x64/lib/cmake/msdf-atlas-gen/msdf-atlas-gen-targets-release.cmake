#----------------------------------------------------------------
# Generated CMake target import file for configuration "Release".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "msdf-atlas-gen::msdf-atlas-gen" for configuration "Release"
set_property(TARGET msdf-atlas-gen::msdf-atlas-gen APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(msdf-atlas-gen::msdf-atlas-gen PROPERTIES
  IMPORTED_IMPLIB_RELEASE "${_IMPORT_PREFIX}/lib/msdf-atlas-gen.lib"
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/bin/msdf-atlas-gen.dll"
  )

list(APPEND _cmake_import_check_targets msdf-atlas-gen::msdf-atlas-gen )
list(APPEND _cmake_import_check_files_for_msdf-atlas-gen::msdf-atlas-gen "${_IMPORT_PREFIX}/lib/msdf-atlas-gen.lib" "${_IMPORT_PREFIX}/bin/msdf-atlas-gen.dll" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
