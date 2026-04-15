using TextLayer.Domain.Geometry;
using TextLayer.Domain.Services;

namespace TextLayer.Tests.Domain;

public sealed class ScreenOverlayCoordinateMapperTests
{
    private readonly ScreenOverlayCoordinateMapper mapper = new();

    [Fact]
    public void ToDipRect_ConvertsPixelsUsingMonitorScale()
    {
        var rect = mapper.ToDipRect(new PixelRect(300, 150, 900, 450), 1.5d, 1.5d);

        Assert.Equal(200d, rect.X, 3);
        Assert.Equal(100d, rect.Y, 3);
        Assert.Equal(600d, rect.Width, 3);
        Assert.Equal(300d, rect.Height, 3);
    }

    [Fact]
    public void CreatePixelRect_ExpandsDipSelectionBackIntoPhysicalPixels()
    {
        var rect = mapper.CreatePixelRect(
            new PointD(10.2d, 20.4d),
            new PointD(110.1d, 70.2d),
            new PixelRect(1920, 0, 2560, 1440),
            1.5d,
            1.5d);

        Assert.Equal(1935, rect.X);
        Assert.Equal(30, rect.Y);
        Assert.Equal(151, rect.Width);
        Assert.Equal(76, rect.Height);
    }
}
