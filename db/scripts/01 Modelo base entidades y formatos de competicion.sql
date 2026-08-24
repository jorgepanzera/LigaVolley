/*
===============================================================================
Liga Volley - Base y Formatos de Competencia
Archivo: LigaVolley_Base_Competencia_v1.sql
Motor : Microsoft SQL Server
Versión del modelo: 1.0

ALCANCE
-------
Este script crea el modelo de datos del núcleo de la liga y de los formatos
parametrizables de competencia.

Incluye:
- Clubes, equipos, sedes, divisionales y temporadas.
- Formatos reutilizables de campeonato.
- Fases, grupos, clasificación y series de playoff.
- Reglas de puntuación, desempate, ascenso y descenso.
- Instancias reales de competencias, fases, grupos, series, partidos y sets.
- Origen lógico de participantes cuando todavía no se conocen.
- Datos de ejemplo para los formatos conocidos de 6-8 y 9-12 equipos.

NO incluye todavía:
- Jugadores, técnicos y planteles.
- Jueces y designaciones.
- Alineaciones, rotaciones, líbero y sustituciones.
- Eventos punto a punto del partido.
- Operación offline y sincronización.
- Estadísticas individuales.

DECISIONES FUNCIONALES CERRADAS EN ESTA VERSIÓN
------------------------------------------------
1) 6 a 8 equipos:
   - Fase regular todos contra todos, ida y vuelta.
   - Semifinales 1º-4º y 2º-3º.
   - 1º y 2º comienzan su semifinal con una victoria.
   - Se requieren 2 victorias para ganar la semifinal.
   - Final a partido único.
   - Tercer puesto a partido único.

2) 9 a 12 equipos:
   - Primera rueda todos contra todos, una vuelta.
   - Segunda fase dividida en dos grupos.
   - La tabla se divide por mitades: mitad superior a Campeonato y mitad inferior
     a Permanencia; con cantidad impar, Campeonato recibe un equipo más.
     En 10 equipos esto produce 1-5 y 6-10.
   - Los grupos juegan una vuelta.
   - No se arrastran puntos de la primera rueda (parametrizable).
   - Los 4 primeros del grupo Campeonato van a semifinales.
   - Semifinales, final y tercer puesto como en el formato anterior.

3) Fixture:
   - Ida/vuelta: segunda rueda espejada invirtiendo localía.
   - Rueda única: sorteo procurando balance de local/visitante.
   - Estas reglas se almacenan en fixture_mode.

4) Puntuación estándar configurada:
   - 3-0 / 3-1: ganador 3, perdedor 0.
   - 3-2: ganador 2, perdedor 1.
   Puede modificarse por formato sin cambiar estructura.

5) Desempate configurado:
   - Puntos de tabla.
   - Partidos ganados.
   - Cociente de sets.
   - Cociente de puntos.
   - Enfrentamiento directo.

6) Ascensos/descensos:
   - En divisionales con nivel superior: campeón y subcampeón ascienden.
   - Los dos últimos descienden cuando existe divisional inferior.
   - El origen de las posiciones es parametrizable por formato.

===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ============================================================================
   1. MAESTROS BASE
   ============================================================================ */

CREATE TABLE dbo.CLUB
(
    club_id        INT IDENTITY(1,1) NOT NULL,
    name           NVARCHAR(150) NOT NULL,
    short_name     NVARCHAR(50) NULL,
    active         BIT NOT NULL CONSTRAINT DF_CLUB_active DEFAULT (1),

    CONSTRAINT PK_CLUB PRIMARY KEY (club_id),
    CONSTRAINT UQ_CLUB_name UNIQUE (name)
);
GO

CREATE TABLE dbo.TEAM
(
    team_id        INT IDENTITY(1,1) NOT NULL,
    club_id        INT NULL,
    name           NVARCHAR(150) NOT NULL,
    gender         CHAR(1) NOT NULL,
    active         BIT NOT NULL CONSTRAINT DF_TEAM_active DEFAULT (1),

    CONSTRAINT PK_TEAM PRIMARY KEY (team_id),
    CONSTRAINT FK_TEAM_CLUB
        FOREIGN KEY (club_id) REFERENCES dbo.CLUB (club_id),
    CONSTRAINT CK_TEAM_gender CHECK (gender IN ('M','F')),
    CONSTRAINT UQ_TEAM_name_gender UNIQUE (name, gender)
);
GO

CREATE TABLE dbo.VENUE
(
    venue_id       INT IDENTITY(1,1) NOT NULL,
    name           NVARCHAR(150) NOT NULL,
    address        NVARCHAR(250) NULL,
    active         BIT NOT NULL CONSTRAINT DF_VENUE_active DEFAULT (1),

    CONSTRAINT PK_VENUE PRIMARY KEY (venue_id),
    CONSTRAINT UQ_VENUE_name UNIQUE (name)
);
GO

CREATE TABLE dbo.SEASON
(
    season_id      INT IDENTITY(1,1) NOT NULL,
    [year]         SMALLINT NOT NULL,
    name           NVARCHAR(100) NOT NULL,
    start_date     DATE NULL,
    end_date       DATE NULL,
    active         BIT NOT NULL CONSTRAINT DF_SEASON_active DEFAULT (1),

    CONSTRAINT PK_SEASON PRIMARY KEY (season_id),
    CONSTRAINT UQ_SEASON_year UNIQUE ([year]),
    CONSTRAINT CK_SEASON_dates
        CHECK (end_date IS NULL OR start_date IS NULL OR end_date >= start_date)
);
GO

CREATE TABLE dbo.DIVISION
(
    division_id    INT IDENTITY(1,1) NOT NULL,
    name           NVARCHAR(50) NOT NULL,
    level_order    SMALLINT NOT NULL,
    gender         CHAR(1) NOT NULL,
    active         BIT NOT NULL CONSTRAINT DF_DIVISION_active DEFAULT (1),

    CONSTRAINT PK_DIVISION PRIMARY KEY (division_id),
    CONSTRAINT CK_DIVISION_gender CHECK (gender IN ('M','F')),
    CONSTRAINT CK_DIVISION_level_order CHECK (level_order > 0),
    CONSTRAINT UQ_DIVISION_name_gender UNIQUE (name, gender),
    CONSTRAINT UQ_DIVISION_level_gender UNIQUE (level_order, gender)
);
GO

/* ============================================================================
   2. DEFINICIÓN PARAMETRIZABLE DE FORMATOS
   ============================================================================ */

CREATE TABLE dbo.COMPETITION_FORMAT
(
    competition_format_id INT IDENTITY(1,1) NOT NULL,
    code                  VARCHAR(30) NOT NULL,
    name                  NVARCHAR(150) NOT NULL,
    description           NVARCHAR(500) NULL,
    min_teams             SMALLINT NOT NULL,
    max_teams             SMALLINT NOT NULL,
    active                BIT NOT NULL CONSTRAINT DF_COMPETITION_FORMAT_active DEFAULT (1),

    CONSTRAINT PK_COMPETITION_FORMAT PRIMARY KEY (competition_format_id),
    CONSTRAINT UQ_COMPETITION_FORMAT_code UNIQUE (code),
    CONSTRAINT CK_COMPETITION_FORMAT_team_range
        CHECK (min_teams > 1 AND max_teams >= min_teams)
);
GO

