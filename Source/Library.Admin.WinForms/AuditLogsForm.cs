using System.Net.Http.Json;

namespace Library.Admin.WinForms;

internal sealed class AuditLogsForm : Form
{
    private sealed record AuditRow(long AuditId,string Username,string Action,string? Entity,string? EntityId,string CreatedAt);
    private readonly HttpClient _api;
    private readonly TextBox _query=new(){Width=240,PlaceholderText="Tài khoản, thao tác hoặc mã dữ liệu"};
    private readonly DataGridView _grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
    private readonly ToolStripStatusLabel _status=new("Sẵn sàng");

    public AuditLogsForm(HttpClient api)
    {
        _api=api;Text="Nhật ký hoạt động hệ thống";Width=1000;Height=620;StartPosition=FormStartPosition.CenterParent;
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(8)};
        var search=new Button{Text="Tìm kiếm",AutoSize=true};search.Click+=async(_,_)=>await LoadAsync();
        var reload=new Button{Text="Tải lại",AutoSize=true};reload.Click+=async(_,_)=>{_query.Clear();await LoadAsync();};
        bar.Controls.AddRange([_query,search,reload]);_query.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await LoadAsync();}};
        var strip=new StatusStrip();strip.Items.Add(_status);Controls.Add(_grid);Controls.Add(bar);Controls.Add(strip);Load+=async(_,_)=>await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var rows=await _api.GetFromJsonAsync<List<AuditRow>>($"api/admin/audit-logs?q={Uri.EscapeDataString(_query.Text.Trim())}")??[];
            _grid.DataSource=rows.Select(x=>new{x.AuditId,x.CreatedAt,x.Username,x.Action,x.Entity,x.EntityId}).ToList();
            _status.Text=$"Đã tải {rows.Count} sự kiện (tối đa 500 gần nhất)";
        }
        catch(Exception ex){_status.Text="Không tải được nhật ký";MessageBox.Show(ex.Message,"Lỗi API",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
}
