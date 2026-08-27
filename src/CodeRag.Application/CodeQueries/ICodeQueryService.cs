using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.CodeQueries;

public interface ICodeQueryService
{
    /// <summary>
    /// Embeds <paramref name="question"/> and returns the most semantically similar code
    /// documents indexed for <paramref name="projectId"/>.
    /// </summary>
    Task<Result<IEnumerable<CodeQueryResult>>> QueryAsync(
        long projectId,
        string? question,
        CancellationToken cancellationToken = default);
}
