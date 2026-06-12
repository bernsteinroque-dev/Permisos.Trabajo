-- ============================================================
-- PTS Synthon - Script de creación de base de datos
-- SQL Server 2019+  /  Synthon Argentina S.A.
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PTS_Synthon')
BEGIN
    CREATE DATABASE PTS_Synthon
        COLLATE Modern_Spanish_CI_AS;
    PRINT 'Base de datos PTS_Synthon creada.';
END
ELSE
    PRINT 'Base de datos PTS_Synthon ya existe.';
GO

USE PTS_Synthon;
GO

-- ============================================================
-- Sequences
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'seq_NumeroPermiso' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE SEQUENCE dbo.seq_NumeroPermiso
        AS INT
        START WITH 7610
        INCREMENT BY 1
        NO CYCLE;
    PRINT 'Sequence seq_NumeroPermiso creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'seq_NumeroProveedor' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE SEQUENCE dbo.seq_NumeroProveedor
        AS INT
        START WITH 1
        INCREMENT BY 1
        NO CYCLE;
    PRINT 'Sequence seq_NumeroProveedor creada.';
END
GO

-- ============================================================
-- Tabla Proveedores
-- ============================================================
IF OBJECT_ID('dbo.Proveedores', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Proveedores (
        Id                  INT IDENTITY(1,1)   NOT NULL,
        NumeroProveedor     INT                 NOT NULL DEFAULT (NEXT VALUE FOR dbo.seq_NumeroProveedor),
        RazonSocial         NVARCHAR(200)       NOT NULL,
        CUIT                NVARCHAR(20)        NULL,
        Rubro               NVARCHAR(100)       NULL,
        Estado              NVARCHAR(20)        NULL DEFAULT 'Activo',
        Habilitacion        NVARCHAR(100)       NULL,
        Contacto            NVARCHAR(100)       NULL,
        Telefono            NVARCHAR(50)        NULL,
        Email               NVARCHAR(200)       NULL,
        Cargo               NVARCHAR(100)       NULL,
        VencART             NVARCHAR(20)        NULL,
        VencRC              NVARCHAR(20)        NULL,
        VencHabMunicipal    NVARCHAR(20)        NULL,
        VencAFIP            NVARCHAR(20)        NULL,
        VencSegHigiene      NVARCHAR(20)        NULL,
        VencOtros           NVARCHAR(20)        NULL,
        ObsDocumentacion    NVARCHAR(MAX)       NULL,
        Observaciones       NVARCHAR(MAX)       NULL,
        FechaCreacion       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
        CreadoPor           NVARCHAR(100)       NOT NULL,
        CONSTRAINT PK_Proveedores PRIMARY KEY CLUSTERED (Id)
    );
    PRINT 'Tabla Proveedores creada.';
END
GO

-- ============================================================
-- Tabla Permisos
-- ============================================================
IF OBJECT_ID('dbo.Permisos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permisos (
        Id                  INT IDENTITY(1,1)   NOT NULL,
        NumeroPermiso       INT                 NOT NULL DEFAULT (NEXT VALUE FOR dbo.seq_NumeroPermiso),
        Tipo                NVARCHAR(20)        NOT NULL,
        Estado              NVARCHAR(20)        NOT NULL DEFAULT 'pending',
        FechaCreacion       DATETIME2           NOT NULL DEFAULT GETUTCDATE(),
        FechaModificacion   DATETIME2           NULL,
        CreadoPor           NVARCHAR(100)       NOT NULL,
        ModificadoPor       NVARCHAR(100)       NULL,
        SupervisorNombre    NVARCHAR(200)       NULL,
        SupervisorComentario NVARCHAR(MAX)      NULL,
        EmpresaPlanta       NVARCHAR(100)       NULL DEFAULT 'Synthon S.A.',
        ProveedorId         INT                 NULL,
        ProveedorNombre     NVARCHAR(200)       NULL,
        ProveedorEmail      NVARCHAR(200)       NULL,
        Empresa             NVARCHAR(200)       NULL,
        Fecha               NVARCHAR(20)        NULL,
        VigenciaDesde       NVARCHAR(20)        NULL,
        VigenciaHasta       NVARCHAR(20)        NULL,
        Orden               NVARCHAR(100)       NULL,
        Planta              NVARCHAR(100)       NULL,
        Equipo              NVARCHAR(200)       NULL,
        Descripcion         NVARCHAR(MAX)       NULL,
        RealizaElTrabajo    NVARCHAR(200)       NULL,
        VerificacionesJson  NVARCHAR(MAX)       NULL,
        PeligrosJson        NVARCHAR(MAX)       NULL,
        EppGeneralJson      NVARCHAR(MAX)       NULL,
        CheckListJson       NVARCHAR(MAX)       NULL,
        HerramientaAntichispa NVARCHAR(10)      NULL,
        IluminacionEspecial NVARCHAR(10)        NULL,
        GeneraResiduos      NVARCHAR(10)        NULL,
        Consigno            NVARCHAR(10)        NULL,
        ConsignoCual        NVARCHAR(200)       NULL,
        ConsignoQuien       NVARCHAR(200)       NULL,
        Observaciones       NVARCHAR(MAX)       NULL,
        Emisor              NVARCHAR(200)       NULL,
        Receptor            NVARCHAR(200)       NULL,
        Ejecutante1         NVARCHAR(200)       NULL,
        Ejecutante2         NVARCHAR(200)       NULL,
        Ejecutante3         NVARCHAR(200)       NULL,
        Ejecutante4         NVARCHAR(200)       NULL,
        TerminacionTrabajo  NVARCHAR(200)       NULL,
        LimpiezaSector      NVARCHAR(10)        NULL,
        TermFirma           NVARCHAR(200)       NULL,
        RecepcionEmisor     NVARCHAR(200)       NULL,
        RecepcionFirma      NVARCHAR(200)       NULL,
        EmailNotificado     BIT                 NOT NULL DEFAULT 0,
        CONSTRAINT PK_Permisos PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Permisos_Proveedores FOREIGN KEY (ProveedorId)
            REFERENCES dbo.Proveedores (Id)
            ON DELETE SET NULL
    );
    PRINT 'Tabla Permisos creada.';
END
GO

-- ============================================================
-- Índices
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permisos_NumeroPermiso')
    CREATE NONCLUSTERED INDEX IX_Permisos_NumeroPermiso ON dbo.Permisos (NumeroPermiso);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permisos_Estado')
    CREATE NONCLUSTERED INDEX IX_Permisos_Estado ON dbo.Permisos (Estado);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permisos_CreadoPor')
    CREATE NONCLUSTERED INDEX IX_Permisos_CreadoPor ON dbo.Permisos (CreadoPor);
GO

-- ============================================================
-- Grupos de Active Directory (crear manualmente en AD)
-- Los siguientes grupos deben existir en el dominio SYNTHON:
--   PTS_Admins         -> Acceso completo
--   PTS_Supervisores   -> Aprobar/rechazar permisos
--   PTS_Proveedores    -> Crear y ver propios permisos
--   PTS_Lectura        -> Solo lectura
-- ============================================================

PRINT 'Script completado exitosamente.';
GO
