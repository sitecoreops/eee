namespace ExperienceEdgeEmu.Web.EmuSchema;

[Serializable]
public class EmuSchemaTypeNameMissingException : Exception
{
    private EmuSchemaTypeNameMissingException(string message) : base(message)
    {

    }

    public static void ThrowIfTypeNameMissing(SitecoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrEmpty(item.TypeName))
        {
            throw new EmuSchemaTypeNameMissingException("Item TypeName is null or empty.");
        }
    }

    public static void ThrowIfTypeNameMissing(SitecoreField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (string.IsNullOrEmpty(field.TypeName))
        {
            throw new EmuSchemaTypeNameMissingException("Field TypeName is null or empty.");
        }
    }
}

