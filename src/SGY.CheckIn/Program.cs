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
builder.Services.AddScoped<LabelService>();
builder.Services.AddSingleton(sp => new BackupService(
    connectionString, sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<BackupService>>()));
builder.Services.AddHostedService<AutomaticBackupService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton<VolunteerPasswordStore>();

// Shared-password cookie auth: two credentials (volunteer/admin), each device gets its own
// independent cookie on sign-in (so multiple tablets can be logged in at once). The cookie
// also carries the group signed in to (see UserGroup).
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

        // Resetting a group's volunteer password signs out that group's volunteer devices: a
        // volunteer cookie is only good while its stamp matches the group's current one.
        // Checked against the in-memory store, so this costs nothing per request.
        options.Events.OnValidatePrincipal = async context =>
        {
            if (context.Principal?.IsInRole(Roles.Volunteer) != true)
            {
                return;
            }

            var store = context.HttpContext.RequestServices.GetRequiredService<VolunteerPasswordStore>();
            var stamp = context.Principal.FindFirstValue(VolunteerPasswordStore.StampClaim) ?? "";
            if (stamp != store.Stamp(context.Principal.GetGroup()))
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

// A backup left as "restore.db" next to the database is swapped in before anything opens
// it (README: Restoring a backup). Then migrations bring an older backup up to date.
await app.Services.GetRequiredService<BackupService>().ApplyPendingRestoreAsync();

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
    var group = GroupExtensions.FromKey(form["group"].ToString()) ?? Group.Youth;

    // One password box, two roles: whichever password matches decides the role. Admin is
    // checked first so that if the same value were ever set for both, it wins. Volunteers
    // get check-in + registration; admins additionally get profile management. The admin
    // password works for every group; a volunteer password only for its own.
    string? role = null;
    if (PasswordHashing.Verify(config["CheckInAuth:AdminPasswordHash"], password))
    {
        role = Roles.Admin;
    }
    else if (volunteerPasswords.Verify(group, password))
    {
        role = Roles.Volunteer;
    }

    if (role is null)
    {
        var retryUrl = $"/login?group={group.ToKey()}&error=1";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            retryUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }
        return Results.Redirect(retryUrl);
    }

    await SignInAsync(http, role, group, role == Roles.Volunteer ? volunteerPasswords.Stamp(group) : null);

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/";
    return Results.Redirect(target);
}).AllowAnonymous();

// Admins can move to another group from the header without typing the password again: the
// admin password would let them in anyway. Volunteers sign out and pick a group instead,
// since their password only opens their own group.
app.MapPost("/account/switch-group", async (HttpContext http) =>
{
    var form = await http.Request.ReadFormAsync();
    if (GroupExtensions.FromKey(form["group"].ToString()) is { } group)
    {
        await SignInAsync(http, Roles.Admin, group, volunteerStamp: null);
    }
    return Results.Redirect("/");
}).RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

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
    if (!PageGroupMatches(http))
    {
        return GroupChangedResult();
    }
    var filter = CheckInExportFilter.FromQuery(http.Request.Query, http.User.GetGroup());
    var fileName = $"sg-{filter.Group.ToKey()}-checkins-{filter.From:yyyy-MM-dd}-to-{filter.To:yyyy-MM-dd}.csv";
    return Results.File(await exports.CheckInHistoryCsvAsync(filter, ct), "text/csv", fileName);
}).RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

app.MapGet("/admin/export/people.csv", async (HttpContext http, ExportService exports, CancellationToken ct) =>
{
    if (!PageGroupMatches(http))
    {
        return GroupChangedResult();
    }
    var filter = YouthExportFilter.FromQuery(http.Request.Query, http.User.GetGroup());
    var fileName = $"sg-{filter.Group.ToKey()}-list-{DateTime.Today:yyyy-MM-dd}.csv";
    return Results.File(await exports.YouthListCsvAsync(filter, ct), "text/csv", fileName);
}).RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

// Saved backups, downloaded from the Settings page. BackupService.PathOf only accepts its
// own file names, so the name from the URL can't reach anything else on disk.
app.MapGet("/admin/backups/{name}", (string name, BackupService backups) =>
    backups.PathOf(name) is { } path
        ? Results.File(path, "application/octet-stream", name)
        : Results.NotFound()
).RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

// A child's check-in label, as a page sized to one DYMO label. The check-in screen loads it
// in a hidden frame and prints it. A plain endpoint rather than a Blazor page, so each label
// doesn't start an interactive circuit of its own. Anyone signed in to Kids can print one.
app.MapGet("/labels/{id:int}", async (int id, HttpContext http, LabelService labels, CancellationToken ct) =>
    !PageGroupMatches(http) ? GroupChangedResult()
    : await labels.RenderAsync(id, http.User.GetGroup(), ct) is { } html
        ? Results.Content(html, "text/html; charset=utf-8")
        : Results.NotFound()
).RequireAuthorization();

app.Run();

// The page that made a download or label link names the group it was showing. That can
// differ from the cookie's if an admin switched group in another tab since, and then the
// file would quietly hold the other group's people: refuse instead.
static bool PageGroupMatches(HttpContext http) =>
    http.Request.Query["group"] == http.User.GetGroup().ToKey();

static IResult GroupChangedResult() =>
    Results.Text("You switched group in another tab. Reload this page and try again.", statusCode: StatusCodes.Status409Conflict);

// Issues the sign-in cookie. A volunteer's carries their group's password stamp, so
// resetting that password signs them out.
static async Task SignInAsync(HttpContext http, string role, Group group, string? volunteerStamp)
{
    List<Claim> claims =
    [
        new Claim(ClaimTypes.Name, role),
        new Claim(ClaimTypes.Role, role),
        new Claim(UserGroup.Claim, group.ToKey()),
    ];
    if (volunteerStamp is not null)
    {
        claims.Add(new Claim(VolunteerPasswordStore.StampClaim, volunteerStamp));
    }

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
}
