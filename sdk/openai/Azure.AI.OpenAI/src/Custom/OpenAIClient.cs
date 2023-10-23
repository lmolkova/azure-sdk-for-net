// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Azure.AI.OpenAI
{
    /// <summary> Azure OpenAI APIs for completions and search. </summary>
    [CodeGenSuppress("GetCompletions", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("GetCompletionsAsync", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("GetChatCompletions", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("GetChatCompletionsAsync", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("GetEmbeddings", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("GetEmbeddingsAsync", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("CreateGetCompletionsRequest", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("CreateGetChatCompletionsRequest", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    [CodeGenSuppress("CreateGetEmbeddingsRequest", typeof(string), typeof(RequestContent), typeof(RequestContext))]
    public partial class OpenAIClient
    {
        private const string UsageTypeAttribute = "openai.usage.type";
        private const string FinishReasonAttribute = "openai.choice.finish_reason";
        private const string ModelNameAttribute = "openai.model";
        private const string ServerAddressAttribute = "server.address";
        private const string TokenUsageTypePrompt = "prompt";
        private const string TokenUsageTypeCompletion = "completion";

        private const int DefaultMaxCompletionsTokens = 100;
        private const string PublicOpenAIApiVersion = "1";
        private const string PublicOpenAIEndpoint = $"https://api.openai.com/v{PublicOpenAIApiVersion}";

        private bool _isConfiguredForAzureOpenAI = true;

        private static readonly Meter s_meter;
        private static readonly Counter<long> s_chatCompletionsTokens;
        private static readonly Counter<long> s_chatCompletionsChoices;
        private static readonly Counter<long> s_completionsTokens;
        private static readonly Counter<long> s_completionsChoices;
        private static readonly Counter<long> s_embeddingsTokens;
        private static readonly Counter<long> s_embeddingsDataCount;
        private static readonly Histogram<double> s_chatCompletionsDuration;
        private static readonly Histogram<double> s_completionsDuration;
        private static readonly Histogram<double> s_embeddingsDuration;
        private static readonly Histogram<double> s_imageGenerationsDuration;

        static OpenAIClient()
        {
            s_meter = new Meter("Azure.AI.OpenAI");
            s_chatCompletionsTokens = s_meter.CreateCounter<long>("openai.chat_completions.tokens", "{token}", "Number of tokens used");
            s_chatCompletionsChoices = s_meter.CreateCounter<long>("openai.chat_completions.choices", "{choice}", "Number of choices returned");
            s_chatCompletionsDuration = s_meter.CreateHistogram<double>("openai.chat_completions.duration", "seconds", "duration of the getchatcompletions call");

            s_completionsTokens = s_meter.CreateCounter<long>("openai.completions.tokens", "{token}", "Number of tokens used");
            s_completionsChoices = s_meter.CreateCounter<long>("openai.completions.choices", "{choice}", "Number of choices returned");
            s_completionsDuration = s_meter.CreateHistogram<double>("openai.completions.duration", "seconds", "duration of the getcompletions call");

            s_embeddingsTokens = s_meter.CreateCounter<long>("openai.embeddings.tokens", "{token}", "Number of tokens used");
            s_embeddingsDataCount = s_meter.CreateCounter<long>("openai.embeddings.vector_size", "{element}", "Number of vector elements returned");
            s_embeddingsDuration = s_meter.CreateHistogram<double>("openai.embeddings.duration", "seconds", "duration of the getembeddings call");

            s_imageGenerationsDuration = s_meter.CreateHistogram<double>("openai.image_generations.duration", "seconds", "duration of the getimagegenerations call");
        }

        /// <summary>
        ///     Initializes a instance of OpenAIClient for use with an Azure OpenAI resource.
        /// </summary>
        ///  <param name="endpoint">
        ///     The URI for an Azure OpenAI resource as retrieved from, for example, Azure Portal.
        ///     This should include protocol and hostname. An example could be:
        ///     https://my-resource.openai.azure.com .
        /// </param>
        /// <param name="keyCredential"> A key credential used to authenticate to an Azure OpenAI resource. </param>
        /// <param name="options"> The options for configuring the client. </param>
        /// <remarks>
        ///     <see cref="OpenAIClient"/> objects initialized with this constructor can only be used with Azure OpenAI
        ///     resources. To use <see cref="OpenAIClient"/> with the non-Azure OpenAI inference endpoint, use a
        ///     constructor that accepts a non-Azure OpenAI API key, instead.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="endpoint"/> or <paramref name="keyCredential"/> is null.
        /// </exception>
        public OpenAIClient(Uri endpoint, AzureKeyCredential keyCredential, OpenAIClientOptions options)
        {
            Argument.AssertNotNull(endpoint, nameof(endpoint));
            Argument.AssertNotNull(keyCredential, nameof(keyCredential));
            options ??= new OpenAIClientOptions();

            ClientDiagnostics = new ClientDiagnostics(options, true);
            _keyCredential = keyCredential;
            _pipeline = HttpPipelineBuilder.Build(options, Array.Empty<HttpPipelinePolicy>(), new HttpPipelinePolicy[] { new AzureKeyCredentialPolicy(_keyCredential, AuthorizationHeader) }, new ResponseClassifier());
            _endpoint = endpoint;
            _apiVersion = options.Version;
        }

        /// <inheritdoc cref="OpenAIClient(Uri, AzureKeyCredential, OpenAIClientOptions)"/>
        public OpenAIClient(Uri endpoint, AzureKeyCredential keyCredential)
            : this(endpoint, keyCredential, new OpenAIClientOptions())
        {
        }

        /// <summary>
        ///     <inheritdoc
        ///         cref="OpenAIClient(Uri, AzureKeyCredential, OpenAIClientOptions)"
        ///         path="/summary"/>
        /// </summary>
        /// <param name="endpoint">
        ///     <inheritdoc
        ///         cref="OpenAIClient(Uri, AzureKeyCredential, OpenAIClientOptions)"
        ///         path="/param[@name='endpoint']"/>
        /// </param>
        /// <param name="options">
        ///     <inheritdoc
        ///         cref="OpenAIClient(Uri, AzureKeyCredential, OpenAIClientOptions)"
        ///         path="/param[@name='options']"/>
        /// </param>
        /// <param name="tokenCredential"> A token credential used to authenticate with an Azure OpenAI resource. </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="endpoint"/> or <paramref name="tokenCredential"/> is null.
        /// </exception>
        public OpenAIClient(Uri endpoint, TokenCredential tokenCredential, OpenAIClientOptions options)
        {
            Argument.AssertNotNull(endpoint, nameof(endpoint));
            Argument.AssertNotNull(tokenCredential, nameof(tokenCredential));
            options ??= new OpenAIClientOptions();

            ClientDiagnostics = new ClientDiagnostics(options, true);
            _tokenCredential = tokenCredential;
            _pipeline = HttpPipelineBuilder.Build(
                options,
                Array.Empty<HttpPipelinePolicy>(),
                new HttpPipelinePolicy[] {
                    new BearerTokenAuthenticationPolicy(_tokenCredential, AuthorizationScopes)
                },
                new ResponseClassifier());
            _endpoint = endpoint;
            _apiVersion = options.Version;
        }

        /// <inheritdoc cref="OpenAIClient(Uri, TokenCredential, OpenAIClientOptions)"/>
        public OpenAIClient(Uri endpoint, TokenCredential tokenCredential)
            : this(endpoint, tokenCredential, new OpenAIClientOptions())
        {
        }

        /// <summary>
        ///     Initializes a instance of OpenAIClient for use with the non-Azure OpenAI endpoint.
        /// </summary>
        /// <param name="openAIApiKey">
        ///     The API key to use when connecting to the non-Azure OpenAI endpoint.
        /// </param>
        /// <param name="options"> The options for configuring the client. </param>
        /// <remarks>
        ///     <see cref="OpenAIClient"/> objects initialized with this constructor can only be used with the
        ///     non-Azure OpenAI inference endpoint. To use <see cref="OpenAIClient"/> with an Azure OpenAI resource,
        ///     use a constructor that accepts a resource URI and Azure authentication credential, instead.
        /// </remarks>
        /// <exception cref="ArgumentNullException"> <paramref name="openAIApiKey"/> is null. </exception>
        public OpenAIClient(string openAIApiKey, OpenAIClientOptions options)
            : this(new Uri(PublicOpenAIEndpoint), CreateDelegatedToken(openAIApiKey), options)
        {
            _isConfiguredForAzureOpenAI = false;
        }

        /// <inheritdoc cref="OpenAIClient(string, OpenAIClientOptions)"/>
        public OpenAIClient(string openAIApiKey)
            : this(new Uri(PublicOpenAIEndpoint), CreateDelegatedToken(openAIApiKey), new OpenAIClientOptions())
        {
            _isConfiguredForAzureOpenAI = false;
        }

        /// <summary> Return textual completions as configured for a given prompt. </summary>
        /// <param name="deploymentOrModelName">
        ///     Specifies either the model deployment name (when using Azure OpenAI) or model name (when using
        ///     non-Azure OpenAI) to use for this request.
        /// </param>
        /// <param name="completionsOptions">
        ///     The options for this completions request.
        /// </param>
        /// <param name="cancellationToken"> The cancellation token to use. </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="deploymentOrModelName"/> or <paramref name="completionsOptions"/> is null.
        /// </exception>
        public virtual Response<Completions> GetCompletions(
            string deploymentOrModelName,
            CompletionsOptions completionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(completionsOptions, nameof(completionsOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetCompletions");
            scope.Start();
            EnrichDiagnosticScope(scope, deploymentOrModelName, completionsOptions);
            TagList tags = CommonMetricsTags(deploymentOrModelName);

            completionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            completionsOptions.InternalShouldStreamResponse = null;

            RequestContent content = completionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                using HttpMessage message = CreatePostRequestMessage(deploymentOrModelName, "completions", content, context);
                Response response = _pipeline.ProcessMessage(message, context, cancellationToken);
                var completions = Completions.FromResponse(response);
                ReportCompletionsTelemetry(scope, completions, tags, stopwatch.Elapsed);
                return Response.FromValue(completions, response);
            }
            catch (Exception e)
            {
                ReportError(s_completionsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        /// <inheritdoc cref="GetCompletions(string, CompletionsOptions, CancellationToken)"/>
        public virtual Response<Completions> GetCompletions(
            string deploymentOrModelName,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(prompt, nameof(prompt));
            CompletionsOptions simpleOptions = GetDefaultCompletionsOptions(prompt);
            return GetCompletions(deploymentOrModelName, simpleOptions, cancellationToken);
        }

        /// <inheritdoc cref="GetCompletions(string, CompletionsOptions, CancellationToken)"/>
        public virtual async Task<Response<Completions>> GetCompletionsAsync(
            string deploymentOrModelName,
            CompletionsOptions completionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(completionsOptions, nameof(completionsOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetCompletions");
            scope.Start();
            EnrichDiagnosticScope(scope, deploymentOrModelName, completionsOptions);
            TagList tags = CommonMetricsTags(deploymentOrModelName);

            completionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            completionsOptions.InternalShouldStreamResponse = null;

            RequestContent content = completionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                using HttpMessage message = CreatePostRequestMessage(deploymentOrModelName, "completions", content, context);
                Response response = await _pipeline.ProcessMessageAsync(message, context, cancellationToken)
                    .ConfigureAwait(false);
                var completions = Completions.FromResponse(response);
                ReportCompletionsTelemetry(scope, completions, tags, stopwatch.Elapsed);
                return Response.FromValue(completions, response);
            }
            catch (Exception e)
            {
                ReportError(s_completionsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        /// <inheritdoc cref="GetCompletions(string, CompletionsOptions, CancellationToken)"/>
        public virtual Task<Response<Completions>> GetCompletionsAsync(
            string deploymentOrModelName,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(prompt, nameof(prompt));
            CompletionsOptions simpleOptions = GetDefaultCompletionsOptions(prompt);
            return GetCompletionsAsync(deploymentOrModelName, simpleOptions, cancellationToken);
        }

        /// <summary>
        ///     Begin a completions request and get an object that can stream response data as it becomes available.
        /// </summary>
        /// <param name="deploymentOrModelName">
        ///     <inheritdoc
        ///         cref="GetCompletions(string, CompletionsOptions, CancellationToken)"
        ///         path="/param[@name='deploymentOrModelName']" />
        /// </param>
        /// <param name="completionsOptions"> the chat completions options for this completions request. </param>
        /// <param name="cancellationToken">
        ///     a cancellation token that can be used to cancel the initial request or ongoing streaming operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="deploymentOrModelName"/> or <paramref name="completionsOptions"/> is null.
        /// </exception>
        /// <exception cref="RequestFailedException"> Service returned a non-success status code. </exception>
        /// <returns>
        /// A response that, if the request was successful, includes a <see cref="StreamingCompletions"/> instance.
        /// </returns>
        public virtual Response<StreamingCompletions> GetCompletionsStreaming(
            string deploymentOrModelName,
            CompletionsOptions completionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(completionsOptions, nameof(completionsOptions));

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetCompletionsStreaming");
            scope.Start();

            completionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            completionsOptions.InternalShouldStreamResponse = true;

            RequestContent content = completionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                // Response value object takes IDisposable ownership of message
                HttpMessage message = CreatePostRequestMessage(
                    deploymentOrModelName,
                    "completions",
                    content,
                    context);
                message.BufferResponse = false;
                Response baseResponse = _pipeline.ProcessMessage(message, context, cancellationToken);
                return Response.FromValue(new StreamingCompletions(baseResponse), baseResponse);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <inheritdoc cref="GetCompletionsStreaming(string, CompletionsOptions, CancellationToken)"/>
        public virtual async Task<Response<StreamingCompletions>> GetCompletionsStreamingAsync(
            string deploymentOrModelName,
            CompletionsOptions completionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(completionsOptions, nameof(completionsOptions));

            completionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            completionsOptions.InternalShouldStreamResponse = true;

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetCompletionsStreaming");
            scope.Start();

            RequestContent content = completionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                // Response value object takes IDisposable ownership of message
                HttpMessage message = CreatePostRequestMessage(
                    deploymentOrModelName,
                    "completions",
                    content,
                    context);
                message.BufferResponse = false;
                Response baseResponse = await _pipeline.ProcessMessageAsync(message, context, cancellationToken)
                    .ConfigureAwait(false);
                return Response.FromValue(new StreamingCompletions(baseResponse), baseResponse);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary> Get chat completions for provided chat context messages. </summary>
        /// <param name="deploymentOrModelName">
        /// <inheritdoc
        ///     cref="GetCompletions(string, CompletionsOptions, CancellationToken)"
        ///     path="/param[@name='deploymentOrModelName']"/>
        /// </param>
        /// <param name="chatCompletionsOptions"> The options for this chat completions request. </param>
        /// <param name="cancellationToken"> The cancellation token to use. </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="deploymentOrModelName"/> or <paramref name="chatCompletionsOptions"/> is null.
        /// </exception>
        public virtual Response<ChatCompletions> GetChatCompletions(
            string deploymentOrModelName,
            ChatCompletionsOptions chatCompletionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(chatCompletionsOptions, nameof(chatCompletionsOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetChatCompletions");
            scope.Start();
            EnrichDiagnosticScope(scope, deploymentOrModelName, chatCompletionsOptions);
            TagList tags = CommonMetricsTags(deploymentOrModelName);

            chatCompletionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            chatCompletionsOptions.InternalShouldStreamResponse = null;

            string operationPath = GetOperationPath(chatCompletionsOptions);

            RequestContent content = chatCompletionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                using HttpMessage message = CreatePostRequestMessage(
                    deploymentOrModelName,
                    operationPath,
                    content,
                    context);
                Response response = _pipeline.ProcessMessage(message, context, cancellationToken);
                var completions = ChatCompletions.FromResponse(response);

                ReportChatCompletionsTelemetry(scope, completions, tags, stopwatch.Elapsed);
                return Response.FromValue(completions, response);
            }
            catch (Exception e)
            {
                ReportError(s_chatCompletionsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        /// <inheritdoc cref="GetChatCompletions(string, ChatCompletionsOptions, CancellationToken)"/>
        public virtual async Task<Response<ChatCompletions>> GetChatCompletionsAsync(
            string deploymentOrModelName,
            ChatCompletionsOptions chatCompletionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(chatCompletionsOptions, nameof(chatCompletionsOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetChatCompletions");
            scope.Start();
            EnrichDiagnosticScope(scope, deploymentOrModelName, chatCompletionsOptions);
            TagList tags = CommonMetricsTags(deploymentOrModelName);

            chatCompletionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            chatCompletionsOptions.InternalShouldStreamResponse = null;

            string operationPath = GetOperationPath(chatCompletionsOptions);

            RequestContent content = chatCompletionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                using HttpMessage message = CreatePostRequestMessage(
                    deploymentOrModelName,
                    operationPath,
                    content,
                    context);
                Response response = await _pipeline.ProcessMessageAsync(message, context, cancellationToken)
                    .ConfigureAwait(false);
                var completions = ChatCompletions.FromResponse(response);
                ReportChatCompletionsTelemetry(scope, completions, tags, stopwatch.Elapsed);
                return Response.FromValue(completions, response);
            }
            catch (Exception e)
            {
                ReportError(s_chatCompletionsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        ///     Begin a chat completions request and get an object that can stream response data as it becomes
        ///     available.
        /// </summary>
        /// <param name="deploymentOrModelName">
        ///     <inheritdoc
        ///         cref="GetCompletions(string, CompletionsOptions, CancellationToken)"
        ///         path="/param[@name='deploymentOrModelName']"/>
        /// </param>
        /// <param name="chatCompletionsOptions">
        ///     the chat completions options for this chat completions request.
        /// </param>
        /// <param name="cancellationToken">
        ///     a cancellation token that can be used to cancel the initial request or ongoing streaming operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="deploymentOrModelName"/> or <paramref name="chatCompletionsOptions"/> is null.
        /// </exception>
        /// <exception cref="RequestFailedException"> Service returned a non-success status code. </exception>
        /// <returns> The response returned from the service. </returns>
        public virtual Response<StreamingChatCompletions> GetChatCompletionsStreaming(
            string deploymentOrModelName,
            ChatCompletionsOptions chatCompletionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(chatCompletionsOptions, nameof(chatCompletionsOptions));

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetChatCompletionsStreaming");
            scope.Start();

            chatCompletionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            chatCompletionsOptions.InternalShouldStreamResponse = true;

            string operationPath = GetOperationPath(chatCompletionsOptions);

            RequestContent content = chatCompletionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                // Response value object takes IDisposable ownership of message
                HttpMessage message = CreatePostRequestMessage(
                    deploymentOrModelName,
                    operationPath,
                    content,
                    context);
                message.BufferResponse = false;
                Response baseResponse = _pipeline.ProcessMessage(message, context, cancellationToken);
                return Response.FromValue(new StreamingChatCompletions(baseResponse), baseResponse);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <inheritdoc cref="GetChatCompletionsStreaming(string, ChatCompletionsOptions, CancellationToken)"/>
        public virtual async Task<Response<StreamingChatCompletions>> GetChatCompletionsStreamingAsync(
            string deploymentOrModelName,
            ChatCompletionsOptions chatCompletionsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(chatCompletionsOptions, nameof(chatCompletionsOptions));

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetChatCompletionsStreaming");
            scope.Start();

            chatCompletionsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;
            chatCompletionsOptions.InternalShouldStreamResponse = true;

            string operationPath = GetOperationPath(chatCompletionsOptions);

            RequestContent content = chatCompletionsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                // Response value object takes IDisposable ownership of message
                HttpMessage message = CreatePostRequestMessage(
                    deploymentOrModelName,
                    operationPath,
                    content,
                    context);
                message.BufferResponse = false;
                Response baseResponse = await _pipeline.ProcessMessageAsync(
                    message,
                    context,
                    cancellationToken).ConfigureAwait(false);
                return Response.FromValue(new StreamingChatCompletions(baseResponse), baseResponse);
            }
            catch (Exception e)
            {
                scope.Failed(e);
                throw;
            }
        }

        /// <summary> Return the computed embeddings for a given prompt. </summary>
        /// <param name="deploymentOrModelName">
        ///     <inheritdoc
        ///         cref="GetCompletions(string, CompletionsOptions, CancellationToken)"
        ///         path="/param[@name='deploymentOrModelName']"/>
        /// </param>
        /// <param name="embeddingsOptions"> The options for this embeddings request. </param>
        /// <param name="cancellationToken"> The cancellation token to use. </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="deploymentOrModelName"/> or <paramref name="embeddingsOptions"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="deploymentOrModelName"/> is an empty string and was expected to be non-empty.
        /// </exception>
        public virtual Response<Embeddings> GetEmbeddings(
            string deploymentOrModelName,
            EmbeddingsOptions embeddingsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(embeddingsOptions, nameof(embeddingsOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetEmbeddings");
            scope.Start();
            EnrichDiagnosticScope(scope, deploymentOrModelName, embeddingsOptions);
            TagList tags = CommonMetricsTags(deploymentOrModelName);

            embeddingsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;

            RequestContent content = embeddingsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                HttpMessage message = CreatePostRequestMessage(deploymentOrModelName, "embeddings", content, context);
                Response response = _pipeline.ProcessMessage(message, context, cancellationToken);

                var embeddings = Embeddings.FromResponse(response);
                ReportEmbeddingsMetrics(scope, embeddings, tags, stopwatch.Elapsed);
                return Response.FromValue(embeddings, response);
            }
            catch (Exception e)
            {
                ReportError(s_embeddingsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        /// <inheritdoc cref="GetEmbeddings(string, EmbeddingsOptions, CancellationToken)"/>
        public virtual async Task<Response<Embeddings>> GetEmbeddingsAsync(
            string deploymentOrModelName,
            EmbeddingsOptions embeddingsOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(deploymentOrModelName, nameof(deploymentOrModelName));
            Argument.AssertNotNull(embeddingsOptions, nameof(embeddingsOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetEmbeddings");
            scope.Start();
            EnrichDiagnosticScope(scope, deploymentOrModelName, embeddingsOptions);
            TagList tags = CommonMetricsTags(deploymentOrModelName);

            embeddingsOptions.InternalNonAzureModelName = _isConfiguredForAzureOpenAI ? null : deploymentOrModelName;

            RequestContent content = embeddingsOptions.ToRequestContent();
            RequestContext context = FromCancellationToken(cancellationToken);

            try
            {
                HttpMessage message = CreatePostRequestMessage(deploymentOrModelName, "embeddings", content, context);
                Response response = await _pipeline.ProcessMessageAsync(message, context, cancellationToken)
                    .ConfigureAwait(false);

                var embeddings = Embeddings.FromResponse(response);
                ReportEmbeddingsMetrics(scope, embeddings, tags, stopwatch.Elapsed);
                return Response.FromValue(embeddings, response);
            }
            catch (Exception e)
            {
                ReportError(s_embeddingsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        ///     Get a set of generated images influenced by a provided textual prompt.
        /// </summary>
        /// <param name="imageGenerationOptions">
        ///     The configuration information for the image generation request that controls the content,
        ///     size, and other details about generated images.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that may be used to abort an ongoing request.
        /// </param>
        /// <returns>
        ///     The response information for the image generations request.
        /// </returns>
        public virtual Response<ImageGenerations> GetImageGenerations(
            ImageGenerationOptions imageGenerationOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(imageGenerationOptions, nameof(imageGenerationOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetImageGenerations");
            scope.Start();
            EnrichDiagnosticScope(scope, imageGenerationOptions);
            TagList tags = CommonMetricsTags(string.Empty);

            try
            {
                Response rawResponse = default;
                ImageGenerations responseValue = default;

                if (_isConfiguredForAzureOpenAI)
                {
                    Operation<BatchImageGenerationOperationResponse> imagesOperation
                        = BeginAzureBatchImageGeneration(
                            WaitUntil.Completed,
                            imageGenerationOptions,
                            cancellationToken);

                    rawResponse = imagesOperation.GetRawResponse();
                    BatchImageGenerationOperationResponse operationResponse = imagesOperation.Value;

                    responseValue = operationResponse.Result;
                }
                else
                {
                    RequestContext context = FromCancellationToken(cancellationToken);
                    HttpMessage message = CreatePostRequestMessage(
                        string.Empty,
                        "images/generations",
                        content: imageGenerationOptions.ToRequestContent(),
                        context);
                    rawResponse = _pipeline.ProcessMessage(message, context, cancellationToken);

                    responseValue = ImageGenerations.FromResponse(rawResponse);
                }

                ReportImageGenerationMetrics(scope, responseValue, tags, stopwatch.Elapsed);
                return Response.FromValue(responseValue, rawResponse);
            }
            catch (Exception e)
            {
                ReportError(s_imageGenerationsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        /// <summary>
        ///     Get a set of generated images influenced by a provided textual prompt.
        /// </summary>
        /// <param name="imageGenerationOptions">
        ///     The configuration information for the image generation request that controls the content,
        ///     size, and other details about generated images.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that may be used to abort an ongoing request.
        /// </param>
        /// <returns>
        ///     The response information for the image generations request.
        /// </returns>
        public virtual async Task<Response<ImageGenerations>> GetImageGenerationsAsync(
            ImageGenerationOptions imageGenerationOptions,
            CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(imageGenerationOptions, nameof(imageGenerationOptions));
            Stopwatch stopwatch = Stopwatch.StartNew();

            using DiagnosticScope scope = ClientDiagnostics.CreateScope("OpenAIClient.GetImageGenerations");
            scope.Start();
            EnrichDiagnosticScope(scope, imageGenerationOptions);
            TagList tags = CommonMetricsTags(string.Empty);

            try
            {
                Response rawResponse = default;
                ImageGenerations responseValue = default;

                if (_isConfiguredForAzureOpenAI)
                {
                    Operation<BatchImageGenerationOperationResponse> imagesOperation
                        = await BeginAzureBatchImageGenerationAsync(
                            WaitUntil.Completed,
                            imageGenerationOptions,
                            cancellationToken)
                        .ConfigureAwait(false);

                    rawResponse = imagesOperation.GetRawResponse();
                    BatchImageGenerationOperationResponse operationResponse = imagesOperation.Value;

                    responseValue = operationResponse.Result;
                }
                else
                {
                    RequestContext context = FromCancellationToken(cancellationToken);
                    HttpMessage message = CreatePostRequestMessage(
                        string.Empty,
                        "images/generations",
                        content: imageGenerationOptions.ToRequestContent(),
                        context);
                    rawResponse = await _pipeline.ProcessMessageAsync(message, context, cancellationToken)
                        .ConfigureAwait(false);
                    responseValue = ImageGenerations.FromResponse(rawResponse);
                }
                ReportImageGenerationMetrics(scope, responseValue, tags, stopwatch.Elapsed);
                return Response.FromValue(responseValue, rawResponse);
            }
            catch (Exception e)
            {
                ReportError(s_imageGenerationsDuration, tags, stopwatch.Elapsed, e);
                scope.Failed(e);
                throw;
            }
        }

        internal RequestUriBuilder GetUri(string deploymentOrModelName, string operationPath)
        {
            var uri = new RawRequestUriBuilder();
            uri.Reset(_endpoint);
            if (_isConfiguredForAzureOpenAI)
            {
                uri.AppendRaw("/openai", false);
                uri.AppendPath("/deployments/", false);
                uri.AppendPath(deploymentOrModelName, true);
                uri.AppendPath($"/{operationPath}", false);
                uri.AppendQuery("api-version", _apiVersion, true);
            }
            else
            {
                uri.AppendPath($"/{operationPath}", false);
            }
            return uri;
        }

        internal HttpMessage CreatePostRequestMessage(
            string deploymentOrModelName,
            string operationPath,
            RequestContent content,
            RequestContext context)
        {
            HttpMessage message = _pipeline.CreateMessage(context, ResponseClassifier200);
            Request request = message.Request;
            request.Method = RequestMethod.Post;
            request.Uri = GetUri(deploymentOrModelName, operationPath);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Content-Type", "application/json");
            request.Content = content;
            return message;
        }

        private static TokenCredential CreateDelegatedToken(string token)
        {
            var accessToken = new AccessToken(token, DateTimeOffset.Now.AddDays(180));
            return DelegatedTokenCredential.Create((_, _) => accessToken);
        }

        private static CompletionsOptions GetDefaultCompletionsOptions(string prompt)
        {
            return new CompletionsOptions()
            {
                Prompts =
                {
                    prompt,
                },
                MaxTokens = DefaultMaxCompletionsTokens,
            };
        }

        private static string GetOperationPath(ChatCompletionsOptions chatCompletionsOptions)
            => chatCompletionsOptions.AzureExtensionsOptions != null
                ? "extensions/chat/completions"
                : "chat/completions";

#nullable enable

        private TagList CommonMetricsTags(string model)
        {
            TagList tags = default;
            tags.Add(ModelNameAttribute, model);
            tags.Add(ServerAddressAttribute, _endpoint.Host);
            return tags;
        }

        private void ReportChatCompletionsTelemetry(DiagnosticScope scope, ChatCompletions completions, TagList tags, TimeSpan duration)
        {
            if (scope.IsEnabled)
            {
                var filterResults = completions.PromptFilterResults.Select(PromptFilterResultsToString);
                if (filterResults.Any())
                {
                    scope.AddArrayAttribute("openai.azure.chat_completions.response.filter_results", filterResults.ToArray());
                }

                var finishReasons = completions.Choices.Select(c => c.FinishReason.ToString()).ToArray();
                scope.AddAttribute("openai.chat_completions.response.id", completions.Id);
                AddCreatedAttribute(scope, "openai.chat_completions.response.created_at", completions.Created);
                scope.AddIntegerAttribute("openai.usage.prompt_tokens", completions.Usage.PromptTokens);
                scope.AddIntegerAttribute("openai.usage.completion_tokens", completions.Usage.CompletionTokens);
                scope.AddArrayAttribute("openai.choices.finish_reasons", finishReasons);
            }

            // before tags are modified
            s_chatCompletionsDuration.Record(duration.TotalSeconds, tags);
            ReportChoicesCounter(s_chatCompletionsChoices, completions.Choices.Select(c => c.FinishReason), tags);

            if (completions.Usage.PromptTokens != 0)
            {
                var promptTags = CopyTags(tags);
                promptTags.Add(UsageTypeAttribute, TokenUsageTypePrompt);
                s_chatCompletionsTokens.Add(completions.Usage.PromptTokens, promptTags);
            }

            if (completions.Usage.CompletionTokens != 0)
            {
                var completionsTags = tags;
                completionsTags.Add(UsageTypeAttribute, TokenUsageTypeCompletion);
                s_chatCompletionsTokens.Add(completions.Usage.CompletionTokens, completionsTags);
            }
        }

        private void ReportCompletionsTelemetry(DiagnosticScope scope, Completions completions, TagList tags, TimeSpan duration)
        {
            if (scope.IsEnabled)
            {
                var filterResults = completions.PromptFilterResults.Select(PromptFilterResultsToString);
                if (filterResults.Any())
                {
                    scope.AddArrayAttribute("openai.azure.completions.response.filter_results", filterResults.ToArray());
                }

                var finishReasons = completions.Choices.Select(c => c.FinishReason.ToString()).ToArray();
                scope.AddAttribute("openai.completions.response.id", completions.Id);
                AddCreatedAttribute(scope, "openai.completions.response.created_at", completions.Created);
                scope.AddArrayAttribute("openai.completions.response.finish_reasons", finishReasons);
            }

            // before tags are modified
            s_completionsDuration.Record(duration.TotalSeconds, tags);
            ReportChoicesCounter(s_completionsChoices, completions.Choices.Select(c => c.FinishReason), tags);

            if (completions.Usage.PromptTokens != 0)
            {
                var promptTags = CopyTags(tags);
                promptTags.Add(UsageTypeAttribute, "prompt");
                s_completionsTokens.Add(completions.Usage.PromptTokens, promptTags);
            }

            if (completions.Usage.CompletionTokens != 0)
            {
                var completionsTags = tags;
                completionsTags.Add(UsageTypeAttribute, "completion");
                s_completionsTokens.Add(completions.Usage.CompletionTokens, completionsTags);
            }
        }

        private void ReportEmbeddingsMetrics(DiagnosticScope scope, Embeddings embeddings, TagList tags, TimeSpan duration)
        {
            scope.AddIntegerAttribute("openai.embeddings.response.vector_size", embeddings.Data.Count);
            s_embeddingsDataCount.Add(embeddings.Data.Count, tags);
            s_embeddingsDuration.Record(duration.TotalSeconds, tags);

            // tags are updated, so has to be the last one
            if (embeddings.Usage.PromptTokens != 0)
            {
                scope.AddIntegerAttribute("openai.embeddings.response.prompt_tokens", embeddings.Usage.PromptTokens);
                tags.Add(UsageTypeAttribute, "prompt");
                s_embeddingsTokens.Add(embeddings.Usage.PromptTokens, tags);
            }
        }

        private void ReportImageGenerationMetrics(DiagnosticScope scope, ImageGenerations generations, TagList tags, TimeSpan duration)
        {
            AddCreatedAttribute(scope, "openai.image_generations.response.created_at", generations.Created);
            s_imageGenerationsDuration.Record(duration.TotalSeconds, tags);
        }

        private void ReportError(Histogram<double> metric, TagList tags, TimeSpan duration, Exception e)
        {
            tags.Add("error.type", e.GetType().Name);
            metric.Record(duration.TotalSeconds, tags);
        }

        private void EnrichDiagnosticScope(DiagnosticScope scope, string model, CompletionsOptions options)
        {
            scope.AddAttribute(ModelNameAttribute, model);
            scope.AddAttribute(ServerAddressAttribute, _endpoint.Host);

            scope.AddDoubleAttribute("openai.completions.request.temperature", options.Temperature);
            scope.AddIntegerAttribute("openai.completions.request.max_tokens", options.MaxTokens);
            scope.AddDoubleAttribute("openai.completions.request.top_p", options.NucleusSamplingFactor);
            scope.AddDoubleAttribute("openai.completions.request.presence_penalty", options.PresencePenalty);
            scope.AddDoubleAttribute("openai.completions.request.frequency_penalty", options.FrequencyPenalty);
        }

        private void EnrichDiagnosticScope(DiagnosticScope scope, string model, ChatCompletionsOptions options)
        {
            scope.AddAttribute(ModelNameAttribute, model);
            scope.AddAttribute(ServerAddressAttribute, _endpoint.Host);

            scope.AddDoubleAttribute("openai.chat_completions.request.temperature", options.Temperature);
            scope.AddIntegerAttribute("openai.chat_completions.request.max_tokens", options.MaxTokens);
            scope.AddDoubleAttribute("openai.chat_completions.request.top_p", options.NucleusSamplingFactor);
            scope.AddDoubleAttribute("openai.chat_completions.request.presence_penalty", options.PresencePenalty);
            scope.AddDoubleAttribute("openai.chat_completions.request.frequency_penalty", options.FrequencyPenalty);
        }

        private void EnrichDiagnosticScope(DiagnosticScope scope, string model, EmbeddingsOptions options)
        {
            scope.AddAttribute(ModelNameAttribute, model);
            scope.AddAttribute(ServerAddressAttribute, _endpoint.Host);
            scope.AddIntegerAttribute("openai.embeddings.request.input_size", options.Input.Count);
        }

        private void EnrichDiagnosticScope(DiagnosticScope scope, ImageGenerationOptions options)
        {
            scope.AddIntegerAttribute("openai.image_generations.request.image_count", options.ImageCount);
            scope.AddAttribute("openai.image_generations.request.image_size", options.Size);
            scope.AddAttribute("openai.image_generations.request.image_format", options.ResponseFormat);
        }

        private static void AddCreatedAttribute(DiagnosticScope scope, string name, DateTimeOffset created)
        {
            if (scope.IsEnabled)
            {
                scope.AddLongAttribute(name, created.ToUnixTimeMilliseconds());
            }
        }

        private static string? PromptFilterResultsToString(PromptFilterResult promptFilterResult)
        {
            var results = promptFilterResult.ContentFilterResults;
            StringBuilder val = new();

            if (results.Sexual.Filtered)
            {
                val.Append("sexual").Append(", ");
            }
            if (results.Violence.Filtered)
            {
                val.Append("violence").Append(", ");
            }
            if (results.Hate.Filtered)
            {
                val.Append("hate").Append(", ");
            }
            if (results.SelfHarm.Filtered)
            {
                val.Append("self_harm").Append(", ");
            }

            if (val.Length > 0)
            {
                val.Remove(val.Length - 2, 2);
                return $"[{promptFilterResult.PromptIndex}]: {val}";
            }

            return null;
        }

        private static TagList CopyTags(TagList tags)
        {
            var copy = new KeyValuePair<string, object?>[tags.Count];
            tags.CopyTo(copy);

            return new TagList(copy);
        }

        private void ReportChoicesCounter(Counter<long> metric, IEnumerable<CompletionsFinishReason?> reasons, TagList tags)
        {
            // TODO optimize
            foreach (var reason in reasons)
            {
                var tagsWithFinishReason = tags;
                if (reason != null)
                {
                    tagsWithFinishReason = CopyTags(tags);
                    tagsWithFinishReason.Add(FinishReasonAttribute, reason.ToString());
                }
                metric.Add(1, tagsWithFinishReason);
            }
        }
    }
}
