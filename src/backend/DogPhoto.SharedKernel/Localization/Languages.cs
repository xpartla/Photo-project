namespace DogPhoto.SharedKernel.Localization;

public static class Languages
{
    public const string Slovak = "sk";
    public const string English = "en";
    public const string Default = Slovak;

    /// <summary>Maps any input to a supported language code, falling back to the default.</summary>
    public static string Normalize(string? lang) =>
        string.Equals(lang, English, StringComparison.OrdinalIgnoreCase) ? English : Default;

    public static string Pick(string lang, string sk, string en) => lang == English ? en : sk;
}
