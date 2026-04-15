using TextLayer.Application.Abstractions;
using TextLayer.Application.Models;
using TextLayer.Domain.Models;

namespace TextLayer.Infrastructure.Ocr;

public sealed class CompositeOcrEngine(
    IOcrEngine fastEngine,
    IOcrEngine accurateEngine,
    OcrImageAnalyzer imageAnalyzer,
    OcrEngineSelector engineSelector,
    ILogService logService) : IOcrEngine
{
    private readonly RecognizedDocumentScoreCalculator scoreCalculator = new();

    public async Task<RecognizedDocument> RecognizeAsync(string sourcePath, OcrRequestOptions request, CancellationToken cancellationToken)
    {
        var analysis = await imageAnalyzer.AnalyzeAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        if (request.LanguageMode == OcrLanguageMode.Auto)
        {
            return await RecognizeWithAutoLanguageAsync(sourcePath, request, analysis, cancellationToken).ConfigureAwait(false);
        }

        if (request.Mode == OcrMode.Auto)
        {
            return await RecognizeWithAutoEngineForExplicitLanguageAsync(sourcePath, request, analysis, cancellationToken).ConfigureAwait(false);
        }

        var engineId = engineSelector.SelectEngineId(sourcePath, request, analysis);
        var candidate = await RecognizeBestFixedLanguageCandidateAsync(
                sourcePath,
                engineId,
                request,
                cancellationToken,
                allowAutoFallback: false)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            throw new InvalidOperationException("TextLayer could not recognize text from the captured image.");
        }

        logService.Info(
            $"OCR mode '{request.Mode}' selected engine '{candidate.EngineId}' with language '{candidate.LanguageMode}' for {Path.GetFileName(sourcePath)}. Score: {candidate.Score.Value:F1}");
        return candidate.Document;
    }

    private async Task<RecognizedDocument> RecognizeWithAutoLanguageAsync(
        string sourcePath,
        OcrRequestOptions request,
        OcrImageAnalysis analysis,
        CancellationToken cancellationToken)
    {
        var engineOrder = engineSelector.GetEnginePreferenceOrder(sourcePath, request, analysis);
        var bestCandidate = default(ScoredRecognitionCandidate?);

        foreach (var engineId in engineOrder)
        {
            var candidate = await RecognizeBestLanguageForEngineAsync(sourcePath, engineId, request, cancellationToken).ConfigureAwait(false);
            if (candidate is null)
            {
                continue;
            }

            if (bestCandidate is null || candidate.Score.Value > bestCandidate.Score.Value)
            {
                bestCandidate = candidate;
            }

            if (candidate.Score.IsStrong)
            {
                break;
            }
        }

        if (bestCandidate is null)
        {
            throw new InvalidOperationException("TextLayer could not recognize text from the captured image.");
        }

        logService.Info(
            $"OCR auto language selected engine '{bestCandidate.EngineId}' with language '{bestCandidate.LanguageMode}' for {Path.GetFileName(sourcePath)}. Score: {bestCandidate.Score.Value:F1}");
        return bestCandidate.Document;
    }

    private async Task<RecognizedDocument> RecognizeWithAutoEngineForExplicitLanguageAsync(
        string sourcePath,
        OcrRequestOptions request,
        OcrImageAnalysis analysis,
        CancellationToken cancellationToken)
    {
        var engineOrder = engineSelector.GetEnginePreferenceOrder(sourcePath, request, analysis);
        var bestCandidate = default(ScoredRecognitionCandidate?);

        foreach (var engineId in engineOrder)
        {
            var candidate = await RecognizeBestFixedLanguageCandidateAsync(
                    sourcePath,
                    engineId,
                    request,
                    cancellationToken,
                    allowAutoFallback: true)
                .ConfigureAwait(false);
            if (candidate is null)
            {
                continue;
            }

            if (bestCandidate is null || candidate.Score.Value > bestCandidate.Score.Value)
            {
                bestCandidate = candidate;
            }

            if (candidate.Score.IsStrong)
            {
                break;
            }
        }

        if (bestCandidate is null)
        {
            throw new InvalidOperationException("TextLayer could not recognize text from the captured image.");
        }

        logService.Info(
            $"OCR auto engine selected engine '{bestCandidate.EngineId}' with language '{bestCandidate.LanguageMode}' for requested mode '{request.LanguageMode}' on {Path.GetFileName(sourcePath)}. Score: {bestCandidate.Score.Value:F1}");
        return bestCandidate.Document;
    }

    private async Task<ScoredRecognitionCandidate?> RecognizeBestLanguageForEngineAsync(
        string sourcePath,
        string engineId,
        OcrRequestOptions originalRequest,
        CancellationToken cancellationToken)
    {
        var evaluatedCandidates = new List<ScoredRecognitionCandidate>();
        var queuedLanguageModes = new HashSet<OcrLanguageMode>();
        await EvaluateLanguageCandidateAsync(OcrLanguageMode.English).ConfigureAwait(false);
        await EvaluateLanguageCandidateAsync(OcrLanguageMode.Russian).ConfigureAwait(false);
        await EvaluateLanguageCandidateAsync(OcrLanguageMode.EnglishRussian).ConfigureAwait(false);

        if (evaluatedCandidates.Count == 0)
        {
            return null;
        }

        var bestCandidate = evaluatedCandidates.OrderByDescending(candidate => candidate.Score.Value).First();
        var bilingualCandidate = evaluatedCandidates
            .Where(candidate =>
                candidate.LanguageMode == OcrLanguageMode.EnglishRussian
                && candidate.Score.LatinCharacterCount >= 2
                && candidate.Score.CyrillicCharacterCount >= 2)
            .OrderByDescending(candidate => candidate.Score.Value)
            .FirstOrDefault();

        if (bilingualCandidate is not null
            && bestCandidate.LanguageMode != OcrLanguageMode.EnglishRussian
            && ShouldPreferMixedLanguageCandidate(bestCandidate, bilingualCandidate))
        {
            return bilingualCandidate;
        }

        return bestCandidate;

        async Task EvaluateLanguageCandidateAsync(OcrLanguageMode languageMode)
        {
            if (!queuedLanguageModes.Add(languageMode))
            {
                return;
            }

            var candidateRequest = originalRequest with { LanguageMode = languageMode };
            try
            {
                var document = await RecognizeWithEngineAsync(
                        sourcePath,
                        engineId,
                        candidateRequest,
                        cancellationToken,
                        allowAutoFallback: false)
                    .ConfigureAwait(false);

                var score = scoreCalculator.Score(document, languageMode);
                evaluatedCandidates.Add(new ScoredRecognitionCandidate(document, engineId, languageMode, score));
            }
            catch (TesseractConfigurationException) when (engineId == OcrEngineSelector.AccurateEngineId && originalRequest.Mode == OcrMode.Auto)
            {
            }
        }
    }

    private async Task<ScoredRecognitionCandidate?> RecognizeBestFixedLanguageCandidateAsync(
        string sourcePath,
        string engineId,
        OcrRequestOptions originalRequest,
        CancellationToken cancellationToken,
        bool allowAutoFallback)
    {
        var evaluatedCandidates = new List<ScoredRecognitionCandidate>();

        foreach (var languageMode in GetLanguageEvaluationOrder(engineId, originalRequest))
        {
            var candidateRequest = originalRequest with { LanguageMode = languageMode };
            try
            {
                var document = await RecognizeWithEngineAsync(
                        sourcePath,
                        engineId,
                        candidateRequest,
                        cancellationToken,
                        allowAutoFallback)
                    .ConfigureAwait(false);

                evaluatedCandidates.Add(new ScoredRecognitionCandidate(
                    document,
                    engineId,
                    languageMode,
                    scoreCalculator.Score(document, languageMode)));
            }
            catch (TesseractConfigurationException) when (engineId == OcrEngineSelector.AccurateEngineId && originalRequest.Mode == OcrMode.Auto)
            {
            }
        }

        if (evaluatedCandidates.Count == 0)
        {
            return null;
        }

        var bestCandidate = evaluatedCandidates.OrderByDescending(candidate => candidate.Score.Value).First();
        var requestedCandidate = evaluatedCandidates.FirstOrDefault(candidate => candidate.LanguageMode == originalRequest.LanguageMode);
        if (engineId != OcrEngineSelector.AccurateEngineId
            && requestedCandidate is not null
            && bestCandidate.LanguageMode != originalRequest.LanguageMode
            && requestedCandidate.Score.Value + 8d >= bestCandidate.Score.Value
            && requestedCandidate.Score.SuspiciousPseudoLatinWordCount <= bestCandidate.Score.SuspiciousPseudoLatinWordCount
            && requestedCandidate.Score.SuspiciousPseudoCyrillicWordCount <= bestCandidate.Score.SuspiciousPseudoCyrillicWordCount)
        {
            return requestedCandidate;
        }

        return bestCandidate;
    }

    private static IReadOnlyList<OcrLanguageMode> GetLanguageEvaluationOrder(string engineId, OcrRequestOptions request)
        => (engineId, request.Mode, request.LanguageMode) switch
        {
            (OcrEngineSelector.AccurateEngineId, OcrMode.Accurate, OcrLanguageMode.Russian)
                => [OcrLanguageMode.Russian, OcrLanguageMode.EnglishRussian],
            (OcrEngineSelector.AccurateEngineId, OcrMode.Accurate, OcrLanguageMode.English)
                => [OcrLanguageMode.English, OcrLanguageMode.EnglishRussian],
            (OcrEngineSelector.AccurateEngineId, _, OcrLanguageMode.EnglishRussian)
                => [OcrLanguageMode.EnglishRussian, OcrLanguageMode.Russian, OcrLanguageMode.English],
            (_, _, OcrLanguageMode.Russian) => [OcrLanguageMode.Russian],
            (_, _, OcrLanguageMode.EnglishRussian) => [OcrLanguageMode.EnglishRussian, OcrLanguageMode.Russian, OcrLanguageMode.English],
            (_, _, OcrLanguageMode.English) => [OcrLanguageMode.English],
            _ => [request.LanguageMode],
        };

    private static bool ShouldPreferMixedLanguageCandidate(
        ScoredRecognitionCandidate bestCandidate,
        ScoredRecognitionCandidate bilingualCandidate)
    {
        var bilingualHasMeaningfulMixedContent =
            bilingualCandidate.Score.SuggestedLanguageMode == OcrLanguageMode.EnglishRussian
            || bilingualCandidate.Score.DominantScript == ScriptDominance.Mixed
            || (bilingualCandidate.Score.LatinCharacterCount >= 4 && bilingualCandidate.Score.CyrillicCharacterCount >= 4);

        if (!bilingualHasMeaningfulMixedContent)
        {
            return false;
        }

        var scoreGap = bestCandidate.Score.Value - bilingualCandidate.Score.Value;
        var bilingualIsCleaner =
            bilingualCandidate.Score.SuspiciousPseudoLatinWordCount <= bestCandidate.Score.SuspiciousPseudoLatinWordCount
            && bilingualCandidate.Score.SuspiciousPseudoCyrillicWordCount <= bestCandidate.Score.SuspiciousPseudoCyrillicWordCount
            && bilingualCandidate.Score.MixedScriptWordCount <= bestCandidate.Score.MixedScriptWordCount + 1;
        var bilingualRecoversMissingScript =
            bilingualCandidate.Score.CorrectedWordCount >= bestCandidate.Score.CorrectedWordCount
            && bilingualCandidate.Score.LatinCharacterCount >= 2
            && bilingualCandidate.Score.CyrillicCharacterCount >= 2
            && (bestCandidate.Score.LatinCharacterCount == 0 || bestCandidate.Score.CyrillicCharacterCount == 0);

        return scoreGap <= 10d
            || (scoreGap <= 18d && bilingualIsCleaner)
            || (scoreGap <= 24d && bilingualIsCleaner && bestCandidate.Score.SuggestedLanguageMode != OcrLanguageMode.EnglishRussian)
            || (scoreGap <= 32d && bilingualIsCleaner && bilingualRecoversMissingScript);
    }

    private async Task<RecognizedDocument> RecognizeWithEngineAsync(
        string sourcePath,
        string engineId,
        OcrRequestOptions request,
        CancellationToken cancellationToken,
        bool allowAutoFallback)
    {
        if (engineId == OcrEngineSelector.AccurateEngineId)
        {
            try
            {
                return await accurateEngine.RecognizeAsync(sourcePath, request, cancellationToken).ConfigureAwait(false);
            }
            catch (TesseractConfigurationException exception) when (allowAutoFallback)
            {
                logService.Warn($"Accurate OCR is unavailable. Falling back to Fast OCR. {exception.Message}");
                return await fastEngine.RecognizeAsync(sourcePath, request, cancellationToken).ConfigureAwait(false);
            }
        }

        return await fastEngine.RecognizeAsync(sourcePath, request, cancellationToken).ConfigureAwait(false);
    }

    private sealed record ScoredRecognitionCandidate(
        RecognizedDocument Document,
        string EngineId,
        OcrLanguageMode LanguageMode,
        RecognizedDocumentScore Score);
}
