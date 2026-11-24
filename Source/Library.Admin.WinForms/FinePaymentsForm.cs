using System.Net.Http.Json;

namespace Library.Admin.WinForms;

internal sealed class FinePaymentsForm : Form
{
    private sealed record FineRow(int ReturnId,string CardNumber,string FullName,string Title,string CopyCode,
        decimal FineAmount,decimal OverdueFine,decimal CompensationAmount,decimal PaidAmount,decimal Balance,string ReturnedAt);
    private readonly HttpClient _api;
    private readonly NumericUpDown _amount=new(){Width=150,Minimum=0,Maximum=1000000000,ThousandsSeparator=true,DecimalPlaces=0};
    private readonly DataGridView _grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
    private readonly ToolStripStatusLabel _message=new("Sẵn sàng");

    public FinePaymentsForm(HttpClient api)
    {
        _api=api;Text="Thu và theo dõi tiền phạt";Width=1050;Height=580;StartPosition=FormStartPosition.CenterParent;
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=54,Padding=new Padding(6),WrapContents=false};
        var reload=new Button{Text="Tải khoản còn nợ",AutoSize=true};reload.Click+=async(_,_)=>await LoadRowsAsync();
        var payPartial=new Button{Text="Thu số tiền nhập",AutoSize=true};payPartial.Click+=async(_,_)=>await RecordPaymentAsync(false);
        var payFull=new Button{Text="Thu hết số còn lại",AutoSize=true};payFull.Click+=async(_,_)=>await RecordPaymentAsync(true);
        bar.Controls.AddRange([reload,new Label{Text="Số tiền thu:",AutoSize=true,Padding=new Padding(0,8,0,0)},_amount,payPartial,payFull]);var bottom=new StatusStrip();bottom.Items.Add(_message);
        Controls.Add(_grid);Controls.Add(bar);Controls.Add(bottom);Load+=async(_,_)=>await LoadRowsAsync();
    }

    private async Task LoadRowsAsync()
    {
        try
        {
            var rows=await _api.GetFromJsonAsync<List<FineRow>>("api/admin/payments/outstanding")??[];
            _grid.DataSource=rows.Select(x=>new{x.ReturnId,x.CardNumber,x.FullName,x.Title,x.CopyCode,x.OverdueFine,x.CompensationAmount,x.FineAmount,x.PaidAmount,x.Balance,x.ReturnedAt}).ToList();
            _message.Text=$"Còn {rows.Count} khoản phạt chưa thu đủ";
        }
        catch(Exception ex){_message.Text="Không tải được khoản phạt";MessageBox.Show(ex.Message,"Lỗi API",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }

    private async Task RecordPaymentAsync(bool payFull)
    {
        if(_grid.CurrentRow is null||!int.TryParse(_grid.CurrentRow.Cells[nameof(FineRow.ReturnId)].Value?.ToString(),out var id)||
            !decimal.TryParse(_grid.CurrentRow.Cells[nameof(FineRow.Balance)].Value?.ToString(),out var balance))
        {MessageBox.Show("Hãy chọn khoản phạt cần thu.","Chưa chọn khoản phạt",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        var amount=payFull?balance:_amount.Value;
        if(amount<=0||amount>balance){MessageBox.Show($"Nhập số tiền lớn hơn 0 và không vượt số còn nợ {balance:N0} đ.","Số tiền không hợp lệ",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        if(MessageBox.Show($"Ghi nhận đã thu {amount:N0} đ?","Xác nhận thanh toán",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        using var response=await _api.PostAsJsonAsync($"api/admin/payments/{id}",new{amount});
        if(!response.IsSuccessStatusCode){var body=await response.Content.ReadAsStringAsync();MessageBox.Show(body,"Không thể ghi thanh toán",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
        _message.Text=$"Đã ghi nhận thanh toán {amount:N0} đ";_amount.Value=0;await LoadRowsAsync();
    }
}
