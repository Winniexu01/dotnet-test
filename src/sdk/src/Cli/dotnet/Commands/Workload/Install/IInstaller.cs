// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal interface IInstaller : IWorkloadManifestInstaller
{
    int ExitCode { get; }

    WorkloadSet GetWorkloadSetContents(string workloadVersion);

    void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null);

    void RepairWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null);

    void GarbageCollect(Func<string, IWorkloadResolver> getResolverForWorkloadSet, DirectoryPath? offlineCache = null, bool cleanAllPacks = false);

    WorkloadSet InstallWorkloadSet(ITransactionContext context, string workloadSetVersion, DirectoryPath? offlineCache = null);

    void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null);

    IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository();

    IEnumerable<WorkloadDownload> GetDownloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, bool includeInstalledItems);

    void AdjustWorkloadSetInInstallState(SdkFeatureBand sdkFeatureBand, string workloadVersion);

    void WriteWorkloadHistoryRecord(WorkloadHistoryRecord workloadHistoryRecord, string sdkFeatureBand);

    IEnumerable<WorkloadHistoryRecord> GetWorkloadHistoryRecords(string sdkFeatureBand);

    /// <summary>
    /// Replace the workload resolver used by this installer. Typically used to call <see cref="GetDownloads(IEnumerable{WorkloadId}, SdkFeatureBand, bool)"/>
    /// for a set of workload manifests that isn't currently installed
    /// </summary>
    /// <param name="workloadResolver">A new workload resolver to use</param>
    void ReplaceWorkloadResolver(IWorkloadResolver workloadResolver);

    void Shutdown();

    /// <summary>
    /// Delete the install state file at the specified path.
    /// </summary>
    /// <param name="sdkFeatureBand">The SDK feature band of the install state file.</param>
    void RemoveManifestsFromInstallState(SdkFeatureBand sdkFeatureBand);

    /// <summary>
    /// Writes the specified JSON contents to the install state file.
    /// </summary>
    /// <param name="sdkFeatureBand">The SDK feature band of the install state file.</param>
    /// <param name="manifestContents">The JSON contents describing the install state.</param>
    void SaveInstallStateManifestVersions(SdkFeatureBand sdkFeatureBand, Dictionary<string, string> manifestContents);

    void UpdateInstallMode(SdkFeatureBand sdkFeatureBand, bool? newMode);

    void RecordWorkloadSetInGlobalJson(SdkFeatureBand sdkFeatureBand, string globalJsonPath, string workloadSetVersion);
}

// Interface to pass to workload manifest updater
internal interface IWorkloadManifestInstaller
{
    PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand);

    //  Extract the contents of the manifest (IE what's in the data directory in the file-based NuGet package) to the targetPath
    Task ExtractManifestAsync(string nupkgPath, string targetPath);
}

public class WorkloadDownload(string id, string nuGetPackageId, string nuGetPackageVersion)
{
    /// <summary>
    /// The ID of the workload pack or manifest to be downloaded (not necessarily the same as the <see cref="NuGetPackageId"/>)
    /// </summary>
    public string Id { get; } = id;

    public string NuGetPackageId { get; } = nuGetPackageId;

    public string NuGetPackageVersion { get; } = nuGetPackageVersion;
}