CREATE TABLE dbo.FORMAT_PHASE
(
    format_phase_id        INT IDENTITY(1,1) NOT NULL,
    competition_format_id  INT NOT NULL,
    code                   VARCHAR(30) NOT NULL,
    name                   NVARCHAR(100) NOT NULL,

    phase_type             VARCHAR(20) NOT NULL,
    phase_role             VARCHAR(20) NOT NULL,
    sequence               SMALLINT NOT NULL,

    rounds                 SMALLINT NULL,
    fixture_mode           VARCHAR(30) NULL,

    active                 BIT NOT NULL CONSTRAINT DF_FORMAT_PHASE_active DEFAULT (1),

    CONSTRAINT PK_FORMAT_PHASE PRIMARY KEY (format_phase_id),

    CONSTRAINT FK_FORMAT_PHASE_COMPETITION_FORMAT
        FOREIGN KEY (competition_format_id)
        REFERENCES dbo.COMPETITION_FORMAT (competition_format_id),

    CONSTRAINT UQ_FORMAT_PHASE_code
        UNIQUE (competition_format_id, code),

    CONSTRAINT UQ_FORMAT_PHASE_id_format
        UNIQUE (format_phase_id, competition_format_id),

    CONSTRAINT CK_FORMAT_PHASE_type
        CHECK (phase_type IN ('ROUND_ROBIN','GROUP_STAGE','PLAYOFF')),

    CONSTRAINT CK_FORMAT_PHASE_role
        CHECK (phase_role IN
        ('REGULAR','CHAMPIONSHIP','RELEGATION',
         'SEMIFINAL','THIRD_PLACE','FINAL')),

    CONSTRAINT CK_FORMAT_PHASE_sequence CHECK (sequence > 0),

    CONSTRAINT CK_FORMAT_PHASE_rounds
        CHECK (rounds IS NULL OR rounds > 0),

    CONSTRAINT CK_FORMAT_PHASE_fixture_mode
        CHECK
        (
            fixture_mode IS NULL
            OR fixture_mode IN
               ('MIRRORED_HOME_AWAY','BALANCED_RANDOM','PLAYOFF')
        ),

    CONSTRAINT CK_FORMAT_PHASE_round_robin
        CHECK
        (
            phase_type <> 'ROUND_ROBIN'
            OR (rounds IS NOT NULL AND fixture_mode IS NOT NULL)
        )
);
GO

CREATE TABLE dbo.FORMAT_GROUP
(
    format_group_id        INT IDENTITY(1,1) NOT NULL,
    competition_format_id  INT NOT NULL,
    format_phase_id        INT NOT NULL,

    code                   VARCHAR(30) NOT NULL,
    name                   NVARCHAR(100) NOT NULL,
    group_role             VARCHAR(20) NOT NULL,
    sequence               SMALLINT NOT NULL,

    rounds                 SMALLINT NOT NULL,
    fixture_mode           VARCHAR(30) NOT NULL,
    carry_over_mode        VARCHAR(20) NOT NULL
        CONSTRAINT DF_FORMAT_GROUP_carry_over DEFAULT ('NONE'),

    active                 BIT NOT NULL CONSTRAINT DF_FORMAT_GROUP_active DEFAULT (1),

    CONSTRAINT PK_FORMAT_GROUP PRIMARY KEY (format_group_id),

    CONSTRAINT FK_FORMAT_GROUP_PHASE
        FOREIGN KEY (format_phase_id, competition_format_id)
        REFERENCES dbo.FORMAT_PHASE (format_phase_id, competition_format_id),

    CONSTRAINT UQ_FORMAT_GROUP_code
        UNIQUE (format_phase_id, code),

    CONSTRAINT UQ_FORMAT_GROUP_id_format
        UNIQUE (format_group_id, competition_format_id),

    CONSTRAINT CK_FORMAT_GROUP_role
        CHECK (group_role IN ('CHAMPIONSHIP','RELEGATION','OTHER')),

    CONSTRAINT CK_FORMAT_GROUP_sequence CHECK (sequence > 0),
    CONSTRAINT CK_FORMAT_GROUP_rounds CHECK (rounds > 0),

    CONSTRAINT CK_FORMAT_GROUP_fixture_mode
        CHECK (fixture_mode IN ('MIRRORED_HOME_AWAY','BALANCED_RANDOM')),

    CONSTRAINT CK_FORMAT_GROUP_carry_over
        CHECK (carry_over_mode IN ('NONE','ALL','QUALIFIED_ONLY'))
);
GO

CREATE TABLE dbo.FORMAT_PLAYOFF_SERIES
(
    format_series_id       INT IDENTITY(1,1) NOT NULL,
    competition_format_id  INT NOT NULL,
    format_phase_id        INT NOT NULL,

    code                   VARCHAR(30) NOT NULL,
    name                   NVARCHAR(100) NOT NULL,
    sequence               SMALLINT NOT NULL,

    wins_required          SMALLINT NOT NULL,
    team1_initial_wins     SMALLINT NOT NULL
        CONSTRAINT DF_FORMAT_PLAYOFF_SERIES_team1_initial_wins DEFAULT (0),
    team2_initial_wins     SMALLINT NOT NULL
        CONSTRAINT DF_FORMAT_PLAYOFF_SERIES_team2_initial_wins DEFAULT (0),

    active                 BIT NOT NULL
        CONSTRAINT DF_FORMAT_PLAYOFF_SERIES_active DEFAULT (1),

    CONSTRAINT PK_FORMAT_PLAYOFF_SERIES PRIMARY KEY (format_series_id),

    CONSTRAINT FK_FORMAT_PLAYOFF_SERIES_PHASE
        FOREIGN KEY (format_phase_id, competition_format_id)
        REFERENCES dbo.FORMAT_PHASE (format_phase_id, competition_format_id),

    CONSTRAINT UQ_FORMAT_PLAYOFF_SERIES_code
        UNIQUE (format_phase_id, code),

    CONSTRAINT UQ_FORMAT_PLAYOFF_SERIES_id_format
        UNIQUE (format_series_id, competition_format_id),

    CONSTRAINT CK_FORMAT_PLAYOFF_SERIES_sequence CHECK (sequence > 0),
    CONSTRAINT CK_FORMAT_PLAYOFF_SERIES_wins_required CHECK (wins_required > 0),

    CONSTRAINT CK_FORMAT_PLAYOFF_SERIES_initial_wins
        CHECK
        (
            team1_initial_wins >= 0
            AND team2_initial_wins >= 0
            AND team1_initial_wins < wins_required
            AND team2_initial_wins < wins_required
        )
);
GO

CREATE TABLE dbo.FORMAT_QUALIFICATION_RULE
(
    qualification_rule_id   INT IDENTITY(1,1) NOT NULL,
    competition_format_id   INT NOT NULL,

    source_format_phase_id  INT NOT NULL,
    source_format_group_id  INT NULL,

    selection_mode          VARCHAR(30) NOT NULL,
    from_position           SMALLINT NULL,
    to_position             SMALLINT NULL,

    target_type             VARCHAR(20) NOT NULL,
    target_format_phase_id  INT NULL,
    target_format_group_id  INT NULL,
    target_format_series_id INT NULL,
    target_side             TINYINT NULL,

    sequence                SMALLINT NOT NULL,

    CONSTRAINT PK_FORMAT_QUALIFICATION_RULE
        PRIMARY KEY (qualification_rule_id),

    CONSTRAINT FK_FORMAT_QUALIFICATION_FORMAT
        FOREIGN KEY (competition_format_id)
        REFERENCES dbo.COMPETITION_FORMAT (competition_format_id),

    CONSTRAINT FK_FORMAT_QUALIFICATION_SOURCE_PHASE
        FOREIGN KEY (source_format_phase_id, competition_format_id)
        REFERENCES dbo.FORMAT_PHASE (format_phase_id, competition_format_id),

    CONSTRAINT FK_FORMAT_QUALIFICATION_SOURCE_GROUP
        FOREIGN KEY (source_format_group_id, competition_format_id)
        REFERENCES dbo.FORMAT_GROUP (format_group_id, competition_format_id),

    CONSTRAINT FK_FORMAT_QUALIFICATION_TARGET_PHASE
        FOREIGN KEY (target_format_phase_id, competition_format_id)
        REFERENCES dbo.FORMAT_PHASE (format_phase_id, competition_format_id),

    CONSTRAINT FK_FORMAT_QUALIFICATION_TARGET_GROUP
        FOREIGN KEY (target_format_group_id, competition_format_id)
        REFERENCES dbo.FORMAT_GROUP (format_group_id, competition_format_id),

    CONSTRAINT FK_FORMAT_QUALIFICATION_TARGET_SERIES
        FOREIGN KEY (target_format_series_id, competition_format_id)
        REFERENCES dbo.FORMAT_PLAYOFF_SERIES (format_series_id, competition_format_id),

    CONSTRAINT CK_FORMAT_QUALIFICATION_selection_mode
        CHECK (selection_mode IN ('POSITION_RANGE','TOP_HALF','BOTTOM_HALF')),

    CONSTRAINT CK_FORMAT_QUALIFICATION_positions
        CHECK
        (
            (
                selection_mode = 'POSITION_RANGE'
                AND from_position IS NOT NULL
                AND to_position IS NOT NULL
                AND from_position > 0
                AND to_position >= from_position
            )
            OR
            (
                selection_mode IN ('TOP_HALF','BOTTOM_HALF')
                AND from_position IS NULL
                AND to_position IS NULL
            )
        ),

    CONSTRAINT CK_FORMAT_QUALIFICATION_target_type
        CHECK (target_type IN ('GROUP','SERIES')),

    CONSTRAINT CK_FORMAT_QUALIFICATION_target_side
        CHECK (target_side IS NULL OR target_side IN (1,2)),

    CONSTRAINT CK_FORMAT_QUALIFICATION_sequence CHECK (sequence > 0),

    CONSTRAINT CK_FORMAT_QUALIFICATION_target
        CHECK
        (
            (
                target_type = 'GROUP'
                AND target_format_phase_id IS NOT NULL
                AND target_format_group_id IS NOT NULL
                AND target_format_series_id IS NULL
                AND target_side IS NULL
            )
            OR
            (
                target_type = 'SERIES'
                AND target_format_phase_id IS NOT NULL
                AND target_format_group_id IS NULL
                AND target_format_series_id IS NOT NULL
                AND target_side IN (1,2)
                AND selection_mode = 'POSITION_RANGE'
                AND from_position = to_position
            )
        )
);
GO

