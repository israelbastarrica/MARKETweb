using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Application.Reposicion;

public interface IControlRemitosService
{
    /// <summary>Estado de control por fecha + local (SP_RemitosControlEstado).</summary>
    Task<IReadOnlyList<ControlEstadoDto>> EstadoAsync(DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Detalle remito por remito (SP_RemitosControlListado). idDestino 0 = TODOS; estado ""/"TODOS"/"RECIBIDO"/"NO RECIBIDO".</summary>
    Task<IReadOnlyList<RemitoControlDto>> ListadoAsync(DateTime desde, DateTime hasta, int idDestino, string estado, CancellationToken ct = default);

    /// <summary>Contenido (items) de un remito según su origen.</summary>
    Task<IReadOnlyList<EventoItemDto>> ContenidoAsync(string remitoId, string origen, CancellationToken ct = default);

    /// <summary>Reasigna el destino de un despacho (SP_RemitosDespachadosReasignarDestino).</summary>
    Task ReasignarDestinoAsync(int despachoId, int nuevoIdDestino, CancellationToken ct = default);

    /// <summary>Baja lógica de un despacho (SP_RemitosDespachadosEliminar).</summary>
    Task EliminarDespachoAsync(int despachoId, CancellationToken ct = default);
}
