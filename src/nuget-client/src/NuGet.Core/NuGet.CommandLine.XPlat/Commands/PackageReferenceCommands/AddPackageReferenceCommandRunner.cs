// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    internal class AddPackageReferenceCommandRunner : IPackageReferenceCommandRunner
    {
        public async Task<int> ExecuteCommand(PackageReferenceArgs packageReferenceArgs, MSBuildAPIUtility msBuild)
        {
            packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.Info_AddPkgAddingReference,
                packageReferenceArgs.PackageId,
                packageReferenceArgs.ProjectPath));

            if (packageReferenceArgs.NoRestore)
            {
                packageReferenceArgs.Logger.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.Warn_AddPkgWithoutRestore));

                VersionRange versionRange = default;
                if (packageReferenceArgs.NoVersion)
                {
                    versionRange = packageReferenceArgs.Prerelease ?
                                        VersionRange.Parse("*-*") :
                                        VersionRange.Parse("*");
                }
                else
                {
                    versionRange = VersionRange.Parse(packageReferenceArgs.PackageVersion);
                }

                var libraryDependency = new LibraryDependency()
                {
                    LibraryRange = new LibraryRange(
                        name: packageReferenceArgs.PackageId,
                        versionRange: versionRange,
                        typeConstraint: LibraryDependencyTarget.Package)
                };

                msBuild.AddPackageReference(packageReferenceArgs.ProjectPath, libraryDependency, packageReferenceArgs.NoVersion);
                return 0;
            }

            // 1. Get project dg file
            packageReferenceArgs.Logger.LogDebug("Reading project Dependency Graph");
            var dgSpec = ReadProjectDependencyGraph(packageReferenceArgs);
            if (dgSpec == null)
            {
                // Logging non localized error on debug stream.
                packageReferenceArgs.Logger.LogDebug(Strings.Error_NoDgSpec);

                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_NoDgSpec));
            }
            packageReferenceArgs.Logger.LogDebug("Project Dependency Graph Read");

            var projectFullPath = Path.GetFullPath(packageReferenceArgs.ProjectPath);

            var matchingPackageSpecs = dgSpec
                .Projects
                .Where(p => p.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference &&
                PathUtility.GetStringComparerBasedOnOS().Equals(Path.GetFullPath(p.RestoreMetadata.ProjectPath), projectFullPath))
                .ToArray();

            // This ensures that the DG specs generated in previous steps contain exactly 1 project with the same path as the project requesting add package.
            // Throw otherwise since we cannot proceed further.
            if (matchingPackageSpecs.Length != 1)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_UnsupportedProject,
                    packageReferenceArgs.PackageId,
                    packageReferenceArgs.ProjectPath));
            }

            // Parse the user specified frameworks once to avoid re-do's
            var userSpecifiedFrameworks = new List<NuGetFramework>();
            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                userSpecifiedFrameworks.AddRange(packageReferenceArgs
                    .Frameworks
                    .Select(NuGetFramework.Parse));
            }

            var originalPackageSpec = matchingPackageSpecs.FirstOrDefault();

            // Check if the project files are correct for CPM
            if (originalPackageSpec.RestoreMetadata.CentralPackageVersionsEnabled && !msBuild.AreCentralVersionRequirementsSatisfied(packageReferenceArgs, originalPackageSpec))
            {
                return 1;
            }

            // 2. Determine the version

            // Setup the Credential Service before making any potential http calls.
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(packageReferenceArgs.Logger, !packageReferenceArgs.Interactive);

            if (packageReferenceArgs.Sources?.Any() == true)
            {
                // Convert relative path to absolute path if there is any
                List<string> sources = new List<string>();

                foreach (string source in packageReferenceArgs.Sources)
                {
                    sources.Add(UriUtility.GetAbsolutePath(Environment.CurrentDirectory, source));
                }

                originalPackageSpec.RestoreMetadata.Sources =
                                    sources.Where(ns => !string.IsNullOrEmpty(ns))
                                    .Select(ns => new PackageSource(ns))
                                    .ToList();
            }

            PackageDependency packageDependency = default;
            if (packageReferenceArgs.NoVersion)
            {
                if (originalPackageSpec.RestoreMetadata.CentralPackageVersionsEnabled)
                {
                    var centralVersion = originalPackageSpec
                        .TargetFrameworks
                        .Where(tf => tf.CentralPackageVersions.ContainsKey(packageReferenceArgs.PackageId))
                        .Select(tf => tf.CentralPackageVersions[packageReferenceArgs.PackageId])
                        .FirstOrDefault();
                    if (centralVersion != null)
                    {
                        packageDependency = new PackageDependency(packageReferenceArgs.PackageId, centralVersion.VersionRange);
                    }
                }
                if (packageDependency == null)
                {
                    var latestVersion = await GetLatestVersionAsync(originalPackageSpec, packageReferenceArgs.PackageId, packageReferenceArgs.Logger, packageReferenceArgs.Prerelease);

                    if (latestVersion == null)
                    {
                        if (!packageReferenceArgs.Prerelease)
                        {
                            latestVersion = await GetLatestVersionAsync(originalPackageSpec, packageReferenceArgs.PackageId, packageReferenceArgs.Logger, !packageReferenceArgs.Prerelease);
                            if (latestVersion != null)
                            {
                                throw new CommandException(string.Format(CultureInfo.CurrentCulture, Strings.PrereleaseVersionsAvailable, latestVersion));
                            }
                        }
                        throw new CommandException(Messages.Error_NoVersionsAvailable(packageReferenceArgs.PackageId));
                    }

                    packageDependency = new PackageDependency(packageReferenceArgs.PackageId, new VersionRange(minVersion: latestVersion, includeMinVersion: true));
                }
            }
            else
            {
                packageDependency = new PackageDependency(packageReferenceArgs.PackageId, VersionRange.Parse(packageReferenceArgs.PackageVersion));
            }

            // Create a copy to avoid modifying the original spec which may be shared.
            var updatedPackageSpec = originalPackageSpec.Clone();
            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                // If user specified frameworks then just use them to add the dependency
                PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec,
                    packageDependency,
                    userSpecifiedFrameworks);
            }
            else
            {
                PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, packageDependency);
            }

            var updatedDgSpec = dgSpec.WithReplacedSpec(updatedPackageSpec).WithoutRestores();
            updatedDgSpec.AddRestore(updatedPackageSpec.RestoreMetadata.ProjectUniqueName);

            // 3. Run Restore Preview
            packageReferenceArgs.Logger.LogDebug("Running Restore preview");

            var restorePreviewResult = await PreviewAddPackageReferenceAsync(packageReferenceArgs,
                updatedDgSpec);

            packageReferenceArgs.Logger.LogDebug("Restore Review completed");

            // 4. Process Restore Result
            var compatibleFrameworks = new HashSet<NuGetFramework>(
                restorePreviewResult
                .Result
                .CompatibilityCheckResults
                .Where(t => t.Success)
                .Select(t => t.Graph.Framework), NuGetFrameworkFullComparer.Instance);

            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                // If the user has specified frameworks then we intersect that with the compatible frameworks.
                var userSpecifiedFrameworkSet = new HashSet<NuGetFramework>(
                    userSpecifiedFrameworks,
                    NuGetFrameworkFullComparer.Instance);

                compatibleFrameworks.IntersectWith(userSpecifiedFrameworkSet);
            }

            // 5. Write to Project
            if (compatibleFrameworks.Count == 0)
            {
                // Package is compatible with none of the project TFMs
                // Do not add a package reference, throw appropriate error
                packageReferenceArgs.Logger.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_AddPkgIncompatibleWithAllFrameworks,
                    packageReferenceArgs.PackageId,
                    packageReferenceArgs.Frameworks?.Any() == true ? Strings.AddPkg_UserSpecified : Strings.AddPkg_All,
                    packageReferenceArgs.ProjectPath));

                return 1;
            }
            // Ignore the graphs with RID
            else if (compatibleFrameworks.Count ==
                     restorePreviewResult.Result.CompatibilityCheckResults.Count(r => string.IsNullOrEmpty(r.Graph.RuntimeIdentifier)))
            {
                // Package is compatible with all the project TFMs
                // Add an unconditional package reference to the project
                packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Info_AddPkgCompatibleWithAllFrameworks,
                    packageReferenceArgs.PackageId,
                    packageReferenceArgs.ProjectPath));

                // generate a library dependency with all the metadata like Include, Exlude and SuppressParent
                var libraryDependency = GenerateLibraryDependency(updatedPackageSpec, packageReferenceArgs.PackageDirectory, restorePreviewResult, userSpecifiedFrameworks, packageDependency);

                msBuild.AddPackageReference(packageReferenceArgs.ProjectPath, libraryDependency, packageReferenceArgs.NoVersion);
            }
            else
            {
                // Package is compatible with some of the project TFMs
                // Add conditional package references to the project for the compatible TFMs
                packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Info_AddPkgCompatibleWithSubsetFrameworks,
                    packageReferenceArgs.PackageId,
                    packageReferenceArgs.ProjectPath));

                var compatibleOriginalFrameworks = compatibleFrameworks
                    .Select(e => GetAliasForFramework(originalPackageSpec, e))
                    .Where(originalFramework => originalFramework != null);

                // generate a library dependency with all the metadata like Include, Exlude and SuppressParent
                var libraryDependency = GenerateLibraryDependency(updatedPackageSpec, packageReferenceArgs.PackageDirectory, restorePreviewResult, userSpecifiedFrameworks, packageDependency);

                msBuild.AddPackageReferencePerTFM(packageReferenceArgs.ProjectPath,
                    libraryDependency,
                    compatibleOriginalFrameworks,
                    packageReferenceArgs.NoVersion);
            }

            // 6. Commit restore result
            await RestoreRunner.CommitAsync(restorePreviewResult, CancellationToken.None);

            return 0;
        }

        internal static string GetAliasForFramework(PackageSpec spec, NuGetFramework framework)
        {
            return spec.TargetFrameworks.FirstOrDefault(e => e.FrameworkName.Equals(framework))?.TargetAlias;
        }

        public static async Task<NuGetVersion> GetLatestVersionAsync(PackageSpec originalPackageSpec, string packageId, ILogger logger, bool prerelease)
        {
            IList<PackageSource> sources = AddPackageCommandUtility.EvaluateSources(originalPackageSpec.RestoreMetadata.Sources, originalPackageSpec.RestoreMetadata.ConfigFilePaths);

            return await AddPackageCommandUtility.GetLatestVersionFromSourcesAsync(sources, logger, packageId, prerelease, CancellationToken.None);
        }

        internal static LibraryDependency GenerateLibraryDependency(
            PackageSpec project,
            string customPackagesPath,
            RestoreResultPair restorePreviewResult,
            List<NuGetFramework> userSpecifiedFrameworks,
            PackageDependency packageDependency)
        {
            // get the package resolved version from restore preview result
            var resolvedVersion = GetPackageVersionFromRestoreResult(restorePreviewResult, packageDependency.Id, userSpecifiedFrameworks);

            // correct package version to write in project file
            var version = packageDependency.VersionRange;

            // update default packages path if user specified custom package directory
            var packagesPath = project.RestoreMetadata.PackagesPath;

            // get if the project is onboarded to CPM
            var isCentralPackageManagementEnabled = project.RestoreMetadata.CentralPackageVersionsEnabled;

            if (!string.IsNullOrEmpty(customPackagesPath))
            {
                packagesPath = customPackagesPath;
            }

            // create a path resolver to get nuspec file of the package
            var pathResolver = new FallbackPackagePathResolver(
                packagesPath,
                project.RestoreMetadata.FallbackFolders);
            var info = pathResolver.GetPackageInfo(packageDependency.Id, resolvedVersion);
            var packageDirectory = info?.PathResolver.GetInstallPath(packageDependency.Id, resolvedVersion);
            var nuspecFile = info?.PathResolver.GetManifestFileName(packageDependency.Id, resolvedVersion);

            var nuspecFilePath = Path.GetFullPath(Path.Combine(packageDirectory, nuspecFile));

            // read development dependency from nuspec file
            NuspecReader nuspecReader = new(nuspecFilePath);
            var developmentDependency = nuspecReader.GetDevelopmentDependency();
            string packageId = nuspecReader.GetId();

            if (developmentDependency)
            {
                var orderedFrameworksWithOriginalIndex = project.TargetFrameworks
                    .Select((frameworkInfo, originalIndex) => (frameworkInfo, originalIndex))
                    .OrderBy(tuple => tuple.frameworkInfo.FrameworkName.ToString(), StringComparer.Ordinal);

                foreach (var (frameworkInfo, originalIndex) in orderedFrameworksWithOriginalIndex)
                {
                    var index = frameworkInfo.Dependencies.FirstIndex(dep => dep.Name.Equals(packageDependency.Id, StringComparison.OrdinalIgnoreCase));
                    var dependency = frameworkInfo.Dependencies[index];
                    var includeType = dependency.IncludeType;
                    var suppressParent = dependency.SuppressParent;

                    // if suppressParent and IncludeType aren't set by user, then only update those as per dev dependency
                    if (suppressParent == LibraryIncludeFlagUtils.DefaultSuppressParent &&
                        includeType == LibraryIncludeFlags.All)
                    {
                        suppressParent = LibraryIncludeFlags.All;
                        includeType = LibraryIncludeFlags.All & ~LibraryIncludeFlags.Compile;
                    }

                    dependency = new LibraryDependency(dependency)
                    {
                        IncludeType = includeType,
                        LibraryRange = new LibraryRange(dependency.LibraryRange) { VersionRange = version, Name = packageId },
                        SuppressParent = suppressParent,
                        VersionCentrallyManaged = isCentralPackageManagementEnabled,
                    };

                    var newDependencies = frameworkInfo.Dependencies.SetItem(index, dependency);
                    project.TargetFrameworks[originalIndex] = new TargetFrameworkInformation(frameworkInfo) { Dependencies = newDependencies };

                    return dependency;
                }
            }

            return new LibraryDependency()
            {
                LibraryRange = new LibraryRange(
                    name: packageId,
                    versionRange: version,
                    typeConstraint: LibraryDependencyTarget.Package),
                VersionCentrallyManaged = isCentralPackageManagementEnabled
            };
        }

        private static async Task<RestoreResultPair> PreviewAddPackageReferenceAsync(PackageReferenceArgs packageReferenceArgs,
            DependencyGraphSpec dgSpec)
        {
            // Set user agent and connection settings.
            XPlatUtility.ConfigureProtocol();

            var providerCache = new RestoreCommandProvidersCache();

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = false;
                cacheContext.IgnoreFailedSources = false;

                // Pre-loaded request provider containing the graph file
                var providers = new List<IPreLoadedRestoreRequestProvider>
                {
                    new DependencyGraphSpecRequestProvider(providerCache, dgSpec)
                };

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = cacheContext,
                    LockFileVersion = LockFileFormat.Version,
                    Log = packageReferenceArgs.Logger,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    GlobalPackagesFolder = packageReferenceArgs.PackageDirectory,
                    PreLoadedRequestProviders = providers
                    // Sources : No need to pass it, because SourceRepositories contains the already built SourceRepository objects
                };

                // Generate Restore Requests. There will always be 1 request here since we are restoring for 1 project.
                var restoreRequests = await RestoreRunner.GetRequests(restoreContext);

                // Run restore without commit. This will always return 1 Result pair since we are restoring for 1 request.
                var restoreResult = await RestoreRunner.RunWithoutCommit(restoreRequests, restoreContext);

                return restoreResult.Single();
            }
        }

        private static DependencyGraphSpec ReadProjectDependencyGraph(PackageReferenceArgs packageReferenceArgs)
        {
            DependencyGraphSpec spec = null;

            if (File.Exists(packageReferenceArgs.DgFilePath))
            {
                spec = DependencyGraphSpec.Load(packageReferenceArgs.DgFilePath);
            }

            return spec;
        }

        private static NuGetVersion GetPackageVersionFromRestoreResult(RestoreResultPair restorePreviewResult,
            string packageId,
            List<NuGetFramework> userSpecifiedFrameworks)
        {
            // Get the restore graphs from the restore result
            var restoreGraphs = restorePreviewResult
                .Result
                .RestoreGraphs;

            if (userSpecifiedFrameworks.Count > 1)
            {
                // If the user specified frameworks then we get the flattened graphs  only from the compatible frameworks.
                var userSpecifiedFrameworkSet = new HashSet<NuGetFramework>(
                    userSpecifiedFrameworks,
                    NuGetFrameworkFullComparer.Instance);

                restoreGraphs = restoreGraphs
                    .Where(r => userSpecifiedFrameworkSet.Contains(r.Framework));
            }

            foreach (var restoreGraph in restoreGraphs)
            {
                var matchingPackageEntries = restoreGraph
                    .Flattened
                    .Select(p => p)
                    .Where(p => p.Key.Name.Equals(packageId, StringComparison.OrdinalIgnoreCase));

                if (matchingPackageEntries.Any())
                {
                    return matchingPackageEntries
                        .First()
                        .Key
                        .Version;
                }
            }
            return null;
        }
    }
}
