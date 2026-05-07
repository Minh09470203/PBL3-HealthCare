# Sử dụng SDK 9.0 để build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy toàn bộ file trong thư mục hiện tại vào
COPY . .

# Restore và Publish (không cần ghi đường dẫn dài dòng nữa)
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Giai đoạn chạy
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Render dùng cổng 10000
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# Chạy file dll (Sếp kiểm tra đúng tên file dll nhé)
ENTRYPOINT ["dotnet", "PBL3_HealthCare.dll"]