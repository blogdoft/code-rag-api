using CodeRag.Application.CodeQueries;
using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Optional filter narrowing results to code documents matching a <c>namespace</c> condition.</summary>
/// <param name="Operator">Comparison operator to apply.</param>
/// <param name="Value">
/// Value to compare <c>namespace</c> against. Must not be empty or blank. When the operator is
/// <c>Contains</c> or <c>NotContains</c>, <c>*</c> acts as a wildcard matching any sequence of
/// characters (e.g. <c>*Billing*</c> matches values containing "Billing"); a value with no
/// <c>*</c> is matched exactly (case-insensitively). Ignored for other operators.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record CodeQueryNamespaceFilterRequest(
    [property: JsonRequired] NamespaceFilterOperator Operator,
    [property: JsonRequired] string Value);
#pragma warning restore SA1313
