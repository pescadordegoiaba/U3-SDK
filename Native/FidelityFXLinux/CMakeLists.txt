cmake_minimum_required(VERSION 3.20)
project(FidelityFXLinux LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

add_library(FidelityFXLinux SHARED
	src/fidelityfx_linux.cpp
)

target_include_directories(FidelityFXLinux
	PUBLIC
		${CMAKE_CURRENT_SOURCE_DIR}/include
)

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