CREATE TABLE dbo.FORMAT_SERIES_PARTICIPANT_SOURCE
(
    format_series_participant_source_id INT IDENTITY(1,1) NOT NULL,
    competition_format_id               INT NOT NULL,

    target_format_series_id             INT NOT NULL,
    target_side                         TINYINT NOT NULL,

    source_type                         VARCHAR(20) NOT NULL,
    source_format_series_id             INT NOT NULL,

    CONSTRAINT PK_FORMAT_SERIES_PARTICIPANT_SOURCE
        PRIMARY KEY (format_series_participant_source_id),

    CONSTRAINT FK_FORMAT_SERIES_SOURCE_FORMAT
        FOREIGN KEY (competition_format_id)
        REFERENCES dbo.COMPETITION_FORMAT (competition_format_id),

    CONSTRAINT FK_FORMAT_SERIES_SOURCE_TARGET
        FOREIGN KEY (target_format_series_id, competition_format_id)
        REFERENCES dbo.FORMAT_PLAYOFF_SERIES
                   (format_series_id, competition_format_id),

    CONSTRAINT FK_FORMAT_SERIES_SOURCE_SOURCE
        FOREIGN KEY (source_format_series_id, competition_format_id)
        REFERENCES dbo.FORMAT_PLAYOFF_SERIES
                   (format_series_id, competition_format_id),

    CONSTRAINT UQ_FORMAT_SERIES_SOURCE_target_side
        UNIQUE (target_format_series_id, target_side),

    CONSTRAINT CK_FORMAT_SERIES_SOURCE_side
        CHECK (target_side IN (1,2)),

    CONSTRAINT CK_FORMAT_SERIES_SOURCE_type
        CHECK (source_type IN ('SERIES_WINNER','SERIES_LOSER')),

    CONSTRAINT CK_FORMAT_SERIES_SOURCE_not_same
        CHECK (target_format_series_id <> source_format_series_id)
);
GO

/* ============================================================================
   3. REGLAS DE TABLA, DESEMPATE Y MOVIMIENTOS
   ============================================================================ */

CREATE TABLE dbo.FORMAT_SCORING_RULE
(
    format_scoring_rule_id INT IDENTITY(1,1) NOT NULL,
    competition_format_id  INT NOT NULL,

    winner_sets            TINYINT NOT NULL,
    loser_sets             TINYINT NOT NULL,
    winner_table_points    SMALLINT NOT NULL,
    loser_table_points     SMALLINT NOT NULL,

    CONSTRAINT PK_FORMAT_SCORING_RULE
        PRIMARY KEY (format_scoring_rule_id),

    CONSTRAINT FK_FORMAT_SCORING_RULE_FORMAT
        FOREIGN KEY (competition_format_id)
        REFERENCES dbo.COMPETITION_FORMAT (competition_format_id),

    CONSTRAINT UQ_FORMAT_SCORING_RULE_score
        UNIQUE (competition_format_id, winner_sets, loser_sets),

    CONSTRAINT CK_FORMAT_SCORING_RULE_sets
        CHECK
        (
            winner_sets = 3
            AND loser_sets BETWEEN 0 AND 2
        ),

    CONSTRAINT CK_FORMAT_SCORING_RULE_points
        CHECK (winner_table_points >= 0 AND loser_table_points >= 0)
);
GO

CREATE TABLE dbo.FORMAT_TIEBREAK_RULE
(
    format_tiebreak_rule_id INT IDENTITY(1,1) NOT NULL,
    competition_format_id   INT NOT NULL,

    sequence                SMALLINT NOT NULL,
    criterion               VARCHAR(30) NOT NULL,
    sort_direction          VARCHAR(4) NOT NULL,

    CONSTRAINT PK_FORMAT_TIEBREAK_RULE
        PRIMARY KEY (format_tiebreak_rule_id),

    CONSTRAINT FK_FORMAT_TIEBREAK_RULE_FORMAT
        FOREIGN KEY (competition_format_id)
        REFERENCES dbo.COMPETITION_FORMAT (competition_format_id),

    CONSTRAINT UQ_FORMAT_TIEBREAK_RULE_sequence
        UNIQUE (competition_format_id, sequence),

    CONSTRAINT CK_FORMAT_TIEBREAK_RULE_sequence CHECK (sequence > 0),

    CONSTRAINT CK_FORMAT_TIEBREAK_RULE_criterion
        CHECK
        (
            criterion IN
            ('TABLE_POINTS','MATCH_WINS','SET_RATIO','POINT_RATIO','HEAD_TO_HEAD')
        ),

    CONSTRAINT CK_FORMAT_TIEBREAK_RULE_direction
        CHECK (sort_direction IN ('ASC','DESC'))
);
GO

CREATE TABLE dbo.FORMAT_MOVEMENT_RULE
(
    format_movement_rule_id INT IDENTITY(1,1) NOT NULL,
    competition_format_id   INT NOT NULL,

    movement_type           VARCHAR(20) NOT NULL,
    source_type             VARCHAR(20) NOT NULL,

    source_format_phase_id  INT NULL,
    source_format_group_id  INT NULL,
    source_format_series_id INT NULL,

    from_position           SMALLINT NOT NULL,
    to_position             SMALLINT NOT NULL,

    target_level_delta      SMALLINT NOT NULL,
    applies_if_target_exists BIT NOT NULL
        CONSTRAINT DF_FORMAT_MOVEMENT_RULE_target_exists DEFAULT (1),

    CONSTRAINT PK_FORMAT_MOVEMENT_RULE
        PRIMARY KEY (format_movement_rule_id),

    CONSTRAINT FK_FORMAT_MOVEMENT_RULE_FORMAT
        FOREIGN KEY (competition_format_id)
        REFERENCES dbo.COMPETITION_FORMAT (competition_format_id),

    CONSTRAINT FK_FORMAT_MOVEMENT_RULE_PHASE
        FOREIGN KEY (source_format_phase_id, competition_format_id)
        REFERENCES dbo.FORMAT_PHASE (format_phase_id, competition_format_id),

    CONSTRAINT FK_FORMAT_MOVEMENT_RULE_GROUP
        FOREIGN KEY (source_format_group_id, competition_format_id)
        REFERENCES dbo.FORMAT_GROUP (format_group_id, competition_format_id),

    CONSTRAINT FK_FORMAT_MOVEMENT_RULE_SERIES
        FOREIGN KEY (source_format_series_id, competition_format_id)
        REFERENCES dbo.FORMAT_PLAYOFF_SERIES
                   (format_series_id, competition_format_id),

    CONSTRAINT CK_FORMAT_MOVEMENT_RULE_type
        CHECK (movement_type IN ('PROMOTION','RELEGATION')),

    CONSTRAINT CK_FORMAT_MOVEMENT_RULE_source_type
        CHECK (source_type IN ('PHASE_POSITION','GROUP_POSITION','SERIES_RESULT','PHASE_LAST_N','GROUP_LAST_N')),

    CONSTRAINT CK_FORMAT_MOVEMENT_RULE_positions
        CHECK (from_position > 0 AND to_position >= from_position),

    CONSTRAINT CK_FORMAT_MOVEMENT_RULE_target_delta
        CHECK
        (
            (movement_type = 'PROMOTION'  AND target_level_delta < 0)
            OR
            (movement_type = 'RELEGATION' AND target_level_delta > 0)
        ),

    CONSTRAINT CK_FORMAT_MOVEMENT_RULE_source
        CHECK
        (
            (source_type = 'PHASE_POSITION'
                AND source_format_phase_id IS NOT NULL
                AND source_format_group_id IS NULL
                AND source_format_series_id IS NULL)
            OR
            (source_type = 'GROUP_POSITION'
                AND source_format_phase_id IS NOT NULL
                AND source_format_group_id IS NOT NULL
                AND source_format_series_id IS NULL)
            OR
            (source_type = 'SERIES_RESULT'
                AND source_format_phase_id IS NOT NULL
                AND source_format_group_id IS NULL
                AND source_format_series_id IS NOT NULL)
            OR
            (source_type = 'PHASE_LAST_N'
                AND source_format_phase_id IS NOT NULL
                AND source_format_group_id IS NULL
                AND source_format_series_id IS NULL
                AND from_position = 1
                AND to_position >= 1)
            OR
            (source_type = 'GROUP_LAST_N'
                AND source_format_phase_id IS NOT NULL
                AND source_format_group_id IS NOT NULL
                AND source_format_series_id IS NULL
                AND from_position = 1
                AND to_position >= 1)
        )
);
GO

