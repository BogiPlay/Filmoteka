-- =====================================================================
--  Филмотека – SQL скрипт на базата данни (SQLite)
--  Таблици: Genres (жанрове) и Movies (филми)
--  Приложението създава същата база автоматично при първо стартиране
--  (файл filmoteka.db до Filmoteka.exe).
-- =====================================================================

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Genres (
    Id    INTEGER PRIMARY KEY AUTOINCREMENT,
    Name  TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Movies (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    Title      TEXT    NOT NULL,                                      -- заглавие
    Director   TEXT    NOT NULL,                                      -- режисьор
    Year       INTEGER NOT NULL CHECK (Year BETWEEN 1888 AND 2100),  -- година
    Rating     INTEGER NOT NULL DEFAULT 0 CHECK (Rating BETWEEN 0 AND 10), -- оценка (0 = без оценка)
    GenreId    INTEGER NOT NULL REFERENCES Genres(Id) ON DELETE RESTRICT,
    PosterPath TEXT    NULL,                                          -- път към постера в TMDB
    TmdbId     INTEGER NULL,                                          -- номер на филма в TMDB
    Overview   TEXT    NULL,                                          -- описание
    CreatedAt  TEXT    NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- ---------------------------------------------------------------------
--  Начални данни
-- ---------------------------------------------------------------------

INSERT INTO Genres (Name) VALUES
    ('Екшън'), ('Приключенски'), ('Анимация'), ('Комедия'), ('Криминален'), ('Документален'),
    ('Драма'), ('Семеен'), ('Фентъзи'), ('Исторически'), ('Ужаси'), ('Музикален'),
    ('Мистерия'), ('Романтичен'), ('Научна фантастика'), ('Трилър'), ('Военен'), ('Уестърн');

INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Кръстникът', 'Франсис Форд Копола', 1972, 10, Id, 238 FROM Genres WHERE Name = 'Криминален';
INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Интерстелар', 'Кристофър Нолан', 2014, 9, Id, 157336 FROM Genres WHERE Name = 'Научна фантастика';
INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Матрицата', 'Лана и Лили Уашовски', 1999, 9, Id, 603 FROM Genres WHERE Name = 'Научна фантастика';
INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Отнесени от духовете', 'Хаяо Миядзаки', 2001, 9, Id, 129 FROM Genres WHERE Name = 'Анимация';
INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Паразит', 'Бонг Джун-хо', 2019, 8, Id, 496243 FROM Genres WHERE Name = 'Трилър';
INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Гладиатор', 'Ридли Скот', 2000, 8, Id, 98 FROM Genres WHERE Name = 'Екшън';
INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Криминале', 'Куентин Тарантино', 1994, 8, Id, 680 FROM Genres WHERE Name = 'Криминален';
INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
SELECT 'Форест Гъмп', 'Робърт Земекис', 1994, 0, Id, 13 FROM Genres WHERE Name = 'Драма';

-- ---------------------------------------------------------------------
--  Примерни заявки, които използва приложението
-- ---------------------------------------------------------------------

-- Списък с филми и жанр (DataGrid):
-- SELECT m.Id, m.Title, m.Director, m.Year, m.Rating, g.Name
-- FROM Movies m INNER JOIN Genres g ON g.Id = m.GenreId ORDER BY m.Title;

-- Жанрове с брой филми:
-- SELECT g.Id, g.Name, COUNT(m.Id) FROM Genres g LEFT JOIN Movies m ON m.GenreId = g.Id GROUP BY g.Id, g.Name;
