-- ============================================================================
-- Telas - Normalizacion de mayusculas / ortografia en TelasMateriales
-- ----------------------------------------------------------------------------
-- Deja los nombres de materiales en un formato canonico (Title Case),
-- corrige typos y agrega tildes. Solo UPDATE de Nombre + Auditoria (baja logica
-- intacta). Idempotente: matchea por el nombre actual, asi que correrlo dos
-- veces no vuelve a tocar nada.
--
-- Tildes escritas con NCHAR() para no depender de la codificacion del archivo:
--   NCHAR(233) = e con tilde (e-aguda) ,  NCHAR(237) = i con tilde (i-aguda)
-- ============================================================================
SET NOCOUNT ON;

DECLARE @aud nvarchar(200) =
    N'Normalizacion de nombres | WEB | '
    + CONVERT(varchar(10), GETDATE(), 103) + N' ' + CONVERT(varchar(8), GETDATE(), 108);

-- Mayusculas / Title Case
UPDATE dbo.TelasMateriales SET Nombre = N'Brodery Ada 2',        Auditoria = @aud WHERE Nombre = N'Brodery ada 2';
UPDATE dbo.TelasMateriales SET Nombre = N'Darlon Liso',          Auditoria = @aud WHERE Nombre = N'Darlon liso';
UPDATE dbo.TelasMateriales SET Nombre = N'Darlon Ribb',          Auditoria = @aud WHERE Nombre = N'Darlon ribb';
UPDATE dbo.TelasMateriales SET Nombre = N'Hawaii XL',            Auditoria = @aud WHERE Nombre = N'Hawaii xl';
UPDATE dbo.TelasMateriales SET Nombre = N'Modal Soft Estampado', Auditoria = @aud WHERE Nombre = N'Modal soft estampado';
UPDATE dbo.TelasMateriales SET Nombre = N'Morley Diesel',        Auditoria = @aud WHERE Nombre = N'Morley diesel';
UPDATE dbo.TelasMateriales SET Nombre = N'Ribb c/Lycra',         Auditoria = @aud WHERE Nombre = N'Ribb c/lycra';
UPDATE dbo.TelasMateriales SET Nombre = N'Ribb Soft Ada',        Auditoria = @aud WHERE Nombre = N'Ribb soft ada';

-- Ortografia: "Hawai" -> "Hawaii"
UPDATE dbo.TelasMateriales SET Nombre = N'Hawaii',               Auditoria = @aud WHERE Nombre = N'Hawai';

-- Ortografia: "disel" -> "Diesel"
UPDATE dbo.TelasMateriales SET Nombre = N'Morley Diesel con Piel', Auditoria = @aud WHERE Nombre = N'Morley disel con piel';

-- Tildes
UPDATE dbo.TelasMateriales SET Nombre = N'Remer' + NCHAR(237) + N'a Soft Brush', Auditoria = @aud WHERE Nombre = N'Remeria Soft Brush';
UPDATE dbo.TelasMateriales SET Nombre = N'T'     + NCHAR(233) + N'rmico',        Auditoria = @aud WHERE Nombre = N'Termico';

-- Control: lista final de materiales
SELECT Id, Codigo, Nombre FROM dbo.TelasMateriales WHERE Eliminado = 0 ORDER BY Nombre;
