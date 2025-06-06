﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Debugging;

internal class TestLSPBreakpointSpanProvider : ILSPBreakpointSpanProvider
{
    private readonly Uri _documentUri;
    private readonly IReadOnlyDictionary<Position, LspRange> _mappings;

    public TestLSPBreakpointSpanProvider(Uri documentUri, IReadOnlyDictionary<Position, LspRange> mappings)
    {
        if (documentUri is null)
        {
            throw new ArgumentNullException(nameof(documentUri));
        }

        if (mappings is null)
        {
            throw new ArgumentNullException(nameof(mappings));
        }

        _documentUri = documentUri;
        _mappings = mappings;
    }

    public Task<LspRange> GetBreakpointSpanAsync(LSPDocumentSnapshot documentSnapshot, long hostDocumentSyncVersion, Position position, CancellationToken cancellationToken)
    {
        if (documentSnapshot.Uri != _documentUri)
        {
            return SpecializedTasks.Null<LspRange>();
        }

        foreach (var mapping in _mappings.OrderBy(d => d.Key))
        {
            if (mapping.Key.Line == position.Line &&
                mapping.Key.Character >= position.Character)
            {
                return Task.FromResult(mapping.Value);
            }
        }

        return SpecializedTasks.Null<LspRange>();
    }
}
