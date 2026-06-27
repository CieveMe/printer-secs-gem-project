using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PrinterSecsGem.Eq;

public static class XmlConfigFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Save(XDocument document, string path, SaveOptions options = SaveOptions.None)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = Utf8NoBom,
            Indent = !options.HasFlag(SaveOptions.DisableFormatting),
            OmitXmlDeclaration = document.Declaration is null
        };

        using var writer = XmlWriter.Create(path, settings);
        document.Save(writer);
    }
}
