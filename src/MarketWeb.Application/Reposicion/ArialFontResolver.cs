using PdfSharpCore.Fonts;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Resolver de fuentes para PdfSharpCore: usa Arial del sistema (el MarketWeb corre en Windows Server).
/// Espejo del ArialFontResolver del desktop. Cae a la fuente regular si falta la negrita.
/// </summary>
public sealed class ArialFontResolver : IFontResolver
{
    private static readonly string FontsDir =
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    public string DefaultFontName => "Arial";

    public byte[] GetFont(string faceName)
    {
        var file = faceName switch
        {
            "Arial#b" => "arialbd.ttf",
            _ => "arial.ttf"
        };
        var path = Path.Combine(FontsDir, file);
        if (!File.Exists(path)) path = Path.Combine(FontsDir, "arial.ttf");
        return File.ReadAllBytes(path);
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(isBold ? "Arial#b" : "Arial");
}
