/*
===============================================================================
Liga Volley - Acta Electrónica del Partido
Archivo: LigaVolley_Acta_Electronica_Match_v1.sql
Motor : Microsoft SQL Server
Versión del modelo: 1.0

PRERREQUISITOS
--------------
Debe haberse ejecutado previamente:
1) Modelo base de entidades y formatos de competición, que contiene:
   - dbo.MATCH
   - dbo.MATCH_SET
   - dbo.TEAM_ENTRY
2) Modelo de Personas, Planteles y Oficiales, que contiene:
   - dbo.COMPETITION_ROSTER
   - dbo.COMPETITION_ROSTER_PLAYER
   - dbo.COMPETITION_ROSTER_STAFF
   - dbo.MATCH_OFFICIAL

PRINCIPIOS CERRADOS PARA EL ACTA
--------------------------------
1) MATCH representa el partido competitivo; MATCH_SHEET representa su acta.
2) Se conservan PK técnicas INT IDENTITY en el repositorio central.
3) Todo registro del acta que pueda originarse offline posee un UUID estable.
4) MATCH_EVENT es la verdad cronológica/auditable y permite sincronización
   idempotente por event_uuid.
5) Un partido tiene una única sesión activa de captura del acta.
6) La alineación inicial de cada equipo en cada set contiene exactamente las
   posiciones reglamentarias P1..P6.
7) No existe MATCH_ROTATION_PLAYER. La rotación se deriva mediante
   rotation_offset (0..5) y el cambio de derecho al saque.
8) Una sustitución reglamentaria normal se registra en MATCH_SUBSTITUTION.
9) El líbero NO se modela como una sustitución normal. Puede haber hasta dos
   líberos declarados por equipo en el partido.
10) El estado físico de cancha se obtiene de:
       alineación inicial P1..P6
       + sustituciones normales vigentes
       + rotation_offset
       + reemplazo de líbero activo
       = seis jugadores físicamente en cancha.
11) MATCH_SET_STATE es una proyección/cache operativa. Puede reconstruirse
    desde MATCH_EVENT + datos estructurados y NO es la fuente de verdad.
12) Las correcciones no borran eventos: generan un evento CORRECTION que anula
    lógicamente un evento anterior.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   0. EXTENSIÓN DE MATCH_SET PARA OPERACIÓN EN VIVO / OFFLINE
   ============================================================================ */

IF COL_LENGTH('dbo.MATCH_SET', 'set_uuid') IS NULL
BEGIN
    ALTER TABLE dbo.MATCH_SET
        ADD set_uuid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_MATCH_SET_set_uuid DEFAULT (NEWID()) WITH VALUES;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.MATCH_SET')
      AND name = 'UX_MATCH_SET_set_uuid'
)
BEGIN
    CREATE UNIQUE INDEX UX_MATCH_SET_set_uuid
        ON dbo.MATCH_SET (set_uuid);
END;
GO

IF COL_LENGTH('dbo.MATCH_SET', 'status') IS NULL
BEGIN
    ALTER TABLE dbo.MATCH_SET
        ADD status VARCHAR(20) NOT NULL
            CONSTRAINT DF_MATCH_SET_status DEFAULT ('PENDING') WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.MATCH_SET', 'started_at') IS NULL
BEGIN
    ALTER TABLE dbo.MATCH_SET ADD started_at DATETIME2(3) NULL;
END;
GO

IF COL_LENGTH('dbo.MATCH_SET', 'ended_at') IS NULL
BEGIN
    ALTER TABLE dbo.MATCH_SET ADD ended_at DATETIME2(3) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_MATCH_SET_status'
      AND parent_object_id = OBJECT_ID('dbo.MATCH_SET')
)
BEGIN
    ALTER TABLE dbo.MATCH_SET WITH CHECK
        ADD CONSTRAINT CK_MATCH_SET_status
        CHECK (status IN ('PENDING','IN_PROGRESS','FINISHED','CANCELLED'));
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_MATCH_SET_dates'
      AND parent_object_id = OBJECT_ID('dbo.MATCH_SET')
)
BEGIN
    ALTER TABLE dbo.MATCH_SET WITH CHECK
        ADD CONSTRAINT CK_MATCH_SET_dates
        CHECK (ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at);
END;
GO

/* ============================================================================
   1. CABECERA DEL ACTA
   ============================================================================ */
