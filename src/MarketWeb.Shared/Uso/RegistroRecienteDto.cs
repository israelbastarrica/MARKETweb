namespace MarketWeb.Shared.Uso;

/// <summary>Un registro abierto recientemente por el usuario (para "Seguir trabajando").</summary>
public sealed class RegistroRecienteDto
{
    public string Ruta { get; set; } = "";       // ruta completa, con id (ej. /insumos/pedidos/567)
    public string Titulo { get; set; } = "";      // nombre legible (ej. "Pedido N° 567 · LURO")
    public DateTime UltimoUso { get; set; }
}
