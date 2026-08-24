/*
===============================================================================
Liga Volley - Datos de ejemplo basados en LIVOSUR / Data Project
Archivo: LigaVolley_Datos_Ejemplo_LIVOSUR_2026.sql
Motor : Microsoft SQL Server

REQUISITO
---------
Ejecutar previamente:
    Modelo base entidades y formatos de competicion.sql

FUENTES CONSULTADAS
-------------------
Caso 8 equipos:
https://livosur-web.dataproject.com/CompetitionMatches.aspx?ID=275
https://livosur-web.dataproject.com/CompetitionStandings.aspx?ID=275

Caso 10 equipos:
https://livosur-web.dataproject.com/CompetitionStandings.aspx?ID=267&PID=275
https://livosur-web.dataproject.com/CompetitionMatches.aspx?ID=267&PID=275

CRITERIO
--------
- Se usan nombres, fechas, sedes y resultados que aparecen públicamente en las
  páginas de ejemplo.
- Se cargan solamente partidos visibles/confirmables en esas páginas.
- MATCH_SET no se popula porque esas vistas no publican el tanteador de cada set.
- No se cargan árbitros, jugadores ni técnicos porque pertenecen a módulos que
  todavía no forman parte del modelo base.
- Los equipos se crean con club_id = NULL porque la relación club/equipo no se
  puede afirmar con seguridad desde las vistas utilizadas.
- Se instancian todas las fases y series definidas por los formatos base aunque
  las fases futuras todavía no tengan participantes.
- División "H" usa level_order = 8 como convención técnica de este dataset de
  prueba (H -> 8); no se presenta como dato obtenido de LIVOSUR.
===============================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ========================================================================
       1. TEMPORADA Y DIVISIONALES
       ======================================================================== */

    IF NOT EXISTS (SELECT 1 FROM dbo.SEASON WHERE [year] = 2026)
    BEGIN
        INSERT INTO dbo.SEASON
            ([year], name, start_date, end_date, active)
        VALUES
            (2026, N'Temporada 2026', '2026-01-01', '2026-12-31', 1);
    END;

    DECLARE @Season2026 INT =
    (
        SELECT season_id
        FROM dbo.SEASON
        WHERE [year] = 2026
    );

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DIVISION
        WHERE name = N'Primera' AND gender = 'F'
    )
    BEGIN
        INSERT INTO dbo.DIVISION
            (name, level_order, gender, active)
        VALUES
            (N'Primera', 1, 'F', 1);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.DIVISION
        WHERE name = N'H' AND gender = 'F'
    )
    BEGIN
        INSERT INTO dbo.DIVISION
            (name, level_order, gender, active)
        VALUES
            (N'H', 8, 'F', 1);
    END;

    DECLARE @DivisionPrimeraF INT =
    (
        SELECT division_id
        FROM dbo.DIVISION
        WHERE name = N'Primera' AND gender = 'F'
    );

    DECLARE @DivisionHF INT =
    (
        SELECT division_id
        FROM dbo.DIVISION
        WHERE name = N'H' AND gender = 'F'
    );

    /* ========================================================================
       2. EQUIPOS
       ======================================================================== */

    DECLARE @Teams TABLE
    (
        team_name NVARCHAR(150) NOT NULL PRIMARY KEY
    );

    INSERT INTO @Teams (team_name)
    VALUES
        /* Competencia ID=275, 8 equipos */
        (N'ARRIETA - CDV 33'),
        (N'ATENAS'),
        (N'CBPS ROJO'),
        (N'COLEGIO SAN PABLO'),
        (N'LEGENDS'),
        (N'SANTA VOLEY'),
        (N'SJC - PIBAS'),
        (N'ZONA 3'),

        /* Competencia ID=267, 10 equipos */
        (N'ARRIETA - CDV'),
        (N'BOHEMIOS'),
        (N'CBR A'),
        (N'CBR B'),
        (N'DEFENSOR DE MALDONADO'),
        (N'JUAN FERREIRA'),
        (N'NACIONAL'),
        (N'NACIONAL B'),
        (N'OLIMPIA'),
        (N'PLAZA HELVETICO');

    INSERT INTO dbo.TEAM (club_id, name, gender, active)
    SELECT NULL, t.team_name, 'F', 1
    FROM @Teams t
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.TEAM x
        WHERE x.name = t.team_name
          AND x.gender = 'F'
    );

    /* ========================================================================
       3. SEDES VISIBLES EN LOS FIXTURES
       ======================================================================== */

    DECLARE @Venues TABLE
    (
        venue_name NVARCHAR(150) NOT NULL PRIMARY KEY
    );

    INSERT INTO @Venues (venue_name)
    VALUES
        (N'UGAB'),
        (N'Gimnasio Juan XXIII'),
        (N'Gim. C. A. Atenas'),
        (N'Zona 3'),
        (N'CDV 33'),
        (N'Gimnasio Club BPS'),
        (N'PLAZA NUEVA HELVECIA'),
        (N'Gim "A" BOHEMIOS'),
        (N'Polideportivo');

    INSERT INTO dbo.VENUE (name, address, active)
    SELECT v.venue_name, NULL, 1
    FROM @Venues v
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.VENUE x WHERE x.name = v.venue_name
    );

    /* ========================================================================
       4. FORMATOS BASE
       ======================================================================== */

    DECLARE @Format8 INT =
    (
        SELECT competition_format_id
        FROM dbo.COMPETITION_FORMAT
        WHERE code = 'RR_6_8'
    );

    DECLARE @Format10 INT =
    (
        SELECT competition_format_id
        FROM dbo.COMPETITION_FORMAT
        WHERE code = 'GROUP_9_12'
    );

    IF @Format8 IS NULL
        THROW 50001, 'No existe COMPETITION_FORMAT RR_6_8. Ejecute primero el script base.', 1;

    IF @Format10 IS NULL
        THROW 50002, 'No existe COMPETITION_FORMAT GROUP_9_12. Ejecute primero el script base.', 1;

    /* ========================================================================
       5. COMPETENCIA EJEMPLO DE 8 EQUIPOS - ID LIVOSUR 275
       CLAUSURA 2026 - FEMENINO H
       ======================================================================== */

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.COMPETITION
        WHERE season_id = @Season2026
          AND name = N'Clausura 2026 - Femenino H [LIVOSUR 275]'
    )
    BEGIN
        INSERT INTO dbo.COMPETITION
        (
            season_id,
            division_id,
            competition_format_id,
            name,
            period_type,
            start_date,
            end_date,
            status
        )
        VALUES
        (
            @Season2026,
            @DivisionHF,
            @Format8,
            N'Clausura 2026 - Femenino H [LIVOSUR 275]',
            'CLAUSURA',
            '2026-08-15',
            NULL,
            'IN_PROGRESS'
        );
    END;

    DECLARE @Competition8 INT =
    (
        SELECT competition_id
        FROM dbo.COMPETITION
        WHERE season_id = @Season2026
          AND name = N'Clausura 2026 - Femenino H [LIVOSUR 275]'
    );

    /* Inscripciones */
    DECLARE @Teams8 TABLE (team_name NVARCHAR(150) NOT NULL PRIMARY KEY);
    INSERT INTO @Teams8 VALUES
        (N'ARRIETA - CDV 33'),
        (N'ATENAS'),
        (N'CBPS ROJO'),
        (N'COLEGIO SAN PABLO'),
        (N'LEGENDS'),
        (N'SANTA VOLEY'),
        (N'SJC - PIBAS'),
        (N'ZONA 3');

    INSERT INTO dbo.TEAM_ENTRY
        (competition_id, team_id, seed, status)
    SELECT
        @Competition8,
        t.team_id,
        NULL,
        'ACTIVE'
    FROM dbo.TEAM t
    JOIN @Teams8 x
      ON x.team_name = t.name
    WHERE t.gender = 'F'
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.TEAM_ENTRY te
          WHERE te.competition_id = @Competition8
            AND te.team_id = t.team_id
      );

    /* Instanciación de fases del formato */
    INSERT INTO dbo.COMPETITION_PHASE
    (
        competition_id,
        format_phase_id,
        code,
        name,
        phase_type,
        phase_role,
        sequence,
        rounds,
        fixture_mode,
        status
    )
    SELECT
        @Competition8,
        fp.format_phase_id,
        fp.code,
        fp.name,
        fp.phase_type,
        fp.phase_role,
        fp.sequence,
        fp.rounds,
        fp.fixture_mode,
        CASE WHEN fp.code = 'REGULAR' THEN 'IN_PROGRESS' ELSE 'PENDING' END
    FROM dbo.FORMAT_PHASE fp
    WHERE fp.competition_format_id = @Format8
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.COMPETITION_PHASE cp
          WHERE cp.competition_id = @Competition8
            AND cp.code = fp.code
      );

    /* Instanciación de series futuras */
    INSERT INTO dbo.PLAYOFF_SERIES
    (
        competition_id,
        phase_id,
        format_series_id,
        code,
        name,
        sequence,
        team1_entry_id,
        team2_entry_id,
        team1_initial_wins,
        team2_initial_wins,
        wins_required,
        winner_team_entry_id,
        status
    )
    SELECT
        @Competition8,
        cp.phase_id,
        fps.format_series_id,
        fps.code,
        fps.name,
        fps.sequence,
        NULL,
        NULL,
        fps.team1_initial_wins,
        fps.team2_initial_wins,
        fps.wins_required,
        NULL,
        'PENDING'
    FROM dbo.FORMAT_PLAYOFF_SERIES fps
    JOIN dbo.COMPETITION_PHASE cp
      ON cp.competition_id = @Competition8
     AND cp.format_phase_id = fps.format_phase_id
    WHERE fps.competition_format_id = @Format8
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.PLAYOFF_SERIES ps
          WHERE ps.competition_id = @Competition8
            AND ps.code = fps.code
      );

    /* Encadenamiento final/tercer puesto desde ganadores/perdedores de semifinal */
    INSERT INTO dbo.PLAYOFF_SERIES_PARTICIPANT_SOURCE
    (
        competition_id,
        target_series_id,
        target_side,
        source_type,
        source_series_id
    )
    SELECT
        @Competition8,
        target_real.series_id,
        src.target_side,
        src.source_type,
        source_real.series_id
    FROM dbo.FORMAT_SERIES_PARTICIPANT_SOURCE src
    JOIN dbo.PLAYOFF_SERIES target_real
      ON target_real.competition_id = @Competition8
    JOIN dbo.FORMAT_PLAYOFF_SERIES target_fmt
      ON target_fmt.format_series_id = src.target_format_series_id
     AND target_real.format_series_id = target_fmt.format_series_id
    JOIN dbo.PLAYOFF_SERIES source_real
      ON source_real.competition_id = @Competition8
    JOIN dbo.FORMAT_PLAYOFF_SERIES source_fmt
      ON source_fmt.format_series_id = src.source_format_series_id
     AND source_real.format_series_id = source_fmt.format_series_id
    WHERE src.competition_format_id = @Format8
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.PLAYOFF_SERIES_PARTICIPANT_SOURCE x
          WHERE x.target_series_id = target_real.series_id
            AND x.target_side = src.target_side
      );

    DECLARE @RegularPhase8 INT =
    (
        SELECT phase_id
        FROM dbo.COMPETITION_PHASE
        WHERE competition_id = @Competition8 AND code = 'REGULAR'
    );

    /* ========================================================================
       6. PARTIDOS VISIBLES DEL CASO DE 8 EQUIPOS
       Fuente: CompetitionMatches.aspx?ID=275
       ======================================================================== */

    DECLARE @Matches8 TABLE
    (
        round_number   SMALLINT,
        match_number   SMALLINT,
        match_date     DATETIME2(0),
        venue_name     NVARCHAR(150),
        home_team      NVARCHAR(150),
        away_team      NVARCHAR(150),
        home_sets      TINYINT,
        away_sets      TINYINT
    );

    INSERT INTO @Matches8 VALUES
        (1,1,'2026-08-15T18:00:00',N'UGAB',
            N'LEGENDS',N'SJC - PIBAS',3,0),

        (1,2,'2026-08-16T17:00:00',N'Gimnasio Juan XXIII',
            N'SANTA VOLEY',N'CBPS ROJO',3,0),

        (1,3,'2026-08-16T19:00:00',N'Gim. C. A. Atenas',
            N'ATENAS',N'COLEGIO SAN PABLO',0,3),

        (1,4,'2026-08-16T19:00:00',N'Zona 3',
            N'ZONA 3',N'ARRIETA - CDV 33',3,2),

        (2,1,'2026-08-22T18:00:00',N'Gimnasio Juan XXIII',
            N'SANTA VOLEY',N'ZONA 3',3,0),

        (2,2,'2026-08-22T19:15:00',N'UGAB',
            N'COLEGIO SAN PABLO',N'LEGENDS',3,1),

        (2,3,'2026-08-23T16:00:00',N'CDV 33',
            N'ARRIETA - CDV 33',N'SJC - PIBAS',3,1),

        (2,4,'2026-08-23T16:30:00',N'Gimnasio Club BPS',
            N'CBPS ROJO',N'ATENAS',3,0);

    INSERT INTO dbo.MATCH
    (
        competition_id,
        phase_id,
        phase_group_id,
        series_id,
        home_team_entry_id,
        away_team_entry_id,
        match_date,
        venue_id,
        round_number,
        match_number,
        status,
        home_sets,
        away_sets,
        winner_team_entry_id
    )
    SELECT
        @Competition8,
        @RegularPhase8,
        NULL,
        NULL,
        home_te.team_entry_id,
        away_te.team_entry_id,
        m.match_date,
        v.venue_id,
        m.round_number,
        m.match_number,
        'FINISHED',
        m.home_sets,
        m.away_sets,
        CASE WHEN m.home_sets > m.away_sets
             THEN home_te.team_entry_id
             ELSE away_te.team_entry_id
        END
    FROM @Matches8 m
    JOIN dbo.TEAM home_t
      ON home_t.name = m.home_team AND home_t.gender = 'F'
    JOIN dbo.TEAM away_t
      ON away_t.name = m.away_team AND away_t.gender = 'F'
    JOIN dbo.TEAM_ENTRY home_te
      ON home_te.competition_id = @Competition8
     AND home_te.team_id = home_t.team_id
    JOIN dbo.TEAM_ENTRY away_te
      ON away_te.competition_id = @Competition8
     AND away_te.team_id = away_t.team_id
    LEFT JOIN dbo.VENUE v
      ON v.name = m.venue_name
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.MATCH x
        WHERE x.competition_id = @Competition8
          AND x.match_date = m.match_date
          AND x.home_team_entry_id = home_te.team_entry_id
          AND x.away_team_entry_id = away_te.team_entry_id
    );

    /* ========================================================================
       7. COMPETENCIA EJEMPLO DE 10 EQUIPOS - ID LIVOSUR 267
       CLAUSURA 2026 - FEMENINO PRIMERA
       ======================================================================== */

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.COMPETITION
        WHERE season_id = @Season2026
          AND name = N'Clausura 2026 - Femenino Primera [LIVOSUR 267]'
    )
    BEGIN
        INSERT INTO dbo.COMPETITION
        (
            season_id,
            division_id,
            competition_format_id,
            name,
            period_type,
            start_date,
            end_date,
            status
        )
        VALUES
        (
            @Season2026,
            @DivisionPrimeraF,
            @Format10,
            N'Clausura 2026 - Femenino Primera [LIVOSUR 267]',
            'CLAUSURA',
            '2026-08-15',
            NULL,
            'IN_PROGRESS'
        );
    END;

    DECLARE @Competition10 INT =
    (
        SELECT competition_id
        FROM dbo.COMPETITION
        WHERE season_id = @Season2026
          AND name = N'Clausura 2026 - Femenino Primera [LIVOSUR 267]'
    );

    DECLARE @Teams10 TABLE (team_name NVARCHAR(150) NOT NULL PRIMARY KEY);
    INSERT INTO @Teams10 VALUES
        (N'ARRIETA - CDV'),
        (N'BOHEMIOS'),
        (N'CBR B'),
        (N'CBR A'),
        (N'DEFENSOR DE MALDONADO'),
        (N'JUAN FERREIRA'),
        (N'NACIONAL'),
        (N'NACIONAL B'),
        (N'OLIMPIA'),
        (N'PLAZA HELVETICO');

    INSERT INTO dbo.TEAM_ENTRY
        (competition_id, team_id, seed, status)
    SELECT
        @Competition10,
        t.team_id,
        NULL,
        'ACTIVE'
    FROM dbo.TEAM t
    JOIN @Teams10 x
      ON x.team_name = t.name
    WHERE t.gender = 'F'
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.TEAM_ENTRY te
          WHERE te.competition_id = @Competition10
            AND te.team_id = t.team_id
      );

    /* Fases */
    INSERT INTO dbo.COMPETITION_PHASE
    (
        competition_id,
        format_phase_id,
        code,
        name,
        phase_type,
        phase_role,
        sequence,
        rounds,
        fixture_mode,
        status
    )
    SELECT
        @Competition10,
        fp.format_phase_id,
        fp.code,
        fp.name,
        fp.phase_type,
        fp.phase_role,
        fp.sequence,
        fp.rounds,
        fp.fixture_mode,
        CASE WHEN fp.code = 'FIRST' THEN 'IN_PROGRESS' ELSE 'PENDING' END
    FROM dbo.FORMAT_PHASE fp
    WHERE fp.competition_format_id = @Format10
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.COMPETITION_PHASE cp
          WHERE cp.competition_id = @Competition10
            AND cp.code = fp.code
      );

    /* Grupos futuros de segunda fase */
    DECLARE @SecondPhase10 INT =
    (
        SELECT phase_id
        FROM dbo.COMPETITION_PHASE
        WHERE competition_id = @Competition10
          AND code = 'SECOND'
    );

    INSERT INTO dbo.PHASE_GROUP
    (
        competition_id,
        phase_id,
        format_group_id,
        code,
        name,
        group_role,
        sequence,
        rounds,
        fixture_mode,
        carry_over_mode
    )
    SELECT
        @Competition10,
        @SecondPhase10,
        fg.format_group_id,
        fg.code,
        fg.name,
        fg.group_role,
        fg.sequence,
        fg.rounds,
        fg.fixture_mode,
        fg.carry_over_mode
    FROM dbo.FORMAT_GROUP fg
    WHERE fg.competition_format_id = @Format10
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.PHASE_GROUP pg
          WHERE pg.competition_id = @Competition10
            AND pg.phase_id = @SecondPhase10
            AND pg.code = fg.code
      );

    /* Series futuras */
    INSERT INTO dbo.PLAYOFF_SERIES
    (
        competition_id,
        phase_id,
        format_series_id,
        code,
        name,
        sequence,
        team1_entry_id,
        team2_entry_id,
        team1_initial_wins,
        team2_initial_wins,
        wins_required,
        winner_team_entry_id,
        status
    )
    SELECT
        @Competition10,
        cp.phase_id,
        fps.format_series_id,
        fps.code,
        fps.name,
        fps.sequence,
        NULL,
        NULL,
        fps.team1_initial_wins,
        fps.team2_initial_wins,
        fps.wins_required,
        NULL,
        'PENDING'
    FROM dbo.FORMAT_PLAYOFF_SERIES fps
    JOIN dbo.COMPETITION_PHASE cp
      ON cp.competition_id = @Competition10
     AND cp.format_phase_id = fps.format_phase_id
    WHERE fps.competition_format_id = @Format10
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.PLAYOFF_SERIES ps
          WHERE ps.competition_id = @Competition10
            AND ps.code = fps.code
      );

    INSERT INTO dbo.PLAYOFF_SERIES_PARTICIPANT_SOURCE
    (
        competition_id,
        target_series_id,
        target_side,
        source_type,
        source_series_id
    )
    SELECT
        @Competition10,
        target_real.series_id,
        src.target_side,
        src.source_type,
        source_real.series_id
    FROM dbo.FORMAT_SERIES_PARTICIPANT_SOURCE src
    JOIN dbo.PLAYOFF_SERIES target_real
      ON target_real.competition_id = @Competition10
    JOIN dbo.FORMAT_PLAYOFF_SERIES target_fmt
      ON target_fmt.format_series_id = src.target_format_series_id
     AND target_real.format_series_id = target_fmt.format_series_id
    JOIN dbo.PLAYOFF_SERIES source_real
      ON source_real.competition_id = @Competition10
    JOIN dbo.FORMAT_PLAYOFF_SERIES source_fmt
      ON source_fmt.format_series_id = src.source_format_series_id
     AND source_real.format_series_id = source_fmt.format_series_id
    WHERE src.competition_format_id = @Format10
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.PLAYOFF_SERIES_PARTICIPANT_SOURCE x
          WHERE x.target_series_id = target_real.series_id
            AND x.target_side = src.target_side
      );

    DECLARE @FirstPhase10 INT =
    (
        SELECT phase_id
        FROM dbo.COMPETITION_PHASE
        WHERE competition_id = @Competition10
          AND code = 'FIRST'
    );

    /* ========================================================================
       8. PARTIDOS VISIBLES DEL CASO DE 10 EQUIPOS
       Fuente: CompetitionMatches.aspx?ID=267&PID=275
       ======================================================================== */

    DECLARE @Matches10 TABLE
    (
        round_number   SMALLINT,
        match_number   SMALLINT,
        match_date     DATETIME2(0),
        venue_name     NVARCHAR(150),
        home_team      NVARCHAR(150),
        away_team      NVARCHAR(150),
        home_sets      TINYINT NULL,
        away_sets      TINYINT NULL,
        match_status   VARCHAR(20)
    );

    INSERT INTO @Matches10 VALUES
        (1,1,'2026-08-15T18:00:00',N'PLAZA NUEVA HELVECIA',
            N'PLAZA HELVETICO',N'NACIONAL',3,0,'FINISHED'),

        (1,2,'2026-08-15T19:15:00',N'UGAB',
            N'ARRIETA - CDV',N'DEFENSOR DE MALDONADO',3,0,'FINISHED'),

        (1,3,'2026-08-19T20:30:00',N'Gim "A" BOHEMIOS',
            N'BOHEMIOS',N'CBR B',1,3,'FINISHED'),

        (2,1,'2026-08-22T15:30:00',N'Polideportivo',
            N'NACIONAL',N'JUAN FERREIRA',0,3,'FINISHED'),

        (2,2,'2026-08-22T17:30:00',N'Polideportivo',
            N'NACIONAL B',N'ARRIETA - CDV',1,3,'FINISHED'),

        (2,3,'2026-08-22T20:00:00',N'PLAZA NUEVA HELVECIA',
            N'PLAZA HELVETICO',N'CBR B',0,3,'FINISHED'),

        /* Visible en el fixture sin resultado al momento de consulta. */
        (2,4,'2026-08-23T19:00:00',N'Gim "A" BOHEMIOS',
            N'BOHEMIOS',N'CBR A',NULL,NULL,'SCHEDULED');

    INSERT INTO dbo.MATCH
    (
        competition_id,
        phase_id,
        phase_group_id,
        series_id,
        home_team_entry_id,
        away_team_entry_id,
        match_date,
        venue_id,
        round_number,
        match_number,
        status,
        home_sets,
        away_sets,
        winner_team_entry_id
    )
    SELECT
        @Competition10,
        @FirstPhase10,
        NULL,
        NULL,
        home_te.team_entry_id,
        away_te.team_entry_id,
        m.match_date,
        v.venue_id,
        m.round_number,
        m.match_number,
        m.match_status,
        m.home_sets,
        m.away_sets,
        CASE
            WHEN m.home_sets IS NULL OR m.away_sets IS NULL THEN NULL
            WHEN m.home_sets > m.away_sets THEN home_te.team_entry_id
            ELSE away_te.team_entry_id
        END
    FROM @Matches10 m
    JOIN dbo.TEAM home_t
      ON home_t.name = m.home_team AND home_t.gender = 'F'
    JOIN dbo.TEAM away_t
      ON away_t.name = m.away_team AND away_t.gender = 'F'
    JOIN dbo.TEAM_ENTRY home_te
      ON home_te.competition_id = @Competition10
     AND home_te.team_id = home_t.team_id
    JOIN dbo.TEAM_ENTRY away_te
      ON away_te.competition_id = @Competition10
     AND away_te.team_id = away_t.team_id
    LEFT JOIN dbo.VENUE v
      ON v.name = m.venue_name
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.MATCH x
        WHERE x.competition_id = @Competition10
          AND x.match_date = m.match_date
          AND x.home_team_entry_id = home_te.team_entry_id
          AND x.away_team_entry_id = away_te.team_entry_id
    );

    COMMIT TRANSACTION;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO

