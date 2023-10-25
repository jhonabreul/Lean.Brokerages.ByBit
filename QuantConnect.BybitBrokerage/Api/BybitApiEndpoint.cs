/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using QuantConnect.Brokerages;
using QuantConnect.BybitBrokerage.Converters;
using QuantConnect.BybitBrokerage.Models;
using QuantConnect.BybitBrokerage.Models.Enums;
using QuantConnect.Logging;
using QuantConnect.Securities;
using RestSharp;

namespace QuantConnect.BybitBrokerage.Api;

/// <summary>
/// Base Bybit api endpoint implementation
/// </summary>
public abstract class BybitApiEndpoint
{
    /// <summary>
    /// Default api JSON serializer settings
    /// </summary>
    protected static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        },
        Converters = new List<JsonConverter>()
            { new ByBitKlineJsonConverter(), new StringEnumConverter(), new BybitDecimalStringConverter() },
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly BybitApiClient _apiClient;
    private readonly string _apiPrefix;


    /// <summary>
    /// Symbol mapper
    /// </summary>
    protected ISymbolMapper SymbolMapper { get; }

    /// <summary>
    /// Security provider
    /// </summary>
    protected ISecurityProvider SecurityProvider { get; }

    /// <summary>
    /// Bybit api client
    /// </summary>
    /// <summary>
    /// Initializes a new instance of the <see cref="BybitApiEndpoint"/> class.
    /// </summary>
    /// <param name="symbolMapper">The symbol mapper</param>
    /// <param name="apiPrefix">The api prefix</param>
    /// <param name="securityProvider">The security provider</param>
    /// <param name="apiClient">The api client</param>
    protected BybitApiEndpoint(
        ISymbolMapper symbolMapper,
        string apiPrefix,
        ISecurityProvider securityProvider,
        BybitApiClient apiClient)
    {
        SymbolMapper = symbolMapper;
        SecurityProvider = securityProvider;
        _apiPrefix = apiPrefix;
        _apiClient = apiClient;
    }

    /// <summary>
    /// Fetches all results from a paginated GET endpoint
    /// </summary>
    /// <param name="endpoint">The endpoint</param>
    /// <param name="category">Optional product category which is added to the request</param>
    /// <param name="limit">The max number of elements the api can return for this request</param>
    /// <param name="parameters">Optional parameters which should be added to the request</param>
    /// <param name="authenticate">Whether the request should be authenticated</param>
    /// <typeparam name="T">The business data type of the response</typeparam>
    /// <returns>An enumerable of the business data returned from the api</returns>
    [StackTraceHidden]
    protected IEnumerable<T> FetchAll<T>(string endpoint, BybitProductCategory category, int limit,
        IEnumerable<KeyValuePair<string, string>> parameters = null, bool authenticate = false)
    {
        var parameterDict = parameters == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(parameters);
        parameterDict["limit"] = limit.ToStringInvariant();

        do
        {
            var result = ExecuteGetRequest<BybitPageResult<T>>(endpoint, category, parameterDict.OrderBy(x => x.Key), authenticate);

            foreach (var data in result.List)
            {
                yield return data;
            }

            var nextCursor = result.NextPageCursor;
            // Break when the cursor is either empty or the same as the one we just processed
            if (string.IsNullOrEmpty(nextCursor) || (parameterDict.TryGetValue("cursor", out var previousCursor) && previousCursor == nextCursor))
            {
                break;
            }

            parameterDict["cursor"] = nextCursor;
        } while (true);
    }
    //[StackTraceHidden]
    //protected IEnumerable<T> FetchAll<T>(string endpoint, BybitProductCategory category, int limit,
    //    IEnumerable<KeyValuePair<string, string>> parameters = null, bool authenticate = false)
    //{
    //    return FetchAllImpl<T>(endpoint, category, limit, parameters, authenticate).SynchronouslyAwaitTaskResult();
    //}

    //[StackTraceHidden]
    //private async Task<IEnumerable<T>> FetchAllImpl<T>(string endpoint, BybitProductCategory category, int limit,
    //    IEnumerable<KeyValuePair<string, string>> parameters = null, bool authenticate = false)
    //{
    //    var dataList = new List<T>();
    //    await foreach (var data in FetchAllAsync<T>(endpoint, category, limit, parameters, authenticate))
    //    {
    //        dataList.Add(data);
    //    }

    //    return dataList;
    //}

    /// <summary>
    /// Asynchronously fetches all results from a paginated GET endpoint
    /// </summary>
    /// <param name="endpoint">The endpoint</param>
    /// <param name="category">Optional product category which is added to the request</param>
    /// <param name="limit">The max number of elements the api can return for this request</param>
    /// <param name="parameters">Optional parameters which should be added to the request</param>
    /// <param name="authenticate">Whether the request should be authenticated</param>
    /// <typeparam name="T">The business data type of the response</typeparam>
    /// <returns>An enumerable of the business data returned from the api</returns>
    [StackTraceHidden]
    protected async IAsyncEnumerable<T> FetchAllAsync<T>(string endpoint, BybitProductCategory category, int limit,
        IEnumerable<KeyValuePair<string, string>> parameters = null, bool authenticate = false)
    {
        var parameterDict = parameters == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(parameters);
        parameterDict["limit"] = limit.ToStringInvariant();

        do
        {
            var result = await ExecuteGetRequestAsync<BybitPageResult<T>>(endpoint, category, parameterDict.OrderBy(x => x.Key), authenticate);

            foreach (var data in result.List)
            {
                yield return data;
            }

            var nextCursor = result.NextPageCursor;
            // Break when the cursor is either empty or the same as the one we just processed
            if (string.IsNullOrEmpty(nextCursor) || (parameterDict.TryGetValue("cursor", out var previousCursor) && previousCursor == nextCursor))
            {
                break;
            }

            parameterDict["cursor"] = nextCursor;
        } while (true);
    }

    /// <summary>
    /// Creates a GET request, authenticates, and executes it and parses the response
    /// </summary>
    /// <param name="endpoint">The endpoint</param>
    /// <param name="category">Optional product category which is added to the request</param>
    /// <param name="parameters">Optional parameters which should be added to the request</param>
    /// <param name="authenticate">Whether the request should be authenticated</param>
    /// <typeparam name="T">The business data type of the response</typeparam>
    /// <returns>The business data of the response</returns>
    [StackTraceHidden]
    protected T ExecuteGetRequest<T>(string endpoint, BybitProductCategory? category = null,
        IEnumerable<KeyValuePair<string, string>> parameters = null, bool authenticate = false)
    {
        // TODO: Running an async task synchronously should be handled better, since this could lead to deadlocks.
        //       See how RestSharp handles it: https://github.com/restsharp/RestSharp/blob/d99d49437af21688152b556f6d3661d2e739b824/src/RestSharp/AsyncHelpers.cs#L27
        return ExecuteGetRequestAsync<T>(endpoint, category, parameters, authenticate).SynchronouslyAwaitTaskResult();
    }

    /// <summary>
    /// Creates a GET request, authenticates, and executes it and parses the response asynchronously
    /// </summary>
    /// <param name="endpoint">The endpoint</param>
    /// <param name="category">Optional product category which is added to the request</param>
    /// <param name="parameters">Optional parameters which should be added to the request</param>
    /// <param name="authenticate">Whether the request should be authenticated</param>
    /// <typeparam name="T">The business data type of the response</typeparam>
    /// <returns>The business data of the response</returns>
    [StackTraceHidden]
    protected async Task<T> ExecuteGetRequestAsync<T>(string endpoint, BybitProductCategory? category = null,
        IEnumerable<KeyValuePair<string, string>> parameters = null, bool authenticate = false)
    {
        var request = new RestRequest($"{_apiPrefix}{endpoint}");

        if (category.HasValue)
        {
            request.AddQueryParameter("category", category.Value.ToStringInvariant().ToLowerInvariant());
        }

        if (parameters != null)
        {
            foreach (var parameter in parameters)
            {
                // The cursor is already encoded
                var encode = parameter.Key != "cursor";
                request.AddQueryParameter(parameter.Key, parameter.Value, encode);
            }
        }

        var response = await ExecuteRequestAsync(request, authenticate);

        return EnsureSuccessAndParse<T>(response);
    }

    /// <summary>
    /// Creates a POST request, authenticates, and executes it and parses the response
    /// </summary>
    /// <param name="endpoint">The endpoint</param>
    /// <param name="body">The body content</param>
    /// <typeparam name="T">The business data type of the response</typeparam>
    /// <returns>The business data of the response</returns>
    [StackTraceHidden]
    protected T ExecutePostRequest<T>(string endpoint, object body)
    {
        var bodyString = JsonConvert.SerializeObject(body, SerializerSettings);

        var request = new RestRequest($"{_apiPrefix}{endpoint}", Method.POST);
        request.AddParameter("", bodyString, "application/json", ParameterType.RequestBody);

        var response = ExecuteRequest(request, true);
        return EnsureSuccessAndParse<T>(response);
    }

    /// <summary>
    /// Ensures the request executed successfully and returns the parsed business object
    /// </summary>
    /// <param name="response">The response to parse</param>
    /// <typeparam name="T">The type of the response business object</typeparam>
    /// <returns>The parsed response business object</returns>
    /// <exception cref="Exception"></exception>
    [StackTraceHidden]
    private T EnsureSuccessAndParse<T>(IRestResponse response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception("ByBitApiClient request failed: " +
                                $"[{(int)response.StatusCode}] {response.StatusDescription}, " +
                                $"Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
        }

        ByBitResponse<T> byBitResponse;
        try
        {
            byBitResponse = JsonConvert.DeserializeObject<ByBitResponse<T>>(response.Content, SerializerSettings);
        }
        catch (Exception e)
        {
            throw new Exception("ByBitApiClient failed deserializing response: " +
                                $"[{(int)response.StatusCode}] {response.StatusDescription}, " +
                                $"Content: {response.Content}, ErrorMessage: {response.ErrorMessage}", e);
        }

        if (byBitResponse?.ReturnCode != 0)
        {
            throw new Exception("ByBitApiClient request failed: " +
                                $"[{(int)response.StatusCode}] {response.StatusDescription}, " +
                                $"Content: {response.Content}, ErrorCode: {byBitResponse?.ReturnCode} ErrorMessage: {byBitResponse?.ReturnMessage}");
        }

        if (Log.DebuggingEnabled)
        {
            Log.Debug(
                $"Bybit request for {response.Request.Resource} executed successfully. Response: {response.Content}");
        }

        return byBitResponse.Result;
    }


    /// <summary>
    /// Executes the rest request
    /// </summary>
    /// <param name="request">The rest request to execute</param>
    /// <param name="authenticate">If the request should be authenticated</param>
    /// <returns>The rest response</returns>
    [StackTraceHidden]
    private IRestResponse ExecuteRequest(IRestRequest request, bool authenticate = false)
    {
        // TODO: Running an async task synchronously should be handled better, since this could lead to deadlocks.
        //       See how RestSharp handles it: https://github.com/restsharp/RestSharp/blob/d99d49437af21688152b556f6d3661d2e739b824/src/RestSharp/AsyncHelpers.cs#L27
        return ExecuteRequestAsync(request, authenticate).SynchronouslyAwaitTaskResult();
    }

    /// <summary>
    /// Executes the rest request asynchronously
    /// </summary>
    /// <param name="request">The rest request to execute</param>
    /// <param name="authenticate">If the request should be authenticated</param>
    /// <returns>The rest response</returns>
    [StackTraceHidden]
    private async Task<IRestResponse> ExecuteRequestAsync(IRestRequest request, bool authenticate = false)
    {
        if (authenticate)
        {
            _apiClient.AuthenticateRequest(request);
        }

        return await _apiClient.ExecuteRequestAsync(request);
    }
}