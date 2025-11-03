namespace ExperienceEdgeEmu.Web.EmuSchema;

public class SdlBuilder(EmuFileSystem emuFileSystem, SchemaMerger schemaMerger, ILogger<SdlBuilder> logger)
{
    public bool CustomSchemaExists { get; private set; }

    public string Build()
    {
        var defaultExperienceEdgeSchema = ReadEmbeddedResource("SchemaFiles.default-ee-schema.graphqls");
        var emuSchema = ReadEmbeddedResource("SchemaFiles.emu-schema.graphqls");
        var activeSchema = defaultExperienceEdgeSchema;

        // load imported schema if exists
        var importedSchemaFilePath = emuFileSystem.GetImportedSchemaFilePath();

        if (File.Exists(importedSchemaFilePath))
        {
            activeSchema = File.ReadAllText(importedSchemaFilePath);

            CustomSchemaExists = true;

            logger.LogInformation("Active Experience Edge schema: {ActiveSchemaFilePath}", importedSchemaFilePath);
        }
        else
        {
            logger.LogInformation("Active Experience Edge schema: default");
        }

        return schemaMerger.Merge(activeSchema, emuSchema);
    }

    private string ReadEmbeddedResource(string name)
    {
        var type = GetType();
        var resourceName = $"{type.Namespace}.{name}";

        using var stream = type.Assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
