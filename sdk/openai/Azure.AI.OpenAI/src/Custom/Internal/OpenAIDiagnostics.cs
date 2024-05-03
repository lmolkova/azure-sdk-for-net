// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Azure.AI.OpenAI.Custom.Internal
{
    internal class OpenAIDiagnostics
    {
        private readonly string _serverAddress;
        private readonly int _serverPort;
        private readonly ClientDiagnostics _clientDiagnostics;
        private readonly bool _recordEvents = AppContextSwitchHelper
            .GetConfigValue("Azure.OpenAI.Experimental.RecordEvents",
                "AZURE_OPENAI_EXPERIMENTAL_RECORD_EVENTS");

        private readonly bool _recordContent = AppContextSwitchHelper
            .GetConfigValue("Azure.OpenAI.Experimental.RecordContent",
                "AZURE_OPENAI_EXPERIMENTAL_RECORD_CONTENT");

        public OpenAIDiagnostics(Uri endpoint, ClientDiagnostics clientDiagnostics)
        {
            _serverAddress = endpoint.AbsoluteUri;
            _serverPort = endpoint.Port;
            _clientDiagnostics = clientDiagnostics;
        }

        // TODO add sampling-relevant attributes
        // TODO we should probably do EventSource => ILogger instead
        // TODO optimize

        public InstrumentationScope StartChatCompletionsScope(ChatCompletionsOptions completionsOptions)
        {
            InstrumentationScope scope = new InstrumentationScope(_clientDiagnostics,
                completionsOptions, "chat.completions",
                _serverAddress, _serverPort,
                _recordEvents, _recordContent);
            scope.Start();
            return scope;
        }

        public StreamingScope<StreamingChatCompletionsUpdate> StartChatCompletionsStreamingScope(ChatCompletionsOptions completionsOptions)
        {
            InstrumentationScope scope = new InstrumentationScope(_clientDiagnostics,
                completionsOptions, "chat.completions", _serverAddress, _serverPort,
                _recordEvents, _recordContent);
            scope.Start();
            int requestedChoicesCount = completionsOptions.ChoiceCount ?? 1;
            return new StreamingScope<StreamingChatCompletionsUpdate>(scope, _recordContent, requestedChoicesCount);
        }

        public InstrumentationScope StartCompletionsScope(CompletionsOptions completionsOptions)
        {
            InstrumentationScope scope = new InstrumentationScope(_clientDiagnostics,
                completionsOptions, "completions", _serverAddress, _serverPort,
                _recordEvents, _recordContent);
            scope.Start();
            return scope;
        }

        public StreamingScope<Completions> StartCompletionsStreamingScope(CompletionsOptions completionsOptions)
        {
            InstrumentationScope scope = new InstrumentationScope(_clientDiagnostics,
                completionsOptions, "completions",
                _serverAddress, _serverPort,
                _recordEvents, _recordContent);
            scope.Start();
            int requestedChoicesCount = completionsOptions.Prompts.Count * (completionsOptions.ChoicesPerPrompt ?? 1);
            return new StreamingScope<Completions>(scope, _recordContent, requestedChoicesCount);
        }
    }
}
