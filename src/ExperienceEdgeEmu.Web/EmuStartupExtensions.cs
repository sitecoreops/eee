using ExperienceEdgeEmu.Web.DataStore;
using ExperienceEdgeEmu.Web.DataStore.Crawler;
using ExperienceEdgeEmu.Web.EmuSchema;
using ExperienceEdgeEmu.Web.Media;
using GraphQL;
using GraphQL.Server.Ui.GraphiQL;
using GraphQL.Types;
using Microsoft.AspNetCore.StaticFiles;

namespace ExperienceEdgeEmu.Web;

public static partial class EmuStartupExtensions
{
    public static IServiceCollection AddEmu(this IServiceCollection services, IConfigurationRoot configuration)
    {
        var emuSection = configuration.GetSection(EmuSettings.Key);

        ArgumentNullException.ThrowIfNull(emuSection);

        services.Configure<EmuSettings>(emuSection);
        services.AddHttpClient();
        services.AddSingleton<EmuFileSystem>();
        services.AddSingleton<InMemoryItemStore>();
        services.AddSingleton<InMemorySiteDataStore>();
        services.AddSingleton<FileDataStore>();
        services.AddSingleton<ExperienceEdgeCrawlerService>();
        services.AddSingleton<ItemPostProcessingQueue>();
        services.AddSingleton<MediaUrlReplacer>();
        services.AddHostedService<ItemPostProcessingWorker>();
        services.AddSingleton<MediaDownloadQueue>();
        services.AddMemoryCache();
        services.AddHostedService<MediaDownloadWorker>();
        services.AddSingleton<FileExtensionContentTypeProvider>();
        services.AddEmuSchema();

        services.AddSingleton<JsonFileChangeQueue>();
        services.AddHostedService<JsonFileWatcherWorker>();
        services.AddHostedService<JsonFileChangeWorker>();

        services.AddGraphQL(b => b
          .AddSystemTextJson()
          .UseMemoryCache(options =>
           {
               options.SizeLimit = 1000000;
               options.SlidingExpiration = null;
           })
          .ConfigureExecutionOptions(options =>
          {
              var logger = options.RequestServices!.GetRequiredService<ILogger<Program>>();

              options.UnhandledExceptionDelegate = ctx =>
              {
                  logger.LogError(ctx.OriginalException, "Unhandled exception.");

                  return Task.CompletedTask;
              };
          })
        );

        return services;
    }

    public static WebApplication UseEmu(this WebApplication app)
    {
        var graphQLEndpoint = "/graphql";

        app.UseWebSockets();
        app.UseGraphQL<ISchema>(graphQLEndpoint, options =>
        {
            options.CsrfProtectionEnabled = false;
        });
        app.UseGraphQLGraphiQL("/", new GraphiQLOptions
        {
            GraphQLEndPoint = graphQLEndpoint,
            SubscriptionsEndPoint = graphQLEndpoint,
        });

        app.UseMiddleware<MediaFileMiddleware>();

        return app;
    }

    public static async Task TriggerDataStoreRebuild(this WebApplication app, CancellationToken cancellationToken) =>
        await app.Services.GetRequiredService<FileDataStore>().RebuildAsync(cancellationToken);
}
