// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Internal.Utilities

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open Microsoft.FSharp.Core
open Internal.Utilities.Library

#nowarn "44" // ConfigurationSettings is obsolete but the new stuff is horribly complicated.

module internal FSharpEnvironment =

    type private TypeInThisAssembly = class end

    /// The F# version reported in the banner
    let FSharpBannerVersion =
        UtilsStrings.SR.fSharpBannerVersion (FSharp.BuildProperties.fsProductVersion, FSharp.BuildProperties.fsLanguageVersion)

    let FSharpProductName = UtilsStrings.SR.buildProductName (FSharpBannerVersion)

    let versionOf<'t> : MaybeNull<string> =
        match typeof<'t>.Assembly.GetName().Version with
        | null -> null
        | v -> v.ToString()

    let FSharpCoreLibRunningVersion =
        try
            match versionOf<Unit> with
            | null -> None
            | s when String.IsNullOrEmpty(s) -> None
            | s -> Some(s)
        with _ ->
            None

    // The F# binary format revision number. The first three digits of this form the significant part of the
    // format revision number for F# binary signature and optimization metadata. The last digit is not significant.
    //
    // WARNING: Do not change this revision number unless you absolutely know what you're doing.
    let FSharpBinaryMetadataFormatRevision = "2.0.0.0"

    let isRunningOnCoreClr =
        match typeof<obj>.Assembly.FullName with
        | null -> false
        | name -> name.StartsWith("System.Private.CoreLib", StringComparison.InvariantCultureIgnoreCase)

    module Option =
        /// Convert string into Option string where null and String.Empty result in None
        let ofString s =
            if String.IsNullOrEmpty(s) then None else Some(s)

    let internal tryCurrentDomain () =
        let pathFromCurrentDomain = AppDomain.CurrentDomain.BaseDirectory

        if not (String.IsNullOrEmpty(pathFromCurrentDomain)) then
            Some pathFromCurrentDomain
        else
            None

    // The default location of FSharp.Core.dll and fsc.exe based on the version of fsc.exe that is running
    // Used for
    //     - location of design-time copies of FSharp.Core.dll and FSharp.Compiler.Interactive.Settings.dll for the default assumed environment for scripts
    //     - default ToolPath in tasks in FSharp.Build.dll (for Fsc tasks, but note a probe location is given)
    //     - default F# binaries directory in service.fs (REVIEW: check this)
    //     - default location of fsi.exe in FSharp.VS.FSI.dll (REVIEW: check this)
    //     - default F# binaries directory in (project system) Project.fs
    let BinFolderOfDefaultFSharpCompiler (probePoint: string option) =
        // Check for an app.config setting to redirect the default compiler location
        // Like fsharp-compiler-location
        try
            // We let you set FSHARP_COMPILER_BIN. I've rarely seen this used and its not documented in the install instructions.
            match Environment.GetEnvironmentVariable("FSHARP_COMPILER_BIN") with
            | result when not (String.IsNullOrWhiteSpace result) -> Some !!result
            | _ ->
                let safeExists f =
                    (try
                        File.Exists(f)
                     with _ ->
                         false)

                // Look in the probePoint if given, e.g. look for a compiler alongside of FSharp.Build.dll
                match probePoint with
                | Some p when safeExists (Path.Combine(p, "FSharp.Core.dll")) -> Some p
                | _ ->
                    let fallback () =
                        let d = Assembly.GetExecutingAssembly()

                        Some(!!Path.GetDirectoryName(d.Location))

                    match tryCurrentDomain () with
                    | None -> fallback ()
                    | Some path -> Some path
        with e ->
            None

    // Specify the tooling-compatible fragments of a path such as:
    //     typeproviders/fsharp41/net461/MyProvider.DesignTime.dll
    //     tools/fsharp41/net461/MyProvider.DesignTime.dll
    // See https://github.com/dotnet/fsharp/issues/3736

    // Represents the F#-compiler <-> type provider protocol.
    // When the API or protocol updates, add a new version moniker to the front of the list here.
    let toolingCompatibleTypeProviderProtocolMonikers () = [ "fsharp41" ]

    // Detect the host tooling context
    let toolingCompatibleVersions =
        if typeof<obj>.Assembly.GetName().Name = "mscorlib" then
            [|
                "net481"
                "net48"
                "net472"
                "net471"
                "net47"
                "net462"
                "net461"
                "net452"
                "net451"
                "net45"
                "netstandard2.0"
            |]
        elif typeof<obj>.Assembly.GetName().Name = "System.Private.CoreLib" then
            [|
                "net10.0"
                "net9.0"
                "net8.0"
                "net7.0"
                "net6.0"
                "net5.0"
                "netcoreapp3.1"
                "netcoreapp3.0"
                "netstandard2.1"
                "netcoreapp2.2"
                "netcoreapp2.1"
                "netcoreapp2.0"
                "netstandard2.0"
            |]
        else
            Debug.Assert(false, "Couldn't determine runtime tooling context, assuming it supports at least .NET Standard 2.0")
            [| "netstandard2.0" |]

    let toolPaths = [| "tools"; "typeproviders" |]

    let toolingCompatiblePaths () =
        [
            for toolPath in toolPaths do
                for protocol in toolingCompatibleTypeProviderProtocolMonikers () do
                    for netRuntime in toolingCompatibleVersions do
                        yield Path.Combine(toolPath, protocol, netRuntime)
        ]

    let searchToolPath compilerToolPath =
        seq {
            yield compilerToolPath

            for toolPath in toolingCompatiblePaths () do
                yield Path.Combine(compilerToolPath, toolPath)
        }

    let rec searchToolPaths path compilerToolPaths =
        seq {
            for toolPath in compilerToolPaths do
                yield! searchToolPath toolPath

            match path with
            | None -> ()
            | Some path -> yield! searchToolPath path
        }

    let getTypeProviderAssembly
        (runTimeAssemblyFileName: string, designTimeAssemblyName: string, compilerToolPaths: string list, raiseError)
        =
        // Find and load the designer assembly for the type provider component.
        // We look in the directories stepping up from the location of the runtime assembly.
        let loadFromLocation designTimeAssemblyPath =
            try
                Some(Assembly.UnsafeLoadFrom designTimeAssemblyPath)
            with e ->
                raiseError (Some designTimeAssemblyPath) e

        let rec searchParentDirChain path assemblyName =
            seq {
                match path with
                | None -> ()
                | Some(p: string) ->
                    match Path.GetDirectoryName(p) with
                    | s when String.IsNullOrEmpty(s) || Path.GetFileName(p) = "packages" || s = p -> ()
                    | parentDir -> yield! searchParentDirChain (Option.ofObj parentDir) assemblyName

                for p in searchToolPaths path compilerToolPaths do
                    let fileName = Path.Combine(p, assemblyName)

                    if File.Exists fileName then
                        yield fileName
            }

        let loadFromParentDirRelativeToRuntimeAssemblyLocation designTimeAssemblyName =
            let runTimeAssemblyPath = Path.GetDirectoryName runTimeAssemblyFileName

            let paths =
                searchParentDirChain (Option.ofObj runTimeAssemblyPath) designTimeAssemblyName

            paths
            |> Seq.tryHead
            |> function
                | Some res -> loadFromLocation res
                | None ->
                    // The search failed, just load from the first location and report an error
                    let runTimeAssemblyPath = !!Path.GetDirectoryName(runTimeAssemblyFileName)
                    loadFromLocation (Path.Combine(runTimeAssemblyPath, designTimeAssemblyName))

        if designTimeAssemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) then
            loadFromParentDirRelativeToRuntimeAssemblyLocation designTimeAssemblyName
        else
            // Cover the case where the ".dll" extension has been left off and no version etc. has been used in the assembly
            // string specification.  The Name=FullName comparison is particularly strange, and was there to support
            // design-time DLLs specified using "x.DesignTIme, Version= ..." long assembly names and GAC loads.
            // These kind of design-time assembly specifications are no longer used to our knowledge so that comparison is basically legacy
            // and will always succeed.
            let name = AssemblyName(!!Path.GetFileNameWithoutExtension(designTimeAssemblyName))

            if name.FullName.Equals(name.Name, StringComparison.OrdinalIgnoreCase) then
                let designTimeFileName = designTimeAssemblyName + ".dll"
                loadFromParentDirRelativeToRuntimeAssemblyLocation designTimeFileName
            else
                // Load from the GAC using Assembly.Load.  This is legacy since type provider design-time components are
                // never in the GAC these days and  "x.DesignTIme, Version= ..." specifications are never used.
                try
                    let name = AssemblyName designTimeAssemblyName
                    Some(Assembly.Load name)
                with e ->
                    raiseError None e

    let getCompilerToolsDesignTimeAssemblyPaths compilerToolPaths = searchToolPaths None compilerToolPaths

    let getFSharpCoreLibraryName = "FSharp.Core"
    let fsiLibraryName = "FSharp.Compiler.Interactive.Settings"

    let getFSharpCompilerLocationWithDefaultFromType (defaultLocation: Type) =
        let location =
            try
                let directory = Path.GetDirectoryName(defaultLocation.Assembly.Location)
                Option.ofObj (directory)
            with _ ->
                None

        match BinFolderOfDefaultFSharpCompiler(location) with
        | Some path -> path
        | None ->
            let path = location |> Option.defaultValue "<null>"
