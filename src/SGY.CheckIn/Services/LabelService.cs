using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>
/// Builds the label stuck on a child at check-in: a page exactly one DYMO 30321 Large
/// Address label in size (89 × 36 mm), printed from the check-in computer's browser to the
/// LabelWriter. Shows the child's name large, their grade, the parent's name and cell, and
/// any medical notes in a black bar that's hard to miss. Never the leaders' comment.
/// </summary>
/// <remarks>
/// The label prints in black only (it's a thermal printer), so contrast comes from weight
/// and the inverted medical bar rather than colour, and the logo is a black copy
/// (sg-logo-print.png) of the white one the app uses. A long name is shrunk to fit one line
/// by the script at the bottom before printing.
/// </remarks>
public class LabelService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>Null if there's no such child in this group, or the group doesn't print labels.</summary>
    public async Task<string?> RenderAsync(int id, Group group, CancellationToken ct = default)
    {
        if (!group.PrintsLabels())
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var child = await db.Youths.AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == id && y.Group == group, ct);
        return child is null ? null : Render(child);
    }

    private static string Render(Youth child)
    {
        static string E(string? value) => HtmlEncoder.Default.Encode(value ?? "");

        var medical = string.IsNullOrWhiteSpace(child.Medical)
            ? """<p class="medical none">No medical notes</p>"""
            : $"""<p class="medical">MEDICAL: {E(child.Medical)}</p>""";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title>Label — {{E(child.Name)}} {{E(child.Surname)}}</title>
            <style>
                @page { size: 89mm 36mm; margin: 0; }
                * { box-sizing: border-box; }
                html, body { margin: 0; padding: 0; background: #fff; color: #000; }
                body { font-family: Arial, Helvetica, sans-serif; }
                .label {
                    width: 89mm; height: 36mm; overflow: hidden;
                    /* Extra on the right: the LabelWriter can't print right to the edge there. */
                    padding: 2.5mm 6mm 2mm 4mm;
                    display: flex; flex-direction: column; gap: 1.2mm;
                }
                .top { display: flex; align-items: baseline; gap: 2mm; }
                .name {
                    flex: 1; min-width: 0; margin: 0;
                    font-size: 20pt; font-weight: 800; line-height: 1.05;
                    white-space: nowrap; overflow: hidden;
                }
                .grade { margin: 0; font-size: 11pt; font-weight: 700; white-space: nowrap; }
                /* The cell number is what a leader needs, so a long parent name is cut short instead. */
                .parent { margin: 0; display: flex; gap: 1.5mm; font-size: 10pt; line-height: 1.2; white-space: nowrap; }
                .parent-name { min-width: 0; overflow: hidden; text-overflow: ellipsis; }
                .parent-name strong, .parent-cell { font-weight: 800; }
                .parent-cell { flex-shrink: 0; }
                .parent-cell::before { content: "· "; font-weight: 400; }
                .medical {
                    margin: 0; padding: 0.6mm 1.5mm;
                    background: #000; color: #fff;
                    font-size: 10pt; font-weight: 800; line-height: 1.2;
                    display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: 2; overflow: hidden;
                    -webkit-print-color-adjust: exact; print-color-adjust: exact;
                }
                .medical.none { background: none; color: #000; font-weight: 400; padding-left: 0; }
                .foot { margin-top: auto; display: flex; justify-content: space-between; align-items: center; font-size: 7pt; }
                .brand { display: flex; align-items: center; gap: 1.2mm; font-weight: 700; }
                .brand img { height: 4mm; }
            </style>
            </head>
            <body>
            <div class="label">
                <div class="top">
                    <p class="name" id="name">{{E(child.Name)}} {{E(child.Surname)}}</p>
                    <p class="grade">{{E(child.Grade.ToDisplayString())}}</p>
                </div>
                <p class="parent">
                    <span class="parent-name">Parent: <strong>{{E(child.ParentName)}} {{E(child.ParentSurname)}}</strong></span>
                    <span class="parent-cell">{{E(child.ParentCellNo)}}</span>
                </p>
                {{medical}}
                <div class="foot">
                    <span class="brand"><img src="/sg-logo-print.png" alt="">SG Kids</span>
                    <span>{{DateTime.Now:d MMM yyyy}}</span>
                </div>
            </div>
            <script>
                // Shrink a long name until it fits on its one line.
                const name = document.getElementById('name');
                let size = 20;
                while (name.scrollWidth > name.clientWidth && size > 9) {
                    name.style.fontSize = (--size) + 'pt';
                }
            </script>
            </body>
            </html>
            """;
    }
}
