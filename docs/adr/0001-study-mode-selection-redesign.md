# ADR 0001: Redesign trang chọn chế độ học

## Status

Proposed

## Context

Trang `/Study/{setId}` hiện tại hiển thị 5 ô chế độ học (Flashcard, Flashcard đã sao, Trắc nghiệm, Nghe chép, Ghép đôi). Trong đó Trắc nghiệm và Ghép đôi chưa khả dụng. Khi user chọn mode bị lọc hết thẻ, hệ thống redirect về lại trang Index với `TempData["Message"]`, gây rối.

Ngườ dùng muốn một flow rõ ràng hơn, UI phù hợp với trang học tiếng Anh, và áp dụng taste skills.

## Decision

1. Coi nút **Học** trên danh sách bộ thẻ là entry chính vào `/Study/{setId}`.
2. Trên `/Study/{setId}` chỉ hiển thị 1 CTA chính là mode được gợi ý; các mode khác là chip phụ.
3. "Chỉ đã sao" / "Chỉ chưa thuộc" là filter, không phải mode riêng.
4. Hiển thị context bộ thẻ: tổng từ, % tiến độ, số lần học 7 ngày, thờ gian dự kiến.
5. Xử lý empty-state/filter conflict inline trên trang.
6. Modes chưa ra mắt chuyển xuống roadmap strip ở footer.
7. Áp dụng phong cách **Editorial Luxury** (warm cream background, serif heading, generous whitespace) và layout **Editorial Split** (trái context + CTA, phải mode cards).

## Consequences

- User thấy rõ mình đang học gì và bắt đầu nhanh hơn.
- Giảm số lựa chọn đồng thờ, giảm tỷ lệ rờ bỏ.
- Code backend cần thêm tính toán stats và recommended mode.
- CSS mới chuyên biệt, không ảnh hưởng các trang khác.
- Có thể cần chỉnh sửa lại nếu sau này thêm mode mới (roadmap strip dễ mở rộng).

## Alternatives considered

- **Giữ 5 card lớn**: bị các mode disabled chiếm vị trí, rối.
- **Bento grid**: đẹp nhưng không nhấn mạnh CTA chính bằng Editorial Split.
- **Dark glassmorphism**: không phù hợp cảm giác học tập tập trung.
