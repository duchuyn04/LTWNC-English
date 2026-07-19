# 12 — Chuyển Nhà cung cấp AI dự phòng và ghi số liệu vận hành

**Nội dung cần xây dựng:** Tạo một đường chạy AI có khả năng chuyển sang danh sách dự phòng đã cấu hình, ghi số liệu vận hành an toàn và cung cấp trạng thái sức khỏe cho Admin.

**Bị chặn bởi:** 11 — Đưa Nhà cung cấp AI vào Area với vòng đời an toàn.

**Trạng thái:** `ready-for-agent`

- [ ] Bộ định tuyến chỉ thử nhà cung cấp đang bật, đã kiểm tra thành công và theo đúng thứ tự Admin cấu hình.
- [ ] Có giới hạn thời gian tổng thể và không thử vô hạn; yêu cầu đang chạy được phép hoàn tất khi nhà cung cấp bị vô hiệu hóa.
- [ ] Nếu không còn nhà cung cấp phù hợp, người học nhận thông báo chung để thử lại sau, không thấy tên hoặc lỗi kỹ thuật.
- [ ] Kết quả đã hoàn thành không tự động chạy lại khi cấu hình thay đổi.
- [ ] Mỗi lần gọi ghi số liệu an toàn gồm thời gian, nhà cung cấp/mô hình, kết quả, độ trễ và lần chuyển dự phòng; không lưu câu lệnh hay nội dung hội thoại.
- [ ] Ba lần kiểm tra thất bại liên tiếp đánh dấu không ổn định; lần thành công đặt lại bộ đếm.
- [ ] Tỷ lệ lỗi dùng cửa sổ 5 phút, ngưỡng trên 10% và tối thiểu 20 yêu cầu; dưới mẫu trả “chưa đủ dữ liệu”.
- [ ] Ngưỡng nằm trong cấu hình hệ thống, không có biểu mẫu Admin thay đổi ở phiên bản 1.
- [ ] Kiểm thử adapter giả lập bao phủ thứ tự, lỗi, thời gian chờ, chuyển dự phòng, vô hiệu hóa giữa lúc chạy và không rò dữ liệu nhạy cảm.

