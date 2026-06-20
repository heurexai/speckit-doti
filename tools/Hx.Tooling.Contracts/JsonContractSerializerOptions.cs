using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hx.Tooling.Contracts;

public static class JsonContractSerializerOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
