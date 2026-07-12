using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

using SkiaSharp;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;

namespace ImageResizer
{
    public class ImageResizerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ImageResizerMiddleware> _logger;
        private readonly ImageResizerOptions _options;

        private static readonly string[] AllowedExtensions = { "png", "jpg", "jpeg", "webp" };
        private static readonly string[] ResizeQueryKeys = { "w", "h", "format", "quality", "mode" };

        public ImageResizerMiddleware(
            RequestDelegate next,
            IWebHostEnvironment env,
            IMemoryCache memoryCache,
            ILogger<ImageResizerMiddleware> logger,
            ImageResizerOptions options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path;

            if (context.Request.Query.Count == 0 || !IsImagePath(path))
            {
                await _next(context);
                return;
            }

            var resizeParams = GetResizeParams(path, context.Request.Query);
            if (!resizeParams.HasParams || !_options.AllowedFormats.Contains(resizeParams.Format))
            {
                await _next(context);
                return;
            }

            var rootPath = _env.WebRootPath ?? _env.ContentRootPath;
            var provider = new PhysicalFileProvider(rootPath);
            var fileInfo = provider.GetFileInfo(path.Value);
            var imagePath = fileInfo.PhysicalPath;

            if (imagePath == null || !File.Exists(imagePath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Resource not found.");
                return;
            }

            try
            {
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(imagePath);
                var buffer = await GetImageDataAsync(imagePath, resizeParams, lastWriteTimeUtc);

                context.Response.ContentType = GetResponseContentType(resizeParams.Format);
                context.Response.ContentLength = buffer.Length;
                context.Response.Headers["Cache-Control"] = _options.CacheControlHeader;

                await context.Response.Body.WriteAsync(buffer, 0, buffer.Length, context.RequestAborted);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Failed to decode image at {ImagePath}", imagePath);
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsync("Unable to process the requested image.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error resizing image at {ImagePath}", imagePath);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("An error occurred while processing the image.");
            }
        }

        private async Task<byte[]> GetImageDataAsync(string imagePath, ResizeParams resizeParams, DateTime lastWriteTimeUtc)
        {
            var cacheKey = $"img_resize:{imagePath}|{lastWriteTimeUtc.Ticks}|{resizeParams}";

            if (_memoryCache.TryGetValue<byte[]>(cacheKey, out var cachedBytes))
            {
                return cachedBytes;
            }

            byte[] processedBytes;
            using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            using (var memoryStream = new MemoryStream())
            {
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var managedStream = new SKManagedStream(memoryStream, false);
                using var codec = SKCodec.Create(managedStream);
                if (codec == null)
                    throw new ArgumentException("Unable to create a decoder for the provided image data.");

                var info = codec.Info;
                using var bitmap = new SKBitmap(
                    info.Width,
                    info.Height,
                    SKImageInfo.PlatformColorType,
                    info.IsOpaque ? SKAlphaType.Opaque : SKAlphaType.Premul);

                var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(out _));
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                    throw new ArgumentException($"Unable to decode image: {result}");

                int targetW = resizeParams.W == 0 ? bitmap.Width : resizeParams.W;
                int targetH = resizeParams.H == 0 ? bitmap.Height : resizeParams.H;

                if (resizeParams.W == 0 && resizeParams.H != 0)
                    targetW = (int)Math.Round(bitmap.Width * (float)targetH / bitmap.Height);
                else if (resizeParams.H == 0 && resizeParams.W != 0)
                    targetH = (int)Math.Round(bitmap.Height * (float)targetW / bitmap.Width);

                using var resizedBitmap = ResizeByMode(bitmap, resizeParams.Mode, targetW, targetH);
                using var resizedImage = SKImage.FromBitmap(resizedBitmap);

                var encodeFormat = resizeParams.Format switch
                {
                    "png" => SKEncodedImageFormat.Png,
                    "webp" => SKEncodedImageFormat.Webp,
                    _ => SKEncodedImageFormat.Jpeg
                };

                using var encodedData = resizedImage.Encode(encodeFormat, resizeParams.Quality);
                processedBytes = encodedData.ToArray();
            }

            _memoryCache.Set(cacheKey, processedBytes, _options.CacheDuration);
            return processedBytes;
        }

        private static SKBitmap ResizeByMode(SKBitmap source, string mode, int targetW, int targetH)
        {
            var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

            switch (mode)
            {
                case "stretch":
                    return source.Resize(new SKImageInfo(targetW, targetH, SKImageInfo.PlatformColorType, source.AlphaType), samplingOptions);
                case "crop":
                    return CropAndResize(source, targetW, targetH);
                case "pad":
                    return ResizeWithPadding(source, targetW, targetH);
                case "max":
                default:
                    var (fitW, fitH) = FitWithinBox(source.Width, source.Height, targetW, targetH);
                    return source.Resize(new SKImageInfo(fitW, fitH, SKImageInfo.PlatformColorType, source.AlphaType), samplingOptions);
            }
        }

