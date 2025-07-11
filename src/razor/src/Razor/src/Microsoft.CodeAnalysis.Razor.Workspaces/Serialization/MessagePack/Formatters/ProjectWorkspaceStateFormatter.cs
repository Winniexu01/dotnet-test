﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Serialization.MessagePack.Formatters.TagHelpers;
using Microsoft.CodeAnalysis.Razor.Utilities;

namespace Microsoft.CodeAnalysis.Razor.Serialization.MessagePack.Formatters;

internal sealed class ProjectWorkspaceStateFormatter : ValueFormatter<ProjectWorkspaceState>
{
    public static readonly ValueFormatter<ProjectWorkspaceState> Instance = new ProjectWorkspaceStateFormatter();

    private ProjectWorkspaceStateFormatter()
    {
    }

    public override ProjectWorkspaceState Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(2);

        var checksums = reader.Deserialize<ImmutableArray<Checksum>>(options);

        reader.ReadArrayHeaderAndVerify(checksums.Length);

        using var builder = new PooledArrayBuilder<TagHelperDescriptor>(capacity: checksums.Length);
        var cache = TagHelperCache.Default;

        foreach (var checksum in checksums)
        {
            if (!cache.TryGet(checksum, out var tagHelper))
            {
                tagHelper = TagHelperFormatter.Instance.Deserialize(ref reader, options);
                cache.TryAdd(checksum, tagHelper);
            }
            else
            {
                TagHelperFormatter.Instance.Skim(ref reader, options);
            }

            builder.Add(tagHelper);
        }

        var tagHelpers = builder.ToImmutableAndClear();

        return ProjectWorkspaceState.Create(tagHelpers);
    }

    public override void Serialize(ref MessagePackWriter writer, ProjectWorkspaceState value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(2);

        var checksums = value.TagHelpers.SelectAsArray(x => x.Checksum);

        writer.Serialize(checksums, options);
        writer.Serialize(value.TagHelpers, options);
    }
}
