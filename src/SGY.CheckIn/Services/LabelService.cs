using System.Text;
using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>
/// Builds the label stuck on a child at check-in: a page exactly one DYMO 30321 Large
/// Address label in size (89 × 36 mm), printed from the check-in computer's browser to the
/// LabelWriter. A black band down the left (logo, age, ministry, date); beside it the child's
/// first name large enough to read across the room, any medical notes in an outlined box
/// marked ⊕, and the parent's name and cell under a rule (no caption: a name and number read
/// as the contact on their own). Never the leaders' comment.
/// </summary>
/// <remarks>
/// The label prints in black only (it's a thermal printer), so contrast comes from weight
/// and the reversed black band rather than colour, and the logo is a black copy
/// (sg-logo-print.png) of the white one the app uses, inverted in the band. The type is
/// Archivo, served by the app so the label looks the same offline. The script at the
/// bottom waits for the font, then shrinks anything too long to fit; labels.js waits for
/// it (window.sgLabelReady) before printing.
///
/// The light style (an admin setting) swaps the black band for a white one behind a rule,
/// for less black on the label.
///
/// A child with a birthday this week (a week either side) gets a cake in place of the logo,
/// and the age they turn (or turned) in place of their age today.
/// Dots under the date mark a child with CMR (one) or in Care Village (two); never both.
/// They're for volunteers who know to look for them, so nothing on the label explains them.
///
/// Once the child is checked in, a QR code beside their name links to the page that checks
/// them out (/out/{code}, see <see cref="CheckInRecord.CheckOutCode"/>): any phone's camera
/// app opens it. The link uses the address the printing computer has the app open on, which
/// is the one the volunteers' phones use too (see <see cref="AppAddress"/>).
/// </remarks>
public class LabelService(IDbContextFactory<AppDbContext> dbFactory, CheckInService checkIns)
{
    // Setting value "light" for the light style; anything else (or none) is the dark one.
    private const string StyleKey = "LabelStyle";
    // A LabelSize.Key; none means Large Address.
    private const string SizeKey = "LabelSize";

    /// <summary>
    /// Null if there's no such child in this group, or the group doesn't print labels.
    /// <paramref name="appAddress"/> is where the app is reached ("http://192.168.0.10:8080"),
    /// for the check-out QR code.
    /// </summary>
    public async Task<string?> RenderAsync(int id, Group group, string appAddress, CancellationToken ct = default)
    {
        if (!group.PrintsLabels())
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var child = await db.Youths.AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == id && y.Group == group, ct);
        if (child is null)
        {
            return null;
        }

