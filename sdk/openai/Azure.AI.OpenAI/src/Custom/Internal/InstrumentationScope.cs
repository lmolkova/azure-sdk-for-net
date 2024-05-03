// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Azure.Core.Pipeline;

namespace Azure.AI.OpenAI.Custom.Internal
{
    internal class InstrumentationScope : IDisposable
    {
        private static readonly Meter s_meterClient = new Meter("Azure.AI.OpenAI.Client");
        private static readonly Histogram<double> s_duration = s_meterClient.CreateHistogram<double>("gen_ai.operation.duration", "s", "GenAI request duration");
        private static readonly Histogram<int> s_tokens = s_meterClient.CreateHistogram<int>("gen_ai.token.usage", "{token}", "GenAI token usage.");

        // up down counter not available in DS 6
        private static readonly Meter s_meterStreams = new Meter("Azure.AI.OpenAI.Streams");
        private static readonly Counter<long> s_streamStart = s_meterStreams.CreateCounter<long>("gen_ai.stream.start", "{stream}", "GenAI streams started.");
        private static readonly Counter<long> s_streamComplete = s_meterStreams.CreateCounter<long>("gen_ai.stream.end", "{stream}", "GenAI streams completed.");
        //private static readonly Counter<long> s_chunks = s_meterStreams.CreateCounter<long>("gen_ai.stream.output.tokens", "{token}", "GenAI streaming chunk count.");

        private readonly string _operationName;
        private readonly ChatCompletionsOptions _chatCompletionsOptions;
        private readonly CompletionsOptions _completionsOptions;
        private readonly string _serverAddress;
        private readonly int _serverPort;
        private readonly bool _recordEvents;
        private readonly bool _recordContent;
        private Stopwatch _duration;

        public InstrumentationScope(ClientDiagnostics clientDiagnostics,
            ChatCompletionsOptions completionsOptions,
            string operationName, string serverAddress, int serverPort,
            bool recordEvents, bool recordContent)
        {
            DiagnosticScope = clientDiagnostics.CreateScope(operationName + " " + completionsOptions.DeploymentName, ActivityKind.Client);
            _chatCompletionsOptions = completionsOptions;
            _operationName = operationName;
            _serverAddress = serverAddress;
            _serverPort = serverPort;
            _recordEvents = recordEvents;
            _recordContent = recordContent;
        }

        public InstrumentationScope(ClientDiagnostics clientDiagnostics,
            CompletionsOptions completionsOptions,
            string operationName, string serverAddress, int serverPort,
            bool recordEvents, bool recordContent)
        {
            DiagnosticScope = clientDiagnostics.CreateScope(operationName + " " + completionsOptions.DeploymentName, ActivityKind.Client);
            _completionsOptions = completionsOptions;
            _operationName = operationName;
            _serverAddress = serverAddress;
            _serverPort = serverPort;
            _recordEvents = recordEvents;
            _recordContent = recordContent;
        }

        public DiagnosticScope DiagnosticScope { get; private set; }
        private string DeploymentName => _chatCompletionsOptions?.DeploymentName ?? _completionsOptions?.DeploymentName;
        private int? MaxTokens => _chatCompletionsOptions?.MaxTokens ?? _completionsOptions?.MaxTokens;
        private double? Temperature => _chatCompletionsOptions?.Temperature ?? _completionsOptions?.Temperature;
        private double? NucleusSamplingFactor => _chatCompletionsOptions?.NucleusSamplingFactor ?? _completionsOptions?.NucleusSamplingFactor;

        public bool IsActivityRecorded => DiagnosticScope.IsEnabled && DiagnosticScope.Activity?.IsAllDataRequested == true;

