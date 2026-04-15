namespace TextLayer.Application.Models;

public sealed record LoadedImageData(
    string SourcePath,
    int PixelWidth,
    int PixelHeight,
    double DpiX,
    double DpiY,
    int Stride,
    byte[] PixelData,
    bool OrientationNormalized,
    string FileExtension);