/* ============================================================================
   4. COMPETENCIA REAL / INSTANCIADA
   ============================================================================ */

CREATE TABLE dbo.COMPETITION
(
    competition_id         INT IDENTITY(1,1) NOT NULL,
    season_id              INT NOT NULL,
    division_id            INT NOT NULL,
    competition_format_id  INT NOT NULL,

    name                   NVARCHAR(150) NOT NULL,
    period_type            VARCHAR(20) NOT NULL,
    start_date             DATE NULL,
    end_date               DATE NULL,
    status                 VARCHAR(20) NOT NULL
        CONSTRAINT DF_COMPETITION_status DEFAULT ('DRAFT'),

    CONSTRAINT PK_COMPETITION PRIMARY KEY (competition_id),

    CONSTRAINT FK_COMPETITION_SEASON
        FOREIGN KEY (season_id) REFERENCES dbo.SEASON (season_id),

    CONSTRAINT FK_COMPETITION_DIVISION
        FOREIGN KEY (division_id) REFERENCES dbo.DIVISION (division_id),

    CONSTRAINT FK_COMPETITION_FORMAT
        FOREIGN KEY (competition_format_id)
        REFERENCES dbo.COMPETITION_FORMAT (competition_format_id),

    CONSTRAINT UQ_COMPETITION_name_season UNIQUE (season_id, name),

    CONSTRAINT CK_COMPETITION_period_type
        CHECK (period_type IN ('ANNUAL','APERTURA','CLAUSURA')),

    CONSTRAINT CK_COMPETITION_status
        CHECK
        (
            status IN ('DRAFT','SCHEDULED','IN_PROGRESS','FINISHED','CANCELLED')
        ),

    CONSTRAINT CK_COMPETITION_dates
        CHECK (end_date IS NULL OR start_date IS NULL OR end_date >= start_date)
);
GO

CREATE TABLE dbo.TEAM_ENTRY
(
    team_entry_id   INT IDENTITY(1,1) NOT NULL,
    competition_id  INT NOT NULL,
    team_id         INT NOT NULL,
    seed            SMALLINT NULL,
    status          VARCHAR(20) NOT NULL
        CONSTRAINT DF_TEAM_ENTRY_status DEFAULT ('REGISTERED'),

    CONSTRAINT PK_TEAM_ENTRY PRIMARY KEY (team_entry_id),

    CONSTRAINT FK_TEAM_ENTRY_COMPETITION
        FOREIGN KEY (competition_id)
        REFERENCES dbo.COMPETITION (competition_id),

    CONSTRAINT FK_TEAM_ENTRY_TEAM
        FOREIGN KEY (team_id)
        REFERENCES dbo.TEAM (team_id),

    CONSTRAINT UQ_TEAM_ENTRY UNIQUE (competition_id, team_id),
    CONSTRAINT UQ_TEAM_ENTRY_id_comp UNIQUE (team_entry_id, competition_id),

    CONSTRAINT CK_TEAM_ENTRY_seed CHECK (seed IS NULL OR seed > 0),

    CONSTRAINT CK_TEAM_ENTRY_status
        CHECK (status IN ('REGISTERED','ACTIVE','WITHDRAWN','DISQUALIFIED'))
);
GO

CREATE TABLE dbo.COMPETITION_PHASE
(
    phase_id          INT IDENTITY(1,1) NOT NULL,
    competition_id    INT NOT NULL,
    format_phase_id   INT NOT NULL,

    code              VARCHAR(30) NOT NULL,
    name              NVARCHAR(100) NOT NULL,
    phase_type        VARCHAR(20) NOT NULL,
    phase_role        VARCHAR(20) NOT NULL,
    sequence          SMALLINT NOT NULL,

    rounds            SMALLINT NULL,
    fixture_mode      VARCHAR(30) NULL,

    status            VARCHAR(20) NOT NULL
        CONSTRAINT DF_COMPETITION_PHASE_status DEFAULT ('PENDING'),

    CONSTRAINT PK_COMPETITION_PHASE PRIMARY KEY (phase_id),

    CONSTRAINT FK_COMPETITION_PHASE_COMPETITION
        FOREIGN KEY (competition_id)
        REFERENCES dbo.COMPETITION (competition_id),

    CONSTRAINT FK_COMPETITION_PHASE_FORMAT_PHASE
        FOREIGN KEY (format_phase_id)
        REFERENCES dbo.FORMAT_PHASE (format_phase_id),

    CONSTRAINT UQ_COMPETITION_PHASE_code
        UNIQUE (competition_id, code),

    CONSTRAINT UQ_COMPETITION_PHASE_id_comp
        UNIQUE (phase_id, competition_id),

    CONSTRAINT CK_COMPETITION_PHASE_type
        CHECK (phase_type IN ('ROUND_ROBIN','GROUP_STAGE','PLAYOFF')),

    CONSTRAINT CK_COMPETITION_PHASE_role
        CHECK
        (
            phase_role IN
            ('REGULAR','CHAMPIONSHIP','RELEGATION',
             'SEMIFINAL','THIRD_PLACE','FINAL')
        ),

    CONSTRAINT CK_COMPETITION_PHASE_status
        CHECK (status IN ('PENDING','IN_PROGRESS','FINISHED','CANCELLED'))
);
GO

CREATE TABLE dbo.PHASE_GROUP
(
    phase_group_id    INT IDENTITY(1,1) NOT NULL,
    competition_id    INT NOT NULL,
    phase_id          INT NOT NULL,
    format_group_id   INT NOT NULL,

    code              VARCHAR(30) NOT NULL,
    name              NVARCHAR(100) NOT NULL,
    group_role        VARCHAR(20) NOT NULL,
    sequence          SMALLINT NOT NULL,

    rounds            SMALLINT NOT NULL,
    fixture_mode      VARCHAR(30) NOT NULL,
    carry_over_mode   VARCHAR(20) NOT NULL,

    CONSTRAINT PK_PHASE_GROUP PRIMARY KEY (phase_group_id),

    CONSTRAINT FK_PHASE_GROUP_PHASE
        FOREIGN KEY (phase_id, competition_id)
        REFERENCES dbo.COMPETITION_PHASE (phase_id, competition_id),

    CONSTRAINT FK_PHASE_GROUP_FORMAT_GROUP
        FOREIGN KEY (format_group_id)
        REFERENCES dbo.FORMAT_GROUP (format_group_id),

    CONSTRAINT UQ_PHASE_GROUP_code
        UNIQUE (phase_id, code),

    CONSTRAINT UQ_PHASE_GROUP_id_comp
        UNIQUE (phase_group_id, competition_id),

    CONSTRAINT CK_PHASE_GROUP_role
        CHECK (group_role IN ('CHAMPIONSHIP','RELEGATION','OTHER')),

    CONSTRAINT CK_PHASE_GROUP_rounds CHECK (rounds > 0),

    CONSTRAINT CK_PHASE_GROUP_fixture_mode
        CHECK (fixture_mode IN ('MIRRORED_HOME_AWAY','BALANCED_RANDOM')),

    CONSTRAINT CK_PHASE_GROUP_carry_over
        CHECK (carry_over_mode IN ('NONE','ALL','QUALIFIED_ONLY'))
);
GO

