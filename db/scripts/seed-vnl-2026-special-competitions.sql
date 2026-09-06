/*
  LigaVolley - VNL 2026 Special Competitions
  Snapshot deportivo: 2026-08-04 (ranking) / planteles internacionales 2026.

  Crea de forma idempotente:
    - divisionales internacionales F/M;
    - 24 clubes/federaciones y 24 equipos nacionales;
    - VNL 2026 Women y VNL 2026 Men en Season 2026;
    - estructura de Competition materializada desde el formato SPLIT_STAGE;
    - 12 TeamEntry ACTIVE por competencia, sembrados por ranking;
    - 288 PERSON + PLAYER y 24 CompetitionRoster ACTIVE de 12 jugadoras/es.

  No crea MATCH ni fixture: deja ambas competencias DRAFT, listas para ejecutar
  POST /api/admin/competitions/{id}/fixture/generate.

  El dorsal y la capitania NO se cargan en CompetitionRosterPlayer: son datos
  contextuales del MatchSheet. PLAYER_ROLE es solamente funcion habitual.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Precondiciones del modelo vigente. */
    IF OBJECT_ID(N'dbo.SEASON', N'U') IS NULL OR OBJECT_ID(N'dbo.COMPETITION', N'U') IS NULL
        THROW 51000, 'LigaVolley schema not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.SEASON WHERE [year] = 2026)
        THROW 51001, 'Season 2026 must exist before running this script.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.COMPETITION_FORMAT WHERE code = 'SPLIT_STAGE' AND active = 1)
        THROW 51002, 'Active CompetitionFormat SPLIT_STAGE (canonical Format 2) is required.', 1;
    IF COL_LENGTH('dbo.COMPETITION_ROSTER_PLAYER', 'jersey_number') IS NOT NULL
       OR COL_LENGTH('dbo.COMPETITION_ROSTER_PLAYER', 'is_captain') IS NOT NULL
        THROW 51003, 'Outdated roster schema detected: apply Match-specific Jersey Number & Captain migrations first.', 1;
    IF NOT EXISTS
       (
           SELECT 1
           FROM sys.foreign_key_columns fkc
           JOIN sys.tables pt ON pt.object_id=fkc.parent_object_id
           JOIN sys.schemas ps ON ps.schema_id=pt.schema_id
           JOIN sys.tables rt ON rt.object_id=fkc.referenced_object_id
           JOIN sys.schemas rs ON rs.schema_id=rt.schema_id
           WHERE ps.name='dbo' AND pt.name='COMPETITION_ROSTER_PLAYER'
             AND rs.name='dbo' AND rt.name='PLAYER_ROLE'
       )
       AND COL_LENGTH('dbo.COMPETITION_ROSTER_PLAYER', 'player_role') IS NULL
       AND COL_LENGTH('dbo.COMPETITION_ROSTER_PLAYER', 'role') IS NULL
        THROW 51004, 'No PLAYER_ROLE foreign key, player_role column or role column found in COMPETITION_ROSTER_PLAYER.', 1;

    IF OBJECT_ID(N'tempdb..#Players', N'U') IS NOT NULL DROP TABLE #Players;
    IF OBJECT_ID(N'tempdb..#Teams', N'U') IS NOT NULL DROP TABLE #Teams;

    DECLARE @SeasonId INT = (SELECT season_id FROM dbo.SEASON WHERE [year] = 2026);
    DECLARE @FormatId INT = (SELECT competition_format_id FROM dbo.COMPETITION_FORMAT WHERE code = 'SPLIT_STAGE' AND active = 1);

    CREATE TABLE #Teams
    (
        gender CHAR(1) NOT NULL,
        seed SMALLINT NOT NULL,
        country_code CHAR(3) NOT NULL,
        country_name NVARCHAR(80) NOT NULL,
        PRIMARY KEY (gender, country_code)
    );

    /* Top 11 del corte + Argentina obligatoria. Seed 12 identifica el cupo invitado. */
    INSERT #Teams (gender, seed, country_code, country_name) VALUES
    ('F',1,'ITA',N'Italy'),('F',2,'BRA',N'Brazil'),('F',3,'TUR',N'Türkiye'),
    ('F',4,'USA',N'United States'),('F',5,'POL',N'Poland'),('F',6,'JPN',N'Japan'),
    ('F',7,'CHN',N'China'),('F',8,'NED',N'Netherlands'),('F',9,'SRB',N'Serbia'),
    ('F',10,'GER',N'Germany'),('F',11,'DOM',N'Dominican Republic'),('F',12,'ARG',N'Argentina'),
    ('M',1,'POL',N'Poland'),('M',2,'ITA',N'Italy'),('M',3,'RUS',N'Russia'),
    ('M',4,'SLO',N'Slovenia'),('M',5,'USA',N'United States'),('M',6,'JPN',N'Japan'),
    ('M',7,'BRA',N'Brazil'),('M',8,'FRA',N'France'),('M',9,'BUL',N'Bulgaria'),
    ('M',10,'TUR',N'Türkiye'),('M',11,'UKR',N'Ukraine'),('M',12,'ARG',N'Argentina');

    CREATE TABLE #Players
    (
        gender CHAR(1) NOT NULL,
        country_code CHAR(3) NOT NULL,
        ordinal TINYINT NOT NULL,
        full_name NVARCHAR(160) NOT NULL,
        role_code VARCHAR(30) NOT NULL,
        PRIMARY KEY (gender, country_code, ordinal)
    );

    /* Women: seleccion de 12 habituales de listas internacionales 2026. */
    INSERT #Players VALUES
    ('F','ITA',1,N'Carlotta Cambi','SETTER'),('F','ITA',2,N'Alessia Orro','SETTER'),
    ('F','ITA',3,N'Paola Egonu','OPPOSITE'),('F','ITA',4,N'Ekaterina Antropova','OPPOSITE'),
    ('F','ITA',5,N'Miriam Sylla','OUTSIDE_HITTER'),('F','ITA',6,N'Stella Nervini','OUTSIDE_HITTER'),
    ('F','ITA',7,N'Loveth Omoruyi','OUTSIDE_HITTER'),('F','ITA',8,N'Anna Danesi','MIDDLE_BLOCKER'),
    ('F','ITA',9,N'Sarah Fahr','MIDDLE_BLOCKER'),('F','ITA',10,N'Linda Nwakalor','MIDDLE_BLOCKER'),
    ('F','ITA',11,N'Eleonora Fersino','LIBERO'),('F','ITA',12,N'Ilaria Spirito','OUTSIDE_HITTER'),
    ('F','BRA',1,N'Macris Carneiro','SETTER'),('F','BRA',2,N'Roberta Ratzke','SETTER'),
    ('F','BRA',3,N'Kisy Nascimento','OPPOSITE'),('F','BRA',4,N'Rosamaria Montibeller','OPPOSITE'),
    ('F','BRA',5,N'Gabriela Guimarães','OUTSIDE_HITTER'),('F','BRA',6,N'Ana Cristina Souza','OUTSIDE_HITTER'),
    ('F','BRA',7,N'Júlia Bergmann','OUTSIDE_HITTER'),('F','BRA',8,N'Thaisa Daher','MIDDLE_BLOCKER'),
    ('F','BRA',9,N'Júlia Kudiess','MIDDLE_BLOCKER'),('F','BRA',10,N'Diana Duarte','MIDDLE_BLOCKER'),
    ('F','BRA',11,N'Nyeme Costa','LIBERO'),('F','BRA',12,N'Marcelle Rabelo','LIBERO'),
    ('F','TUR',1,N'Cansu Özbay','SETTER'),('F','TUR',2,N'Elif Şahin','SETTER'),
    ('F','TUR',3,N'Melissa Vargas','OPPOSITE'),('F','TUR',4,N'Ebrar Karakurt','OPPOSITE'),
    ('F','TUR',5,N'Hande Baladın','OUTSIDE_HITTER'),('F','TUR',6,N'İlkin Aydın','OUTSIDE_HITTER'),
    ('F','TUR',7,N'Derya Cebecioğlu','OUTSIDE_HITTER'),('F','TUR',8,N'Eda Erdem','MIDDLE_BLOCKER'),
    ('F','TUR',9,N'Zehra Güneş','MIDDLE_BLOCKER'),('F','TUR',10,N'Aslı Kalaç','MIDDLE_BLOCKER'),
    ('F','TUR',11,N'Gizem Örge','LIBERO'),('F','TUR',12,N'Simge Aköz','LIBERO'),
    ('F','USA',1,N'Jordyn Poulter','SETTER'),('F','USA',2,N'Lauren Carlini','SETTER'),
    ('F','USA',3,N'Jordan Thompson','OPPOSITE'),('F','USA',4,N'Andrea Drews','OPPOSITE'),
    ('F','USA',5,N'Kathryn Plummer','OUTSIDE_HITTER'),('F','USA',6,N'Avery Skinner','OUTSIDE_HITTER'),
    ('F','USA',7,N'Khalia Lanier','OUTSIDE_HITTER'),('F','USA',8,N'Chiaka Ogbogu','MIDDLE_BLOCKER'),
    ('F','USA',9,N'Haleigh Washington','MIDDLE_BLOCKER'),('F','USA',10,N'Dana Rettke','MIDDLE_BLOCKER'),
    ('F','USA',11,N'Justine Wong-Orantes','LIBERO'),('F','USA',12,N'Zoe Fleck','LIBERO'),
    ('F','POL',1,N'Joanna Wołosz','SETTER'),('F','POL',2,N'Katarzyna Wenerska','SETTER'),
    ('F','POL',3,N'Magdalena Stysiak','OPPOSITE'),('F','POL',4,N'Malwina Smarzek','OPPOSITE'),
    ('F','POL',5,N'Martyna Łukasik','OUTSIDE_HITTER'),('F','POL',6,N'Martyna Czyrniańska','OUTSIDE_HITTER'),
    ('F','POL',7,N'Olivia Różański','OUTSIDE_HITTER'),('F','POL',8,N'Agnieszka Korneluk','MIDDLE_BLOCKER'),
    ('F','POL',9,N'Magdalena Jurczyk','MIDDLE_BLOCKER'),('F','POL',10,N'Klaudia Alagierska','MIDDLE_BLOCKER'),
    ('F','POL',11,N'Maria Stenzel','LIBERO'),('F','POL',12,N'Aleksandra Szczygłowska','LIBERO'),
    ('F','JPN',1,N'Nanami Seki','SETTER'),('F','JPN',2,N'Koyomi Iwasaki','SETTER'),
    ('F','JPN',3,N'Yukiko Wada','OPPOSITE'),('F','JPN',4,N'Kotona Hayashi','OUTSIDE_HITTER'),
    ('F','JPN',5,N'Mayu Ishikawa','OUTSIDE_HITTER'),('F','JPN',6,N'Yoshino Sato','OUTSIDE_HITTER'),
    ('F','JPN',7,N'Rin Honda','OUTSIDE_HITTER'),('F','JPN',8,N'Ayaka Araki','MIDDLE_BLOCKER'),
    ('F','JPN',9,N'Nichika Yamada','MIDDLE_BLOCKER'),('F','JPN',10,N'Airi Miyabe','MIDDLE_BLOCKER'),
    ('F','JPN',11,N'Manami Kojima','LIBERO'),('F','JPN',12,N'Satomi Fukudome','LIBERO'),
    ('F','CHN',1,N'Diao Linyu','SETTER'),('F','CHN',2,N'Ding Xia','SETTER'),
    ('F','CHN',3,N'Gong Xiangyu','OPPOSITE'),('F','CHN',4,N'Zhou Yetong','OPPOSITE'),
    ('F','CHN',5,N'Zhu Ting','OUTSIDE_HITTER'),('F','CHN',6,N'Li Yingying','OUTSIDE_HITTER'),
    ('F','CHN',7,N'Wu Mengjie','OUTSIDE_HITTER'),('F','CHN',8,N'Yuan Xinyue','MIDDLE_BLOCKER'),
    ('F','CHN',9,N'Wang Yuanyuan','MIDDLE_BLOCKER'),('F','CHN',10,N'Gao Yi','MIDDLE_BLOCKER'),
    ('F','CHN',11,N'Wang Mengjie','LIBERO'),('F','CHN',12,N'Ni Feifan','LIBERO'),
    ('F','NED',1,N'Sarah van Aalen','SETTER'),('F','NED',2,N'Britt Bongaerts','SETTER'),
    ('F','NED',3,N'Nika Daalderop','OUTSIDE_HITTER'),('F','NED',4,N'Celeste Plak','OUTSIDE_HITTER'),
    ('F','NED',5,N'Anne Buijs','OUTSIDE_HITTER'),('F','NED',6,N'Nova Marring','OPPOSITE'),
    ('F','NED',7,N'Juliet Lohuis','MIDDLE_BLOCKER'),('F','NED',8,N'Eline Timmerman','MIDDLE_BLOCKER'),
    ('F','NED',9,N'Indy Baijens','MIDDLE_BLOCKER'),('F','NED',10,N'Jolien Knollema','OUTSIDE_HITTER'),
    ('F','NED',11,N'Florien Reesink','LIBERO'),('F','NED',12,N'Kirsten Knip','LIBERO'),
    ('F','SRB',1,N'Maja Ognjenović','SETTER'),('F','SRB',2,N'Bojana Drča','SETTER'),
    ('F','SRB',3,N'Tijana Bošković','OPPOSITE'),('F','SRB',4,N'Ana Bjelica','OPPOSITE'),
    ('F','SRB',5,N'Bianka Buša','OUTSIDE_HITTER'),('F','SRB',6,N'Katarina Lazović','OUTSIDE_HITTER'),
    ('F','SRB',7,N'Sara Lozo','OUTSIDE_HITTER'),('F','SRB',8,N'Maja Aleksić','MIDDLE_BLOCKER'),
    ('F','SRB',9,N'Jovana Stevanović','MIDDLE_BLOCKER'),('F','SRB',10,N'Hena Kurtagić','MIDDLE_BLOCKER'),
    ('F','SRB',11,N'Silvija Popović','LIBERO'),('F','SRB',12,N'Teodora Pušić','LIBERO'),
    ('F','GER',1,N'Pia Kästner','SETTER'),('F','GER',2,N'Denise Imoudu','SETTER'),
    ('F','GER',3,N'Kimberly Drewniok','OPPOSITE'),('F','GER',4,N'Lena Stigrot','OUTSIDE_HITTER'),
    ('F','GER',5,N'Hanna Orthmann','OUTSIDE_HITTER'),('F','GER',6,N'Lina Alsmeier','OUTSIDE_HITTER'),
    ('F','GER',7,N'Leana Grozer','OUTSIDE_HITTER'),('F','GER',8,N'Camilla Weitzel','MIDDLE_BLOCKER'),
    ('F','GER',9,N'Marie Schölzel','MIDDLE_BLOCKER'),('F','GER',10,N'Anna Pogany','LIBERO'),
    ('F','GER',11,N'Linda Bock','LIBERO'),('F','GER',12,N'Monique Strubbe','MIDDLE_BLOCKER'),
    ('F','DOM',1,N'Niverka Marte','SETTER'),('F','DOM',2,N'Camila de la Rosa','SETTER'),
    ('F','DOM',3,N'Gaila González','OPPOSITE'),('F','DOM',4,N'Bethania de la Cruz','OUTSIDE_HITTER'),
    ('F','DOM',5,N'Brayelin Martínez','OUTSIDE_HITTER'),('F','DOM',6,N'Yonkaira Peña','OUTSIDE_HITTER'),
    ('F','DOM',7,N'Jineiry Martínez','MIDDLE_BLOCKER'),('F','DOM',8,N'Lisvel Eve','MIDDLE_BLOCKER'),
    ('F','DOM',9,N'Geraldine González','MIDDLE_BLOCKER'),('F','DOM',10,N'Madeline Guillén','OUTSIDE_HITTER'),
    ('F','DOM',11,N'Brenda Castillo','LIBERO'),('F','DOM',12,N'Larysmer Martínez','LIBERO'),
    ('F','ARG',1,N'Victoria Mayer','SETTER'),('F','ARG',2,N'Valentina Galiano','SETTER'),
    ('F','ARG',3,N'Bianca Cugno','OPPOSITE'),('F','ARG',4,N'Elina Rodríguez','OUTSIDE_HITTER'),
    ('F','ARG',5,N'Daniela Bulaich','OUTSIDE_HITTER'),('F','ARG',6,N'Bianca Farriol','MIDDLE_BLOCKER'),
    ('F','ARG',7,N'Candelaria Herrera','MIDDLE_BLOCKER'),('F','ARG',8,N'Emilce Sosa','MIDDLE_BLOCKER'),
    ('F','ARG',9,N'Antonela Fortuna','OUTSIDE_HITTER'),('F','ARG',10,N'Agostina Pelozo','LIBERO'),
    ('F','ARG',11,N'Tatiana Rizzo','LIBERO'),('F','ARG',12,N'Nicole Pérez','OUTSIDE_HITTER');

    /* Men: seleccion de 12 habituales de listas internacionales 2026. */
    INSERT #Players VALUES
    ('M','POL',1,N'Marcin Janusz','SETTER'),('M','POL',2,N'Grzegorz Łomacz','SETTER'),
    ('M','POL',3,N'Kewin Sasak','OPPOSITE'),('M','POL',4,N'Bartosz Kurek','OPPOSITE'),
    ('M','POL',5,N'Wilfredo León','OUTSIDE_HITTER'),('M','POL',6,N'Tomasz Fornal','OUTSIDE_HITTER'),
    ('M','POL',7,N'Aleksander Śliwka','OUTSIDE_HITTER'),('M','POL',8,N'Jakub Kochanowski','MIDDLE_BLOCKER'),
    ('M','POL',9,N'Norbert Huber','MIDDLE_BLOCKER'),('M','POL',10,N'Mateusz Bieniek','MIDDLE_BLOCKER'),
    ('M','POL',11,N'Jakub Popiwczak','LIBERO'),('M','POL',12,N'Paweł Zatorski','LIBERO'),
    ('M','ITA',1,N'Simone Giannelli','SETTER'),('M','ITA',2,N'Riccardo Sbertoli','SETTER'),
    ('M','ITA',3,N'Yuri Romanò','OPPOSITE'),('M','ITA',4,N'Kamil Rychlicki','OPPOSITE'),
    ('M','ITA',5,N'Alessandro Michieletto','OUTSIDE_HITTER'),('M','ITA',6,N'Daniele Lavia','OUTSIDE_HITTER'),
    ('M','ITA',7,N'Mattia Bottolo','OUTSIDE_HITTER'),('M','ITA',8,N'Roberto Russo','MIDDLE_BLOCKER'),
    ('M','ITA',9,N'Gianluca Galassi','MIDDLE_BLOCKER'),('M','ITA',10,N'Giovanni Sanguinetti','MIDDLE_BLOCKER'),
    ('M','ITA',11,N'Fabio Balaso','LIBERO'),('M','ITA',12,N'Leonardo Scanferla','LIBERO'),
    ('M','RUS',1,N'Pavel Pankov','SETTER'),('M','RUS',2,N'Konstantin Abaev','SETTER'),
    ('M','RUS',3,N'Maksim Mikhaylov','OPPOSITE'),('M','RUS',4,N'Viktor Poletaev','OPPOSITE'),
    ('M','RUS',5,N'Dmitry Volkov','OUTSIDE_HITTER'),('M','RUS',6,N'Yaroslav Podlesnykh','OUTSIDE_HITTER'),
    ('M','RUS',7,N'Egor Kliuka','OUTSIDE_HITTER'),('M','RUS',8,N'Ivan Iakovlev','MIDDLE_BLOCKER'),
    ('M','RUS',9,N'Ilya Vlasov','MIDDLE_BLOCKER'),('M','RUS',10,N'Vadim Likhosherstov','MIDDLE_BLOCKER'),
    ('M','RUS',11,N'Valentin Golubev','LIBERO'),('M','RUS',12,N'Ilya Fedorov','LIBERO'),
    ('M','SLO',1,N'Gregor Ropret','SETTER'),('M','SLO',2,N'Dejan Vinčić','SETTER'),
    ('M','SLO',3,N'Tonček Štern','OPPOSITE'),('M','SLO',4,N'Rok Možič','OUTSIDE_HITTER'),
    ('M','SLO',5,N'Tine Urnaut','OUTSIDE_HITTER'),('M','SLO',6,N'Žiga Štern','OUTSIDE_HITTER'),
    ('M','SLO',7,N'Klemen Čebulj','OUTSIDE_HITTER'),('M','SLO',8,N'Jan Kozamernik','MIDDLE_BLOCKER'),
    ('M','SLO',9,N'Alen Pajenk','MIDDLE_BLOCKER'),('M','SLO',10,N'Sašo Štalekar','MIDDLE_BLOCKER'),
    ('M','SLO',11,N'Jani Kovačič','LIBERO'),('M','SLO',12,N'Žiga Kumer','LIBERO'),
    ('M','USA',1,N'Micah Christenson','SETTER'),('M','USA',2,N'Micah Maʻa','SETTER'),
    ('M','USA',3,N'Matthew Anderson','OPPOSITE'),('M','USA',4,N'Kyle Ensing','OPPOSITE'),
    ('M','USA',5,N'Aaron Russell','OUTSIDE_HITTER'),('M','USA',6,N'Torey DeFalco','OUTSIDE_HITTER'),
    ('M','USA',7,N'Cody Kessel','OUTSIDE_HITTER'),('M','USA',8,N'Taylor Averill','MIDDLE_BLOCKER'),
    ('M','USA',9,N'Max Holt','MIDDLE_BLOCKER'),('M','USA',10,N'Jeff Jendryk','MIDDLE_BLOCKER'),
    ('M','USA',11,N'Erik Shoji','LIBERO'),('M','USA',12,N'Kyle Dagostino','LIBERO'),
    ('M','JPN',1,N'Masahiro Sekita','SETTER'),('M','JPN',2,N'Masato Kai','SETTER'),
    ('M','JPN',3,N'Yuji Nishida','OPPOSITE'),('M','JPN',4,N'Kento Miyaura','OPPOSITE'),
    ('M','JPN',5,N'Yuki Ishikawa','OUTSIDE_HITTER'),('M','JPN',6,N'Ran Takahashi','OUTSIDE_HITTER'),
    ('M','JPN',7,N'Tatsunori Otsuka','OUTSIDE_HITTER'),('M','JPN',8,N'Taishi Onodera','MIDDLE_BLOCKER'),
    ('M','JPN',9,N'Akihiro Yamauchi','MIDDLE_BLOCKER'),('M','JPN',10,N'Kentaro Takahashi','MIDDLE_BLOCKER'),
    ('M','JPN',11,N'Tomohiro Yamamoto','LIBERO'),('M','JPN',12,N'Tomohiro Ogawa','LIBERO'),
    ('M','BRA',1,N'Fernando Kreling','SETTER'),('M','BRA',2,N'Matheus Brasília','SETTER'),
    ('M','BRA',3,N'Alan Souza','OPPOSITE'),('M','BRA',4,N'Darlan Souza','OPPOSITE'),
    ('M','BRA',5,N'Ricardo Lucarelli','OUTSIDE_HITTER'),('M','BRA',6,N'Leal Hidalgo','OUTSIDE_HITTER'),
    ('M','BRA',7,N'Douglas Souza','OUTSIDE_HITTER'),('M','BRA',8,N'Flávio Gualberto','MIDDLE_BLOCKER'),
    ('M','BRA',9,N'Lucas Saatkamp','MIDDLE_BLOCKER'),('M','BRA',10,N'Judson Nunes','MIDDLE_BLOCKER'),
    ('M','BRA',11,N'Maique Nascimento','LIBERO'),('M','BRA',12,N'Thales Hoss','LIBERO'),
    ('M','FRA',1,N'Antoine Brizard','SETTER'),('M','FRA',2,N'Benjamin Toniutti','SETTER'),
    ('M','FRA',3,N'Jean Patry','OPPOSITE'),('M','FRA',4,N'Théo Faure','OPPOSITE'),
    ('M','FRA',5,N'Trevor Clévenot','OUTSIDE_HITTER'),('M','FRA',6,N'Yacine Louati','OUTSIDE_HITTER'),
    ('M','FRA',7,N'Kevin Tillie','OUTSIDE_HITTER'),('M','FRA',8,N'Nicolas Le Goff','MIDDLE_BLOCKER'),
    ('M','FRA',9,N'Barthélémy Chinenyeze','MIDDLE_BLOCKER'),('M','FRA',10,N'Quentin Jouffroy','MIDDLE_BLOCKER'),
    ('M','FRA',11,N'Jenia Grebennikov','LIBERO'),('M','FRA',12,N'Benjamin Diez','LIBERO'),
    ('M','BUL',1,N'Simeon Nikolov','SETTER'),('M','BUL',2,N'Georgi Seganov','SETTER'),
    ('M','BUL',3,N'Venisklav Antov','OPPOSITE'),('M','BUL',4,N'Dimitar Dimitrov','OPPOSITE'),
    ('M','BUL',5,N'Aleksandar Nikolov','OUTSIDE_HITTER'),('M','BUL',6,N'Martin Atanasov','OUTSIDE_HITTER'),
    ('M','BUL',7,N'Asparuh Asparuhov','OUTSIDE_HITTER'),('M','BUL',8,N'Aleks Grozdanov','MIDDLE_BLOCKER'),
    ('M','BUL',9,N'Iliya Petkov','MIDDLE_BLOCKER'),('M','BUL',10,N'Nikolay Kolev','MIDDLE_BLOCKER'),
    ('M','BUL',11,N'Damian Kolev','LIBERO'),('M','BUL',12,N'Simeon Dobrev','LIBERO'),
    ('M','TUR',1,N'Murat Yenipazar','SETTER'),('M','TUR',2,N'Arslan Ekşi','SETTER'),
    ('M','TUR',3,N'Adis Lagumdzija','OPPOSITE'),('M','TUR',4,N'Kaan Gürbüz','OPPOSITE'),
    ('M','TUR',5,N'Efe Mandıracı','OUTSIDE_HITTER'),('M','TUR',6,N'Burutay Subaşı','OUTSIDE_HITTER'),
    ('M','TUR',7,N'Mirza Lagumdzija','OUTSIDE_HITTER'),('M','TUR',8,N'Bedirhan Bülbül','MIDDLE_BLOCKER'),
    ('M','TUR',9,N'Vahit Emre Savaş','MIDDLE_BLOCKER'),('M','TUR',10,N'Faik Samed Güneş','MIDDLE_BLOCKER'),
    ('M','TUR',11,N'Volkan Döne','LIBERO'),('M','TUR',12,N'Berkay Bayraktar','LIBERO'),
    ('M','UKR',1,N'Volodymyr Kovalchuk','SETTER'),('M','UKR',2,N'Vitalii Shchytkov','SETTER'),
    ('M','UKR',3,N'Vasyl Tupchii','OPPOSITE'),('M','UKR',4,N'Oleksandr Kovalov','OPPOSITE'),
    ('M','UKR',5,N'Oleh Plotnytskyi','OUTSIDE_HITTER'),('M','UKR',6,N'Oleksii Holoven','OUTSIDE_HITTER'),
    ('M','UKR',7,N'Illia Kovalov','OUTSIDE_HITTER'),('M','UKR',8,N'Yurii Semeniuk','MIDDLE_BLOCKER'),
    ('M','UKR',9,N'Dmytro Teryomenko','MIDDLE_BLOCKER'),('M','UKR',10,N'Oleksandr Boiko','MIDDLE_BLOCKER'),
    ('M','UKR',11,N'Oleksandr Hladkyi','LIBERO'),('M','UKR',12,N'Vladyslav Shchurov','LIBERO'),
    ('M','ARG',1,N'Luciano De Cecco','SETTER'),('M','ARG',2,N'Matías Sánchez','SETTER'),
    ('M','ARG',3,N'Bruno Lima','OPPOSITE'),('M','ARG',4,N'Pablo Kukartsev','OPPOSITE'),
    ('M','ARG',5,N'Luciano Palonsky','OUTSIDE_HITTER'),('M','ARG',6,N'Ezequiel Palacios','OUTSIDE_HITTER'),
    ('M','ARG',7,N'Jan Martínez','OUTSIDE_HITTER'),('M','ARG',8,N'Agustín Loser','MIDDLE_BLOCKER'),
    ('M','ARG',9,N'Martín Ramos','MIDDLE_BLOCKER'),('M','ARG',10,N'Joaquín Gallego','MIDDLE_BLOCKER'),
    ('M','ARG',11,N'Santiago Danani','LIBERO'),('M','ARG',12,N'Franco Massimino','LIBERO');

    IF EXISTS (SELECT gender, country_code FROM #Players GROUP BY gender, country_code HAVING COUNT(*) <> 12)
        THROW 51005, 'Every national team must contain exactly 12 players.', 1;
    IF EXISTS
       (
           SELECT 1 FROM #Players
           WHERE role_code NOT IN ('SETTER','OUTSIDE_HITTER','MIDDLE_BLOCKER','OPPOSITE','LIBERO')
       )
        THROW 51006, 'An unsupported player role code was supplied.', 1;

    /* Divisiones internacionales: asigna level_order libre sin asumir IDs. */
    DECLARE @WomenDivisionId INT, @MenDivisionId INT;
    IF NOT EXISTS (SELECT 1 FROM dbo.DIVISION WHERE name=N'International' AND gender='F')
        INSERT dbo.DIVISION(name,level_order,gender,active)
        SELECT N'International',ISNULL(MAX(level_order),0)+1,'F',1 FROM dbo.DIVISION WHERE gender='F';
    IF NOT EXISTS (SELECT 1 FROM dbo.DIVISION WHERE name=N'International' AND gender='M')
        INSERT dbo.DIVISION(name,level_order,gender,active)
        SELECT N'International',ISNULL(MAX(level_order),0)+1,'M',1 FROM dbo.DIVISION WHERE gender='M';
    SELECT @WomenDivisionId=division_id FROM dbo.DIVISION WHERE name=N'International' AND gender='F';
    SELECT @MenDivisionId=division_id FROM dbo.DIVISION WHERE name=N'International' AND gender='M';

    /* Federación/club y Team. Se prefija para no colisionar con clubes locales. */
    INSERT dbo.CLUB(name,short_name,active)
    SELECT N'National Federation - '+country_name, country_code, 1 FROM #Teams t
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CLUB c WHERE c.name=N'National Federation - '+t.country_name)
    GROUP BY country_name,country_code;

    INSERT dbo.TEAM(club_id,name,gender,active)
    SELECT c.club_id, t.country_name+N' National Team', t.gender, 1
    FROM #Teams t JOIN dbo.CLUB c ON c.name=N'National Federation - '+t.country_name
    WHERE NOT EXISTS (SELECT 1 FROM dbo.TEAM x WHERE x.name=t.country_name+N' National Team' AND x.gender=t.gender);

    /* Competitions DRAFT. */
    IF NOT EXISTS (SELECT 1 FROM dbo.COMPETITION WHERE season_id=@SeasonId AND name=N'VNL 2026 Women')
        INSERT dbo.COMPETITION(season_id,division_id,competition_format_id,name,period_type,start_date,end_date,status)
        VALUES(@SeasonId,@WomenDivisionId,@FormatId,N'VNL 2026 Women','ANNUAL','2026-06-01','2026-08-31','DRAFT');
    IF NOT EXISTS (SELECT 1 FROM dbo.COMPETITION WHERE season_id=@SeasonId AND name=N'VNL 2026 Men')
        INSERT dbo.COMPETITION(season_id,division_id,competition_format_id,name,period_type,start_date,end_date,status)
        VALUES(@SeasonId,@MenDivisionId,@FormatId,N'VNL 2026 Men','ANNUAL','2026-06-01','2026-08-31','DRAFT');

    DECLARE @WomenCompetitionId INT=(SELECT competition_id FROM dbo.COMPETITION WHERE season_id=@SeasonId AND name=N'VNL 2026 Women');
    DECLARE @MenCompetitionId INT=(SELECT competition_id FROM dbo.COMPETITION WHERE season_id=@SeasonId AND name=N'VNL 2026 Men');
    IF EXISTS (SELECT 1 FROM dbo.COMPETITION WHERE competition_id IN(@WomenCompetitionId,@MenCompetitionId) AND (status<>'DRAFT' OR competition_format_id<>@FormatId))
        THROW 51007, 'Existing VNL competition is not DRAFT or does not use SPLIT_STAGE.', 1;

    /* Materialización FROM_FORMAT equivalente al caso de uso Application. */
    INSERT dbo.COMPETITION_PHASE(competition_id,format_phase_id,code,name,phase_type,phase_role,sequence,rounds,fixture_mode,status)
    SELECT c.competition_id,fp.format_phase_id,fp.code,fp.name,fp.phase_type,fp.phase_role,fp.sequence,fp.rounds,fp.fixture_mode,'PENDING'
    FROM (VALUES(@WomenCompetitionId),(@MenCompetitionId)) c(competition_id)
    CROSS JOIN dbo.FORMAT_PHASE fp
    WHERE fp.competition_format_id=@FormatId AND fp.active=1
      AND NOT EXISTS(SELECT 1 FROM dbo.COMPETITION_PHASE cp WHERE cp.competition_id=c.competition_id AND cp.format_phase_id=fp.format_phase_id);

    /* Estructura materializada con el esquema vigente. */
    INSERT dbo.PHASE_GROUP(competition_phase_id,format_group_id,code,name,group_role,sequence,rounds,fixture_mode,carry_over_mode)
    SELECT cp.competition_phase_id,fg.format_group_id,fg.code,fg.name,fg.group_role,fg.sequence,fg.rounds,fg.fixture_mode,fg.carry_over_mode
    FROM dbo.FORMAT_GROUP fg JOIN dbo.COMPETITION_PHASE cp ON cp.format_phase_id=fg.format_phase_id AND cp.competition_id IN(@WomenCompetitionId,@MenCompetitionId)
    WHERE fg.competition_format_id=@FormatId AND fg.active=1
      AND NOT EXISTS(SELECT 1 FROM dbo.PHASE_GROUP pg WHERE pg.competition_phase_id=cp.competition_phase_id AND pg.format_group_id=fg.format_group_id);

    INSERT dbo.PLAYOFF_SERIES(competition_id,competition_phase_id,format_series_id,code,name,sequence,team1_entry_id,team2_entry_id,team1_initial_wins,team2_initial_wins,wins_required,winner_team_entry_id,status)
    SELECT cp.competition_id,cp.competition_phase_id,fs.format_series_id,fs.code,fs.name,fs.sequence,NULL,NULL,fs.team1_initial_wins,fs.team2_initial_wins,fs.wins_required,NULL,'PENDING'
    FROM dbo.FORMAT_PLAYOFF_SERIES fs JOIN dbo.COMPETITION_PHASE cp ON cp.format_phase_id=fs.format_phase_id AND cp.competition_id IN(@WomenCompetitionId,@MenCompetitionId)
    WHERE fs.competition_format_id=@FormatId AND fs.active=1
      AND NOT EXISTS(SELECT 1 FROM dbo.PLAYOFF_SERIES ps WHERE ps.competition_phase_id=cp.competition_phase_id AND ps.format_series_id=fs.format_series_id);

    INSERT dbo.SERIES_PARTICIPANT_SOURCE(target_playoff_series_id,target_side,source_type,source_playoff_series_id)
    SELECT target.playoff_series_id,fss.target_side,fss.source_type,source.playoff_series_id
    FROM dbo.FORMAT_SERIES_PARTICIPANT_SOURCE fss
    JOIN dbo.PLAYOFF_SERIES target ON target.format_series_id=fss.target_format_series_id
    JOIN dbo.COMPETITION_PHASE target_phase ON target_phase.competition_phase_id=target.competition_phase_id AND target_phase.competition_id IN(@WomenCompetitionId,@MenCompetitionId)
    JOIN dbo.PLAYOFF_SERIES source ON source.format_series_id=fss.source_format_series_id
    JOIN dbo.COMPETITION_PHASE source_phase ON source_phase.competition_phase_id=source.competition_phase_id AND source_phase.competition_id=target_phase.competition_id
    WHERE fss.competition_format_id=@FormatId
      AND NOT EXISTS(SELECT 1 FROM dbo.SERIES_PARTICIPANT_SOURCE x WHERE x.target_playoff_series_id=target.playoff_series_id AND x.target_side=fss.target_side);

    /* Team entries ACTIVE: el seed reproduce el puesto del corte de ranking. */
    INSERT dbo.TEAM_ENTRY(competition_id,team_id,seed,status)
    SELECT CASE t.gender WHEN 'F' THEN @WomenCompetitionId ELSE @MenCompetitionId END,tm.team_id,t.seed,'ACTIVE'
    FROM #Teams t JOIN dbo.TEAM tm ON tm.name=t.country_name+N' National Team' AND tm.gender=t.gender
    WHERE NOT EXISTS(SELECT 1 FROM dbo.TEAM_ENTRY te WHERE te.competition_id=CASE t.gender WHEN 'F' THEN @WomenCompetitionId ELSE @MenCompetitionId END AND te.team_id=tm.team_id);

    /* Personas: clave técnica estable y explícita; evita deduplicar homónimos. */
    INSERT dbo.PERSON(document_type,document_number,first_name,last_name,birth_date,gender,email,phone,active)
    SELECT 'VNL2026',p.gender+':'+p.country_code+':'+RIGHT('00'+CONVERT(VARCHAR(2),p.ordinal),2),
           LEFT(p.full_name,LEN(p.full_name)-CHARINDEX(' ',REVERSE(p.full_name))),
           RIGHT(p.full_name,CHARINDEX(' ',REVERSE(p.full_name))-1),NULL,p.gender,NULL,NULL,1
    FROM #Players p
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PERSON x WHERE x.document_type='VNL2026' AND x.document_number=p.gender+':'+p.country_code+':'+RIGHT('00'+CONVERT(VARCHAR(2),p.ordinal),2));

    INSERT dbo.PLAYER(person_id,active)
    SELECT p.person_id,1 FROM dbo.PERSON p
    WHERE p.document_type='VNL2026' AND NOT EXISTS(SELECT 1 FROM dbo.PLAYER x WHERE x.person_id=p.person_id);

    INSERT dbo.COMPETITION_ROSTER(team_entry_id,status)
    SELECT te.team_entry_id,'ACTIVE' FROM dbo.TEAM_ENTRY te
    WHERE te.competition_id IN(@WomenCompetitionId,@MenCompetitionId)
      AND NOT EXISTS(SELECT 1 FROM dbo.COMPETITION_ROSTER r WHERE r.team_entry_id=te.team_entry_id);

    INSERT dbo.COMPETITION_ROSTER_PLAYER(competition_roster_id,player_id,player_role,status)
    SELECT cr.competition_roster_id,pl.player_id,s.role_code,'ACTIVE'
    FROM #Players s
    JOIN dbo.PERSON pe ON pe.document_type='VNL2026' AND pe.document_number=s.gender+':'+s.country_code+':'+RIGHT('00'+CONVERT(VARCHAR(2),s.ordinal),2)
    JOIN dbo.PLAYER pl ON pl.person_id=pe.person_id
    JOIN #Teams t ON t.gender=s.gender AND t.country_code=s.country_code
    JOIN dbo.TEAM tm ON tm.name=t.country_name+N' National Team' AND tm.gender=t.gender
    JOIN dbo.TEAM_ENTRY te ON te.team_id=tm.team_id AND te.competition_id=CASE s.gender WHEN 'F' THEN @WomenCompetitionId ELSE @MenCompetitionId END
    JOIN dbo.COMPETITION_ROSTER cr ON cr.team_entry_id=te.team_entry_id
    WHERE NOT EXISTS(SELECT 1 FROM dbo.COMPETITION_ROSTER_PLAYER rp WHERE rp.competition_roster_id=cr.competition_roster_id AND rp.player_id=pl.player_id);

    /* Validación final antes de commit. */
    IF (SELECT COUNT(*) FROM dbo.TEAM_ENTRY WHERE competition_id=@WomenCompetitionId AND status='ACTIVE')<>12
       OR (SELECT COUNT(*) FROM dbo.TEAM_ENTRY WHERE competition_id=@MenCompetitionId AND status='ACTIVE')<>12
        THROW 51008, 'Final validation failed: each competition must have 12 ACTIVE entries.', 1;
    IF EXISTS
    (
        SELECT cr.competition_roster_id FROM dbo.COMPETITION_ROSTER cr
        LEFT JOIN dbo.COMPETITION_ROSTER_PLAYER rp ON rp.competition_roster_id=cr.competition_roster_id AND rp.status='ACTIVE'
        JOIN dbo.TEAM_ENTRY te ON te.team_entry_id=cr.team_entry_id
        WHERE te.competition_id IN(@WomenCompetitionId,@MenCompetitionId)
        GROUP BY cr.competition_roster_id HAVING COUNT(rp.competition_roster_player_id)<>12
    ) THROW 51009, 'Final validation failed: each roster must have 12 ACTIVE players.', 1;

    COMMIT TRANSACTION;

    SELECT c.competition_id,c.name,c.status,COUNT(DISTINCT te.team_entry_id) AS active_teams,
           COUNT(DISTINCT rp.competition_roster_player_id) AS active_players
    FROM dbo.COMPETITION c
    JOIN dbo.TEAM_ENTRY te ON te.competition_id=c.competition_id AND te.status='ACTIVE'
    JOIN dbo.COMPETITION_ROSTER cr ON cr.team_entry_id=te.team_entry_id AND cr.status='ACTIVE'
    JOIN dbo.COMPETITION_ROSTER_PLAYER rp ON rp.competition_roster_id=cr.competition_roster_id AND rp.status='ACTIVE'
    WHERE c.competition_id IN(@WomenCompetitionId,@MenCompetitionId)
    GROUP BY c.competition_id,c.name,c.status;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
