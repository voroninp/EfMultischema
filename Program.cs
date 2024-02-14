using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppContext>(cfg =>
{
    cfg.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=EfMultischema");
    cfg.ReplaceService<IModelCacheKeyFactory, ModelPerTenantCacheKeyFactory>();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/{tenantId}", async (string tenantId, [FromServices] AppContext ctx) =>
{
    await ctx.Database.EnsureCreatedAsync();
    return tenantId;
})
.WithName("TenantTest")
.WithOpenApi();

app.Run();



internal sealed class AppContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public AppContext(ITenantProvider tenantProvider, DbContextOptions<AppContext> options) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_tenantProvider.TenantId);

        base.OnModelCreating(modelBuilder);
    }
}

public interface ITenantProvider
{
    string TenantId { get; }
}

internal sealed class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string TenantId => _httpContextAccessor.HttpContext?.Request.RouteValues["tenantId"] as string ?? "default";
}

internal sealed class ModelPerTenantCacheKeyFactory : IModelCacheKeyFactory
{
    private readonly ITenantProvider _tenantProvider;

    public ModelPerTenantCacheKeyFactory(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public object Create(DbContext context, bool designTime)
        =>
        (context.GetType(), _tenantProvider.TenantId, designTime);
}