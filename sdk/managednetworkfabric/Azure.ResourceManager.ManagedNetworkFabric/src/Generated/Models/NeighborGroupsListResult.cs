// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Collections.Generic;
using Azure.Core;
using Azure.ResourceManager.ManagedNetworkFabric;

namespace Azure.ResourceManager.ManagedNetworkFabric.Models
{
    /// <summary> List of Neighbor Group. </summary>
    internal partial class NeighborGroupsListResult
    {
        /// <summary> Initializes a new instance of NeighborGroupsListResult. </summary>
        internal NeighborGroupsListResult()
        {
            Value = new ChangeTrackingList<NetworkFabricNeighborGroupData>();
        }

        /// <summary> Initializes a new instance of NeighborGroupsListResult. </summary>
        /// <param name="value"> List of Neighbor Group resources. </param>
        /// <param name="nextLink"> Url to follow for getting next page of resources. </param>
        internal NeighborGroupsListResult(IReadOnlyList<NetworkFabricNeighborGroupData> value, string nextLink)
        {
            Value = value;
            NextLink = nextLink;
        }

        /// <summary> List of Neighbor Group resources. </summary>
        public IReadOnlyList<NetworkFabricNeighborGroupData> Value { get; }
        /// <summary> Url to follow for getting next page of resources. </summary>
        public string NextLink { get; }
    }
}
