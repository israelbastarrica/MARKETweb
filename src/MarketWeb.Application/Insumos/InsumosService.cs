using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Insumos;

namespace MarketWeb.Application.Insumos;

/// <summary>
/// Insumos — pedidos por local (Fase 1, solo lectura). Espejo de la grilla de
/// frmRepoLocalInsumos. Tablas MARKET (PedidosInsumos/Detalle/Ubicaciones);
/// Dragonfish recién se usa en ABM/impresión (fases siguientes). Parametrizado.
/// </summary>
public sealed class InsumosService : IInsumosService
{
    private readonly ISqlConnectionFactory _db;
    private readonly MarketWeb.Application.Dragonfish.IDragonfishService _dragon;

    public InsumosService(ISqlConnectionFactory db, MarketWeb.Application.Dragonfish.IDragonfishService dragon)
    {
        _db = db;
        _dragon = dragon;
    }

    public async Task<IReadOnlyList<UbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT ID AS Id, Descripcion FROM Ubicaciones WHERE Eliminado = 0 ORDER BY Descripcion;";
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<UbicacionDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PedidoInsumoDto>> ListarPedidosAsync(
        int? ubicacionId, string estado, CancellationToken ct = default)
    {
        // Modo de ubicación: TODOS / LOCALES (Luro=2, Peralta=3) / ID puntual.
        var modoUbic = ubicacionId is null ? "TODOS" : (ubicacionId == -1 ? "LOCALES" : "ID");
        var idLoc = ubicacionId is > 0 ? ubicacionId.Value : 0;
        var est = string.IsNullOrWhiteSpace(estado) ? "TODOS" : estado.Trim().ToUpperInvariant();

        const string sql = """
            SELECT
                ISNULL(U.Descripcion, 'SIN ASIGNAR') AS Ubicacion,
                CONVERT(VARCHAR, P.NroPedido) AS NroPedido,
                CASE
                    WHEN P.FechaEnviado   IS NOT NULL THEN 'ENVIADO '  + CONVERT(VARCHAR, P.FechaEnviado, 120)
                    WHEN P.FechaImpresion IS NOT NULL THEN 'EN ARMADO ' + CONVERT(VARCHAR, P.FechaImpresion, 120)
                    ELSE ISNULL(P.Estado, '')
                END AS Estado,
                ISNULL((SELECT COUNT(ID)          FROM PedidosInsumosDetalle D WHERE D.IDPedido = P.ID AND D.Eliminado = 0), 0) AS CantArt,
                ISNULL((SELECT SUM(Cantidad)       FROM PedidosInsumosDetalle D WHERE D.IDPedido = P.ID AND D.Eliminado = 0), 0) AS CantidadTotal,
                ISNULL((SELECT COUNT(ID)          FROM PedidosInsumosDetalle D WHERE D.IDPedido = P.ID AND D.Eliminado = 0 AND D.Existencia = 1), 0) AS CantArtEnviada,
                ISNULL((SELECT SUM(CantidadEnviada) FROM PedidosInsumosDetalle D WHERE D.IDPedido = P.ID AND D.Eliminado = 0 AND D.Existencia = 1), 0) AS CantidadTotalEnviada,
                P.ID AS Id,
                CAST(CASE WHEN P.FechaEnviado IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS Enviado,
                CAST(CASE WHEN P.FechaImpresion IS NOT NULL OR P.FechaEnviado IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS Cerrado
            FROM PedidosInsumos P
            LEFT JOIN Ubicaciones U ON P.IDLocal = U.ID
            WHERE P.Eliminado = 0
              AND (@modoUbic = 'TODOS'
                   OR (@modoUbic = 'LOCALES' AND P.IDLocal IN (2, 3))
                   OR (@modoUbic = 'ID' AND P.IDLocal = @idLoc))
              AND (@estado = 'TODOS'
                   OR (@estado = 'SIN ENVIAR' AND P.FechaEnviado IS NULL)
                   OR (@estado = 'ENVIADOS'  AND P.FechaEnviado IS NOT NULL))
            ORDER BY P.FechaPedido DESC, P.NroPedido DESC;
            """;

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<PedidoInsumoDto>(new CommandDefinition(
            sql, new { modoUbic = modoUbic, idLoc, estado = est }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<InsumoConsumoDto>> ListarConsumosAsync(
        int? ubicacionId, string estado, CancellationToken ct = default)
    {
        var modoUbic = ubicacionId is null ? "TODOS" : (ubicacionId == -1 ? "LOCALES" : "ID");
        var idLoc = ubicacionId is > 0 ? ubicacionId.Value : 0;
        var est = string.IsNullOrWhiteSpace(estado) ? "SIN MARCAR" : estado.Trim().ToUpperInvariant();

        const string sql = """
            WITH Consumos AS (
                SELECT ISNULL(U.Descripcion,'SIN ASIGNAR') AS Ubicacion, RTRIM(R.ARTCOD) AS ARTCOD,
                       R.Cantidad AS Cantidad, 'R|' + CAST(R.ID AS VARCHAR) AS IDRef
                FROM PedidosInsumosRegistro R
                LEFT JOIN Ubicaciones U ON R.IDLocal = U.ID
                WHERE R.Eliminado = 0
                  AND (@modoUbic='TODOS' OR (@modoUbic='LOCALES' AND R.IDLocal IN (2,3)) OR (@modoUbic='ID' AND R.IDLocal=@idLoc))
                  AND (@estado='TODOS'
                       OR (@estado='SIN MARCAR' AND R.ID NOT IN (SELECT IDPedidosDetalle FROM PedidosInsumosAdministracion WHERE Motivo='REGISTRO' AND Procesado=1 AND Eliminado=0))
                       OR (@estado='MARCADOS'  AND R.ID IN     (SELECT IDPedidosDetalle FROM PedidosInsumosAdministracion WHERE Motivo='REGISTRO' AND Procesado=1 AND Eliminado=0)))
                UNION ALL
                SELECT ISNULL(U.Descripcion,'SIN ASIGNAR') AS Ubicacion, RTRIM(D.ARTCOD) AS ARTCOD,
                       ISNULL(D.CantidadEnviada, D.Cantidad) AS Cantidad, 'P|' + CAST(D.ID AS VARCHAR) AS IDRef
                FROM PedidosInsumosDetalle D
                INNER JOIN PedidosInsumos P ON D.IDPedido = P.ID
                LEFT JOIN Ubicaciones U ON P.IDLocal = U.ID
                WHERE D.Eliminado = 0 AND P.Eliminado = 0
                  AND ISNULL(D.Existencia,1) <> 0 AND ISNULL(D.CantidadEnviada, D.Cantidad) > 0
                  AND P.FechaEnviado IS NOT NULL
                  AND (@modoUbic='TODOS' OR (@modoUbic='LOCALES' AND P.IDLocal IN (2,3)) OR (@modoUbic='ID' AND P.IDLocal=@idLoc))
                  AND (@estado='TODOS'
                       OR (@estado='SIN MARCAR' AND D.ID NOT IN (SELECT IDPedidosDetalle FROM PedidosInsumosAdministracion WHERE Motivo='PEDIDO' AND Procesado=1 AND Eliminado=0))
                       OR (@estado='MARCADOS'  AND D.ID IN     (SELECT IDPedidosDetalle FROM PedidosInsumosAdministracion WHERE Motivo='PEDIDO' AND Procesado=1 AND Eliminado=0)))
            )
            SELECT
                ISNULL(RTRIM(PROV.CLNOM),'SIN PROVEEDOR') AS Proveedor,
                C.Ubicacion AS Ubicacion,
                C.ARTCOD AS ArtCod,
                ISNULL(RTRIM(ART.ARTDES),'Sin descripción') AS Descripcion,
                SUM(C.Cantidad) AS CantidadTotal,
                STUFF((SELECT ',' + C2.IDRef FROM Consumos C2 WHERE C2.Ubicacion = C.Ubicacion AND C2.ARTCOD = C.ARTCOD FOR XML PATH('')),1,1,'') AS AgrupacionIds
            FROM Consumos C
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART  ART  WITH(NOLOCK) ON C.ARTCOD = RTRIM(ART.ARTCOD)
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.PROV PROV WITH(NOLOCK) ON ART.ARTFAB = PROV.CLCOD
            GROUP BY ISNULL(RTRIM(PROV.CLNOM),'SIN PROVEEDOR'), C.Ubicacion, C.ARTCOD, RTRIM(ART.ARTDES)
            ORDER BY Proveedor ASC, C.ARTCOD ASC;
            """;

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<InsumoConsumoDto>(new CommandDefinition(
            sql, new { modoUbic = modoUbic, idLoc, estado = est }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> MarcarAsync(IEnumerable<string> refs, string usuario, CancellationToken ct = default)
    {
        // Parseamos "R|id" / "P|id" → (Motivo, Id) descartando lo inválido.
        var items = new List<object>();
        var audit = $"Procesado a Proveedor | {usuario} | {DateTime.Now}";
        foreach (var raw in refs ?? Enumerable.Empty<string>())
        {
            var partes = (raw ?? "").Split('|');
            if (partes.Length != 2) continue;
            var motivo = partes[0].Trim().ToUpperInvariant() == "R" ? "REGISTRO" : "PEDIDO";
            if (!int.TryParse(partes[1].Trim(), out var id)) continue;
            items.Add(new { id, motivo, audit });
        }
        if (items.Count == 0) return 0;

        // Idempotente: el NOT EXISTS evita duplicar si el consumo ya estaba marcado.
        const string sql = """
            INSERT INTO PedidosInsumosAdministracion (IDPedidosDetalle, Motivo, Procesado, Eliminado, Auditoria)
            SELECT @id, @motivo, 1, 0, @audit
            WHERE NOT EXISTS (SELECT 1 FROM PedidosInsumosAdministracion WHERE IDPedidosDetalle = @id AND Motivo = @motivo);
            """;
        using var cn = _db.Create();
        return await cn.ExecuteAsync(new CommandDefinition(sql, items, cancellationToken: ct));
    }

    // ================= ABM de pedidos (LOCALES) =================

    public async Task<ValidacionFechaDto> ValidarFechaPedidoAsync(int idLocal, CancellationToken ct = default)
    {
        // Día 1 o 15 → libre.
        var hoy = DateTime.Now;
        if (hoy.Day == 1 || hoy.Day == 15)
            return new ValidacionFechaDto { Resultado = "OK", Mensaje = "" };

        // Fuera de término: ¿ya pidió en esta quincena?
        const string sql = """
            SELECT COUNT(*) FROM PedidosInsumos
            WHERE IDLocal = @idLocal AND Eliminado = 0
              AND MONTH(FechaPedido) = MONTH(GETDATE()) AND YEAR(FechaPedido) = YEAR(GETDATE())
              AND ((DAY(GETDATE()) <= 15 AND DAY(FechaPedido) <= 15) OR (DAY(GETDATE()) > 15 AND DAY(FechaPedido) > 15));
            """;
        using var cn = _db.Create();
        var enQuincena = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { idLocal }, cancellationToken: ct));

        // Textos calcados de la app Android / desktop para que el local vea lo mismo.
        if (enQuincena == 0)
            return new ValidacionFechaDto
            {
                Resultado = "AVISO",
                Titulo = "⚠️ Fuera de término",
                Mensaje = "Recordá que los pedidos de insumos deben hacerse EXCLUSIVAMENTE los días 1 o 15 de cada mes.\n\n" +
                          "Como no tenés pedidos en esta quincena, el sistema te dejará ingresar por esta vez."
            };

        return new ValidacionFechaDto
        {
            Resultado = "BLOQUEADO",
            Titulo = "❌ Acceso Denegado",
            Mensaje = "Ya realizaste un pedido de insumos durante esta quincena.\n\nSolo podés volver a pedir el próximo día 1 o 15."
        };
    }

    public async Task<PedidoEditorDto?> ObtenerEditorAsync(int idPedido, CancellationToken ct = default)
    {
        const string sqlCab = """
            SELECT P.ID AS Id, P.NroPedido, P.FechaPedido, P.IDLocal AS IDLocal,
                   ISNULL(U.Descripcion,'SIN ASIGNAR') AS LocalNombre,
                   CASE WHEN P.FechaEnviado IS NOT NULL THEN 'ENVIADO'
                        WHEN P.FechaImpresion IS NOT NULL THEN 'EN ARMADO'
                        ELSE ISNULL(P.Estado,'PENDIENTE') END AS Estado,
                   CAST(CASE WHEN P.FechaEnviado IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS Enviado,
                   CAST(CASE WHEN P.FechaImpresion IS NOT NULL OR P.FechaEnviado IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS Cerrado
            FROM PedidosInsumos P
            LEFT JOIN Ubicaciones U ON P.IDLocal = U.ID
            WHERE P.ID = @idPedido AND P.Eliminado = 0;
            """;
        const string sqlDet = """
            SELECT D.ID AS Id, RTRIM(D.ARTCOD) AS ArtCod,
                   ISNULL(RTRIM(ART.ARTDES),'Sin descripción') AS Descripcion,
                   D.Cantidad AS Cantidad,
                   CAST(CASE WHEN ISNULL(D.Existencia,1) = 0 THEN 0 ELSE 1 END AS BIT) AS Existencia,
                   D.CantidadEnviada AS CantidadEnviada,
                   CAST(CASE WHEN ISNULL(D.NoRequiereConsumo,0) = 1 THEN 1 ELSE 0 END AS BIT) AS NoRequiereConsumo
            FROM PedidosInsumosDetalle D
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART WITH(NOLOCK) ON RTRIM(ART.ARTCOD) = RTRIM(D.ARTCOD)
            WHERE D.IDPedido = @idPedido AND D.Eliminado = 0
            ORDER BY D.ID;
            """;
        using var cn = _db.Create();
        var cab = await cn.QuerySingleOrDefaultAsync<PedidoCabeceraDto>(new CommandDefinition(sqlCab, new { idPedido }, cancellationToken: ct));
        if (cab is null) return null;
        var det = (await cn.QueryAsync<PedidoRenglonDto>(new CommandDefinition(sqlDet, new { idPedido }, commandTimeout: 60, cancellationToken: ct))).ToList();
        return new PedidoEditorDto { Cabecera = cab, Renglones = det };
    }

    public async Task<IReadOnlyList<ArticuloInsumoDto>> BuscarArticulosInsumoAsync(string busqueda, CancellationToken ct = default)
    {
        var q = (busqueda ?? "").Trim();
        if (q.Length < 2) return new List<ArticuloInsumoDto>();
        const string sql = """
            SELECT TOP 50 RTRIM(ART.ARTCOD) AS ArtCod, RTRIM(ART.ARTDES) AS ArtDes,
                   ISNULL((SELECT SUM(COCANT) FROM DRAGONFISH_CENTRAL.Zoologic.COMB WHERE COART = ART.ARTCOD), 0) AS Stock,
                   (SELECT TOP 1 RTRIM(UnidadMedida) FROM PedidosInsumosArticulos WHERE RTRIM(ARTCOD) = RTRIM(ART.ARTCOD)) AS UnidadMedida
            FROM DRAGONFISH_CENTRAL.Zoologic.ART ART WITH(NOLOCK)
            WHERE ART.TIPOARTI = 'IS' AND ART.ARTDES LIKE '%' + @q + '%'
            ORDER BY ART.ARTDES;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<ArticuloInsumoDto>(new CommandDefinition(sql, new { q }, commandTimeout: 60, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<CrearPedidoResultado> GuardarPedidoAsync(GuardarPedidoRequest req, string usuario, bool esDeposito, CancellationToken ct = default)
    {
        // El depósito modifica un pedido existente: actualiza POR RENGLÓN (idéntico al .Net).
        // Solo toca Existencia / CantidadEnviada / NoRequiereConsumo; la cantidad pedida no.
        if (esDeposito && req.Id != 0)
            return await GuardarDepositoAsync(req, usuario, ct);

        var renglones = (req.Renglones ?? new())
            .Where(r => !string.IsNullOrWhiteSpace(r.ArtCod) && r.Cantidad > 0)
            .Select(r => new
            {
                Art = r.ArtCod.Trim(),
                r.Cantidad,
                Existencia = r.Existencia ? 1 : 0,
                // Sin stock → 0 enviado; si no, lo indicado (o la pedida si no se tocó).
                CantEnv = !r.Existencia ? 0 : (r.CantidadEnviada ?? r.Cantidad)
            })
            .ToList();
        if (renglones.Count == 0) throw new InvalidOperationException("El pedido debe tener al menos un insumo.");

        // El alta re-valida la regla 1/15 (solo aplica al local; el depósito no crea con esa regla).
        if (req.Id == 0)
        {
            if (req.IdLocal != 2 && req.IdLocal != 3) throw new InvalidOperationException("Local inválido para pedido de insumos.");
            if (!esDeposito)
            {
                var val = await ValidarFechaPedidoAsync(req.IdLocal, ct);
                if (val.Resultado == "BLOQUEADO") throw new InvalidOperationException(val.Mensaje);
            }
        }

        using var cn = _db.Create();
        await cn.OpenAsync(ct);
        using var tx = cn.BeginTransaction();
        try
        {
            int id, nro;
            if (req.Id == 0)
            {
                var auditH = $"Alta Pedido Insumos | {usuario} | {DateTime.Now}";
                var crea = await cn.QuerySingleAsync<CrearPedidoResultado>(new CommandDefinition("""
                    DECLARE @nro INT = (SELECT ISNULL(MAX(NroPedido),0)+1 FROM PedidosInsumos);
                    INSERT INTO PedidosInsumos (IDLocal, NroPedido, FechaPedido, Estado, Eliminado, Auditoria)
                    VALUES (@idLocal, @nro, GETDATE(), 'PENDIENTE', 0, @audit);
                    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id, @nro AS NroPedido;
                    """, new { idLocal = req.IdLocal, audit = auditH }, tx, cancellationToken: ct));
                id = crea.Id; nro = crea.NroPedido;
            }
            else
            {
                // Local: bloquea desde que DEPÓSITO imprimió (EN ARMADO) o envió.
                // Depósito: puede editar EN ARMADO; solo se bloquea si ya está ENVIADO.
                var lockSql = esDeposito
                    ? "SELECT 1 FROM PedidosInsumos WHERE ID=@id AND FechaEnviado IS NOT NULL AND Eliminado=0;"
                    : "SELECT 1 FROM PedidosInsumos WHERE ID=@id AND (FechaImpresion IS NOT NULL OR FechaEnviado IS NOT NULL) AND Eliminado=0;";
                var cerrado = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(lockSql, new { id = req.Id }, tx, cancellationToken: ct));
                if (cerrado is not null)
                    throw new InvalidOperationException(esDeposito
                        ? "El pedido ya fue ENVIADO: no se puede modificar."
                        : "El pedido ya fue tomado por Depósito (en armado/enviado): cerrado para el local.");

                var auditH = $"Modificación Pedido Insumos | {usuario} | {DateTime.Now}";
                // Reemplazo de renglones: baja lógica de los actuales + re-inserta los confirmados.
                await cn.ExecuteAsync(new CommandDefinition("""
                    UPDATE PedidosInsumosDetalle SET Eliminado=1, Auditoria=@audit WHERE IDPedido=@id AND Eliminado=0;
                    UPDATE PedidosInsumos SET Auditoria=@audit WHERE ID=@id;
                    """, new { id = req.Id, audit = auditH }, tx, cancellationToken: ct));
                id = req.Id;
                nro = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT NroPedido FROM PedidosInsumos WHERE ID=@id;", new { id }, tx, cancellationToken: ct));
            }

            var auditD = $"Alta registro | {usuario} | {DateTime.Now}";
            // Local: sin existencia/cant enviada (las marca el depósito después).
            const string insLocal = """
                INSERT INTO PedidosInsumosDetalle (IDPedido, ARTCOD, Cantidad, Eliminado, Auditoria, NoRequiereConsumo)
                VALUES (@id, @art, @cant, 0, @audit, 0);
                """;
            // Depósito: persiste lo marcado por renglón (existencia + cantidad enviada).
            const string insDeposito = """
                INSERT INTO PedidosInsumosDetalle (IDPedido, ARTCOD, Cantidad, Existencia, CantidadEnviada, Eliminado, Auditoria, NoRequiereConsumo)
                VALUES (@id, @art, @cant, @existencia, @cantEnv, 0, @audit, 0);
                """;
            foreach (var r in renglones)
            {
                if (esDeposito)
                    await cn.ExecuteAsync(new CommandDefinition(insDeposito,
                        new { id, art = r.Art, cant = r.Cantidad, existencia = r.Existencia, cantEnv = r.CantEnv, audit = auditD }, tx, cancellationToken: ct));
                else
                    await cn.ExecuteAsync(new CommandDefinition(insLocal,
                        new { id, art = r.Art, cant = r.Cantidad, audit = auditD }, tx, cancellationToken: ct));
            }

            tx.Commit();
            return new CrearPedidoResultado { Id = id, NroPedido = nro };
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // Guardado del DEPÓSITO sobre un pedido existente (idéntico al .Net): por renglón,
    // solo Existencia / CantidadEnviada / NoRequiereConsumo (no la cantidad pedida ni el ARTCOD).
    // Los renglones que el depósito quitó van a baja lógica. Solo se bloquea si ya está ENVIADO.
    private async Task<CrearPedidoResultado> GuardarDepositoAsync(GuardarPedidoRequest req, string usuario, CancellationToken ct)
    {
        var presentes = (req.Renglones ?? new()).Where(r => r.Id > 0).ToList();
        if (presentes.Count == 0) throw new InvalidOperationException("El pedido debe tener al menos un insumo.");

        using var cn = _db.Create();
        await cn.OpenAsync(ct);
        // Marca de "procesado por depósito" (idempotente). Sirve para exigir que Logística
        // haya guardado el pedido al menos una vez antes de generar el remito.
        await cn.ExecuteAsync(new CommandDefinition(
            "IF COL_LENGTH('dbo.PedidosInsumos','FechaProceso') IS NULL ALTER TABLE dbo.PedidosInsumos ADD FechaProceso DATETIME NULL;",
            cancellationToken: ct));
        using var tx = cn.BeginTransaction();
        try
        {
            var enviado = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT 1 FROM PedidosInsumos WHERE ID=@id AND FechaEnviado IS NOT NULL AND Eliminado=0;",
                new { id = req.Id }, tx, cancellationToken: ct));
            if (enviado is not null) throw new InvalidOperationException("El pedido ya fue ENVIADO: no se puede modificar.");

            var audit = $"Modificación depósito | {usuario} | {DateTime.Now}";
            var ids = presentes.Select(r => r.Id).ToArray();

            // Renglones que el depósito quitó (existían y ya no vienen) → baja lógica.
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE PedidosInsumosDetalle SET Eliminado=1, Auditoria=@audit WHERE IDPedido=@id AND Eliminado=0 AND ID NOT IN @ids;",
                new { id = req.Id, audit, ids }, tx, cancellationToken: ct));

            const string upd = """
                UPDATE PedidosInsumosDetalle
                SET Existencia=@ex, CantidadEnviada=@cantEnv, NoRequiereConsumo=@nrc, Auditoria=@audit
                WHERE ID=@idDet AND IDPedido=@idPed AND Eliminado=0;
                """;
            foreach (var r in presentes)
            {
                var ex = r.Existencia ? 1 : 0;
                var cantEnv = !r.Existencia ? 0 : (r.CantidadEnviada ?? r.Cantidad);
                await cn.ExecuteAsync(new CommandDefinition(upd,
                    new { idDet = r.Id, idPed = req.Id, ex, cantEnv, nrc = r.NoRequiereConsumo ? 1 : 0, audit }, tx, cancellationToken: ct));
            }

            // Cabecera: auditoría + marca de PROCESADO por depósito (guardó con sus cambios).
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE PedidosInsumos SET Auditoria=@audit, FechaProceso=GETDATE() WHERE ID=@id;", new { id = req.Id, audit }, tx, cancellationToken: ct));

            var nro = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT NroPedido FROM PedidosInsumos WHERE ID=@id;", new { id = req.Id }, tx, cancellationToken: ct));
            tx.Commit();
            return new CrearPedidoResultado { Id = req.Id, NroPedido = nro };
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private sealed record ArmadoRow(int PedidoId, int NroPedido, string Local, string ArtCod, string Descripcion, int Cantidad);

    public async Task<ArmadoInsumosDto> ImprimirArmadoAsync(int? ubicacionId, string usuario, CancellationToken ct = default)
    {
        var modoUbic = ubicacionId is null ? "TODOS" : (ubicacionId == -1 ? "LOCALES" : "ID");
        var idLoc = ubicacionId is > 0 ? ubicacionId.Value : 0;

        using var cn = _db.Create();
        await cn.OpenAsync(ct);
        using var tx = cn.BeginTransaction();
        try
        {
            // Pedidos pendientes (sin imprimir ni enviar) con detalle, del filtro de ubicación.
            const string sqlSel = """
                SELECT P.ID AS PedidoId, P.NroPedido AS NroPedido, ISNULL(U.Descripcion,'SIN ASIGNAR') AS Local,
                       RTRIM(D.ARTCOD) AS ArtCod, ISNULL(RTRIM(ART.ARTDES),'Sin descripción') AS Descripcion, D.Cantidad AS Cantidad
                FROM PedidosInsumos P
                LEFT JOIN Ubicaciones U ON P.IDLocal = U.ID
                INNER JOIN PedidosInsumosDetalle D ON D.IDPedido = P.ID AND D.Eliminado = 0
                LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART WITH(NOLOCK) ON RTRIM(ART.ARTCOD) = RTRIM(D.ARTCOD)
                WHERE P.Eliminado = 0 AND P.FechaImpresion IS NULL AND P.FechaEnviado IS NULL
                  AND (@modoUbic='TODOS'
                       OR (@modoUbic='LOCALES' AND P.IDLocal IN (2,3))
                       OR (@modoUbic='ID' AND P.IDLocal=@idLoc))
                ORDER BY P.NroPedido, D.ID;
                """;
            var rows = (await cn.QueryAsync<ArmadoRow>(new CommandDefinition(
                sqlSel, new { modoUbic, idLoc }, tx, commandTimeout: 60, cancellationToken: ct))).ToList();

            var pedidos = rows
                .GroupBy(r => new { r.PedidoId, r.NroPedido, r.Local })
                .Select(g => new ArmadoPedidoDto
                {
                    Id = g.Key.PedidoId,
                    NroPedido = g.Key.NroPedido,
                    Local = g.Key.Local,
                    Renglones = g.Select(x => new ArmadoRenglonDto { ArtCod = x.ArtCod, Descripcion = x.Descripcion, Cantidad = x.Cantidad }).ToList()
                })
                .ToList();

            if (pedidos.Count > 0)
            {
                var ids = pedidos.Select(p => p.Id).ToArray();
                var audit = $"Impreso armado (EN ARMADO) | {usuario} | {DateTime.Now}";
                await cn.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE PedidosInsumos SET FechaImpresion = GETDATE(), Auditoria = @audit
                    WHERE ID IN @ids AND FechaImpresion IS NULL AND FechaEnviado IS NULL AND Eliminado = 0;
                    """,
                    new { ids, audit }, tx, cancellationToken: ct));
            }

            tx.Commit();
            return new ArmadoInsumosDto { Pedidos = pedidos };
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private sealed record RemitoRow(int PedidoId, int IdLocal, string Local, string ArtCod, int Cant);

    // Genera los remitos de insumos (uno por local) en Dragonfish (Motivo 13, CENTRAL→local) para los
    // pedidos EN ARMADO no enviados. Solo crea el remito; el despacho/aceptación lo hace la app del local.
    // Al crear ok, marca el pedido como ENVIADO (FechaEnviado).
    public async Task<GenerarRemitosResultado> GenerarRemitosAsync(int? ubicacionId, string usuario, CancellationToken ct = default)
    {
        var res = new GenerarRemitosResultado();
        if (!_dragon.Configurado)
        {
            res.Locales.Add(new RemitoLocalResultado { Local = "—", Ok = false, Error = "La API Dragonfish no está configurada en el servidor." });
            return res;
        }

        var modoUbic = ubicacionId is null ? "TODOS" : (ubicacionId == -1 ? "LOCALES" : "ID");
        var idLoc = ubicacionId is > 0 ? ubicacionId.Value : 0;

        using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(
            "IF COL_LENGTH('dbo.PedidosInsumos','FechaProceso') IS NULL ALTER TABLE dbo.PedidosInsumos ADD FechaProceso DATETIME NULL;",
            cancellationToken: ct));

        // Solo se remiten pedidos que el DEPÓSITO ya guardó (FechaProceso): así el envío refleja lo que
        // realmente se manda, no una copia de lo pedido. Los EN ARMADO sin guardar quedan afuera y se avisan.
        res.SinProcesar = await cn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM PedidosInsumos P
            WHERE P.Eliminado = 0 AND P.FechaImpresion IS NOT NULL AND P.FechaEnviado IS NULL AND P.FechaProceso IS NULL
              AND (@modoUbic='TODOS' OR (@modoUbic='LOCALES' AND P.IDLocal IN (2,3)) OR (@modoUbic='ID' AND P.IDLocal=@idLoc));
            """, new { modoUbic, idLoc }, cancellationToken: ct));

        // Renglones enviados de pedidos EN ARMADO, YA PROCESADOS por depósito y todavía no enviados.
        const string sql = """
            SELECT P.ID AS PedidoId, P.IDLocal AS IdLocal, ISNULL(U.Descripcion,'') AS Local,
                   RTRIM(D.ARTCOD) AS ArtCod, CAST(ISNULL(D.CantidadEnviada, D.Cantidad) AS INT) AS Cant
            FROM PedidosInsumos P
            LEFT JOIN Ubicaciones U ON P.IDLocal = U.ID
            INNER JOIN PedidosInsumosDetalle D ON D.IDPedido = P.ID AND D.Eliminado = 0
            WHERE P.Eliminado = 0 AND P.FechaImpresion IS NOT NULL AND P.FechaEnviado IS NULL AND P.FechaProceso IS NOT NULL
              AND ISNULL(D.Existencia,1) = 1 AND ISNULL(D.CantidadEnviada, D.Cantidad) > 0
              AND (@modoUbic='TODOS'
                   OR (@modoUbic='LOCALES' AND P.IDLocal IN (2,3))
                   OR (@modoUbic='ID' AND P.IDLocal=@idLoc));
            """;
        var rows = (await cn.QueryAsync<RemitoRow>(new CommandDefinition(
            sql, new { modoUbic, idLoc }, commandTimeout: 60, cancellationToken: ct))).ToList();

        if (rows.Count == 0) return res;

        foreach (var grupo in rows.GroupBy(r => new { r.IdLocal, r.Local }))
        {
            var pedidoIds = grupo.Select(r => r.PedidoId).Distinct().ToArray();
            // Consolida por artículo (varios pedidos del mismo local suman). Insumos = sin variante (color/talle vacíos).
            var items = grupo.GroupBy(r => r.ArtCod)
                .Select(g => new MarketWeb.Shared.Dragonfish.DragonRemitoItemDto
                {
                    Articulo = g.Key,
                    Color = "",
                    Talle = "",
                    Cantidad = g.Sum(x => x.Cant)
                }).ToList();

            var r = new RemitoLocalResultado
            {
                Local = grupo.Key.Local,
                Pedidos = pedidoIds.Length,
                Articulos = items.Count,
                Cantidad = items.Sum(i => i.Cantidad)
            };

            var dragonReq = new MarketWeb.Shared.Dragonfish.DragonRemitoRequest
            {
                Local = (grupo.Key.Local ?? "").Trim().ToUpperInvariant(),
                Motivo = "13",   // Insumos
                Items = items
            };
            var dr = await _dragon.CrearRemitoAsync(dragonReq, ct);

            if (dr.Ok)
            {
                r.Ok = true;
                r.Comprobante = !string.IsNullOrWhiteSpace(dr.Codigo) ? dr.Codigo!
                    : (dr.Numero is { } num ? num.ToString() : "");
                var audit = $"Remito insumos generado | {usuario} | {DateTime.Now}";
                await cn.ExecuteAsync(new CommandDefinition(
                    "UPDATE PedidosInsumos SET FechaEnviado = GETDATE(), Auditoria = @audit WHERE ID IN @ids AND FechaEnviado IS NULL AND Eliminado = 0;",
                    new { ids = pedidoIds, audit }, cancellationToken: ct));
            }
            else
            {
                r.Ok = false;
                r.Error = !string.IsNullOrWhiteSpace(dr.Error) ? dr.Error!
                    : $"HTTP {dr.HttpStatus}: {(dr.Respuesta ?? "").Trim()}";
            }
            res.Locales.Add(r);
        }
        return res;
    }

    public async Task<bool> EliminarPedidoAsync(int idPedido, string usuario, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        // El local solo puede borrar mientras DEPÓSITO no lo imprimió/envió.
        var cerrado = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM PedidosInsumos WHERE ID = @id AND (FechaImpresion IS NOT NULL OR FechaEnviado IS NOT NULL) AND Eliminado = 0;",
            new { id = idPedido }, cancellationToken: ct));
        if (cerrado is not null) return false;

        var audit = $"Pedido eliminado | {usuario} | {DateTime.Now}";
        const string sql = """
            UPDATE PedidosInsumosDetalle SET Eliminado = 1, Auditoria = @audit WHERE IDPedido = @id AND Eliminado = 0;
            UPDATE PedidosInsumos SET Eliminado = 1, Auditoria = @audit WHERE ID = @id;
            """;
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id = idPedido, audit }, cancellationToken: ct));
        return true;
    }
}
