# Bài thực hành: OrderTestingLab

**Môn:** Kiểm thử API với C# (ASP.NET Core)  
**Phiên bản:** .NET 8  
**Tên solution:** `OrderTestingLab`

**Chính sách công ty (bắt buộc):** Toàn bộ mã ứng dụng API (entity, DTO, service, repository, DbContext, controller, `Program.cs`) nằm trong **một project duy nhất** — `OrderTestingLab.API`. Không tách Domain / Application / Infrastructure thành project riêng. Solution vẫn có thêm **project test** (unit + integration) vì đó không phải “API project”.

---

## 1. Giới thiệu

Sinh viên xây một Web API quản lý **Order** trong **một project Web API**, tổ chức code theo **thư mục** (`Entities`, `Dtos`, `Services`, …). Sau đó viết **unit test** (Moq, FluentAssertions, xUnit) và **integration test** (`WebApplicationFactory`, SQLite In-Memory).

---

## 2. Mục tiêu

1. Build được solution.  
2. Chạy được API (SQLite file).  
3. Chạy được unit test và integration test.  
4. Xác minh dữ liệu trong DB (integration test đọc `AppDbContext`).

---

## 3. Công nghệ sử dụng

| Công nghệ | Vai trò |
|-----------|---------|
| **xUnit** | Framework test. |
| **Moq** | Mock `IOrderRepository` / `IOrderService`. |
| **FluentAssertions** | Assert dễ đọc. |
| **WebApplicationFactory&lt;Program&gt;** | Host API trong process, gọi HTTP thật. |
| **SQLite In-Memory** | DB cho integration test (giữ `SqliteConnection` mở). |
| **EF Core + SQLite (file)** | DB khi chạy API (`OrderTestingLab.db`). |

---

## 4. Cấu trúc solution

```
OrderTestingLab/
  OrderTestingLab.sln
  src/
    OrderTestingLab.API/          ← toàn bộ mã API (một project)
      Entities/
      Dtos/
      Interfaces/
      Services/
      Persistence/
      Repositories/
      Controllers/
      Program.cs
  tests/
    OrderTestingLab.UnitTests/
    OrderTestingLab.IntegrationTests/
```

**Namespace gốc:** `OrderTestingLab` (ví dụ `OrderTestingLab.Entities`, `OrderTestingLab.Dtos`, `OrderTestingLab.Persistence`, `OrderTestingLab.Controllers`).

---

## 5. Các bước tạo project (dotnet CLI)

Chạy tại thư mục cha (ví dụ `C:\UnitFunctionalTestPractice`).

### 5.1 — Tạo thư mục và solution

```powershell
mkdir OrderTestingLab
cd OrderTestingLab
dotnet new sln -n OrderTestingLab
mkdir src
mkdir tests
```

### 5.2 — Chỉ tạo Web API + 2 project test (không tạo class library Domain/Application/Infrastructure)

```powershell
dotnet new webapi -n OrderTestingLab.API -o src/OrderTestingLab.API -f net8.0 --no-openapi
dotnet new xunit -n OrderTestingLab.UnitTests -o tests/OrderTestingLab.UnitTests -f net8.0
dotnet new xunit -n OrderTestingLab.IntegrationTests -o tests/OrderTestingLab.IntegrationTests -f net8.0
```

### 5.3 — Thêm vào solution

```powershell
dotnet sln add src/OrderTestingLab.API/OrderTestingLab.API.csproj
dotnet sln add tests/OrderTestingLab.UnitTests/OrderTestingLab.UnitTests.csproj
dotnet sln add tests/OrderTestingLab.IntegrationTests/OrderTestingLab.IntegrationTests.csproj
```

### 5.4 — Reference: test projects chỉ tham chiếu API

```powershell
dotnet add tests/OrderTestingLab.UnitTests/OrderTestingLab.UnitTests.csproj reference src/OrderTestingLab.API/OrderTestingLab.API.csproj
dotnet add tests/OrderTestingLab.IntegrationTests/OrderTestingLab.IntegrationTests.csproj reference src/OrderTestingLab.API/OrderTestingLab.API.csproj
```

