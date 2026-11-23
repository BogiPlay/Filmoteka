namespace Filmoteka.Models;

/// <summary>
/// Ред от таблица Genres.
/// </summary>
public class Genre
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Брой филми с този жанр (изчислява се със заявка, не е колона в таблицата).</summary>
    public int MovieCount { get; set; }

    public override string ToString() => Name;
}
