using DogPhoto.Infrastructure.BlobStorage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DogPhoto.Infrastructure.ImagePipeline;

public record ImageMetadata(
    int Width,
    int Height,
    string? CameraSettings,
    string? DominantColor,
    string Blurhash,
    DateTime? ShotDate);

public record VariantInfo(
    int Width,
    int Height,
    string Format,
    int Quality,
    string BlobUrl,
    long SizeBytes);

public record ImageProcessingResult(
    ImageMetadata Metadata,
    List<VariantInfo> Variants);

public interface IImageProcessor
{
    Task<ImageProcessingResult> ProcessAsync(Guid photoId, Stream originalStream);
}

public class ImageProcessor : IImageProcessor
{
    private static readonly int[] TargetWidths = [400, 800, 1200, 2000];
    private const int WebPQuality = 80;
    private const int JpegQuality = 85;

    private readonly IBlobStorageService _blobStorage;

    public ImageProcessor(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public async Task<ImageProcessingResult> ProcessAsync(Guid photoId, Stream originalStream)
    {
        using var image = await Image.LoadAsync<Rgba32>(originalStream);

        var metadata = ExtractMetadata(image);
        var variants = await GenerateVariantsAsync(photoId, image);

        return new ImageProcessingResult(metadata, variants);
    }

    private static ImageMetadata ExtractMetadata(Image<Rgba32> image)
    {
        var exif = image.Metadata.ExifProfile;

        var cameraSettings = ExtractCameraSettings(exif);
        var shotDate = ExtractShotDate(exif);
        var dominantColor = ExtractDominantColor(image);
        var blurhash = EncodeBlurhash(image);

        return new ImageMetadata(
            Width: image.Width,
            Height: image.Height,
            CameraSettings: cameraSettings,
            DominantColor: dominantColor,
            Blurhash: blurhash,
            ShotDate: shotDate);
    }

    private static string? ExtractCameraSettings(ExifProfile? exif)
    {
        if (exif is null) return null;

        var parts = new List<string>();

        if (exif.TryGetValue(ExifTag.Model, out var model) && model.Value is not null)
            parts.Add(model.Value.Trim());

        if (exif.TryGetValue(ExifTag.FNumber, out var fNumber))
            parts.Add($"f/{fNumber.Value.ToDouble():0.#}");

        if (exif.TryGetValue(ExifTag.ExposureTime, out var exposure))
        {
            var val = exposure.Value.ToDouble();
            parts.Add(val >= 1 ? $"{val:0.#}s" : $"1/{1.0 / val:0}s");
        }

        if (exif.TryGetValue(ExifTag.ISOSpeedRatings, out var iso) && iso.Value is not null)
            parts.Add($"ISO {iso.Value}");

        if (exif.TryGetValue(ExifTag.FocalLength, out var focal))
            parts.Add($"{focal.Value.ToDouble():0}mm");

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static DateTime? ExtractShotDate(ExifProfile? exif)
    {
        if (exif is null) return null;

        if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var dateOriginal) && dateOriginal.Value is not null)
        {
            if (DateTime.TryParseExact(dateOriginal.Value, "yyyy:MM:dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var date))
                return date;
        }

        return null;
    }

    private static string ExtractDominantColor(Image<Rgba32> image)
    {
        // Resize to 1x1 to get average/dominant color
        using var thumb = image.Clone(ctx => ctx.Resize(1, 1));
        var pixel = thumb[0, 0];
        return $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";
    }

    private static string EncodeBlurhash(Image<Rgba32> image)
    {
        // Resize to small dimensions for blurhash computation
        const int bhWidth = 32;
        const int bhHeight = 32;
        const int componentsX = 4;
        const int componentsY = 3;

        using var small = image.Clone(ctx => ctx.Resize(bhWidth, bhHeight));

        var pixels = new double[bhWidth * bhHeight * 3];
        for (int y = 0; y < bhHeight; y++)
        {
            for (int x = 0; x < bhWidth; x++)
            {
                var p = small[x, y];
                int idx = (y * bhWidth + x) * 3;
                pixels[idx] = SRGBToLinear(p.R);
                pixels[idx + 1] = SRGBToLinear(p.G);
                pixels[idx + 2] = SRGBToLinear(p.B);
            }
        }

        var factors = new double[componentsX * componentsY][];
        for (int j = 0; j < componentsY; j++)
        {
            for (int i = 0; i < componentsX; i++)
            {
                double r = 0, g = 0, b = 0;
                double normalisation = (i == 0 && j == 0) ? 1 : 2;

                for (int y = 0; y < bhHeight; y++)
                {
                    for (int x = 0; x < bhWidth; x++)
                    {
                        double basis = normalisation
                            * Math.Cos(Math.PI * i * x / bhWidth)
                            * Math.Cos(Math.PI * j * y / bhHeight);
                        int idx = (y * bhWidth + x) * 3;
                        r += basis * pixels[idx];
                        g += basis * pixels[idx + 1];
                        b += basis * pixels[idx + 2];
                    }
                }

                double scale = 1.0 / (bhWidth * bhHeight);
                factors[j * componentsX + i] = [r * scale, g * scale, b * scale];
            }
        }

        // Encode to base83 string
        var chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#$%*+,-./:;=?@[]^_{|}~";
        var result = new List<char>();

        // Size flag
        int sizeFlag = (componentsX - 1) + (componentsY - 1) * 9;
        EncodeBase83(sizeFlag, 1, chars, result);

        // Quantised maximum value
        double maximumValue = 0;
        for (int i = 1; i < factors.Length; i++)
        {
            var factor = factors[i];
            maximumValue = Math.Max(maximumValue, Math.Abs(factor[0]));
            maximumValue = Math.Max(maximumValue, Math.Abs(factor[1]));
            maximumValue = Math.Max(maximumValue, Math.Abs(factor[2]));
        }

        int quantisedMaximumValue;
        if (maximumValue > 0)
        {
            int floor = (int)Math.Floor(maximumValue * 166 - 0.5);
            quantisedMaximumValue = Math.Clamp(floor, 0, 82);
        }
        else
        {
            quantisedMaximumValue = 0;
        }
        EncodeBase83(quantisedMaximumValue, 1, chars, result);

        double realMaximumValue = (quantisedMaximumValue + 1) / 166.0;

        // DC value
        var dc = factors[0];
        int dcValue = (EncodeSRGB(dc[0]) << 16) + (EncodeSRGB(dc[1]) << 8) + EncodeSRGB(dc[2]);
        EncodeBase83(dcValue, 4, chars, result);

        // AC values
        for (int i = 1; i < factors.Length; i++)
        {
            var factor = factors[i];
            int acValue = Math.Max(0, Math.Min(18,
                    (int)Math.Floor(SignPow(factor[0] / realMaximumValue, 0.5) * 9 + 9.5))) * 19 * 19
                + Math.Max(0, Math.Min(18,
                    (int)Math.Floor(SignPow(factor[1] / realMaximumValue, 0.5) * 9 + 9.5))) * 19
                + Math.Max(0, Math.Min(18,
                    (int)Math.Floor(SignPow(factor[2] / realMaximumValue, 0.5) * 9 + 9.5)));
            EncodeBase83(acValue, 2, chars, result);
        }

        return new string(result.ToArray());
    }

    private static void EncodeBase83(int value, int length, string chars, List<char> result)
    {
        for (int i = 1; i <= length; i++)
        {
            int digit = (value / (int)Math.Pow(83, length - i)) % 83;
            result.Add(chars[digit]);
        }
    }

    private static double SRGBToLinear(byte value)
    {
        double v = value / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static int EncodeSRGB(double value)
    {
        double v = Math.Max(0, Math.Min(1, value));
        return v <= 0.0031308
            ? (int)(v * 12.92 * 255 + 0.5)
            : (int)((1.055 * Math.Pow(v, 1 / 2.4) - 0.055) * 255 + 0.5);
    }

    private static double SignPow(double value, double exp)
    {
        return Math.CopySign(Math.Pow(Math.Abs(value), exp), value);
    }

    private async Task<List<VariantInfo>> GenerateVariantsAsync(Guid photoId, Image<Rgba32> image)
    {
        var variants = new List<VariantInfo>();

        foreach (var targetWidth in TargetWidths)
        {
            // Skip variants larger than the original
            if (targetWidth > image.Width) continue;

            var targetHeight = (int)Math.Round((double)image.Height / image.Width * targetWidth);

            using var resized = image.Clone(ctx => ctx.Resize(targetWidth, targetHeight));

            // WebP variant
            var webpVariant = await SaveVariantAsync(photoId, resized, targetWidth, targetHeight, "webp", WebPQuality);
            variants.Add(webpVariant);

            // JPEG variant
            var jpegVariant = await SaveVariantAsync(photoId, resized, targetWidth, targetHeight, "jpeg", JpegQuality);
            variants.Add(jpegVariant);
        }

        return variants;
    }

    private async Task<VariantInfo> SaveVariantAsync(
        Guid photoId, Image<Rgba32> resized, int width, int height, string format, int quality)
    {
        using var ms = new MemoryStream();

        if (format == "webp")
            await resized.SaveAsync(ms, new WebpEncoder { Quality = quality });
        else
            await resized.SaveAsync(ms, new JpegEncoder { Quality = quality });

        var sizeBytes = ms.Length;
        ms.Position = 0;

        var blobName = $"{photoId}/{width}w.{format}";
        var contentType = format == "webp" ? "image/webp" : "image/jpeg";
        var blobUrl = await _blobStorage.UploadAsync(BlobStorageService.ProcessedContainer, blobName, ms, contentType);

        return new VariantInfo(width, height, format, quality, blobUrl, sizeBytes);
    }
}