CREATE TABLE dbo.PHASE_GROUP_ENTRY
(
    phase_group_entry_id INT IDENTITY(1,1) NOT NULL,
    competition_id       INT NOT NULL,
    phase_group_id       INT NOT NULL,
    team_entry_id        INT NOT NULL,

    source_position      SMALLINT NULL,
    seed                 SMALLINT NULL,

    CONSTRAINT PK_PHASE_GROUP_ENTRY PRIMARY KEY (phase_group_entry_id),

    CONSTRAINT FK_PHASE_GROUP_ENTRY_GROUP
        FOREIGN KEY (phase_group_id, competition_id)
        REFERENCES dbo.PHASE_GROUP (phase_group_id, competition_id),

    CONSTRAINT FK_PHASE_GROUP_ENTRY_TEAM
        FOREIGN KEY (team_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),

    CONSTRAINT UQ_PHASE_GROUP_ENTRY
        UNIQUE (phase_group_id, team_entry_id),

    CONSTRAINT CK_PHASE_GROUP_ENTRY_source_position
        CHECK (source_position IS NULL OR source_position > 0),

    CONSTRAINT CK_PHASE_GROUP_ENTRY_seed
        CHECK (seed IS NULL OR seed > 0)
);
GO

CREATE TABLE dbo.PLAYOFF_SERIES
(
    series_id              INT IDENTITY(1,1) NOT NULL,
    competition_id         INT NOT NULL,
    phase_id               INT NOT NULL,
    format_series_id       INT NOT NULL,

    code                   VARCHAR(30) NOT NULL,
    name                   NVARCHAR(100) NOT NULL,
    sequence               SMALLINT NOT NULL,

    team1_entry_id         INT NULL,
    team2_entry_id         INT NULL,

    team1_initial_wins     SMALLINT NOT NULL CONSTRAINT DF_PLAYOFF_SERIES_t1iw DEFAULT (0),
    team2_initial_wins     SMALLINT NOT NULL CONSTRAINT DF_PLAYOFF_SERIES_t2iw DEFAULT (0),
    wins_required          SMALLINT NOT NULL,

    winner_team_entry_id   INT NULL,
    status                 VARCHAR(20) NOT NULL
        CONSTRAINT DF_PLAYOFF_SERIES_status DEFAULT ('PENDING'),

    CONSTRAINT PK_PLAYOFF_SERIES PRIMARY KEY (series_id),

    CONSTRAINT FK_PLAYOFF_SERIES_PHASE
        FOREIGN KEY (phase_id, competition_id)
        REFERENCES dbo.COMPETITION_PHASE (phase_id, competition_id),

    CONSTRAINT FK_PLAYOFF_SERIES_FORMAT
        FOREIGN KEY (format_series_id)
        REFERENCES dbo.FORMAT_PLAYOFF_SERIES (format_series_id),

    CONSTRAINT FK_PLAYOFF_SERIES_TEAM1
        FOREIGN KEY (team1_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),

    CONSTRAINT FK_PLAYOFF_SERIES_TEAM2
        FOREIGN KEY (team2_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),

    CONSTRAINT FK_PLAYOFF_SERIES_WINNER
        FOREIGN KEY (winner_team_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),

    CONSTRAINT UQ_PLAYOFF_SERIES_code
        UNIQUE (phase_id, code),

    CONSTRAINT UQ_PLAYOFF_SERIES_id_comp
        UNIQUE (series_id, competition_id),

    CONSTRAINT CK_PLAYOFF_SERIES_wins_required CHECK (wins_required > 0),

    CONSTRAINT CK_PLAYOFF_SERIES_initial_wins
        CHECK
        (
            team1_initial_wins >= 0
            AND team2_initial_wins >= 0
            AND team1_initial_wins < wins_required
            AND team2_initial_wins < wins_required
        ),

    CONSTRAINT CK_PLAYOFF_SERIES_different_teams
        CHECK
        (
            team1_entry_id IS NULL
            OR team2_entry_id IS NULL
            OR team1_entry_id <> team2_entry_id
        ),

    CONSTRAINT CK_PLAYOFF_SERIES_status
        CHECK (status IN ('PENDING','READY','IN_PROGRESS','FINISHED','CANCELLED'))
);
GO

CREATE TABLE dbo.PLAYOFF_SERIES_PARTICIPANT_SOURCE
(
    series_participant_source_id INT IDENTITY(1,1) NOT NULL,
    competition_id               INT NOT NULL,

    target_series_id             INT NOT NULL,
    target_side                  TINYINT NOT NULL,

    source_type                  VARCHAR(20) NOT NULL,
    source_series_id             INT NOT NULL,

    CONSTRAINT PK_PLAYOFF_SERIES_PARTICIPANT_SOURCE
        PRIMARY KEY (series_participant_source_id),

    CONSTRAINT FK_PLAYOFF_SERIES_SOURCE_TARGET
        FOREIGN KEY (target_series_id, competition_id)
        REFERENCES dbo.PLAYOFF_SERIES (series_id, competition_id),

    CONSTRAINT FK_PLAYOFF_SERIES_SOURCE_SOURCE
        FOREIGN KEY (source_series_id, competition_id)
        REFERENCES dbo.PLAYOFF_SERIES (series_id, competition_id),

    CONSTRAINT UQ_PLAYOFF_SERIES_SOURCE_target_side
        UNIQUE (target_series_id, target_side),

    CONSTRAINT CK_PLAYOFF_SERIES_SOURCE_side
        CHECK (target_side IN (1,2)),

    CONSTRAINT CK_PLAYOFF_SERIES_SOURCE_type
        CHECK (source_type IN ('SERIES_WINNER','SERIES_LOSER')),

    CONSTRAINT CK_PLAYOFF_SERIES_SOURCE_not_same
        CHECK (target_series_id <> source_series_id)
);
GO

