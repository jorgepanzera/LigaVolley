/*
===============================================================================
Liga Volley - Personas, Planteles y Oficiales
Archivo: LigaVolley_Personas_Planteles_v1.sql
Motor : Microsoft SQL Server
Versión del modelo: 1.0

PRERREQUISITO
-------------
Debe haberse ejecutado previamente el modelo base de Liga Volley que contiene:
- dbo.TEAM_ENTRY
- dbo.MATCH

INCLUYE
-------
- PERSON
- PLAYER
- COACH
- REFEREE
- PLAYER_ROLE
- COMPETITION_ROSTER
- COMPETITION_ROSTER_PLAYER
- COMPETITION_ROSTER_STAFF
- MATCH_OFFICIAL

DECISIONES DE MODELO
--------------------
1) PERSON representa a la persona física.
2) PLAYER, COACH y REFEREE son perfiles independientes de una PERSON.
3) Una PERSON puede poseer varios perfiles simultáneamente y estos pueden
   variar con el tiempo.
4) Los perfiles poseen vigencia temporal.
5) Un jugador no pertenece permanentemente a un TEAM.
6) La inscripción deportiva se realiza sobre TEAM_ENTRY mediante
   COMPETITION_ROSTER.
7) El número de camiseta, rol táctico y condición de capitán corresponden
   a la inscripción del jugador en el plantel.
8) Un plantel admite como máximo 15 jugadores activos.
9) Un plantel admite como máximo 2 técnicos activos.
10) El rol táctico del jugador NO representa su posición de rotación 1..6.
11) Los oficiales de un partido se registran mediante MATCH_OFFICIAL.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   1. PERSONAS
   ============================================================================ */
CREATE TABLE dbo.PERSON
(
    person_id          INT IDENTITY(1,1) NOT NULL,
    document_type      VARCHAR(20) NULL,
    document_number    VARCHAR(30) NULL,
    first_name         NVARCHAR(100) NOT NULL,
    last_name          NVARCHAR(100) NOT NULL,
    birth_date         DATE NULL,
    gender             CHAR(1) NULL,
    email              NVARCHAR(200) NULL,
    phone              NVARCHAR(50) NULL,
    active             BIT NOT NULL CONSTRAINT DF_PERSON_active DEFAULT (1),

    CONSTRAINT PK_PERSON PRIMARY KEY (person_id),
    CONSTRAINT CK_PERSON_gender CHECK (gender IS NULL OR gender IN ('M','F')),
    CONSTRAINT CK_PERSON_document CHECK
    (
        (document_type IS NULL AND document_number IS NULL)
        OR
        (document_type IS NOT NULL AND document_number IS NOT NULL)
    )
);
GO

CREATE UNIQUE INDEX UX_PERSON_document
ON dbo.PERSON (document_type, document_number)
WHERE document_type IS NOT NULL AND document_number IS NOT NULL;
GO

/* ============================================================================
   2. PERFILES DE LA PERSONA
   ============================================================================ */
