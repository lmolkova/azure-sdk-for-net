// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.SecurityCenter.Models;

namespace Azure.ResourceManager.SecurityCenter
{
    public partial class SecurityCenterPricingData : IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("properties"u8);
            writer.WriteStartObject();
            if (Optional.IsDefined(PricingTier))
            {
                writer.WritePropertyName("pricingTier"u8);
                writer.WriteStringValue(PricingTier.Value.ToString());
            }
            if (Optional.IsDefined(SubPlan))
            {
                writer.WritePropertyName("subPlan"u8);
                writer.WriteStringValue(SubPlan);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        internal static SecurityCenterPricingData DeserializeSecurityCenterPricingData(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            ResourceIdentifier id = default;
            string name = default;
            ResourceType type = default;
            Optional<SystemData> systemData = default;
            Optional<SecurityCenterPricingTier> pricingTier = default;
            Optional<string> subPlan = default;
            Optional<TimeSpan> freeTrialRemainingTime = default;
            Optional<bool> deprecated = default;
            Optional<IReadOnlyList<string>> replacedBy = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("id"u8))
                {
                    id = new ResourceIdentifier(property.Value.GetString());
                    continue;
                }
                if (property.NameEquals("name"u8))
                {
                    name = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("type"u8))
                {
                    type = new ResourceType(property.Value.GetString());
                    continue;
                }
                if (property.NameEquals("systemData"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    systemData = JsonSerializer.Deserialize<SystemData>(property.Value.GetRawText());
                    continue;
                }
                if (property.NameEquals("properties"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        property.ThrowNonNullablePropertyIsNull();
                        continue;
                    }
                    foreach (var property0 in property.Value.EnumerateObject())
                    {
                        if (property0.NameEquals("pricingTier"u8))
                        {
                            if (property0.Value.ValueKind == JsonValueKind.Null)
                            {
                                continue;
                            }
                            pricingTier = new SecurityCenterPricingTier(property0.Value.GetString());
                            continue;
                        }
                        if (property0.NameEquals("subPlan"u8))
                        {
                            subPlan = property0.Value.GetString();
                            continue;
                        }
                        if (property0.NameEquals("freeTrialRemainingTime"u8))
                        {
                            if (property0.Value.ValueKind == JsonValueKind.Null)
                            {
                                continue;
                            }
                            freeTrialRemainingTime = property0.Value.GetTimeSpan("P");
                            continue;
                        }
                        if (property0.NameEquals("deprecated"u8))
                        {
                            if (property0.Value.ValueKind == JsonValueKind.Null)
                            {
                                continue;
                            }
                            deprecated = property0.Value.GetBoolean();
                            continue;
                        }
                        if (property0.NameEquals("replacedBy"u8))
                        {
                            if (property0.Value.ValueKind == JsonValueKind.Null)
                            {
                                continue;
                            }
                            List<string> array = new List<string>();
                            foreach (var item in property0.Value.EnumerateArray())
                            {
                                array.Add(item.GetString());
                            }
                            replacedBy = array;
                            continue;
                        }
                    }
                    continue;
                }
            }
            return new SecurityCenterPricingData(id, name, type, systemData.Value, Optional.ToNullable(pricingTier), subPlan.Value, Optional.ToNullable(freeTrialRemainingTime), Optional.ToNullable(deprecated), Optional.ToList(replacedBy));
        }
    }
}
