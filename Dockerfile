# Dockerfile cho Library.Api (build context = thư mục gốc solution).
# Chỉ build/publish Library.Api; Library.Web và Library.Admin.WinForms chạy trực tiếp trên Windows.

FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /src

# Copy riêng các file .csproj trước để Docker cache bước restore khi chỉ sửa mã nguồn.
COPY Source/Library.Domain/Library.Domain.csproj Source/Library.Domain/
COPY Source/Library.Infrastructure/Library.Infrastructure.csproj Source/Library.Infrastructure/
COPY Source/Library.Api/Library.Api.csproj Source/Library.Api/
RUN dotnet restore Source/Library.Api/Library.Api.csproj

# Copy mã nguồn của API và các project nó phụ thuộc (Domain, Infrastructure).
COPY Source/Library.Domain/ Source/Library.Domain/
COPY Source/Library.Infrastructure/ Source/Library.Infrastructure/
COPY Source/Library.Api/ Source/Library.Api/
RUN dotnet publish Source/Library.Api/Library.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Script SQL để API tự tạo database/dữ liệu mẫu lần chạy đầu (Database__AutoInitialize=true).
COPY Database/ ./Database/

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "Library.Api.dll"]
