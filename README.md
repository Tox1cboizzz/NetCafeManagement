# NetCafe Management System

Hệ thống quản lý tiệm net sử dụng .NET 8 Microservices Architecture.

## Kiến trúc

```
ApiGateway (Ocelot) :5000
├── AuthService       :5001  → Đăng ký, đăng nhập, JWT
├── WalletService     :5002  → Số dư, nạp tiền, lịch sử giao dịch
├── MachineService    :5003  → Quản lý máy tính
├── SessionService    :5004  → Phiên chơi, tính tiền, hóa đơn
├── FoodService       :5005  → Menu đồ ăn, order
└── ReportService     :5006  → Báo cáo doanh thu
```

## Tech Stack

- **.NET 8** — Web API
- **Clean Architecture** — Domain / Application / Infrastructure / API
- **MediatR** — CQRS pattern
- **Entity Framework Core** + **SQL Server** — ORM
- **JWT Bearer** — Authentication
- **Ocelot** — API Gateway
- **BCrypt** — Password hashing
- **Swagger** — API documentation

## Business Rules

- **MemberTier:** Khách nạp tổng ≥ 10,000,000đ → tự động lên VIP, giảm 5% giá giờ chơi
- **Máy Normal / Premium** — giá/giờ config riêng từng máy
- **Session** lưu giá tại thời điểm mở → không bị ảnh hưởng khi admin đổi giá sau

## Cách chạy

### Yêu cầu
- .NET 8 SDK
- SQL Server (LocalDB hoặc full)

### Chạy từng service

```bash
# AuthService
cd AuthService/src/AuthService.API
dotnet run

# WalletService
cd WalletService/src/WalletService.API
dotnet run

# MachineService
cd MachineService/src/MachineService.API
dotnet run

# SessionService
cd SessionService/src/SessionService.API
dotnet run

# FoodService
cd FoodService/src/FoodService.API
dotnet run

# ReportService
cd ReportService/src/ReportService.API
dotnet run

# ApiGateway
cd ApiGateway
dotnet run
```

### Migration (chạy lần đầu, từng service)
```bash
cd AuthService/src/AuthService.API
dotnet ef migrations add InitialCreate --project ../AuthService.Infrastructure
dotnet ef database update
```

> Các service khác làm tương tự. DB sẽ tự migrate khi khởi động.

## API Flow cơ bản

```
1. POST /api/auth/register     → Tạo tài khoản
2. POST /api/auth/login        → Lấy JWT token
3. POST /api/wallet            → Tạo wallet cho user (Admin)
4. POST /api/wallet/topup      → Nạp tiền (Staff/Admin)
5. POST /api/machines          → Thêm máy (Admin)
6. POST /api/machines/{id}/occupy → Gán khách vào máy
7. POST /api/sessions/start    → Mở phiên chơi
8. POST /api/food/orders       → Order đồ ăn trong phiên
9. POST /api/sessions/{id}/close → Đóng phiên, xuất hóa đơn
10. GET /api/reports/daily     → Xem báo cáo doanh thu
```

## Roles

| Role     | Quyền |
|----------|-------|
| Admin    | Toàn quyền |
| Staff    | Quản lý máy, session, order, nạp tiền |
| Customer | Xem thông tin cá nhân, session, menu |
