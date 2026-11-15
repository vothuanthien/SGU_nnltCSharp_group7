namespace Library.Domain;

/// <summary>Represents a book genre in the catalog.</summary>
public sealed class Genre
{
    /// <summary>Gets or sets the genre identifier.</summary>
    public int GenreId { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the genre is available for use.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the titles assigned to this genre.</summary>
    public ICollection<BookTitle> Books { get; set; } = new List<BookTitle>();
}

/// <summary>Represents an author of one or more book titles.</summary>
public sealed class Author
{
    /// <summary>Gets or sets the author identifier.</summary>
    public int AuthorId { get; set; }

    /// <summary>Gets or sets the author's full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Gets or sets the title links for this author.</summary>
    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}

/// <summary>Represents a catalog title, independent of its physical copies.</summary>
public sealed class BookTitle
{
    /// <summary>Gets or sets the book title identifier.</summary>
    public int BookId { get; set; }

    /// <summary>Gets or sets the title text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional ISBN.</summary>
    public string? ISBN { get; set; }

    /// <summary>Gets or sets the optional publication year.</summary>
    public int? PublishedYear { get; set; }

    /// <summary>Gets or sets the optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the optional genre identifier.</summary>
    public int? GenreId { get; set; }

    /// <summary>Gets or sets the optional publisher identifier.</summary>
    public int? PublisherId { get; set; }

    /// <summary>Gets or sets whether this title is active in the catalog.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the associated genre, when assigned.</summary>
    public Genre? Genre { get; set; }

    /// <summary>Gets or sets the associated publisher, when assigned.</summary>
    public Publisher? Publisher { get; set; }

    /// <summary>Gets or sets the physical copies of this title.</summary>
    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();

    /// <summary>Gets or sets the author links for this title.</summary>
    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}

/// <summary>Represents a publisher in the catalog.</summary>
public sealed class Publisher
{
    /// <summary>Gets or sets the publisher identifier.</summary>
    public int PublisherId { get; set; }

    /// <summary>Gets or sets the publisher name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets titles issued by this publisher.</summary>
    public ICollection<BookTitle> Books { get; set; } = new List<BookTitle>();
}

/// <summary>Represents an individually tracked physical copy of a title.</summary>
public sealed class BookCopy
{
    /// <summary>Gets or sets the copy identifier.</summary>
    public int CopyId { get; set; }

    /// <summary>Gets or sets the unique inventory code.</summary>
    public string CopyCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent title identifier.</summary>
    public int BookId { get; set; }

    /// <summary>Gets or sets the parent title navigation property.</summary>
    public BookTitle Book { get; set; } = null!;

    /// <summary>Gets or sets the optional shelf location.</summary>
    public string? ShelfLocation { get; set; }

    /// <summary>Gets or sets the circulation status.</summary>
    public string Status { get; set; } = "Available";
}

/// <summary>Associates a title with an author.</summary>
public sealed class BookAuthor
{
    /// <summary>Gets or sets the associated title identifier.</summary>
    public int BookId { get; set; }

    /// <summary>Gets or sets the associated title.</summary>
    public BookTitle Book { get; set; } = null!;

    /// <summary>Gets or sets the associated author identifier.</summary>
    public int AuthorId { get; set; }

    /// <summary>Gets or sets the associated author.</summary>
    public Author Author { get; set; } = null!;
}

/// <summary>Represents a hold/reservation placed on a book title by a reader.</summary>
public sealed class Reservation
{
    /// <summary>Gets or sets the reservation identifier.</summary>
    public int ReservationId { get; set; }

    /// <summary>Gets or sets the reader identifier.</summary>
    public int ReaderId { get; set; }

    /// <summary>Gets or sets the book title identifier.</summary>
    public int BookId { get; set; }

    /// <summary>Gets or sets the timestamp when the reservation was requested.</summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the reservation status (Pending, Fulfilled, Cancelled, Expired).</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Gets or sets the hold expiry deadline.</summary>
    public DateTime? HoldUntil { get; set; }
}
