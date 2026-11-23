using System.Globalization;
using Filmoteka.Models;
using Microsoft.Data.Sqlite;

namespace Filmoteka.Data;

/// <summary>
/// Целият достъп до SQLite базата данни: създаване на таблиците и CRUD операции.
/// Базата е файлът filmoteka.db до .exe файла на приложението.
/// </summary>
public static class Database
{
    public static string DbPath { get; } = Path.Combine(AppContext.BaseDirectory, "filmoteka.db");

    private static readonly StringComparer BgComparer = StringComparer.Create(new CultureInfo("bg-BG"), ignoreCase: true);

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS Genres (
            Id    INTEGER PRIMARY KEY AUTOINCREMENT,
            Name  TEXT    NOT NULL UNIQUE
        );

        CREATE TABLE IF NOT EXISTS Movies (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            Title      TEXT    NOT NULL,
            Director   TEXT    NOT NULL,
            Year       INTEGER NOT NULL CHECK (Year BETWEEN 1888 AND 2100),
            Rating     INTEGER NOT NULL DEFAULT 0 CHECK (Rating BETWEEN 0 AND 10),
            GenreId    INTEGER NOT NULL REFERENCES Genres(Id) ON DELETE RESTRICT,
            PosterPath TEXT    NULL,
            TmdbId     INTEGER NULL,
            Overview   TEXT    NULL,
            CreatedAt  TEXT    NOT NULL DEFAULT (datetime('now', 'localtime'))
        );
        """;

    // Имената съвпадат с жанровете на TMDB, за да се избират автоматично.
    private static readonly string[] SeedGenres =
    [
        "Екшън", "Приключенски", "Анимация", "Комедия", "Криминален", "Документален",
        "Драма", "Семеен", "Фентъзи", "Исторически", "Ужаси", "Музикален",
        "Мистерия", "Романтичен", "Научна фантастика", "Трилър", "Военен", "Уестърн"
    ];

    private static readonly (string Title, string Director, int Year, string Genre, int Rating, int TmdbId)[] SeedMovies =
    [
        ("Кръстникът", "Франсис Форд Копола", 1972, "Криминален", 10, 238),
        ("Интерстелар", "Кристофър Нолан", 2014, "Научна фантастика", 9, 157336),
        ("Матрицата", "Лана и Лили Уашовски", 1999, "Научна фантастика", 9, 603),
        ("Отнесени от духовете", "Хаяо Миядзаки", 2001, "Анимация", 9, 129),
        ("Паразит", "Бонг Джун-хо", 2019, "Трилър", 8, 496243),
        ("Гладиатор", "Ридли Скот", 2000, "Екшън", 8, 98),
        ("Криминале", "Куентин Тарантино", 1994, "Криминален", 8, 680),
        ("Форест Гъмп", "Робърт Земекис", 1994, "Драма", 0, 13),
    ];

    // ------------------------------------------------------------------
    //  Връзка и помощни методи
    // ------------------------------------------------------------------

    private static SqliteConnection Open()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = DbPath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    private static string? GetNullableString(SqliteDataReader reader, int index)
        => reader.IsDBNull(index) ? null : reader.GetString(index);

    // ------------------------------------------------------------------
    //  Създаване на базата
    // ------------------------------------------------------------------

    public static void Initialize()
    {
        using var connection = Open();

        using (var create = Command(connection, Schema))
            create.ExecuteNonQuery();

        using var count = Command(connection, "SELECT COUNT(*) FROM Genres;");
        long genres = Convert.ToInt64(count.ExecuteScalar() ?? 0L);
        if (genres == 0)
            Seed(connection);
    }

    private static void Seed(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        foreach (var name in SeedGenres)
        {
            using var insert = Command(connection, "INSERT INTO Genres (Name) VALUES (@name);", ("@name", name));
            insert.Transaction = transaction;
            insert.ExecuteNonQuery();
        }

        foreach (var m in SeedMovies)
        {
            using var insert = Command(connection, """
                INSERT INTO Movies (Title, Director, Year, Rating, GenreId, TmdbId)
                SELECT @title, @director, @year, @rating, Id, @tmdb FROM Genres WHERE Name = @genre;
                """,
                ("@title", m.Title), ("@director", m.Director), ("@year", m.Year),
                ("@rating", m.Rating), ("@tmdb", m.TmdbId), ("@genre", m.Genre));
            insert.Transaction = transaction;
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    // ------------------------------------------------------------------
    //  Жанрове (Genres)
    // ------------------------------------------------------------------

    public static List<Genre> GetGenres()
    {
        using var connection = Open();
        using var command = Command(connection, """
            SELECT g.Id, g.Name, COUNT(m.Id)
            FROM Genres g
            LEFT JOIN Movies m ON m.GenreId = g.Id
            GROUP BY g.Id, g.Name;
            """);

        var result = new List<Genre>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Genre
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                MovieCount = reader.GetInt32(2)
            });
        }

        return result.OrderBy(g => g.Name, BgComparer).ToList();
    }

    public static bool GenreExists(string name, int excludeId = 0)
        => GetGenres().Any(g => g.Id != excludeId && BgComparer.Equals(g.Name, name));

    public static int AddGenre(string name)
    {
        using var connection = Open();
        using var command = Command(connection, "INSERT INTO Genres (Name) VALUES (@name); SELECT last_insert_rowid();", ("@name", name));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static void RenameGenre(int id, string name)
    {
        using var connection = Open();
        using var command = Command(connection, "UPDATE Genres SET Name = @name WHERE Id = @id;", ("@name", name), ("@id", id));
        command.ExecuteNonQuery();
    }

    public static void DeleteGenre(int id)
    {
        using var connection = Open();

        using (var check = Command(connection, "SELECT COUNT(*) FROM Movies WHERE GenreId = @id;", ("@id", id)))
        {
            long used = Convert.ToInt64(check.ExecuteScalar() ?? 0L);
            if (used > 0)
                throw new InvalidOperationException($"Жанрът се използва от {used} филм(а) и не може да бъде изтрит. Първо сменете жанра на тези филми.");
        }

        using var command = Command(connection, "DELETE FROM Genres WHERE Id = @id;", ("@id", id));
        command.ExecuteNonQuery();
    }

    // ------------------------------------------------------------------
    //  Филми (Movies)
    // ------------------------------------------------------------------

    public static List<Movie> GetMovies()
    {
        using var connection = Open();
        using var command = Command(connection, """
            SELECT m.Id, m.Title, m.Director, m.Year, m.Rating, m.GenreId, g.Name,
                   m.PosterPath, m.TmdbId, m.Overview, m.CreatedAt
            FROM Movies m
            INNER JOIN Genres g ON g.Id = m.GenreId
            ORDER BY m.Title;
            """);

        var result = new List<Movie>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Movie
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Director = reader.GetString(2),
                Year = reader.GetInt32(3),
                Rating = reader.GetInt32(4),
                GenreId = reader.GetInt32(5),
                GenreName = reader.GetString(6),
                PosterPath = GetNullableString(reader, 7),
                TmdbId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Overview = GetNullableString(reader, 9),
                CreatedAt = reader.GetString(10)
            });
        }

        return result;
    }

    /// <summary>Проверява дали вече има филм със същото заглавие и година.</summary>
    public static bool MovieExists(string title, int year, int excludeId = 0)
    {
        using var connection = Open();
        using var command = Command(connection, "SELECT Id, Title FROM Movies WHERE Year = @year;", ("@year", year));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetInt32(0) != excludeId && BgComparer.Equals(reader.GetString(1).Trim(), title.Trim()))
                return true;
        }
        return false;
    }

    public static int InsertMovie(Movie movie)
    {
        using var connection = Open();
        using var command = Command(connection, """
            INSERT INTO Movies (Title, Director, Year, Rating, GenreId, PosterPath, TmdbId, Overview)
            VALUES (@title, @director, @year, @rating, @genreId, @poster, @tmdb, @overview);
            SELECT last_insert_rowid();
            """,
            ("@title", movie.Title), ("@director", movie.Director), ("@year", movie.Year),
            ("@rating", movie.Rating), ("@genreId", movie.GenreId), ("@poster", movie.PosterPath),
            ("@tmdb", movie.TmdbId), ("@overview", movie.Overview));
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static void UpdateMovie(Movie movie)
    {
        using var connection = Open();
        using var command = Command(connection, """
            UPDATE Movies
            SET Title = @title, Director = @director, Year = @year, Rating = @rating,
                GenreId = @genreId, PosterPath = @poster, TmdbId = @tmdb, Overview = @overview
            WHERE Id = @id;
            """,
            ("@title", movie.Title), ("@director", movie.Director), ("@year", movie.Year),
            ("@rating", movie.Rating), ("@genreId", movie.GenreId), ("@poster", movie.PosterPath),
            ("@tmdb", movie.TmdbId), ("@overview", movie.Overview), ("@id", movie.Id));
        command.ExecuteNonQuery();
    }

    public static void UpdateRating(int movieId, int rating)
    {
        using var connection = Open();
        using var command = Command(connection, "UPDATE Movies SET Rating = @rating WHERE Id = @id;", ("@rating", rating), ("@id", movieId));
        command.ExecuteNonQuery();
    }

    public static void UpdateTmdbInfo(int movieId, string? posterPath, int? tmdbId, string? overview)
    {
        using var connection = Open();
        using var command = Command(connection,
            "UPDATE Movies SET PosterPath = @poster, TmdbId = @tmdb, Overview = @overview WHERE Id = @id;",
            ("@poster", posterPath), ("@tmdb", tmdbId), ("@overview", overview), ("@id", movieId));
        command.ExecuteNonQuery();
    }

    public static void DeleteMovie(int movieId)
    {
        using var connection = Open();
        using var command = Command(connection, "DELETE FROM Movies WHERE Id = @id;", ("@id", movieId));
        command.ExecuteNonQuery();
    }
}
