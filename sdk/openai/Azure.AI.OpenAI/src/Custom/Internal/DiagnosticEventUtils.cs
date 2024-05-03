// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Azure.Core.Pipeline;
using System.Text.Json;
using System.Diagnostics.Tracing;

namespace Azure.AI.OpenAI.Custom.Internal
{
    internal static class DiagnosticEventUtils
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new JsonSerializerOptions()
        {
            IgnoreNullValues = true,
        };

        public static void RecordPrompt(DiagnosticScope scope, string prompt, bool recordContent)
        {
            var sanitizedPrompt = Sanitize(prompt, recordContent);
            scope.Activity.AddEvent(new ActivityEvent("gen_ai.user.message",
                default,
                new ActivityTagsCollection
                {
                    { "event.data", JsonSerializer.Serialize(new { content = sanitizedPrompt }, s_jsonSerializerOptions) },
                    { "gen_ai.system", "openai"}
                }));

            // failed experiment, Otel .NET logging does not support structured bodies.
            OpenAIEventSource.Log.RecordPrompt("gen_ai.user.message", "openai", new KeyValuePair<string, string>("content", sanitizedPrompt));
        }

        public static void RecordRequestMessage(DiagnosticScope scope, ChatRequestMessage message, bool recordContent)
        {
            // TODO we should probably do EventSource => ILogger instead
            string eventName = "gen_ai.message";

            // TODO optimize
            object payload = null;
            if (message is ChatRequestSystemMessage systemMessage)
            {
                eventName = "gen_ai.system.message";
                payload = new { content = Sanitize(systemMessage.Content, recordContent) };
            }
            else if (message is ChatRequestUserMessage userMessage)
            {
                eventName = "gen_ai.user.message";
                payload = new { content = GetSanitizedUserMessage(userMessage, recordContent) };
            }
            else if (message is ChatRequestToolMessage toolMessage)
            {
                eventName = "gen_ai.tool.message";
                payload = new
                {
                    content = Sanitize(toolMessage.Content, recordContent),
                    tool_call_id = toolMessage.ToolCallId
                };
            }
            else if (message is ChatRequestFunctionMessage functionMessage)
            {
                eventName = "gen_ai.function.message";
                payload = new
                {
                    content = Sanitize(functionMessage.Content, recordContent),
                };
            }
            else if (message is ChatRequestAssistantMessage assistantMessage)
            {
                eventName = "gen_ai.assistant.message";
                payload = new
                {
                    content = Sanitize(assistantMessage.Content, recordContent),
                    tool_calls = GetSanitizedToolCalls(assistantMessage.ToolCalls, recordContent)
                };
            }

            scope.Activity?.AddEvent(new ActivityEvent(eventName, default, new ActivityTagsCollection
            {
                { "event.data", JsonSerializer.Serialize(payload, s_jsonSerializerOptions) },
                { "gen_ai.system", "openai"}
            }));
        }

        private static object GetSanitizedUserMessage(ChatRequestUserMessage userMessage, bool recordContent)
        {
            if (userMessage.Content != null)
            {
                return Sanitize(userMessage.Content, recordContent);
            }

            if (userMessage.MultimodalContentItems != null && userMessage.MultimodalContentItems.Any())
            {
                return userMessage.MultimodalContentItems.Select(m =>
                {
                    if (m is ChatMessageTextContentItem textMessage)
                    {
                        return new
                        {
                            type = "text",
                            content = Sanitize(textMessage.Text, recordContent)
                        };
                    }
                    else if (m is ChatMessageImageContentItem image)
                    {
                        return (object)new
                        {
                            type = "image",
                            detail_level = image.ImageUrl?.Detail?.ToString(),
                            content = Sanitize(image.ImageUrl?.Url?.OriginalString, recordContent)
                        };
                    }
                    return null;
                });
            }

            return null;
        }

        public static void RecordChoice(DiagnosticScope scope, int? index, CompletionsFinishReason? finishReason, ChatRole? role, string content,
    IEnumerable<ChatCompletionsToolCall> toolCalls, bool recordContent)
        {
            // TODO we should probably do EventSource => ILogger instead
            string eventName = "gen_ai.choice";

            object payload = new
            {
                index = index,
                finish_reason = finishReason?.ToString(),
                message = new
                {
                    role = role?.ToString(),
                    content = Sanitize(content, recordContent),
                    tool_calls = GetSanitizedToolCalls(toolCalls, recordContent)
                }
            };
            scope.Activity.AddEvent(new ActivityEvent(eventName, default, new ActivityTagsCollection
            {
                { "event.data", JsonSerializer.Serialize(payload, s_jsonSerializerOptions) },
                { "gen_ai.system", "openai"}
            }));
        }

        private static List<object> GetSanitizedToolCalls(IEnumerable<ChatCompletionsToolCall> calls, bool recordContent)
        {
            if (calls == null || !calls.Any())
            {
                return null;
            }
            List<object> toolCalls = new List<object>();
            foreach (ChatCompletionsToolCall call in calls)
            {
                toolCalls.Add(new
                {
                    id = call.Id,
                    type = call.Type,
                    function = (call is ChatCompletionsFunctionToolCall funcCall) ? new
                    {
                        name = funcCall.Name,
                        arguments = Sanitize(funcCall.Arguments, recordContent)
                    } : null
                });
            }
            return toolCalls;
        }

        private static string Sanitize(string content, bool recordContent)
        {
            if (content == null)
            {
                return null;
            }

            return recordContent ? content : "REDACTED";
        }
    }
}
