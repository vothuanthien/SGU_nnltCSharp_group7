using System.Net.Http.Json;

namespace Library.Admin.WinForms;

internal sealed class LoanRequestsForm:Form
{
    private sealed record RequestRow(int RequestId,string Status,string RequestedAt,string ReaderCode,string CardNumber,
        string FullName,int BookId,string Title,int AvailableCopies,string? RejectionReason,string? CopyCode,string? LoanCode);
    private readonly HttpClient _api;
    private readonly DataGridView _grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
    private readonly ComboBox _status=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=130};
    private readonly ToolStripStatusLabel _message=new("Sẵn sàng");

    public LoanRequestsForm(HttpClient api)
    {
        _api=api;Text="Duyệt yêu cầu mượn sách";Width=1180;Height=620;StartPosition=FormStartPosition.CenterParent;
        _status.Items.AddRange(["Tất cả","Requested","Approved","Rejected","Cancelled"]);_status.SelectedIndex=0;
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(6),WrapContents=false};
        var reload=new Button{Text="Tải lại",AutoSize=true};reload.Click+=async(_,_)=>await LoadRowsAsync();
        var approve=new Button{Text="Duyệt và lập phiếu",AutoSize=true};approve.Click+=async(_,_)=>await DecideAsync("Approved");
        var reject=new Button{Text="Từ chối",AutoSize=true};reject.Click+=async(_,_)=>await DecideAsync("Rejected");
        bar.Controls.AddRange([new Label{Text="Trạng thái:",AutoSize=true,Padding=new Padding(0,7,0,0)},_status,reload,approve,reject]);
        var bottom=new StatusStrip();bottom.Items.Add(_message);
        Controls.Add(_grid);Controls.Add(bar);Controls.Add(bottom);
        _status.SelectedIndexChanged+=async(_,_)=>await LoadRowsAsync();Load+=async(_,_)=>await LoadRowsAsync();
    }

    private async Task LoadRowsAsync()
    {
        try
        {
            var query=_status.SelectedIndex==0?string.Empty:$"?status={Uri.EscapeDataString(_status.Text)}";
            var rows=await _api.GetFromJsonAsync<List<RequestRow>>($"api/admin/loan-requests{query}")??[];
            _grid.DataSource=rows.Select(x=>new{x.RequestId,x.Status,x.RequestedAt,x.ReaderCode,x.CardNumber,x.FullName,x.BookId,x.Title,x.AvailableCopies,x.RejectionReason,x.CopyCode,x.LoanCode}).ToList();
            _message.Text=$"Đã tải {rows.Count} yêu cầu";
        }
        catch(Exception ex){_message.Text="Không tải được yêu cầu mượn";MessageBox.Show(ex.Message,"Lỗi API",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private async Task DecideAsync(string status)
    {
        if(_grid.CurrentRow is null||!int.TryParse(_grid.CurrentRow.Cells[nameof(RequestRow.RequestId)].Value?.ToString(),out var id))
        {MessageBox.Show("Hãy chọn một yêu cầu trước.","Chưa chọn yêu cầu",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        string? reason=null;
        if(status=="Rejected")
        {
            using var dialog=new RejectionReasonDialog();
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            reason=dialog.Reason;
        }
        using var response=await _api.PutAsJsonAsync($"api/admin/loan-requests/{id}",new{status,rejectionReason=reason});
        if(!response.IsSuccessStatusCode){var body=await response.Content.ReadAsStringAsync();MessageBox.Show(body,"Không thể xử lý yêu cầu",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        _message.Text=status=="Approved"?"Đã duyệt và lập phiếu mượn.":"Đã từ chối yêu cầu.";
        await LoadRowsAsync();
    }
}

internal sealed class RejectionReasonDialog:Form
{
    private readonly TextBox _reason=new(){Dock=DockStyle.Fill,MaxLength=300,PlaceholderText="Lý do (không bắt buộc)"};
    public string? Reason=>string.IsNullOrWhiteSpace(_reason.Text)?null:_reason.Text.Trim();
    public RejectionReasonDialog()
    {
        Text="Từ chối yêu cầu mượn";Width=430;Height=170;StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=2};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
        var reject=new Button{Text="Từ chối",AutoSize=true};reject.Click+=(_,_)=>{DialogResult=DialogResult.OK;Close();};
        var cancel=new Button{Text="Hủy",AutoSize=true,DialogResult=DialogResult.Cancel};
        buttons.Controls.AddRange([reject,cancel]);layout.Controls.Add(_reason,0,0);layout.Controls.Add(buttons,0,1);Controls.Add(layout);AcceptButton=reject;CancelButton=cancel;
    }
}
