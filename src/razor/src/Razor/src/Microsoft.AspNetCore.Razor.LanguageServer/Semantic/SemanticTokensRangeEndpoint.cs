﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

[RazorLanguageServerEndpoint(Methods.TextDocumentSemanticTokensRangeName)]
internal sealed class SemanticTokensRangeEndpoint(
    IRazorSemanticTokensInfoService semanticTokensInfoService,
    ISemanticTokensLegendService semanticTokensLegendService,
    RazorLSPOptionsMonitor razorLSPOptionsMonitor,
    ITelemetryReporter? telemetryReporter)
    : IRazorRequestHandler<SemanticTokensRangeParams, SemanticTokens?>, ICapabilitiesProvider
{
    private readonly IRazorSemanticTokensInfoService _semanticTokensInfoService = semanticTokensInfoService;
    private readonly ISemanticTokensLegendService _semanticTokensLegendService = semanticTokensLegendService;
    private readonly RazorLSPOptionsMonitor _razorLSPOptionsMonitor = razorLSPOptionsMonitor;
    private readonly ITelemetryReporter? _telemetryReporter = telemetryReporter;

    public bool MutatesSolutionState { get; } = false;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        serverCapabilities.EnableSemanticTokens(_semanticTokensLegendService);
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(SemanticTokensRangeParams request)
    {
        return request.TextDocument;
    }

    public async Task<SemanticTokens?> HandleRequestAsync(SemanticTokensRangeParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return null;
        }

        var colorBackground = _razorLSPOptionsMonitor.CurrentValue.ColorBackground;

        var correlationId = Guid.NewGuid();
        using var _ = _telemetryReporter?.TrackLspRequest(Methods.TextDocumentSemanticTokensRangeName, LanguageServerConstants.RazorLanguageServerName, TelemetryThresholds.SemanticTokensRazorTelemetryThreshold, correlationId);

        var data = await _semanticTokensInfoService.GetSemanticTokensAsync(documentContext, request.Range.ToLinePositionSpan(), colorBackground, correlationId, cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            return null;
        }

        return new SemanticTokens
        {
            Data = data,
        };
    }
}
