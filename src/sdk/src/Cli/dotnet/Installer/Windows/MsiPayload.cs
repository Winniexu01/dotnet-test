﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Newtonsoft.Json;

namespace Microsoft.DotNet.Cli.Installer.Windows;

/// <summary>
/// Represents the payload associated with a single workload pack installer. The payload
/// consists of the installer (MSI) and its JSON manifest.
/// </summary>
internal class MsiPayload(string manifestPath, string msiPath)
{
    private MsiManifest _manifest;

    private IEnumerable<RelatedProduct> _relatedProducts;

    /// <summary>
    /// The full path of the JSON manifest associated with this payload.
    /// </summary>
    public readonly string ManifestPath = manifestPath;

    /// <summary>
    /// The full path of the MSI associated with this payload.
    /// </summary>
    public readonly string MsiPath = msiPath;

    /// <summary>
    /// The name of the MSI package.
    /// </summary>
    public string Payload => Manifest.Payload;

    /// <summary>
    /// The product code of the MSI.
    /// </summary>
    public string ProductCode => Manifest.ProductCode;

    /// <summary>
    /// The product version of the MSI.
    /// </summary>
    public Version ProductVersion => Manifest.ProductVersion;

    /// <summary>
    /// The name and extensions of the MSI package.
    /// </summary>
    public string Name => Path.GetFileName(MsiPath);

    /// <summary>
    /// A set of all related products associated with the MSI. May be empty if the MSI does not define
    /// an Upgrade table.
    /// </summary>
    public IEnumerable<RelatedProduct> RelatedProducts
    {
        get
        {
            _relatedProducts ??= Manifest.RelatedProducts ?? Enumerable.Empty<RelatedProduct>();

            return _relatedProducts;
        }
    }

    /// <summary>
    /// The manifest data describing the associated MSI.
    /// </summary>
    public MsiManifest Manifest
    {
        get
        {
            _manifest ??= JsonConvert.DeserializeObject<MsiManifest>(File.ReadAllText(ManifestPath));

            return _manifest;
        }
    }

    /// <summary>
    /// The upgrade code of the MSI.
    /// </summary>
    public string UpgradeCode => Manifest.UpgradeCode;
}
