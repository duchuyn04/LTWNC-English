# Nơi quản lý công việc: Markdown cục bộ

Đặc tả và hạng mục công việc của project được lưu thành file Markdown trong `.scratch/`. Không tự động đăng dữ liệu lên dịch vụ bên ngoài.

## Quy ước

- Mỗi tính năng có một thư mục: `.scratch/<ten-tinh-nang>/`.
- Đặc tả của tính năng nằm tại `.scratch/<ten-tinh-nang>/spec.md`.
- Mỗi hạng mục triển khai là một file riêng tại `.scratch/<ten-tinh-nang>/issues/<NN>-<ten-ngan>.md`, đánh số từ `01` theo thứ tự phụ thuộc.
- Trạng thái được ghi bằng dòng `Status:` gần đầu mỗi file; xem `triage-labels.md` để biết các giá trị chuẩn.
- Ý kiến và lịch sử trao đổi được nối thêm ở cuối file dưới tiêu đề `## Bình luận`.

## Khi skill yêu cầu “xuất bản vào nơi quản lý công việc”

Tạo hoặc cập nhật file phù hợp trong `.scratch/<ten-tinh-nang>/`.

## Khi skill yêu cầu đọc một hạng mục

Đọc toàn bộ file theo đường dẫn hoặc số thứ tự mà người dùng cung cấp.

## Quy ước quan hệ phụ thuộc

- Mỗi hạng mục ghi rõ `Bị chặn bởi:` và liệt kê số/tên các hạng mục phải hoàn thành trước.
- Hạng mục không có phụ thuộc có thể bắt đầu ngay.
- Chỉ triển khai một hạng mục khi mọi hạng mục chặn nó đã hoàn thành.