CREATE TABLE dbo.MATCH_SHEET
(
    match_sheet_id          INT IDENTITY(1,1) NOT NULL,
    sheet_uuid              UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_SHEET_uuid DEFAULT (NEWID()),
    match_id                INT NOT NULL,
    status                  VARCHAR(20) NOT NULL
        CONSTRAINT DF_MATCH_SHEET_status DEFAULT ('OPEN'),
    opened_at               DATETIME2(3) NOT NULL
        CONSTRAINT DF_MATCH_SHEET_opened_at DEFAULT (SYSUTCDATETIME()),
    started_at              DATETIME2(3) NULL,
    ended_at                DATETIME2(3) NULL,
    home_sets               TINYINT NOT NULL
        CONSTRAINT DF_MATCH_SHEET_home_sets DEFAULT (0),
    away_sets               TINYINT NOT NULL
        CONSTRAINT DF_MATCH_SHEET_away_sets DEFAULT (0),
    winning_team_entry_id   INT NULL,
    created_at              DATETIME2(3) NOT NULL
        CONSTRAINT DF_MATCH_SHEET_created_at DEFAULT (SYSUTCDATETIME()),
    updated_at              DATETIME2(3) NOT NULL
        CONSTRAINT DF_MATCH_SHEET_updated_at DEFAULT (SYSUTCDATETIME()),
    row_version             ROWVERSION NOT NULL,

    CONSTRAINT PK_MATCH_SHEET PRIMARY KEY (match_sheet_id),
    CONSTRAINT UQ_MATCH_SHEET_uuid UNIQUE (sheet_uuid),
    CONSTRAINT UQ_MATCH_SHEET_match UNIQUE (match_id),
    CONSTRAINT FK_MATCH_SHEET_MATCH
        FOREIGN KEY (match_id) REFERENCES dbo.MATCH (match_id),
    CONSTRAINT FK_MATCH_SHEET_WINNER
        FOREIGN KEY (winning_team_entry_id) REFERENCES dbo.TEAM_ENTRY (team_entry_id),
    CONSTRAINT CK_MATCH_SHEET_status
        CHECK (status IN ('OPEN','IN_PROGRESS','SUSPENDED','CLOSED','CANCELLED')),
    CONSTRAINT CK_MATCH_SHEET_sets
        CHECK
        (
            home_sets BETWEEN 0 AND 3
            AND away_sets BETWEEN 0 AND 3
            AND NOT (home_sets = 3 AND away_sets = 3)
        ),
    CONSTRAINT CK_MATCH_SHEET_dates
        CHECK
        (
            (started_at IS NULL OR started_at >= opened_at)
            AND (ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at)
        )
);
GO

/* ============================================================================
   2. EQUIPOS Y SNAPSHOT DE PARTICIPANTES DEL ACTA
   ============================================================================ */
CREATE TABLE dbo.MATCH_TEAM
(
    match_team_id           INT IDENTITY(1,1) NOT NULL,
    match_team_uuid         UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_TEAM_uuid DEFAULT (NEWID()),
    match_sheet_id          INT NOT NULL,
    team_entry_id           INT NOT NULL,
    competition_roster_id   INT NOT NULL,
    side                    VARCHAR(4) NOT NULL,

    CONSTRAINT PK_MATCH_TEAM PRIMARY KEY (match_team_id),
    CONSTRAINT UQ_MATCH_TEAM_uuid UNIQUE (match_team_uuid),
    CONSTRAINT UQ_MATCH_TEAM_side UNIQUE (match_sheet_id, side),
    CONSTRAINT UQ_MATCH_TEAM_entry UNIQUE (match_sheet_id, team_entry_id),
    CONSTRAINT FK_MATCH_TEAM_SHEET
        FOREIGN KEY (match_sheet_id) REFERENCES dbo.MATCH_SHEET (match_sheet_id),
    CONSTRAINT FK_MATCH_TEAM_TEAM_ENTRY
        FOREIGN KEY (team_entry_id) REFERENCES dbo.TEAM_ENTRY (team_entry_id),
    CONSTRAINT FK_MATCH_TEAM_ROSTER
        FOREIGN KEY (competition_roster_id) REFERENCES dbo.COMPETITION_ROSTER (competition_roster_id),
    CONSTRAINT CK_MATCH_TEAM_side CHECK (side IN ('HOME','AWAY'))
);
GO

CREATE TABLE dbo.MATCH_PLAYER
(
    match_player_id                 INT IDENTITY(1,1) NOT NULL,
    match_player_uuid               UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_PLAYER_uuid DEFAULT (NEWID()),
    match_team_id                   INT NOT NULL,
    competition_roster_player_id    INT NOT NULL,
    jersey_number                   TINYINT NOT NULL,
    is_match_captain                BIT NOT NULL
        CONSTRAINT DF_MATCH_PLAYER_captain DEFAULT (0),
    status                          VARCHAR(20) NOT NULL
        CONSTRAINT DF_MATCH_PLAYER_status DEFAULT ('AVAILABLE'),

    CONSTRAINT PK_MATCH_PLAYER PRIMARY KEY (match_player_id),
    CONSTRAINT UQ_MATCH_PLAYER_uuid UNIQUE (match_player_uuid),
    CONSTRAINT UQ_MATCH_PLAYER_roster_player
        UNIQUE (match_team_id, competition_roster_player_id),
    CONSTRAINT UQ_MATCH_PLAYER_jersey UNIQUE (match_team_id, jersey_number),
    CONSTRAINT FK_MATCH_PLAYER_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT FK_MATCH_PLAYER_ROSTER_PLAYER
        FOREIGN KEY (competition_roster_player_id)
        REFERENCES dbo.COMPETITION_ROSTER_PLAYER (competition_roster_player_id),
    CONSTRAINT CK_MATCH_PLAYER_jersey CHECK (jersey_number BETWEEN 1 AND 99),
    CONSTRAINT CK_MATCH_PLAYER_status
        CHECK (status IN ('AVAILABLE','ABSENT','DISQUALIFIED','INJURED'))
);
GO

CREATE UNIQUE INDEX UX_MATCH_PLAYER_captain
    ON dbo.MATCH_PLAYER (match_team_id)
    WHERE is_match_captain = 1;
GO

