// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

namespace Azure.ResourceManager.ServiceFabricManagedClusters.Models
{
    /// <summary> Service Fabric managed cluster Sku definition. </summary>
    public partial class ServiceFabricManagedClustersSku
    {
        /// <summary> Initializes a new instance of ServiceFabricManagedClustersSku. </summary>
        /// <param name="name"> Sku Name. </param>
        public ServiceFabricManagedClustersSku(ServiceFabricManagedClustersSkuName name)
        {
            Name = name;
        }

        /// <summary> Sku Name. </summary>
        public ServiceFabricManagedClustersSkuName Name { get; set; }
    }
}
