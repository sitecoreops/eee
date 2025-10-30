using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace ExperienceEdgeEmu.Web.EmuSchema;

public record SitecoreLayout(SitecoreItem? Item);

public record SitecoreUrl(string Path, string Url, string HostName, string Scheme, string SiteName);

public record SitecoreBaseTemplate(string Id);

public record SitecoreTemplate(string Id, List<SitecoreBaseTemplate> BaseTemplates);

public record SitecorePersonalization(List<string> VariantIds);

public record SitecoreItem(
    string TypeName,
    string Id,
    string Name,
    string DisplayName,
    int Version,
    bool HasChildren,
    string Path,
    object? Rendered,
    SitecoreTemplate Template,
    SitecoreUrl Url,
    SitecoreLanguage Language,
    SitecoreLanguageItem[] Languages,
    SitecoreField[] Fields,
    SitecorePersonalization? Personalization,
    SitecoreItem? Parent,
    SitecoreChildren Children,
    SitecoreItem[] Ancestors
)
{
    public SitecoreField? Field(string name) => Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public record SitecoreLanguageItem(SitecoreLanguage Language);

public record SitecoreLanguage(string DisplayName, string EnglishName, string NativeName, string Name);

public record SitecoreFieldDefinition(string Id, string Type, string Name, string Section, int SectionSortOrder, bool Shared, int SortOrder, string Source, string Title, bool Unversioned);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "__typename")]
[JsonDerivedType(typeof(SitecoreTextField), "TextField")]
[JsonDerivedType(typeof(SitecoreCheckboxField), "CheckboxField")]
[JsonDerivedType(typeof(SitecoreDateField), "DateField")]
[JsonDerivedType(typeof(SitecoreIntegerField), "IntegerField")]
[JsonDerivedType(typeof(SitecoreNumberField), "NumberField")]
[JsonDerivedType(typeof(SitecoreRichTextField), "RichTextField")]
[JsonDerivedType(typeof(SitecoreMultilistField), "MultilistField")]
[JsonDerivedType(typeof(SitecoreLinkField), "LinkField")]
[JsonDerivedType(typeof(SitecoreLookupField), "LookupField")]
[JsonDerivedType(typeof(SitecoreNameValueListField), "NameValueListField")]
[JsonDerivedType(typeof(SitecoreImageField), "ImageField")]
[JsonDerivedType(typeof(SitecoreFileField), "FileField")]
[JsonDerivedType(typeof(SitecoreLayoutField), "LayoutField")]

public record SitecoreField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName);

public record SitecoreTextField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName);

public record SitecoreRichTextField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName);

public record SitecoreDateField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public long? DateValue()
    {
        if (Value == null)
        {
            return 0;
        }

        var dateTimeValue = DateTime.ParseExact(Value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        var unixMs = new DateTimeOffset(dateTimeValue).ToUnixTimeMilliseconds();

        return unixMs;
    }

    public string? FormattedDateValue(string? format, int? offset) => throw new NotSupportedException();
}

public record SitecoreMultilistField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public int Count() => TargetIds()?.Length ?? 0;

    public string[]? TargetIds()
    {
        if (string.IsNullOrEmpty(Value))
        {
            return [];
        }

        return Value.Split(['|'], StringSplitOptions.RemoveEmptyEntries);
    }

    public SitecoreItem[]? TargetItems { get; set; }
}

public record SitecoreNumberField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public double? NumberValue() => Value != null && double.TryParse(Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result) ? result : 0f;
}

public record SitecoreIntegerField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public int? IntValue() => Value != null && int.TryParse(Value, out var intValue) ? intValue : 0;
}

public record SitecoreCheckboxField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public bool? BoolValue() => Value != null && (Value.Equals("1") || Value.Equals("true", StringComparison.OrdinalIgnoreCase));
}

public record SitecoreLinkField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public SitecoreItem? TargetItem { get; set; }

    public string Url() => DataField?.Href ?? string.Empty;

    public string? Anchor() => DataField?.Anchor;

    public string? QueryString() => DataField?.QueryString;

    public string? Target() => DataField?.Target;

    public string? Text() => DataField?.Text;

    public string? LinkType() => DataField?.LinkType;

    public string? ClassName() => DataField?.Class;

    internal string? TargetId() => DataField?.Id;

    private LinkField? _field;

    private LinkField? DataField
    {
        get
        {
            if (_field == null && JsonValue != null)
            {
                _field = JsonValue?.GetProperty("value").Deserialize<LinkField>(JsonSerializerOptions.Web);
            }

            return _field;
        }
    }
}

public record LinkField(string? Href, string? Id, string? Anchor, string? QueryString, string? Target, string? Text, string? LinkType, string? Class);

public record SitecoreLookupField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public SitecoreItem? TargetItem { get; set; }
    public string? TargetId() => Value;
}

public record SitecoreNameValueListField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public NameValueListValue[]? Values()
    {
        if (Value == null)
        {
            return [];
        }

        var values = new List<NameValueListValue>();
        var parsed = HttpUtility.ParseQueryString(Value);

        foreach (var key in parsed.AllKeys)
        {
            if (key == null)
            {
                continue;

            }

            var value = parsed[key];

            values.Add(new NameValueListValue(key, value));
        }

        return [.. values];
    }
}

public record NameValueListValue(string Name, string? Value);

public record SitecoreFileField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public string Url() => DataField?.Src ?? string.Empty;
    public string? Title() => DataField?.Title;
    public string? Description() => DataField?.Description;
    public string? Extension() => DataField?.Extension;
    public string? Keywords() => DataField?.Keywords;
    public string? MimeType() => DataField?.MimeType;
    public int? Size() => DataField?.Size;

    private FileField? _field;

    private FileField? DataField
    {
        get
        {
            if (_field == null && JsonValue != null)
            {
                _field = JsonValue?.GetProperty("value").Deserialize<FileField>(JsonSerializerOptions.Web);
            }

            return _field;
        }
    }
}

public record FileField(string? Src, string? Description, string DisplayName, string? Extension, string? Keywords, string? MimeType, int? Size, string? Title);

public record SitecoreImageField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public string? Src(int? maxHeight, int? maxWidth) => DataField?.Src;
    public int? Width() => DataField?.Width;
    public int? Height() => DataField?.Height;
    public string? Alt() => DataField?.Alt;
    public string? Title() => DataField?.Title;
    public string? Description() => DataField?.Description;
    public string? Extension() => DataField?.Extension;
    public string? Keywords() => DataField?.Keywords;
    public string? MimeType() => DataField?.MimeType;
    public int? Size() => DataField?.Size;

    private ImageField? _field;

    private ImageField? DataField
    {
        get
        {
            if (_field == null && JsonValue != null)
            {
                _field = JsonValue?.GetProperty("value").Deserialize<ImageField>(JsonSerializerOptions.Web);
            }

            return _field;
        }
    }
}

public record ImageField(string? Src, string? Alt, string? Description, string? Extension, int? Width, int? Height, string? Keywords, string? MimeType, int? Size, string? Title);

public record SitecoreLayoutField(string Id, string Name, string? Value, JsonElement? JsonValue, SitecoreFieldDefinition? Definition, string TypeName) : SitecoreField(Id, Name, Value, JsonValue, Definition, TypeName)
{
    public LayoutFieldDevice? Device() => throw new NotSupportedException();
    public LayoutFieldDevice[]? Devices() => throw new NotSupportedException();
}

public record LayoutFieldDevice();

public record PageInfo(bool HasNext, string? EndCursor);

public record SitecoreChildren(PageInfo PageInfo, int Total, SitecoreItem[] Results);