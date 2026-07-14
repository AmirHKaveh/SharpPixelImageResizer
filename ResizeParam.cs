namespace SharpPixel.AspNetCore.ImageResizer
{
    public class ImageResizerOptions
    {
        public int MaxDimension { get; set; } = 4000;
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(7);
        public string CacheControlHeader { get; set; } = "public, max-age=604800";
        public string[] AllowedFormats { get; set; } = { "webp", "jpeg", "jpg", "png" };
        public bool EnableDiskCache { get; set; } = false;
        public string CacheFolderName { get; set; } = "_imagecache";

        public bool EnableBackgroundCleanup { get; set; } = true;
        public int CleanupBatchSize { get; set; } = 50;
        public int CleanupThrottleDelayMs { get; set; } = 100;
        public int ExecutionHour { get; set; } = 3;
        public bool UseUtcTime { get; set; } = false;
    }

    public class ResizeParams
    {
        public static readonly string[] Modes = { "crop", "stretch", "pad", "max" };

        public bool HasParams { get; set; }
        public string Format { get; set; } = "jpeg";
        public int Quality { get; set; } = 75;
        public int W { get; set; }
        public int H { get; set; }
        public string Mode { get; set; } = "max";
        public string BgColor { get; set; } = "ffffff";

        public override string ToString()
        {
            return $"{Format}_{Quality}_{W}x{H}_{Mode}_{BgColor}";
        }
    }
}
