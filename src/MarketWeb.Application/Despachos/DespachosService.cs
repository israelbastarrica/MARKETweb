using System.Data;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Despachos;

namespace MarketWeb.Application.Despachos;

public sealed class DespachosService : IDespachosService
{
    private readonly ISqlConnectionFactory _db;
    public DespachosService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<DespachoLocalDto>> ListarLocalesAsync(CancellationToken ct = default)
    {
        // Destinos posibles (incluye DEPÓSITO id=1: Logística ve los remitos hacia el depósito).
        const string sql = "SELECT ID AS Id, Descripcion AS Nombre FROM Ubicaciones WHERE Eliminado = 0 ORDER BY Descripcion;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<DespachoLocalDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<int> ResolverIdLocalAsync(string? nombre, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return 0;
        const string sql = "SELECT TOP 1 ID FROM Ubicaciones WHERE Eliminado = 0 AND Descripcion = @nombre;";
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<int?>(new CommandDefinition(sql, new { nombre = nombre.Trim() }, cancellationToken: ct)) ?? 0;
    }

    private sealed record EmisionRow(string Cod, DateTime Fecha);

    private sealed class Row
    {
        public string RemitoID { get; set; } = "";
        public string NroRemito { get; set; } = "";
        public DateTime FechaRemito { get; set; }
        public string Origen { get; set; } = "";
        public string Destino { get; set; } = "";
        public string? Estado { get; set; }
        public DateTime? FechaEscaneo { get; set; }
        public string? UsuarioApp { get; set; }
        public int IDLocalDestino { get; set; }
        public bool EsQRDePantalla { get; set; }
        public string? EstadoDespacho { get; set; }
        public int IDLocalDestinoDespacho { get; set; }
        public string? DestinoDespacho { get; set; }
        public DateTime? FechaDespacho { get; set; }
        public string? EstadoDragon { get; set; }
    }

    public async Task<IReadOnlyList<DespachoDto>> ListarAsync(DateTime desde, DateTime hasta, int? idLocalDestino, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<Row>(new CommandDefinition(
            "SP_RemitosControlListado",
            new { FechaDesde = desde.Date, FechaHasta = hasta.Date, IDLocalDestino = idLocalDestino, Estado = (string?)null },
            commandType: CommandType.StoredProcedure, commandTimeout: 180, cancellationToken: ct));

        var lista = rows.Select(r =>
        {
            var recibido = string.Equals(r.Estado, "RECIBIDO", StringComparison.OrdinalIgnoreCase);
            var despachado = string.Equals(r.EstadoDespacho, "DESPACHADO", StringComparison.OrdinalIgnoreCase);
            var estadoVisual = recibido ? "RECIBIDO" : despachado ? "EN TRÁNSITO" : "PENDIENTE DESPACHO";
            var cruzado = despachado && r.IDLocalDestinoDespacho > 0 && r.IDLocalDestinoDespacho != r.IDLocalDestino;
            return new DespachoDto
            {
                RemitoId = (r.RemitoID ?? "").Trim(),
                NroRemito = r.NroRemito,
                FechaRemito = r.FechaRemito,
                Origen = (r.Origen ?? "").Trim(),
                Destino = (r.Destino ?? "").Trim(),
                EstadoVisual = estadoVisual,
                FechaDespacho = r.FechaDespacho,
                FechaRecepcion = r.FechaEscaneo,
                UsuarioRecepcion = r.UsuarioApp?.Trim(),
                EstadoDragon = r.EstadoDragon?.Trim(),
                EsQRDePantalla = r.EsQRDePantalla,
                Cruzado = cruzado,
                DestinoReal = cruzado ? r.DestinoDespacho?.Trim() : null
            };
        }).ToList();

        // Enriquecer con la fecha+hora de emisión (ImpresorRemito_Cola) para ver mañana/tarde.
        var cods = lista.Select(x => x.RemitoId).Where(c => c.Length > 0).Distinct().ToList();
        if (cods.Count > 0)
        {
            const string sqlEm = "SELECT RTRIM(RemitoCODIGO) AS Cod, MAX(FechaEmision) AS Fecha FROM dbo.ImpresorRemito_Cola WHERE RTRIM(RemitoCODIGO) IN @cods GROUP BY RTRIM(RemitoCODIGO);";
            var emis = (await cn.QueryAsync<EmisionRow>(new CommandDefinition(sqlEm, new { cods }, cancellationToken: ct)))
                .ToDictionary(x => x.Cod, x => x.Fecha, StringComparer.OrdinalIgnoreCase);
            foreach (var d in lista)
                if (emis.TryGetValue(d.RemitoId, out var f)) d.FechaEmision = f;
        }
        return lista;
    }

