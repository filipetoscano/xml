using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lefty.Xml;

/// <summary>
/// Serializes the reports which commands write with --output json.
/// </summary>
public static class JsonReport
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        /*
         * The default escapes characters such as quotes for safe embedding in HTML;
         * this is read by people and scripts, and only needs to be valid JSON.
         */
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter( JsonNamingPolicy.CamelCase ) },
    };


    /// <summary />
    public static string Serialize<T>( T value )
    {
        return JsonSerializer.Serialize( value, Options );
    }
}