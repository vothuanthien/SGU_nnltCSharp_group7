using Library.Api.Data;
using Library.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using System.Data;
using System.ComponentModel.DataAnnotations;

namespace Library.Api.Controllers;

/// <summary>
/// API tra cứu thông tin thẻ độc giả và danh sách sách đang mượn.
/// Phục vụ cả màn hình Circulation (Desktop) và trang tra cứu thẻ (Web MVC).
/// </summary>
[ApiController]
[Route("api/readers")]
public sealed class ReadersController(DbFactory db, AdminSessionStore sessions) : ControllerBase
{
    public sealed class RegisterReaderRequest
    {
        [Required, StringLength(80, MinimumLength = 3), RegularExpression("^[a-zA-Z0-9._-]+$")]
        public string? Username { get; init; }

        [Required, StringLength(200, MinimumLength = 2)]
        public string? FullName { get; init; }

        [EmailAddress, StringLength(254)]
        public string? Email { get; init; }

        [StringLength(30)]
        public string? Phone { get; init; }

        [Required, StringLength(100, MinimumLength = 8)]
        public string? Password { get; init; }
    }

    /// <summary>Đăng ký bạn đọc từ Web và lưu trực tiếp vào cùng database dùng bởi màn hình thủ thư.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterReaderRequest request)
    {
        var fullName = request.FullName?.Trim();
        var username = request.Username?.Trim();
        var email = request.Email?.Trim();
        var phone = request.Phone?.Trim();
        var password = request.Password;
        if (string.IsNullOrWhiteSpace(fullName))
            return BadRequest(new { error = "Vui lòng nhập họ tên bạn đọc." });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // Mã được tạo ở server, không nhận mã thẻ từ trình duyệt; mã ngẫu nhiên tránh đụng nhau khi đăng ký đồng thời.
            var readerCode = $"DG{Guid.NewGuid():N}"[..12].ToUpperInvariant();
            var cardNumber = $"TV{DateTime.UtcNow:yyMMdd}{Guid.NewGuid():N}"[..18].ToUpperInvariant();
            const string sql = "INSERT INTO Readers(ReaderCode,CardNumber,FullName,Email,Phone,RegisteredAt,ExpiryDate,Status,PasswordHash,Username) " +
                "OUTPUT INSERTED.ReaderId,INSERTED.ExpiryDate VALUES(@code,@card,@name,@email,@phone,CAST(SYSDATETIME() AS date)," +
                "DATEADD(year,1,CAST(SYSDATETIME() AS date)),'Active',@passwordHash,@username)";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@code", SqlDbType.NVarChar, 30).Value = readerCode;
            command.Parameters.Add("@card", SqlDbType.NVarChar, 30).Value = cardNumber;
            command.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = fullName;
            command.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = string.IsNullOrWhiteSpace(email) ? DBNull.Value : email;
            command.Parameters.Add("@phone", SqlDbType.NVarChar, 30).Value = string.IsNullOrWhiteSpace(phone) ? DBNull.Value : phone;
            command.Parameters.Add("@passwordHash", SqlDbType.NVarChar, 500).Value = PasswordHashing.Hash(password!);
            command.Parameters.Add("@username", SqlDbType.NVarChar, 80).Value = username!;
            int readerId;
            DateTime expiryDate;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                readerId = reader.GetInt32(0);
                expiryDate = reader.GetDateTime(1);
            }
            await transaction.CommitAsync();
            return Ok(new { readerId, readerCode, cardNumber, username, fullName, expiryDate = expiryDate.ToString("yyyy-MM-dd") });
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync();
            if (ex.Number is 2601 or 2627)
                return Conflict(new { error = "Tên đăng nhập đã được sử dụng hoặc mã thẻ bị trùng. Vui lòng chọn tên khác." });
            throw;
        }
    }

    public sealed record ReaderLoginRequest(string? Username, string? Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ReaderLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { error = "Vui lòng nhập tên đăng nhập và mật khẩu." });

        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT CardNumber, FullName, PasswordHash, Status, ExpiryDate FROM Readers WHERE Username=@username", connection);
        command.Parameters.Add("@username", SqlDbType.NVarChar, 80).Value = request.Username.Trim();
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (!await reader.ReadAsync() || reader.IsDBNull(2) ||
            !PasswordHashing.Verify(request.Password, reader.GetString(2)) ||
            !reader.GetString(3).Equals("Active", StringComparison.OrdinalIgnoreCase) ||
            reader.GetDateTime(4).Date < DateTime.Today)
            return Unauthorized(new { error = "Tên đăng nhập hoặc mật khẩu không đúng, hoặc thẻ đã hết hạn/bị khóa." });

        var cardNumber = reader.GetString(0);
        var fullName = reader.GetString(1);
        var (token, session) = sessions.Create(cardNumber, "Reader");
        return Ok(new { token, cardNumber, fullName, expiresAt = session.ExpiresAt });
    }

    [Authorize(Roles = "Reader")]
    [HttpGet("me")]
    public async Task<IActionResult> MyPortal()
    {
        var cardNumber = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(cardNumber)) return Unauthorized();
        await using var connection = db.Create();
        await connection.OpenAsync();
        int readerId;
        object profile;
        await using (var command = new SqlCommand(
            "SELECT ReaderId,ReaderCode,CardNumber,FullName,Email,Phone,ExpiryDate,Status,Username FROM Readers WHERE CardNumber=@card", connection))
        {
            command.Parameters.Add("@card", SqlDbType.NVarChar, 30).Value = cardNumber;
            await using (var r = await command.ExecuteReaderAsync(CommandBehavior.SingleRow))
            {
                if (!await r.ReadAsync()) return NotFound(new { error = "Không tìm thấy hồ sơ bạn đọc." });
                readerId = r.GetInt32(0);
                profile = new
                {
                    readerCode = r.GetString(1), cardNumber = r.GetString(2), fullName = r.GetString(3),
                    email = r.IsDBNull(4) ? null : r.GetString(4), phone = r.IsDBNull(5) ? null : r.GetString(5),
                    expiryDate = r.GetDateTime(6).ToString("yyyy-MM-dd"), status = r.GetString(7),
                    username = r.IsDBNull(8) ? null : r.GetString(8)
                };
            }
        }
        await ReservationsAdminController.ExpireHoldsAsync(connection);

        var loans = new List<object>();
        await using (var command = new SqlCommand("""
            SELECT d.LoanDetailId,l.LoanCode,b.Title,c.CopyCode,l.LoanDate,d.DueDate,d.ReturnedAt,
                   ISNULL(ret.FineAmount,0),ISNULL(ret.OverdueFine,ISNULL(ret.FineAmount,0)),ISNULL(ret.CompensationAmount,0),ISNULL(pay.Paid,0)
                   ,d.RenewalCount,CASE WHEN EXISTS(SELECT 1 FROM Reservations z WHERE z.BookId=b.BookId AND z.Status IN ('Pending','Ready'))
                       OR EXISTS(SELECT 1 FROM LoanRequests q WHERE q.BookId=b.BookId AND q.Status='Requested' AND q.ReaderId<>l.ReaderId)
                       THEN 1 ELSE 0 END
            FROM Loans l JOIN LoanDetails d ON d.LoanId=l.LoanId
            JOIN BookCopies c ON c.CopyId=d.CopyId JOIN BookTitles b ON b.BookId=c.BookId
            LEFT JOIN [Returns] ret ON ret.LoanDetailId=d.LoanDetailId
            OUTER APPLY (SELECT SUM(Amount) AS Paid FROM FinePayments fp WHERE fp.ReturnId=ret.ReturnId) pay
            WHERE l.ReaderId=@reader ORDER BY l.LoanDate DESC,d.LoanDetailId DESC
            """, connection))
        {
            command.Parameters.Add("@reader", SqlDbType.Int).Value = readerId;
            await using var r = await command.ExecuteReaderAsync();
            while (await r.ReadAsync())
                loans.Add(new { loanDetailId=r.GetInt32(0),loanCode=r.GetString(1), title=r.GetString(2), copyCode=r.GetString(3),
                    loanDate=r.GetDateTime(4).ToString("yyyy-MM-dd"), dueDate=r.GetDateTime(5).ToString("yyyy-MM-dd"),
                    returnedAt=r.IsDBNull(6)?null:r.GetDateTime(6).ToString("yyyy-MM-dd HH:mm"),
                    fineAmount=r.GetDecimal(7), overdueFine=r.GetDecimal(8), compensationAmount=r.GetDecimal(9),
                    paidAmount=r.GetDecimal(10), renewalCount=r.GetInt32(11),
                    canRenew=r.IsDBNull(6) && r.GetInt32(11)<1 && r.GetInt32(12)==0 && r.GetDateTime(5).Date>=DateTime.Today,
                    status=r.IsDBNull(6)?"Borrowed":"Returned" });
        }

        var reservations = new List<object>();
        await using (var command = new SqlCommand("""
            SELECT r.ReservationId,b.Title,r.Status,r.RequestedAt,r.HoldUntil,c.CopyCode
            FROM Reservations r JOIN BookTitles b ON b.BookId=r.BookId
            LEFT JOIN BookCopies c ON c.CopyId=r.HeldCopyId
            WHERE r.ReaderId=@reader ORDER BY r.RequestedAt DESC
            """, connection))
        {
            command.Parameters.Add("@reader", SqlDbType.Int).Value = readerId;
            await using var r = await command.ExecuteReaderAsync();
            while (await r.ReadAsync())
                reservations.Add(new { reservationId=r.GetInt32(0), title=r.GetString(1), status=r.GetString(2),
                    requestedAt=r.GetDateTime(3).ToString("yyyy-MM-dd HH:mm"), holdUntil=r.IsDBNull(4)?null:r.GetDateTime(4).ToString("yyyy-MM-dd HH:mm"),
                    copyCode=r.IsDBNull(5)?null:r.GetString(5) });
        }

        var loanRequests = new List<object>();
        await using (var command = new SqlCommand("""
            SELECT q.LoanRequestId,b.Title,q.Status,q.RequestedAt,q.ProcessedAt,q.RejectionReason,c.CopyCode,l.LoanCode
            FROM LoanRequests q JOIN BookTitles b ON b.BookId=q.BookId
            LEFT JOIN BookCopies c ON c.CopyId=q.CopyId LEFT JOIN Loans l ON l.LoanId=q.LoanId
            WHERE q.ReaderId=@reader ORDER BY q.RequestedAt DESC
            """, connection))
        {
            command.Parameters.Add("@reader", SqlDbType.Int).Value = readerId;
            await using var r = await command.ExecuteReaderAsync();
            while (await r.ReadAsync())
                loanRequests.Add(new { requestId=r.GetInt32(0),title=r.GetString(1),status=r.GetString(2),
                    requestedAt=r.GetDateTime(3).ToString("yyyy-MM-dd HH:mm"),processedAt=r.IsDBNull(4)?null:r.GetDateTime(4).ToString("yyyy-MM-dd HH:mm"),
                    rejectionReason=r.IsDBNull(5)?null:r.GetString(5),copyCode=r.IsDBNull(6)?null:r.GetString(6),loanCode=r.IsDBNull(7)?null:r.GetString(7) });
        }

        var notifications = new List<object>();
        await using (var command = new SqlCommand("SELECT TOP(30) NotificationId,[Type],Message,CreatedAt,ReadAt FROM ReaderNotifications WHERE ReaderId=@reader ORDER BY CreatedAt DESC", connection))
        {
            command.Parameters.Add("@reader", SqlDbType.Int).Value = readerId;
            await using var r = await command.ExecuteReaderAsync();
            while (await r.ReadAsync())
                notifications.Add(new { notificationId=r.GetInt32(0),type=r.GetString(1),message=r.GetString(2),
                    createdAt=r.GetDateTime(3).ToString("yyyy-MM-dd HH:mm"),isRead=!r.IsDBNull(4) });
        }
        return Ok(new { profile, loans, reservations, loanRequests, notifications });
    }

    public sealed record ChangeReaderPasswordRequest(string? CurrentPassword,string? NewPassword);

    [Authorize(Roles = "Reader")]
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeReaderPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error="Nhập mật khẩu hiện tại và mật khẩu mới có ít nhất 8 ký tự." });
        var card=User.Identity?.Name;
        await using var connection=db.Create();
        await connection.OpenAsync();
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            int readerId; string username,hash;
            await using(var find=new SqlCommand("SELECT ReaderId,Username,PasswordHash FROM Readers WITH (UPDLOCK,HOLDLOCK) WHERE CardNumber=@card AND Status='Active'",connection,tx))
            {
                find.Parameters.Add("@card",SqlDbType.NVarChar,30).Value=card??string.Empty;
                await using var r=await find.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if(!await r.ReadAsync()||r.IsDBNull(2))return Unauthorized(new{error="Không tìm thấy tài khoản bạn đọc đang hoạt động."});
                readerId=r.GetInt32(0);username=r.GetString(1);hash=r.GetString(2);
            }
            if(!PasswordHashing.Verify(request.CurrentPassword,hash))return BadRequest(new{error="Mật khẩu hiện tại chưa đúng."});
            if(PasswordHashing.Verify(request.NewPassword,hash))return BadRequest(new{error="Mật khẩu mới phải khác mật khẩu hiện tại."});
            await using(var update=new SqlCommand("UPDATE Readers SET PasswordHash=@hash WHERE ReaderId=@reader",connection,tx))
            {
                update.Parameters.Add("@hash",SqlDbType.NVarChar,500).Value=PasswordHashing.Hash(request.NewPassword);
                update.Parameters.Add("@reader",SqlDbType.Int).Value=readerId;await update.ExecuteNonQueryAsync();
            }
            await ReaderNotificationStore.AuditAsync(connection,tx,username,"Reader.PasswordChanged","Reader",readerId.ToString());
            await tx.CommitAsync();
            sessions.RevokeUserSessions(card??string.Empty);
            var (token,session)=sessions.Create(card??string.Empty,"Reader");
            return Ok(new{token,expiresAt=session.ExpiresAt});
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    [Authorize(Roles = "Reader")]
    [HttpPost("me/loans/{loanDetailId:int}/renew")]
    public async Task<IActionResult> RenewLoan(int loanDetailId)
    {
        var card=User.Identity?.Name;
        await using var connection=db.Create();
        await connection.OpenAsync();
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            int readerId,bookId,renewalCount;string username;DateTime dueDate,today;bool returned;
            await using(var find=new SqlCommand("""
                SELECT l.ReaderId,c.BookId,d.DueDate,d.ReturnedAt,d.RenewalCount,r.Username,r.Status,r.ExpiryDate,CAST(SYSDATETIME() AS date)
                FROM LoanDetails d WITH (UPDLOCK,HOLDLOCK) JOIN Loans l ON l.LoanId=d.LoanId
                JOIN BookCopies c ON c.CopyId=d.CopyId JOIN Readers r ON r.ReaderId=l.ReaderId
                WHERE d.LoanDetailId=@detail AND r.CardNumber=@card
                """,connection,tx))
            {
                find.Parameters.Add("@detail",SqlDbType.Int).Value=loanDetailId;
                find.Parameters.Add("@card",SqlDbType.NVarChar,30).Value=card??string.Empty;
                await using var r=await find.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if(!await r.ReadAsync())return NotFound(new{error="Không tìm thấy lượt mượn của bạn."});
                readerId=r.GetInt32(0);bookId=r.GetInt32(1);dueDate=r.GetDateTime(2);returned=!r.IsDBNull(3);
                renewalCount=r.GetInt32(4);username=r.GetString(5);
                if(r.GetString(6)!="Active"||r.GetDateTime(7).Date<r.GetDateTime(8).Date)return Conflict(new{error="Thẻ bạn đọc đang khóa hoặc hết hạn."});
                today=r.GetDateTime(8);
            }
            if(returned)return Conflict(new{error="Sách đã trả, không thể gia hạn."});
            if(dueDate.Date<today.Date)return Conflict(new{error="Sách đã quá hạn, không thể gia hạn online. Vui lòng liên hệ thủ thư."});
            if(renewalCount>=1)return Conflict(new{error="Mỗi lượt mượn chỉ được gia hạn online một lần."});
            await using(var waiting=new SqlCommand("SELECT (SELECT COUNT(*) FROM Reservations WITH (UPDLOCK,HOLDLOCK) WHERE BookId=@book AND Status IN ('Pending','Ready'))+(SELECT COUNT(*) FROM LoanRequests WITH (UPDLOCK,HOLDLOCK) WHERE BookId=@book AND Status='Requested' AND ReaderId<>@reader)",connection,tx))
            {
                waiting.Parameters.Add("@book",SqlDbType.Int).Value=bookId;
                waiting.Parameters.Add("@reader",SqlDbType.Int).Value=readerId;
                if(Convert.ToInt32(await waiting.ExecuteScalarAsync())>0)return Conflict(new{error="Sách đang có bạn đọc đặt trước, không thể gia hạn."});
            }
            var policy=await LoanPolicyStore.LoadAsync(connection,tx);
            DateTime newDue;
            await using(var update=new SqlCommand("UPDATE LoanDetails SET DueDate=DATEADD(day,@days,DueDate),RenewalCount=RenewalCount+1 OUTPUT INSERTED.DueDate WHERE LoanDetailId=@detail AND ReturnedAt IS NULL AND RenewalCount=0",connection,tx))
            {
                update.Parameters.Add("@days",SqlDbType.Int).Value=policy.LoanDays;update.Parameters.Add("@detail",SqlDbType.Int).Value=loanDetailId;
                var result=await update.ExecuteScalarAsync();
                if(result is null)return Conflict(new{error="Lượt mượn vừa được xử lý. Tải lại trang và thử lại."});
                newDue=Convert.ToDateTime(result);
            }
            await ReaderNotificationStore.AddAsync(connection,tx,readerId,"LoanRenewed",$"Đã gia hạn sách đến {newDue:yyyy-MM-dd}.");
            await ReaderNotificationStore.AuditAsync(connection,tx,username,"Loan.Renewed","LoanDetail",loanDetailId.ToString());
            await tx.CommitAsync();
            return Ok(new{loanDetailId,dueDate=newDue.ToString("yyyy-MM-dd"),renewalCount=renewalCount+1});
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    [Authorize(Roles = "Reader")]
    [HttpPost("me/notifications/{id:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        await using var connection=db.Create();await connection.OpenAsync();
        await using var command=new SqlCommand("UPDATE n SET ReadAt=COALESCE(ReadAt,SYSDATETIME()) FROM ReaderNotifications n JOIN Readers r ON r.ReaderId=n.ReaderId WHERE n.NotificationId=@id AND r.CardNumber=@card",connection);
        command.Parameters.Add("@id",SqlDbType.Int).Value=id;command.Parameters.Add("@card",SqlDbType.NVarChar,30).Value=User.Identity?.Name??string.Empty;
        return await command.ExecuteNonQueryAsync()==1?NoContent():NotFound(new{error="Không tìm thấy thông báo."});
    }

    public sealed record CreateLoanRequest(int BookId);

    [Authorize(Roles = "Reader")]
    [HttpPost("me/loan-requests")]
    public async Task<IActionResult> RequestLoan([FromBody] CreateLoanRequest request)
    {
        if(request.BookId<=0) return BadRequest(new { error="Đầu sách không hợp lệ." });
        var cardNumber=User.Identity?.Name;
        await using var connection=db.Create();
        await connection.OpenAsync();
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            int readerId;
            await using(var readerCmd=new SqlCommand("SELECT ReaderId FROM Readers WITH (UPDLOCK,HOLDLOCK) WHERE CardNumber=@card AND Status='Active' AND ExpiryDate>=CAST(SYSDATETIME() AS date)",connection,tx))
            {
                readerCmd.Parameters.Add("@card",SqlDbType.NVarChar,30).Value=cardNumber!;
                var value=await readerCmd.ExecuteScalarAsync();
                if(value is null) return Conflict(new { error="Thẻ bạn đọc không hoạt động hoặc đã hết hạn." });
                readerId=Convert.ToInt32(value);
            }
            int available;
            await using(var bookCmd=new SqlCommand("SELECT COUNT(*) FROM BookCopies c JOIN BookTitles b ON b.BookId=c.BookId WHERE c.BookId=@book AND c.Status='Available' AND b.IsActive=1",connection,tx))
            {
                bookCmd.Parameters.Add("@book",SqlDbType.Int).Value=request.BookId;
                available=Convert.ToInt32(await bookCmd.ExecuteScalarAsync());
            }
            if(available==0) return Conflict(new { error="Không còn bản sao sẵn có để gửi yêu cầu mượn. Bạn có thể đặt trước sách." });
            int requestId;
            await using(var insert=new SqlCommand("INSERT INTO LoanRequests(ReaderId,BookId) OUTPUT INSERTED.LoanRequestId VALUES(@reader,@book)",connection,tx))
            {
                insert.Parameters.Add("@reader",SqlDbType.Int).Value=readerId;
                insert.Parameters.Add("@book",SqlDbType.Int).Value=request.BookId;
                requestId=Convert.ToInt32(await insert.ExecuteScalarAsync());
            }
            await tx.CommitAsync();
            return Ok(new { requestId,status="Requested",message="Đã gửi yêu cầu mượn. Thủ thư sẽ kiểm tra và xác nhận bản sao." });
        }
        catch(SqlException ex) when(ex.Number is 2601 or 2627)
        {
            await tx.RollbackAsync();
            return Conflict(new { error="Bạn đã có yêu cầu mượn đang chờ cho đầu sách này." });
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    [Authorize(Roles = "Reader")]
    [HttpDelete("me/loan-requests/{id:int}")]
    public async Task<IActionResult> CancelLoanRequest(int id)
    {
        await using var connection=db.Create();
        await connection.OpenAsync();
        await using var command=new SqlCommand("UPDATE q SET Status='Cancelled',ProcessedAt=SYSDATETIME() FROM LoanRequests q JOIN Readers r ON r.ReaderId=q.ReaderId WHERE q.LoanRequestId=@id AND q.Status='Requested' AND r.CardNumber=@card",connection);
        command.Parameters.Add("@id",SqlDbType.Int).Value=id;
        command.Parameters.Add("@card",SqlDbType.NVarChar,30).Value=User.Identity?.Name ?? string.Empty;
        return await command.ExecuteNonQueryAsync()==1?NoContent():NotFound(new { error="Không tìm thấy yêu cầu đang chờ của bạn." });
    }

    [Authorize(Roles = "Reader")]
    [HttpDelete("me/reservations/{id:int}")]
    public async Task<IActionResult> CancelMyReservation(int id)
    {
        var cardNumber = User.Identity?.Name;
        await using var connection = db.Create();
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using var command = new SqlCommand("""
                UPDATE r SET Status='Cancelled',HeldCopyId=NULL,HoldUntil=NULL
                OUTPUT DELETED.HeldCopyId
                FROM Reservations r JOIN Readers p ON p.ReaderId=r.ReaderId
                WHERE r.ReservationId=@id AND r.Status IN ('Pending','Ready') AND p.CardNumber=@card
                """, connection, tx);
            command.Parameters.Add("@id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@card", SqlDbType.NVarChar, 30).Value = cardNumber!;
            var heldValue = await command.ExecuteScalarAsync();
            if (heldValue is null) { await tx.RollbackAsync(); return NotFound(new { error="Không tìm thấy yêu cầu đang chờ/hẹn lấy." }); }
            if (heldValue != DBNull.Value)
            {
                await using var release = new SqlCommand("UPDATE BookCopies SET Status='Available' WHERE CopyId=@copy AND Status='Reserved'", connection, tx);
                release.Parameters.Add("@copy", SqlDbType.Int).Value = heldValue;
                await release.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            return NoContent();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    [Authorize(Roles = "Admin,Librarian")]
    [HttpGet("card/{cardNumber}")]
    public async Task<IActionResult> LookupCard(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return BadRequest(new { error = "Vui lòng nhập số thẻ bạn đọc." });

        await using var connection = db.Create();
        await connection.OpenAsync();

        // 1. Tra cứu thông tin bạn đọc
        const string readerSql = """
            SELECT ReaderId, CardNumber, FullName, ExpiryDate, Status
            FROM Readers
            WHERE CardNumber = @card
            """;

        int readerId;
        string fullName, status;
        DateTime expiryDate;

        await using (var cmd = new SqlCommand(readerSql, connection))
        {
            cmd.Parameters.Add("@card", SqlDbType.NVarChar, 30).Value = cardNumber.Trim();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return NotFound(new { error = $"Không tìm thấy bạn đọc với số thẻ '{cardNumber}'." });

            readerId = reader.GetInt32(0);
            cardNumber = reader.GetString(1);
            fullName = reader.GetString(2);
            expiryDate = reader.GetDateTime(3);
            status = reader.GetString(4);
        }

        var policy = await LoanPolicyStore.LoadAsync(connection, null);
        var today = DateTime.Today;
        var isExpired = expiryDate.Date < today;

        // 2. Lấy danh sách các cuốn đang mượn (chưa trả)
        const string loansSql = """
            SELECT l.LoanCode, l.LoanDate, d.DueDate, c.CopyCode, b.Title
            FROM LoanDetails d
            JOIN Loans l ON l.LoanId = d.LoanId
            JOIN BookCopies c ON c.CopyId = d.CopyId
            JOIN BookTitles b ON b.BookId = c.BookId
            WHERE l.ReaderId = @readerId AND d.ReturnedAt IS NULL
            ORDER BY d.DueDate ASC
            """;

        var borrowedBooks = new List<object>();
        await using (var cmd = new SqlCommand(loansSql, connection))
        {
            cmd.Parameters.Add("@readerId", SqlDbType.Int).Value = readerId;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var loanCode = reader.GetString(0);
                var loanDate = reader.GetDateTime(1);
                var dueDate = reader.GetDateTime(2);
                var copyCode = reader.GetString(3);
                var title = reader.GetString(4);

                var overdueDays = today > dueDate.Date ? (today - dueDate.Date).Days : 0;
                var fineExpected = overdueDays * policy.FinePerDay;

                borrowedBooks.Add(new
                {
                    loanCode,
                    copyCode,
                    title,
                    loanDate = loanDate.ToString("yyyy-MM-dd"),
                    dueDate = dueDate.ToString("yyyy-MM-dd"),
                    overdueDays,
                    fineExpected,
                    isOverdue = overdueDays > 0
                });
            }
        }

        return Ok(new
        {
            cardNumber,
            fullName,
            expiryDate = expiryDate.ToString("yyyy-MM-dd"),
            status,
            isExpired,
            canBorrow = status == "Active" && !isExpired && borrowedBooks.Count < policy.MaxLoans,
            openLoansCount = borrowedBooks.Count,
            maxLoans = policy.MaxLoans,
            borrowedBooks
        });
    }
}
