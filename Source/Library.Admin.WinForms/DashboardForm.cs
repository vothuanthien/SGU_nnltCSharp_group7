using System.Globalization;
using System.Net.Http.Json;
using System.Text;

namespace Library.Admin.WinForms;

/// <summary>Hiển thị số liệu và xuất báo cáo CSV lấy từ API quản trị.</summary>
internal sealed class DashboardForm : Form
{
    private sealed record DashboardStats(
        int totalActiveBooks,
        int totalBooks,
        int totalCopies,
        int borrowedCopies,
        int availableCopies,
        int overdueLoans,
        decimal totalFinesCollected,
        decimal totalFinesIncurred,
        string updatedAt);

    private sealed record BookExport(int bookId, string title, string genre, string publisher, string authors,
        int totalCopies, int availableCopies, string status);

    private sealed record LoanExport(string loanCode, string cardNumber, string readerName, string copyCode,
        string bookTitle, string loanDate, string dueDate, string returnedAt, string status, decimal fineAmount);

    private readonly HttpClient _api;
    private readonly Label[] _values = Enumerable.Range(0, 5)
        .Select(_ => new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = Color.DarkSlateBlue })
        .ToArray();
    private readonly Label _updated = new() { AutoSize = true, ForeColor = Color.DimGray, Text = "Chưa tải dữ liệu" };
    private readonly ComboBox _report = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _refresh = new() { Text = "Làm mới", AutoSize = true };
    private readonly Button _export = new() { Text = "Xuất CSV…", AutoSize = true };

    public DashboardForm(HttpClient api)
    {
        _api = api;
        Text = "Dashboard thư viện";
        Width = 920;
        Height = 580;
        StartPosition = FormStartPosition.CenterParent;

        var names = new[]
        {
            "Đầu sách đang phục vụ",
            "Tổng bản sao",
            "Bản sao đang mượn",
            "Lượt mượn quá hạn",
            "Tiền phạt đã thu"
        };
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 3, RowCount = 2 };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        for (var i = 0; i < names.Length; i++)
        {
            var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8), Padding = new Padding(8), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            card.Controls.Add(_values[i]);
            var label = new Label { Text = names[i], Dock = DockStyle.Top, Height = 45, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            card.Controls.Add(label);
            cards.Controls.Add(card, i % 3, i / 3);
        }

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(12), WrapContents = false };
        _report.Items.AddRange(["Danh sách sách", "Phiếu mượn"]);
        _report.SelectedIndex = 0;
        _refresh.Click += async (_, _) => await LoadStatsAsync();
        _export.Click += async (_, _) => await ExportAsync();
        toolbar.Controls.AddRange([_refresh, _updated, _report, _export]);
        Controls.Add(cards);
        Controls.Add(toolbar);
        Load += async (_, _) => await LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        _refresh.Enabled = false;
        try
        {
            var stats = await _api.GetFromJsonAsync<DashboardStats>("api/admin/dashboard/stats")
                ?? throw new InvalidOperationException("API không trả về số liệu dashboard.");
            _values[0].Text = $"{stats.totalActiveBooks:N0} / {stats.totalBooks:N0}";
            _values[1].Text = stats.totalCopies.ToString("N0");
            _values[2].Text = stats.borrowedCopies.ToString("N0");
            _values[3].Text = stats.overdueLoans.ToString("N0");
            _values[4].Text = $"{stats.totalFinesCollected:N0} đ";
            _updated.Text = $"Cập nhật lúc {stats.updatedAt}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không tải được dashboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _refresh.Enabled = true;
        }
    }

    private async Task ExportAsync()
    {
        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "csv",
            Filter = "Tệp CSV (mở bằng Excel)|*.csv",
            FileName = $"{(_report.SelectedIndex == 0 ? "DanhSachSach" : "PhieuMuon")}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _export.Enabled = false;
        try
        {
            string csv = _report.SelectedIndex == 0
                ? await ExportBooksAsync()
                : await ExportLoansAsync();
            await File.WriteAllTextAsync(dialog.FileName, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            MessageBox.Show($"Đã xuất báo cáo:\n{dialog.FileName}", "Xuất báo cáo thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không thể xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _export.Enabled = true;
        }
    }

    private async Task<string> ExportBooksAsync()
    {
        var rows = await _api.GetFromJsonAsync<List<BookExport>>("api/admin/dashboard/export/books") ?? [];
        var csv = new StringBuilder();
        AddCsvRow(csv, ["Mã sách", "Tên sách", "Thể loại", "Nhà xuất bản", "Tác giả", "Tổng bản sao", "Bản sao khả dụng", "Trạng thái"]);
        foreach (var row in rows)
            AddCsvRow(csv, [row.bookId.ToString(CultureInfo.InvariantCulture), row.title, row.genre, row.publisher,
                row.authors, row.totalCopies.ToString(CultureInfo.InvariantCulture),
                row.availableCopies.ToString(CultureInfo.InvariantCulture), row.status]);
        return csv.ToString();
    }

    private async Task<string> ExportLoansAsync()
    {
        var rows = await _api.GetFromJsonAsync<List<LoanExport>>("api/admin/dashboard/export/loans") ?? [];
        var csv = new StringBuilder();
        AddCsvRow(csv, ["Mã phiếu", "Số thẻ", "Tên bạn đọc", "Mã bản sao", "Tên sách", "Ngày mượn", "Hạn trả", "Ngày trả", "Trạng thái", "Tiền phạt"]);
        foreach (var row in rows)
            AddCsvRow(csv, [row.loanCode, row.cardNumber, row.readerName, row.copyCode, row.bookTitle,
                row.loanDate, row.dueDate, row.returnedAt, row.status, row.fineAmount.ToString(CultureInfo.InvariantCulture)]);
        return csv.ToString();
    }

    private static void AddCsvRow(StringBuilder csv, IEnumerable<string> cells)
    {
        csv.AppendLine(string.Join(',', cells.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r') text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
