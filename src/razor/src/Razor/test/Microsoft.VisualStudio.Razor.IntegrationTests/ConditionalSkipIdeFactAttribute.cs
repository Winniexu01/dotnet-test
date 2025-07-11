﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ConditionalSkipIdeFactAttribute : IdeFactAttribute
{
    private readonly Lazy<bool> _runFlakyTests = new(() => Environment.GetEnvironmentVariable("RAZOR_RUN_CONDITIONAL_IDE_TESTS")?.ToLower() == "true");

    public ConditionalSkipIdeFactAttribute()
    {
    }

    private string _issue = "";
    public string Issue
    {
        get => _issue;
        set
        {
            _issue = value;

            if (!_runFlakyTests.Value)
            {
                #pragma warning disable CS0618
                Skip = _issue;
                #pragma warning restore CS0618
            }
        }
    }

    [Obsolete("Use Issue instead of Skip")]
    public new string Skip
    {
        get => base.Skip;
        set => base.Skip = value;
    }
}
