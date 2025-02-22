// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System;
using System.Collections.Generic;
using Azure.Core;

namespace Azure.ResourceManager.AppComplianceAutomation.Models
{
    /// <summary> Single resource Id's metadata. </summary>
    public partial class ResourceMetadata
    {
        /// <summary> Initializes a new instance of ResourceMetadata. </summary>
        /// <param name="resourceId"> Resource Id - e.g. "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm1". </param>
        /// <exception cref="ArgumentNullException"> <paramref name="resourceId"/> is null. </exception>
        public ResourceMetadata(string resourceId)
        {
            Argument.AssertNotNull(resourceId, nameof(resourceId));

            ResourceId = resourceId;
            Tags = new ChangeTrackingDictionary<string, string>();
        }

        /// <summary> Initializes a new instance of ResourceMetadata. </summary>
        /// <param name="resourceId"> Resource Id - e.g. "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm1". </param>
        /// <param name="resourceType"> Resource type. </param>
        /// <param name="resourceKind"> Resource kind. </param>
        /// <param name="resourceName"> Resource name. </param>
        /// <param name="tags"> Resource's tag type. </param>
        internal ResourceMetadata(string resourceId, string resourceType, string resourceKind, string resourceName, IDictionary<string, string> tags)
        {
            ResourceId = resourceId;
            ResourceType = resourceType;
            ResourceKind = resourceKind;
            ResourceName = resourceName;
            Tags = tags;
        }

        /// <summary> Resource Id - e.g. "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm1". </summary>
        public string ResourceId { get; set; }
        /// <summary> Resource type. </summary>
        public string ResourceType { get; set; }
        /// <summary> Resource kind. </summary>
        public string ResourceKind { get; set; }
        /// <summary> Resource name. </summary>
        public string ResourceName { get; set; }
        /// <summary> Resource's tag type. </summary>
        public IDictionary<string, string> Tags { get; }
    }
}
