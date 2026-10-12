namespace Hmi.Hardware.Contracts;

public record ResourceConstraints(
    long FlashSizeBytes,
    long RamSizeBytes,
    long RecommendedLvglHeapBytes,
    int MaxTextureSize
);
