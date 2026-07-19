# LTWNC English

LTWNC English là hệ thống học từ vựng, quản lý nội dung học, hoạt động học tập, thành tích và các Nhiệm vụ tiếng Anh có hỗ trợ AI cho người dùng đã đăng ký.

## Quản trị

**Quản trị viên**:
Người dùng đáng tin cậy chịu trách nhiệm cho mọi lĩnh vực quản trị trong hệ thống. Ở phiên bản 1, mọi Quản trị viên có cùng quyền hạn; không có các nhóm quyền quản trị nhỏ hơn.
_Tránh dùng_: Quản trị viên cấp cao, Người kiểm duyệt, Người vận hành

**Khóa tài khoản**:
Thao tác có thể hoàn tác của Quản trị viên nhằm ngăn người dùng đăng nhập. Quản trị viên có thể khóa, mở khóa và thu hồi mọi phiên đăng nhập của người dùng, nhưng không thể tự khóa mình, khóa Quản trị viên đang hoạt động cuối cùng hoặc tài khoản Quản trị viên khởi tạo được cấu hình. Ở phiên bản 1, Quản trị viên không được xem hay đặt mật khẩu, xóa vĩnh viễn tài khoản hoặc thay đổi thành viên của vai trò Admin.
_Tránh dùng_: Cấm vĩnh viễn, Xóa người dùng

**Nhà cung cấp AI**:
Kết nối và cấu hình do Quản trị viên quản lý, được Nhiệm vụ tiếng Anh và các tính năng học tập có AI sử dụng. Quản trị viên có thể tạo, cấu hình, kiểm tra, chọn mô hình, đặt nhà cung cấp chính, sắp xếp danh sách dự phòng đã bật, thay khóa bí mật và vô hiệu hóa nhà cung cấp. Khóa bí mật chỉ được ghi mới; nhà cung cấp đã có lịch sử sử dụng chỉ được vô hiệu hóa, không xóa vĩnh viễn. Hệ thống chỉ tự chuyển sang nhà cung cấp dự phòng đã được Quản trị viên cấu hình và kiểm tra. Người học không thấy danh tính, khóa bí mật, mô hình, trạng thái, quá trình chuyển dự phòng hay chi tiết vận hành của nhà cung cấp.
_Tránh dùng_: Cài đặt AI của người dùng, Nhà cung cấp của người học

**Cách ly nội dung**:
Thao tác kiểm duyệt có thể hoàn tác, loại bộ flashcard của người dùng khỏi tìm kiếm, chia sẻ và học công khai mà không sửa nội dung hay xóa lịch sử. Tác giả vẫn có thể xem và chỉnh sửa nhưng không thể tự xuất bản lại; chỉ Quản trị viên có thể khôi phục. Mỗi lần cách ly hoặc khôi phục đều phải có lý do và bản ghi kiểm toán quản trị.
_Tránh dùng_: Quản trị viên sửa nội dung, Xóa cứng, Cấm nội dung

**Báo cáo nội dung**:
Vụ việc kiểm duyệt do người học đã đăng nhập tạo cho một bộ flashcard công khai. Mỗi người chỉ có một báo cáo đang mở cho cùng một bộ và không thể báo cáo bộ của chính mình. Quản trị viên xử lý bằng cách bác bỏ hoặc cách ly nội dung, bắt buộc ghi lý do; phiên bản 1 chưa có quy trình kháng nghị tự động.
_Tránh dùng_: Yêu cầu hỗ trợ

**Truy cập hồ sơ học tập**:
Quyền chỉ đọc của Quản trị viên đối với phiên học, câu trả lời, điểm và tiến độ của người học nhằm hỗ trợ hoặc điều tra lỗi. Việc mở dữ liệu chi tiết của một người học tạo bản ghi kiểm toán; Quản trị viên không thể sửa điểm, tiến độ hoặc lịch sử phiên học.
_Tránh dùng_: Sửa tiến độ, Ghi đè điểm

**Truy cập hội thoại Nhiệm vụ tiếng Anh**:
Quyền truy cập ngoại lệ, bắt buộc có lý do, cho phép Quản trị viên xem hội thoại của một nhiệm vụ bị báo cáo hoặc một vụ hỗ trợ cụ thể. Mặc định chỉ hiển thị số liệu tổng hợp; việc truy cập được kiểm toán và không bao gồm câu lệnh hệ thống, khóa bí mật hay chi tiết nội bộ của Nhà cung cấp AI. Nội dung hội thoại chi tiết được lưu 90 ngày; trường hợp gắn với báo cáo hoặc vụ hỗ trợ được giữ đến khi vụ việc kết thúc và tối đa 12 tháng. Kết quả học tập và số liệu tổng hợp được giữ lại sau khi nội dung chi tiết hết hạn.
_Tránh dùng_: Trình duyệt hội thoại, Truy cập hội thoại không giới hạn

**Đồng bộ lại thành tích**:
Phép tính lại do Quản trị viên kích hoạt nhằm đưa kết quả thành tích về đúng trạng thái được suy ra từ danh mục thành tích có quản lý phiên bản và hoạt động của người học. Đây không phải việc cấp, thu hồi thủ công hoặc sửa định nghĩa thành tích trên trang quản trị.
_Tránh dùng_: Cấp thành tích thủ công, Ghi đè thành tích

**Bản ghi kiểm toán quản trị**:
Bản ghi chỉ ghi thêm về thao tác quản trị hoặc lần truy cập dữ liệu nhạy cảm, gồm người thực hiện, hành động, đối tượng, thời gian, kết quả, lý do và siêu dữ liệu an toàn. Bản ghi không chứa mật khẩu, khóa bí mật hoặc toàn bộ hội thoại; có thể tìm kiếm nhưng không thể sửa hay xóa thủ công và được lưu trong 12 tháng.
_Tránh dùng_: Nhật ký ứng dụng, Ghi chú hoạt động có thể sửa

**Ảnh chụp vận hành**:
Góc nhìn gần thời gian thực của Quản trị viên về trạng thái Nhà cung cấp AI, tỷ lệ lỗi AI, số Báo cáo nội dung đang chờ, cảnh báo và các chỉ số tổng quan. Các khối Razor được làm mới bằng yêu cầu AJAX có xác thực mỗi 30 giây khi thẻ trình duyệt đang hoạt động.
_Tránh dùng_: Theo dõi trực tiếp người dùng, Hội thoại thời gian thực

**Phiên quản trị đặc quyền**:
Phiên đã đăng nhập của người có vai trò Admin và đã bật xác thực hai bước. Thay đổi nhạy cảm đối với Nhà cung cấp AI yêu cầu xác nhận lại danh tính nếu lần xác thực gần nhất đã quá 15 phút.
_Tránh dùng_: Phiên người dùng thông thường, Phiên quản trị vĩnh viễn