CREATE TABLE dbo.MATCH_TEAM_STAFF
(
    match_team_staff_id             INT IDENTITY(1,1) NOT NULL,
    match_team_staff_uuid           UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_TEAM_STAFF_uuid DEFAULT (NEWID()),
    match_team_id                   INT NOT NULL,
    competition_roster_staff_id     INT NOT NULL,
    staff_role                      VARCHAR(30) NOT NULL,
    status                          VARCHAR(20) NOT NULL
        CONSTRAINT DF_MATCH_TEAM_STAFF_status DEFAULT ('PRESENT'),

    CONSTRAINT PK_MATCH_TEAM_STAFF PRIMARY KEY (match_team_staff_id),
    CONSTRAINT UQ_MATCH_TEAM_STAFF_uuid UNIQUE (match_team_staff_uuid),
    CONSTRAINT UQ_MATCH_TEAM_STAFF_person
        UNIQUE (match_team_id, competition_roster_staff_id),
    CONSTRAINT FK_MATCH_TEAM_STAFF_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT FK_MATCH_TEAM_STAFF_ROSTER_STAFF
        FOREIGN KEY (competition_roster_staff_id)
        REFERENCES dbo.COMPETITION_ROSTER_STAFF (competition_roster_staff_id),
    CONSTRAINT CK_MATCH_TEAM_STAFF_status
        CHECK (status IN ('PRESENT','ABSENT','DISQUALIFIED'))
);
GO

