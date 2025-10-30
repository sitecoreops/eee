namespace ExperienceEdgeEmu.Web.EmuSchema;

public class SdlBuilder(EmuFileSystem emuFileSystem, SchemaMerger schemaMerger)
{
    public bool CustomSchemaExists { get; private set; }

    public string Build()
    {
        var cleanExperienceEdgeSchema = ReadEmbeddedResource("SchemaFiles.clean-schema.graphqls");
        var emuSchema = ReadEmbeddedResource("SchemaFiles.emu-schema.graphqls");
        var userSchemas = string.Empty;

        // load all custom schema files
        foreach (var file in emuFileSystem.GetSchemaFilePaths())
        {
            userSchemas += File.ReadAllText(file);

            CustomSchemaExists = true;
        }

        return schemaMerger.Merge(cleanExperienceEdgeSchema, userSchemas, emuSchema);
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
