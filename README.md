# SharpPixel.AspNetCore.ImageResizer 🚀

[![NuGet Version](https://img.shields.io/nuget/v/SharpPixel.AspNetCore.ImageResizer)](https://www.nuget.org/packages/SharpPixel.AspNetCore.ImageResizer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SharpPixel.AspNetCore.ImageResizer)](https://www.nuget.org/packages/SharpPixel.AspNetCore.ImageResizer)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

A high-performance, asynchronous, and **DoS-protected** image resizing middleware for ASP.NET Core built on top of **SkiaSharp v3**. It intercepts static image requests on-the-fly, processes them based on query parameters, and caches the result for maximum efficiency.

---

## 🌟 Features

-  **High Performance:** Powered by SkiaSharp v3 for fast, native image manipulation.
-  **DoS Protection:** Automatically prevents server resource exhaustion by restricting resizing requests to predefined dimensions and qualities.
-  **Smart Caching:** Server-side caching for processed images and customizable `Cache-Control` headers for browsers.
-  **Modern Formats:** Supports converting images to next-gen formats like **WebP** on-the-fly.
-  **Flexible Processing Modes:** Support for `crop` (smart cropping) and `pad` (padding with backgrounds).

---

## 📦 Installation

Install the package via NuGet CLI:


```bash
dotnet add package SharpPixel.AspNetCore.ImageResizer
```

Or via the Package Manager Console:
```bash
Install-Package SharpPixel.AspNetCore.ImageResizer
```

🚀 Quick Start & Integration
1. Configure your Program.cs
To activate the image resizing capabilities, register the service and place the middleware in your request pipeline. Note: The middleware must be placed before UseStaticFiles.

```bash
using Mind.AspNetCore.ImageResizer;

var builder = WebApplication.CreateBuilder(args);

// Add services with DoS protection and cache configurations
builder.Services.AddImageResizer(options =>
{
    options.MaxDimension = 2000;                          // Safety ceiling for width/height
    options.CacheDuration = TimeSpan.FromHours(12);        // Server-side disk cache lifetime
    options.CacheControlHeader = "public, max-age=43200"; // Browser-side cache (12 Hours)
});

var app = builder.Build();

// CRITICAL: Intercept image requests before the static files handler serves them
app.UseImageResizer();

app.UseStaticFiles();

app.MapGet("/", () => "Image Resizer is running smoothly! 🎉");

app.Run();
```
Image Resizing Modes & Query Parameters
Once the middleware is active, you can dynamically transform any image located inside your wwwroot folder simply by appending query parameters (w, h, mode, quality, format) to its URL.

Here are the visual modes supported by the package:

1. Resize by Width Only (w)
Maintains the original aspect ratio automatically while scaling the width down to 300px.

```bash
<img src="/images/scenery.jpg?w=300" alt="Resized Width" />
```

2. Smart Center Crop (mode=crop)Resizes the image and crops it perfectly into a $400 \times 400$ square from the center, discarding any overflowing edges. Excellent for user avatars!

```bash
<img src="/images/avatar.jpg?w=400&h=400&mode=crop" alt="Center Cropped" />
```

3. Padded Fit (mode=pad)Fits the entire image inside an $800 \times 600$ bounding box without cropping anything. Any empty spaces are padded with a solid background color (perfect for e-commerce product grids).

```bash
<img src="/images/product.jpg?w=800&h=600&mode=pad" alt="Padded Product" />
 ```

4. Format Conversion & Quality Compression (format & quality)
Converts the output format on-the-fly to a next-gen format like WebP and drops the compression quality to 60% for extreme web performance optimization.

```bash
<img src="/images/banner.png?w=1000&quality=60&format=webp" alt="Optimized WebP" />
 ```

##  Query Reference Table

| Parameter | Allowed Values | Example | Description |
| :--- | :--- | :--- | :--- |
| `w` | Integer (up to `MaxDimension`) | `?w=500` | Sets the target width in pixels. |
| `h` | Integer (up to `MaxDimension`) | `?h=300` | Sets the target height in pixels. |
| `mode` | `crop` \| `pad` | `?mode=crop` | The resizing algorithm/style to apply (`crop` for smart center crop, `pad` for padded fit). |
| `quality` | `1` to `100` | `?quality=75` | Sets the image compression level (lower means smaller file size). |
| `format` | `webp` \| `jpeg` \| `png` | `?format=webp` | Forces on-the-fly image format conversion. |

##  Configuration Options

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `MaxDimension` | `int` | `3000` | The maximum width or height allowed for a resizing request to avoid DoS attacks. |
| `CacheDuration` | `TimeSpan` | `12 Hours` | How long the resized image remains cached on the server disk. |
| `CacheControlHeader` | `string` | `public, max-age=43200` | The standard HTTP header sent to client browsers for client-side caching. |

## ⭐ Support the Project
If you find this package useful, please consider giving it a **Star** on GitHub! It helps the project grow and motivates further development.
