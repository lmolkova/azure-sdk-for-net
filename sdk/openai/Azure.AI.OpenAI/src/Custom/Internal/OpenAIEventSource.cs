// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Azure.Core.Diagnostics;

namespace Azure.AI.OpenAI.Custom.Internal
{
    [EventSource(Name = EventSourceName)]
    internal class OpenAIEventSource : AzureEventSource
    {
        private const string EventSourceName = "Azure-AI-OpenAI-Content";
        public static OpenAIEventSource Log { get; } = new OpenAIEventSource();
        public const string EventName = "gen_ai.user.message";
        public const string GenAISystem = "openai";

        internal const int PromptContent = 1;
        public OpenAIEventSource() : base(EventSourceName)
        {
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(KeyValuePair<,>))]
        [Event(PromptContent, Level = EventLevel.Informational)]
        public virtual void RecordPrompt(string eventName, string genAiSystem, KeyValuePair<string, string> content)
        {
            if (IsEnabled())
            {
                WriteEvent(PromptContent, eventName, genAiSystem, content);
            }
        }
    }
}