        public void Start()
        {
            DiagnosticScope.Start();
            RecordCommonAttirbutes();
            _duration = Stopwatch.StartNew();

            if (IsActivityRecorded && _recordEvents)
            {
                if (_completionsOptions?.Prompts != null)
                {
                    foreach (var prompt in _completionsOptions.Prompts)
                    {
                        DiagnosticEventUtils.RecordPrompt(DiagnosticScope, prompt, _recordContent);
                    }
                }
                else if (_chatCompletionsOptions.Messages != null)
                {
                    foreach (var message in _chatCompletionsOptions.Messages)
                    {
                        DiagnosticEventUtils.RecordRequestMessage(DiagnosticScope, message, _recordContent);
                    }
                }
            }
        }

        private void RecordCommonAttirbutes()
        {
            if (IsActivityRecorded)
            {
                DiagnosticScope.AddAttribute("gen_ai.system", "openai");
                DiagnosticScope.AddAttribute("gen_ai.request.model", DeploymentName);
                DiagnosticScope.AddAttribute("server.address", _serverAddress);
                DiagnosticScope.AddIntegerAttribute("server.port", _serverPort);
                DiagnosticScope.AddAttribute("gen_ai.operation.name", _operationName);

                if (MaxTokens != null)
                {
                    DiagnosticScope.AddIntegerAttribute("gen_ai.request.max_tokens", MaxTokens.Value);
                }

                if (Temperature != null)
                {
                    DiagnosticScope.AddDoubleAttribute("gen_ai.request.temperature", Temperature.Value);
                }

                if (NucleusSamplingFactor != null)
                {
                    DiagnosticScope.AddDoubleAttribute("gen_ai.request.top_p", NucleusSamplingFactor.Value);
                }
            }
        }

        public void RecordChatCompletions(ChatCompletions completions)
        {
            if (IsActivityRecorded)
            {
                string firstFinishReason = null;
                if (completions?.Choices != null && completions.Choices.Count > 0)
                {
                    firstFinishReason = completions.Choices[0].FinishReason?.ToString();
                    if (_recordEvents)
                    {
                        foreach (var choice in completions.Choices)
                        {
                            DiagnosticEventUtils.RecordChoice(DiagnosticScope, choice.Index, choice.FinishReason, choice.Message.Role, choice.Message.Content,
                                choice.Message.ToolCalls, _recordContent);
                        }
                    }
                }

                RecordResponseAttributes(completions.Id, completions.Model, firstFinishReason,
                    completions.Usage?.PromptTokens, completions.Usage?.CompletionTokens);
            }

            RecordMetrics(completions.Model, null, completions.Usage?.PromptTokens, completions.Usage?.CompletionTokens);
        }

        public void RecordStreamingResponse(string responseId, string model, string finishReason, int? completionTokens, Exception ex, bool canceled)
        {
            if (IsActivityRecorded)
            {
                RecordResponseAttributes(responseId, model, finishReason, null, completionTokens);
                if (canceled)
                {
                    DiagnosticScope.Failed(GetErrorType(null, true));
                }
            }

            RecordMetrics(model, GetErrorType(ex, canceled), null, completionTokens);
        }

        public void RecordCompletions(Completions completions)
        {
            if (IsActivityRecorded)
            {
                string firstFinishReason = null;
                if (completions?.Choices != null && completions.Choices.Count > 0)
                {
                    firstFinishReason = completions.Choices[0].FinishReason?.ToString();
                    if (_recordEvents)
                    {
                        foreach (var choice in completions.Choices)
                        {
                            DiagnosticEventUtils.RecordChoice(DiagnosticScope, choice.Index, choice.FinishReason, null, choice.Text, null, _recordContent);
                        }
                    }
                }

                RecordResponseAttributes(completions.Id, firstFinishReason, null,
                        completions.Usage?.PromptTokens, completions.Usage?.CompletionTokens);
            }

            RecordMetrics(null, null, completions?.Usage?.PromptTokens, completions?.Usage?.CompletionTokens);
        }

        public void RecordException(Exception ex, bool canceled)
        {
            string errorType = GetErrorType(ex, canceled);
            RecordMetrics(null, errorType, null, null);
            if (ex != null)
            {
                DiagnosticScope.Failed(ex);
            }
            else
            {
                DiagnosticScope.Failed(errorType);
            }
        }

