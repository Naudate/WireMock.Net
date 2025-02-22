#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using WireMock.Constants;
using WireMock.Server;
using WireMock.Types;

// ReSharper disable once CheckNamespace
namespace WireMock.FluentAssertions;

public class WireMockAssertions
{
    private const string Any = "*";
    private readonly int? _callsCount;
    private IReadOnlyList<IRequestMessage> _requestMessages;
    private readonly IReadOnlyList<KeyValuePair<string, WireMockList<string>>> _headers;

    public WireMockAssertions(IWireMockServer subject, int? callsCount)
    {
        _callsCount = callsCount;
        _requestMessages = subject.LogEntries.Select(logEntry => logEntry.RequestMessage).ToList();
        _headers = _requestMessages.SelectMany(req => req.Headers).ToList();
    }

    [CustomAssertion]
    public AndWhichConstraint<WireMockAssertions, string> AtAbsoluteUrl(string absoluteUrl, string because = "", params object[] becauseArgs)
    {
        Func<IRequestMessage, bool> predicate = request => string.Equals(request.AbsoluteUrl, absoluteUrl, StringComparison.OrdinalIgnoreCase);

        var (filter, condition) = BuildFilterAndCondition(predicate);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => _requestMessages)
            .ForCondition(requests => _callsCount == 0 || requests.Any())
            .FailWith(
                "Expected {context:wiremockserver} to have been called at address matching the absolute url {0}{reason}, but no calls were made.",
                absoluteUrl
            )
            .Then
            .ForCondition(condition)
            .FailWith(
                "Expected {context:wiremockserver} to have been called at address matching the absolute url {0}{reason}, but didn't find it among the calls to {1}.",
                _ => absoluteUrl, requests => requests.Select(request => request.AbsoluteUrl)
            );

        _requestMessages = filter(_requestMessages).ToList();

