using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Admin.WinForms;

/// <summary>
/// Hộp thoại chọn và gán tác giả cho đầu sách bằng CheckedListBox trực quan.
/// </summary>
internal sealed class BookAuthorsForm : Form
{
    private sealed record AuthorItem(int Id, string FullName)
    {
        public override string ToString() => FullName;
    }

    private readonly HttpClient _api;
    private readonly int _bookId;
    private readonly CheckedListBox _list = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray, Dock = DockStyle.Bottom, Padding = new Padding(8) };
    private readonly Button _save = new() { Text = "Lưu thay đổi", AutoSize = true, Enabled = false };
    private readonly Button _cancel = new() { Text = "Đóng", AutoSize = true, DialogResult = DialogResult.Cancel };

    public BookAuthorsForm(HttpClient api, int bookId, string bookTitle)
    {
        _api = api;
        _bookId = bookId;
        Text = $"Gán tác giả cho sách: {bookTitle}";
        Width = 480;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var header = new Label
        {
            Text = $"Chọn các tác giả cho sách:\n\"{bookTitle}\"",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        buttonPanel.Controls.AddRange([_cancel, _save]);

        Controls.Add(_list);
        Controls.Add(_status);
        Controls.Add(buttonPanel);
        Controls.Add(header);

        AcceptButton = _save;
        CancelButton = _cancel;

        _save.Click += async (_, _) => await SaveAuthorsAsync();
        Load += async (_, _) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _status.Text = "Đang tải danh sách tác giả...";
        try
        {
            // 1. Tải tất cả tác giả từ API
            var authors = await _api.GetFromJsonAsync<List<Dictionary<string, JsonElement>>>("api/admin/catalog/authors") ?? [];
            var authorItems = new List<AuthorItem>();
            foreach (var a in authors)
            {
                if (a.TryGetValue("id", out var idElem) && idElem.TryGetInt32(out var id) &&
                    a.TryGetValue("fullName", out var nameElem))
                {
                    authorItems.Add(new AuthorItem(id, nameElem.GetString() ?? $"Tác giả #{id}"));
                }
            }

            // 2. Tải danh sách tác giả hiện tại của sách
            var current = await _api.GetFromJsonAsync<JsonElement>($"api/admin/catalog/books/{_bookId}/authors");
            var assignedIds = current.GetProperty("authorIds").EnumerateArray().Select(x => x.GetInt32()).ToHashSet();

            // 3. Đưa vào CheckedListBox
            _list.Items.Clear();
            foreach (var item in authorItems)
            {
                var isChecked = assignedIds.Contains(item.Id);
                _list.Items.Add(item, isChecked);
            }

            _status.Text = $"Đã tải {authorItems.Count} tác giả. Đã gán {assignedIds.Count} tác giả.";
            _save.Enabled = true;
        }
        catch (Exception ex)
        {
            _status.Text = "Lỗi khi tải dữ liệu: " + ex.Message;
            MessageBox.Show(ex.Message, "Lỗi tải tác giả", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveAuthorsAsync()
    {
        _save.Enabled = false;
        _status.Text = "Đang lưu thay đổi...";
        try
        {
            var selectedIds = new List<int>();
            foreach (var item in _list.CheckedItems)
            {
                if (item is AuthorItem a) selectedIds.Add(a.Id);
            }

            var response = await _api.PutAsJsonAsync($"api/admin/catalog/books/{_bookId}/authors", new { authorIds = selectedIds });
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                MessageBox.Show(error, "Không thể lưu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show("Cập nhật danh sách tác giả thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _save.Enabled = true;
        }
    }
}
