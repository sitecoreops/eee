using GraphQLParser.AST;
using GraphQLParser.Visitors;

namespace ExperienceEdgeEmu.Web.EmuSchema;

public class SchemaMerger(ILoggerFactory loggerFactory)
{
    private readonly ILogger<SchemaMerger> _logger = loggerFactory.CreateLogger<SchemaMerger>();

    public string Merge(params string[] schemaContents)
    {
        var definitions = new Dictionary<ASTNodeKind, Dictionary<string, ASTNode>>();
        var printer = new SDLPrinter();

        foreach (var content in schemaContents)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var document = GraphQLParser.Parser.Parse(content);

            foreach (var definition in document.Definitions)
            {
                if (definition is not INamedNode hasName)
                {
                    continue;
                }

                Dictionary<string, ASTNode> definitionsPerKind;

                if (definitions.TryGetValue(definition.Kind, out var def))
                {
                    definitionsPerKind = def;
                }
                else
                {
                    definitionsPerKind = [];
                }

                var name = hasName.Name.Value;

                if (definitionsPerKind.TryGetValue(name.ToString(), out var existingDef))
                {
                    var existingDefString = printer.Print(existingDef);
                    var currentDefString = printer.Print(definition);

                    if (existingDefString != currentDefString)
                    {
                        _logger.LogError("Schema merge conflict for '{Kind} {Name}', definitions are not identical.", definition.Kind, name);
                    }
                }
                else
                {
                    definitionsPerKind.Add(name.ToString(), definition);

                }

                definitions[definition.Kind] = definitionsPerKind;
            }
        }

        var finalDefinitions = definitions.SelectMany(x => x.Value.Values)
                                        .GroupBy(x => ((INamedNode)x).Name.StringValue)
                                        .Select(x => x.First())
                                        .ToList();

        var finalDocument = new GraphQLDocument(finalDefinitions);

        return printer.Print(finalDocument);
    }
}