        return new AndWhichConstraint<WireMockAssertions, string>(this, absoluteUrl);
    }

    [CustomAssertion]
    public AndWhichConstraint<WireMockAssertions, string> AtUrl(string url, string because = "", params object[] becauseArgs)
    {
        Func<IRequestMessage, bool> predicate = request => string.Equals(request.Url, url, StringComparison.OrdinalIgnoreCase);

        var (filter, condition) = BuildFilterAndCondition(predicate);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => _requestMessages)
            .ForCondition(requests => _callsCount == 0 || requests.Any())
            .FailWith(
                "Expected {context:wiremockserver} to have been called at address matching the url {0}{reason}, but no calls were made.",
                url
            )
            .Then
            .ForCondition(condition)
            .FailWith(
                "Expected {context:wiremockserver} to have been called at address matching the url {0}{reason}, but didn't find it among the calls to {1}.",
                _ => url,
                requests => requests.Select(request => request.Url)
            );

        _requestMessages = filter(_requestMessages).ToList();

        return new AndWhichConstraint<WireMockAssertions, string>(this, url);
    }

    [CustomAssertion]
    public AndWhichConstraint<WireMockAssertions, string> WithProxyUrl(string proxyUrl, string because = "", params object[] becauseArgs)
    {
        Func<IRequestMessage, bool> predicate = request => string.Equals(request.ProxyUrl, proxyUrl, StringComparison.OrdinalIgnoreCase);

        var (filter, condition) = BuildFilterAndCondition(predicate);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => _requestMessages)
            .ForCondition(requests => _callsCount == 0 || requests.Any())
            .FailWith(
                "Expected {context:wiremockserver} to have been called with proxy url {0}{reason}, but no calls were made.",
                proxyUrl
            )
            .Then
            .ForCondition(condition)
            .FailWith(
                "Expected {context:wiremockserver} to have been called with proxy url {0}{reason}, but didn't find it among the calls with {1}.",
                _ => proxyUrl,
                requests => requests.Select(request => request.ProxyUrl)
            );

        _requestMessages = filter(_requestMessages).ToList();

        return new AndWhichConstraint<WireMockAssertions, string>(this, proxyUrl);
    }

    [CustomAssertion]
    public AndWhichConstraint<WireMockAssertions, string> FromClientIP(string clientIP, string because = "", params object[] becauseArgs)
    {
        Func<IRequestMessage, bool> predicate = request => string.Equals(request.ClientIP, clientIP, StringComparison.OrdinalIgnoreCase);

        var (filter, condition) = BuildFilterAndCondition(predicate);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => _requestMessages)
            .ForCondition(requests => _callsCount == 0 || requests.Any())
            .FailWith(
                "Expected {context:wiremockserver} to have been called from client IP {0}{reason}, but no calls were made.",
                clientIP
            )
            .Then
            .ForCondition(condition)
            .FailWith(
                "Expected {context:wiremockserver} to have been called from client IP {0}{reason}, but didn't find it among the calls from IP(s) {1}.",
                _ => clientIP, requests => requests.Select(request => request.ClientIP)
            );

        _requestMessages = filter(_requestMessages).ToList();

        return new AndWhichConstraint<WireMockAssertions, string>(this, clientIP);
    }

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> WithHeader(string expectedKey, string value, string because = "", params object[] becauseArgs)
        => WithHeader(expectedKey, new[] { value }, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> WithHeader(string expectedKey, string[] expectedValues, string because = "", params object[] becauseArgs)
    {
        using (new AssertionScope("headers from requests sent"))
        {
            _headers.Select(h => h.Key).Should().Contain(expectedKey, because, becauseArgs);
        }

        using (new AssertionScope($"header \"{expectedKey}\" from requests sent with value(s)"))
        {
            var matchingHeaderValues = _headers.Where(h => h.Key == expectedKey).SelectMany(h => h.Value.ToArray()).ToArray();

            if (expectedValues.Length == 1)
            {
                matchingHeaderValues.Should().Contain(expectedValues.First(), because, becauseArgs);
            }
            else
            {
                var trimmedHeaderValues = string.Join(",", matchingHeaderValues.Select(x => x)).Split(',').Select(x => x.Trim()).ToList();
                foreach (var expectedValue in expectedValues)
                {
                    trimmedHeaderValues.Should().Contain(expectedValue, because, becauseArgs);
                }
            }
        }

        return new AndConstraint<WireMockAssertions>(this);
    }

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingConnect(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.CONNECT, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingDelete(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.DELETE, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingGet(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.GET, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingHead(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.HEAD, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingOptions(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.OPTIONS, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingPost(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.POST, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingPatch(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.PATCH, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingPut(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.PUT, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingTrace(string because = "", params object[] becauseArgs)
        => UsingMethod(HttpRequestMethod.TRACE, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingAnyMethod(string because = "", params object[] becauseArgs)
        => UsingMethod(Any, because, becauseArgs);

    [CustomAssertion]
    public AndConstraint<WireMockAssertions> UsingMethod(string method, string because = "", params object[] becauseArgs)
    {
        var any = method == Any;
        Func<IRequestMessage, bool> predicate = request => (any && !string.IsNullOrEmpty(request.Method)) ||
                                                           string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase);

        var (filter, condition) = BuildFilterAndCondition(predicate);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => _requestMessages)
            .ForCondition(requests => _callsCount == 0 || requests.Any())
            .FailWith(
                "Expected {context:wiremockserver} to have been called using method {0}{reason}, but no calls were made.",
                method
            )
            .Then
            .ForCondition(condition)
            .FailWith(
                "Expected {context:wiremockserver} to have been called using method {0}{reason}, but didn't find it among the methods {1}.",
                _ => method,
                requests => requests.Select(request => request.Method)
            );

        _requestMessages = filter(_requestMessages).ToList();

        return new AndConstraint<WireMockAssertions>(this);
    }

    private (Func<IReadOnlyList<IRequestMessage>, IReadOnlyList<IRequestMessage>> Filter, Func<IReadOnlyList<IRequestMessage>, bool> Condition) BuildFilterAndCondition(Func<IRequestMessage, bool> predicate)
    {
        Func<IReadOnlyList<IRequestMessage>, IReadOnlyList<IRequestMessage>> filter = requests => requests.Where(predicate).ToList();

        return (filter, requests => (_callsCount is null && filter(requests).Any()) || _callsCount == filter(requests).Count);
    }
}