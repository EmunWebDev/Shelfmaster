using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using test.Data;
using test.Services;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// Configure DbContext with your connection string
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register your services
builder.Services.AddScoped<ApplicationDbContext>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddHttpClient<EbayAuthService>();
builder.Services.AddHttpClient<EbayBookService>();
builder.Services.AddSingleton<EmailService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Data Protection - persist keys in "keys" folder inside content root
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("ShelfMaster");

// Your exact cookie authentication setup
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                var path = context.Request.Path;

                if (path.StartsWithSegments("/Admin"))
                {
                    context.Response.Redirect("/Admin/Auth/Login");
                }
                else
                {
                    context.Response.Redirect("/Auth/Login");
                }
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                var path = context.Request.Path;

                if (path.StartsWithSegments("/Admin"))
                {
                    context.Response.Redirect("/Admin/Auth/UnauthorizedAccess");
                }
                else
                {
                    context.Response.Redirect("/Auth/UnauthorizedAccess");
                }
                return Task.CompletedTask;
            }
        };

        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
        options.Cookie.IsEssential = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Apply EF Core migrations automatically on startup
//using (var scope = app.Services.CreateScope())
//{
//    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
//    dbContext.Database.Migrate();
//}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Auth}/{action=Login}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
