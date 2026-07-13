
namespace ImageResizer
{
    public class CacheCleanerHostedService : BackgroundService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ImageResizerOptions _options;
        private readonly ILogger<CacheCleanerHostedService> _logger;

        public CacheCleanerHostedService(IWebHostEnvironment env, ImageResizerOptions options, ILogger<CacheCleanerHostedService> logger)
        {
            _env = env;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            int targetHour = Math.Clamp(_options.ExecutionHour, 0, 23);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_options.EnableDiskCache || !_options.EnableBackgroundCleanup)
                {
                    _logger.LogInformation("Image cache cleaner background service is currently idle (disabled by configuration).");
                    await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
                    continue;
                }

                DateTime now = _options.UseUtcTime ? DateTime.UtcNow : DateTime.Now;
                DateTime nextRunTime = new DateTime(now.Year, now.Month, now.Day, targetHour, 0, 0, _options.UseUtcTime ? DateTimeKind.Utc : DateTimeKind.Local);

                if (now >= nextRunTime)
                {
                    nextRunTime = nextRunTime.AddDays(1);
                }

                TimeSpan delayTime = nextRunTime - now;
                string timeZoneLabel = _options.UseUtcTime ? "UTC" : "Local Time";
                _logger.LogInformation("Cache cleaner scheduled to run in {DelayHours:F2} hours (at {TargetHour}:00 {TimeZone}).",
                    delayTime.TotalHours, targetHour, timeZoneLabel);

                await Task.Delay(delayTime, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    string cacheFolderPath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, _options.CacheFolderName);
                    var directoryInfo = new DirectoryInfo(cacheFolderPath);

                    if (directoryInfo.Exists)
                    {
                        _logger.LogInformation("Starting midnight image cache cleanup...");
                        var files = directoryInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly);
                        int processedCount = 0;

                        foreach (var fileInfo in files)
                        {
                            if (stoppingToken.IsCancellationRequested) break;

                            DateTime fileTime = _options.UseUtcTime ? fileInfo.LastWriteTimeUtc : fileInfo.LastWriteTime;

                            if (now - fileTime > _options.CacheDuration)
                            {
                                try
                                {
                                    fileInfo.Delete();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Could not delete cache file {Name}: {Message}", fileInfo.Name, ex.Message);
                                }
                            }

                            processedCount++;
                            if (processedCount % _options.CleanupBatchSize == 0)
                            {
                                await Task.Delay(_options.CleanupThrottleDelayMs, stoppingToken);
                            }
                        }
                        _logger.LogInformation("Midnight image cache cleanup finished.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during background cache cleanup.");
                }
            }
        }
    }
}