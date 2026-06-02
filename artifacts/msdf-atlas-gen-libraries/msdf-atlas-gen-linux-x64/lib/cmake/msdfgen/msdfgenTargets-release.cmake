#----------------------------------------------------------------
# Generated CMake target import file for configuration "Release".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "msdfgen::msdfgen-core" for configuration "Release"
set_property(TARGET msdfgen::msdfgen-core APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(msdfgen::msdfgen-core PROPERTIES
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/lib/libmsdfgen-core.so"
  IMPORTED_SONAME_RELEASE "libmsdfgen-core.so"
  )

list(APPEND _cmake_import_check_targets msdfgen::msdfgen-core )
list(APPEND _cmake_import_check_files_for_msdfgen::msdfgen-core "${_IMPORT_PREFIX}/lib/libmsdfgen-core.so" )

# Import target "msdfgen::msdfgen-ext" for configuration "Release"
set_property(TARGET msdfgen::msdfgen-ext APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(msdfgen::msdfgen-ext PROPERTIES
  IMPORTED_LINK_DEPENDENT_LIBRARIES_RELEASE "msdfgen::msdfgen-core"
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/lib/libmsdfgen-ext.so"
  IMPORTED_SONAME_RELEASE "libmsdfgen-ext.so"
  )

list(APPEND _cmake_import_check_targets msdfgen::msdfgen-ext )
list(APPEND _cmake_import_check_files_for_msdfgen::msdfgen-ext "${_IMPORT_PREFIX}/lib/libmsdfgen-ext.so" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
