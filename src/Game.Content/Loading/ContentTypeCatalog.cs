using System.Reflection;
using System.Text.Json;

namespace Game.Content.Loading;

internal sealed record ContentTypeSpec(
    string Kind,
    string FileName,
    string PackagePropertyName);

internal static class ContentTypeCatalog
{
    public static IReadOnlyList<ContentTypeSpec> All { get; } = typeof(ContentPackage)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(CreateSpec)
        .Where(static spec => spec is not null)
        .Cast<ContentTypeSpec>()
        .ToArray();

    private static ContentTypeSpec? CreateSpec(PropertyInfo property)
    {
        if (!property.PropertyType.IsGenericType ||
            property.PropertyType.GetGenericTypeDefinition() != typeof(List<>))
        {
            return null;
        }

        var elementName = property.PropertyType.GetGenericArguments()[0].Name;
        const string definitionSuffix = "Definition";
        if (!elementName.EndsWith(definitionSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        return new ContentTypeSpec(
            JsonNamingPolicy.CamelCase.ConvertName(elementName[..^definitionSuffix.Length]),
            $"{JsonNamingPolicy.KebabCaseLower.ConvertName(property.Name)}.json",
            JsonNamingPolicy.CamelCase.ConvertName(property.Name));
    }
}
