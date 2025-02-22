// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using Azure.Core;
using Azure.ResourceManager.Avs.Models;
using Azure.ResourceManager.Models;

namespace Azure.ResourceManager.Avs
{
    /// <summary>
    /// A class representing the HcxEnterpriseSite data model.
    /// An HCX Enterprise Site resource
    /// </summary>
    public partial class HcxEnterpriseSiteData : ResourceData
    {
        /// <summary> Initializes a new instance of HcxEnterpriseSiteData. </summary>
        public HcxEnterpriseSiteData()
        {
        }

        /// <summary> Initializes a new instance of HcxEnterpriseSiteData. </summary>
        /// <param name="id"> The id. </param>
        /// <param name="name"> The name. </param>
        /// <param name="resourceType"> The resourceType. </param>
        /// <param name="systemData"> The systemData. </param>
        /// <param name="activationKey"> The activation key. </param>
        /// <param name="status"> The status of the HCX Enterprise Site. </param>
        internal HcxEnterpriseSiteData(ResourceIdentifier id, string name, ResourceType resourceType, SystemData systemData, string activationKey, HcxEnterpriseSiteStatus? status) : base(id, name, resourceType, systemData)
        {
            ActivationKey = activationKey;
            Status = status;
        }

        /// <summary> The activation key. </summary>
        public string ActivationKey { get; }
        /// <summary> The status of the HCX Enterprise Site. </summary>
        public HcxEnterpriseSiteStatus? Status { get; }
    }
}
