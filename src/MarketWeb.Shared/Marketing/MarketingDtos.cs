namespace MarketWeb.Shared.Marketing;

/// <summary>Perfil de una red (seguidores/publicaciones) — última foto del día.</summary>
public sealed class MktPerfilDto
{
    public string Red { get; set; } = "";          // IG / FB
    public int? Seguidores { get; set; }
    public int? Publicaciones { get; set; }
    public DateTime? FechaHora { get; set; }
}

/// <summary>Un punto de una serie temporal (para el gráfico de tendencia).</summary>
public sealed class MktSeriePuntoDto
{
    public DateTime Fecha { get; set; }
    public long Valor { get; set; }
}

/// <summary>KPIs + tendencia + contenido destacado para el Dashboard de Marketing (insights de cuenta).</summary>
public sealed class MktDashboardDto
{
    public DateTime? Hasta { get; set; }
    public int? VisualizacionesIG { get; set; }       // IG views (período)
    public int? Alcance { get; set; }                 // IG reach del período (seguidores + no)
    public int? AlcanceSeguidores { get; set; }
    public int? AlcanceNoSeguidores { get; set; }
    public int? Interacciones { get; set; }           // IG total_interactions
    public int? CuentasAlcanzadas { get; set; }       // IG accounts_engaged
    public int? FbInteracciones { get; set; }         // FB page_post_engagements (suma del período)
    public int? FbSeguidoresNuevos { get; set; }      // FB page_daily_follows (suma)
    public int? FbDejaronSeguir { get; set; }         // FB page_daily_unfollows (suma)
    public List<MktSeriePuntoDto> AlcanceSerie { get; set; } = new();   // alcance IG por día (tendencia)
    public List<MktPublicacionDto> Destacados { get; set; } = new();    // top publicaciones por alcance
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
