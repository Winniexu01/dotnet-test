// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using Wasm.Build.Tests;
using Xunit.Abstractions;
using Xunit;
#nullable enable

namespace Wasm.Build.Tests;

public class SignalRTestsBase : WasmTemplateTestsBase
{
    public SignalRTestsBase(ITestOutputHelper output, SharedBuildPerTestClassFixture buildContext)
        : base(output, buildContext)
    {
    }

    protected async Task SignalRPassMessage(string staticWebAssetBasePath, Configuration config, string transport)
    {
        TestAsset asset = new() { Name = "WasmBasicTestApp", RunnableProjectSubPath = "AspNetCoreServer" };
        ProjectInfo info = CopyTestAsset(config, false, asset, "SignalRClientTests");
        PublishProject(info, config, new PublishOptions(RuntimeType: RuntimeVariant.MultiThreaded, AssertAppBundle: false));

        var result = await RunForPublishWithWebServer(new BrowserRunOptions(
            Configuration: config,
            ServerEnvironment: new Dictionary<string, string> { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
            BrowserPath: staticWebAssetBasePath,
            BrowserQueryString: new NameValueCollection {
                { "transport", transport},
                { "message", "ping" }
            }));

        string testOutput = string.Join("\n", result.TestOutput) ?? "";
        Assert.NotEmpty(testOutput);
        // check sending and receiving threadId
        string threadIdUsedForSending = GetThreadOfAction(testOutput, @"SignalRPassMessages was sent by CurrentManagedThreadId=(\d+)", "signalR message was sent");
        string threadIdUsedForReceiving = GetThreadOfAction(testOutput, @"ReceiveMessage from server on CurrentManagedThreadId=(\d+)", "signalR message was received");
        string consoleOutput = string.Join("\n", result.ConsoleOutput);
        Assert.True("1" != threadIdUsedForSending || "1" != threadIdUsedForReceiving,
            $"Expected to send/receive with signalR in non-UI threads, instead only CurrentManagedThreadId=1 was used. ConsoleOutput: {consoleOutput}.");
    }

    private string GetThreadOfAction(string testOutput, string pattern, string actionDescription)
    {
        Match match = Regex.Match(testOutput, pattern);
        Assert.True(match.Success, $"Expected to find a log that {actionDescription}. TestOutput: {testOutput}.");
        return match.Groups[1].Value ?? "";
    }
}