/* Hasta dos líberos declarados por equipo del partido. */
CREATE TABLE dbo.MATCH_LIBERO
(
    match_libero_id          INT IDENTITY(1,1) NOT NULL,
    match_libero_uuid        UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_LIBERO_uuid DEFAULT (NEWID()),
    match_team_id            INT NOT NULL,
    match_player_id          INT NOT NULL,
    libero_order             TINYINT NOT NULL,

    CONSTRAINT PK_MATCH_LIBERO PRIMARY KEY (match_libero_id),
    CONSTRAINT UQ_MATCH_LIBERO_uuid UNIQUE (match_libero_uuid),
    CONSTRAINT UQ_MATCH_LIBERO_order UNIQUE (match_team_id, libero_order),
    CONSTRAINT UQ_MATCH_LIBERO_player UNIQUE (match_team_id, match_player_id),
    CONSTRAINT FK_MATCH_LIBERO_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT FK_MATCH_LIBERO_PLAYER
        FOREIGN KEY (match_player_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT CK_MATCH_LIBERO_order CHECK (libero_order IN (1,2))
);
GO

/* ============================================================================
   3. SESIÓN DE CAPTURA / AUTORIDAD DE ESCRITURA
   ============================================================================ */
CREATE TABLE dbo.MATCH_SHEET_SESSION
(
    match_sheet_session_id   INT IDENTITY(1,1) NOT NULL,
    session_uuid             UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_SHEET_SESSION_uuid DEFAULT (NEWID()),
    match_sheet_id           INT NOT NULL,
    match_official_id        INT NOT NULL,
    device_id                NVARCHAR(100) NOT NULL,
    status                   VARCHAR(20) NOT NULL
        CONSTRAINT DF_MATCH_SHEET_SESSION_status DEFAULT ('ACTIVE'),
    started_at               DATETIME2(3) NOT NULL
        CONSTRAINT DF_MATCH_SHEET_SESSION_started DEFAULT (SYSUTCDATETIME()),
    ended_at                 DATETIME2(3) NULL,

    CONSTRAINT PK_MATCH_SHEET_SESSION PRIMARY KEY (match_sheet_session_id),
    CONSTRAINT UQ_MATCH_SHEET_SESSION_uuid UNIQUE (session_uuid),
    CONSTRAINT FK_MATCH_SHEET_SESSION_SHEET
        FOREIGN KEY (match_sheet_id) REFERENCES dbo.MATCH_SHEET (match_sheet_id),
    CONSTRAINT FK_MATCH_SHEET_SESSION_OFFICIAL
        FOREIGN KEY (match_official_id) REFERENCES dbo.MATCH_OFFICIAL (match_official_id),
    CONSTRAINT CK_MATCH_SHEET_SESSION_status
        CHECK (status IN ('ACTIVE','CLOSED','ABANDONED')),
    CONSTRAINT CK_MATCH_SHEET_SESSION_dates
        CHECK (ended_at IS NULL OR ended_at >= started_at)
);
GO

CREATE UNIQUE INDEX UX_MATCH_SHEET_SESSION_active
    ON dbo.MATCH_SHEET_SESSION (match_sheet_id)
    WHERE status = 'ACTIVE';
GO

/* ============================================================================
   4. ALINEACIÓN INICIAL DE CADA SET
   ============================================================================ */
CREATE TABLE dbo.MATCH_SET_LINEUP
(
    match_set_lineup_id      INT IDENTITY(1,1) NOT NULL,
    lineup_uuid              UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_SET_LINEUP_uuid DEFAULT (NEWID()),
    match_set_id             INT NOT NULL,
    match_team_id            INT NOT NULL,
    status                   VARCHAR(20) NOT NULL
        CONSTRAINT DF_MATCH_SET_LINEUP_status DEFAULT ('DRAFT'),
    submitted_at             DATETIME2(3) NULL,
    confirmed_at             DATETIME2(3) NULL,

    CONSTRAINT PK_MATCH_SET_LINEUP PRIMARY KEY (match_set_lineup_id),
    CONSTRAINT UQ_MATCH_SET_LINEUP_uuid UNIQUE (lineup_uuid),
    CONSTRAINT UQ_MATCH_SET_LINEUP_set_team UNIQUE (match_set_id, match_team_id),
    CONSTRAINT FK_MATCH_SET_LINEUP_SET
        FOREIGN KEY (match_set_id) REFERENCES dbo.MATCH_SET (match_set_id),
    CONSTRAINT FK_MATCH_SET_LINEUP_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT CK_MATCH_SET_LINEUP_status
        CHECK (status IN ('DRAFT','SUBMITTED','CONFIRMED')),
    CONSTRAINT CK_MATCH_SET_LINEUP_dates
        CHECK
        (
            confirmed_at IS NULL
            OR submitted_at IS NULL
            OR confirmed_at >= submitted_at
        )
);
GO

CREATE TABLE dbo.MATCH_SET_LINEUP_POSITION
(
    match_set_lineup_position_id INT IDENTITY(1,1) NOT NULL,
    lineup_position_uuid         UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_SET_LINEUP_POSITION_uuid DEFAULT (NEWID()),
    match_set_lineup_id          INT NOT NULL,
    rotation_position            TINYINT NOT NULL,
    match_player_id              INT NOT NULL,

    CONSTRAINT PK_MATCH_SET_LINEUP_POSITION PRIMARY KEY (match_set_lineup_position_id),
    CONSTRAINT UQ_MATCH_SET_LINEUP_POSITION_uuid UNIQUE (lineup_position_uuid),
    CONSTRAINT UQ_MATCH_SET_LINEUP_POSITION_position
        UNIQUE (match_set_lineup_id, rotation_position),
    CONSTRAINT UQ_MATCH_SET_LINEUP_POSITION_player
        UNIQUE (match_set_lineup_id, match_player_id),
    CONSTRAINT FK_MATCH_SET_LINEUP_POSITION_LINEUP
        FOREIGN KEY (match_set_lineup_id)
        REFERENCES dbo.MATCH_SET_LINEUP (match_set_lineup_id),
    CONSTRAINT FK_MATCH_SET_LINEUP_POSITION_PLAYER
        FOREIGN KEY (match_player_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT CK_MATCH_SET_LINEUP_POSITION_rotation
        CHECK (rotation_position BETWEEN 1 AND 6)
);
GO

/* ============================================================================
   5. LOG CRONOLÓGICO / SINCRONIZACIÓN
   ============================================================================ */
CREATE TABLE dbo.MATCH_EVENT
(
    match_event_id          BIGINT IDENTITY(1,1) NOT NULL,
    event_uuid              UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_EVENT_uuid DEFAULT (NEWID()),
    match_sheet_id          INT NOT NULL,
    match_set_id            INT NULL,
    match_sheet_session_id  INT NOT NULL,

    sequence_number         INT NOT NULL,
    local_sequence          INT NOT NULL,
    event_type              VARCHAR(30) NOT NULL,
    team_side               VARCHAR(4) NULL,
    match_player_id         INT NULL,
    related_match_player_id INT NULL,

    home_score_after        SMALLINT NULL,
    away_score_after        SMALLINT NULL,

    voided_event_uuid       UNIQUEIDENTIFIER NULL,

    device_id               NVARCHAR(100) NOT NULL,
    client_created_at       DATETIME2(3) NOT NULL,
    server_received_at      DATETIME2(3) NOT NULL
        CONSTRAINT DF_MATCH_EVENT_received DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_MATCH_EVENT PRIMARY KEY (match_event_id),
    CONSTRAINT UQ_MATCH_EVENT_uuid UNIQUE (event_uuid),
    CONSTRAINT UQ_MATCH_EVENT_sequence UNIQUE (match_sheet_id, sequence_number),
    CONSTRAINT UQ_MATCH_EVENT_local UNIQUE (match_sheet_session_id, local_sequence),

    CONSTRAINT FK_MATCH_EVENT_SHEET
        FOREIGN KEY (match_sheet_id) REFERENCES dbo.MATCH_SHEET (match_sheet_id),
    CONSTRAINT FK_MATCH_EVENT_SET
        FOREIGN KEY (match_set_id) REFERENCES dbo.MATCH_SET (match_set_id),
    CONSTRAINT FK_MATCH_EVENT_SESSION
        FOREIGN KEY (match_sheet_session_id)
        REFERENCES dbo.MATCH_SHEET_SESSION (match_sheet_session_id),
    CONSTRAINT FK_MATCH_EVENT_PLAYER
        FOREIGN KEY (match_player_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT FK_MATCH_EVENT_RELATED_PLAYER
        FOREIGN KEY (related_match_player_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT FK_MATCH_EVENT_VOIDED_EVENT
        FOREIGN KEY (voided_event_uuid) REFERENCES dbo.MATCH_EVENT (event_uuid),

    CONSTRAINT CK_MATCH_EVENT_sequence CHECK (sequence_number > 0),
    CONSTRAINT CK_MATCH_EVENT_local_sequence CHECK (local_sequence > 0),
    CONSTRAINT CK_MATCH_EVENT_type
        CHECK
        (
            event_type IN
            ('SHEET_OPEN','MATCH_START','SET_START','POINT',
             'SUBSTITUTION','LIBERO_ENTER','LIBERO_EXIT','TIMEOUT',
             'SANCTION','SET_END','CORRECTION','MATCH_END')
        ),
    CONSTRAINT CK_MATCH_EVENT_side
        CHECK (team_side IS NULL OR team_side IN ('HOME','AWAY')),
    CONSTRAINT CK_MATCH_EVENT_scores
        CHECK
        (
            (home_score_after IS NULL AND away_score_after IS NULL)
            OR
            (home_score_after >= 0 AND away_score_after >= 0)
        ),
    CONSTRAINT CK_MATCH_EVENT_correction
        CHECK
        (
            (event_type = 'CORRECTION' AND voided_event_uuid IS NOT NULL)
            OR
            (event_type <> 'CORRECTION' AND voided_event_uuid IS NULL)
        ),
    CONSTRAINT CK_MATCH_EVENT_not_self_void
        CHECK (voided_event_uuid IS NULL OR voided_event_uuid <> event_uuid)
);
GO

CREATE UNIQUE INDEX UX_MATCH_EVENT_void_once
    ON dbo.MATCH_EVENT (voided_event_uuid)
    WHERE voided_event_uuid IS NOT NULL;
GO

CREATE INDEX IX_MATCH_EVENT_sheet_sequence
    ON dbo.MATCH_EVENT (match_sheet_id, sequence_number)
    INCLUDE (event_uuid, event_type, match_set_id, team_side,
             home_score_after, away_score_after);
GO

CREATE INDEX IX_MATCH_EVENT_set_sequence
    ON dbo.MATCH_EVENT (match_set_id, sequence_number)
    WHERE match_set_id IS NOT NULL;
GO

/* ============================================================================
   6. EVENTOS DEPORTIVOS ESTRUCTURADOS
   ============================================================================ */
CREATE TABLE dbo.MATCH_SUBSTITUTION
(
    match_substitution_id   BIGINT IDENTITY(1,1) NOT NULL,
    substitution_uuid       UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_SUBSTITUTION_uuid DEFAULT (NEWID()),
    event_uuid              UNIQUEIDENTIFIER NOT NULL,
    match_set_id            INT NOT NULL,
    match_team_id           INT NOT NULL,
    player_out_id           INT NOT NULL,
    player_in_id            INT NOT NULL,
    home_score              SMALLINT NOT NULL,
    away_score              SMALLINT NOT NULL,

    CONSTRAINT PK_MATCH_SUBSTITUTION PRIMARY KEY (match_substitution_id),
    CONSTRAINT UQ_MATCH_SUBSTITUTION_uuid UNIQUE (substitution_uuid),
    CONSTRAINT UQ_MATCH_SUBSTITUTION_event UNIQUE (event_uuid),
    CONSTRAINT FK_MATCH_SUBSTITUTION_EVENT
        FOREIGN KEY (event_uuid) REFERENCES dbo.MATCH_EVENT (event_uuid),
    CONSTRAINT FK_MATCH_SUBSTITUTION_SET
        FOREIGN KEY (match_set_id) REFERENCES dbo.MATCH_SET (match_set_id),
    CONSTRAINT FK_MATCH_SUBSTITUTION_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT FK_MATCH_SUBSTITUTION_PLAYER_OUT
        FOREIGN KEY (player_out_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT FK_MATCH_SUBSTITUTION_PLAYER_IN
        FOREIGN KEY (player_in_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT CK_MATCH_SUBSTITUTION_players CHECK (player_out_id <> player_in_id),
    CONSTRAINT CK_MATCH_SUBSTITUTION_score CHECK (home_score >= 0 AND away_score >= 0)
);
GO

CREATE TABLE dbo.MATCH_LIBERO_REPLACEMENT
(
    match_libero_replacement_id BIGINT IDENTITY(1,1) NOT NULL,
    libero_replacement_uuid     UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_LIBERO_REPLACEMENT_uuid DEFAULT (NEWID()),
    event_uuid                  UNIQUEIDENTIFIER NOT NULL,
    match_set_id                INT NOT NULL,
    match_team_id               INT NOT NULL,
    match_libero_id             INT NOT NULL,
    replaced_match_player_id    INT NOT NULL,
    action                      VARCHAR(10) NOT NULL,
    home_score                  SMALLINT NOT NULL,
    away_score                  SMALLINT NOT NULL,

    CONSTRAINT PK_MATCH_LIBERO_REPLACEMENT PRIMARY KEY (match_libero_replacement_id),
    CONSTRAINT UQ_MATCH_LIBERO_REPLACEMENT_uuid UNIQUE (libero_replacement_uuid),
    CONSTRAINT UQ_MATCH_LIBERO_REPLACEMENT_event UNIQUE (event_uuid),
    CONSTRAINT FK_MATCH_LIBERO_REPLACEMENT_EVENT
        FOREIGN KEY (event_uuid) REFERENCES dbo.MATCH_EVENT (event_uuid),
    CONSTRAINT FK_MATCH_LIBERO_REPLACEMENT_SET
        FOREIGN KEY (match_set_id) REFERENCES dbo.MATCH_SET (match_set_id),
    CONSTRAINT FK_MATCH_LIBERO_REPLACEMENT_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT FK_MATCH_LIBERO_REPLACEMENT_LIBERO
        FOREIGN KEY (match_libero_id) REFERENCES dbo.MATCH_LIBERO (match_libero_id),
    CONSTRAINT FK_MATCH_LIBERO_REPLACEMENT_PLAYER
        FOREIGN KEY (replaced_match_player_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT CK_MATCH_LIBERO_REPLACEMENT_action CHECK (action IN ('ENTER','EXIT')),
    CONSTRAINT CK_MATCH_LIBERO_REPLACEMENT_score CHECK (home_score >= 0 AND away_score >= 0)
);
GO

CREATE TABLE dbo.MATCH_TIMEOUT
(
    match_timeout_id         BIGINT IDENTITY(1,1) NOT NULL,
    timeout_uuid             UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_TIMEOUT_uuid DEFAULT (NEWID()),
    event_uuid               UNIQUEIDENTIFIER NOT NULL,
    match_set_id             INT NOT NULL,
    match_team_id            INT NOT NULL,
    timeout_number           TINYINT NOT NULL,
    home_score               SMALLINT NOT NULL,
    away_score               SMALLINT NOT NULL,
    started_at               DATETIME2(3) NULL,
    ended_at                 DATETIME2(3) NULL,

    CONSTRAINT PK_MATCH_TIMEOUT PRIMARY KEY (match_timeout_id),
    CONSTRAINT UQ_MATCH_TIMEOUT_uuid UNIQUE (timeout_uuid),
    CONSTRAINT UQ_MATCH_TIMEOUT_event UNIQUE (event_uuid),
    CONSTRAINT UQ_MATCH_TIMEOUT_number UNIQUE (match_set_id, match_team_id, timeout_number),
    CONSTRAINT FK_MATCH_TIMEOUT_EVENT
        FOREIGN KEY (event_uuid) REFERENCES dbo.MATCH_EVENT (event_uuid),
    CONSTRAINT FK_MATCH_TIMEOUT_SET
        FOREIGN KEY (match_set_id) REFERENCES dbo.MATCH_SET (match_set_id),
    CONSTRAINT FK_MATCH_TIMEOUT_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT CK_MATCH_TIMEOUT_number CHECK (timeout_number > 0),
    CONSTRAINT CK_MATCH_TIMEOUT_score CHECK (home_score >= 0 AND away_score >= 0),
    CONSTRAINT CK_MATCH_TIMEOUT_dates CHECK (ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at)
);
GO

CREATE TABLE dbo.MATCH_SANCTION
(
    match_sanction_id        BIGINT IDENTITY(1,1) NOT NULL,
    sanction_uuid            UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_MATCH_SANCTION_uuid DEFAULT (NEWID()),
    event_uuid               UNIQUEIDENTIFIER NOT NULL,
    match_set_id             INT NULL,
    match_team_id            INT NOT NULL,
    target_type              VARCHAR(10) NOT NULL,
    match_player_id          INT NULL,
    match_team_staff_id      INT NULL,
    sanction_type            VARCHAR(30) NOT NULL,
    home_score               SMALLINT NULL,
    away_score               SMALLINT NULL,
    notes                    NVARCHAR(500) NULL,

    CONSTRAINT PK_MATCH_SANCTION PRIMARY KEY (match_sanction_id),
    CONSTRAINT UQ_MATCH_SANCTION_uuid UNIQUE (sanction_uuid),
    CONSTRAINT UQ_MATCH_SANCTION_event UNIQUE (event_uuid),
    CONSTRAINT FK_MATCH_SANCTION_EVENT
        FOREIGN KEY (event_uuid) REFERENCES dbo.MATCH_EVENT (event_uuid),
    CONSTRAINT FK_MATCH_SANCTION_SET
        FOREIGN KEY (match_set_id) REFERENCES dbo.MATCH_SET (match_set_id),
    CONSTRAINT FK_MATCH_SANCTION_TEAM
        FOREIGN KEY (match_team_id) REFERENCES dbo.MATCH_TEAM (match_team_id),
    CONSTRAINT FK_MATCH_SANCTION_PLAYER
        FOREIGN KEY (match_player_id) REFERENCES dbo.MATCH_PLAYER (match_player_id),
    CONSTRAINT FK_MATCH_SANCTION_STAFF
        FOREIGN KEY (match_team_staff_id) REFERENCES dbo.MATCH_TEAM_STAFF (match_team_staff_id),
    CONSTRAINT CK_MATCH_SANCTION_target_type
        CHECK (target_type IN ('TEAM','PLAYER','STAFF')),
    CONSTRAINT CK_MATCH_SANCTION_target
        CHECK
        (
            (target_type = 'TEAM' AND match_player_id IS NULL AND match_team_staff_id IS NULL)
            OR
            (target_type = 'PLAYER' AND match_player_id IS NOT NULL AND match_team_staff_id IS NULL)
            OR
            (target_type = 'STAFF' AND match_player_id IS NULL AND match_team_staff_id IS NOT NULL)
        ),
    CONSTRAINT CK_MATCH_SANCTION_score
        CHECK
        (
            (home_score IS NULL AND away_score IS NULL)
            OR
            (home_score >= 0 AND away_score >= 0)
        )
);
GO

/* ============================================================================
   7. PROYECCIÓN DEL ESTADO ACTUAL DEL SET

   Esta tabla es una cache/proyección. La información autoritativa es el log de
   eventos más las tablas estructuradas. No se sincroniza como hecho deportivo;
   puede recalcularse completamente.
   ============================================================================ */
CREATE TABLE dbo.MATCH_SET_STATE
(
    match_set_id             INT NOT NULL,
    home_score               SMALLINT NOT NULL
        CONSTRAINT DF_MATCH_SET_STATE_home_score DEFAULT (0),
    away_score               SMALLINT NOT NULL
        CONSTRAINT DF_MATCH_SET_STATE_away_score DEFAULT (0),
    serving_side             VARCHAR(4) NULL,
    home_rotation_offset     TINYINT NOT NULL
        CONSTRAINT DF_MATCH_SET_STATE_home_rotation DEFAULT (0),
    away_rotation_offset     TINYINT NOT NULL
        CONSTRAINT DF_MATCH_SET_STATE_away_rotation DEFAULT (0),
    last_event_sequence      INT NULL,
    last_event_uuid          UNIQUEIDENTIFIER NULL,
    state_version            BIGINT NOT NULL
        CONSTRAINT DF_MATCH_SET_STATE_version DEFAULT (0),
    updated_at               DATETIME2(3) NOT NULL
        CONSTRAINT DF_MATCH_SET_STATE_updated DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_MATCH_SET_STATE PRIMARY KEY (match_set_id),
    CONSTRAINT FK_MATCH_SET_STATE_SET
        FOREIGN KEY (match_set_id) REFERENCES dbo.MATCH_SET (match_set_id),
    CONSTRAINT FK_MATCH_SET_STATE_LAST_EVENT
        FOREIGN KEY (last_event_uuid) REFERENCES dbo.MATCH_EVENT (event_uuid),
    CONSTRAINT CK_MATCH_SET_STATE_score CHECK (home_score >= 0 AND away_score >= 0),
    CONSTRAINT CK_MATCH_SET_STATE_serving
        CHECK (serving_side IS NULL OR serving_side IN ('HOME','AWAY')),
    CONSTRAINT CK_MATCH_SET_STATE_home_rotation CHECK (home_rotation_offset BETWEEN 0 AND 5),
    CONSTRAINT CK_MATCH_SET_STATE_away_rotation CHECK (away_rotation_offset BETWEEN 0 AND 5),
    CONSTRAINT CK_MATCH_SET_STATE_sequence
        CHECK (last_event_sequence IS NULL OR last_event_sequence > 0),
    CONSTRAINT CK_MATCH_SET_STATE_version CHECK (state_version >= 0)
);
GO

/* ============================================================================
   8. ÍNDICES DE CONSULTA
   ============================================================================ */
CREATE INDEX IX_MATCH_TEAM_sheet
    ON dbo.MATCH_TEAM (match_sheet_id, side);
GO

CREATE INDEX IX_MATCH_PLAYER_team
    ON dbo.MATCH_PLAYER (match_team_id, status, jersey_number);
GO

CREATE INDEX IX_MATCH_SET_LINEUP_set
    ON dbo.MATCH_SET_LINEUP (match_set_id, match_team_id);
GO

CREATE INDEX IX_MATCH_SUBSTITUTION_set_team
    ON dbo.MATCH_SUBSTITUTION (match_set_id, match_team_id, match_substitution_id);
GO

CREATE INDEX IX_MATCH_LIBERO_REPLACEMENT_set_team
    ON dbo.MATCH_LIBERO_REPLACEMENT (match_set_id, match_team_id, match_libero_replacement_id);
GO

CREATE INDEX IX_MATCH_TIMEOUT_set_team
    ON dbo.MATCH_TIMEOUT (match_set_id, match_team_id, timeout_number);
GO

CREATE INDEX IX_MATCH_SANCTION_set_team
    ON dbo.MATCH_SANCTION (match_set_id, match_team_id);
GO

/* ============================================================================
   9. VALIDACIONES DE INTEGRIDAD ENTRE TABLAS
   ============================================================================ */

/* MATCH_TEAM debe coincidir con HOME/AWAY del MATCH y con el roster del equipo. */
CREATE TRIGGER dbo.TR_MATCH_TEAM_VALIDATE
ON dbo.MATCH_TEAM
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_SHEET ms ON ms.match_sheet_id = i.match_sheet_id
        INNER JOIN dbo.MATCH m ON m.match_id = ms.match_id
        WHERE
            (i.side = 'HOME' AND (m.home_team_entry_id IS NULL OR i.team_entry_id <> m.home_team_entry_id))
            OR
            (i.side = 'AWAY' AND (m.away_team_entry_id IS NULL OR i.team_entry_id <> m.away_team_entry_id))
    )
    BEGIN
        THROW 50100, 'MATCH_TEAM no coincide con el equipo HOME/AWAY definido en MATCH.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.COMPETITION_ROSTER cr
            ON cr.competition_roster_id = i.competition_roster_id
        WHERE cr.team_entry_id <> i.team_entry_id
    )
    BEGIN
        THROW 50101, 'El COMPETITION_ROSTER indicado no pertenece al TEAM_ENTRY del MATCH_TEAM.', 1;
    END;
END;
GO

/* Los jugadores del acta deben pertenecer al roster del equipo correspondiente. */
CREATE TRIGGER dbo.TR_MATCH_PLAYER_VALIDATE
ON dbo.MATCH_PLAYER
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_TEAM mt ON mt.match_team_id = i.match_team_id
        INNER JOIN dbo.COMPETITION_ROSTER_PLAYER crp
            ON crp.competition_roster_player_id = i.competition_roster_player_id
        WHERE crp.competition_roster_id <> mt.competition_roster_id
    )
    BEGIN
        THROW 50102, 'El jugador del acta no pertenece al COMPETITION_ROSTER del equipo.', 1;
    END;
END;
GO

/* El líbero declarado debe ser un jugador del mismo MATCH_TEAM. */
CREATE TRIGGER dbo.TR_MATCH_LIBERO_VALIDATE
ON dbo.MATCH_LIBERO
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_PLAYER mp ON mp.match_player_id = i.match_player_id
        WHERE mp.match_team_id <> i.match_team_id
    )
    BEGIN
        THROW 50103, 'El líbero debe pertenecer al mismo MATCH_TEAM.', 1;
    END;
END;
GO

/*
Al confirmar una alineación debe haber exactamente seis posiciones, todos los
jugadores deben pertenecer al mismo equipo y ninguno puede estar declarado como
líbero. Durante DRAFT/SUBMITTED se permite construir la alineación gradualmente.
*/
CREATE TRIGGER dbo.TR_MATCH_SET_LINEUP_CONFIRM
ON dbo.MATCH_SET_LINEUP
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        WHERE i.status = 'CONFIRMED'
          AND
          (
              SELECT COUNT(*)
              FROM dbo.MATCH_SET_LINEUP_POSITION p
              WHERE p.match_set_lineup_id = i.match_set_lineup_id
          ) <> 6
    )
    BEGIN
        THROW 50104, 'Una alineación confirmada debe contener exactamente seis posiciones P1..P6.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_SET_LINEUP_POSITION p
            ON p.match_set_lineup_id = i.match_set_lineup_id
        INNER JOIN dbo.MATCH_PLAYER mp
            ON mp.match_player_id = p.match_player_id
        WHERE i.status = 'CONFIRMED'
          AND mp.match_team_id <> i.match_team_id
    )
    BEGIN
        THROW 50105, 'Todos los jugadores de la alineación deben pertenecer al MATCH_TEAM.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_SET_LINEUP_POSITION p
            ON p.match_set_lineup_id = i.match_set_lineup_id
        INNER JOIN dbo.MATCH_LIBERO ml
            ON ml.match_player_id = p.match_player_id
           AND ml.match_team_id = i.match_team_id
        WHERE i.status = 'CONFIRMED'
    )
    BEGIN
        THROW 50106, 'Un jugador declarado como líbero no forma parte de las seis posiciones iniciales P1..P6.', 1;
    END;