CREATE TABLE dbo.MATCH
(
    match_id               INT IDENTITY(1,1) NOT NULL,
    competition_id         INT NOT NULL,
    phase_id               INT NOT NULL,
    phase_group_id         INT NULL,
    series_id              INT NULL,

    home_team_entry_id     INT NULL,
    away_team_entry_id     INT NULL,

    match_date             DATETIME2(0) NULL,
    venue_id               INT NULL,

    round_number           SMALLINT NULL,
    match_number           SMALLINT NULL,

    status                 VARCHAR(20) NOT NULL
        CONSTRAINT DF_MATCH_status DEFAULT ('PENDING'),

    home_sets              TINYINT NULL,
    away_sets              TINYINT NULL,
    winner_team_entry_id   INT NULL,

    CONSTRAINT PK_MATCH PRIMARY KEY (match_id),

    CONSTRAINT UQ_MATCH_id_comp
        UNIQUE (match_id, competition_id),

    CONSTRAINT FK_MATCH_PHASE
        FOREIGN KEY (phase_id, competition_id)
        REFERENCES dbo.COMPETITION_PHASE (phase_id, competition_id),

    CONSTRAINT FK_MATCH_PHASE_GROUP
        FOREIGN KEY (phase_group_id, competition_id)
        REFERENCES dbo.PHASE_GROUP (phase_group_id, competition_id),

    CONSTRAINT FK_MATCH_SERIES
        FOREIGN KEY (series_id, competition_id)
        REFERENCES dbo.PLAYOFF_SERIES (series_id, competition_id),

    CONSTRAINT FK_MATCH_HOME_TEAM
        FOREIGN KEY (home_team_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),

    CONSTRAINT FK_MATCH_AWAY_TEAM
        FOREIGN KEY (away_team_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),

    CONSTRAINT FK_MATCH_WINNER
        FOREIGN KEY (winner_team_entry_id, competition_id)
        REFERENCES dbo.TEAM_ENTRY (team_entry_id, competition_id),

    CONSTRAINT FK_MATCH_VENUE
        FOREIGN KEY (venue_id)
        REFERENCES dbo.VENUE (venue_id),

    CONSTRAINT CK_MATCH_round_number
        CHECK (round_number IS NULL OR round_number > 0),

    CONSTRAINT CK_MATCH_match_number
        CHECK (match_number IS NULL OR match_number > 0),

    CONSTRAINT CK_MATCH_status
        CHECK
        (
            status IN
            ('PENDING','SCHEDULED','IN_PROGRESS',
             'FINISHED','SUSPENDED','CANCELLED')
        ),

    CONSTRAINT CK_MATCH_sets
        CHECK
        (
            (home_sets IS NULL AND away_sets IS NULL)
            OR
            (
                home_sets BETWEEN 0 AND 3
                AND away_sets BETWEEN 0 AND 3
                AND NOT (home_sets = 3 AND away_sets = 3)
            )
        ),

    CONSTRAINT CK_MATCH_different_teams
        CHECK
        (
            home_team_entry_id IS NULL
            OR away_team_entry_id IS NULL
            OR home_team_entry_id <> away_team_entry_id
        ),

    CONSTRAINT CK_MATCH_group_or_series
        CHECK
        (
            NOT (phase_group_id IS NOT NULL AND series_id IS NOT NULL)
        )
);
GO

CREATE TABLE dbo.MATCH_SET
(
    match_set_id   INT IDENTITY(1,1) NOT NULL,
    match_id       INT NOT NULL,
    set_number     TINYINT NOT NULL,
    home_points    SMALLINT NOT NULL,
    away_points    SMALLINT NOT NULL,

    CONSTRAINT PK_MATCH_SET PRIMARY KEY (match_set_id),

    CONSTRAINT FK_MATCH_SET_MATCH
        FOREIGN KEY (match_id)
        REFERENCES dbo.MATCH (match_id),

    CONSTRAINT UQ_MATCH_SET UNIQUE (match_id, set_number),

    CONSTRAINT CK_MATCH_SET_number CHECK (set_number BETWEEN 1 AND 5),

    CONSTRAINT CK_MATCH_SET_points
        CHECK (home_points >= 0 AND away_points >= 0)
);
GO

/*
MATCH_PARTICIPANT_SOURCE permite precrear partidos cuyos participantes todavía
dependen de una posición o de una serie previa. Se usan columnas FK explícitas
en lugar de un source_id polimórfico para preservar integridad referencial.
*/
CREATE TABLE dbo.MATCH_PARTICIPANT_SOURCE
(
    match_participant_source_id INT IDENTITY(1,1) NOT NULL,
    competition_id              INT NOT NULL,
    match_id                    INT NOT NULL,

    side                        VARCHAR(4) NOT NULL,
    source_type                 VARCHAR(20) NOT NULL,

    source_phase_id             INT NULL,
    source_group_id             INT NULL,
    source_series_id            INT NULL,
    source_position             SMALLINT NULL,

    CONSTRAINT PK_MATCH_PARTICIPANT_SOURCE
        PRIMARY KEY (match_participant_source_id),

    CONSTRAINT FK_MATCH_PARTICIPANT_SOURCE_MATCH
        FOREIGN KEY (match_id, competition_id)
        REFERENCES dbo.MATCH (match_id, competition_id),

    CONSTRAINT FK_MATCH_PARTICIPANT_SOURCE_PHASE
        FOREIGN KEY (source_phase_id, competition_id)
        REFERENCES dbo.COMPETITION_PHASE (phase_id, competition_id),

    CONSTRAINT FK_MATCH_PARTICIPANT_SOURCE_GROUP
        FOREIGN KEY (source_group_id, competition_id)
        REFERENCES dbo.PHASE_GROUP (phase_group_id, competition_id),

    CONSTRAINT FK_MATCH_PARTICIPANT_SOURCE_SERIES
        FOREIGN KEY (source_series_id, competition_id)
        REFERENCES dbo.PLAYOFF_SERIES (series_id, competition_id),

    CONSTRAINT UQ_MATCH_PARTICIPANT_SOURCE_side
        UNIQUE (match_id, side),

    CONSTRAINT CK_MATCH_PARTICIPANT_SOURCE_side
        CHECK (side IN ('HOME','AWAY')),

    CONSTRAINT CK_MATCH_PARTICIPANT_SOURCE_type
        CHECK (source_type IN ('POSITION','SERIES_WINNER','SERIES_LOSER')),

    CONSTRAINT CK_MATCH_PARTICIPANT_SOURCE_position
        CHECK (source_position IS NULL OR source_position > 0),

    CONSTRAINT CK_MATCH_PARTICIPANT_SOURCE_source
        CHECK
        (
            (
                source_type = 'POSITION'
                AND source_phase_id IS NOT NULL
                AND source_series_id IS NULL
                AND source_position IS NOT NULL
            )
            OR
            (
                source_type IN ('SERIES_WINNER','SERIES_LOSER')
                AND source_phase_id IS NULL
                AND source_group_id IS NULL
                AND source_series_id IS NOT NULL
                AND source_position IS NULL
            )
        )
);
GO

/* ============================================================================
   5. ÍNDICES ÚTILES
   ============================================================================ */

CREATE INDEX IX_TEAM_ENTRY_competition
    ON dbo.TEAM_ENTRY (competition_id, status);
GO

CREATE INDEX IX_COMPETITION_PHASE_competition_sequence
    ON dbo.COMPETITION_PHASE (competition_id, sequence);
GO

CREATE INDEX IX_PHASE_GROUP_ENTRY_group
    ON dbo.PHASE_GROUP_ENTRY (phase_group_id, team_entry_id);
GO

CREATE INDEX IX_MATCH_competition_date
    ON dbo.MATCH (competition_id, match_date);
GO

CREATE INDEX IX_MATCH_phase_round
    ON dbo.MATCH (phase_id, round_number);
GO

CREATE INDEX IX_MATCH_series
    ON dbo.MATCH (series_id)
    WHERE series_id IS NOT NULL;
GO

CREATE INDEX IX_MATCH_SET_match
    ON dbo.MATCH_SET (match_id, set_number);
GO

/* ============================================================================
   6. DATOS DE EJEMPLO / FORMATOS BASE
   Se insertan solamente si todavía no existen.
   ============================================================================ */

IF NOT EXISTS (SELECT 1 FROM dbo.COMPETITION_FORMAT WHERE code = 'RR_6_8')
BEGIN
    INSERT INTO dbo.COMPETITION_FORMAT
        (code, name, description, min_teams, max_teams)
    VALUES
        ('RR_6_8',
         N'Ida y vuelta + Playoffs',
         N'6 a 8 equipos: todos contra todos ida y vuelta, semifinales con ventaja, tercer puesto y final.',
         6, 8);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.COMPETITION_FORMAT WHERE code = 'GROUP_9_12')
BEGIN
    INSERT INTO dbo.COMPETITION_FORMAT
        (code, name, description, min_teams, max_teams)
    VALUES
        ('GROUP_9_12',
         N'Rueda + Grupos + Playoffs',
         N'9 a 12 equipos: primera rueda, grupos Campeonato/Permanencia y playoffs.',
         9, 12);
END;
GO

/* ----- Formato 6-8 ------------------------------------------------------- */

