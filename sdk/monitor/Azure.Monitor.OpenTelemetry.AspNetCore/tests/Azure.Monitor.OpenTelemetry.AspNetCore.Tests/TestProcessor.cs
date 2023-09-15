// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using NUnit.Framework;
using OpenTelemetry;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Azure.Monitor.OpenTelemetry.AspNetCore.Tests
{
    internal class TestProcessor : BaseProcessor<Activity>
    {
        public ConcurrentQueue<Activity> ProcessedActivities { get; } = new ConcurrentQueue<Activity>();
        public override void OnEnd(Activity data)
        {
            ProcessedActivities.Enqueue(data);
        }
    }
}
#endif
