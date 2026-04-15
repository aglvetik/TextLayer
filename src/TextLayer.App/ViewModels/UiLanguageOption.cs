using TextLayer.Application.Models;
using TextLayer.App.Services;

namespace TextLayer.App.ViewModels;

public sealed class UiLanguageOption(string labelKey, UiLanguagePreference value)
{
    public string Label => UiTextService.Instance[labelKey];

    public UiLanguagePreference Value { get; } = value;
}