### 5.5 — Package NuGet

**Project API** (EF Core + Swagger):

```powershell
dotnet add src/OrderTestingLab.API/OrderTestingLab.API.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.11
dotnet add src/OrderTestingLab.API/OrderTestingLab.API.csproj package Swashbuckle.AspNetCore --version 6.6.2
```

**UnitTests:**

```powershell
dotnet add tests/OrderTestingLab.UnitTests/OrderTestingLab.UnitTests.csproj package Moq --version 4.20.72
dotnet add tests/OrderTestingLab.UnitTests/OrderTestingLab.UnitTests.csproj package FluentAssertions --version 6.12.0
```

**IntegrationTests:**

```powershell
dotnet add tests/OrderTestingLab.IntegrationTests/OrderTestingLab.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 8.0.11
dotnet add tests/OrderTestingLab.IntegrationTests/OrderTestingLab.IntegrationTests.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.11
dotnet add tests/OrderTestingLab.IntegrationTests/OrderTestingLab.IntegrationTests.csproj package FluentAssertions --version 6.12.0
```

### 5.6 — Trong `OrderTestingLab.API.csproj`

Có thể đặt `<RootNamespace>OrderTestingLab</RootNamespace>` để namespace khớp thư mục (tùy chọn).

### 5.7 — Xóa file mẫu

- Xóa `UnitTest1.cs` trong các project test.  
- Thay `Program.cs` (template weather) bằng code lab.

---

## 6. Full code — nơi lưu trong repo

Toàn bộ **mã nguồn đầy đủ** nằm trong thư mục:

`src/OrderTestingLab.API/`

| Thư mục / file | Nội dung |
|----------------|----------|
| `Entities/Order.cs` | Entity EF |
| `Dtos/CreateOrderRequest.cs`, `OrderResponse.cs` | DTO + validation |
| `Interfaces/IOrderRepository.cs`, `IOrderService.cs` | Hợp đồng |
| `Services/OrderService.cs` | Nghiệp vụ (trim, lower email, TotalAmount) |
| `Persistence/AppDbContext.cs` | DbContext, bảng `Orders` |
| `Repositories/OrderRepository.cs` | EF repository |
| `Controllers/OrdersController.cs` | `POST` / `GET` |
| `Program.cs` | DI, Swagger, SQLite file, `EnsureCreated`, `public partial class Program` |
| `appsettings.json` | `ConnectionStrings:Default` |

**Tests:**

- `tests/OrderTestingLab.UnitTests/OrderServiceTests.cs`, `OrdersControllerTests.cs`
- `tests/OrderTestingLab.IntegrationTests/CustomWebApplicationFactory.cs`, `OrdersIntegrationTests.cs`

---

## 7. Cách chạy

```powershell
cd OrderTestingLab
dotnet build
dotnet test
dotnet run --project src/OrderTestingLab.API/OrderTestingLab.API.csproj
```

Swagger (Development): mở `/swagger`.

---

## 8. Kết quả mong đợi

- **POST** hợp lệ → **201 Created**, body JSON có `customerName` đã trim, `email` lowercase, `totalAmount` = `quantity * unitPrice`.  
- **POST** thiếu email → **400 BadRequest**, nội dung có thông tin lỗi liên quan **Email**.  
- `dotnet test` → **5** test passed (3 unit + 2 integration).

---

## 9. Sai lầm thường gặp

| Vấn đề | Gợi ý |
|--------|--------|
| Sếp không cho nhiều project API | Gom code vào `OrderTestingLab.API`, chỉ tách **test** project. |
| `WebApplicationFactory` không thấy `Program` | Thêm `public partial class Program { }` cuối `Program.cs`. |
| SQLite in-memory “mất” dữ liệu | Một `SqliteConnection` mở + `UseSqlite(connection)` chung trong factory. |

---

**Kết thúc bài thực hành (phiên bản 1 project API).**
