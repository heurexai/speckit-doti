using YamlDotNet.RepresentationModel;

namespace Hx.Doti.Core.ManagedAssets;

public static partial class CanonicalContentHasher
{
    private static string CanonicalizeYaml(byte[] bytes)
    {
        string text = DecodeUtf8(bytes);
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return string.Join("\n", stream.Documents.Select(d => CanonicalYamlNode(d.RootNode)));
    }

    private static string CanonicalYamlNode(YamlNode node) =>
        node switch
        {
            YamlScalarNode scalar => "S" + Field(TagOf(scalar)) + Field(scalar.Value ?? ""),
            YamlSequenceNode sequence => "Q" + Field(TagOf(sequence)) + "[" +
                                         string.Join("", sequence.Children.Select(CanonicalYamlNode)) + "]",
            YamlMappingNode mapping => CanonicalYamlMapping(mapping),
            _ => throw new InvalidOperationException($"Unsupported YAML node type '{node.GetType().Name}'."),
        };

    private static string CanonicalYamlMapping(YamlMappingNode mapping)
    {
        var pairs = new List<(string Key, string Value)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<YamlNode, YamlNode> child in mapping.Children)
        {
            string key = CanonicalYamlNode(child.Key);
            if (!seen.Add(key))
            {
                throw new InvalidOperationException("YAML mapping contains duplicate canonical keys.");
            }

            pairs.Add((key, CanonicalYamlNode(child.Value)));
        }

        return "M" + Field(TagOf(mapping)) + "{" +
               string.Join("", pairs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Field(p.Key) + Field(p.Value))) +
               "}";
    }

    private static string TagOf(YamlNode node) => node.Tag.ToString();

    private static string Field(string value) => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;
}
