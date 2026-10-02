using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Auth;
using SGY.CheckIn.Components;
using SGY.CheckIn.Data;
using SGY.CheckIn.Models;
using SGY.CheckIn.Services;

// Special CLI mode: generate a hash for the admin password, without starting the web
// server. Run with: dotnet SGY.CheckIn.dll --hash-password <password>
// Paste the printed hash into appsettings.json (CheckInAuth:AdminPasswordHash) or set it
// via the CheckInAuth__AdminPasswordHash environment variable. See README for details.
// (The volunteer password is set by an admin on the Settings page instead.)
if (args is ["--hash-password", var passwordToHash])
{
    Console.WriteLine(PasswordHashing.Hash(passwordToHash));
    return;
}
if (args is ["--hash-password"])
{
    Console.WriteLine("Usage: dotnet SGY.CheckIn.dll --hash-password <password>");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=sgy_checkin.db";
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<YouthService>();
builder.Services.AddScoped<CheckInService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton<VolunteerPasswordStore>();

// Shared-password cookie auth: two credentials (volunteer/admin), each device gets its own
// independent cookie on sign-in (so multiple tablets can be logged in at once).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        // A logged-in volunteer who reaches an admin-only page lands here (rather than the
        // default /Account/AccessDenied, which doesn't exist and shows a confusing "Not
        // Found"). From the login screen they can enter the admin password if they have it.
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.Name = "sgy_checkin_auth";

        // Resetting the volunteer password signs out every volunteer device: a volunteer
        // cookie is only good while its stamp matches the current one. Checked against the
        // in-memory store, so this costs nothing per request.
        options.Events.OnValidatePrincipal = async context =>
        {
            if (context.Principal?.IsInRole(Roles.Volunteer) != true)
            {
                return;
            }

            var store = context.HttpContext.RequestServices.GetRequiredService<VolunteerPasswordStore>();
            var stamp = context.Principal.FindFirstValue(VolunteerPasswordStore.StampClaim) ?? "";
            if (stamp != store.Stamp)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
// Deliberately no global RequireAuthenticatedUser() FallbackPolicy here: applying one at
// the ASP.NET Core endpoint level also wraps Blazor Server's own internal framework
// endpoints (the ones that serve the interactive circuit's JS runtime and negotiate the
// SignalR connection), which breaks interactivity app-wide once those requests get
// redirected to the login page too. Instead, each protected page declares
// @attribute [Authorize] itself, enforced by AuthorizeRouteView in Routes.razor.
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// The auth cookie is signed/encrypted using ASP.NET Core's Data Protection keys. By
// default those keys live only in the container's ephemeral filesystem, so every
// restart/rebuild would invalidate every device's login — defeating the point of the
// 14-day sliding expiration. Persist them to the same mounted volume as the SQLite file
// (see DataProtection__KeyPath in the Dockerfile) so logins survive container updates.
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrEmpty(keyPath))
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}

var app = builder.Build();

// Apply any pending EF Core migrations on startup so the SQLite file/schema
// self-initializes on first run — no manual migration step for church volunteers.
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = dbFactory.CreateDbContext();
    db.Database.Migrate();
}

// The volunteer password used to live in config; the first run after upgrading copies it
// into the database, after which the config value is ignored.
await app.Services.GetRequiredService<VolunteerPasswordStore>()
    .LoadAsync(app.Configuration["CheckInAuth:VolunteerPasswordHash"]);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// No HTTPS redirection: this runs as a plain-HTTP app on the church's internal LAN
// (Docker container on port 8080, no TLS certificate to manage for a small deployment).

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Login/logout as minimal API endpoints (not interactive Blazor handlers): signing in
// needs to write a Set-Cookie response header before the interactive circuit takes over,
// so Login.razor posts here via a plain HTML form instead of an EditForm. Routed under
// /account/... rather than /login itself, since the Login.razor page endpoint already
// occupies "/login" for GET and (being method-agnostic) would otherwise collide with a
// POST mapped to the same route (AmbiguousMatchException).
app.MapPost("/account/login", async (HttpContext http, IConfiguration config, VolunteerPasswordStore volunteerPasswords) =>
{
    var form = await http.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    // One password box, two roles: whichever password matches decides the role. Admin is
    // checked first so that if the same value were ever set for both, it wins. Volunteers
    // get check-in + registration; admins additionally get profile management.
    string? role = null;
    if (PasswordHashing.Verify(config["CheckInAuth:AdminPasswordHash"], password))
    {
        role = Roles.Admin;
    }
    else if (volunteerPasswords.Verify(password))
    {
        role = Roles.Volunteer;
    }

    if (role is null)
    {
        var retryUrl = "/login?group=youth&error=1";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            retryUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }
        return Results.Redirect(retryUrl);
    }

    List<Claim> claims = [new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role)];
    if (role == Roles.Volunteer)
    {
        claims.Add(new Claim(VolunteerPasswordStore.StampClaim, volunteerPasswords.Stamp));
    }

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/";
    return Results.Redirect(target);
}).AllowAnonymous();

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

// CSV downloads for the Export page. Minimal API endpoints rather than Blazor handlers:
// the browser needs a plain GET it can download, and the interactive circuit can't set
// response headers. The filters arrive in the query string, built by the page from the
// same filter records, so the file matches the row count the page showed. Admin-only,
// matching the page they're linked from.
app.MapGet("/admin/export/checkins.csv", async (HttpContext http, ExportService exports, CancellationToken ct) =>
{
    var filter = CheckInExportFilter.FromQuery(http.Request.Query);
    var fileName = $"sgy-checkins-{filter.From:yyyy-MM-dd}-to-{filter.To:yyyy-MM-dd}.csv";
    return Results.File(await exports.CheckInHistoryCsvAsync(filter, ct), "text/csv", fileName);
}).RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

app.MapGet("/admin/export/youth.csv", async (HttpContext http, ExportService exports, CancellationToken ct) =>
{
    var filter = YouthExportFilter.FromQuery(http.Request.Query);
    var fileName = $"sgy-youth-{DateTime.Today:yyyy-MM-dd}.csv";
    return Results.File(await exports.YouthListCsvAsync(filter, ct), "text/csv", fileName);
}).RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

app.Run();