        private void RecordMetrics(string responseModelName, string errorType, int? promptTokens, int? completionTokens)
        {
            if (promptTokens != null)
            {
                s_tokens.Record(promptTokens.Value, new TagList
                {
                    { "gen_ai.system", "openai" },
                    { "gen_ai.request.model", DeploymentName },
                    { "gen_ai.response.model", responseModelName },
                    { "server.address", _serverAddress},
                    { "server.port", _serverPort },
                    { "gen_ai.operation.name", _operationName },
                    { "error.type", errorType },
                    { "gen_ai.usage.token_type", "input" },
                });
            }

            if (completionTokens != null)
            {
                s_tokens.Record(completionTokens.Value, new TagList
                {
                    { "gen_ai.system", "openai" },
                    { "gen_ai.request.model", DeploymentName },
                    { "gen_ai.response.model", responseModelName },
                    { "server.address", _serverAddress},
                    { "server.port", _serverPort },
                    { "gen_ai.operation.name", _operationName },
                    { "error.type", errorType },
                    { "gen_ai.usage.token_type", "output" },
                });
            }

            s_duration.Record(_duration.Elapsed.TotalSeconds, new TagList
                {
                    { "gen_ai.system", "openai" },
                    { "gen_ai.request.model", DeploymentName },
                    { "gen_ai.response.model", responseModelName },
                    { "server.address", _serverAddress},
                    { "server.port", _serverPort },
                    { "gen_ai.operation.name", _operationName },
                    { "error.type", errorType }
                });
        }

        public void RecordStreamStart()
        {
            s_streamStart.Add(1, new TagList
            {
                { "gen_ai.system", "openai" },
                { "gen_ai.request.model", DeploymentName },
                { "server.address", _serverAddress},
                { "server.port", _serverPort },
                { "gen_ai.operation.name", _operationName },
            });
        }

        public void RecordStreamComplete()
        {
            s_streamComplete.Add(1, new TagList
            {
                { "gen_ai.system", "openai" },
                { "gen_ai.request.model", DeploymentName },
                { "server.address", _serverAddress},
                { "server.port", _serverPort },
                { "gen_ai.operation.name", _operationName },
            });
        }

        /*public void RecordStreamChunk(string responseModelName)
        {
            s_chunks.Add(1, new TagList
            {
                { "gen_ai.system", "openai" },
                { "gen_ai.request.model", DeploymentName },
                { "gen_ai.response.model", responseModelName },
                { "server.address", _serverAddress},
                { "server.port", _serverPort },
                { "gen_ai.operation.name", _operationName },
            });
        }*/

        private void RecordResponseAttributes(string responseId, string model, string finishReason, int? promptTokens, int? completionTokens)
        {
            if (responseId != null)
            {
                DiagnosticScope.AddAttribute("gen_ai.response.id", responseId);
            }

            if (model != null)
            {
                DiagnosticScope.AddAttribute("gen_ai.response.model", model);
            }

            if (finishReason != null)
            {
                DiagnosticScope.AddAttribute("gen_ai.response.finish_reason", finishReason?.ToString());
            }

            if (promptTokens != null)
            {
                DiagnosticScope.AddIntegerAttribute("gen_ai.usage.prompt_tokens", promptTokens.Value);
            }

            if (completionTokens != null)
            {
                DiagnosticScope.AddIntegerAttribute("gen_ai.usage.completion_tokens", completionTokens.Value);
            }
        }

        public void Dispose()
        {
            DiagnosticScope.Dispose();
        }

        private string GetErrorType(Exception exception, bool canceled)
        {
            if (canceled)
            {
                return typeof(TaskCanceledException).FullName;
            }

            if (exception is RequestFailedException requestFailedException)
            {
                // TODO (limolkova) when we start targeting .NET 8 we should put
                // requestFailedException.InnerException.HttpRequestError into error.type
                if (!string.IsNullOrEmpty(requestFailedException.ErrorCode))
                {
                    return requestFailedException.ErrorCode;
                }
            }

            return exception?.GetType()?.FullName;
        }
    }
}
