-- Carga inicial de grupos de articulos unificados (Reposicion) - de "Articulos a unificar.xlsx".
-- Idempotente: no re-inserta un grupo si ya existe uno con el mismo Nombre. Correr en MARKET.
SET NOCOUNT ON;
IF OBJECT_ID('dbo.RepoGruposUnificados','U') IS NULL
CREATE TABLE dbo.RepoGruposUnificados(Id INT IDENTITY(1,1) PRIMARY KEY, Nombre NVARCHAR(150) NULL, Eliminado BIT NOT NULL CONSTRAINT DF_RepoGrupUnif_Elim DEFAULT(0), Auditoria NVARCHAR(300) NULL);
IF OBJECT_ID('dbo.RepoGruposUnificadosDet','U') IS NULL
CREATE TABLE dbo.RepoGruposUnificadosDet(Id INT IDENTITY(1,1) PRIMARY KEY, IdGrupo INT NOT NULL, ARTCOD NVARCHAR(50) NOT NULL, Eliminado BIT NOT NULL CONSTRAINT DF_RepoGrupUnifDet_Elim DEFAULT(0), Auditoria NVARCHAR(300) NULL);
DECLARE @g INT;

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LH024.266 … LH024.281' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LH024.266 … LH024.281', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LH024.266', 0, N'Seed Excel'),
    (@g, N'LH024.267', 0, N'Seed Excel'),
    (@g, N'LH024.268', 0, N'Seed Excel'),
    (@g, N'LH024.278', 0, N'Seed Excel'),
    (@g, N'LH024.279', 0, N'Seed Excel'),
    (@g, N'LH024.280', 0, N'Seed Excel'),
    (@g, N'LH024.281', 0, N'Seed Excel');
END

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LM024.270 … LM024.292' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LM024.270 … LM024.292', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LM024.270', 0, N'Seed Excel'),
    (@g, N'LM024.271', 0, N'Seed Excel'),
    (@g, N'LM024.273', 0, N'Seed Excel'),
    (@g, N'LM024.274', 0, N'Seed Excel'),
    (@g, N'LM024.275', 0, N'Seed Excel'),
    (@g, N'LM024.276', 0, N'Seed Excel'),
    (@g, N'LM024.277', 0, N'Seed Excel'),
    (@g, N'LM024.278', 0, N'Seed Excel'),
    (@g, N'LM024.279', 0, N'Seed Excel'),
    (@g, N'LM024.280', 0, N'Seed Excel'),
    (@g, N'LM024.282', 0, N'Seed Excel'),
    (@g, N'LM024.283', 0, N'Seed Excel'),
    (@g, N'LM024.284', 0, N'Seed Excel'),
    (@g, N'LM024.285', 0, N'Seed Excel'),
    (@g, N'LM024.286', 0, N'Seed Excel'),
    (@g, N'LM024.287', 0, N'Seed Excel'),
    (@g, N'LM024.288', 0, N'Seed Excel'),
    (@g, N'LM024.289', 0, N'Seed Excel'),
    (@g, N'LM024.290', 0, N'Seed Excel'),
    (@g, N'LM024.291', 0, N'Seed Excel'),
    (@g, N'LM024.292', 0, N'Seed Excel');
END

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LNE024.288 … LNE024.304' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LNE024.288 … LNE024.304', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LNE024.288', 0, N'Seed Excel'),
    (@g, N'LNE024.289', 0, N'Seed Excel'),
    (@g, N'LNE024.290', 0, N'Seed Excel'),
    (@g, N'LNE024.291', 0, N'Seed Excel'),
    (@g, N'LNE024.292', 0, N'Seed Excel'),
    (@g, N'LNE024.300', 0, N'Seed Excel'),
    (@g, N'LNE024.301', 0, N'Seed Excel'),
    (@g, N'LNE024.302', 0, N'Seed Excel'),
    (@g, N'LNE024.303', 0, N'Seed Excel'),
    (@g, N'LNE024.304', 0, N'Seed Excel');