#if DEBUG
            Debug.Print(
                sprintf
                    """FSharpEnvironment.BinFolderOfDefaultFSharpCompiler (Some '%s') returned None Location
                customized incorrectly: algorithm here: https://github.com/dotnet/fsharp/blob/03f3f1c35f82af26593d025dabca57a6ef3ea9a1/src/utils/CompilerLocationUtils.fs#L171"""
                    path
            )
#endif
            // Use the location of this dll
            path

    // Fallback to ambient FSharp.CompilerService.dll
    let getFSharpCompilerLocation () =
        Path.Combine(getFSharpCompilerLocationWithDefaultFromType (typeof<TypeInThisAssembly>))

    // Fallback to ambient FSharp.Core.dll
    let getDefaultFSharpCoreLocation () =
        Path.Combine(getFSharpCompilerLocationWithDefaultFromType (typeof<Unit>), getFSharpCoreLibraryName + ".dll")

    // Must be alongside the location of FSharp.CompilerService.dll
    let getDefaultFsiLibraryLocation () =
        Path.Combine(!!Path.GetDirectoryName(getFSharpCompilerLocation ()), fsiLibraryName + ".dll")

    let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

    let dotnet = if isWindows then "dotnet.exe" else "dotnet"

    let fileExists pathToFile =
        try
            File.Exists(pathToFile)
        with _ ->
            false

    // Look for global install of dotnet sdk
    let getDotnetGlobalHostPath () =
        let pf = Environment.GetEnvironmentVariable("ProgramW6432")

        let pf =
            if String.IsNullOrEmpty(pf) then
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            else
                !!pf

        let candidate = Path.Combine(pf, "dotnet", dotnet)

        if fileExists candidate then
            Some candidate
        else
            // Can't find it --- give up
            None

    let getDotnetHostPath () =
        // How to find dotnet.exe --- woe is me; probing rules make me sad.
        // Algorithm:
        // 1. Look for DOTNET_HOST_PATH environment variable
        //    this is the main user programmable override .. provided by user to find a specific dotnet.exe
        // 2. Probe for are we part of an .NetSDK install
        //    In an sdk install we are always installed in:   sdk\3.0.100-rc2-014234\FSharp
        //    dotnet or dotnet.exe will be found in the directory that contains the sdk directory
        // 3. We are loaded in-process to some other application ... Eg. try .net
        //    See if the host is dotnet.exe ... from net5.0 on this is fairly unlikely
        // 4. If it's none of the above we are going to have to rely on the path containing the way to find dotnet.exe
        // Use the path to search for dotnet.exe
        let probePathForDotnetHost () =
            let paths =
                let p = Environment.GetEnvironmentVariable("PATH")

                match p with
                | null -> [||]
                | p -> p.Split(Path.PathSeparator)

            paths |> Array.tryFind (fun f -> fileExists (Path.Combine(f, dotnet)))

        match (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")) with
        // Value set externally
        | NonEmptyString value when fileExists value -> Some value
        | _ ->
            // Probe for netsdk install, dotnet. and dotnet.exe is a constant offset from the location of System.Int32
            let candidate =
                let assemblyLocation =
                    Path.GetDirectoryName(typeof<Int32>.GetTypeInfo().Assembly.Location)

                Path.GetFullPath(Path.Combine(!!assemblyLocation, "..", "..", "..", dotnet))

            if fileExists candidate then
                Some candidate
            else
                match probePathForDotnetHost () with
                | Some f -> Some(Path.Combine(f, dotnet))
                | None -> getDotnetGlobalHostPath ()

    let getDotnetHostDirectories () =
        let isDotnetMultilevelLookup =
            (Int32.TryParse(Environment.GetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP"))
             |> snd)
            <> 0

        [|
            match getDotnetHostPath (), getDotnetGlobalHostPath () with
            | Some hostPath, Some globalHostPath ->
                yield !!Path.GetDirectoryName(hostPath)

                if isDotnetMultilevelLookup && hostPath <> globalHostPath then
                    yield !!Path.GetDirectoryName(globalHostPath)
            | Some hostPath, None -> yield !!Path.GetDirectoryName(hostPath)
            | None, Some globalHostPath -> yield !!Path.GetDirectoryName(globalHostPath)
            | None, None -> ()
        |]

    let getDotnetHostDirectory () =
        getDotnetHostDirectories () |> Array.tryHead

    let getDotnetHostSubDirectories (path: string) =
        [|
            for directory in getDotnetHostDirectories () do
                let subdirectory = Path.Combine(directory, path)

                if Directory.Exists(subdirectory) then
                    yield! DirectoryInfo(subdirectory).GetDirectories()
        |]
