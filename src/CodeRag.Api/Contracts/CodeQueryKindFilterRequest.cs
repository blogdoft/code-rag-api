using CodeRag.Application.CodeQueries;
using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Optional filter narrowing results to code documents matching a <c>kind</c> condition.</summary>
/// <param name="Operator">Comparison operator to apply.</param>
/// <param name="Value">
/// Value to compare <c>kind</c> against. Must not be empty or blank. When the operator is
/// <c>Contains</c>, <c>*</c> acts as a wildcard matching any sequence of characters (e.g.
/// <c>fun*</c> matches values starting with "fun"); a value with no <c>*</c> is matched exactly
/// (case-insensitively). Ignored for other operators.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record CodeQueryKindFilterRequest(
    [property: JsonRequired] KindFilterOperator Operator,
    [property: JsonRequired] string Value);
#pragma warning restore SA1313