        private static (int w, int h) FitWithinBox(int sourceW, int sourceH, int boxW, int boxH)
        {
            var sourceRatio = (float)sourceW / sourceH;
            var boxRatio = (float)boxW / boxH;

            return sourceRatio > boxRatio
                ? (boxW, (int)Math.Round(sourceH * ((float)boxW / sourceW)))
                : ((int)Math.Round(sourceW * ((float)boxH / sourceH)), boxH);
        }

        private static SKBitmap CropAndResize(SKBitmap source, int targetW, int targetH)
        {
            var sourceRatio = (float)source.Width / source.Height;
            var targetRatio = (float)targetW / targetH;

            int cropW, cropH;
            if (sourceRatio > targetRatio)
            {
                cropH = source.Height;
                cropW = (int)Math.Round(cropH * targetRatio);
            }
            else
            {
                cropW = source.Width;
                cropH = (int)Math.Round(cropW / targetRatio);
            }

            var left = (source.Width - cropW) / 2;
            var top = (source.Height - cropH) / 2;

            var cropRect = new SKRect(left, top, left + cropW, top + cropH);
            var destRect = new SKRect(0, 0, cropW, cropH);

            using var cropped = new SKBitmap(cropW, cropH, source.ColorType, source.AlphaType);
            using (var canvas = new SKCanvas(cropped))
            {
                using var skImage = SKImage.FromBitmap(source);
                canvas.DrawImage(skImage, cropRect, destRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            }

            return cropped.Resize(
                new SKImageInfo(targetW, targetH, SKImageInfo.PlatformColorType, source.AlphaType),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }

        private static SKBitmap ResizeWithPadding(SKBitmap source, int targetW, int targetH)
        {
            var (fitW, fitH) = FitWithinBox(source.Width, source.Height, targetW, targetH);
            var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

            using var fitted = source.Resize(new SKImageInfo(fitW, fitH, SKImageInfo.PlatformColorType, source.AlphaType), samplingOptions);
            var padded = new SKBitmap(targetW, targetH, source.ColorType, source.AlphaType);

            using var canvas = new SKCanvas(padded);
            canvas.Clear(source.AlphaType == SKAlphaType.Opaque ? SKColors.White : SKColors.Transparent);

            var left = (targetW - fitW) / 2;
            var top = (targetH - fitH) / 2;
            canvas.DrawBitmap(fitted, left, top);

            return padded;
        }

        private static bool IsImagePath(PathString path)
        {
            if (!path.HasValue) return false;
            var value = path.Value;
            var dotIndex = value.LastIndexOf('.');
            if (dotIndex < 0 || dotIndex == value.Length - 1) return false;

            var ext = value.Substring(dotIndex + 1);
            return AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private ResizeParams GetResizeParams(PathString path, IQueryCollection query)
        {
            var resizeParams = new ResizeParams
            {
                HasParams = ResizeQueryKeys.Any(query.ContainsKey)
            };

            if (!resizeParams.HasParams) return resizeParams;

            if (query.ContainsKey("format"))
            {
                resizeParams.Format = query["format"].ToString().ToLowerInvariant();
            }
            else
            {
                var dotIndex = path.Value.LastIndexOf('.');
                resizeParams.Format = dotIndex >= 0 ? path.Value.Substring(dotIndex + 1).ToLowerInvariant() : "jpeg";
            }

            int quality = 75;
            if (query.ContainsKey("quality") && int.TryParse(query["quality"], out var parsedQuality))
                quality = parsedQuality;

            resizeParams.Quality = quality switch
            {
                <= 65 => 60,
                <= 82 => 75,
                _ => 90
            };

            int w = 0;
            if (query.ContainsKey("w") && int.TryParse(query["w"], out var parsedW))
                w = Math.Clamp(parsedW, 0, _options.MaxDimension);
            resizeParams.W = w > 0 ? (int)(Math.Round(w / 50.0) * 50) : 0;

            int h = 0;
            if (query.ContainsKey("h") && int.TryParse(query["h"], out var parsedH))
                h = Math.Clamp(parsedH, 0, _options.MaxDimension);
            resizeParams.H = h > 0 ? (int)(Math.Round(h / 50.0) * 50) : 0;

            if (query.ContainsKey("mode"))
            {
                var requestedMode = query["mode"].ToString().ToLowerInvariant();
                if (ResizeParams.Modes.Contains(requestedMode))
                    resizeParams.Mode = requestedMode;
            }

            return resizeParams;
        }

        private static string GetResponseContentType(string imageFormat)
        {
            return imageFormat switch
            {
                "png" => "image/png",
                "webp" => "image/webp",
                _ => "image/jpeg"
            };
        }
    }
}