DECLARE @Format8 INT =
(
    SELECT competition_format_id
    FROM dbo.COMPETITION_FORMAT
    WHERE code = 'RR_6_8'
);

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format8
)
BEGIN
    INSERT INTO dbo.FORMAT_PHASE
        (competition_format_id, code, name, phase_type, phase_role,
         sequence, rounds, fixture_mode)
    VALUES
        (@Format8, 'REGULAR', N'Fase Regular',
         'ROUND_ROBIN', 'REGULAR', 1, 2, 'MIRRORED_HOME_AWAY'),

        (@Format8, 'SF', N'Semifinales',
         'PLAYOFF', 'SEMIFINAL', 2, NULL, 'PLAYOFF'),

        (@Format8, 'THIRD', N'Tercer Puesto',
         'PLAYOFF', 'THIRD_PLACE', 3, NULL, 'PLAYOFF'),

        (@Format8, 'FINAL', N'Final',
         'PLAYOFF', 'FINAL', 3, NULL, 'PLAYOFF');
END;

DECLARE @Regular8 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format8 AND code = 'REGULAR'
);
DECLARE @SF8 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format8 AND code = 'SF'
);
DECLARE @Third8 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format8 AND code = 'THIRD'
);
DECLARE @Final8 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format8 AND code = 'FINAL'
);

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format8
)
BEGIN
    INSERT INTO dbo.FORMAT_PLAYOFF_SERIES
        (competition_format_id, format_phase_id, code, name, sequence,
         wins_required, team1_initial_wins, team2_initial_wins)
    VALUES
        (@Format8, @SF8, 'SF1', N'Semifinal 1', 1, 2, 1, 0),
        (@Format8, @SF8, 'SF2', N'Semifinal 2', 2, 2, 1, 0),
        (@Format8, @Third8, 'THIRD', N'Tercer Puesto', 1, 1, 0, 0),
        (@Format8, @Final8, 'FINAL', N'Final', 1, 1, 0, 0);
END;

DECLARE @SF1_8 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format8 AND code = 'SF1'
);
DECLARE @SF2_8 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format8 AND code = 'SF2'
);
DECLARE @ThirdSeries8 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format8 AND code = 'THIRD'
);
DECLARE @FinalSeries8 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format8 AND code = 'FINAL'
);

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_QUALIFICATION_RULE
    WHERE competition_format_id = @Format8
)
BEGIN
    INSERT INTO dbo.FORMAT_QUALIFICATION_RULE
        (competition_format_id,
         source_format_phase_id, source_format_group_id,
         selection_mode, from_position, to_position,
         target_type, target_format_phase_id, target_format_group_id,
         target_format_series_id, target_side, sequence)
    VALUES
        (@Format8, @Regular8, NULL, 'POSITION_RANGE', 1, 1, 'SERIES', @SF8, NULL, @SF1_8, 1, 1),
        (@Format8, @Regular8, NULL, 'POSITION_RANGE', 4, 4, 'SERIES', @SF8, NULL, @SF1_8, 2, 2),
        (@Format8, @Regular8, NULL, 'POSITION_RANGE', 2, 2, 'SERIES', @SF8, NULL, @SF2_8, 1, 3),
        (@Format8, @Regular8, NULL, 'POSITION_RANGE', 3, 3, 'SERIES', @SF8, NULL, @SF2_8, 2, 4);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_SERIES_PARTICIPANT_SOURCE
    WHERE competition_format_id = @Format8
)
BEGIN
    INSERT INTO dbo.FORMAT_SERIES_PARTICIPANT_SOURCE
        (competition_format_id, target_format_series_id, target_side,
         source_type, source_format_series_id)
    VALUES
        (@Format8, @FinalSeries8, 1, 'SERIES_WINNER', @SF1_8),
        (@Format8, @FinalSeries8, 2, 'SERIES_WINNER', @SF2_8),
        (@Format8, @ThirdSeries8, 1, 'SERIES_LOSER', @SF1_8),
        (@Format8, @ThirdSeries8, 2, 'SERIES_LOSER', @SF2_8);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_SCORING_RULE
    WHERE competition_format_id = @Format8
)
BEGIN
    INSERT INTO dbo.FORMAT_SCORING_RULE
        (competition_format_id, winner_sets, loser_sets,
         winner_table_points, loser_table_points)
    VALUES
        (@Format8, 3, 0, 3, 0),
        (@Format8, 3, 1, 3, 0),
        (@Format8, 3, 2, 2, 1);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_TIEBREAK_RULE
    WHERE competition_format_id = @Format8
)
BEGIN
    INSERT INTO dbo.FORMAT_TIEBREAK_RULE
        (competition_format_id, sequence, criterion, sort_direction)
    VALUES
        (@Format8, 1, 'TABLE_POINTS', 'DESC'),
        (@Format8, 2, 'MATCH_WINS', 'DESC'),
        (@Format8, 3, 'SET_RATIO', 'DESC'),
        (@Format8, 4, 'POINT_RATIO', 'DESC'),
        (@Format8, 5, 'HEAD_TO_HEAD', 'DESC');
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_MOVEMENT_RULE
    WHERE competition_format_id = @Format8
)
BEGIN
    INSERT INTO dbo.FORMAT_MOVEMENT_RULE
        (competition_format_id, movement_type, source_type,
         source_format_phase_id, source_format_group_id, source_format_series_id,
         from_position, to_position, target_level_delta)
    VALUES
        /* Final: posición 1 = campeón, 2 = subcampeón */
        (@Format8, 'PROMOTION', 'SERIES_RESULT',
         @Final8, NULL, @FinalSeries8, 1, 2, -1),

        /* PHASE_LAST_N: from_position=1 / to_position=2 significa "últimos 2". */
        (@Format8, 'RELEGATION', 'PHASE_LAST_N',
         @Regular8, NULL, NULL, 1, 2, 1);
END;
GO

/* ----- Formato 9-12 ------------------------------------------------------ */

DECLARE @Format10 INT =
(
    SELECT competition_format_id
    FROM dbo.COMPETITION_FORMAT
    WHERE code = 'GROUP_9_12'
);

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format10
)
BEGIN
    INSERT INTO dbo.FORMAT_PHASE
        (competition_format_id, code, name, phase_type, phase_role,
         sequence, rounds, fixture_mode)
    VALUES
        (@Format10, 'FIRST', N'Primera Rueda',
         'ROUND_ROBIN', 'REGULAR', 1, 1, 'BALANCED_RANDOM'),

        (@Format10, 'SECOND', N'Segunda Fase',
         'GROUP_STAGE', 'REGULAR', 2, NULL, NULL),

        (@Format10, 'SF', N'Semifinales',
         'PLAYOFF', 'SEMIFINAL', 3, NULL, 'PLAYOFF'),

        (@Format10, 'THIRD', N'Tercer Puesto',
         'PLAYOFF', 'THIRD_PLACE', 4, NULL, 'PLAYOFF'),

        (@Format10, 'FINAL', N'Final',
         'PLAYOFF', 'FINAL', 4, NULL, 'PLAYOFF');
END;

DECLARE @First10 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format10 AND code = 'FIRST'
);
DECLARE @Second10 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format10 AND code = 'SECOND'
);
DECLARE @SF10 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format10 AND code = 'SF'
);
DECLARE @Third10 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format10 AND code = 'THIRD'
);
DECLARE @Final10 INT =
(
    SELECT format_phase_id FROM dbo.FORMAT_PHASE
    WHERE competition_format_id = @Format10 AND code = 'FINAL'
);

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_GROUP
    WHERE competition_format_id = @Format10
)
BEGIN
    INSERT INTO dbo.FORMAT_GROUP
        (competition_format_id, format_phase_id,
         code, name, group_role, sequence,
         rounds, fixture_mode, carry_over_mode)
    VALUES
        (@Format10, @Second10,
         'CHAMP', N'Campeonato', 'CHAMPIONSHIP', 1,
         1, 'BALANCED_RANDOM', 'NONE'),

        (@Format10, @Second10,
         'RELEG', N'Permanencia', 'RELEGATION', 2,
         1, 'BALANCED_RANDOM', 'NONE');
END;

