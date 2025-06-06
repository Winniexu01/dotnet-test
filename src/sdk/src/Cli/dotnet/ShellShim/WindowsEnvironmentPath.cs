﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.ShellShim;

internal class WindowsEnvironmentPath(string packageExecutablePath,
    string nonExpandedPackageExecutablePath,
    IEnvironmentProvider expandedEnvironmentReader,
    IWindowsRegistryEnvironmentPathEditor environmentPathEditor,
    IReporter reporter) : IEnvironmentPath
{
    private readonly IReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    private const string PathName = "PATH";
    private readonly string _expandedPackageExecutablePath = packageExecutablePath ?? throw new ArgumentNullException(nameof(packageExecutablePath));
    private readonly string _nonExpandedPackageExecutablePath = nonExpandedPackageExecutablePath ?? throw new ArgumentNullException(nameof(packageExecutablePath));

    /// <summary>
    /// This will read cached and expanded environment variable. We use this
    /// to check if the expanded tool shim path exists. Since this is ultimately how shell will invoke command
    /// </summary>
    private readonly IEnvironmentProvider _expandedEnvironmentReader = expandedEnvironmentReader ?? throw new ArgumentNullException(nameof(expandedEnvironmentReader));

    /// <summary>
    /// This will read from registry with non expanded environment like %USERPROFILE%\AppData\Local\Microsoft\WindowsApps
    /// when append tool shim PATH. Use to read and write to avoid edit existing PATH.
    /// </summary>
    private readonly IWindowsRegistryEnvironmentPathEditor _environmentPathEditor = environmentPathEditor ?? throw new ArgumentNullException(nameof(environmentPathEditor));

    public void AddPackageExecutablePathToUserPath()
    {
        if (PackageExecutablePathExists())
        {
            return;
        }

        var existingUserEnvPath =
            _environmentPathEditor.Get(SdkEnvironmentVariableTarget.CurrentUser);

        try
        {
            if (existingUserEnvPath == null)
            {
                _environmentPathEditor.Set(
                    _nonExpandedPackageExecutablePath,
                    SdkEnvironmentVariableTarget.CurrentUser);
            }
            else
            {
                if (existingUserEnvPath.EndsWith(';'))
                {
                    existingUserEnvPath = existingUserEnvPath.Substring(0, existingUserEnvPath.Length - 1);
                }

                _environmentPathEditor.Set(
                    $"{existingUserEnvPath};{_nonExpandedPackageExecutablePath}",
                    SdkEnvironmentVariableTarget.CurrentUser);
            }
        }
        catch (System.Security.SecurityException)
        {
            _reporter.WriteLine(
                string.Format(
                    CliStrings.FailedToSetToolsPathEnvironmentVariable,
                    _expandedPackageExecutablePath).Yellow());
        }
    }

    private bool PackageExecutablePathExists()
    {
        return PackageExecutablePathExistsForCurrentProcess() ||
               PackageExecutablePathWillExistForFutureNewProcess();
    }

    private bool PackageExecutablePathWillExistForFutureNewProcess()
    {
        return EnvironmentVariableConatinsPackageExecutablePath(
                   _expandedEnvironmentReader.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.User))
               || EnvironmentVariableConatinsPackageExecutablePath(
                   _expandedEnvironmentReader.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Machine));
    }

    private bool PackageExecutablePathExistsForCurrentProcess()
    {
        return EnvironmentVariableConatinsPackageExecutablePath(
            _expandedEnvironmentReader.GetEnvironmentVariable(PathName, EnvironmentVariableTarget.Process));
    }

    private bool EnvironmentVariableConatinsPackageExecutablePath(string environmentVariable)
    {
        if (environmentVariable == null)
        {
            return false;
        }

        return environmentVariable
            .Split(';')
            .Any(p => string.Equals(p, _expandedPackageExecutablePath, StringComparison.OrdinalIgnoreCase));
    }

    public void PrintAddPathInstructionIfPathDoesNotExist()
    {
        if (!PackageExecutablePathExistsForCurrentProcess() && PackageExecutablePathWillExistForFutureNewProcess())
        {
            _reporter.WriteLine(CliStrings.EnvironmentPathWindowsNeedReopen);
        }
        else if (!PackageExecutablePathWillExistForFutureNewProcess())
        {
            _reporter.WriteLine(
                string.Format(
                    CliStrings.EnvironmentPathWindowsManualInstructions,
                    _expandedPackageExecutablePath));
        }
    }
}
