using TextLayer.Application.Models;
using TextLayer.App.Services;

namespace TextLayer.App.ViewModels;

public sealed class OcrModeOption(
    string labelKey,
    OcrMode value,
    bool isEnabled = true,
    string? descriptionKey = null)
{
    public string Label => UiTextService.Instance[labelKey];

    public string Description => descriptionKey is null ? string.Empty : UiTextService.Instance[descriptionKey];

    public bool HasDescription => descriptionKey is not null;

    public bool IsEnabled { get; } = isEnabled;

    public OcrMode Value { get; } = value;
}
