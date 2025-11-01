using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExperienceEdgeEmu.Web.DataStore.Crawler;

public class IntrospectionJsonToSdlConverter
{
    public string ConvertJsonToSdl(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return string.Empty;
        }

        var schema = JsonSerializer.Deserialize<IntrospectionSchema>(json, JsonSerializerOptions.Web);
        var sdlBuilder = new StringBuilder();

        if (schema?.Data?.Schema?.Types == null)
        {
            return string.Empty;
        }

        foreach (var type in schema.Data.Schema.Types)
        {
            switch (type.Kind)
            {
                case "OBJECT":
                    AppendObjectType(sdlBuilder, type);
                    break;
                case "INTERFACE":
                    AppendInterfaceType(sdlBuilder, type);
                    break;
                case "ENUM":
                    AppendEnumType(sdlBuilder, type);
                    break;
                case "SCALAR":
                    AppendScalarType(sdlBuilder, type);
                    break;
            }
        }

        return sdlBuilder.ToString();
    }

    private void AppendObjectType(StringBuilder sdlBuilder, IntrospectionType type)
    {
        if (type?.Name == null)
        {
            return;
        }

        var implements = string.Empty;

        if (type.Interfaces != null && type.Interfaces.Any())
        {
            var names = type.Interfaces.Select(i => i?.Name).Where(n => !string.IsNullOrEmpty(n));
            var list = string.Join(" & ", names);

            if (!string.IsNullOrEmpty(list))
            {
                implements = " implements " + list;
            }
        }

        sdlBuilder.AppendLine($"type {type.Name}{implements} {{");

        AppendFields(sdlBuilder, type.Fields ?? []);

        sdlBuilder.AppendLine("}");
        sdlBuilder.AppendLine();
    }

    private void AppendInterfaceType(StringBuilder sdlBuilder, IntrospectionType type)
    {
        if (type?.Name == null)
        {
            return;
        }

        sdlBuilder.AppendLine($"interface {type.Name} {{");

        AppendFields(sdlBuilder, type.Fields ?? []);

        sdlBuilder.AppendLine("}");
        sdlBuilder.AppendLine();
    }

    private void AppendEnumType(StringBuilder sdlBuilder, IntrospectionType type)
    {
        if (type?.Name == null)
        {
            return;
        }

        sdlBuilder.AppendLine($"enum {type.Name} {{");

        foreach (var value in type.EnumValues ?? [])
        {
            if (value?.Name == null)
            {
                continue;
            }

            sdlBuilder.Append($"  {value.Name}");

            if (value.IsDeprecated == true)
            {
                var reason = EscapeString(value.DeprecationReason);

                sdlBuilder.Append($" @deprecated(reason: \"{reason}\")");
            }

            sdlBuilder.AppendLine();
        }

        sdlBuilder.AppendLine("}");
        sdlBuilder.AppendLine();
    }

    private void AppendScalarType(StringBuilder sdlBuilder, IntrospectionType type)
    {
        if (type?.Name == null)
        {
            return;
        }

        if (!IsBuiltInScalar(type.Name))
        {
            sdlBuilder.AppendLine($"scalar {type.Name}");
            sdlBuilder.AppendLine();
        }
    }

    private void AppendFields(StringBuilder sdlBuilder, IEnumerable<IntrospectionField> fields)
    {
        foreach (var field in fields ?? Array.Empty<IntrospectionField>())
        {
            if (field?.Name == null)
            {
                continue;
            }

            var fieldLine = new StringBuilder();

            fieldLine.Append($"  {field.Name}");

            if (field.Args != null && field.Args.Any())
            {
                fieldLine.Append("(");

                for (var i = 0; i < field.Args.Length; i++)
                {
                    var arg = field.Args[i];

                    if (arg?.Name == null)
                    {
                        continue;
                    }

                    var defaultPart = string.IsNullOrEmpty(arg.DefaultValue) ? string.Empty : $" = {arg.DefaultValue}";

                    fieldLine.Append($"{arg.Name}: {FormatType(arg.Type)}{defaultPart}");

                    if (i < field.Args.Length - 1)
                    {
                        fieldLine.Append(", ");
                    }
                }

                fieldLine.Append(")");
            }

            fieldLine.Append($": {FormatType(field.Type)}");

            if (field.IsDeprecated == true)
            {
                var reason = EscapeString(field.DeprecationReason);

                fieldLine.Append($" @deprecated(reason: \"{reason}\")");
            }

            sdlBuilder.AppendLine(fieldLine.ToString());
        }
    }

    private static string EscapeString(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private string FormatType(IntrospectionTypeReference? type)
    {
        if (type == null)
        {
            return "";
        }

        return type.Kind switch
        {
            "NON_NULL" => $"{FormatType(type.OfType)}!",
            "LIST" => $"[{FormatType(type.OfType)}]",
            _ => type.Name ?? string.Empty,
        };
    }

    private bool IsBuiltInScalar(string scalarName) => scalarName is "String" or "Int" or "Float" or "Boolean" or "ID";

    private class IntrospectionSchema
    {
        public IntrospectionData? Data { get; set; }
    }

    private record IntrospectionData([property: JsonPropertyName("__schema")] IntrospectionSchemaDefinition? Schema);

    private record IntrospectionSchemaDefinition(IntrospectionType[]? Types);

    private record IntrospectionType(IntrospectionTypeReference[]? Interfaces, string? Kind, string? Name, IntrospectionField[]? Fields, IntrospectionEnumValue[]? EnumValues);

    private record IntrospectionField(string? Name, IntrospectionTypeReference? Type, IntrospectionInputValue[]? Args, bool? IsDeprecated, string? DeprecationReason);

    private record IntrospectionInputValue(string? Name, IntrospectionTypeReference? Type, string? DefaultValue);

    private record IntrospectionTypeReference(string? Kind, string? Name, IntrospectionTypeReference? OfType);

    private record IntrospectionEnumValue(string? Name, bool? IsDeprecated, string? DeprecationReason);
}

