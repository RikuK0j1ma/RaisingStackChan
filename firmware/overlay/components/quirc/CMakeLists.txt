# quirc (ISC license, https://github.com/dlbeer/quirc), vendored for the Wi-Fi QR scan of MOGUMOGU.
# Unmodified copy of lib/. The sources are plain C and need no IDF component.
idf_component_register(SRCS "decode.c" "identify.c" "quirc.c" "version_db.c"
                       INCLUDE_DIRS ".")
# Upstream sources are not warning-clean under IDF's flags
target_compile_options(${COMPONENT_LIB} PRIVATE -Wno-unused-parameter -Wno-sign-compare -Wno-implicit-fallthrough)
