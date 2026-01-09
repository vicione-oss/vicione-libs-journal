using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace ViciOne.Journal;

public static class JournalLoggerExtensions
{
    public static ILoggingBuilder AddJournal(this ILoggingBuilder builder)
    {
        builder.AddConfiguration();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, JournalLoggerProvider>());

        return builder;
    }

    public static ILoggingBuilder AddJournal(this ILoggingBuilder builder, Action<JournalLoggerOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.AddJournal();

        return builder;
    }
}
