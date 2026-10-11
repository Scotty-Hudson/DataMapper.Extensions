using System.Text;
using DataMapper.Extensions.Web.Components;
using DataMapper.Extensions.Web.Components.Partials;
using DataMapper.Extensions.Web.Data;
using DataMapper.Extensions.Web.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

const int ExportLimit = 50;
const int ChunkSize = 25;

var builder = WebApplication.CreateBuilder(args);

// Static server rendering only. No render mode is registered, so there is no circuit, no
// WebAssembly, and nothing to prerender: every request is one render of one component.
builder.Services.AddRazorComponents();

var database = new SqliteDatabase(builder.Configuration["Database:Path"] ?? "dispatch.db");

builder.Services.AddSingleton<IDatabase>(database);
builder.Services.AddScoped<JobRepository>();

var app = builder.Build();

Seeder.Seed(database);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

// htmx is the only client script in the app, and these are the routes it calls: they return a
// fragment rather than a page. Every htmx element also has a plain action or href, so the pages
// still work when the script is not running.
app.MapGet("/jobs/results", async (HttpContext context, JobRepository jobs) =>
{
    var filter = JobFilter.FromQuery(context.Request.Query);
    var result = await jobs.QueryAsync(filter, context.RequestAborted);

    return new RazorComponentResult<JobResults>(new { Filter = filter, Result = result });
});

app.MapGet("/stream/chunk", async (HttpContext context, JobRepository jobs) =>
{
    var query = context.Request.Query;

    var chunk = Math.Clamp(int.TryParse(query["chunk"], out var size) ? size : ChunkSize, 1, 200);
    var cancelAfter = int.TryParse(query["cancel_after"], out var after) ? after : 0;
    var cursor = int.TryParse(query["after"], out var value) ? value : 0;

    var filter = JobFilter.FromQuery(query);
    var result = await jobs.GetChunkAsync(filter, cursor, chunk, cancelAfter > 0 ? cancelAfter : null, context.RequestAborted);

    return new RazorComponentResult<StreamChunk>(new { Chunk = result, FilterQuery = filter.ToFilterQueryString() });
});

// Static rendering has no component events, so an action has to be a plain HTML form posting to
// an endpoint. Antiforgery is validated here because these forms are plain HTML.
app.MapPost("/export/csv", async (HttpContext context, JobRepository jobs, CancellationToken cancellationToken) =>
{
    if (!await IsValid(context)) return Results.BadRequest("Invalid antiforgery token.");

    var filter = JobFilter.FromForm(context.Request.Form);
    var table = await jobs.ExportTableAsync(filter, ExportLimit, cancellationToken);
    var bytes = Encoding.UTF8.GetBytes(CsvBuilder.Build(table));

    return Results.File(bytes, "text/csv", "dispatch-jobs.csv");
});

app.MapPost("/export/archive", async (HttpContext context, JobRepository jobs, CancellationToken cancellationToken) =>
{
    if (!await IsValid(context)) return Results.BadRequest("Invalid antiforgery token.");

    var filter = JobFilter.FromForm(context.Request.Form);
    var rows = await jobs.ExportRowsAsync(filter, ExportLimit, cancellationToken);
    var inserted = await jobs.ArchiveAsync(rows, cancellationToken);

    // Read the archive back rather than echoing what was written: the rows come out of SQLite as
    // text, so the same conversions run again on the way out.
    var archived = await jobs.GetArchivedAsync(ExportLimit, cancellationToken);

    // A file download cannot be swapped, so the CSV form is a plain form. This one can, so htmx
    // replaces just the archive region and the page keeps its filter.
    if (context.Request.Headers.ContainsKey("HX-Request"))
        return new RazorComponentResult<ArchiveResult>(new { Inserted = inserted, Rows = archived });

    return Results.Redirect($"/export?archived={inserted}");
});

// No interactive render mode, so this is the whole routing pipeline.
app.MapRazorComponents<App>();

app.Run();

async Task<bool> IsValid(HttpContext context)
{
    try
    {
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        return false;
    }
}
