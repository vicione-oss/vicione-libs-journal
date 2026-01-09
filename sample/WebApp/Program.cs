using ViciOne.Journal;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSingleton<JournalService>()
    .AddOptions<JournalOptions>();

var app = builder.Build();

app.MapGet("api/v2/journal/field-descriptions", JournalWebMethods.GetFieldDescriptions);
app.MapGet("api/v2/journal/fields", JournalWebMethods.GetFields);
app.MapGet("api/v2/journal/field-values/{field}", JournalWebMethods.GetFieldValues);
app.MapGet("api/v2/journal/logs", JournalWebMethods.GetLogs);
app.MapGet("api/v2/journal/priority-counts", JournalWebMethods.GetPriorityCounts);
app.Run();
