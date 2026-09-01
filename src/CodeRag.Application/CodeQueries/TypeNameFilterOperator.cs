namespace CodeRag.Application.CodeQueries;

/// <summary>Comparison operators supported by the <c>typeName</c> code query filter.</summary>
public enum TypeNameFilterOperator
{
    /// <summary>Matches documents whose <c>type_name</c> contains the filter value.</summary>
    Contains,

    /// <summary>Matches documents whose <c>type_name</c> does not contain the filter value.</summary>
    NotContains,

    /// <summary>Matches documents whose <c>type_name</c> equals the filter value.</summary>
    Equals,
}
