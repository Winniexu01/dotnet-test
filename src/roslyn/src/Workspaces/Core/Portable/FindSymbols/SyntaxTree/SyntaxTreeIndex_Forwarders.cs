﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.LanguageService;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.FindSymbols;

internal sealed partial class SyntaxTreeIndex
{
    public bool ProbablyContainsIdentifier(string identifier) => _identifierInfo.ProbablyContainsIdentifier(identifier);
    public bool ProbablyContainsEscapedIdentifier(string identifier) => _identifierInfo.ProbablyContainsEscapedIdentifier(identifier);

    public bool ContainsPredefinedType(PredefinedType type) => _contextInfo.ContainsPredefinedType(type);
    public bool ContainsPredefinedOperator(PredefinedOperator op) => _contextInfo.ContainsPredefinedOperator(op);

    public bool ProbablyContainsStringValue(string value) => _literalInfo.ProbablyContainsStringValue(value);
    public bool ProbablyContainsInt64Value(long value) => _literalInfo.ProbablyContainsInt64Value(value);

    public bool ContainsAwait => _contextInfo.ContainsAwait;
    public bool ContainsBaseConstructorInitializer => _contextInfo.ContainsBaseConstructorInitializer;
    public bool ContainsConversion => _contextInfo.ContainsConversion;
    public bool ContainsDeconstruction => _contextInfo.ContainsDeconstruction;
    public bool ContainsExplicitOrImplicitElementAccessExpression => _contextInfo.ContainsExplicitOrImplicitElementAccessExpression;
    public bool ContainsForEachStatement => _contextInfo.ContainsForEachStatement;
    public bool ContainsGlobalKeyword => _contextInfo.ContainsGlobalKeyword;
    public bool ContainsGlobalSuppressMessageAttribute => _contextInfo.ContainsGlobalSuppressMessageAttribute;
    public bool ContainsImplicitObjectCreation => _contextInfo.ContainsImplicitObjectCreation;
    public bool ContainsIndexerMemberCref => _contextInfo.ContainsIndexerMemberCref;
    public bool ContainsLockStatement => _contextInfo.ContainsLockStatement;
    public bool ContainsPartialClass => _contextInfo.ContainsPartialClass;
    public bool ContainsQueryExpression => _contextInfo.ContainsQueryExpression;
    public bool ContainsThisConstructorInitializer => _contextInfo.ContainsThisConstructorInitializer;
    public bool ContainsTupleExpressionOrTupleType => _contextInfo.ContainsTupleExpressionOrTupleType;
    public bool ContainsUsingStatement => _contextInfo.ContainsUsingStatement;
    public bool ContainsCollectionInitializer => _contextInfo.ContainsCollectionInitializer;
    public bool ContainsAttribute => _contextInfo.ContainsAttribute;
    public bool ContainsDirective => _contextInfo.ContainsDirective;
    public bool ContainsPrimaryConstructorBaseType => _contextInfo.ContainsPrimaryConstructorBaseType;

    /// <summary>
    /// Gets the set of global aliases that point to something with the provided name and arity.
    /// For example of there is <c>global alias X = A.B.C&lt;int&gt;</c>, then looking up with
    /// <c>name="C"</c> and arity=1 will return <c>X</c>.
    /// </summary>
    public ImmutableArray<string> GetGlobalAliases(string name, int arity)
        => GetAliasesWorker(name, arity, isGlobal: true);

    public ImmutableArray<string> GetAliases(string name, int arity)
        => GetAliasesWorker(name, arity, isGlobal: false);

    private ImmutableArray<string> GetAliasesWorker(
        string name, int arity, bool isGlobal)
    {
        if (_aliasInfo == null)
            return [];

        using var result = TemporaryArray<string>.Empty;

        foreach (var (alias, aliasName, aliasArity, aliasIsGlobal) in _aliasInfo)
        {
            if (aliasIsGlobal == isGlobal && aliasArity == arity && aliasName == name)
                result.Add(alias);
        }

        return result.ToImmutableAndClear();
    }

    public bool TryGetInterceptsLocation(InterceptsLocationData data, out TextSpan span)
    {
        if (_interceptsLocationInfo == null)
        {
            span = default;
            return false;
        }

        return _interceptsLocationInfo.TryGetValue(data, out span);
    }
}
