/*
===============================================================================
Liga Volley - Acta Electrónica del Partido
Patch de integridad cruzada v1.1
Motor: Microsoft SQL Server

Aplicar DESPUÉS de:
  1. Modelo base entidades y formatos de competición.sql
  2. Modelo_Personas_Planteles_v1.sql
  3. Modelo_Acta_Electronica_Match_v1.sql

Motivo
------
La validación con un partido completo mostró que las FK individuales garantizan
que los IDs existan, pero no siempre que SET, TEAM, PLAYER y EVENT pertenezcan
al mismo partido/equipo. Este patch agrega esas validaciones cruzadas sin
cambiar el diseño conceptual acordado.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* 1) Staff del acta debe pertenecer al roster del MATCH_TEAM. */
CREATE OR ALTER TRIGGER dbo.TR_MATCH_TEAM_STAFF_VALIDATE
ON dbo.MATCH_TEAM_STAFF
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.MATCH_TEAM mt
          ON mt.match_team_id = i.match_team_id
        JOIN dbo.COMPETITION_ROSTER_STAFF crs
          ON crs.competition_roster_staff_id = i.competition_roster_staff_id
        WHERE crs.competition_roster_id <> mt.competition_roster_id
           OR crs.staff_role <> i.staff_role
    )
    BEGIN
        THROW 50200, 'MATCH_TEAM_STAFF no coincide con el roster/rol del MATCH_TEAM.', 1;
    END;
END;
GO

/* 2) La alineación debe usar un SET del mismo MATCH que el MATCH_TEAM. */
CREATE OR ALTER TRIGGER dbo.TR_MATCH_SET_LINEUP_SCOPE_VALIDATE
ON dbo.MATCH_SET_LINEUP
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.MATCH_SET st
          ON st.match_set_id = i.match_set_id
        JOIN dbo.MATCH_TEAM mt
          ON mt.match_team_id = i.match_team_id
        JOIN dbo.MATCH_SHEET sh
          ON sh.match_sheet_id = mt.match_sheet_id
        WHERE st.match_id <> sh.match_id
    )
    BEGIN
        THROW 50201, 'MATCH_SET_LINEUP: MATCH_SET y MATCH_TEAM deben pertenecer al mismo MATCH.', 1;
    END;
END;
GO

/* 3) Sustitución: evento, set, equipo y jugadores deben coincidir. */
CREATE OR ALTER TRIGGER dbo.TR_MATCH_SUBSTITUTION_VALIDATE
ON dbo.MATCH_SUBSTITUTION
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.MATCH_EVENT e
          ON e.event_uuid = i.event_uuid
        JOIN dbo.MATCH_TEAM mt
          ON mt.match_team_id = i.match_team_id
        JOIN dbo.MATCH_SHEET sh
          ON sh.match_sheet_id = mt.match_sheet_id
        JOIN dbo.MATCH_SET st
          ON st.match_set_id = i.match_set_id
        JOIN dbo.MATCH_PLAYER po
          ON po.match_player_id = i.player_out_id
        JOIN dbo.MATCH_PLAYER pi
          ON pi.match_player_id = i.player_in_id
        WHERE e.event_type <> 'SUBSTITUTION'
           OR e.match_sheet_id <> mt.match_sheet_id
           OR e.match_set_id <> i.match_set_id
           OR st.match_id <> sh.match_id
           OR e.team_side <> mt.side
           OR po.match_team_id <> i.match_team_id
           OR pi.match_team_id <> i.match_team_id
           OR e.match_player_id <> i.player_out_id
           OR e.related_match_player_id <> i.player_in_id
           OR ISNULL(e.home_score_after, -1) <> i.home_score
           OR ISNULL(e.away_score_after, -1) <> i.away_score
    )
    BEGIN
        THROW 50202, 'MATCH_SUBSTITUTION no es consistente con EVENT/SET/TEAM/PLAYER.', 1;
    END;
END;
GO

