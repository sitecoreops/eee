using ExperienceEdgeEmu.Web.DataStore;
using ExperienceEdgeEmu.Web.DataStore.Crawler;
using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using GraphQL.Utilities;
using System.Text.RegularExpressions;

namespace ExperienceEdgeEmu.Web.EmuSchema;

public partial class EmuSchemaBuilder(SdlBuilder sdlBuilder)
{
    [GeneratedRegex("([A-Z])")]
    private static partial Regex CapitalRegex();

    public ISchema Build()
    {
        // build SDL
        var sdl = sdlBuilder.Build();

        // configure schema
        var schema = Schema.For(sdl, config =>
        {
            config.Types.For("Mutation").FieldFor("crawl").Resolver = new FuncFieldResolver<CrawlResult>(async context =>
            {
                var crawler = context.RequestServices!.GetRequiredService<ExperienceEdgeCrawlerService>();

                return await crawler.Crawl(context);
            });

            config.Types.For("Query").FieldFor("layout").Resolver = new FuncFieldResolver<SitecoreLayout>(context =>
            {
                var repository = context.RequestServices!.GetRequiredService<FileDataStore>();
                var routePath = context.GetArgument<string>("routePath");
                var language = context.GetArgument<string>("language");
                var site = context.GetArgument<string>("site");
                var layout = repository.GetLayoutByRoute(site, language, routePath);

                if (layout is null)
                {
                    context.Errors.Add(new ExecutionError("Layout not found"));
                }

                return layout;
            });

            config.Types.For("Query").FieldFor("item").Resolver = new FuncFieldResolver<SitecoreItem>(context =>
            {
                var repository = context.RequestServices!.GetRequiredService<FileDataStore>();
                var path = context.GetArgument<string>("path");
                var language = context.GetArgument<string>("language");
                var item = repository.GetItemByPath(path, language);

                if (item is null)
                {
                    context.Errors.Add(new ExecutionError("Item not found"));
                }

                return item;
            });

            config.Types.For("Query").FieldFor("site").Resolver = new FuncFieldResolver<SitecoreSiteData>(context =>
            {
                var repository = context.RequestServices!.GetRequiredService<FileDataStore>();
                var sites = repository.GetSites();

                if (sites is null)
                {
                    context.Errors.Add(new ExecutionError("Sites not found"));
                }

                return sites;
            });

            config.Types.For("Query").FieldFor("search").Resolver = new FuncFieldResolver<object>(context =>
            {
                context.Errors.Add(new ExecutionError("Not supported."));

                return null;
            });

            config.Types.For("Item").ResolveType = obj =>
            {
                if (obj is SitecoreItem item)
                {
                    EmuSchemaTypeNameMissingException.ThrowIfTypeNameMissing(item);

                    if (sdlBuilder.CustomSchemaExists)
                    {
                        return new GraphQLTypeReference(item.TypeName);
                    }

                    return new GraphQLTypeReference("UnknownItem");
                }
                else if (obj is SitecoreLanguageItem languageItem)
                {
                    return new GraphQLTypeReference("UnknownItem");
                }

                return null!;
            };

            config.Types.For("ItemField").ResolveType = obj =>
            {
                if (obj is SitecoreField field)
                {
                    EmuSchemaTypeNameMissingException.ThrowIfTypeNameMissing(field);

                    return new GraphQLTypeReference(field.TypeName);
                }

                return null!;
            };
        });

        schema.FieldMiddleware.Use(next =>
        {
            return async context =>
            {
                if (context.Source is SitecoreItem item && context.FieldDefinition.ResolvedType is ObjectGraphType)
                {
                    // support for "... on <TYPE> { <FIELD_NAME> { }}"
                    var fieldName = context.FieldDefinition.Name;
                    var field = item.Fields?.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

                    if (field == null)
                    {
                        var spacedName = CapitalRegex().Replace(fieldName, " $1").Trim();

                        field = item.Fields?.FirstOrDefault(f => f.Name.Equals(spacedName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (field != null)
                    {
                        return field;
                    }
                }

                return await next(context);
            };
        });

        schema.RegisterType(new ComplexScalarGraphType { Name = "JSON" });
        schema.RegisterType(new ComplexScalarGraphType { Name = "Any" });

        return schema;
    }
}