END

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LNU024.293 … LNU024.310' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LNU024.293 … LNU024.310', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LNU024.293', 0, N'Seed Excel'),
    (@g, N'LNU024.294', 0, N'Seed Excel'),
    (@g, N'LNU024.295', 0, N'Seed Excel'),
    (@g, N'LNU024.296', 0, N'Seed Excel'),
    (@g, N'LNU024.297', 0, N'Seed Excel'),
    (@g, N'LNU024.298', 0, N'Seed Excel'),
    (@g, N'LNU024.305', 0, N'Seed Excel'),
    (@g, N'LNU024.306', 0, N'Seed Excel'),
    (@g, N'LNU024.307', 0, N'Seed Excel'),
    (@g, N'LNU024.308', 0, N'Seed Excel'),
    (@g, N'LNU024.309', 0, N'Seed Excel'),
    (@g, N'LNU024.310', 0, N'Seed Excel');
END

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LM024.010 … LM024.183' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LM024.010 … LM024.183', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LM024.010', 0, N'Seed Excel'),
    (@g, N'LM024.053', 0, N'Seed Excel'),
    (@g, N'LM024.072', 0, N'Seed Excel'),
    (@g, N'LM024.073', 0, N'Seed Excel'),
    (@g, N'LM024.074', 0, N'Seed Excel'),
    (@g, N'LM024.075', 0, N'Seed Excel'),
    (@g, N'LM024.076', 0, N'Seed Excel'),
    (@g, N'LM024.077', 0, N'Seed Excel'),
    (@g, N'LM024.078', 0, N'Seed Excel'),
    (@g, N'LM024.079', 0, N'Seed Excel'),
    (@g, N'LM024.080', 0, N'Seed Excel'),
    (@g, N'LM024.183', 0, N'Seed Excel');
END

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LNA024.057 … LNA024.191' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LNA024.057 … LNA024.191', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LNA024.057', 0, N'Seed Excel'),
    (@g, N'LNA024.085', 0, N'Seed Excel'),
    (@g, N'LNA024.086', 0, N'Seed Excel'),
    (@g, N'LNA024.087', 0, N'Seed Excel'),
    (@g, N'LNA024.088', 0, N'Seed Excel'),
    (@g, N'LNA024.089', 0, N'Seed Excel'),
    (@g, N'LNA024.090', 0, N'Seed Excel'),
    (@g, N'LNA024.091', 0, N'Seed Excel'),
    (@g, N'LNA024.186', 0, N'Seed Excel'),
    (@g, N'LNA024.187', 0, N'Seed Excel'),
    (@g, N'LNA024.188', 0, N'Seed Excel'),
    (@g, N'LNA024.189', 0, N'Seed Excel'),
    (@g, N'LNA024.190', 0, N'Seed Excel'),
    (@g, N'LNA024.191', 0, N'Seed Excel');
END

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LNE014.197 … LNE024.195' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LNE014.197 … LNE024.195', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LNE014.197', 0, N'Seed Excel'),
    (@g, N'LNE024.092', 0, N'Seed Excel'),
    (@g, N'LNE024.093', 0, N'Seed Excel'),
    (@g, N'LNE024.094', 0, N'Seed Excel'),
    (@g, N'LNE024.096', 0, N'Seed Excel'),
    (@g, N'LNE024.098', 0, N'Seed Excel'),
    (@g, N'LNE024.192', 0, N'Seed Excel'),
    (@g, N'LNE024.193', 0, N'Seed Excel'),
    (@g, N'LNE024.194', 0, N'Seed Excel'),
    (@g, N'LNE024.195', 0, N'Seed Excel');
END

IF NOT EXISTS (SELECT 1 FROM dbo.RepoGruposUnificados WHERE Nombre = N'LH097.336 … LH097.403' AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (N'LH097.336 … LH097.403', 0, N'Seed Excel');
    SET @g = SCOPE_IDENTITY();
    INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES
    (@g, N'LH097.336', 0, N'Seed Excel'),
    (@g, N'LH097.337', 0, N'Seed Excel'),
    (@g, N'LH097.338', 0, N'Seed Excel'),
    (@g, N'LH097.403', 0, N'Seed Excel');
END