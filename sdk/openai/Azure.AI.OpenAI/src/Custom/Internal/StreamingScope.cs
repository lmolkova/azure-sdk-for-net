// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Text;
using System.Threading;
using Azure.Core.Pipeline;

namespace Azure.AI.OpenAI.Custom.Internal
{
    internal class StreamingScope<T> : IDisposable
    {
        private string _responseModel;
        private readonly InstrumentationScope _scope;
        private readonly bool _recordContent;
        private readonly ChoiceBuffer[] _buffers;
        private string _responseId;
        private int _numberOfChoices;
        private int _reported;

        public StreamingScope(InstrumentationScope scope, bool recordContent, int numberOfChoices)
        {
            _scope = scope;
            _recordContent = recordContent;
            _numberOfChoices = numberOfChoices;
            _buffers = new ChoiceBuffer[numberOfChoices];
            for (int i = 0; i < numberOfChoices; i++)
            {
                _buffers[i] = new ChoiceBuffer(i, _recordContent);
            }

            _scope.RecordStreamStart();
        }

        public void RecordChunk(T chunk)
        {
            if (chunk is StreamingChatCompletionsUpdate chatChunk)
            {
                if (chatChunk.ChoiceIndex != null)
                {
                    if (chatChunk.Model != null && _responseModel == null)
                    {
                        _responseModel = chatChunk.Model;
                    }

                    if (chatChunk.Id != null)
                    {
                        _responseId = chatChunk.Id;
                    }

                    var buffer = _buffers[chatChunk.ChoiceIndex.Value];
                    buffer.AddChunk(chatChunk);
                    if (chatChunk.FinishReason != null)
                    {
                        buffer.RecordChoice(_scope.DiagnosticScope);
                    }
                }
            }
            else if (chunk is Completions completionsChunk)
            {
                _responseId = completionsChunk?.Id;

                foreach (var choice in completionsChunk.Choices)
                {
                    var buffer = _buffers[choice.Index];
                    buffer.AddChunk(choice);

                    if (choice.FinishReason != null)
                    {
                        buffer.RecordChoice(_scope.DiagnosticScope);
                    }
                }
            }

            //_scope.RecordStreamChunk(_responseModel);

            if (_numberOfChoices == _buffers.Count(b => b.IsRecorded))
            {
                EndScope(null, false);
            }
        }

        public void RecordException(Exception ex)
        {
            EndScope(ex, false);
        }

        public void RecordCancellation()
        {
            EndScope(null, true);
        }

        private void EndScope(Exception ex, bool canceled)
        {
            if (Interlocked.Exchange(ref _reported, 1) == 0)
            {
                _scope.RecordStreamComplete();
                string firstFinishReason = _buffers.FirstOrDefault()?.FinishReason?.ToString();
                foreach (var buffer in _buffers.Where(b => !b.IsRecorded))
                {
                    buffer.RecordChoice(_scope.DiagnosticScope);
                }

                _scope.RecordStreamingResponse(_responseId, _responseModel, firstFinishReason, _buffers.Sum(b => b.CompletionsTokenCount), ex, canceled);
                _scope.Dispose();
            }
        }

        public void Dispose()
        {
            EndScope(null, false);
        }

        private class ChoiceBuffer
        {
            private readonly int _index;
            private readonly bool _recordContent;
            private ChatRole _role;
            private StringBuilder _content;

            public ChoiceBuffer(int index, bool recordContent)
            {
                _index = index;
                _recordContent = recordContent;
                if (recordContent)
                {
                    _content = new StringBuilder();
                }
            }

            public bool IsRecorded { get; private set; } = false;
            public int CompletionsTokenCount { get; private set; } = 0;
            public CompletionsFinishReason? FinishReason { get; private set; } = null;

            public void AddChunk(Choice chunk)
            {
                // TODO: can SSE come concurrently?
                if (FinishReason != null || IsRecorded)
                {
                    // should not be, todo log
                    return;
                }

                if (chunk.Text != null)
                {
                    CompletionsTokenCount++;
                    _content?.Append(chunk.Text);
                }

                if (chunk.FinishReason != null)
                {
                    FinishReason = chunk.FinishReason.Value;
                }
            }

            public void AddChunk(StreamingChatCompletionsUpdate chunk)
            {
                // TODO: can SSE come concurrently?
                if (FinishReason != null || IsRecorded)
                {
                    // should not be, todo log
                    return;
                }

                if (chunk.Role != null)
                {
                    _role = chunk.Role.Value;
                }

                if (chunk.ContentUpdate != null)
                {
                    CompletionsTokenCount++;
                    _content?.Append(chunk.ContentUpdate);
                }

                if (chunk.FinishReason != null)
                {
                    FinishReason = chunk.FinishReason.Value;
                }
            }

            public void RecordChoice(DiagnosticScope scope)
            {
                if (!IsRecorded)
                {
                    // thread safety?
                    IsRecorded = true;
                    DiagnosticEventUtils.RecordChoice(scope, _index, FinishReason, _role, _content?.ToString(), null, _recordContent);
                }
            }
        }
    }
}
