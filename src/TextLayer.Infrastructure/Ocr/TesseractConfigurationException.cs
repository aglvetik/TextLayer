namespace TextLayer.Infrastructure.Ocr;

public sealed class TesseractConfigurationException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
