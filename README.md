# SharpPixel.AspNetCore.ImageResizer 🚀

[![NuGet Version](https://img.shields.io/nuget/v/SharpPixel.AspNetCore.ImageResizer)](https://www.nuget.org/packages/SharpPixel.AspNetCore.ImageResizer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SharpPixel.AspNetCore.ImageResizer)](https://www.nuget.org/packages/SharpPixel.AspNetCore.ImageResizer)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

A high-performance, asynchronous, and **DoS-protected** image resizing middleware for ASP.NET Core built on top of **SkiaSharp v3**. It intercepts static image requests on-the-fly, processes them based on query parameters, and caches the result for maximum efficiency.

---

## 🌟 Features

- ⚡ **High Performance:** Powered by SkiaSharp v3 for fast, native image manipulation.
- 🛡️ **DoS Protection:** Automatically prevents server resource exhaustion by restricting resizing requests to predefined dimensions and qualities.
- 📦 **Smart Caching:** Server-side caching for processed images and customizable `Cache-Control` headers for browsers.
- 🔄 **Modern Formats:** Supports converting images to next-gen formats like **WebP** on-the-fly.
- 🛠️ **Flexible Processing Modes:** Support for `crop` (smart cropping) and `pad` (padding with backgrounds).

---

## 📦 Installation

Install the package via NuGet CLI:

```bash
dotnet add package SharpPixel.AspNetCore.ImageResizer