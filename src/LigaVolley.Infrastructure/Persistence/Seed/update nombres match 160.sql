-- ==============================================================================
-- 1) ACTUALIZACIÓN DE DATOS PERSONALES (TABLA PERSON)
-- ==============================================================================

-- Player 1287 -> Roberta Ratzke (SETTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Ratzke', [last_name] = N'Roberta'
WHERE [person_id] = 1629;

-- Player 1288 -> Ana Cristina de Souza (OUTSIDE_HITTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'de Souza', [last_name] = N'Ana Cristina'
WHERE [person_id] = 1630;

-- Player 1289 -> Diana Duarte (MIDDLE_BLOCKER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Duarte', [last_name] = N'Diana'
WHERE [person_id] = 1631;

-- Player 1290 -> Rosamaria Montibeller (OPPOSITE)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Montibeller', [last_name] = N'Rosamaria'
WHERE [person_id] = 1632;

-- Player 1291 -> Júlia Bergmann (OUTSIDE_HITTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Júlia', [last_name] = N'Bergmann'
WHERE [person_id] = 1633;

-- Player 1292 -> Thaisa Menezes (MIDDLE_BLOCKER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Menezes', [last_name] = N'Thaisa'
WHERE [person_id] = 1634;

-- Player 1293 -> Nyeme Costa (LIBERO)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Costa', [last_name] = N'Nyeme'
WHERE [person_id] = 1635;

-- Player 1294 -> Gabriela Guimarães (OUTSIDE_HITTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Guimarães', [last_name] = N'Gabi'
WHERE [person_id] = 1636;


-- ==============================================================================
-- 2) ACTUALIZACIÓN DE ROLES EN EL PLANTEL (TABLA COMPETITION_ROSTER_PLAYER)
-- ==============================================================================

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'SETTER'
WHERE [player_id] = 1287
and competition_roster_id = 176;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OUTSIDE_HITTER'
WHERE [player_id] = 1288
and competition_roster_id = 176;;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'MIDDLE_BLOCKER'
WHERE [player_id] = 1289
and competition_roster_id = 176;;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OPPOSITE'
WHERE [player_id] = 1290
and competition_roster_id = 176;;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OUTSIDE_HITTER'
WHERE [player_id] = 1291
and competition_roster_id = 176;;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'MIDDLE_BLOCKER'
WHERE [player_id] = 1292
and competition_roster_id = 176;;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'LIBERO'
WHERE [player_id] = 1293
and competition_roster_id = 176;;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OUTSIDE_HITTER'
WHERE [player_id] = 1294
and competition_roster_id = 176;;


-- ==============================================================================
-- 1) ACTUALIZACIÓN DE DATOS PERSONALES (TABLA PERSON)
-- ==============================================================================

-- Player 1295 -> Maja Ognjenović (SETTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Maja', [last_name] = N'Ognjenović'
WHERE [person_id] = 1637;

-- Player 1296 -> Tijana Bošković (OPPOSITE)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Tijana', [last_name] = N'Bošković'
WHERE [person_id] = 1638;

-- Player 1297 -> Aleksandra Uzelac (OUTSIDE_HITTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Aleksandra', [last_name] = N'Uzelac'
WHERE [person_id] = 1639;

-- Player 1298 -> Hena Kurtagić (MIDDLE_BLOCKER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Hena', [last_name] = N'Kurtagić'
WHERE [person_id] = 1640;

-- Player 1299 -> Bianka Buša (OUTSIDE_HITTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Bianka', [last_name] = N'Buša'
WHERE [person_id] = 1641;

-- Player 1300 -> Maja Aleksić (MIDDLE_BLOCKER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Maja', [last_name] = N'Aleksić'
WHERE [person_id] = 1642;

-- Player 1301 -> Teodora Pušić (LIBERO)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Teodora', [last_name] = N'Pušić'
WHERE [person_id] = 1643;

-- Player 1302 -> Katarina Lazović (OUTSIDE_HITTER)
UPDATE [dbo].[PERSON]
SET [first_name] = N'Katarina', [last_name] = N'Lazović'
WHERE [person_id] = 1644;


-- ==============================================================================
-- 2) ACTUALIZACIÓN DE ROLES EN EL PLANTEL (TABLA COMPETITION_ROSTER_PLAYER)
-- ==============================================================================

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'SETTER'
WHERE [player_id] = 1295
and competition_roster_id = 177;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OPPOSITE'
WHERE [player_id] = 1296
and competition_roster_id = 177;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OUTSIDE_HITTER'
WHERE [player_id] = 1297
and competition_roster_id = 177;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'MIDDLE_BLOCKER'
WHERE [player_id] = 1298
and competition_roster_id = 177;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OUTSIDE_HITTER'
WHERE [player_id] = 1299
and competition_roster_id = 177;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'MIDDLE_BLOCKER'
WHERE [player_id] = 1300
and competition_roster_id = 177;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'LIBERO'
WHERE [player_id] = 1301
and competition_roster_id = 177;

UPDATE [dbo].[COMPETITION_ROSTER_PLAYER]
SET [player_role] = 'OUTSIDE_HITTER'
WHERE [player_id] = 1302
and competition_roster_id = 177;