CREATE TABLE dbo.PLAYER
(
    player_id          INT IDENTITY(1,1) NOT NULL,
    person_id          INT NOT NULL,
    valid_from         DATE NULL,
    valid_to           DATE NULL,
    active             BIT NOT NULL CONSTRAINT DF_PLAYER_active DEFAULT (1),

    CONSTRAINT PK_PLAYER PRIMARY KEY (player_id),
    CONSTRAINT FK_PLAYER_PERSON FOREIGN KEY (person_id) REFERENCES dbo.PERSON (person_id),
    CONSTRAINT UQ_PLAYER_person UNIQUE (person_id),
    CONSTRAINT CK_PLAYER_dates CHECK
    (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);
GO

CREATE TABLE dbo.COACH
(
    coach_id           INT IDENTITY(1,1) NOT NULL,
    person_id          INT NOT NULL,
    valid_from         DATE NULL,
    valid_to           DATE NULL,
    active             BIT NOT NULL CONSTRAINT DF_COACH_active DEFAULT (1),

    CONSTRAINT PK_COACH PRIMARY KEY (coach_id),
    CONSTRAINT FK_COACH_PERSON FOREIGN KEY (person_id) REFERENCES dbo.PERSON (person_id),
    CONSTRAINT UQ_COACH_person UNIQUE (person_id),
    CONSTRAINT CK_COACH_dates CHECK
    (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);
GO

CREATE TABLE dbo.REFEREE
(
    referee_id         INT IDENTITY(1,1) NOT NULL,
    person_id          INT NOT NULL,
    license_number     VARCHAR(50) NULL,
    category           NVARCHAR(50) NULL,
    valid_from         DATE NULL,
    valid_to           DATE NULL,
    active             BIT NOT NULL CONSTRAINT DF_REFEREE_active DEFAULT (1),

    CONSTRAINT PK_REFEREE PRIMARY KEY (referee_id),
    CONSTRAINT FK_REFEREE_PERSON FOREIGN KEY (person_id) REFERENCES dbo.PERSON (person_id),
    CONSTRAINT UQ_REFEREE_person UNIQUE (person_id),
    CONSTRAINT CK_REFEREE_dates CHECK
    (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);
GO

CREATE UNIQUE INDEX UX_REFEREE_license_number
ON dbo.REFEREE (license_number)
WHERE license_number IS NOT NULL;
GO

/* ============================================================================
   3. ROLES TÁCTICOS DE JUGADOR
   ============================================================================ */
CREATE TABLE dbo.PLAYER_ROLE
(
    player_role_id     INT IDENTITY(1,1) NOT NULL,
    code               VARCHAR(30) NOT NULL,
    name               NVARCHAR(100) NOT NULL,
    description        NVARCHAR(300) NULL,
    active             BIT NOT NULL CONSTRAINT DF_PLAYER_ROLE_active DEFAULT (1),

    CONSTRAINT PK_PLAYER_ROLE PRIMARY KEY (player_role_id),
    CONSTRAINT UQ_PLAYER_ROLE_code UNIQUE (code),
    CONSTRAINT UQ_PLAYER_ROLE_name UNIQUE (name)
);
GO

INSERT INTO dbo.PLAYER_ROLE (code, name, description)
VALUES
('SETTER', N'Armador', N'Jugador especializado en la distribución y armado del juego.'),
('OUTSIDE_HITTER', N'Punta / Receptor', N'Atacante de punta con participación principal en recepción.'),
('MIDDLE_BLOCKER', N'Central', N'Jugador especializado en bloqueo y ataques rápidos por el centro.'),
('OPPOSITE', N'Opuesto', N'Atacante ubicado tácticamente en diagonal al armador.'),
('LIBERO', N'Líbero', N'Especialista defensivo sujeto a las reglas específicas del líbero.');
GO

/* ============================================================================
   4. PLANTEL INSCRIPTO EN UNA COMPETENCIA
   ============================================================================ */
CREATE TABLE dbo.COMPETITION_ROSTER
(
    competition_roster_id INT IDENTITY(1,1) NOT NULL,
    competition_id        INT NOT NULL,
    team_entry_id         INT NOT NULL,
    registration_date     DATE NULL,
    approved_date         DATE NULL,
    status                VARCHAR(20) NOT NULL CONSTRAINT DF_COMPETITION_ROSTER_status DEFAULT ('DRAFT'),

    CONSTRAINT PK_COMPETITION_ROSTER PRIMARY KEY (competition_roster_id),
    CONSTRAINT FK_COMPETITION_ROSTER_TEAM_ENTRY
        FOREIGN KEY (team_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),
    CONSTRAINT UQ_COMPETITION_ROSTER_team_entry UNIQUE (team_entry_id),
    CONSTRAINT UQ_COMPETITION_ROSTER_id_comp UNIQUE (competition_roster_id, competition_id),
    CONSTRAINT CK_COMPETITION_ROSTER_status CHECK
        (status IN ('DRAFT','SUBMITTED','APPROVED','CLOSED','CANCELLED')),
    CONSTRAINT CK_COMPETITION_ROSTER_dates CHECK
        (approved_date IS NULL OR registration_date IS NULL OR approved_date >= registration_date)
);
GO

/* ============================================================================
   5. JUGADORES DEL PLANTEL
   ============================================================================ */
CREATE TABLE dbo.COMPETITION_ROSTER_PLAYER
(
    competition_roster_player_id INT IDENTITY(1,1) NOT NULL,
    competition_roster_id        INT NOT NULL,
    player_id                    INT NOT NULL,
    primary_player_role_id       INT NULL,
    valid_from                   DATE NULL,
    valid_to                     DATE NULL,
    status                       VARCHAR(20) NOT NULL CONSTRAINT DF_COMPETITION_ROSTER_PLAYER_status DEFAULT ('ACTIVE'),

    CONSTRAINT PK_COMPETITION_ROSTER_PLAYER PRIMARY KEY (competition_roster_player_id),
    CONSTRAINT FK_COMPETITION_ROSTER_PLAYER_ROSTER
        FOREIGN KEY (competition_roster_id) REFERENCES dbo.COMPETITION_ROSTER (competition_roster_id),
    CONSTRAINT FK_COMPETITION_ROSTER_PLAYER_PLAYER
        FOREIGN KEY (player_id) REFERENCES dbo.PLAYER (player_id),
    CONSTRAINT FK_COMPETITION_ROSTER_PLAYER_ROLE
        FOREIGN KEY (primary_player_role_id) REFERENCES dbo.PLAYER_ROLE (player_role_id),
    CONSTRAINT UQ_COMPETITION_ROSTER_PLAYER_player UNIQUE (competition_roster_id, player_id),
    CONSTRAINT CK_COMPETITION_ROSTER_PLAYER_status CHECK
        (status IN ('ACTIVE','INACTIVE','SUSPENDED','WITHDRAWN')),
    CONSTRAINT CK_COMPETITION_ROSTER_PLAYER_dates CHECK
        (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);
GO

/* ============================================================================
   6. TÉCNICOS DEL PLANTEL
   ============================================================================ */
CREATE TABLE dbo.COMPETITION_ROSTER_STAFF
(
    competition_roster_staff_id INT IDENTITY(1,1) NOT NULL,
    competition_roster_id       INT NOT NULL,
    coach_id                    INT NOT NULL,
    staff_role                  VARCHAR(30) NOT NULL,
    valid_from                  DATE NULL,
    valid_to                    DATE NULL,
    status                      VARCHAR(20) NOT NULL CONSTRAINT DF_COMPETITION_ROSTER_STAFF_status DEFAULT ('ACTIVE'),

    CONSTRAINT PK_COMPETITION_ROSTER_STAFF PRIMARY KEY (competition_roster_staff_id),
    CONSTRAINT FK_COMPETITION_ROSTER_STAFF_ROSTER
        FOREIGN KEY (competition_roster_id) REFERENCES dbo.COMPETITION_ROSTER (competition_roster_id),
    CONSTRAINT FK_COMPETITION_ROSTER_STAFF_COACH
        FOREIGN KEY (coach_id) REFERENCES dbo.COACH (coach_id),
    CONSTRAINT UQ_COMPETITION_ROSTER_STAFF_coach UNIQUE (competition_roster_id, coach_id),
    CONSTRAINT CK_COMPETITION_ROSTER_STAFF_role CHECK
        (staff_role IN ('HEAD_COACH','ASSISTANT_COACH')),
    CONSTRAINT CK_COMPETITION_ROSTER_STAFF_status CHECK
        (status IN ('ACTIVE','INACTIVE','SUSPENDED','WITHDRAWN')),
    CONSTRAINT CK_COMPETITION_ROSTER_STAFF_dates CHECK
        (valid_to IS NULL OR valid_from IS NULL OR valid_to >= valid_from)
);
GO

/* ============================================================================
   7. OFICIALES / JUECES DESIGNADOS AL PARTIDO
   ============================================================================ */
CREATE TABLE dbo.MATCH_OFFICIAL
(
    match_official_id    INT IDENTITY(1,1) NOT NULL,
    match_id             INT NOT NULL,
    referee_id           INT NOT NULL,
    official_role        VARCHAR(30) NOT NULL,
    status               VARCHAR(20) NOT NULL CONSTRAINT DF_MATCH_OFFICIAL_status DEFAULT ('ASSIGNED'),

    CONSTRAINT PK_MATCH_OFFICIAL PRIMARY KEY (match_official_id),
    CONSTRAINT FK_MATCH_OFFICIAL_MATCH FOREIGN KEY (match_id) REFERENCES dbo.MATCH (match_id),
    CONSTRAINT FK_MATCH_OFFICIAL_REFEREE FOREIGN KEY (referee_id) REFERENCES dbo.REFEREE (referee_id),
    CONSTRAINT UQ_MATCH_OFFICIAL_referee UNIQUE (match_id, referee_id),
    CONSTRAINT UQ_MATCH_OFFICIAL_role UNIQUE (match_id, official_role),
    CONSTRAINT CK_MATCH_OFFICIAL_role CHECK
        (official_role IN ('FIRST_REFEREE','SECOND_REFEREE','SCORER')),
    CONSTRAINT CK_MATCH_OFFICIAL_status CHECK
        (status IN ('ASSIGNED','CONFIRMED','REPLACED','ABSENT'))
);
GO

/* ============================================================================
   8. REGLAS DE CANTIDAD DEL PLANTEL
   ============================================================================ */
CREATE TRIGGER dbo.TR_COMPETITION_ROSTER_PLAYER_MAX_15
ON dbo.COMPETITION_ROSTER_PLAYER
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.COMPETITION_ROSTER_PLAYER p
        INNER JOIN (SELECT DISTINCT competition_roster_id FROM inserted) i
            ON i.competition_roster_id = p.competition_roster_id
        WHERE p.status = 'ACTIVE'
        GROUP BY p.competition_roster_id
        HAVING COUNT(*) > 15
    )
    BEGIN
        THROW 50001, 'Un plantel no puede tener más de 15 jugadores activos.', 1;
    END;
END;
GO

CREATE TRIGGER dbo.TR_COMPETITION_ROSTER_STAFF_MAX_2
ON dbo.COMPETITION_ROSTER_STAFF
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.COMPETITION_ROSTER_STAFF s
        INNER JOIN (SELECT DISTINCT competition_roster_id FROM inserted) i
            ON i.competition_roster_id = s.competition_roster_id
        WHERE s.status = 'ACTIVE'
        GROUP BY s.competition_roster_id
        HAVING COUNT(*) > 2
    )
    BEGIN
        THROW 50002, 'Un plantel no puede tener más de 2 técnicos activos.', 1;
    END;
END;
GO
