namespace CodeRag.Application.CodeQueries;

/// <summary>Comparison operators supported by the <c>namespace</c> code query filter.</summary>
public enum NamespaceFilterOperator
{
    /// <summary>Matches documents whose <c>namespace</c> contains the filter value.</summary>
    Contains,

    /// <summary>Matches documents whose <c>namespace</c> does not contain the filter value.</summary>
    NotContains,

    /// <summary>Matches documents whose <c>namespace</c> equals the filter value.</summary>
    Equals,

    /// <summary>Matches documents whose <c>namespace</c> does not equal the filter value.</summary>
    NotEquals,
}
