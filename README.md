# Dự Án: Bán Vé Sự Kiện Có Sơ Đồ Ghế

Tài liệu hướng dẫn khởi tạo, phát triển và triển khai hạ tầng nền tảng cho User Story **S-01: Khung ứng dụng chạy được trên staging**.

---

## 1. Kiến Trúc & Cấu Trúc Thư Mục

Dự án được tổ chức theo mô hình phân tầng đơn giản, dễ tiếp cận cho các bạn thực tập sinh:

```text
d:\minh antigravity\dự án\
├── .github/
│   └── workflows/
│       ├── ci.yml                     # Pipeline CI (Build, Lint, Typecheck, Test song song)
│       └── deploy-staging.yml         # Pipeline CD (Build SHA image, Blue-Green deploy qua SSH)
├── deploy/
│   └── staging/
│       ├── docker-compose.staging.yml # Cấu hình chạy DB, Redis, Nginx và Blue/Green slots
│       ├── env.staging.example        # Mẫu biến môi trường trên máy chủ Staging
│       ├── nginx/                     # Reverse Proxy điều phối traffic Blue/Green
│       │   ├── nginx.conf
│       │   └── conf.d/
│       │       ├── default.conf       # Khai báo location /health/, /api/, / và HTTPS mẫu
│       │       └── upstream.conf      # Định tuyến traffic tới slot active
│       └── scripts/
│           ├── deploy.sh              # Kéo image, migration, smoke test, cutover, rollback
│           └── rollback.sh            # Hoàn tác slot tức thì khi có sự cố
├── src/
│   ├── backend/
│   │   ├── TicketBooking.sln          # Solution .NET 8 mở bằng Visual Studio
│   │   ├── TicketBooking.Api/         # Dự án ASP.NET Core Web API
│   │   │   ├── Data/                  # DbContext, Entities, Migrations EF Core
│   │   │   ├── HealthChecks/          # Liveness & Readiness checks cho Postgres & Redis
│   │   │   ├── Program.cs             # Khởi tạo DI, cấu hình CORS, endpoint, CLI migration
│   │   │   └── Dockerfile             # Multi-stage Dockerfile (.NET 8 Alpine)
│   │   └── TicketBooking.Tests/       # Kiểm thử tự động xUnit (HealthChecks, Migrations)
│   └── frontend/
│       ├── src/
│       │   ├── App.tsx                # Trang chủ tiếng Việt hiển thị trạng thái kết nối backend
│       │   ├── App.test.tsx           # Unit tests Vitest kiểm tra giao diện & trạng thái
│       │   ├── main.tsx
│       │   └── index.css
│       ├── Dockerfile                 # Multi-stage Dockerfile (Node 20 Alpine + Nginx)
│       ├── nginx.conf                 # Cấu hình Web Server SPA & proxy API nội bộ
│       ├── package.json
│       ├── tsconfig.json
│       └── vite.config.ts
├── docker-compose.yml                  # Khởi chạy toàn bộ hệ thống Local chỉ với 1 lệnh
├── .env.example                       # Mẫu cấu hình môi trường local
├── .gitignore                         # Loại trừ secret, file môi trường thật, build artifacts
├── .editorconfig                      # Quy chuẩn định dạng code thống nhất
└── README.md                          # Tài liệu hướng dẫn chi tiết cho thành viên mới
```

---

## 2. Phần Mềm Yêu Cầu & Phiên Bản Đã Ghim

Tất cả các công nghệ được lựa chọn đều là các phiên bản ổn định (LTS / Active Support), ghim tag rõ ràng:

| Phần mềm | Phiên bản khuyến nghị | Mục đích |
| :--- | :--- | :--- |
| **.NET SDK** | `8.0.x` (LTS) | Phát triển & chạy Backend C# ASP.NET Core Web API |
| **Node.js & npm** | Node `20.x` hoặc `24.x` LTS, npm `10+` | Phát triển & đóng gói Frontend React + TypeScript |
| **Docker & Docker Compose** | Docker `24+` / Compose v2 | Đóng gói và chạy môi trường đa dịch vụ cục bộ & staging |
| **PostgreSQL** | `16.4-alpine3.20` | Hệ quản trị cơ sở dữ liệu quan hệ |
| **Redis** | `7.4.0-alpine3.20` | Bộ nhớ đệm phân tán (Distributed In-Memory Cache) |
| **Nginx** | `1.27.2-alpine` | Web Server & Reverse Proxy điều phối Blue-Green |
| **Visual Studio** | 2022 (v17.8 trở lên) | IDE khuyến nghị cho phát triển C# .NET |

