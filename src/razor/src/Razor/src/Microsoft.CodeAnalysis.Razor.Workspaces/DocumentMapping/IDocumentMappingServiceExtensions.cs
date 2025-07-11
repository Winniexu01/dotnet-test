﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal static class IDocumentMappingServiceExtensions
{
    public static TextEdit[] GetHostDocumentEdits(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, TextEdit[] generatedDocumentEdits)
    {
        var generatedDocumentSourceText = generatedDocument.GetGeneratedSourceText();
        var documentText = generatedDocument.CodeDocument.AssumeNotNull().Source.Text;

        var changes = generatedDocumentEdits.SelectAsArray(generatedDocumentSourceText.GetTextChange);
        var mappedChanges = service.GetHostDocumentEdits(generatedDocument, changes);
        return mappedChanges.Select(documentText.GetTextEdit).ToArray();
    }

    public static bool TryMapToHostDocumentRange(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, LinePositionSpan projectedRange, out LinePositionSpan originalRange)
        => service.TryMapToHostDocumentRange(generatedDocument, projectedRange, MappingBehavior.Strict, out originalRange);

    public static bool TryMapToHostDocumentRange(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, LspRange projectedRange, [NotNullWhen(true)] out LspRange? originalRange)
        => service.TryMapToHostDocumentRange(generatedDocument, projectedRange, MappingBehavior.Strict, out originalRange);

    public static DocumentPositionInfo GetPositionInfo(
        this IDocumentMappingService service,
        RazorCodeDocument codeDocument,
        int hostDocumentIndex)
    {
        var sourceText = codeDocument.Source.Text;

        if (sourceText.Length == 0)
        {
            Debug.Assert(hostDocumentIndex == 0);

            // Special case for empty documents, to just force Html. When there is no content, then there are no source mappings,
            // so the map call below fails, and we would default to Razor. This is fine for most cases, but empty documents are a
            // special case where Html provides much better results when users first start typing.
            return new DocumentPositionInfo(RazorLanguageKind.Html, new Position(0, 0), hostDocumentIndex);
        }

        var position = sourceText.GetPosition(hostDocumentIndex);

        var languageKind = codeDocument.GetLanguageKind(hostDocumentIndex, rightAssociative: false);
        if (languageKind is not RazorLanguageKind.Razor)
        {
            var generatedDocument = languageKind is RazorLanguageKind.CSharp
                ? (IRazorGeneratedDocument)codeDocument.GetRequiredCSharpDocument()
                : codeDocument.GetHtmlDocument();
            if (service.TryMapToGeneratedDocumentPosition(generatedDocument, hostDocumentIndex, out Position? mappedPosition, out _))
            {
                // For C# locations, we attempt to return the corresponding position
                // within the projected document
                position = mappedPosition;
            }
            else
            {
                // It no longer makes sense to think of this location as C# or Html, since it doesn't
                // correspond to any position in the projected document. This should not happen
                // since there should be source mappings for all the C# spans.
                languageKind = RazorLanguageKind.Razor;
            }
        }

        return new DocumentPositionInfo(languageKind, position, hostDocumentIndex);
    }

    public static bool TryMapToHostDocumentRange(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, LspRange generatedDocumentRange, MappingBehavior mappingBehavior, [NotNullWhen(true)] out LspRange? hostDocumentRange)
    {
        var result = service.TryMapToHostDocumentRange(generatedDocument, generatedDocumentRange.ToLinePositionSpan(), mappingBehavior, out var hostDocumentLinePositionSpan);
        hostDocumentRange = result ? hostDocumentLinePositionSpan.ToRange() : null;
        return result;
    }

    public static bool TryMapToGeneratedDocumentRange(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, LspRange hostDocumentRange, [NotNullWhen(true)] out LspRange? generatedDocumentRange)
    {
        var result = service.TryMapToGeneratedDocumentRange(generatedDocument, hostDocumentRange.ToLinePositionSpan(), out var generatedDocumentLinePositionSpan);
        generatedDocumentRange = result ? generatedDocumentLinePositionSpan.ToRange() : null;
        return result;
    }

    public static bool TryMapToHostDocumentPosition(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, int generatedDocumentIndex, [NotNullWhen(true)] out Position? hostDocumentPosition, out int hostDocumentIndex)
    {
        var result = service.TryMapToHostDocumentPosition(generatedDocument, generatedDocumentIndex, out var hostDocumentLinePosition, out hostDocumentIndex);
        hostDocumentPosition = result ? hostDocumentLinePosition.ToPosition() : null;
        return result;
    }

    public static bool TryMapToGeneratedDocumentPosition(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, int hostDocumentIndex, [NotNullWhen(true)] out Position? generatedPosition, out int generatedIndex)
    {
        var result = service.TryMapToGeneratedDocumentPosition(generatedDocument, hostDocumentIndex, out var generatedLinePosition, out generatedIndex);
        generatedPosition = result ? generatedLinePosition.ToPosition() : null;
        return result;
    }

    public static bool TryMapToGeneratedDocumentOrNextCSharpPosition(this IDocumentMappingService service, IRazorGeneratedDocument generatedDocument, int hostDocumentIndex, [NotNullWhen(true)] out Position? generatedPosition, out int generatedIndex)
    {
        var result = service.TryMapToGeneratedDocumentOrNextCSharpPosition(generatedDocument, hostDocumentIndex, out var generatedLinePosition, out generatedIndex);
        generatedPosition = result ? generatedLinePosition.ToPosition() : null;
        return result;
    }
}
