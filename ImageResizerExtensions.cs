namespace SharpPixel.AspNetCore.ImageResizer
{
    public static class ImageResizerExtensions
    {
        public static IServiceCollection AddImageResizer(this IServiceCollection services, Action<ImageResizerOptions>? configureOptions = null)
        {
            services.AddMemoryCache();

            var options = new ImageResizerOptions();
            configureOptions?.Invoke(options);
            services.AddSingleton(options);
            services.AddHostedService<CacheCleanerHostedService>();
            return services;
        }

        public static IApplicationBuilder UseImageResizer(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ImageResizerMiddleware>();
        }
    }
}