    public async Task<IReadOnlyList<DespachoArticuloDto>> ListarArticulosAsync(string remitoId, string origen, CancellationToken ct = default)
    {
        var cod = (remitoId ?? "").Trim();
        if (cod.Length == 0) return new List<DespachoArticuloDto>();

        using var cn = _db.Create();

        // Origen restringido a un whitelist → nombre de base Dragonfish (anti-inyección).
        async Task<List<DespachoArticuloDto>> Leer(string baseDb)
        {
            var sql = $@"
                SELECT  ArtCod = RTRIM(DET.FART),
                        Descripcion = MAX(ART.ARTDES),
                        Color = DET.FCOLTXT,
                        Talle = DET.TALLE,
                        Cantidad = SUM(DET.FCANT)
                FROM {baseDb}.ZooLogic.COMPROBANTEVDET DET
                LEFT JOIN {baseDb}.ZooLogic.ART ART ON DET.FART = ART.ARTCOD
                WHERE RTRIM(DET.CODIGO) = @cod
                GROUP BY DET.FART, DET.FCOLTXT, DET.TALLE
                ORDER BY RTRIM(DET.FART), DET.TALLE;";
            return (await cn.QueryAsync<DespachoArticuloDto>(new CommandDefinition(sql, new { cod }, commandTimeout: 60, cancellationToken: ct))).ToList();
        }

        // Misma resolución de base que ControlRemitosService.ContenidoAsync (CENTRAL incluye CCENTRAL unificado).
        switch ((origen ?? "").Trim().ToUpperInvariant())
        {
            case "LURO": return await Leer("DRAGONFISH_LURO");
            case "PERALTA": return await Leer("DRAGONFISH_PERALTA");
            case "CCENTRAL": return await Leer("DRAGONFISH_CCENTRAL");
            default:
                var items = await Leer("DRAGONFISH_CENTRAL");
                if (items.Count == 0) items = await Leer("DRAGONFISH_CCENTRAL");
                return items;
        }
    }

    private sealed record ColaRow(string Cod, string? LocalOrigen, string? LocalDestino, int FPTOVEN, int FNUMCOMP);

    public async Task<QrRemitoDto> PrepararQrAsync(string remitoCodigo, bool esPc, int idLocalUsuario, string? localUsuario, string machineName, CancellationToken ct = default)
    {
        var cod = (remitoCodigo ?? "").Trim();
        if (cod.Length == 0) return new QrRemitoDto { Ok = false, Error = "Remito inválido." };

        using var cn = _db.Create();

        // 1. Buscar el remito en la cola de impresión.
        const string sqlCola = """
            SELECT TOP 1 Cod = RTRIM(RemitoCODIGO), LocalOrigen, LocalDestino, FPTOVEN, FNUMCOMP
            FROM ImpresorRemito_Cola
            WHERE ID > 776 AND RTRIM(RemitoCODIGO) = @cod
            ORDER BY ID DESC;
            """;
        var cola = await cn.QuerySingleOrDefaultAsync<ColaRow>(new CommandDefinition(sqlCola, new { cod }, cancellationToken: ct));
        if (cola is null) return new QrRemitoDto { Ok = false, Error = "No se encontró el remito en la cola." };

        var nroRemito = $"{cola.FPTOVEN:D4}-{cola.FNUMCOMP:D8}";
        var origen = (cola.LocalOrigen ?? "").Trim();
        var destino = (cola.LocalDestino ?? "").Trim();
        var idLocalDestino = await ResolverIdLocalAsync(destino, ct);

        var baseDb = origen.ToUpperInvariant() switch
        {
            "LURO" => "DRAGONFISH_LURO",
            "PERALTA" => "DRAGONFISH_PERALTA",
            "CCENTRAL" => "DRAGONFISH_CCENTRAL",
            _ => "DRAGONFISH_CENTRAL"
        };

        // 2. Validar que NO esté anulado en Dragon.
        try
        {
            var anulado = await cn.ExecuteScalarAsync<bool?>(new CommandDefinition(
                $"SELECT TOP 1 ANULADO FROM {baseDb}.ZooLogic.COMPROBANTEV WITH (NOLOCK) WHERE RTRIM(CODIGO) = @cod AND FLETRA = 'R';",
                new { cod }, commandTimeout: 30, cancellationToken: ct));
            if (anulado == true) return new QrRemitoDto { Ok = false, Error = "El remito está anulado." };
        }
        catch { /* si Dragon no responde, no bloqueamos la regeneración */ }

        // 3. Validar que NO esté ya recibido (escaneado).
        var yaEscaneado = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 1 FROM RemitosEscaneados WHERE RTRIM(CODIGO) = @cod AND IDLocal = @idLocal AND EsDesconocido = 0;",
            new { cod, idLocal = idLocalDestino }, cancellationToken: ct));
        if (yaEscaneado is not null) return new QrRemitoDto { Ok = false, Error = "El remito ya fue recibido." };

        // 4. Loguear la regeneración (auditoría).
        const string sqlLog = """
            INSERT INTO RemitoQRGenerado_Log (Fecha, MachineName, LocalUsuario, IDLocalUsuario, RemitoCODIGO, NroRemito, Origen, Destino)
            VALUES (GETDATE(), @machineName, @localUsuario, @idLocalUsuario, @cod, @nroRemito, @origen, @destino);
            """;
        await cn.ExecuteAsync(new CommandDefinition(sqlLog, new
        {
            machineName = string.IsNullOrWhiteSpace(machineName) ? "WEB" : machineName,
            localUsuario,
            idLocalUsuario = idLocalUsuario > 0 ? idLocalUsuario : (int?)null,
            cod, nroRemito, origen, destino
        }, cancellationToken: ct));

        // 5. El QR codifica el código + "-PC" si se escanea por esta opción del sistema.
        var codigoQr = esPc ? cod + "-PC" : cod;
        return new QrRemitoDto { Ok = true, NroRemito = nroRemito, Origen = origen, Destino = destino, CodigoQr = codigoQr };
    }
}
