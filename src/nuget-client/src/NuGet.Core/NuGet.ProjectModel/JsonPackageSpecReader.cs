// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public static partial class JsonPackageSpecReader
    {
        private static readonly char[] DelimitedStringSeparators = { ' ', ',' };
        private static readonly char[] VersionSeparators = new[] { ';' };
        private const char VersionSeparator = ';';
        public static readonly string RestoreOptions = "restore";
        public static readonly string RestoreSettings = "restoreSettings";
        public static readonly string HideWarningsAndErrors = "hideWarningsAndErrors";
        public static readonly string PackOptions = "packOptions";
        public static readonly string PackageType = "packageType";
        public static readonly string Files = "files";

        /// <summary>
        /// Load and parse a project.json file
        /// </summary>
        /// <param name="name">project name</param>
        /// <param name="packageSpecPath">file path</param>
        public static PackageSpec GetPackageSpec(string name, string packageSpecPath)
        {
            return FileUtility.SafeRead(filePath: packageSpecPath, read: (stream, filePath) => GetPackageSpec(stream, name, filePath, null));
        }

        public static PackageSpec GetPackageSpec(string json, string name, string packageSpecPath)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return GetPackageSpec(ms, name, packageSpecPath, null);
            }
        }

        public static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue)
        {
            return GetPackageSpec(stream, name, packageSpecPath, snapshotValue, EnvironmentVariableWrapper.Instance);
        }

        [Obsolete("This method is obsolete and will be removed in a future release.")]
        public static PackageSpec GetPackageSpec(JObject json)
        {
            return GetPackageSpec(json, name: null, packageSpecPath: null, snapshotValue: null);
        }

        [Obsolete("This method is obsolete and will be removed in a future release.")]
        public static PackageSpec GetPackageSpec(JObject rawPackageSpec, string name, string packageSpecPath, string snapshotValue)
        {
            using (var stringReader = new StringReader(rawPackageSpec.ToString()))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                return GetPackageSpec(jsonReader, name, packageSpecPath, EnvironmentVariableWrapper.Instance, snapshotValue);
            }
        }

        [Obsolete]
        internal static PackageSpec GetPackageSpec(JsonTextReader jsonReader, string packageSpecPath)
        {
            return GetPackageSpec(jsonReader, name: null, packageSpecPath, EnvironmentVariableWrapper.Instance);
        }

        internal static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue, IEnvironmentVariableReader environmentVariableReader, bool bypassCache = false)
        {
            if (!JsonUtility.UseNewtonsoftJsonForParsing(environmentVariableReader, bypassCache))
            {
                return GetPackageSpecUtf8JsonStreamReader(stream, name, packageSpecPath, environmentVariableReader, snapshotValue);
            }
            else
            {
                using (var textReader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(textReader))
                {
#pragma warning disable CS0612 // Type or member is obsolete
                    return GetPackageSpec(jsonReader, name, packageSpecPath, environmentVariableReader, snapshotValue);
#pragma warning restore CS0612 // Type or member is obsolete
                }
            }
        }

        [Obsolete]
        internal static PackageSpec GetPackageSpec(JsonTextReader jsonReader, string name, string packageSpecPath, IEnvironmentVariableReader environmentVariableReader, string snapshotValue = null)
        {
            var packageSpec = new PackageSpec();

            List<CompatibilityProfile> compatibilityProfiles = null;
            List<RuntimeDescription> runtimeDescriptions = null;

            string filePath = name == null ? null : Path.GetFullPath(packageSpecPath);

            jsonReader.ReadObject(propertyName =>
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    return;
                }

                switch (propertyName)
                {
                    case "dependencies":
                        ReadDependencies(
                            jsonReader,
                            packageSpec.Dependencies,
                            filePath,
                            isGacOrFrameworkReference: false);
                        break;

                    case "frameworks":
                        ReadFrameworks(jsonReader, packageSpec);
                        break;

                    case "restore":
                        ReadMSBuildMetadata(jsonReader, packageSpec, environmentVariableReader);
                        break;

                    case "runtimes":
                        runtimeDescriptions = ReadRuntimes(jsonReader);
                        break;

                    case "supports":
                        compatibilityProfiles = ReadSupports(jsonReader);
                        break;

                    case "version":
                        string version = jsonReader.ReadAsString();

                        if (version != null)
                        {
                            try
                            {
                                packageSpec.Version = PackageSpecUtility.SpecifySnapshot(version, snapshotValue);
                            }
                            catch (Exception ex)
                            {
                                throw FileFormatException.Create(ex, version, packageSpec.FilePath);
                            }
                        }
                        break;
                }
            });

            packageSpec.Name = name;
            packageSpec.FilePath = name == null ? null : Path.GetFullPath(packageSpecPath);

            packageSpec.RuntimeGraph = new RuntimeGraph(
                runtimeDescriptions ?? Enumerable.Empty<RuntimeDescription>(),
                compatibilityProfiles ?? Enumerable.Empty<CompatibilityProfile>());

            if (packageSpec.Name == null)
            {
                packageSpec.Name = packageSpec.RestoreMetadata?.ProjectName;
            }

            // Use the project.json path if one is set, otherwise use the project path
            if (packageSpec.FilePath == null)
            {
                packageSpec.FilePath = packageSpec.RestoreMetadata?.ProjectJsonPath
                    ?? packageSpec.RestoreMetadata?.ProjectPath;
            }

            return packageSpec;
        }

        [Obsolete]
        private static void ReadCentralPackageVersions(
            JsonTextReader jsonReader,
            IDictionary<string, CentralPackageVersion> centralPackageVersions,
            string filePath)
        {
            jsonReader.ReadObject(propertyName =>
            {
                int line = jsonReader.LineNumber;
                int column = jsonReader.LinePosition;

                if (string.IsNullOrEmpty(propertyName))
                {
                    throw FileFormatException.Create(
                        "Unable to resolve central version ''.",
                        line,
                        column,
                        filePath);
                }

                string version = jsonReader.ReadNextTokenAsString();

                if (string.IsNullOrEmpty(version))
                {
                    throw FileFormatException.Create(
                        "The version cannot be null or empty.",
                        line,
                        column,
                        filePath);
                }

                centralPackageVersions[propertyName] = new CentralPackageVersion(propertyName, VersionRange.Parse(version));
            });
        }

        [Obsolete]
        private static void ReadPackagesToPrune(
            JsonTextReader jsonReader,
            IDictionary<string, PrunePackageReference> packagesToPrune,
            string filePath)
        {
            jsonReader.ReadObject(propertyName =>
            {
                int line = jsonReader.LineNumber;
                int column = jsonReader.LinePosition;

                if (string.IsNullOrEmpty(propertyName))
                {
                    throw FileFormatException.Create(
                        "Unable to resolve package to prune version ''.",
                        line,
                        column,
                        filePath);
                }

                string version = jsonReader.ReadNextTokenAsString();

                if (string.IsNullOrEmpty(version))
                {
                    throw FileFormatException.Create(
                        "The version cannot be null or empty.",
                        line,
                        column,
                        filePath);
                }

                packagesToPrune[propertyName] = new PrunePackageReference(propertyName, VersionRange.Parse(version));
            });
        }

        [Obsolete]
        private static CompatibilityProfile ReadCompatibilityProfile(JsonTextReader jsonReader, string profileName)
        {
            List<FrameworkRuntimePair> sets = null;

            jsonReader.ReadObject(propertyName =>
            {
                sets = sets ?? new List<FrameworkRuntimePair>();

                IEnumerable<FrameworkRuntimePair> profiles = ReadCompatibilitySets(jsonReader, propertyName);

                sets.AddRange(profiles);
            });

            return new CompatibilityProfile(profileName, sets ?? Enumerable.Empty<FrameworkRuntimePair>());
        }

        [Obsolete]
        private static IEnumerable<FrameworkRuntimePair> ReadCompatibilitySets(JsonTextReader jsonReader, string compatibilitySetName)
        {
            NuGetFramework framework = NuGetFramework.Parse(compatibilitySetName);

            IReadOnlyList<string> values = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList() ?? Array.Empty<string>();

            foreach (string value in values)
            {
                yield return new FrameworkRuntimePair(framework, value);
            }
        }

        [Obsolete]
        private static void ReadDependencies(
            JsonTextReader jsonReader,
            IList<LibraryDependency> results,
            string packageSpecPath,
            bool isGacOrFrameworkReference)
        {
            jsonReader.ReadObject(propertyName =>
            {
                if (string.IsNullOrEmpty(propertyName))
                {
                    // Advance the reader's position to be able to report the line and column for the property value.
                    jsonReader.ReadNextToken();

                    throw FileFormatException.Create(
                        "Unable to resolve dependency ''.",
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }

                // Support
                // "dependencies" : {
                //    "Name" : "1.0"
                // }

                if (jsonReader.ReadNextToken())
                {
                    int dependencyValueLine = jsonReader.LineNumber;
                    int dependencyValueColumn = jsonReader.LinePosition;
                    var versionLine = 0;
                    var versionColumn = 0;

                    var dependencyIncludeFlagsValue = LibraryIncludeFlags.All;
                    var dependencyExcludeFlagsValue = LibraryIncludeFlags.None;
                    var suppressParentFlagsValue = LibraryIncludeFlagUtils.DefaultSuppressParent;
                    ImmutableArray<NuGetLogCode> noWarn = [];

                    // This method handles both the dependencies and framework assembly sections.
                    // Framework references should be limited to references.
                    // Dependencies should allow everything but framework references.
                    LibraryDependencyTarget targetFlagsValue = isGacOrFrameworkReference
                        ? LibraryDependencyTarget.Reference
                        : LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference;

                    var autoReferenced = false;
                    var generatePathProperty = false;
                    var versionCentrallyManaged = false;
                    string aliases = null;
                    string dependencyVersionValue = null;
                    VersionRange versionOverride = null;

                    if (jsonReader.TokenType == JsonToken.String)
                    {
                        dependencyVersionValue = (string)jsonReader.Value;
                    }
                    else if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        jsonReader.ReadProperties(dependenciesPropertyName =>
                        {
                            IEnumerable<string> values = null;

                            switch (dependenciesPropertyName)
                            {
                                case "autoReferenced":
                                    autoReferenced = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpecPath);
                                    break;

                                case "exclude":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "generatePathProperty":
                                    generatePathProperty = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpecPath);
                                    break;

                                case "include":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "noWarn":
                                    noWarn = ReadNuGetLogCodesList(jsonReader);
                                    break;

                                case "suppressParent":
                                    values = jsonReader.ReadDelimitedString();
                                    suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "target":
                                    targetFlagsValue = ReadTarget(jsonReader, packageSpecPath, targetFlagsValue);
                                    break;

                                case "version":
                                    if (jsonReader.ReadNextToken())
                                    {
                                        versionLine = jsonReader.LineNumber;
                                        versionColumn = jsonReader.LinePosition;

                                        dependencyVersionValue = (string)jsonReader.Value;
                                    }
                                    break;
                                case "versionOverride":
                                    if (jsonReader.ReadNextToken())
                                    {
                                        try
                                        {
                                            versionOverride = VersionRange.Parse((string)jsonReader.Value);
                                        }
                                        catch (Exception ex)
                                        {
                                            throw FileFormatException.Create(
                                                ex,
                                                jsonReader.LineNumber,
                                                jsonReader.LinePosition,
                                                packageSpecPath);
                                        }
                                    }
                                    break;
                                case "versionCentrallyManaged":
                                    versionCentrallyManaged = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpecPath);
                                    break;

                                case "aliases":
                                    aliases = jsonReader.ReadAsString();
                                    break;
                            }
                        });
                    }

                    VersionRange dependencyVersionRange = null;

                    if (!string.IsNullOrEmpty(dependencyVersionValue))
                    {
                        try
                        {
                            dependencyVersionRange = VersionRange.Parse(dependencyVersionValue);
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(
                                ex,
                                versionLine,
                                versionColumn,
                                packageSpecPath);
                        }
                    }

                    // Projects and References may have empty version ranges, Packages may not
                    if (dependencyVersionRange == null)
                    {
                        if ((targetFlagsValue & LibraryDependencyTarget.Package) == LibraryDependencyTarget.Package)
                        {
                            throw FileFormatException.Create(
                                new ArgumentException(Strings.MissingVersionOnDependency),
                                dependencyValueLine,
                                dependencyValueColumn,
                                packageSpecPath);
                        }
                        else
                        {
                            // Projects and references with no version property allow all versions
                            dependencyVersionRange = VersionRange.All;
                        }
                    }

                    // the dependency flags are: Include flags - Exclude flags
                    var includeFlags = dependencyIncludeFlagsValue & ~dependencyExcludeFlagsValue;
                    var libraryDependency = new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = propertyName,
                            TypeConstraint = targetFlagsValue,
                            VersionRange = dependencyVersionRange
                        },
                        IncludeType = includeFlags,
                        SuppressParent = suppressParentFlagsValue,
                        AutoReferenced = autoReferenced,
                        GeneratePathProperty = generatePathProperty,
                        VersionCentrallyManaged = versionCentrallyManaged,
                        Aliases = aliases,
                        // The ReferenceType is not persisted to the assets file
                        // Default to LibraryDependencyReferenceType.Direct on Read
                        ReferenceType = LibraryDependencyReferenceType.Direct,
                        VersionOverride = versionOverride,
                        NoWarn = noWarn
                    };

                    results.Add(libraryDependency);
                }
            });
        }

        [Obsolete]
        internal static void ReadCentralTransitiveDependencyGroup(
            JsonTextReader jsonReader,
            IList<LibraryDependency> results,
            string packageSpecPath)
        {
            jsonReader.ReadObject(propertyName =>
            {
                if (string.IsNullOrEmpty(propertyName))
                {
                    // Advance the reader's position to be able to report the line and column for the property value.
                    jsonReader.ReadNextToken();

                    throw FileFormatException.Create(
                        "Unable to resolve dependency ''.",
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }

                if (jsonReader.ReadNextToken())
                {
                    int dependencyValueLine = jsonReader.LineNumber;
                    int dependencyValueColumn = jsonReader.LinePosition;
                    var versionLine = 0;
                    var versionColumn = 0;

                    var dependencyIncludeFlagsValue = LibraryIncludeFlags.All;
                    var dependencyExcludeFlagsValue = LibraryIncludeFlags.None;
                    var suppressParentFlagsValue = LibraryIncludeFlagUtils.DefaultSuppressParent;
                    string dependencyVersionValue = null;

                    if (jsonReader.TokenType == JsonToken.String)
                    {
                        dependencyVersionValue = (string)jsonReader.Value;
                    }
                    else if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        jsonReader.ReadProperties(dependenciesPropertyName =>
                        {
                            IEnumerable<string> values = null;

                            switch (dependenciesPropertyName)
                            {
                                case "exclude":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "include":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "suppressParent":
                                    values = jsonReader.ReadDelimitedString();
                                    suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "version":
                                    if (jsonReader.ReadNextToken())
                                    {
                                        versionLine = jsonReader.LineNumber;
                                        versionColumn = jsonReader.LinePosition;
                                        dependencyVersionValue = (string)jsonReader.Value;
                                    }
                                    break;

                                default:
                                    break;
                            }
                        });
                    }

                    VersionRange dependencyVersionRange = null;

                    if (!string.IsNullOrEmpty(dependencyVersionValue))
                    {
                        try
                        {
                            dependencyVersionRange = VersionRange.Parse(dependencyVersionValue);
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(
                                ex,
                                versionLine,
                                versionColumn,
                                packageSpecPath);
                        }
                    }

                    if (dependencyVersionRange == null)
                    {
                        throw FileFormatException.Create(
                                new ArgumentException(Strings.MissingVersionOnDependency),
                                dependencyValueLine,
                                dependencyValueColumn,
                                packageSpecPath);
                    }

                    // the dependency flags are: Include flags - Exclude flags
                    var includeFlags = dependencyIncludeFlagsValue & ~dependencyExcludeFlagsValue;
                    var libraryDependency = new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = propertyName,
                            TypeConstraint = LibraryDependencyTarget.Package,
                            VersionRange = dependencyVersionRange
                        },

                        IncludeType = includeFlags,
                        SuppressParent = suppressParentFlagsValue,
                        VersionCentrallyManaged = true,
                        ReferenceType = LibraryDependencyReferenceType.Transitive
                    };

                    results.Add(libraryDependency);
                }
            });
        }

        [Obsolete]
        private static void ReadDownloadDependencies(
            JsonTextReader jsonReader,
            IList<DownloadDependency> downloadDependencies,
            string packageSpecPath)
        {
            var seenIds = new HashSet<string>();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.StartArray)
            {
                do
                {
                    string name = null;
                    string versionValue = null;
                    var isNameDefined = false;
                    var isVersionDefined = false;
                    int line = jsonReader.LineNumber;
                    int column = jsonReader.LinePosition;
                    int versionLine = 0;
                    int versionColumn = 0;

                    jsonReader.ReadObject(propertyName =>
                    {
                        switch (propertyName)
                        {
                            case "name":
                                isNameDefined = true;
                                name = jsonReader.ReadNextTokenAsString();
                                break;

                            case "version":
                                isVersionDefined = true;
                                versionValue = jsonReader.ReadNextTokenAsString();
                                versionLine = jsonReader.LineNumber;
                                versionColumn = jsonReader.LinePosition;
                                break;
                        }
                    }, out line, out column);

                    if (jsonReader.TokenType == JsonToken.EndArray)
                    {
                        break;
                    }

                    if (!isNameDefined)
                    {
                        throw FileFormatException.Create(
                            "Unable to resolve downloadDependency ''.",
                            line,
                            column,
                            packageSpecPath);
                    }

                    if (!seenIds.Add(name))
                    {
                        // package ID already seen, only use first definition.
                        continue;
                    }

                    if (string.IsNullOrEmpty(versionValue))
                    {
                        throw FileFormatException.Create(
                            "The version cannot be null or empty",
                            isVersionDefined ? versionLine : line,
                            isVersionDefined ? versionColumn : column,
                            packageSpecPath);
                    }

                    string[] versions = versionValue.Split(VersionSeparators, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string singleVersionValue in versions)
                    {
                        try
                        {
                            VersionRange version = VersionRange.Parse(singleVersionValue);

                            downloadDependencies.Add(new DownloadDependency(name, version));
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(
                                ex,
                                isVersionDefined ? versionLine : line,
                                isVersionDefined ? versionColumn : column,
                                packageSpecPath);
                        }
                    }
                } while (jsonReader.TokenType == JsonToken.EndObject);
            }
        }

        [Obsolete]
        private static IReadOnlyList<string> ReadEnumerableOfString(JsonTextReader jsonReader)
        {
            string value = jsonReader.ReadNextTokenAsString();

            return value.Split(DelimitedStringSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        [Obsolete]
        private static void ReadFrameworkReferences(
            JsonTextReader jsonReader,
            ISet<FrameworkDependency> frameworkReferences,
            string packageSpecPath)
        {
            jsonReader.ReadObject(frameworkName =>
            {
                if (string.IsNullOrEmpty(frameworkName))
                {
                    // Advance the reader's position to be able to report the line and column for the property value.
                    jsonReader.ReadNextToken();

                    throw FileFormatException.Create(
                        "Unable to resolve frameworkReference.",
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }

                var privateAssets = FrameworkDependencyFlagsUtils.Default;

                jsonReader.ReadObject(propertyName =>
                {
                    if (propertyName == "privateAssets")
                    {
                        IEnumerable<string> strings = ReadEnumerableOfString(jsonReader);

                        privateAssets = FrameworkDependencyFlagsUtils.GetFlags(strings);
                    }
                });

                frameworkReferences.Add(new FrameworkDependency(frameworkName, privateAssets));
            });
        }

        [Obsolete]
        private static void ReadFrameworks(JsonTextReader jsonReader, PackageSpec packageSpec)
        {
            jsonReader.ReadObject(_ =>
            {
                var frameworkLine = 0;
                var frameworkColumn = 0;

                try
                {
                    ReadTargetFrameworks(packageSpec, jsonReader, out frameworkLine, out frameworkColumn);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, frameworkLine, frameworkColumn, packageSpec.FilePath);
                }
            });
        }

        [Obsolete]
        private static void ReadImports(PackageSpec packageSpec, JsonTextReader jsonReader, List<NuGetFramework> importFrameworks)
        {
            int lineNumber = jsonReader.LineNumber;
            int linePosition = jsonReader.LinePosition;

            IReadOnlyList<string> imports = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();

            if (imports != null && imports.Count > 0)
            {
                foreach (string import in imports.Where(element => !string.IsNullOrEmpty(element)))
                {
                    NuGetFramework framework = NuGetFramework.Parse(import);

                    if (!framework.IsSpecificFramework)
                    {
                        throw FileFormatException.Create(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Log_InvalidImportFramework,
                                import,
                                packageSpec.FilePath),
                            lineNumber,
                            linePosition,
                            packageSpec.FilePath);
                    }

                    importFrameworks.Add(framework);
                }
            }
        }

        [Obsolete]
        private static void ReadMSBuildMetadata(JsonTextReader jsonReader, PackageSpec packageSpec, IEnvironmentVariableReader environmentVariableReader)
        {
            var centralPackageVersionsManagementEnabled = false;
            var centralPackageFloatingVersionsEnabled = false;
            var centralPackageVersionOverrideDisabled = false;
            var CentralPackageTransitivePinningEnabled = false;
            List<string> configFilePaths = null;
            var crossTargeting = false;
            List<string> fallbackFolders = null;
            List<ProjectRestoreMetadataFile> files = null;
            var legacyPackagesDirectory = false;
            ProjectRestoreMetadata msbuildMetadata = null;
            List<string> originalTargetFrameworks = null;
            string outputPath = null;
            string packagesConfigPath = null;
            string packagesPath = null;
            string projectJsonPath = null;
            string projectName = null;
            string projectPath = null;
            ProjectStyle? projectStyle = null;
            string projectUniqueName = null;
            RestoreLockProperties restoreLockProperties = null;
            var skipContentFileWrite = false;
            List<PackageSource> sources = null;
            List<ProjectRestoreMetadataFrameworkInfo> targetFrameworks = null;
            var validateRuntimeAssets = false;
            WarningProperties warningProperties = null;
            RestoreAuditProperties auditProperties = null;
            bool useMacros = MSBuildStringUtility.IsTrue(environmentVariableReader.GetEnvironmentVariable(MacroStringsUtility.NUGET_ENABLE_EXPERIMENTAL_MACROS));
            var userSettingsDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
            bool usingMicrosoftNetSdk = true;
            bool restoreUseLegacyDependencyResolver = false;
            NuGetVersion sdkAnalysisLevel = null;

            jsonReader.ReadObject(propertyName =>
            {
                switch (propertyName)
                {
                    case "centralPackageVersionsManagementEnabled":
                        centralPackageVersionsManagementEnabled = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "centralPackageFloatingVersionsEnabled":
                        centralPackageFloatingVersionsEnabled = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "centralPackageVersionOverrideDisabled":
                        centralPackageVersionOverrideDisabled = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "CentralPackageTransitivePinningEnabled":
                        CentralPackageTransitivePinningEnabled = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "configFilePaths":
                        configFilePaths = jsonReader.ReadStringArrayAsList();
                        ExtractMacros(configFilePaths, userSettingsDirectory, useMacros);
                        break;

                    case "crossTargeting":
                        crossTargeting = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "fallbackFolders":
                        fallbackFolders = jsonReader.ReadStringArrayAsList();
                        ExtractMacros(fallbackFolders, userSettingsDirectory, useMacros);
                        break;

                    case "files":
                        jsonReader.ReadObject(filePropertyName =>
                        {
                            files = files ?? new List<ProjectRestoreMetadataFile>();

                            files.Add(new ProjectRestoreMetadataFile(filePropertyName, jsonReader.ReadNextTokenAsString()));
                        });
                        break;

                    case "frameworks":
                        targetFrameworks = ReadTargetFrameworks(jsonReader);
                        break;

                    case "legacyPackagesDirectory":
                        legacyPackagesDirectory = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "originalTargetFrameworks":
                        originalTargetFrameworks = jsonReader.ReadStringArrayAsList();
                        break;

                    case "outputPath":
                        outputPath = jsonReader.ReadNextTokenAsString();
                        break;

                    case "packagesConfigPath":
                        packagesConfigPath = jsonReader.ReadNextTokenAsString();
                        break;

                    case "packagesPath":
                        packagesPath = ExtractMacro(jsonReader.ReadNextTokenAsString(), userSettingsDirectory, useMacros);
                        break;

                    case "projectJsonPath":
                        projectJsonPath = jsonReader.ReadNextTokenAsString();
                        break;

                    case "projectName":
                        projectName = jsonReader.ReadNextTokenAsString();
                        break;

                    case "projectPath":
                        projectPath = jsonReader.ReadNextTokenAsString();
                        break;

                    case "projectStyle":
                        string projectStyleString = jsonReader.ReadNextTokenAsString();

                        if (!string.IsNullOrEmpty(projectStyleString)
                            && Enum.TryParse<ProjectStyle>(projectStyleString, ignoreCase: true, result: out ProjectStyle projectStyleValue))
                        {
                            projectStyle = projectStyleValue;
                        }
                        break;

                    case "projectUniqueName":
                        projectUniqueName = ExtractMacro(jsonReader.ReadNextTokenAsString(), userSettingsDirectory, useMacros);
                        break;

                    case "restoreLockProperties":
                        string nuGetLockFilePath = null;
                        var restoreLockedMode = false;
                        string restorePackagesWithLockFile = null;

                        jsonReader.ReadObject(restoreLockPropertiesPropertyName =>
                        {
                            switch (restoreLockPropertiesPropertyName)
                            {
                                case "nuGetLockFilePath":
                                    nuGetLockFilePath = jsonReader.ReadNextTokenAsString();
                                    break;

                                case "restoreLockedMode":
                                    restoreLockedMode = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                                    break;

                                case "restorePackagesWithLockFile":
                                    restorePackagesWithLockFile = jsonReader.ReadNextTokenAsString();
                                    break;
                            }
                        });

                        restoreLockProperties = new RestoreLockProperties(restorePackagesWithLockFile, nuGetLockFilePath, restoreLockedMode);
                        break;

                    case "restoreAuditProperties":
                        string enableAudit = null, auditLevel = null, auditMode = null;
                        HashSet<string> suppressedAdvisories = null;

                        jsonReader.ReadObject(auditPropertyName =>
                        {
                            switch (auditPropertyName)
                            {
                                case "enableAudit":
                                    enableAudit = jsonReader.ReadNextTokenAsString();
                                    break;

                                case "auditLevel":
                                    auditLevel = jsonReader.ReadNextTokenAsString();
                                    break;

                                case "auditMode":
                                    auditMode = jsonReader.ReadNextTokenAsString();
                                    break;

                                case "suppressedAdvisories":
                                    suppressedAdvisories = ReadSuppressedAdvisories(jsonReader);
                                    break;
                            }
                        });
                        auditProperties = new RestoreAuditProperties()
                        {
                            EnableAudit = enableAudit,
                            AuditLevel = auditLevel,
                            AuditMode = auditMode,
                            SuppressedAdvisories = suppressedAdvisories
                        };
                        break;

                    case "skipContentFileWrite":
                        skipContentFileWrite = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "sources":
                        jsonReader.ReadObject(sourcePropertyName =>
                        {
                            sources = sources ?? new List<PackageSource>();

                            sources.Add(new PackageSource(sourcePropertyName));
                        });
                        break;

                    case "validateRuntimeAssets":
                        validateRuntimeAssets = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "warningProperties":
                        var allWarningsAsErrors = false;
                        var noWarn = new HashSet<NuGetLogCode>();
                        var warnAsError = new HashSet<NuGetLogCode>();
                        var warningsNotAsErrors = new HashSet<NuGetLogCode>();

                        jsonReader.ReadObject(warningPropertiesPropertyName =>
                        {
                            switch (warningPropertiesPropertyName)
                            {
                                case "allWarningsAsErrors":
                                    allWarningsAsErrors = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                                    break;

                                case "noWarn":
                                    ReadNuGetLogCodes(jsonReader, noWarn);
                                    break;

                                case "warnAsError":
                                    ReadNuGetLogCodes(jsonReader, warnAsError);
                                    break;

                                case "warnNotAsError":
                                    ReadNuGetLogCodes(jsonReader, warningsNotAsErrors);
                                    break;
                            }
                        });

                        warningProperties = new WarningProperties(warnAsError, noWarn, allWarningsAsErrors, warningsNotAsErrors);
                        break;

                    case "SdkAnalysisLevel":
                        string skdAnalysisLevelString = jsonReader.ReadNextTokenAsString();

                        if (!string.IsNullOrEmpty(skdAnalysisLevelString))
                        {
                            try
                            {
                                sdkAnalysisLevel = new NuGetVersion(skdAnalysisLevelString);
                            }
                            catch (ArgumentException ex)
                            {
                                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Invalid_AttributeValue, "SdkAnalysisLevel", skdAnalysisLevelString, "9.0.100"), ex);
                            }
                        }
                        break;

                    case "UsingMicrosoftNETSdk":
                        try
                        {
                            usingMicrosoftNetSdk = jsonReader.ReadAsBoolean() ?? usingMicrosoftNetSdk;
                        }
                        catch (ArgumentException ex)
                        {
                            throw new ArgumentException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    Strings.Invalid_AttributeValue,
                                    "UsingMicrosoftNETSdk",
                                    jsonReader.ReadNextTokenAsString(),
                                    "false"), ex);
                        }
                        break;

                    case "restoreUseLegacyDependencyResolver":
                        restoreUseLegacyDependencyResolver = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;
                }
            });

            if (projectStyle == ProjectStyle.PackagesConfig)
            {
                msbuildMetadata = new PackagesConfigProjectRestoreMetadata()
                {
                    PackagesConfigPath = packagesConfigPath
                };
            }
            else
            {
                msbuildMetadata = new ProjectRestoreMetadata();
            }

            msbuildMetadata.CentralPackageVersionsEnabled = centralPackageVersionsManagementEnabled;
            msbuildMetadata.CentralPackageFloatingVersionsEnabled = centralPackageFloatingVersionsEnabled;
            msbuildMetadata.CentralPackageVersionOverrideDisabled = centralPackageVersionOverrideDisabled;
            msbuildMetadata.CentralPackageTransitivePinningEnabled = CentralPackageTransitivePinningEnabled;
            msbuildMetadata.RestoreAuditProperties = auditProperties;
            msbuildMetadata.UsingMicrosoftNETSdk = usingMicrosoftNetSdk;
            msbuildMetadata.SdkAnalysisLevel = sdkAnalysisLevel;
            msbuildMetadata.UseLegacyDependencyResolver = restoreUseLegacyDependencyResolver;

            if (configFilePaths != null)
            {
                msbuildMetadata.ConfigFilePaths = configFilePaths;
            }

            msbuildMetadata.CrossTargeting = crossTargeting;

            if (fallbackFolders != null)
            {
                msbuildMetadata.FallbackFolders = fallbackFolders;
            }

            if (files != null)
            {
                msbuildMetadata.Files = files;
            }

            msbuildMetadata.LegacyPackagesDirectory = legacyPackagesDirectory;

            if (originalTargetFrameworks != null)
            {
                msbuildMetadata.OriginalTargetFrameworks = originalTargetFrameworks;
            }

            msbuildMetadata.OutputPath = outputPath;
            msbuildMetadata.PackagesPath = packagesPath;
            msbuildMetadata.ProjectJsonPath = projectJsonPath;
            msbuildMetadata.ProjectName = projectName;
            msbuildMetadata.ProjectPath = projectPath;

            if (projectStyle.HasValue)
            {
                msbuildMetadata.ProjectStyle = projectStyle.Value;
            }

            msbuildMetadata.ProjectUniqueName = projectUniqueName;

            if (restoreLockProperties != null)
            {
                msbuildMetadata.RestoreLockProperties = restoreLockProperties;
            }

            msbuildMetadata.SkipContentFileWrite = skipContentFileWrite;

            if (sources != null)
            {
                msbuildMetadata.Sources = sources;
            }

            if (targetFrameworks != null)
            {
                msbuildMetadata.TargetFrameworks = targetFrameworks;
            }

            msbuildMetadata.ValidateRuntimeAssets = validateRuntimeAssets;

            if (warningProperties != null)
            {
                msbuildMetadata.ProjectWideWarningProperties = warningProperties;
            }

            packageSpec.RestoreMetadata = msbuildMetadata;
        }

        private static string ExtractMacro(string value, string userSettingsDirectory, bool useMacros)
        {
            if (useMacros)
            {
                return MacroStringsUtility.ExtractMacro(value, userSettingsDirectory, MacroStringsUtility.UserMacro);
            }
            return value;
        }

        private static void ExtractMacros(List<string> paths, string userSettingsDirectory, bool useMacros)
        {
            if (useMacros)
            {
                MacroStringsUtility.ExtractMacros(paths, userSettingsDirectory, MacroStringsUtility.UserMacro);
            }
        }

        private static bool ReadNextTokenAsBoolOrFalse(JsonTextReader jsonReader, string filePath)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.Boolean)
            {
                try
                {
                    return (bool)jsonReader.Value;
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, jsonReader.LineNumber, jsonReader.LinePosition, filePath);
                }
            }

            return false;
        }

        [Obsolete]
        private static void ReadNuGetLogCodes(JsonTextReader jsonReader, HashSet<NuGetLogCode> hashCodes)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.StartArray)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType == JsonToken.String && Enum.TryParse((string)jsonReader.Value, out NuGetLogCode code))
                    {
                        hashCodes.Add(code);
                    }
                }
            }
        }

        private static ImmutableArray<NuGetLogCode> ReadNuGetLogCodesList(JsonTextReader jsonReader)
        {
            NuGetLogCode[] items = null;
            var index = 0;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.StartArray)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType == JsonToken.String && Enum.TryParse((string)jsonReader.Value, out NuGetLogCode code))
                    {
                        if (items == null)
                        {
                            items = ArrayPool<NuGetLogCode>.Shared.Rent(16);
                        }
                        else if (items.Length == index)
                        {
                            var oldItems = items;

                            items = ArrayPool<NuGetLogCode>.Shared.Rent(items.Length * 2);
                            oldItems.CopyTo(items, index: 0);

                            ArrayPool<NuGetLogCode>.Shared.Return(oldItems);
                        }

                        items[index++] = code;
                    }
                }
            }

            if (items == null)
            {
                return [];
            }

            var retVal = items.AsSpan(0, index).ToImmutableArray();
            ArrayPool<NuGetLogCode>.Shared.Return(items);

            return retVal;
        }

        [Obsolete]
        static RuntimeDependencySet ReadRuntimeDependencySet(JsonTextReader jsonReader, string dependencySetName)
        {
            List<RuntimePackageDependency> dependencies = null;

            jsonReader.ReadObject(propertyName =>
            {
                dependencies ??= new List<RuntimePackageDependency>();

                var dependency = new RuntimePackageDependency(propertyName, VersionRange.Parse(jsonReader.ReadNextTokenAsString()));

                dependencies.Add(dependency);
            });

            return new RuntimeDependencySet(
                dependencySetName,
                dependencies);
        }

        [Obsolete]
        private static RuntimeDescription ReadRuntimeDescription(JsonTextReader jsonReader, string runtimeName)
        {
            List<string> inheritedRuntimes = null;
            List<RuntimeDependencySet> additionalDependencies = null;

            jsonReader.ReadObject(propertyName =>
            {
                if (propertyName == "#import")
                {
                    inheritedRuntimes = jsonReader.ReadStringArrayAsList();
                }
                else
                {
                    additionalDependencies ??= new List<RuntimeDependencySet>();

                    RuntimeDependencySet dependency = ReadRuntimeDependencySet(jsonReader, propertyName);

                    additionalDependencies.Add(dependency);
                }
            });

            return new RuntimeDescription(
                runtimeName,
                inheritedRuntimes,
                additionalDependencies);
        }

        [Obsolete]
        private static List<RuntimeDescription> ReadRuntimes(JsonTextReader jsonReader)
        {
            var runtimeDescriptions = new List<RuntimeDescription>();

            jsonReader.ReadObject(propertyName =>
            {
                RuntimeDescription runtimeDescription = ReadRuntimeDescription(jsonReader, propertyName);

                runtimeDescriptions.Add(runtimeDescription);
            });

            return runtimeDescriptions;
        }

        [Obsolete]
        private static List<CompatibilityProfile> ReadSupports(JsonTextReader jsonReader)
        {
            var compatibilityProfiles = new List<CompatibilityProfile>();

            jsonReader.ReadObject(propertyName =>
            {
                CompatibilityProfile compatibilityProfile = ReadCompatibilityProfile(jsonReader, propertyName);

                compatibilityProfiles.Add(compatibilityProfile);
            });

            return compatibilityProfiles;
        }

        [Obsolete]
        private static LibraryDependencyTarget ReadTarget(
            JsonTextReader jsonReader,
            string packageSpecPath,
            LibraryDependencyTarget targetFlagsValue)
        {
            if (jsonReader.ReadNextToken())
            {
                var targetString = (string)jsonReader.Value;

                targetFlagsValue = LibraryDependencyTargetUtils.Parse(targetString);

                // Verify that the value specified is package, project, or external project
                if (!ValidateDependencyTarget(targetFlagsValue))
                {
                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidDependencyTarget,
                        targetString);

                    throw FileFormatException.Create(
                        message,
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }
            }

            return targetFlagsValue;
        }

        [Obsolete]
        private static List<ProjectRestoreMetadataFrameworkInfo> ReadTargetFrameworks(JsonTextReader jsonReader)
        {
            var targetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>();

            jsonReader.ReadObject(frameworkPropertyName =>
            {
                NuGetFramework framework = NuGetFramework.Parse(frameworkPropertyName);
                var frameworkGroup = new ProjectRestoreMetadataFrameworkInfo(framework);

                jsonReader.ReadObject(propertyName =>
                {
                    if (propertyName == "projectReferences")
                    {
                        jsonReader.ReadObject(projectReferencePropertyName =>
                        {
                            string excludeAssets = null;
                            string includeAssets = null;
                            string privateAssets = null;
                            string projectReferenceProjectPath = null;

                            jsonReader.ReadObject(projectReferenceObjectPropertyName =>
                            {
                                switch (projectReferenceObjectPropertyName)
                                {
                                    case "excludeAssets":
                                        excludeAssets = jsonReader.ReadNextTokenAsString();
                                        break;

                                    case "includeAssets":
                                        includeAssets = jsonReader.ReadNextTokenAsString();
                                        break;

                                    case "privateAssets":
                                        privateAssets = jsonReader.ReadNextTokenAsString();
                                        break;

                                    case "projectPath":
                                        projectReferenceProjectPath = jsonReader.ReadNextTokenAsString();
                                        break;
                                }
                            });

                            frameworkGroup.ProjectReferences.Add(new ProjectRestoreReference()
                            {
                                ProjectUniqueName = projectReferencePropertyName,
                                ProjectPath = projectReferenceProjectPath,

                                IncludeAssets = LibraryIncludeFlagUtils.GetFlags(
                                    flags: includeAssets,
                                    defaultFlags: LibraryIncludeFlags.All),

                                ExcludeAssets = LibraryIncludeFlagUtils.GetFlags(
                                    flags: excludeAssets,
                                    defaultFlags: LibraryIncludeFlags.None),

                                PrivateAssets = LibraryIncludeFlagUtils.GetFlags(
                                    flags: privateAssets,
                                    defaultFlags: LibraryIncludeFlagUtils.DefaultSuppressParent),
                            });
                        });
                    }
                    else if (propertyName == "targetAlias")
                    {
                        frameworkGroup.TargetAlias = jsonReader.ReadNextTokenAsString();
                    }
                });

                targetFrameworks.Add(frameworkGroup);
            });

            return targetFrameworks;
        }

        [Obsolete]
        private static void ReadTargetFrameworks(PackageSpec packageSpec, JsonTextReader jsonReader, out int frameworkLine, out int frameworkColumn)
        {
            frameworkLine = 0;
            frameworkColumn = 0;

            NuGetFramework frameworkName = NuGetFramework.Parse((string)jsonReader.Value);

            bool assetTargetFallback = false;
            Dictionary<string, CentralPackageVersion> centralPackageVersions = null;
            List<LibraryDependency> dependencies = null;
            List<DownloadDependency> downloadDependencies = null;
            HashSet<FrameworkDependency> frameworkReferences = null;
            List<NuGetFramework> imports = null;
            string runtimeIdentifierGraphPath = null;
            string targetAlias = string.Empty;
            bool warn = false;
            Dictionary<string, PrunePackageReference> packagesToPrune = null;

            NuGetFramework secondaryFramework = default;

            jsonReader.ReadObject(propertyName =>
            {
                switch (propertyName)
                {
                    case "assetTargetFallback":
                        assetTargetFallback = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "secondaryFramework":
                        var secondaryFrameworkString = jsonReader.ReadAsString();
                        if (!string.IsNullOrEmpty(secondaryFrameworkString))
                        {
                            secondaryFramework = NuGetFramework.Parse(secondaryFrameworkString);
                        }
                        break;

                    case "centralPackageVersions":
                        centralPackageVersions ??= new Dictionary<string, CentralPackageVersion>();
                        ReadCentralPackageVersions(
                            jsonReader,
                            centralPackageVersions,
                            packageSpec.FilePath);
                        break;

                    case "dependencies":
                        dependencies ??= new List<LibraryDependency>();
                        ReadDependencies(
                            jsonReader,
                            dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: false);
                        break;

                    case "downloadDependencies":
                        downloadDependencies ??= new List<DownloadDependency>();
                        ReadDownloadDependencies(
                            jsonReader,
                            downloadDependencies,
                            packageSpec.FilePath);
                        break;

                    case "frameworkAssemblies":
                        dependencies ??= new List<LibraryDependency>();
                        ReadDependencies(
                            jsonReader,
                            dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: true);
                        break;

                    case "frameworkReferences":
                        frameworkReferences ??= new HashSet<FrameworkDependency>();
                        ReadFrameworkReferences(
                            jsonReader,
                            frameworkReferences,
                            packageSpec.FilePath);
                        break;

                    case "imports":
                        imports ??= new List<NuGetFramework>();
                        ReadImports(packageSpec, jsonReader, imports);
                        break;

                    case "packagesToPrune":
                        packagesToPrune ??= new Dictionary<string, PrunePackageReference>(StringComparer.OrdinalIgnoreCase);
                        ReadPackagesToPrune(
                            jsonReader,
                            packagesToPrune,
                            packageSpec.FilePath);
                        break;

                    case "runtimeIdentifierGraphPath":
                        runtimeIdentifierGraphPath = jsonReader.ReadNextTokenAsString();
                        break;

                    case "targetAlias":
                        targetAlias = jsonReader.ReadNextTokenAsString();
                        break;

                    case "warn":
                        warn = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;
                }
            }, out frameworkLine, out frameworkColumn);

            var targetFrameworkInformation = new TargetFrameworkInformation()
            {
                AssetTargetFallback = assetTargetFallback,
                CentralPackageVersions = centralPackageVersions,
                Dependencies = dependencies != null ? dependencies.ToImmutableArray() : [],
                DownloadDependencies = downloadDependencies != null ? downloadDependencies.ToImmutableArray() : [],
                FrameworkReferences = frameworkReferences,
                Imports = imports != null ? imports.ToImmutableArray() : [],
                RuntimeIdentifierGraphPath = runtimeIdentifierGraphPath,
                TargetAlias = targetAlias,
                Warn = warn,
                PackagesToPrune = packagesToPrune,
            };

            AddTargetFramework(packageSpec, frameworkName, secondaryFramework, targetFrameworkInformation);
        }

        [Obsolete]
        private static void AddTargetFramework(PackageSpec packageSpec, NuGetFramework frameworkName, NuGetFramework secondaryFramework, TargetFrameworkInformation targetFrameworkInformation)
        {
            NuGetFramework updatedFramework = frameworkName;

            if (targetFrameworkInformation.Imports.Length > 0)
            {
                NuGetFramework[] imports = targetFrameworkInformation.Imports.ToArray();

                if (targetFrameworkInformation.AssetTargetFallback)
                {
                    updatedFramework = new AssetTargetFallbackFramework(GetDualCompatibilityFrameworkIfNeeded(frameworkName, secondaryFramework), imports);
                }
                else
                {
                    updatedFramework = new FallbackFramework(GetDualCompatibilityFrameworkIfNeeded(frameworkName, secondaryFramework), imports);
                }
            }
            else
            {
                updatedFramework = GetDualCompatibilityFrameworkIfNeeded(frameworkName, secondaryFramework);
            }

            targetFrameworkInformation = new TargetFrameworkInformation(targetFrameworkInformation) { FrameworkName = updatedFramework };

            packageSpec.TargetFrameworks.Add(targetFrameworkInformation);
        }


        [Obsolete]
        private static NuGetFramework GetDualCompatibilityFrameworkIfNeeded(NuGetFramework frameworkName, NuGetFramework secondaryFramework)
        {
            if (secondaryFramework != default)
            {
                return new DualCompatibilityFramework(frameworkName, secondaryFramework);
            }

            return frameworkName;
        }

        [Obsolete]
        private static bool ValidateDependencyTarget(LibraryDependencyTarget targetValue)
        {
            var isValid = false;

            switch (targetValue)
            {
                case LibraryDependencyTarget.Package:
                case LibraryDependencyTarget.Project:
                case LibraryDependencyTarget.ExternalProject:
                    isValid = true;
                    break;
            }

            return isValid;
        }

        private static HashSet<string> ReadSuppressedAdvisories(JsonTextReader jsonReader)
        {
            HashSet<string> suppressedAdvisories = null;

            jsonReader.ReadObject(advisoryUrl =>
            {
                suppressedAdvisories ??= new HashSet<string>();
                suppressedAdvisories.Add(advisoryUrl);
            });

            return suppressedAdvisories;
        }
    }
}
