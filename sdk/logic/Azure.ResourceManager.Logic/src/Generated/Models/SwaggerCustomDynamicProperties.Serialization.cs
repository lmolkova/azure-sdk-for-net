// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using Azure.Core;

namespace Azure.ResourceManager.Logic.Models
{
    public partial class SwaggerCustomDynamicProperties : IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            if (Optional.IsDefined(OperationId))
            {
                writer.WritePropertyName("operationId"u8);
                writer.WriteStringValue(OperationId);
            }
            if (Optional.IsDefined(ValuePath))
            {
                writer.WritePropertyName("valuePath"u8);
                writer.WriteStringValue(ValuePath);
            }
            if (Optional.IsCollectionDefined(Parameters))
            {
                writer.WritePropertyName("parameters"u8);
                writer.WriteStartObject();
                foreach (var item in Parameters)
                {
                    writer.WritePropertyName(item.Key);
                    writer.WriteObjectValue(item.Value);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        internal static SwaggerCustomDynamicProperties DeserializeSwaggerCustomDynamicProperties(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            Optional<string> operationId = default;
            Optional<string> valuePath = default;
            Optional<IDictionary<string, SwaggerCustomDynamicProperties>> parameters = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("operationId"u8))
                {
                    operationId = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("valuePath"u8))
                {
                    valuePath = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("parameters"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    Dictionary<string, SwaggerCustomDynamicProperties> dictionary = new Dictionary<string, SwaggerCustomDynamicProperties>();
                    foreach (var property0 in property.Value.EnumerateObject())
                    {
                        dictionary.Add(property0.Name, DeserializeSwaggerCustomDynamicProperties(property0.Value));
                    }
                    parameters = dictionary;
                    continue;
                }
            }
            return new SwaggerCustomDynamicProperties(operationId.Value, valuePath.Value, Optional.ToDictionary(parameters));
        }
    }
}
