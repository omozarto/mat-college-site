using MathCollegeSiteBack;
using MathCollegeSiteBack.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

var localDataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MathCollegeSite");

Directory.CreateDirectory(localDataDirectory);

var databasePath = Path.Combine(localDataDirectory, "MathCollegeSite.db");
var connectionString =
    builder.Configuration.GetConnectionString("MathCollegeDB")
    ?? $"Data Source={databasePath}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();