        // No QR before they're checked in today: there's nothing to check out.
        var code = await checkIns.GetTodayCheckOutCodeAsync(id, group, ct);
        var checkOutUrl = code is null ? null : $"{appAddress.TrimEnd('/')}/out/{code}";
        return Render(child, await IsLightAsync(db, ct), await GetSizeAsync(db, ct), checkOutUrl);
    }

    /// <summary>The label the printers are loaded with.</summary>
    public async Task<LabelSize> GetSizeAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await GetSizeAsync(db, ct);
    }

    public Task SetSizeAsync(LabelSize size, CancellationToken ct = default) => SaveAsync(SizeKey, size.Key, ct);

    /// <summary>Whether labels print in the light style rather than the dark one.</summary>
    public async Task<bool> IsLightAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await IsLightAsync(db, ct);
    }

    public Task SetLightAsync(bool light, CancellationToken ct = default) => SaveAsync(StyleKey, light ? "light" : "dark", ct);

    private async Task SaveAsync(string key, string value, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var setting = await db.Settings.FindAsync([key], ct);
        if (setting is null)
        {
            db.Settings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<bool> IsLightAsync(AppDbContext db, CancellationToken ct) =>
        await db.Settings.AsNoTracking().AnyAsync(s => s.Key == StyleKey && s.Value == "light", ct);

    private static async Task<LabelSize> GetSizeAsync(AppDbContext db, CancellationToken ct) =>
        LabelSize.FromKey(await db.Settings.AsNoTracking()
            .Where(s => s.Key == SizeKey).Select(s => s.Value).FirstOrDefaultAsync(ct));

    private static string Render(Youth child, bool light, LabelSize size, string? checkOutUrl)
    {
        // CSS and script numbers in invariant culture: "1.35", never "1,35".
        static string N(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        static string E(string? value) => HtmlEncoder.Default.Encode(value ?? "");

        // The name leaders call the child by goes large; any second names join the surname below.
        var names = child.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = names.ElementAtOrDefault(0) ?? "";
        var restOfName = string.Join(' ', names.Skip(1).Append(child.Surname));

        var today = DateOnly.FromDateTime(DateTime.Today);
        // In their birthday week the band celebrates: a cake where the logo goes, and the age
        // they turn (or turned) rather than the age today.
        var birthday = Birthdays.NearbyBirthday(child.DateOfBirth, today);
        var age = birthday is { } day ? day.Year - child.DateOfBirth.Year : Birthdays.AgeOn(child.DateOfBirth, today);
        var ageCaption = birthday is null ? "Age" : birthday >= today ? "Turns" : "Turned";
        // A child too young for a ministry (registered before the age rule) is just SG Kids.
        var ministry = KidsMinistries.For(child.DateOfBirth, today)?.ToDisplayString() ?? "SG Kids";
        var topOfBand = birthday is null
            ? """<img src="/sg-logo-print.png" alt="">"""
            : """
              <svg class="cake" viewBox="0 0 24 24" aria-hidden="true">
                  <path d="M8 .6C9.3 2 9.2 3.6 8 3.9 6.8 3.6 6.7 2 8 .6ZM12 .6C13.3 2 13.2 3.6 12 3.9 10.8 3.6 10.7 2 12 .6ZM16 .6C17.3 2 17.2 3.6 16 3.9 14.8 3.6 14.7 2 16 .6Z"/>
                  <rect x="7.2" y="4.6" width="1.6" height="3.6" rx=".4"/><rect x="11.2" y="4.6" width="1.6" height="3.6" rx=".4"/><rect x="15.2" y="4.6" width="1.6" height="3.6" rx=".4"/>
                  <path d="M6.5 8.8h11a1.5 1.5 0 0 1 1.5 1.5v3.2H5v-3.2a1.5 1.5 0 0 1 1.5-1.5Z"/>
                  <path d="M4.5 14.4h15a1.5 1.5 0 0 1 1.5 1.5v4.6H3v-4.6a1.5 1.5 0 0 1 1.5-1.5Z"/>
                  <path class="icing" d="M3 17.4q1.5-1.5 3 0t3 0t3 0t3 0t3 0t3 0"/>
                  <rect x="1.5" y="21.2" width="21" height="1.5" rx=".75"/>
              </svg>
              """;
        var dots = child.InCmr ? 1 : child.InCareVillage ? 2 : 0;
        var qr = checkOutUrl is null ? "" : QrSvg(checkOutUrl);

        // Nothing at all when there are no medical notes, so the box only ever means something.
        var medical = string.IsNullOrWhiteSpace(child.Medical)
            ? ""
            : $"""
              <p class="medical" id="medical">
                  <svg viewBox="0 0 20 20" aria-hidden="true"><circle cx="10" cy="10" r="10"/><path d="M10 5v10M5 10h10"/></svg>
                  <span>{E(child.Medical)}</span>
              </p>
              """;

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title>Label — {{E(child.Name)}} {{E(child.Surname)}}</title>
            <style>
                @font-face {
                    font-family: 'Archivo';
                    font-style: normal;
                    font-weight: 400 900;
                    font-stretch: 62% 125%;
                    font-display: block;
                    src: url(/fonts/Archivo-Variable.woff2) format('woff2');
                }
                @page { size: {{N(size.WidthMm)}}mm {{N(size.HeightMm)}}mm; margin: 0; }
                /* Laid out for Large Address; --s scales the type and spacing to the other sizes. */
                :root { --s: {{N(size.Scale)}}; }
                * { box-sizing: border-box; }
                html, body { margin: 0; padding: 0; background: #fff; color: #000; }
                body { font-family: Archivo, Arial, Helvetica, sans-serif; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
                p { margin: 0; }
                .label {
                    width: {{N(size.WidthMm)}}mm; height: {{N(size.HeightMm)}}mm; overflow: hidden;
                    display: grid; grid-template-columns: {{N(size.BandMm)}}mm minmax(0, 1fr);
                }
                .band {
                    background: #000; color: #fff;
                    display: flex; flex-direction: column; align-items: center; text-align: center;
                    padding: calc(2.8mm * var(--s)) 0 calc(1.9mm * var(--s));
                }
                .band img { height: calc(6mm * var(--s)); filter: invert(1); }
                .band .age { margin: auto 0; }
                .caption { font-size: calc(6pt * var(--s)); font-weight: 700; letter-spacing: 0.12em; margin-right: -0.12em; text-transform: uppercase; }
                .band .age-value { font-size: calc(30pt * var(--s)); font-weight: 800; line-height: 1; }
                /* The birthday cake, a little bigger than the logo it stands in for; the icing is a line in the band's colour. */
                .band .cake { width: calc(8.5mm * var(--s)); height: calc(8.5mm * var(--s)); margin-top: calc(-1.2mm * var(--s)); fill: currentColor; }
                .band .cake .icing { fill: none; stroke: #000; stroke-width: 1.3; }
                .light .band .cake .icing { stroke: #fff; }
                /* Light: a white band set off by a rule, the same weight as the one over the parent. */
                .light .band { background: #fff; color: #000; border-right: 0.6mm solid #000; }
                .light .band img { filter: none; }
                /* Condensed, so even BRAVEHEARTS fits across the band, with room either side: the
                   printer can be a little off at the label's edge, and a word that fills the band
                   then looks off-centre. Letter spacing also trails the last letter; the negative
                   margin takes it back out so the letters themselves are centred (same for AGE). */
                .band .ministry { font-size: calc(7pt * var(--s)); font-weight: 800; font-stretch: 72%; letter-spacing: 0.06em; margin-right: -0.06em; text-transform: uppercase; white-space: nowrap; }
                .band .date { font-size: calc(6pt * var(--s)); font-weight: 600; text-transform: uppercase; }
                /* Always the same height, dots or not, so the band looks the same either way. */
                .band .dots { display: flex; gap: calc(0.8mm * var(--s)); height: calc(1.1mm * var(--s)); margin-top: calc(0.8mm * var(--s)); }
                .band .dots i { width: calc(1.1mm * var(--s)); height: calc(1.1mm * var(--s)); border-radius: 50%; background: currentColor; }
                /* Extra on the right: the LabelWriter can't print right to the edge there. */
                .body {
                    min-width: 0; overflow: hidden; padding: calc(1.2mm * var(--s)) 6mm calc(1.6mm * var(--s)) calc(3.5mm * var(--s));
                    display: flex; flex-direction: column;
                }
                .body > * { flex-shrink: 0; }
                /* The names, with the check-out QR code beside them once the child is checked in. */
                .head { display: flex; align-items: flex-start; gap: calc(2mm * var(--s)); }
                .names { flex: 1; min-width: 0; }
                .qr { flex: none; width: max(11mm, calc(13mm * var(--s))); height: max(11mm, calc(13mm * var(--s))); margin-top: calc(0.6mm * var(--s)); }
                .first-name {
                    font-size: calc(34pt * var(--s)); font-weight: 800; letter-spacing: -0.02em; line-height: 1;
                    white-space: nowrap; overflow: hidden;
                }
                .rest-of-name { font-size: calc(10pt * var(--s)); line-height: 1.2; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin-top: calc(0.6mm * var(--s)); }
                .medical {
                    margin-bottom: calc(1.3mm * var(--s)); padding: calc(0.7mm * var(--s)) calc(1.5mm * var(--s)) calc(0.7mm * var(--s)) calc(0.8mm * var(--s));
                    border: 0.3mm solid #000; border-radius: calc(0.7mm * var(--s));
                    display: flex; align-items: center; gap: calc(1.2mm * var(--s));
                    font-size: calc(8.5pt * var(--s)); font-weight: 600; line-height: 1.15;
                }
                .medical svg { width: calc(3.6mm * var(--s)); height: calc(3.6mm * var(--s)); flex-shrink: 0; }
                .medical svg path { stroke: #fff; stroke-width: 2.6; }
                .medical span { display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 3; overflow: hidden; }
                /* The parent sits at the bottom under a rule with any medical box just above it; the
                   spacer takes up the slack under the name, keeping at least a gap. */
                .body > .spacer { flex: 1 0 calc(1.3mm * var(--s)); }
                .parent-block { padding-top: calc(0.9mm * var(--s)); border-top: 0.6mm solid #000; }
                .parent { display: flex; justify-content: space-between; align-items: baseline; gap: calc(2mm * var(--s)); font-size: calc(10.5pt * var(--s)); font-weight: 700; white-space: nowrap; line-height: 1.15; }
                /* A long line shrinks to fit first (see the script). Only past that is the parent's
                   name cut short: the cell number is what a leader needs, so it always shows whole. */
                .parent-name { min-width: 0; overflow: hidden; text-overflow: ellipsis; }
                .parent-cell { flex-shrink: 0; }
            </style>
            </head>
            <body>
            <div class="label{{(light ? " light" : "")}}">
                <div class="band">
                    {{topOfBand}}
                    <div class="age"><p class="caption">{{ageCaption}}</p><p class="age-value">{{age}}</p></div>
                    <p class="ministry" id="ministry">{{E(ministry)}}</p>
                    <p class="date">{{DateTime.Now:d MMM yyyy}}</p>
                    <p class="dots">{{string.Concat(Enumerable.Repeat("<i></i>", dots))}}</p>
                </div>
                <div class="body" id="body">
                    <div class="head">
                        <div class="names">
                            <p class="first-name" id="first-name">{{E(firstName)}}</p>
                            <p class="rest-of-name">{{E(restOfName)}}</p>
                        </div>
                        {{qr}}
                    </div>
                    <div class="spacer"></div>
                    {{medical}}
                    <div class="parent-block">
                        <p class="parent" id="parent">
                            <span class="parent-name" id="parent-name">{{E(child.ParentName)}} {{E(child.ParentSurname)}}</span>
                            <span class="parent-cell">{{E(child.ParentCellNo)}}</span>
                        </p>
                    </div>
                </div>
            </div>
            <script>
                // Shrink a line until the part that clips fits, down to a size still easy to read.
                // Sizes are for Large Address, scaled to the label in use.
                const S = {{N(size.Scale)}};
                function fit(line, fits, size, smallest) {
                    // Never below 5.5pt, even on the smallest label: past that it can't be read.
                    size *= S; smallest = Math.max(smallest * S, 5.5);
                    while (!fits() && size > smallest) {
                        size -= 0.5;
                        line.style.fontSize = size + 'pt';
                    }
                }
                // Measure only once the label's own font is in: a fallback font is a different width.
                window.sgLabelReady = Promise.all([
                    document.fonts.load("800 34pt Archivo"),
                    document.fonts.load("400 10pt Archivo"),
                    document.fonts.load("600 8.5pt Archivo"),
                ]).catch(() => {}).then(() => {
                    const body = document.getElementById('body');
                    const first = document.getElementById('first-name');
                    const medical = document.getElementById('medical');
                    // Medical notes are shown whole if at all possible: a little smaller to keep to two
                    // lines, then a third line if they need it, with the name shrinking to make room.
                    const notes = medical?.querySelector('span');
                    // The lines the notes take unclamped: the clamp hides any overflow from scrollHeight.
                    const lines = () => {
                        notes.style.webkitLineClamp = 'none';
                        const n = Math.round(notes.clientHeight / parseFloat(getComputedStyle(notes).lineHeight));
                        notes.style.webkitLineClamp = '';
                        return n;
                    };
                    if (notes) fit(medical, () => lines() <= 2, 8.5, 7);
                    fit(first, () => first.scrollWidth <= first.clientWidth && body.scrollHeight <= body.clientHeight, 34, 16);
                    if (notes) fit(medical, () => body.scrollHeight <= body.clientHeight && lines() <= 3, parseFloat(medical.style.fontSize || 8.5 * S) / S, 6.5);
                    const ministry = document.getElementById('ministry');
                    // At least 2mm clear of each edge of the band (96px to the inch, 25.4mm).
                    const clear = 2 * 96 / 25.4 * S;
                    fit(ministry, () => ministry.scrollWidth <= ministry.parentElement.clientWidth - 2 * clear, 7, 5);
                    const parentName = document.getElementById('parent-name');
                    fit(document.getElementById('parent'), () => parentName.scrollWidth <= parentName.clientWidth, 10.5, 7.5);
                });
            </script>
            </body>
            </html>
            """;
    }

    // The QR code as an SVG drawn module by module, so it prints sharp at any size. Medium
    // error correction, to survive a smudge or a crease. A narrow quiet zone (normally four
    // modules): the label around it is blank anyway, and the code stays bigger.
    private static string QrSvg(string text)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;   // includes a four-module quiet zone all round
        const int Trim = 2;
        var size = matrix.Count - 2 * Trim;
        var path = new StringBuilder();
        for (var y = 0; y < size; y++)
        {
            var row = matrix[y + Trim];
            for (var x = 0; x < size;)
            {
                if (!row[x + Trim])
                {
                    x++;
                    continue;
                }
                var start = x;
                while (x < size && row[x + Trim])
                {
                    x++;
                }
                path.Append($"M{start} {y}h{x - start}v1h-{x - start}z");
            }
        }
        return $"""<svg class="qr" viewBox="0 0 {size} {size}" shape-rendering="crispEdges" aria-hidden="true"><path d="{path}"/></svg>""";
    }
}
