using System.Net.Http.Json;
using System.Text.Json;

namespace Library.Admin.WinForms;

internal sealed class ReservationsForm : Form
{
    private sealed record ReservationRow(int ReservationId,string Status,string RequestedAt,string? HoldUntil,
        string ReaderCode,string CardNumber,string FullName,int BookId,string Title,string? CopyCode);
    private readonly HttpClient _api;
    private readonly DataGridView _grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
    private readonly ComboBox _status=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=130};
    private readonly ToolStripStatusLabel _message=new("Sẵn sàng");

    public ReservationsForm(HttpClient api)
    {
        _api=api; Text="Xử lý đặt trước"; Width=1120; Height=620; StartPosition=FormStartPosition.CenterParent;
        _status.Items.AddRange(["Tất cả","Pending","Ready","Fulfilled","Cancelled"]); _status.SelectedIndex=0;
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(6),WrapContents=false};
        var reload=new Button{Text="Tải lại",AutoSize=true}; reload.Click+=async(_,_)=>await LoadRowsAsync();
        var ready=new Button{Text="Giữ sách / Báo có sách",AutoSize=true}; ready.Click+=async(_,_)=>await ChangeStatusAsync("Ready");
        var fulfilled=new Button{Text="Hoàn tất mượn",AutoSize=true}; fulfilled.Click+=async(_,_)=>await ChangeStatusAsync("Fulfilled");
        var cancel=new Button{Text="Hủy yêu cầu",AutoSize=true}; cancel.Click+=async(_,_)=>await ChangeStatusAsync("Cancelled");
        bar.Controls.AddRange([new Label{Text="Trạng thái:",AutoSize=true,Padding=new Padding(0,7,0,0)},_status,reload,ready,fulfilled,cancel]);
        var bottom=new StatusStrip(); bottom.Items.Add(_message);
        Controls.Add(_grid); Controls.Add(bar); Controls.Add(bottom);
        _status.SelectedIndexChanged+=async(_,_)=>await LoadRowsAsync(); Load+=async(_,_)=>await LoadRowsAsync();
    }

    private async Task LoadRowsAsync()
    {
        try
        {
            var status=_status.SelectedIndex<=0?string.Empty:$"?status={Uri.EscapeDataString(_status.Text)}";
            var rows=await _api.GetFromJsonAsync<List<ReservationRow>>($"api/admin/reservations{status}")??[];
            _grid.DataSource=rows.Select(x=>new {x.ReservationId,x.Status,x.RequestedAt,x.HoldUntil,x.ReaderCode,x.CardNumber,x.FullName,x.BookId,x.Title,x.CopyCode}).ToList();
            _message.Text=$"Đã tải {rows.Count} yêu cầu";
        }
        catch(Exception ex){_message.Text="Không tải được yêu cầu đặt trước";MessageBox.Show(ex.Message,"Lỗi API",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private async Task ChangeStatusAsync(string status)
    {
        if(_grid.CurrentRow is null||!int.TryParse(_grid.CurrentRow.Cells[nameof(ReservationRow.ReservationId)].Value?.ToString(),out var id))
        {MessageBox.Show("Hãy chọn một yêu cầu trước.","Chưa chọn yêu cầu",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using var response=await _api.PutAsJsonAsync($"api/admin/reservations/{id}/status",new{status});
        if(!response.IsSuccessStatusCode){var body=await response.Content.ReadAsStringAsync();MessageBox.Show(body,"Không thể xử lý yêu cầu",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        _message.Text=status switch{"Ready"=>"Đã giữ bản sao trong 3 ngày.","Fulfilled"=>"Đã lập phiếu mượn và chuyển bản sao sang Borrowed.",_=>"Đã hủy yêu cầu."};
        await LoadRowsAsync();
    }
}