END;
GO

/* Impide alterar posiciones de una alineación ya confirmada. */
CREATE TRIGGER dbo.TR_MATCH_SET_LINEUP_POSITION_LOCK
ON dbo.MATCH_SET_LINEUP_POSITION
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM
        (
            SELECT match_set_lineup_id FROM inserted
            UNION
            SELECT match_set_lineup_id FROM deleted
        ) x
        INNER JOIN dbo.MATCH_SET_LINEUP l
            ON l.match_set_lineup_id = x.match_set_lineup_id
        WHERE l.status = 'CONFIRMED'
    )
    BEGIN
        THROW 50107, 'No se pueden modificar posiciones de una alineación confirmada.', 1;
    END;
END;
GO

/* La sesión activa debe pertenecer a un oficial del mismo partido y ser SCORER. */
CREATE TRIGGER dbo.TR_MATCH_SHEET_SESSION_VALIDATE
ON dbo.MATCH_SHEET_SESSION
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_SHEET ms ON ms.match_sheet_id = i.match_sheet_id
        INNER JOIN dbo.MATCH_OFFICIAL mo ON mo.match_official_id = i.match_official_id
        WHERE mo.match_id <> ms.match_id
           OR mo.official_role <> 'SCORER'
    )
    BEGIN
        THROW 50108, 'La sesión de captura debe pertenecer al SCORER asignado al mismo MATCH.', 1;
    END;
