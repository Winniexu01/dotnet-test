﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.CommandFactory;

public interface ICommandFactory
{
    ICommand Create(
        string commandName,
        IEnumerable<string> args,
        NuGetFramework framework = null,
        string configuration = Constants.DefaultConfiguration);
}
