#!/usr/bin/env bash

echo "Commencing build of native components"

__RepoRootDir="$(cd "$(dirname "$0")"/..; pwd -P)"
__BuildType=Debug
__TargetArch=
__Compiler=clang
__CommonMSBuildArgs=
__ConfigureOnly=0
__PortableBuild=1
__SkipConfigure=0
__RootBinDir="$__RepoRootDir/artifacts"
__ArtifactsObjDir="$__RootBinDir/obj"
__ArtifactsBinDir="$__RootBinDir/bin"
__UnprocessedBuildArgs=
__CMakeArgs=
__CMakeTarget=install

source "$__RepoRootDir"/eng/native/build-commons.sh

# Set the remaining variables based upon the determined build configuration
__ConfigTriplet="$__TargetOS.$__TargetArch.$__BuildType"
__LogsDir="$__RootBinDir/log/$__BuildType"
__IntermediatesDir="$__ArtifactsObjDir/$__ConfigTriplet"
__BinDir="$__ArtifactsBinDir/$__ConfigTriplet"

__CMakeBinDir="$__BinDir"
export __IntermediatesDir __CMakeBinDir

setup_dirs
check_prereqs

if [[ "$__TargetArch" != "$__HostArch" ]]; then
    __CMakeArgs="-DCLR_CMAKE_TARGET_ARCH=$__TargetArch $__CMakeArgs"
fi

build_native "$__HostOS" "$__HostArch" "$__RepoRootDir" "$__IntermediatesDir" "$__CMakeTarget" "$__CMakeArgs" "diagnostics"

echo "Native binaries are available at $__BinDir"
exit 0