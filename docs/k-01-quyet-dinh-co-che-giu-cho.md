# [ADR] BÁO CÁO QUYẾT ĐỊNH KIẾN TRÚC & ĐO LƯỜNG HIỆU NĂNG (k-01)
* **Mã task:** SCRUM-10 (Epic: E-04 Chọn ghế và giữ chỗ có thời hạn)
* **Người thực hiện:** Đinh Hải Anh (dinhhaianh2007pt)
* **Dự án:** Event Ticketing System (Bán vé sự kiện có sơ đồ ghế)
* **Mục tiêu:** Quyết định cơ chế giữ chỗ tạm thời (10 phút), chống bán trùng ghế tuyệt đối và đo lường số liệu giữa 2 phương án.

---

## 1. MÔ TẢ HAI PHƯƠNG ÁN KHẢO SÁT

### Phương án A: PostgreSQL Optimistic Locking (Trực tiếp trên CSDL)
* **Cơ chế:** Thêm các trường `status` ('AVAILABLE', 'HELD', 'SOLD'), `held_by_user_id`, `held_until` vào bảng `Seats`.
* **Câu lệnh giữ chỗ (Atomic Update):**
  ```sql
  UPDATE seats 
  SET status = 'HELD', held_by_user_id = @userId, held_until = NOW() + INTERVAL '10 minutes'
  WHERE id = @seatId AND (status = 'AVAILABLE' OR held_until < NOW());
