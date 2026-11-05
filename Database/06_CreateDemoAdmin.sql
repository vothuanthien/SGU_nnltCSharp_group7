/*
    Tài khoản demo dành riêng cho môi trường học tập chạy localhost.
    Username: admin
    Password: Admin@12345

    PasswordHash dùng PBKDF2-SHA256, 210.000 vòng lặp.
    Không dùng tài khoản/mật khẩu này trên hệ thống thật hoặc máy chủ công khai.
*/
SET QUOTED_IDENTIFIER ON;
USE LibraryManagement;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE Username = N'admin')
BEGIN
    INSERT INTO dbo.Accounts(Username, PasswordHash, Role, IsActive)
    VALUES
    (
        N'admin',
        N'PBKDF2-SHA256$210000$XyqcfR5LY4Cl2fIMezFG7g==$4lHJC4BcUsZZ0U4HeWhkL5MZVzbTok5NUmIpe5DABys=',
        'Admin',
        1
    );
END;
GO
SELECT AccountId, Username, Role, IsActive
FROM dbo.Accounts
WHERE Username = N'admin';
GO
