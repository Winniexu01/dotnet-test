// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.EntityFrameworkCore.Query;

/// <summary>
///     This is an experimental API used by the Entity Framework Core feature and it is not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
[Experimental(EFDiagnostics.PrecompiledQueryExperimental)]
public class RelationalLiftableConstantProcessor : LiftableConstantProcessor
{
    private readonly RelationalMaterializerLiftableConstantContext _relationalMaterializerLiftableConstantContext;

    /// <summary>
    ///     This is an experimental API used by the Entity Framework Core feature and it is not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public RelationalLiftableConstantProcessor(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        RelationalShapedQueryCompilingExpressionVisitorDependencies relationalDependencies,
        RelationalCommandBuilderDependencies commandBuilderDependencies)
        : base(dependencies)
        => _relationalMaterializerLiftableConstantContext = new RelationalMaterializerLiftableConstantContext(
            dependencies, relationalDependencies, commandBuilderDependencies);

    /// <inheritdoc />
    protected override ConstantExpression InlineConstant(LiftableConstantExpression liftableConstant)
    {
        if (liftableConstant.ResolverExpression is Expression<Func<RelationalMaterializerLiftableConstantContext, object>>
            resolverExpression)
        {
            // TODO: deep dive into this - see issue #35210
            var resolver = resolverExpression.Compile(preferInterpretation: false);
            var value = resolver(_relationalMaterializerLiftableConstantContext);
            return Expression.Constant(value, liftableConstant.Type);
        }

        return base.InlineConstant(liftableConstant);
    }
}
