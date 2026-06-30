namespace MarketWeb.Shared.Marketing;

/// <summary>Perfil de una red (seguidores/publicaciones) — última foto del día.</summary>
public sealed class MktPerfilDto
{
    public string Red { get; set; } = "";          // IG / FB
    public int? Seguidores { get; set; }
    public int? Publicaciones { get; set; }
    public DateTime? FechaHora { get; set; }
}

/// <summary>Una publicación con sus últimas métricas (catálogo + snapshot más reciente).</summary>
public sealed class MktPublicacionDto
{
    public string Red { get; set; } = "";
    public string PostId { get; set; } = "";
    public string Tipo { get; set; } = "";
    public DateTime? FechaPublicacion { get; set; }
    public string Texto { get; set; } = "";
    public string Permalink { get; set; } = "";
    public int? Alcance { get; set; }
    public int? Reproducciones { get; set; }
    public int? MeGusta { get; set; }
    public int? Comentarios { get; set; }
    public int? Compartidos { get; set; }
    public int? Guardados { get; set; }
    public int? Interacciones { get; set; }
    public decimal? TiempoPromedioSeg { get; set; }
    public int? ImpresionesPago { get; set; }
    public int? ImpresionesOrganico { get; set; }
    public decimal? Gasto { get; set; }            // de Meta Ads (MKT_RedesAds), si está pauteada
    public string MonedaAd { get; set; } = "";
    public bool Pagada => (ImpresionesPago ?? 0) > 0 || (Gasto ?? 0) > 0;
    public DateTime? FechaMetrica { get; set; }
}
