using TextLayer.Domain.Models;
using TextLayer.Application.Models;

namespace TextLayer.Application.Abstractions;

public interface IOcrEngine
{
    Task<RecognizedDocument> RecognizeAsync(string sourcePath, OcrRequestOptions request, CancellationToken cancellationToken);
}
