using MarketWeb.Shared.Articulos;
using MarketWeb.Shared.Insumos; // UbicacionDto

namespace MarketWeb.Application.Articulos;

public interface IArticulosService
{
    Task<IReadOnlyList<UbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default);

    /// <summary>Consulta la ficha de un artículo por código (o código de barras vía EQUI) en la ubicación dada.</summary>
    Task<ConsultaArticuloDto?> ConsultarAsync(string codigo, string ubicacion, CancellationToken ct = default);

    /// <summary>Foto del artículo (Drive o IA). Null si no hay.</summary>
    Task<byte[]?> ObtenerFotoAsync(string codigo, bool ia, CancellationToken ct = default);

    /// <summary>Palets activos del depósito que contienen el artículo (lento; endpoint aparte).</summary>
    Task<IReadOnlyList<UbicacionArtDto>> BuscarEnPaletsAsync(string codigo, CancellationToken ct = default);
}
