﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Mapping;

public class RazorMapToDocumentRangesEndpointTest : LanguageServerTestBase
{
    private readonly IDocumentMappingService _documentMappingService;

    public RazorMapToDocumentRangesEndpointTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _documentMappingService = new LspDocumentMappingService(
            FilePathService,
            new TestDocumentContextFactory(),
            LoggerFactory);
    }

    // These are more integration tests to validate that all the pieces work together
    [Fact]
    public async Task Handle_MapToDocumentRanges_CSharp()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "var __o = DateTime.Now",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(10, 12))]);
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var languageEndpoint = new RazorMapToDocumentRangesEndpoint(_documentMappingService);
        var request = new RazorMapToDocumentRangesParams()
        {
            Kind = RazorLanguageKind.CSharp,
            ProjectedRanges = [LspFactory.CreateSingleLineRange(line: 0, character: 10, length: 12)],
            RazorDocumentUri = documentPath,
        };
        var expectedRange = LspFactory.CreateSingleLineRange(line: 0, character: 4, length: 12);

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(expectedRange, response.Ranges[0]);
    }

    [Fact]
    public async Task Handle_MapToDocumentRanges_CSharp_Unmapped()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "var __o = DateTime.Now",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(10, 12))]);
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var languageEndpoint = new RazorMapToDocumentRangesEndpoint(_documentMappingService);
        var request = new RazorMapToDocumentRangesParams()
        {
            Kind = RazorLanguageKind.CSharp,
            ProjectedRanges = [LspFactory.CreateSingleLineRange(start: (0, 0), length: 3)],
            RazorDocumentUri = documentPath,
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(LspFactory.UndefinedRange, response.Ranges[0]);
    }

    [Fact]
    public async Task Handle_MapToDocumentRanges_CSharp_LeadingOverlapsUnmapped()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "var __o = DateTime.Now",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(10, 12))]);
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var languageEndpoint = new RazorMapToDocumentRangesEndpoint(_documentMappingService);
        var request = new RazorMapToDocumentRangesParams()
        {
            Kind = RazorLanguageKind.CSharp,
            ProjectedRanges = [LspFactory.CreateSingleLineRange(start: (0, 0), length: 22)],
            RazorDocumentUri = documentPath,
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(LspFactory.UndefinedRange, response.Ranges[0]);
    }

    [Fact]
    public async Task Handle_MapToDocumentRanges_CSharp_TrailingOverlapsUnmapped()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: "var __o = DateTime.Now",
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(10, 12))]);
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var languageEndpoint = new RazorMapToDocumentRangesEndpoint(_documentMappingService);
        var request = new RazorMapToDocumentRangesParams()
        {
            Kind = RazorLanguageKind.CSharp,
            ProjectedRanges = [LspFactory.CreateSingleLineRange(line: 0, character: 10, length: 13)],
            RazorDocumentUri = documentPath,
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(LspFactory.UndefinedRange, response.Ranges[0]);
    }

    [Fact]
    public async Task Handle_MapToDocumentRanges_Html()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocument("<p>@DateTime.Now</p>");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var languageEndpoint = new RazorMapToDocumentRangesEndpoint(_documentMappingService);
        var request = new RazorMapToDocumentRangesParams()
        {
            Kind = RazorLanguageKind.Html,
            ProjectedRanges = [LspFactory.CreateSingleLineRange(line: 0, character: 16, length: 4)],
            RazorDocumentUri = documentPath,
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(request.ProjectedRanges[0], response.Ranges[0]);
    }

    [Fact]
    public async Task Handle_MapToDocumentRanges_Razor()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocument("<p>@DateTime.Now</p>");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var languageEndpoint = new RazorMapToDocumentRangesEndpoint(_documentMappingService);
        var request = new RazorMapToDocumentRangesParams()
        {
            Kind = RazorLanguageKind.Razor,
            ProjectedRanges = [LspFactory.CreateSingleLineRange(line: 0, character: 4, length: 1)],
            RazorDocumentUri = documentPath,
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task Handle_MapToDocumentRanges_CSharp_LargeFile()
    {
        // Arrange
        var chsarpAnnotatedSource = """
            class C
            {
                public void M()
                {
                    var currentTime = [|DateTime.Now|];
                }
            }
            """;

        TestFileMarkupParser.GetSpan(chsarpAnnotatedSource, out var csharpSource, out var projectedRange);
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocumentWithCSharpProjection(
            razorSource: "<p>@DateTime.Now</p>",
            projectedCSharpSource: csharpSource,
            sourceMappings: [new SourceMapping(new SourceSpan(4, 12), new SourceSpan(projectedRange.Start, projectedRange.Length))]);
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var languageEndpoint = new RazorMapToDocumentRangesEndpoint(_documentMappingService);
        var request = new RazorMapToDocumentRangesParams()
        {
            Kind = RazorLanguageKind.CSharp,
            ProjectedRanges = [LspFactory.CreateSingleLineRange(line: 4, character: 26, length: 12)],
            RazorDocumentUri = documentPath,
        };
        var expectedRange = LspFactory.CreateSingleLineRange(line: 0, character: 4, length: 12);

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(expectedRange, response.Ranges[0]);
    }

    private static RazorCodeDocument CreateCodeDocumentWithCSharpProjection(string razorSource, string projectedCSharpSource, ImmutableArray<SourceMapping> sourceMappings)
    {
        var codeDocument = CreateCodeDocument(razorSource, tagHelpers: []);
        var csharpDocument = TestRazorCSharpDocument.Create(
            codeDocument,
            projectedCSharpSource,
            sourceMappings);
        codeDocument.SetCSharpDocument(csharpDocument);
        return codeDocument;
    }
}