---

## 3. Khởi Động Nhanh Toàn Bộ Bằng Docker Compose (Khuyên dùng)

### Bước 1: Chuẩn bị file môi trường
Sao chép file mẫu `.env.example` thành `.env`:

```powershell
# Trên Windows PowerShell
Copy-Item .env.example .env

# Trên Linux / macOS / Bash
cp .env.example .env
```

Mở file `.env` kiểm tra các thông số mặc định (mật khẩu dev, cổng port).

### Bước 2: Khởi động toàn bộ hệ thống
Chạy lệnh duy nhất sau tại thư mục gốc của dự án:

```bash
docker compose up --build
```

**Thứ tự khởi động tự động của Docker Compose:**
1. Khởi động `db` (Postgres) và `redis`.
2. Chờ healthcheck của Postgres và Redis chuyển sang trạng thái `healthy`.
3. Khởi động service `migration`: áp dụng tự động các EF Core Migrations lên database rồi dừng an toàn (`service_completed_successfully`).
4. Khởi động `backend`: chỉ chạy khi migration đã thành công.
5. Khởi động `frontend`: kết nối đến backend.

### Bước 3: Kiểm tra các cổng truy cập (URLs)
- **Frontend Trang chủ:** [http://localhost:3000](http://localhost:3000)
- **Backend API Info:** [http://localhost:5000/](http://localhost:5000/)
- **Backend Liveness Check:** [http://localhost:5000/health/live](http://localhost:5000/health/live)
- **Backend Readiness Check:** [http://localhost:5000/health/ready](http://localhost:5000/health/ready)
- **PostgreSQL Local:** `localhost:5432` (User: `ticket_user`, DB: `ticket_booking_db`)
- **Redis Local:** `localhost:6379`

### Xem log, Dừng và Giữ lại Dữ liệu
```bash
# Xem log thời gian thực của toàn bộ hệ thống
docker compose logs -f

# Xem log riêng backend hoặc db
docker compose logs -f backend
docker compose logs -f db

# Dừng hệ thống nhưng GIỮ NGUYÊN dữ liệu trong volume
docker compose down

# Khởi động lại hệ thống
docker compose up -d

# CHỈ dùng khi muốn XOÁ SẠCH database để làm lại từ đầu:
# docker compose down -v
```

---

## 4. Hướng Dẫn Debug Bằng Visual Studio & Terminal

Khi cần debug code từng dòng, bạn có thể chạy PostgreSQL + Redis bằng Docker, sau đó chạy Backend bằng Visual Studio và Frontend bằng Terminal.

### Bước 1: Khởi động database & cache nền
```bash
docker compose up -d db redis
```

### Bước 2: Debug Backend bằng Visual Studio
1. Mở file solution: `src\backend\TicketBooking.sln` bằng Visual Studio 2022.
2. Chọn project khởi động: Chuột phải vào `TicketBooking.Api` -> chọn **Set as Startup Project**.
3. Đặt breakpoint tại các Controller hoặc HealthChecks nếu cần.
4. Nhấn **F5** (Debug) hoặc **Ctrl + F5** (Run without debugging).
5. Backend sẽ lắng nghe tại `http://localhost:5000` (được đọc từ `appsettings.Development.json`).

### Bước 3: Debug Frontend bằng Terminal
Mở một cửa sổ PowerShell / Terminal mới:

```bash
cd src/frontend
npm install
npm run dev
```
Truy cập [http://localhost:3000](http://localhost:3000). Mọi thay đổi code trên React sẽ tự động Hot Reload.

---

## 5. Lệnh Kiểm Tra Code, Lint, Typecheck & Tests

### Backend (.NET)
```bash
# 1. Khôi phục packages
dotnet restore src/backend/TicketBooking.sln

# 2. Kiểm tra định dạng code (Format / Lint)
dotnet format src/backend/TicketBooking.sln --verify-no-changes --severity warn

# 3. Tự động sửa định dạng nếu có lỗi
dotnet format src/backend/TicketBooking.sln

# 4. Biên dịch dự án (Build)
dotnet build src/backend/TicketBooking.sln --configuration Release

# 5. Chạy toàn bộ Unit & Integration Tests (13 tests)
dotnet test src/backend/TicketBooking.sln
```

### Frontend (React + TypeScript)
```bash
cd src/frontend

# 1. Cài đặt thư viện
npm install

# 2. Kiểm tra TypeScript (Typecheck)
npm run typecheck

# 3. Kiểm tra ESLint
npm run lint

# 4. Chạy Unit Tests giao diện (Vitest)
npm run test

# 5. Đóng gói sản phẩm (Build bundle)
npm run build
```

---

## 6. Quản Lý Migration Cơ Sở Dữ Liệu (EF Core)

Hệ thống sử dụng Entity Framework Core với Provider `Npgsql`.

### Các lệnh quản lý Migration:
```bash
# Tạo một migration mới (ví dụ khi thêm bảng hoặc cột)
dotnet ef migrations add <TenMigration> --project src/backend/TicketBooking.Api --startup-project src/backend/TicketBooking.Api

# Áp dụng migration lên database local (chạy tiến - Up)
dotnet ef database update --project src/backend/TicketBooking.Api --startup-project src/backend/TicketBooking.Api

# Hoàn tác migration về trạng thái trước đó (chạy lùi - Down)
# Ví dụ: hoàn tác về migration ban đầu
dotnet ef database update 20260927000000_InitialSystemConfig --project src/backend/TicketBooking.Api --startup-project src/backend/TicketBooking.Api

# Hoàn tác toàn bộ database về trạng thái rỗng (Down to 0)
dotnet ef database update 0 --project src/backend/TicketBooking.Api --startup-project src/backend/TicketBooking.Api
```

*Lưu ý:* Backend cũng hỗ trợ chạy migration trực tiếp từ file thực thi không cần cài `dotnet-ef`:
```bash
dotnet run --project src/backend/TicketBooking.Api -- --migrate-database
```

---

## 7. Cấu Hình CI / CD & Quy Tắc Bảo Vệ Nhánh `main`

### Danh sách Status Checks bắt buộc cho Quản trị viên GitHub:
Để kích hoạt quy tắc chặn merge khi CI thất bại, Quản trị viên Repo cần truy cập:
👉 **Settings** -> **Rules** -> **Rulesets** (hoặc **Branches** -> **Branch protection rules**) trên GitHub:
1. Thêm nhánh bảo vệ: `main`.
2. Tích chọn **Require status checks to pass before merging**.
3. Tìm và thêm chính xác tên các status check sau vào danh sách bắt buộc (**Required**):
   - `Backend / Format & Lint` (`backend-lint-format`)
   - `Backend / Build & Tests (Postgres + Redis)` (`backend-build-test`)
   - `Frontend / Lint & Typecheck` (`frontend-lint-typecheck`)
   - `Frontend / Tests & Build` (`frontend-build-test`)
   - `CI Gate / Toàn bộ kiểm tra vượt qua` (`ci-gate`)
4. Tích chọn **Require branches to be up to date before merging**.
5. Bật **Do not allow bypassing the above settings**.

> **LƯU Ý:** GitHub Actions workflow không thể tự động chặn pull request nếu Quản trị viên chưa bật Branch Protection Ruleset như hướng dẫn trên!

---

## 8. Chuẩn Bị Máy Chủ Staging & Triển Khai Blue-Green

### A. Danh sách GitHub Secrets cần cung cấp
Vào **Settings** -> **Secrets and variables** -> **Actions** trên GitHub, cấu hình các secrets sau cho môi trường `staging`:

| Tên Secret | Bắt buộc | Mô tả & Cách lấy giá trị |
| :--- | :--- | :--- |
| `STAGING_SSH_HOST` | Có | Địa chỉ IP Public hoặc domain của máy chủ Linux Staging |
| `STAGING_SSH_USER` | Có | Tên tài khoản Linux có quyền chạy Docker (ví dụ: `ubuntu` hoặc `deploy`) |
| `STAGING_SSH_PORT` | Không | Cổng SSH của server (mặc định là `22`) |
| `STAGING_SSH_KEY` | Có | Nội dung SSH Private Key (`id_rsa`) để CI kết nối không cần mật khẩu |
| `STAGING_SSH_KNOWN_HOSTS` | Có | Fingerprint của server. Lấy bằng lệnh: `ssh-keyscan -H <STAGING_IP>` |

### B. Chuẩn bị trên máy chủ Linux Staging
1. Cài đặt Docker Engine và Docker Compose plugin.
2. Thêm user vào group docker: `sudo usermod -aG docker $USER`.
3. Tạo thư mục làm việc: `mkdir -p ~/app/deploy/staging`.
4. Copy file `deploy/staging/env.staging.example` thành `~/app/deploy/staging/.env.staging` và điền mật khẩu mạnh.
5. Cổng PostgreSQL (`5432`) và Redis (`6379`) hoàn toàn được cô lập trong mạng `ticket_staging_network`, KHÔNG public ra internet. Chỉ duy nhất cổng `80` (và `443` khi có HTTPS) của Nginx được mở ra bên ngoài.

### C. Cơ chế triển khai Blue-Green Zero-Downtime (`deploy.sh`)
- Khi merge vào `main` và CI vượt qua, workflow CD tự động kích hoạt `deploy-staging.yml`.
- CD đóng gói Docker image với thẻ Commit SHA và đẩy lên GitHub Container Registry (`ghcr.io`).
- CD gọi `deploy.sh <COMMIT_SHA>` trên server:
  1. Xác định slot rảnh (`blue` hoặc `green`).
  2. Kéo image mới về server.
  3. Khởi chạy Database Migration độc lập. Nếu migration thất bại, dừng ngay lập tức, slot cũ vẫn chạy bình thường.
  4. Khởi động code mới trên slot rảnh bên cạnh slot cũ.
  5. Chạy Smoke Test kiểm tra `/health/ready` trên slot mới cho đến khi HTTP 200.
  6. Hoán đổi định tuyến Nginx sang slot mới và reload Nginx (thời gian chuyển đổi 0 giây, không rớt request).
  7. Hậu kiểm tra traffic qua cổng chính. Nếu có lỗi, tự động kích hoạt rollback.
  8. Dừng slot cũ sau 10 giây chờ kết nối cũ hoàn tất.

### D. Quy trình Hoàn tác Khẩn cấp (Rollback)
Nếu phát hiện lỗi nghiêm trọng trên phiên bản vừa chuyển đổi, chạy lệnh sau trực tiếp trên server:

```bash
cd ~/app/deploy/staging
./scripts/rollback.sh
```

> **QUY TẮC SỐNG CÒN VỀ DATABASE KHI ROLLBACK:**
> Lệnh `rollback.sh` chỉ hoàn tác các container ứng dụng về phiên bản trước. Tuyệt đối **KHÔNG** tự động chạy migration lùi (`Down`) trên Database vì sẽ gây mất dữ liệu của người dùng thật. Mọi thiết kế migration trong dự án phải tuân thủ nguyên tắc mở rộng tương thích ngược (Expand and Contract pattern).

---

## 9. Xử Lý Các Lỗi Thường Gặp (Troubleshooting)

### 1. Lỗi Trùng Cổng (Port already in use)
- **Triệu chứng:** `bind: address already in use` (cổng 3000, 5000, 5432 hoặc 6379).
- **Khắc phục:**
  - Kiểm tra tiến trình đang chiếm cổng:
    ```powershell
    netstat -ano | findstr :5000
    ```
  - Hoặc chỉnh sửa số cổng khác trong file `.env` (ví dụ `BACKEND_PORT=5005`, `FRONTEND_PORT=3005`).

### 2. Docker Daemon chưa khởi động
- **Triệu chứng:** `error during connect: This error may indicate that the docker daemon is not running`.
- **Khắc phục:** Mở ứng dụng **Docker Desktop** trên Windows và chờ biểu tượng góc dưới chuyển sang màu xanh lá cây trước khi chạy lại lệnh `docker compose up`.

### 3. Sai thông tin kết nối Database hoặc Dependency chưa sẵn sàng
- **Triệu chứng:** Trang chủ hiển thị khung màu đỏ kèm thông báo `Chưa sẵn sàng / Mất kết nối` và mã HTTP `503`.
- **Khắc phục:**
  - Xem chi tiết lỗi: endpoint `/health/ready` sẽ liệt kê rõ thành phần nào bị lỗi (`postgresql` hay `redis`).
  - Kiểm tra trạng thái container: `docker compose ps`.
  - Kiểm tra log PostgreSQL: `docker compose logs db`.
  - Đảm bảo mật khẩu và tên user trong `.env` khớp với cấu hình kết nối.

### 4. Lỗi quyền thực thi Script trên PowerShell (ExecutionPolicy)
- **Triệu chứng:** `cannot be loaded because running scripts is disabled on this system`.
- **Khắc phục:** Chạy lệnh `npm.cmd` thay vì `npm`, hoặc chạy PowerShell dưới quyền Administrator và gõ:
  ```powershell
  Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
  ```
