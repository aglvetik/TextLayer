using TextLayer.Application.Models;
using TextLayer.App.Services;

namespace TextLayer.App.ViewModels;

public sealed class OcrModeOption(string labelKey, OcrMode value)
{
    public string Label => UiTextService.Instance[labelKey];

    public OcrMode Value { get; } = value;
}
