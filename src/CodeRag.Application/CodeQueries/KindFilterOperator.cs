namespace CodeRag.Application.CodeQueries;

/// <summary>Comparison operators supported by the <c>kind</c> code query filter.</summary>
public enum KindFilterOperator
{
    /// <summary>Matches documents whose <c>kind</c> contains the filter value.</summary>
    Contains,

    /// <summary>Matches documents whose <c>kind</c> equals the filter value.</summary>
    Equals,

    /// <summary>Matches documents whose <c>kind</c> does not equal the filter value.</summary>
    NotEquals,
}
