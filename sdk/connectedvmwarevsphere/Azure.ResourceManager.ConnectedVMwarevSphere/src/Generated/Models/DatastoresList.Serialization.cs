// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager.ConnectedVMwarevSphere;

namespace Azure.ResourceManager.ConnectedVMwarevSphere.Models
{
    internal partial class DatastoresList
    {
        internal static DatastoresList DeserializeDatastoresList(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            Optional<string> nextLink = default;
            IReadOnlyList<VMwareDatastoreData> value = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("nextLink"u8))
                {
                    nextLink = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("value"u8))
                {
                    List<VMwareDatastoreData> array = new List<VMwareDatastoreData>();
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        array.Add(VMwareDatastoreData.DeserializeVMwareDatastoreData(item));
                    }
                    value = array;
                    continue;
                }
            }
            return new DatastoresList(nextLink.Value, value);
        }
    }
}
