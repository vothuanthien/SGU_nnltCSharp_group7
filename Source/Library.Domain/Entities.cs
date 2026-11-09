namespace Library.Domain;
public sealed class Genre{public int GenreId{get;set;} public string Name{get;set;}=string.Empty; public bool IsActive{get;set;}=true; public ICollection<BookTitle> Books{get;set;}=new List<BookTitle>();}
public sealed class Author{public int AuthorId{get;set;} public string FullName{get;set;}=string.Empty; public ICollection<BookAuthor> BookAuthors{get;set;}=new List<BookAuthor>();}
public sealed class BookTitle{public int BookId{get;set;} public string Title{get;set;}=string.Empty; public string? ISBN{get;set;} public int? PublishedYear{get;set;} public string? Description{get;set;} public int? GenreId{get;set;} public int? PublisherId{get;set;} public bool IsActive{get;set;}=true; public Genre? Genre{get;set;} public Publisher? Publisher{get;set;} public ICollection<BookCopy> Copies{get;set;}=new List<BookCopy>(); public ICollection<BookAuthor> BookAuthors{get;set;}=new List<BookAuthor>();}
public sealed class Publisher{public int PublisherId{get;set;} public string Name{get;set;}=string.Empty; public ICollection<BookTitle> Books{get;set;}=new List<BookTitle>();}
public sealed class BookCopy{public int CopyId{get;set;} public string CopyCode{get;set;}=string.Empty; public int BookId{get;set;} public BookTitle Book{get;set;}=null!; public string? ShelfLocation{get;set;} public string Status{get;set;}="Available";}
public sealed class BookAuthor{public int BookId{get;set;} public BookTitle Book{get;set;}=null!; public int AuthorId{get;set;} public Author Author{get;set;}=null!;}

