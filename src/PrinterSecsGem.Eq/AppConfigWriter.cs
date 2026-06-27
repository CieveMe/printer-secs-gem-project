using System.Xml.Linq;
using Microsoft.Extensions.Hosting;

namespace PrinterSecsGem.Eq;

public sealed class AppConfigWriter
{
    public AppConfigWriter(IHostEnvironment environment)
    {
    }

    public void SetAppSetting(string key, string value)
    {
        var configPath = GetWritableConfigPath();
        var document = File.Exists(configPath)
            ? XDocument.Load(configPath, LoadOptions.PreserveWhitespace)
            : new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("configuration"));

        var root = document.Root ?? new XElement("configuration");
        if (document.Root is null)
        {
            document.Add(root);
        }

        var appSettings = root.Element("appSettings");
        if (appSettings is null)
        {
            appSettings = new XElement("appSettings");
            root.Add(appSettings);
        }

        var item = appSettings
            .Elements("add")
            .FirstOrDefault(element => string.Equals(
                element.Attribute("key")?.Value,
                key,
                StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            appSettings.Add(new XElement("add", new XAttribute("key", key), new XAttribute("value", value)));
        }
        else
        {
            item.SetAttributeValue("value", value);
        }

        XmlConfigFile.Save(document, configPath, SaveOptions.DisableFormatting);
    }

    private string GetWritableConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "App.config");
    }
}
