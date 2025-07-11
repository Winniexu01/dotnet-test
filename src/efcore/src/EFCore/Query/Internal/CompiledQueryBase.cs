// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Internal;

namespace Microsoft.EntityFrameworkCore.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public abstract class CompiledQueryBase<TContext, TResult>
    where TContext : DbContext
{
    private readonly LambdaExpression _queryExpression;

    private ExecutorAndModel? _executor;

    private sealed class ExecutorAndModel(Func<QueryContext, TResult> executor, IModel model)
    {
        public Func<QueryContext, TResult> Executor { get; } = executor;
        public IModel Model { get; } = model;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected CompiledQueryBase(LambdaExpression queryExpression)
        => _queryExpression = queryExpression;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected virtual TResult ExecuteCore(
        TContext context,
        params object?[] parameters)
        => ExecuteCore(context, default, parameters);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected virtual TResult ExecuteCore(
        TContext context,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        EnsureExecutor(context);

        if (_executor!.Model != context.Model)
        {
            throw new InvalidOperationException(CoreStrings.CompiledQueryDifferentModel(_queryExpression.Print()));
        }

        var queryContextFactory = context.GetService<IQueryContextFactory>();
        var queryContext = queryContextFactory.Create();

        queryContext.CancellationToken = cancellationToken;

        var queryParameters = queryContext.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            queryParameters.Add(_queryExpression.Parameters[i + 1].Name!, parameters[i]);
        }

        return _executor.Executor(queryContext);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected abstract Func<QueryContext, TResult> CreateCompiledQuery(IQueryCompiler queryCompiler, Expression expression);

    private void EnsureExecutor(TContext context)
        => NonCapturingLazyInitializer.EnsureInitialized(
            ref _executor,
            this,
            context,
            _queryExpression,
            static (t, c, q) =>
            {
                var queryCompiler = c.GetService<IQueryCompiler>();
                var expression = new QueryExpressionRewriter(c, q.Parameters).Visit(q.Body);

                return new ExecutorAndModel(t.CreateCompiledQuery(queryCompiler, expression), c.Model);
            });

    private sealed class QueryExpressionRewriter(TContext context, IReadOnlyCollection<ParameterExpression> parameters) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression parameterExpression)
            => typeof(TContext).IsAssignableFrom(parameterExpression.Type)
                ? Expression.Constant(context)
                : parameters.Contains(parameterExpression)
                    ? new QueryParameterExpression(parameterExpression.Name!, parameterExpression.Type)
                    : parameterExpression;
    }
}
