-- ============================================================================
-- Telas - Depósito 955 = Marcelo
-- ----------------------------------------------------------------------------
-- Pone el nombre "Marcelo" a TODOS los depósitos con código 955.
-- Idempotente: correrlo de nuevo deja el mismo valor. Baja lógica intacta.
-- ============================================================================
SET NOCOUNT ON;

UPDATE dbo.TelasDepositos
SET    Nombre    = N'Marcelo',
       Auditoria = N'Nombre depósito 955 = Marcelo | WEB | '
                   + CONVERT(varchar(10), GETDATE(), 103) + N' ' + CONVERT(varchar(8), GETDATE(), 108)
WHERE  Codigo = N'955';

-- Control
SELECT Id, Codigo, Nombre FROM dbo.TelasDepositos WHERE Codigo = N'955';
