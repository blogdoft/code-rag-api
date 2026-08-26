using CodeRag.Embeddings.Abstraction;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace CodeRag.Embeddings.Local;

/// <summary>
/// Generates embeddings in-process using an ONNX-exported BERT-family model, without any
/// network round-trip. Expects <see cref="EmbeddingOptions.LocalModelPath"/> to point at a
/// directory containing <c>model.onnx</c> and <c>vocab.txt</c>, matching the standard layout
/// produced by Hugging Face's ONNX export tooling for sentence-embedding models (e.g. BGE,
/// MiniLM): inputs named "input_ids"/"attention_mask"/"token_type_ids" and a "last_hidden_state"
/// output of shape [batch, sequence, hidden].
/// </summary>
public sealed class LocalEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;

    public LocalEmbeddingGenerator(EmbeddingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LocalModelPath))
        {
            throw new InvalidOperationException(
                $"'{EmbeddingOptions.SectionName}:{nameof(EmbeddingOptions.LocalModelPath)}' must be set when using the Local embedding provider.");
        }

        var modelPath = Path.Combine(options.LocalModelPath, "model.onnx");
        var vocabPath = Path.Combine(options.LocalModelPath, "vocab.txt");

        try
        {
            _session = new InferenceSession(modelPath);
            _tokenizer = BertTokenizer.Create(vocabPath);
        }
        catch (Exception ex) when (ex is IOException or OnnxRuntimeException)
        {
            throw new InvalidOperationException(
                $"Failed to load the local embedding model from '{options.LocalModelPath}'. " +
                "Expected a 'model.onnx' and 'vocab.txt' pair in that directory.", ex);
        }

        Model = options.Model;
        Dimensions = options.Dimensions;
        Normalized = options.Normalized;
    }

    public string Provider => "Local";

    public string Model { get; }

    public int Dimensions { get; }

    private bool Normalized { get; }

    public Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var vector = Embed(text);
            return Task.FromResult(vector);
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or IndexOutOfRangeException)
        {
            throw new EmbeddingGenerationException(
                $"Local ONNX inference failed while embedding text of length {text.Length}.", ex);
        }
    }

    private EmbeddingVector Embed(string text)
    {
        var tokenIds = _tokenizer.EncodeToIds(text, addSpecialTokens: true, considerPreTokenization: true);
        var sequenceLength = tokenIds.Count;

        var inputIds = new DenseTensor<long>([1, sequenceLength]);
        var attentionMask = new DenseTensor<long>([1, sequenceLength]);
        var tokenTypeIds = new DenseTensor<long>([1, sequenceLength]);

        for (var i = 0; i < sequenceLength; i++)
        {
            inputIds[0, i] = tokenIds[i];
            attentionMask[0, i] = 1;
            tokenTypeIds[0, i] = 0;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds),
        };

        using var results = _session.Run(inputs);
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();

        var pooled = MeanPool(lastHiddenState, sequenceLength);

        if (Normalized)
        {
            NormalizeInPlace(pooled);
        }

        return new EmbeddingVector(pooled);
    }

    private static float[] MeanPool(Tensor<float> lastHiddenState, int sequenceLength)
    {
        var hiddenSize = lastHiddenState.Dimensions[2];
        var pooled = new float[hiddenSize];

        for (var i = 0; i < sequenceLength; i++)
        {
            for (var h = 0; h < hiddenSize; h++)
            {
                pooled[h] += lastHiddenState[0, i, h];
            }
        }

        for (var h = 0; h < hiddenSize; h++)
        {
            pooled[h] /= sequenceLength;
        }

        return pooled;
    }

    private static void NormalizeInPlace(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm <= float.Epsilon)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }

    public void Dispose() => _session.Dispose();
}
