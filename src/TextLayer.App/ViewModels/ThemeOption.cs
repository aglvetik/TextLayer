using TextLayer.Application.Models;
using TextLayer.App.Services;

namespace TextLayer.App.ViewModels;

public sealed class ThemeOption(string labelKey, ThemePreference value)
{
    public string Label => UiTextService.Instance[labelKey];

    public ThemePreference Value { get; } = value;
}
