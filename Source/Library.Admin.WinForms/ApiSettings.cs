namespace Library.Admin.WinForms;

/// <summary>
/// Địa chỉ Library.Api dùng chung cho đăng nhập và quản trị.
/// Mặc định http://localhost:5001/ (API chạy trong Visual Studio hoặc Docker đều ánh xạ cổng 5001);
/// đổi bằng biến môi trường LIBRARY_API_URL.
/// </summary>
internal static class ApiSettings
{
    public static Uri BaseUri
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("LIBRARY_API_URL");
            if (string.IsNullOrWhiteSpace(value)) value = "http://localhost:5001/";
            if (!value.EndsWith('/')) value += "/";
            return new Uri(value);
        }
    }
}
