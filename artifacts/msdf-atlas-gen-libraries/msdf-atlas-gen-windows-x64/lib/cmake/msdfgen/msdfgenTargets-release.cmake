#----------------------------------------------------------------
# Generated CMake target import file for configuration "Release".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "msdfgen::msdfgen-core" for configuration "Release"
set_property(TARGET msdfgen::msdfgen-core APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(msdfgen::msdfgen-core PROPERTIES
  IMPORTED_IMPLIB_RELEASE "${_IMPORT_PREFIX}/lib/msdfgen-core.lib"
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/bin/msdfgen-core.dll"
  )

list(APPEND _cmake_import_check_targets msdfgen::msdfgen-core )
list(APPEND _cmake_import_check_files_for_msdfgen::msdfgen-core "${_IMPORT_PREFIX}/lib/msdfgen-core.lib" "${_IMPORT_PREFIX}/bin/msdfgen-core.dll" )

# Import target "msdfgen::msdfgen-ext" for configuration "Release"
set_property(TARGET msdfgen::msdfgen-ext APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(msdfgen::msdfgen-ext PROPERTIES
  IMPORTED_IMPLIB_RELEASE "${_IMPORT_PREFIX}/lib/msdfgen-ext.lib"
  IMPORTED_LINK_DEPENDENT_LIBRARIES_RELEASE "msdfgen::msdfgen-core"
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/bin/msdfgen-ext.dll"
  )

list(APPEND _cmake_import_check_targets msdfgen::msdfgen-ext )
list(APPEND _cmake_import_check_files_for_msdfgen::msdfgen-ext "${_IMPORT_PREFIX}/lib/msdfgen-ext.lib" "${_IMPORT_PREFIX}/bin/msdfgen-ext.dll" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
