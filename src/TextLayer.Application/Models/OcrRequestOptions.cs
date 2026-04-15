namespace TextLayer.Application.Models;

public sealed record OcrRequestOptions(OcrMode Mode, OcrLanguageMode LanguageMode)
{
    public static OcrRequestOptions Default { get; } = new(OcrMode.Auto, OcrLanguageMode.Auto);
}
