# Issue tracker: Local Markdown

Issues và spec của repo được lưu dưới dạng Markdown trong `.scratch/`.

## Quy ước

- Mỗi feature có một thư mục: `.scratch/<feature-slug>/`
- Spec nằm tại `.scratch/<feature-slug>/spec.md`
- Mỗi ticket là một file riêng tại `.scratch/<feature-slug>/issues/<NN>-<slug>.md`, đánh số từ `01`
- Trạng thái triage nằm trên dòng `Status:` gần đầu ticket
- Bình luận và lịch sử trao đổi được nối thêm dưới heading `## Comments`

## Khi skill yêu cầu publish vào issue tracker

Tạo file mới dưới `.scratch/<feature-slug>/`, đồng thời tạo thư mục nếu chưa tồn tại.

## Khi skill yêu cầu đọc ticket

Đọc file theo đường dẫn hoặc số ticket được cung cấp.

## Wayfinding

- Map: `.scratch/<effort>/map.md`
- Ticket con: `.scratch/<effort>/issues/NN-<slug>.md`
- Dependency: dòng `Blocked by: NN, NN`
- Ticket chỉ được thực hiện khi tất cả blocker có trạng thái `resolved`
- Claim: đổi trạng thái thành `claimed` trước khi làm
- Resolve: thêm kết quả dưới `## Answer`, đổi trạng thái thành `resolved`, rồi cập nhật map
