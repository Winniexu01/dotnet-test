// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class MisplacedAssemblyUnderLibRule : IPackageRule
    {
        public string MessageFormat { get; }

        public MisplacedAssemblyUnderLibRule(string messageFormat)
        {
            MessageFormat = messageFormat;
        }
        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            foreach (var packageFile in builder.GetFiles())
            {
                var file = PathUtility.GetPathWithDirectorySeparator(packageFile);
                var directory = Path.GetDirectoryName(file);

                // if under 'lib' directly
                if (directory.Equals(PackagingConstants.Folders.Lib, StringComparison.OrdinalIgnoreCase))
                {
                    if (PackageHelper.IsAssembly(file))
                    {
                        yield return CreatePackageIssueForAssembliesUnderLib(file);
                    }
                }
            }
        }

        private PackagingLogMessage CreatePackageIssueForAssembliesUnderLib(string target)
        {
            return PackagingLogMessage.CreateWarning(
                string.Format(CultureInfo.CurrentCulture, MessageFormat, target),
                NuGetLogCode.NU5101);
        }
    }
}
