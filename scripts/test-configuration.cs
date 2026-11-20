#:project ../Jellyfin.Plugin.YouTubeArchivar/Jellyfin.Plugin.YouTubeArchivar.csproj
#:property PublishAot=false

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using Jellyfin.Plugin.YouTubeArchivar.Configuration;

var serializer = new XmlSerializer(typeof(PluginConfiguration));
var legacyPath = Path.Combine("scripts", "fixtures", "legacy-plugin-configuration.xml");
var path = Path.GetTempFileName();

try
{
    var configuration = Load(legacyPath);
    Require(configuration.CollectionTitle == "YouTube", "Collection title was lost.");
    Require(configuration.MaxDescriptionLength == 300, "Overview length was lost.");
    Require(configuration.EpisodeNumberingScheme == NumberingScheme.YYYYMMDD, "Numbering scheme was lost.");

    var json = JsonSerializer.SerializeToElement(configuration);
    Require(json.TryGetProperty("ServerUrl", out var url) && url.GetString() == "https://example.com/archive", "Saved server URL was lost.");
    Require(json.TryGetProperty("ApiKey", out var key) && key.GetString() == "fixture", "Saved API key was lost.");
    Require(json.EnumerateObject().Count() == 5, "Configuration JSON exposed extra settings.");

    using (var output = File.Create(path))
    {
        serializer.Serialize(output, configuration);
    }

    var saved = XDocument.Load(path).Root!;
    Require((string?)saved.Element("ServerUrl") == "https://example.com/archive", "Server URL was not saved under its new name.");
    Require((string?)saved.Element("ApiKey") == "fixture", "API key was not saved under its new name.");
    Require(saved.Elements().Count() == 5, "Configuration XML wrote extra elements.");
    Require(JsonSerializer.SerializeToElement(Load(path)).ToString() == json.ToString(), "Reload changed the saved settings.");

    var legacyXml = XDocument.Load(legacyPath);
    legacyXml.Root!.AddFirst(new XElement("ServerUrl", string.Empty));
    legacyXml.Root.Add(new XElement("ApiKey", string.Empty));
    legacyXml.Save(path);
    var cleared = Load(path);
    Require(cleared.ServerUrl == string.Empty && cleared.ApiKey == string.Empty, "Legacy values overrode cleared settings.");

    Console.WriteLine("Configuration migration checks passed");
}
finally
{
    File.Delete(path);
}

PluginConfiguration Load(string file)
{
    using var input = File.OpenRead(file);
    return (PluginConfiguration)serializer.Deserialize(input)!;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