END;
GO

/* Un evento y su sesión/set deben pertenecer al mismo acta/partido. */
CREATE TRIGGER dbo.TR_MATCH_EVENT_VALIDATE
ON dbo.MATCH_EVENT
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_SHEET_SESSION s
            ON s.match_sheet_session_id = i.match_sheet_session_id
        WHERE s.match_sheet_id <> i.match_sheet_id
    )
    BEGIN
        THROW 50109, 'MATCH_EVENT y MATCH_SHEET_SESSION deben pertenecer al mismo MATCH_SHEET.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_SHEET ms ON ms.match_sheet_id = i.match_sheet_id
        INNER JOIN dbo.MATCH_SET st ON st.match_set_id = i.match_set_id
        WHERE i.match_set_id IS NOT NULL
          AND st.match_id <> ms.match_id
    )
    BEGIN
        THROW 50110, 'El MATCH_SET del evento no pertenece al MATCH del acta.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.MATCH_EVENT olde ON olde.event_uuid = i.voided_event_uuid
        WHERE i.event_type = 'CORRECTION'
          AND
          (
              olde.match_sheet_id <> i.match_sheet_id
              OR olde.sequence_number >= i.sequence_number
          )
    )
    BEGIN
        THROW 50111, 'Una corrección sólo puede anular un evento anterior del mismo MATCH_SHEET.', 1;
    END;
