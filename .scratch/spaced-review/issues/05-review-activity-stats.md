# 05 — Ngày có hoạt động ôn và tổng số lượt hoàn thành

**What to build:** Người học thấy Ôn tập đến hạn được tính đúng vào ngày hoạt động và thống kê hồ sơ, trong khi Tiến độ học chung và thành tích cũ vẫn độc lập.

**Blocked by:** 02 — Lượt ôn nhiều thẻ đến hạn và tiếp tục.

**Status:** resolved

- [x] Đánh giá ít nhất một thẻ tạo Ngày có hoạt động ôn theo múi giờ Việt Nam dù lượt chưa hoàn thành.
- [x] Chỉ Lượt ôn hoàn thành được cộng vào tổng số lượt học hoàn thành; kết thúc sớm không được cộng.
- [x] Nhiều lượt trong cùng ngày chỉ tạo một ngày streak.
- [x] Hồ sơ hiển thị thống kê Ôn tập đến hạn phù hợp với dữ liệu ReviewSession và ReviewSessionItem.
- [x] Ôn tập đến hạn không làm thay đổi UserProgress, số thẻ đã thuộc hoặc các thành tích Flashcard/Dictation hiện có.
- [x] Có test boundary múi giờ, session completion, early end và hồi quy thống kê cũ.

## Answer

- Profile statistics now derive review activity from rated `ReviewSessionItem` records and convert timestamps to the Vietnam calendar. Unfinished sessions contribute to activity days and streaks as soon as one card is rated; duplicate sessions on the same day collapse to one activity day.
- Completed review rounds are added to the profile's total completed-session count and exposed as a separate breakdown, while early-ended rounds are excluded from both completed counts. Review activity is also represented in the existing profile timeline.
- Added focused service tests covering the UTC/Vietnam boundary, completed versus early-ended sessions, same-day deduplication, and regression counts for `UserProgress`, learned cards, achievements, and legacy study sessions.
