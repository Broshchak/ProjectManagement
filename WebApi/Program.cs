using Application;
using Infrastructure;
using Scalar.AspNetCore;
using WebApi;
using WebApi.Bot;
using WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_000_000);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    var bearer = new BearerSecurityTransformer();
    options.AddDocumentTransformer(bearer);
    options.AddOperationTransformer(bearer);
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWebApiSecurity(builder.Configuration);
builder.Services.AddBotIntegration(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(o => o.AddPreferredSecuritySchemes("Bearer")).AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
