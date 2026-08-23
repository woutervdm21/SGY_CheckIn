using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SGY.CheckIn.Auth;
using SGY.CheckIn.Components;
using SGY.CheckIn.Data;
using SGY.CheckIn.Services;

// Special CLI mode: generate a hash for the shared staff password, without starting the
// web server. Run with: dotnet SGY.CheckIn.dll --hash-password <password>
// Paste the printed hash into appsettings.json (CheckInAuth:PasswordHash) or set it via
// the CheckInAuth__PasswordHash environment variable. See README for details.
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
app.MapPost("/account/login", async (HttpContext http, IConfiguration config) =>
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
    else if (PasswordHashing.Verify(config["CheckInAuth:VolunteerPasswordHash"], password))
    {
        role = Roles.Volunteer;
    }

    if (role is null)
    {
        var retryUrl = "/login?error=1";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            retryUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }
        return Results.Redirect(retryUrl);
    }

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role)],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/";
    return Results.Redirect(target);
}).AllowAnonymous();

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

app.Run();