DECLARE @Champ10 INT =
(
    SELECT format_group_id FROM dbo.FORMAT_GROUP
    WHERE competition_format_id = @Format10 AND code = 'CHAMP'
);
DECLARE @Releg10 INT =
(
    SELECT format_group_id FROM dbo.FORMAT_GROUP
    WHERE competition_format_id = @Format10 AND code = 'RELEG'
);

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format10
)
BEGIN
    INSERT INTO dbo.FORMAT_PLAYOFF_SERIES
        (competition_format_id, format_phase_id, code, name, sequence,
         wins_required, team1_initial_wins, team2_initial_wins)
    VALUES
        (@Format10, @SF10, 'SF1', N'Semifinal 1', 1, 2, 1, 0),
        (@Format10, @SF10, 'SF2', N'Semifinal 2', 2, 2, 1, 0),
        (@Format10, @Third10, 'THIRD', N'Tercer Puesto', 1, 1, 0, 0),
        (@Format10, @Final10, 'FINAL', N'Final', 1, 1, 0, 0);
END;

DECLARE @SF1_10 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format10 AND code = 'SF1'
);
DECLARE @SF2_10 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format10 AND code = 'SF2'
);
DECLARE @ThirdSeries10 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format10 AND code = 'THIRD'
);
DECLARE @FinalSeries10 INT =
(
    SELECT format_series_id FROM dbo.FORMAT_PLAYOFF_SERIES
    WHERE competition_format_id = @Format10 AND code = 'FINAL'
);

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_QUALIFICATION_RULE
    WHERE competition_format_id = @Format10
)
BEGIN
    /* Regla general adoptada para 9-12:
       mitad superior -> Campeonato, mitad inferior -> Permanencia.
       Si el total es impar, el grupo Campeonato recibe el equipo adicional.
       Para 10 equipos produce exactamente 1-5 y 6-10. */
    INSERT INTO dbo.FORMAT_QUALIFICATION_RULE
        (competition_format_id,
         source_format_phase_id, source_format_group_id,
         selection_mode, from_position, to_position,
         target_type, target_format_phase_id, target_format_group_id,
         target_format_series_id, target_side, sequence)
    VALUES
        /* TOP_HALF: mitad superior; si la cantidad es impar, el grupo superior
           recibe el equipo adicional. Ej.: 9 -> 5/4, 10 -> 5/5, 11 -> 6/5. */
        (@Format10, @First10, NULL, 'TOP_HALF', NULL, NULL,
         'GROUP', @Second10, @Champ10, NULL, NULL, 1),

        (@Format10, @First10, NULL, 'BOTTOM_HALF', NULL, NULL,
         'GROUP', @Second10, @Releg10, NULL, NULL, 2),

        (@Format10, @Second10, @Champ10, 'POSITION_RANGE', 1, 1,
         'SERIES', @SF10, NULL, @SF1_10, 1, 3),

        (@Format10, @Second10, @Champ10, 'POSITION_RANGE', 4, 4,
         'SERIES', @SF10, NULL, @SF1_10, 2, 4),

        (@Format10, @Second10, @Champ10, 'POSITION_RANGE', 2, 2,
         'SERIES', @SF10, NULL, @SF2_10, 1, 5),

        (@Format10, @Second10, @Champ10, 'POSITION_RANGE', 3, 3,
         'SERIES', @SF10, NULL, @SF2_10, 2, 6);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_SERIES_PARTICIPANT_SOURCE
    WHERE competition_format_id = @Format10
)
BEGIN
    INSERT INTO dbo.FORMAT_SERIES_PARTICIPANT_SOURCE
        (competition_format_id, target_format_series_id, target_side,
         source_type, source_format_series_id)
    VALUES
        (@Format10, @FinalSeries10, 1, 'SERIES_WINNER', @SF1_10),
        (@Format10, @FinalSeries10, 2, 'SERIES_WINNER', @SF2_10),
        (@Format10, @ThirdSeries10, 1, 'SERIES_LOSER', @SF1_10),
        (@Format10, @ThirdSeries10, 2, 'SERIES_LOSER', @SF2_10);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_SCORING_RULE
    WHERE competition_format_id = @Format10
)
BEGIN
    INSERT INTO dbo.FORMAT_SCORING_RULE
        (competition_format_id, winner_sets, loser_sets,
         winner_table_points, loser_table_points)
    VALUES
        (@Format10, 3, 0, 3, 0),
        (@Format10, 3, 1, 3, 0),
        (@Format10, 3, 2, 2, 1);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_TIEBREAK_RULE
    WHERE competition_format_id = @Format10
)
BEGIN
    INSERT INTO dbo.FORMAT_TIEBREAK_RULE
        (competition_format_id, sequence, criterion, sort_direction)
    VALUES
        (@Format10, 1, 'TABLE_POINTS', 'DESC'),
        (@Format10, 2, 'MATCH_WINS', 'DESC'),
        (@Format10, 3, 'SET_RATIO', 'DESC'),
        (@Format10, 4, 'POINT_RATIO', 'DESC'),
        (@Format10, 5, 'HEAD_TO_HEAD', 'DESC');
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.FORMAT_MOVEMENT_RULE
    WHERE competition_format_id = @Format10
)
BEGIN
    INSERT INTO dbo.FORMAT_MOVEMENT_RULE
        (competition_format_id, movement_type, source_type,
         source_format_phase_id, source_format_group_id, source_format_series_id,
         from_position, to_position, target_level_delta)
    VALUES
        (@Format10, 'PROMOTION', 'SERIES_RESULT',
         @Final10, NULL, @FinalSeries10, 1, 2, -1),

        /* GROUP_LAST_N: últimos 2 del grupo Permanencia, cualquiera sea su tamaño. */
        (@Format10, 'RELEGATION', 'GROUP_LAST_N',
         @Second10, @Releg10, NULL, 1, 2, 1);
END;
GO

/* ============================================================================
   7. CONSULTAS DE VALIDACIÓN
   ============================================================================ */

SELECT
    cf.code                    AS format_code,
    cf.name                    AS format_name,
    fp.sequence                AS phase_sequence,
    fp.code                    AS phase_code,
    fp.name                    AS phase_name,
    fp.phase_type,
    fp.phase_role,
    fp.rounds,
    fp.fixture_mode
FROM dbo.COMPETITION_FORMAT cf
JOIN dbo.FORMAT_PHASE fp
  ON fp.competition_format_id = cf.competition_format_id
ORDER BY cf.code, fp.sequence, fp.format_phase_id;
GO

SELECT
    cf.code AS format_code,
    fg.code AS group_code,
    fg.name AS group_name,
    fg.group_role,
    fg.rounds,
    fg.fixture_mode,
    fg.carry_over_mode
FROM dbo.COMPETITION_FORMAT cf
JOIN dbo.FORMAT_GROUP fg
  ON fg.competition_format_id = cf.competition_format_id
ORDER BY cf.code, fg.sequence;
GO

SELECT
    cf.code AS format_code,
    fps.code AS series_code,
    fps.name AS series_name,
    fps.wins_required,
    fps.team1_initial_wins,
    fps.team2_initial_wins
FROM dbo.COMPETITION_FORMAT cf
JOIN dbo.FORMAT_PLAYOFF_SERIES fps
  ON fps.competition_format_id = cf.competition_format_id
ORDER BY cf.code, fps.format_phase_id, fps.sequence;
GO

SELECT
    cf.code AS format_code,
    fqr.sequence,
    sfp.code AS source_phase,
    sfg.code AS source_group,
    fqr.selection_mode,
    fqr.from_position,
    fqr.to_position,
    fqr.target_type,
    tfp.code AS target_phase,
    tfg.code AS target_group,
    tfs.code AS target_series,
    fqr.target_side
FROM dbo.FORMAT_QUALIFICATION_RULE fqr
JOIN dbo.COMPETITION_FORMAT cf
  ON cf.competition_format_id = fqr.competition_format_id
JOIN dbo.FORMAT_PHASE sfp
  ON sfp.format_phase_id = fqr.source_format_phase_id
LEFT JOIN dbo.FORMAT_GROUP sfg
  ON sfg.format_group_id = fqr.source_format_group_id
LEFT JOIN dbo.FORMAT_PHASE tfp
  ON tfp.format_phase_id = fqr.target_format_phase_id
LEFT JOIN dbo.FORMAT_GROUP tfg
  ON tfg.format_group_id = fqr.target_format_group_id
LEFT JOIN dbo.FORMAT_PLAYOFF_SERIES tfs
  ON tfs.format_series_id = fqr.target_format_series_id
ORDER BY cf.code, fqr.sequence;
GO
