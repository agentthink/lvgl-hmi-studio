namespace Hmi.Hardware.Contracts;

public enum ColorDepth { RGB565, ARGB8888, Indexed8 }
public enum DisplayInterface { RGB666, RGB888, MIPI_DSI, SPI, I80 }

public record DisplayConfig(
    int DefaultWidth,
    int DefaultHeight,
    IReadOnlyList<ColorDepth> SupportedColorDepths,
    DisplayInterface Interface,
    int MaxRefreshRate
);