/* ============================================================================
   9. VALIDACIÓN DEL POPULADO
   ============================================================================ */

SELECT
    c.competition_id,
    c.name,
    d.name AS division,
    cf.code AS format_code,
    COUNT(DISTINCT te.team_entry_id) AS teams
FROM dbo.COMPETITION c
JOIN dbo.DIVISION d
  ON d.division_id = c.division_id
JOIN dbo.COMPETITION_FORMAT cf
  ON cf.competition_format_id = c.competition_format_id
LEFT JOIN dbo.TEAM_ENTRY te
  ON te.competition_id = c.competition_id
WHERE c.name LIKE N'%[[]LIVOSUR %'
GROUP BY
    c.competition_id, c.name, d.name, cf.code
ORDER BY c.competition_id;
GO

SELECT
    c.name AS competition,
    cp.sequence,
    cp.code,
    cp.name AS phase,
    cp.phase_type,
    cp.phase_role,
    cp.status
FROM dbo.COMPETITION c
JOIN dbo.COMPETITION_PHASE cp
  ON cp.competition_id = c.competition_id
WHERE c.name LIKE N'%[[]LIVOSUR %'
ORDER BY c.competition_id, cp.sequence, cp.phase_id;
GO

SELECT
    c.name AS competition,
    m.round_number,
    m.match_number,
    m.match_date,
    ht.name AS home_team,
    m.home_sets,
    m.away_sets,
    at.name AS away_team,
    v.name AS venue,
    m.status
FROM dbo.MATCH m
JOIN dbo.COMPETITION c
  ON c.competition_id = m.competition_id
LEFT JOIN dbo.TEAM_ENTRY hte
  ON hte.team_entry_id = m.home_team_entry_id
LEFT JOIN dbo.TEAM ht
  ON ht.team_id = hte.team_id
LEFT JOIN dbo.TEAM_ENTRY ate
  ON ate.team_entry_id = m.away_team_entry_id
LEFT JOIN dbo.TEAM at
  ON at.team_id = ate.team_id
LEFT JOIN dbo.VENUE v
  ON v.venue_id = m.venue_id
WHERE c.name LIKE N'%[[]LIVOSUR %'
ORDER BY c.competition_id, m.round_number, m.match_number, m.match_date;
GO