/* 4) Reemplazo de líbero: evento, set, equipo, líbero y reemplazado coinciden. */
CREATE OR ALTER TRIGGER dbo.TR_MATCH_LIBERO_REPLACEMENT_VALIDATE
ON dbo.MATCH_LIBERO_REPLACEMENT
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.MATCH_EVENT e
          ON e.event_uuid = i.event_uuid
        JOIN dbo.MATCH_TEAM mt
          ON mt.match_team_id = i.match_team_id
        JOIN dbo.MATCH_SHEET sh
          ON sh.match_sheet_id = mt.match_sheet_id
        JOIN dbo.MATCH_SET st
          ON st.match_set_id = i.match_set_id
        JOIN dbo.MATCH_LIBERO ml
          ON ml.match_libero_id = i.match_libero_id
        JOIN dbo.MATCH_PLAYER lp
          ON lp.match_player_id = ml.match_player_id
        JOIN dbo.MATCH_PLAYER rp
          ON rp.match_player_id = i.replaced_match_player_id
        WHERE e.event_type <> CASE i.action WHEN 'ENTER' THEN 'LIBERO_ENTER' ELSE 'LIBERO_EXIT' END
           OR e.match_sheet_id <> mt.match_sheet_id
           OR e.match_set_id <> i.match_set_id
           OR st.match_id <> sh.match_id
           OR e.team_side <> mt.side
           OR ml.match_team_id <> i.match_team_id
           OR lp.match_team_id <> i.match_team_id
           OR rp.match_team_id <> i.match_team_id
           OR e.match_player_id <> ml.match_player_id
           OR e.related_match_player_id <> i.replaced_match_player_id
           OR ISNULL(e.home_score_after, -1) <> i.home_score
           OR ISNULL(e.away_score_after, -1) <> i.away_score
    )
    BEGIN
        THROW 50203, 'MATCH_LIBERO_REPLACEMENT no es consistente con EVENT/SET/TEAM/LIBERO.', 1;
    END;
END;
GO

/* 5) Timeout: evento, set y equipo deben coincidir. */
CREATE OR ALTER TRIGGER dbo.TR_MATCH_TIMEOUT_VALIDATE
ON dbo.MATCH_TIMEOUT
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.MATCH_EVENT e
          ON e.event_uuid = i.event_uuid
        JOIN dbo.MATCH_TEAM mt
          ON mt.match_team_id = i.match_team_id
        JOIN dbo.MATCH_SHEET sh
          ON sh.match_sheet_id = mt.match_sheet_id
        JOIN dbo.MATCH_SET st
          ON st.match_set_id = i.match_set_id
        WHERE e.event_type <> 'TIMEOUT'
           OR e.match_sheet_id <> mt.match_sheet_id
           OR e.match_set_id <> i.match_set_id
           OR st.match_id <> sh.match_id
           OR e.team_side <> mt.side
           OR ISNULL(e.home_score_after, -1) <> i.home_score
           OR ISNULL(e.away_score_after, -1) <> i.away_score
    )
    BEGIN
        THROW 50204, 'MATCH_TIMEOUT no es consistente con EVENT/SET/TEAM.', 1;
    END;
END;
GO

/* 6) Sanción: evento y destinatario deben pertenecer al equipo/set del acta. */
CREATE OR ALTER TRIGGER dbo.TR_MATCH_SANCTION_VALIDATE
ON dbo.MATCH_SANCTION
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.MATCH_EVENT e
          ON e.event_uuid = i.event_uuid
        JOIN dbo.MATCH_TEAM mt
          ON mt.match_team_id = i.match_team_id
        JOIN dbo.MATCH_SHEET sh
          ON sh.match_sheet_id = mt.match_sheet_id
        LEFT JOIN dbo.MATCH_SET st
          ON st.match_set_id = i.match_set_id
        LEFT JOIN dbo.MATCH_PLAYER mp
          ON mp.match_player_id = i.match_player_id
        LEFT JOIN dbo.MATCH_TEAM_STAFF ms
          ON ms.match_team_staff_id = i.match_team_staff_id
        WHERE e.event_type <> 'SANCTION'
           OR e.match_sheet_id <> mt.match_sheet_id
           OR ISNULL(e.match_set_id, -1) <> ISNULL(i.match_set_id, -1)
           OR (st.match_set_id IS NOT NULL AND st.match_id <> sh.match_id)
           OR e.team_side <> mt.side
           OR (i.target_type = 'PLAYER' AND mp.match_team_id <> i.match_team_id)
           OR (i.target_type = 'STAFF' AND ms.match_team_id <> i.match_team_id)
    )
    BEGIN
        THROW 50205, 'MATCH_SANCTION no es consistente con EVENT/SET/TEAM/destinatario.', 1;
    END;
END;
GO

/* 7) MATCH_SET_STATE: el último evento debe ser del mismo set y secuencia. */
CREATE OR ALTER TRIGGER dbo.TR_MATCH_SET_STATE_VALIDATE
ON dbo.MATCH_SET_STATE
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        LEFT JOIN dbo.MATCH_EVENT e
          ON e.event_uuid = i.last_event_uuid
        WHERE (i.last_event_uuid IS NULL AND i.last_event_sequence IS NOT NULL)
           OR (i.last_event_uuid IS NOT NULL AND i.last_event_sequence IS NULL)
           OR (i.last_event_uuid IS NOT NULL AND
               (e.match_set_id <> i.match_set_id OR e.sequence_number <> i.last_event_sequence))
    )
    BEGIN
        THROW 50206, 'MATCH_SET_STATE.last_event debe pertenecer al mismo SET y coincidir en secuencia.', 1;
    END;
END;
GO