END;
GO

/* ============================================================================
   10. NOTAS DE USO
   ============================================================================

A) CÁLCULO DE LOS SEIS JUGADORES EN CANCHA
-------------------------------------------
Para cada equipo y set:

1. Partir de MATCH_SET_LINEUP_POSITION (P1..P6).
2. Aplicar MATCH_SUBSTITUTION válidas hasta la secuencia consultada.
3. Aplicar el rotation_offset de MATCH_SET_STATE (o reconstruirlo desde eventos).
4. Aplicar MATCH_LIBERO_REPLACEMENT activo:
      jugador reglamentario -> líbero que ocupa temporalmente su lugar.

No se persiste una fotografía de seis jugadores después de cada rally.

B) OFFLINE / SINCRONIZACIÓN
---------------------------
- Los UUID son generables por la PWA sin conexión.
- event_uuid es idempotente: reenviar un evento no debe duplicarlo.
- sequence_number representa el orden lógico del acta.
- local_sequence identifica el orden generado dentro de una sesión/dispositivo.
- client_created_at NO determina el orden deportivo.
- server_received_at permite auditoría de la sincronización.
- MATCH_SET_STATE puede descartarse y reconstruirse.

C) CORRECCIONES
---------------
- Nunca eliminar físicamente un evento oficial para corregir el partido.
- Insertar un MATCH_EVENT de tipo CORRECTION con voided_event_uuid.
- Insertar luego, cuando corresponda, el evento deportivo correcto.
- Al reconstruir el estado deben excluirse los eventos anulados.

D) REGLAS DE NEGOCIO QUE DEBEN VALIDARSE EN SERVICIO/APLICACIÓN
---------------------------------------------------------------
Algunas reglas dependen del estado completo y no conviene resolverlas solamente
con CHECK/FK:
- límites y reingresos de sustituciones según reglamento aplicable;
- secuencia válida de ENTER/EXIT del líbero;
- qué jugador puede reemplazar/reingresar por un líbero;
- recuperación del derecho al saque y actualización de rotation_offset;
- consistencia entre POINT, marcador y servidor;
- reglas de cierre de set y partido;
- sanciones con efecto sobre el marcador;
- reconstrucción posterior a una CORRECTION.
===============================================================================
*/
