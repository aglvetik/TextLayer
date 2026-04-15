using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Domain.Models;

namespace TextLayer.Application.UseCases;

public sealed class ImageDocumentUseCase(
    IImageLoader imageLoader,
    IOcrEngine ocrEngine,
    ILogService logService)
{
    public async Task<LoadedImageData> LoadImageAsync(string sourcePath, CancellationToken cancellationToken)
    {
        logService.Info($"Loading image: {sourcePath}");
        return await imageLoader.LoadAsync(sourcePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecognizedDocument> RecognizeAsync(string sourcePath, OcrRequestOptions request, CancellationToken cancellationToken)
    {
        var document = await ocrEngine.RecognizeAsync(sourcePath, request, cancellationToken).ConfigureAwait(false);
        logService.Info(
            $"OCR completed in {document.RecognitionDurationMs} ms. Lines: {document.Lines.Count}, Words: {document.Words.Count}, Engine: {document.OcrEngineId}");
        return document;
    }
}
