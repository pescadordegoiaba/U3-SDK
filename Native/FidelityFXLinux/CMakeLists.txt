cmake_minimum_required(VERSION 3.20)
project(FidelityFXLinux LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(UNITY_PLUGIN_API_DIR "" CACHE PATH "Unity Editor Data/PluginAPI directory")
if(NOT EXISTS "${UNITY_PLUGIN_API_DIR}/IUnityGraphicsVulkan.h")
	message(FATAL_ERROR "UNITY_PLUGIN_API_DIR must contain IUnityGraphicsVulkan.h")
endif()

find_package(Vulkan REQUIRED)
find_program(GLSLANG_VALIDATOR glslangValidator REQUIRED)

set(GENERATED_DIR "${CMAKE_CURRENT_BINARY_DIR}/generated")
set(FSR2_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/FidelityFX-FSR2/src/ffx-fsr2-api")
set(FSR2_GENERATED_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/FidelityFX-FSR2/generated/vk")
set(FSR2_SOURCES
	${FSR2_DIR}/ffx_assert.cpp
	${FSR2_DIR}/ffx_fsr2.cpp
	${FSR2_DIR}/vk/ffx_fsr2_vk.cpp
	${FSR2_DIR}/vk/shaders/ffx_fsr2_shaders_vk.cpp
)
set(SMOKE_SHADER_HEADER "${GENERATED_DIR}/vulkan_smoke_comp_spv.h")
add_custom_command(
	OUTPUT "${SMOKE_SHADER_HEADER}"
	COMMAND ${CMAKE_COMMAND} -E make_directory "${GENERATED_DIR}"
	COMMAND "${GLSLANG_VALIDATOR}" -V --vn u3ffx_vulkan_smoke_comp_spv -o "${SMOKE_SHADER_HEADER}" "${CMAKE_CURRENT_SOURCE_DIR}/src/vulkan_smoke.comp"
	DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/src/vulkan_smoke.comp"
	VERBATIM
)

add_library(FidelityFXLinux SHARED
	src/fidelityfx_linux.cpp
	${FSR2_SOURCES}
	"${SMOKE_SHADER_HEADER}"
)

target_include_directories(FidelityFXLinux
	PUBLIC
		${CMAKE_CURRENT_SOURCE_DIR}/include
		${UNITY_PLUGIN_API_DIR}
		${GENERATED_DIR}
		${FSR2_DIR}
		${FSR2_DIR}/vk
		${FSR2_DIR}/vk/shaders
		${FSR2_DIR}/shaders
		${FSR2_GENERATED_DIR}
)

target_link_libraries(FidelityFXLinux PRIVATE Vulkan::Vulkan)

target_compile_options(FidelityFXLinux PRIVATE
	-include ${CMAKE_CURRENT_SOURCE_DIR}/src/ffx_linux_compat.h
	-Wall
	-Wextra
	-Wpedantic
)

target_compile_definitions(FidelityFXLinux PRIVATE FFX_GCC=1)

# O código AMD e os headers gerados usam inicializadores e structs anônimas aceitas
# pelo toolchain upstream. Limite essas supressões aos arquivos vendorizados.
set_source_files_properties(${FSR2_SOURCES} PROPERTIES COMPILE_OPTIONS
	"-Wno-pedantic;-Wno-missing-field-initializers;-Wno-unused-parameter;-Wno-unused-variable;-Wno-unused-but-set-variable;-Wno-sign-compare;-Wno-deprecated-declarations"
)

set_target_properties(FidelityFXLinux PROPERTIES
	OUTPUT_NAME "FidelityFXLinux"
	BUILD_RPATH "$ORIGIN"
	INSTALL_RPATH "$ORIGIN"
)
