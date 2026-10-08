namespace SGY.CheckIn.Models;

/// <summary>
/// A DYMO LabelWriter label the Kids name label can be printed on (an admin setting). The
/// label is laid out for Large Address and scaled to the others: <see cref="Scale"/> sizes the
/// type and spacing, <see cref="BandMm"/> is the width of the band down the left.
/// </summary>
/// <remarks>
/// All fit the LabelWriter 450's 56 mm print width, printed across the label. The paper size
/// in Chrome's print settings (or the DYMO driver's default) has to match, or the label is cut
/// off or shrunk.
/// </remarks>
public sealed record LabelSize(string Key, string Name, string Codes, double WidthMm, double HeightMm, double BandMm, double Scale)
{
    public static readonly LabelSize LargeAddress = new("large-address", "Large Address", "30321 · 99012", 89, 36, 18.8, 1);

    public static readonly IReadOnlyList<LabelSize> All =
    [
        LargeAddress,
        new("address", "Address", "30252 · 99010", 89, 28, 16, 0.8),
        new("multi-purpose", "Multi-purpose", "11354", 57, 32, 14, 0.72),
        new("shipping", "Shipping", "99014", 101, 54, 22, 1.2),
        new("name-badge", "Name badge", "30256 · 30857", 102, 59, 23, 1.25),
    ];

    /// <summary>The size saved under <paramref name="key"/>, or Large Address.</summary>
    public static LabelSize FromKey(string? key) => All.FirstOrDefault(s => s.Key == key) ?? LargeAddress;

    /// <summary>"89 × 36 mm"</summary>
    public string Dimensions => $"{WidthMm:0.#} × {HeightMm:0.#} mm";
}
