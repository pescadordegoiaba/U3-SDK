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
	"${SMOKE_SHADER_HEADER}"
)

target_include_directories(FidelityFXLinux
	PUBLIC
		${CMAKE_CURRENT_SOURCE_DIR}/include
		${UNITY_PLUGIN_API_DIR}
		${GENERATED_DIR}
)

target_link_libraries(FidelityFXLinux PRIVATE Vulkan::Vulkan)

target_compile_options(FidelityFXLinux PRIVATE
	-Wall
	-Wextra
	-Wpedantic
)

set_target_properties(FidelityFXLinux PROPERTIES
	OUTPUT_NAME "FidelityFXLinux"
	BUILD_RPATH "$ORIGIN"
	INSTALL_RPATH "$ORIGIN"
)
