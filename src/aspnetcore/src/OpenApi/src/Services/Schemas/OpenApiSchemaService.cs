// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Supports managing elements that belong in the "components" section of
/// an OpenAPI document. In particular, this is the API that is used to
/// interact with the JSON schemas that are managed by a given OpenAPI document.
/// </summary>
internal sealed class OpenApiSchemaService(
    [ServiceKey] string documentName,
    IOptions<JsonOptions> jsonOptions,
    IOptionsMonitor<OpenApiOptions> optionsMonitor)
{
    private readonly ConcurrentDictionary<Type, string?> _schemaIdCache = new();
    private readonly OpenApiJsonSchemaContext _jsonSchemaContext = new(new(jsonOptions.Value.SerializerOptions));
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(jsonOptions.Value.SerializerOptions)
    {
        // In order to properly handle the `RequiredAttribute` on type properties, add a modifier to support
        // setting `JsonPropertyInfo.IsRequired` based on the presence of the `RequiredAttribute`.
        TypeInfoResolver = jsonOptions.Value.SerializerOptions.TypeInfoResolver?.WithAddedModifier(jsonTypeInfo =>
        {
            if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }
            foreach (var propertyInfo in jsonTypeInfo.Properties)
            {
                var hasRequiredAttribute = propertyInfo.AttributeProvider?
                    .GetCustomAttributes(inherit: false)
                    .Any(attr => attr is RequiredAttribute);
                propertyInfo.IsRequired |= hasRequiredAttribute ?? false;
            }
        })
    };

    private readonly JsonSchemaExporterOptions _configuration = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = (context, schema) =>
        {
            var type = context.TypeInfo.Type;
            // Fix up schemas generated for IFormFile, IFormFileCollection, Stream, and PipeReader
            // that appear as properties within complex types.
            if (type == typeof(IFormFile) || type == typeof(Stream) || type == typeof(PipeReader))
            {
                schema = new JsonObject
                {
                    [OpenApiSchemaKeywords.TypeKeyword] = "string",
                    [OpenApiSchemaKeywords.FormatKeyword] = "binary",
                    [OpenApiConstants.SchemaId] = "IFormFile"
                };
            }
            else if (type == typeof(IFormFileCollection))
            {
                schema = new JsonObject
                {
                    [OpenApiSchemaKeywords.TypeKeyword] = "array",
                    [OpenApiSchemaKeywords.ItemsKeyword] = new JsonObject
                    {
                        [OpenApiSchemaKeywords.TypeKeyword] = "string",
                        [OpenApiSchemaKeywords.FormatKeyword] = "binary",
                        [OpenApiConstants.SchemaId] = "IFormFile"
                    }
                };
            }
            // STJ uses `true` in place of an empty object to represent a schema that matches
            // anything (like the `object` type) or types with user-defined converters. We override
            // this default behavior here to match the format expected in OpenAPI v3.
            if (schema.GetValueKind() == JsonValueKind.True)
            {
                schema = new JsonObject();
            }
            var createSchemaReferenceId = optionsMonitor.Get(documentName).CreateSchemaReferenceId;
            schema.ApplyPrimitiveTypesAndFormats(context, createSchemaReferenceId);
            schema.ApplySchemaReferenceId(context, createSchemaReferenceId);
            schema.MapPolymorphismOptionsToDiscriminator(context, createSchemaReferenceId);
            if (context.PropertyInfo is { } jsonPropertyInfo)
            {
                schema.ApplyNullabilityContextInfo(jsonPropertyInfo);
            }
            if (context.PropertyInfo is { AttributeProvider: { } attributeProvider })
            {
                if (attributeProvider.GetCustomAttributes(inherit: false).OfType<ValidationAttribute>() is { } validationAttributes)
                {
                    schema.ApplyValidationAttributes(validationAttributes);
                }
                if (attributeProvider.GetCustomAttributes(inherit: false).OfType<DefaultValueAttribute>().LastOrDefault() is DefaultValueAttribute defaultValueAttribute)
                {
                    schema.ApplyDefaultValue(defaultValueAttribute.Value, context.TypeInfo);
                }
                if (attributeProvider.GetCustomAttributes(inherit: false).OfType<DescriptionAttribute>().LastOrDefault() is DescriptionAttribute descriptionAttribute)
                {
                    schema[OpenApiSchemaKeywords.DescriptionKeyword] = descriptionAttribute.Description;
                }
            }

            return schema;
        }
    };

    internal async Task<OpenApiSchema> GetOrCreateUnresolvedSchemaAsync(OpenApiDocument? document, Type type, IServiceProvider scopedServiceProvider, IOpenApiSchemaTransformer[] schemaTransformers, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
    {
        var key = parameterDescription?.ParameterDescriptor is IParameterInfoParameterDescriptor parameterInfoDescription
            && parameterDescription.ModelMetadata.PropertyName is null
            ? new OpenApiSchemaKey(type, parameterInfoDescription.ParameterInfo) : new OpenApiSchemaKey(type, null);
        var schemaAsJsonObject = CreateSchema(key);
        if (parameterDescription is not null)
        {
            schemaAsJsonObject.ApplyParameterInfo(parameterDescription, _jsonSerializerOptions.GetTypeInfo(type));
        }
        // Use _jsonSchemaContext constructed from _jsonSerializerOptions to respect shared config set by end-user,
        // particularly in the case of maxDepth.
        var deserializedSchema = JsonSerializer.Deserialize(schemaAsJsonObject, _jsonSchemaContext.OpenApiJsonSchema);
        Debug.Assert(deserializedSchema != null, "The schema should have been deserialized successfully and materialize a non-null value.");
        var schema = deserializedSchema.Schema;
        await ApplySchemaTransformersAsync(document, schema, type, scopedServiceProvider, schemaTransformers, parameterDescription, cancellationToken);
        return schema;
    }

    internal async Task<IOpenApiSchema> GetOrCreateSchemaAsync(OpenApiDocument document, Type type, IServiceProvider scopedServiceProvider, IOpenApiSchemaTransformer[] schemaTransformers, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
    {
        var schema = await GetOrCreateUnresolvedSchemaAsync(document, type, scopedServiceProvider, schemaTransformers, parameterDescription, cancellationToken);

        // Cache the root schema IDs since we expect to be called
        // on the same type multiple times within an API
        var baseSchemaId = _schemaIdCache.GetOrAdd(type, t =>
        {
            var jsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(t);
            return optionsMonitor.Get(documentName).CreateSchemaReferenceId(jsonTypeInfo);
        });

        return ResolveReferenceForSchema(document, schema, baseSchemaId);
    }

    internal static IOpenApiSchema ResolveReferenceForSchema(OpenApiDocument document, IOpenApiSchema inputSchema, string? rootSchemaId, string? baseSchemaId = null)
    {
        var schema = UnwrapOpenApiSchema(inputSchema);

        if (schema.Metadata is not null &&
            schema.Metadata.TryGetValue(OpenApiConstants.SchemaId, out var resolvedBaseSchemaId))
        {
            if (schema.AnyOf is { Count: > 0 })
            {
                for (var i = 0; i < schema.AnyOf.Count; i++)
                {
                    schema.AnyOf[i] = ResolveReferenceForSchema(document, schema.AnyOf[i], rootSchemaId, resolvedBaseSchemaId?.ToString());
                }
            }
        }

        if (schema.Properties is not null)
        {
            foreach (var property in schema.Properties)
            {
                schema.Properties[property.Key] = ResolveReferenceForSchema(document, property.Value, rootSchemaId);
            }
        }

        if (schema.AllOf is { Count: > 0 })
        {
            for (var i = 0; i < schema.AllOf.Count; i++)
            {
                schema.AllOf[i] = ResolveReferenceForSchema(document, schema.AllOf[i], rootSchemaId);
            }
        }

        if (schema.OneOf is { Count: > 0 })
        {
            for (var i = 0; i < schema.OneOf.Count; i++)
            {
                schema.OneOf[i] = ResolveReferenceForSchema(document, schema.OneOf[i], rootSchemaId);
            }
        }

        if (schema.AdditionalProperties is not null)
        {
            schema.AdditionalProperties = ResolveReferenceForSchema(document, schema.AdditionalProperties, rootSchemaId);
        }

        if (schema.Items is not null)
        {
            schema.Items = ResolveReferenceForSchema(document, schema.Items, rootSchemaId);
        }

        if (schema.Not is not null)
        {
            schema.Not = ResolveReferenceForSchema(document, schema.Not, rootSchemaId);
        }

        // Handle schemas where the references have been inlined by the JsonSchemaExporter. In this case,
        // the `#` ID is generated by the exporter since it has no base document to baseline against. In this
        // case we we want to replace the reference ID with the schema ID that was generated by the
        // `CreateSchemaReferenceId` method in the OpenApiSchemaService.
        if (schema.Metadata is not null &&
            schema.Metadata.TryGetValue(OpenApiConstants.RefId, out var refId) &&
            refId is string refIdString)
        {
            if (schema.Metadata.TryGetValue(OpenApiConstants.SchemaId, out var schemaId) &&
                schemaId is string schemaIdString)
            {
                return document.AddOpenApiSchemaByReference(schemaIdString, schema);
            }
            var relativeSchemaId = $"#/components/schemas/{rootSchemaId}{refIdString.Replace("#", string.Empty)}";
            return new OpenApiSchemaReference(relativeSchemaId, document);
        }

        // If we're resolving schemas for a top-level schema being referenced in the `components.schema` property
        // we don't want to replace the top-level inline schema with a reference to itself. We want to replace
        // inline schemas to reference schemas for all schemas referenced in the top-level schema though (such as
        // `allOf`, `oneOf`, `anyOf`, `items`, `properties`, etc.) which is why `isTopLevel` is only set once.
        if (schema is OpenApiSchema && schema.Metadata is not null &&
            !schema.Metadata.ContainsKey(OpenApiConstants.RefId) &&
            schema.Metadata.TryGetValue(OpenApiConstants.SchemaId, out var referenceId) &&
            referenceId is string referenceIdString)
        {
            var targetReferenceId = baseSchemaId is not null
                ? $"{baseSchemaId}{referenceIdString}"
                : referenceIdString;
            if (!string.IsNullOrEmpty(targetReferenceId))
            {
                return document.AddOpenApiSchemaByReference(targetReferenceId, schema);
            }
        }

        return schema;
    }

    private static OpenApiSchema UnwrapOpenApiSchema(IOpenApiSchema sourceSchema)
    {
        if (sourceSchema is OpenApiSchemaReference schemaReference)
        {
            if (schemaReference.Target is OpenApiSchema target)
            {
                return target;
            }
            else
            {
                throw new InvalidOperationException($"The input schema must be an {nameof(OpenApiSchema)} or {nameof(OpenApiSchemaReference)}.");
            }
        }
        else if (sourceSchema is OpenApiSchema directSchema)
        {
            return directSchema;
        }
        else
        {
            throw new InvalidOperationException($"The input schema must be an {nameof(OpenApiSchema)} or {nameof(OpenApiSchemaReference)}.");
        }
    }

    internal async Task ApplySchemaTransformersAsync(OpenApiDocument? document, IOpenApiSchema schema, Type type, IServiceProvider scopedServiceProvider, IOpenApiSchemaTransformer[] schemaTransformers, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
    {
        if (schemaTransformers.Length == 0)
        {
            return;
        }
        var jsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(type);
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = documentName,
            JsonTypeInfo = jsonTypeInfo,
            JsonPropertyInfo = null,
            ParameterDescription = parameterDescription,
            ApplicationServices = scopedServiceProvider,
            Document = document,
            SchemaTransformers = schemaTransformers
        };
        for (var i = 0; i < schemaTransformers.Length; i++)
        {
            // Reset context object to base state before running each transformer.
            var transformer = schemaTransformers[i];
            await InnerApplySchemaTransformersAsync(schema, jsonTypeInfo, null, context, transformer, cancellationToken);
        }
    }

    private async Task InnerApplySchemaTransformersAsync(IOpenApiSchema inputSchema,
        JsonTypeInfo jsonTypeInfo,
        JsonPropertyInfo? jsonPropertyInfo,
        OpenApiSchemaTransformerContext context,
        IOpenApiSchemaTransformer transformer,
        CancellationToken cancellationToken = default)
    {
        context.UpdateJsonTypeInfo(jsonTypeInfo, jsonPropertyInfo);
        var schema = UnwrapOpenApiSchema(inputSchema);
        await transformer.TransformAsync(schema, context, cancellationToken);

        // Only apply transformers on polymorphic schemas where we can resolve the derived
        // types associated with the base type.
        if (schema.AnyOf is { Count: > 0 } && jsonTypeInfo.PolymorphismOptions is not null)
        {
            var anyOfIndex = 0;
            foreach (var derivedType in jsonTypeInfo.PolymorphismOptions.DerivedTypes)
            {
                var derivedJsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(derivedType.DerivedType);
                if (schema.AnyOf.Count <= anyOfIndex)
                {
                    break;
                }
                await InnerApplySchemaTransformersAsync(schema.AnyOf[anyOfIndex], derivedJsonTypeInfo, null, context, transformer, cancellationToken);
                anyOfIndex++;
            }
        }

        if (schema.Items is not null)
        {
            var elementTypeInfo = _jsonSerializerOptions.GetTypeInfo(jsonTypeInfo.ElementType!);
            await InnerApplySchemaTransformersAsync(schema.Items, elementTypeInfo, null, context, transformer, cancellationToken);
        }

        if (schema.Properties is { Count: > 0 })
        {
            foreach (var propertyInfo in jsonTypeInfo.Properties)
            {
                if (schema.Properties.TryGetValue(propertyInfo.Name, out var propertySchema))
                {
                    await InnerApplySchemaTransformersAsync(propertySchema, _jsonSerializerOptions.GetTypeInfo(propertyInfo.PropertyType), propertyInfo, context, transformer, cancellationToken);
                }
            }
        }

        if (schema is { AdditionalPropertiesAllowed: true, AdditionalProperties: not null } &&
            jsonTypeInfo.ElementType is not null)
        {
            var elementTypeInfo = _jsonSerializerOptions.GetTypeInfo(jsonTypeInfo.ElementType);
            await InnerApplySchemaTransformersAsync(schema.AdditionalProperties, elementTypeInfo, null, context, transformer, cancellationToken);
        }
    }

    private JsonNode CreateSchema(OpenApiSchemaKey key)
        => JsonSchemaExporter.GetJsonSchemaAsNode(_jsonSerializerOptions, key.Type, _configuration);
}
