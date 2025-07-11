﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

// Extensible directives only have codegen for design time, so we're only testing that.
public class ExtensibleDirectiveTest : IntegrationTestBase
{
    public ExtensibleDirectiveTest()
        : base(layer: TestProject.Layer.Compiler)
    {
    }

    [Fact]
    public void NamespaceToken()
    {
        // Arrange
        var engine = CreateProjectEngine(builder =>
        {
            builder.ConfigureDocumentClassifier(GetTestFileName(nameof(NamespaceToken)));

            builder.AddDirective(DirectiveDescriptor.CreateDirective("custom", DirectiveKind.SingleLine, b => b.AddNamespaceToken()));
        });

        var projectItem = CreateProjectItemFromFile();

        // Act
        var codeDocument = engine.ProcessDesignTime(projectItem);

        // Assert
        AssertDocumentNodeMatchesBaseline(codeDocument.GetRequiredDocumentNode());
        AssertCSharpDocumentMatchesBaseline(codeDocument.GetRequiredCSharpDocument());
        AssertLinePragmas(codeDocument);
        AssertSourceMappingsMatchBaseline(codeDocument);
    }
}
