using Library.Domain;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure;

/// <summary>Provides Entity Framework access to library catalog data.</summary>
/// <param name="options">Provider and database options for this context.</param>
public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options)
    : DbContext(options)
{
    /// <summary>Gets the catalog titles set.</summary>
    public DbSet<BookTitle> BookTitles => Set<BookTitle>();

    /// <summary>Gets the physical book copies set.</summary>
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();

    /// <summary>Gets the genres set.</summary>
    public DbSet<Genre> Genres => Set<Genre>();

    /// <summary>Gets the authors set.</summary>
    public DbSet<Author> Authors => Set<Author>();

    /// <summary>Gets the publishers set.</summary>
    public DbSet<Publisher> Publishers => Set<Publisher>();

    /// <summary>Gets the title-author links set.</summary>
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();

    /// <summary>Configures table names, keys, indexes, and entity relationships.</summary>
    /// <param name="modelBuilder">The builder used to configure the EF model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookTitle>().ToTable("BookTitles").HasKey(book => book.BookId);
        modelBuilder.Entity<BookTitle>().Property(book => book.Title).HasMaxLength(300).IsRequired();
        modelBuilder.Entity<BookTitle>().Property(book => book.ISBN).HasMaxLength(20);
        modelBuilder.Entity<BookTitle>()
            .HasIndex(book => book.ISBN)
            .IsUnique()
            .HasFilter("[ISBN] IS NOT NULL");

        modelBuilder.Entity<BookCopy>().ToTable("BookCopies").HasKey(copy => copy.CopyId);
        modelBuilder.Entity<BookCopy>().HasIndex(copy => copy.CopyCode).IsUnique();
        modelBuilder.Entity<BookCopy>().Property(copy => copy.CopyCode).HasMaxLength(40);

        modelBuilder.Entity<Genre>().ToTable("Genres").HasKey(genre => genre.GenreId);
        modelBuilder.Entity<Author>().ToTable("Authors").HasKey(author => author.AuthorId);
        modelBuilder.Entity<Publisher>().ToTable("Publishers").HasKey(publisher => publisher.PublisherId);

        modelBuilder.Entity<BookTitle>()
            .HasOne(book => book.Publisher)
            .WithMany(publisher => publisher.Books)
            .HasForeignKey(book => book.PublisherId);

        modelBuilder.Entity<BookAuthor>().ToTable("BookAuthors")
            .HasKey(link => new { link.BookId, link.AuthorId });
        modelBuilder.Entity<BookAuthor>()
            .HasOne(link => link.Book)
            .WithMany(book => book.BookAuthors)
            .HasForeignKey(link => link.BookId);
        modelBuilder.Entity<BookAuthor>()
            .HasOne(link => link.Author)
            .WithMany(author => author.BookAuthors)
            .HasForeignKey(link => link.AuthorId);
    }
}
