using System.ComponentModel;
using System.Globalization;
using TextLayer.Application.Models;

namespace TextLayer.App.Services;

public sealed class UiTextService : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["App.Name"] = "TextLayer",
        ["Window.Title.WithFile"] = "TextLayer - {0}",
        ["Common.Settings"] = "Settings",
        ["Common.OpenImage"] = "Open Image",
        ["Common.ReRecognize"] = "Re-recognize",
        ["Common.CopyAllText"] = "Copy All Text",
        ["Common.Clear"] = "Clear",
        ["Common.Fit"] = "Fit",
        ["Common.ActualSize"] = "100%",
        ["Common.ResetView"] = "Reset View",
        ["Common.TextPanel"] = "Text Panel",
        ["Common.ControlCenter"] = "Control Center",
        ["Common.OpenSettings"] = "Open Settings",
        ["Common.ExitApp"] = "Exit TextLayer",
        ["Common.RecognizedText"] = "Recognized Text",
        ["Common.RecognizedTextDescription"] = "Review the OCR result with native text selection and copy support.",
        ["Common.SecondaryViewer"] = "Secondary viewer",
        ["Common.Cancel"] = "Cancel",
        ["Common.Save"] = "Save",
        ["Common.Close"] = "Close",
        ["Common.Theme"] = "Theme",
        ["Common.AppLanguage"] = "App Language",
        ["Common.OcrEngine"] = "OCR Engine",
        ["Common.OcrLanguage"] = "OCR Language",
        ["Common.English"] = "English",
        ["Common.Russian"] = "Russian",
        ["Common.Auto"] = "Auto",
        ["Common.InActiveDevelopment"] = "In active development",
        ["Common.System"] = "System",
        ["Common.Light"] = "Light",
        ["Common.Dark"] = "Dark",
        ["ControlCenter.Description"] = "Manage TextLayer while it runs in the tray. Press Ctrl+Shift+O from any app to capture a local OCR overlay.",
        ["ControlCenter.EnableOverlay"] = "Enable OCR overlay capture",
        ["ControlCenter.CloseToTray"] = "Close button minimizes to tray",
        ["ControlCenter.OcrLanguageHelper"] = "Auto language remains visible for testing and is still in active development. English and Russian are the supported public choices.",
        ["ControlCenter.OcrRecommendation.English"] = "Fast OCR recommended for English captures.",
        ["ControlCenter.OcrRecommendation.Russian"] = "Fast OCR respects Russian selection. Use Accurate OCR when you want fuller Russian and mixed-language coverage.",
        ["ControlCenter.OcrRecommendation.Auto"] = "Smart/Accurate OCR recommended while Auto language is in active development.",
        ["Settings.Title"] = "Settings",
        ["Settings.HeaderTitle"] = "TextLayer Settings",
        ["Settings.HeaderDescription"] = "These preferences are stored locally per user.",
        ["Settings.EnableOverlayWorkflow"] = "Enable the Ctrl+Shift+O OCR overlay workflow",
        ["Settings.LaunchAtStartup"] = "Launch TextLayer at Windows startup",
        ["Settings.CloseToTray"] = "Close button minimizes to tray",
        ["Settings.AutoRunOcr"] = "Run OCR automatically when opening an image",
        ["Settings.AccurateHelper"] = "Accurate mode uses local Tesseract OCR and requires tessdata assets shipped with the app.",
        ["Settings.DebugBounds"] = "Show debug OCR bounds in the secondary viewer",
        ["Settings.KeepTextPanel"] = "Keep the recognized text side panel visible",
        ["Settings.CloseOverlayAfterCopy"] = "Close the OCR overlay automatically after a successful copy",
        ["Settings.UiLanguageHelper"] = "Changes apply after saving. Reopen any already-open auxiliary windows if they still show the previous language.",
        ["About.Title"] = "About TextLayer",
        ["About.Tagline"] = "Local-first OCR overlay utility for Windows.",
        ["About.Description"] = "Run TextLayer in the tray, press Ctrl+Shift+O over another app, then select and copy text from the local overlay.",
        ["About.Version"] = "Version {0}",
        ["Processing.Title"] = "Processing OCR...",
        ["Processing.Description"] = "Analyzing the captured region locally",
        ["RegionSelection.Hint"] = "Drag to capture an OCR region. Esc cancels.",
        ["Overlay.Action.SelectAll"] = "Select All",
        ["Overlay.Action.Copy"] = "Copy",
        ["Overlay.Action.Close"] = "Close",
        ["Tray.CaptureRegion"] = "Capture Region",
        ["Tray.CaptureActiveWindow"] = "Capture Active Window",
        ["Tray.OpenControlCenter"] = "Open Control Center",
        ["Tray.OpenImage"] = "Open Image",
        ["Tray.Settings"] = "Settings",
        ["Tray.About"] = "About",
        ["Tray.Exit"] = "Exit",
        ["Notification.AppTitle"] = "TextLayer",
        ["Notification.OcrTitle"] = "TextLayer OCR",
        ["Notification.OverlayPaused"] = "Overlay capture is paused. Re-enable it from the control center or Settings.",
        ["Notification.Startup"] = "TextLayer is running in the tray. Press Ctrl+Shift+O to capture an OCR region.",
        ["Notification.ActiveWindowMissing"] = "TextLayer could not find an active window to capture.",
        ["Notification.NoTextFound"] = "No selectable text was found in the captured area.",
        ["State.EmptyTitle"] = "TextLayer control center",
        ["State.LoadingImageTitle"] = "Loading image...",
        ["State.RecognizingTitle"] = "Recognizing text...",
        ["State.NoTextFoundTitle"] = "No text found",
        ["State.ErrorTitle"] = "Something went wrong",
        ["State.EmptyDescription.Enabled"] = "Press Ctrl+Shift+O from any app to capture an OCR overlay. Use this window and the tray menu to manage settings, OCR mode, and secondary viewer actions.",
        ["State.EmptyDescription.Disabled"] = "Overlay capture is currently paused. Re-enable it here to use Ctrl+Shift+O from other apps, or open the secondary viewer when you want to inspect an image manually.",
        ["State.LoadingImageDescription"] = "Preparing the image viewer and normalizing orientation.",
        ["State.RecognizingDescription.Fast"] = "Windows OCR is analyzing the image locally in Fast mode.",
        ["State.RecognizingDescription.Accurate"] = "Tesseract OCR is analyzing the image locally in Accurate mode.",
        ["State.RecognizingDescription.Auto"] = "TextLayer is choosing the best local OCR engine and language profile for this image.",
        ["State.NoTextFoundDescription"] = "OCR finished successfully, but no selectable text was detected.",
        ["Status.Ready"] = "Ready",
        ["Status.LoadingImage"] = "Loading image...",
        ["Status.ImageLoaded"] = "Loaded {0}",
        ["Status.ImageReady"] = "Image ready. Run OCR when you want to analyze the text layer.",
        ["Status.Recognizing"] = "Recognizing text...",
        ["Status.NoTextDetected"] = "Recognition finished. No text was detected.",
        ["Status.RecognizedSummary"] = "Recognized {0} words across {1} lines with {2}.",
        ["Status.CopiedAll"] = "Copied all recognized text.",
        ["Status.CopiedSelection"] = "Copied {0} selected words.",
        ["Status.ImageCleared"] = "Image cleared.",
        ["Overlay.Status.Enabled"] = "Ctrl+Shift+O is enabled. TextLayer is ready to capture OCR overlays from other apps.",
        ["Overlay.Status.Disabled"] = "Overlay capture is paused. Re-enable it here or in Settings before using the global hotkey again.",
        ["Hover.Word"] = "Hover: {0}",
        ["FileDialog.Title"] = "Open image",
        ["FileDialog.Filter"] = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap (*.bmp)|*.bmp|WebP (*.webp)|*.webp",
        ["OcrMode.Auto"] = "Auto",
        ["OcrMode.Fast"] = "Fast (Windows OCR)",
        ["OcrMode.Accurate"] = "Accurate (Tesseract)",
        ["OcrLanguage.Auto"] = "Auto",
        ["OcrLanguage.English"] = "English",
        ["OcrLanguage.Russian"] = "Russian",
        ["UiLanguage.English"] = "English",
        ["UiLanguage.Russian"] = "Russian",
        ["Theme.System"] = "System",
        ["Theme.Light"] = "Light",
        ["Theme.Dark"] = "Dark",
    };

    private static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>
    {
        ["App.Name"] = "TextLayer",
        ["Window.Title.WithFile"] = "TextLayer - {0}",
        ["Common.Settings"] = "Настройки",
        ["Common.OpenImage"] = "Открыть изображение",
        ["Common.ReRecognize"] = "Распознать заново",
        ["Common.CopyAllText"] = "Копировать весь текст",
        ["Common.Clear"] = "Очистить",
        ["Common.Fit"] = "По размеру",
        ["Common.ActualSize"] = "100%",
        ["Common.ResetView"] = "Сбросить вид",
        ["Common.TextPanel"] = "Текстовая панель",
        ["Common.ControlCenter"] = "Центр управления",
        ["Common.OpenSettings"] = "Открыть настройки",
        ["Common.ExitApp"] = "Выйти из TextLayer",
        ["Common.RecognizedText"] = "Распознанный текст",
        ["Common.RecognizedTextDescription"] = "Проверьте результат OCR со стандартным выделением и копированием текста.",
        ["Common.SecondaryViewer"] = "Дополнительный просмотр",
        ["Common.Cancel"] = "Отмена",
        ["Common.Save"] = "Сохранить",
        ["Common.Close"] = "Закрыть",
        ["Common.Theme"] = "Тема",
        ["Common.AppLanguage"] = "Язык интерфейса",
        ["Common.OcrEngine"] = "OCR-движок",
        ["Common.OcrLanguage"] = "Язык OCR",
        ["Common.English"] = "Английский",
        ["Common.Russian"] = "Русский",
        ["Common.Auto"] = "Авто",
        ["Common.InActiveDevelopment"] = "В активной разработке",
        ["Common.System"] = "Системная",
        ["Common.Light"] = "Светлая",
        ["Common.Dark"] = "Тёмная",
        ["ControlCenter.Description"] = "Управляйте TextLayer, пока приложение работает в трее. Нажмите Ctrl+Shift+O в любом приложении, чтобы захватить локальный OCR-оверлей.",
        ["ControlCenter.EnableOverlay"] = "Включить OCR-оверлей",
        ["ControlCenter.CloseToTray"] = "Кнопка закрытия сворачивает приложение в трей",
        ["ControlCenter.OcrLanguageHelper"] = "Автоязык оставлен видимым для тестирования и всё ещё находится в активной разработке. Для обычного использования поддерживаются английский и русский.",
        ["ControlCenter.OcrRecommendation.English"] = "Для английских захватов рекомендуется Fast OCR.",
        ["ControlCenter.OcrRecommendation.Russian"] = "Fast OCR уважает выбранный русский язык. Используйте Accurate OCR, когда нужен более полный охват русского и смешанного текста.",
        ["ControlCenter.OcrRecommendation.Auto"] = "Рекомендуется Smart/Accurate OCR, пока автоязык находится в активной разработке.",
        ["Settings.Title"] = "Настройки",
        ["Settings.HeaderTitle"] = "Настройки TextLayer",
        ["Settings.HeaderDescription"] = "Эти параметры сохраняются локально для текущего пользователя.",
        ["Settings.EnableOverlayWorkflow"] = "Включить OCR-оверлей по Ctrl+Shift+O",
        ["Settings.LaunchAtStartup"] = "Запускать TextLayer вместе с Windows",
        ["Settings.CloseToTray"] = "Кнопка закрытия сворачивает приложение в трей",
        ["Settings.AutoRunOcr"] = "Запускать OCR автоматически при открытии изображения",
        ["Settings.AccurateHelper"] = "Режим Accurate использует локальный Tesseract OCR и требует tessdata, поставляемые вместе с приложением.",
        ["Settings.DebugBounds"] = "Показывать отладочные границы OCR во втором просмотрщике",
        ["Settings.KeepTextPanel"] = "Оставлять панель распознанного текста открытой",
        ["Settings.CloseOverlayAfterCopy"] = "Закрывать OCR-оверлей после успешного копирования",
        ["Settings.UiLanguageHelper"] = "Изменения применяются после сохранения. Если уже открытые дополнительные окна остались на прежнем языке, откройте их заново.",
        ["About.Title"] = "О TextLayer",
        ["About.Tagline"] = "Локальная OCR-утилита с оверлеем для Windows.",
        ["About.Description"] = "Запустите TextLayer в трее, нажмите Ctrl+Shift+O поверх другого приложения, затем выделяйте и копируйте текст из локального оверлея.",
        ["About.Version"] = "Версия {0}",
        ["Processing.Title"] = "Обработка OCR...",
        ["Processing.Description"] = "Локально анализируем захваченную область",
        ["RegionSelection.Hint"] = "Потяните мышью, чтобы захватить область для OCR. Esc отменяет.",
        ["Overlay.Action.SelectAll"] = "Выделить всё",
        ["Overlay.Action.Copy"] = "Копировать",
        ["Overlay.Action.Close"] = "Закрыть",
        ["Tray.CaptureRegion"] = "Захватить область",
        ["Tray.CaptureActiveWindow"] = "Захватить активное окно",
        ["Tray.OpenControlCenter"] = "Открыть центр управления",
        ["Tray.OpenImage"] = "Открыть изображение",
        ["Tray.Settings"] = "Настройки",
        ["Tray.About"] = "О программе",
        ["Tray.Exit"] = "Выход",
        ["Notification.AppTitle"] = "TextLayer",
        ["Notification.OcrTitle"] = "OCR TextLayer",
        ["Notification.OverlayPaused"] = "Захват оверлея приостановлен. Включите его снова в центре управления или в настройках.",
        ["Notification.Startup"] = "TextLayer работает в трее. Нажмите Ctrl+Shift+O, чтобы захватить область для OCR.",
        ["Notification.ActiveWindowMissing"] = "TextLayer не смог найти активное окно для захвата.",
        ["Notification.NoTextFound"] = "В захваченной области не найдено выделяемого текста.",
        ["State.EmptyTitle"] = "Центр управления TextLayer",
        ["State.LoadingImageTitle"] = "Загрузка изображения...",
        ["State.RecognizingTitle"] = "Распознавание текста...",
        ["State.NoTextFoundTitle"] = "Текст не найден",
        ["State.ErrorTitle"] = "Что-то пошло не так",
        ["State.EmptyDescription.Enabled"] = "Нажмите Ctrl+Shift+O в любом приложении, чтобы захватить OCR-оверлей. Используйте это окно и меню трея для управления настройками, режимом OCR и вторым просмотрщиком.",
        ["State.EmptyDescription.Disabled"] = "Захват оверлея сейчас приостановлен. Включите его здесь, чтобы использовать Ctrl+Shift+O в других приложениях, или откройте дополнительный просмотрщик для ручной проверки изображения.",
        ["State.LoadingImageDescription"] = "Подготавливаем просмотрщик изображения и нормализуем ориентацию.",
        ["State.RecognizingDescription.Fast"] = "Windows OCR локально анализирует изображение в режиме Fast.",
        ["State.RecognizingDescription.Accurate"] = "Tesseract OCR локально анализирует изображение в режиме Accurate.",
        ["State.RecognizingDescription.Auto"] = "TextLayer подбирает наиболее подходящий локальный OCR-движок и языковой профиль для этого изображения.",
        ["State.NoTextFoundDescription"] = "OCR завершился успешно, но выделяемый текст не обнаружен.",
        ["Status.Ready"] = "Готово",
        ["Status.LoadingImage"] = "Загрузка изображения...",
        ["Status.ImageLoaded"] = "Загружено: {0}",
        ["Status.ImageReady"] = "Изображение готово. Запустите OCR, когда захотите проанализировать текстовый слой.",
        ["Status.Recognizing"] = "Распознавание текста...",
        ["Status.NoTextDetected"] = "Распознавание завершено. Текст не обнаружен.",
        ["Status.RecognizedSummary"] = "Распознано {0} слов в {1} строках с помощью {2}.",
        ["Status.CopiedAll"] = "Весь распознанный текст скопирован.",
        ["Status.CopiedSelection"] = "Скопировано выделенных слов: {0}.",
        ["Status.ImageCleared"] = "Изображение очищено.",
        ["Overlay.Status.Enabled"] = "Ctrl+Shift+O включён. TextLayer готов захватывать OCR-оверлеи поверх других приложений.",
        ["Overlay.Status.Disabled"] = "Захват оверлея приостановлен. Включите его здесь или в настройках перед повторным использованием глобальной горячей клавиши.",
        ["Hover.Word"] = "Под курсором: {0}",
        ["FileDialog.Title"] = "Открыть изображение",
        ["FileDialog.Filter"] = "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap (*.bmp)|*.bmp|WebP (*.webp)|*.webp",
        ["OcrMode.Auto"] = "Авто",
        ["OcrMode.Fast"] = "Fast (Windows OCR)",
        ["OcrMode.Accurate"] = "Accurate (Tesseract)",
        ["OcrLanguage.Auto"] = "Авто",
        ["OcrLanguage.English"] = "Английский",
        ["OcrLanguage.Russian"] = "Русский",
        ["UiLanguage.English"] = "English",
        ["UiLanguage.Russian"] = "Русский",
        ["Theme.System"] = "Системная",
        ["Theme.Light"] = "Светлая",
        ["Theme.Dark"] = "Тёмная",
    };

    private UiLanguagePreference currentLanguage = UiLanguagePreference.English;
    private IReadOnlyDictionary<string, string> currentCatalog = English;

    private UiTextService()
    {
    }

    public static UiTextService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiLanguagePreference CurrentLanguage => currentLanguage;

    public string this[string key]
        => currentCatalog.TryGetValue(key, out var value)
            ? value
            : English.TryGetValue(key, out var fallback)
                ? fallback
                : key;

    public void ApplyLanguage(UiLanguagePreference language)
    {
        currentLanguage = language;
        currentCatalog = language == UiLanguagePreference.Russian ? Russian : English;

        var cultureName = language == UiLanguagePreference.Russian ? "ru-RU" : "en-US";
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string Format(string key, params object[] arguments)
        => string.Format(CultureInfo.CurrentCulture, this[key], arguments);
}
