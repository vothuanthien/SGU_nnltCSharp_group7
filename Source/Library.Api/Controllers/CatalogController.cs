using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using Library.Infrastructure;
namespace Library.Api.Controllers;
[ApiController, Route("api/catalog")]
public class CatalogController(LibraryDbContext db) : ControllerBase {
 [HttpGet("books")] public async Task<IActionResult> Search([FromQuery]string? q,[FromQuery]int page=1,[FromQuery]int pageSize=12) {
  page=Math.Max(1,page); pageSize=Math.Clamp(pageSize,1,50); var term=q?.Trim(); var books=db.BookTitles.AsNoTracking().Where(b=>b.IsActive);
  if(!string.IsNullOrWhiteSpace(term)) books=books.Where(b=>b.Title.Contains(term)|| (b.ISBN!=null&&b.ISBN.Contains(term))||b.BookAuthors.Any(x=>x.Author.FullName.Contains(term)));
  var rows=await books.OrderBy(b=>b.Title).Skip((page-1)*pageSize).Take(pageSize).Select(b=>new{id=b.BookId,title=b.Title,isbn=b.ISBN,year=b.PublishedYear,genre=b.Genre==null?"Chưa phân loại":b.Genre.Name,available=b.Copies.Count(x=>x.Status=="Available")}).ToListAsync(); return Ok(rows);
 }
}
