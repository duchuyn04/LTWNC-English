# Đặc tả: Khu vực quản trị LTWNC English

**Trạng thái:** `ready-for-agent`

## Vấn đề

LTWNC English đã có chức năng học tập, bộ flashcard, thành tích, Nhiệm vụ tiếng Anh và cấu hình Nhà cung cấp AI, nhưng chưa có một khu vực quản trị hoàn chỉnh và tách biệt. Chức năng Nhà cung cấp AI hiện dùng đường dẫn mang tên Admin nhưng chưa nằm trong ASP.NET Core Area; project cũng chưa có cơ chế kiểm duyệt nội dung, nhật ký quản trị tổng quát, theo dõi vận hành, bảo vệ dữ liệu nhạy cảm hoặc luồng xác thực mạnh dành riêng cho Admin.

Admin cần một nơi duy nhất để theo dõi tình trạng hệ thống, hỗ trợ người học, xử lý nội dung vi phạm và vận hành AI mà không can thiệp trái phép vào dữ liệu học tập. Người học không được biết hoặc điều khiển chi tiết Nhà cung cấp AI. Các thao tác nhạy cảm phải có lý do, chống ghi đè đồng thời và để lại dấu vết kiểm toán.

## Giải pháp

Xây dựng Area `Admin` riêng trong ứng dụng ASP.NET Core MVC, dùng phương án giao diện A của prototype làm bố cục chính và tái sử dụng hệ thống thiết kế hiện có. Khu vực gồm trang tổng quan, người dùng, nội dung, phiên học, Nhiệm vụ tiếng Anh, thành tích, Nhà cung cấp AI và nhật ký quản trị.

Mọi Admin dùng chung một vai trò `Admin` và có cùng quyền. Quyền truy cập yêu cầu xác thực hai bước; thao tác AI đặc biệt nhạy cảm yêu cầu xác nhận lại danh tính. Dữ liệu nhạy cảm chỉ được mở theo lý do cụ thể và được kiểm toán. Nội dung vi phạm được cách ly thay vì xóa, tài khoản được khóa có thể khôi phục, Nhà cung cấp AI có lịch sử được vô hiệu hóa thay vì xóa.

Trang tổng quan dùng Razor Views và cập nhật các khối vận hành gần thời gian thực qua AJAX mỗi 30 giây theo ADR 0001. Dữ liệu lớn được lọc, sắp xếp và phân trang ở máy chủ. Giao diện bằng tiếng Việt, đáp ứng từ 360 px, hỗ trợ bàn phím và mức tương đương WCAG 2.1 AA.

## Câu chuyện người dùng

1. Là Admin, tôi muốn đăng nhập được chuyển thẳng đến khu vực quản trị để không đi qua trang dành cho khách hoặc người học.
2. Là người học đã đăng nhập, tôi muốn được chuyển về khu vực học khi truy cập trang đăng nhập, đăng ký hoặc trang chủ công khai để không thấy các màn hình không còn phù hợp.
3. Là khách chưa đăng nhập, tôi muốn được chuyển đến trang đăng nhập khi mở khu vực quản trị để có thể xác thực.
4. Là người đã đăng nhập nhưng không có vai trò Admin, tôi muốn nhận trang từ chối truy cập rõ ràng để biết mình không có quyền.
5. Là Admin, tôi muốn phải bật xác thực hai bước trước khi vào khu vực quản trị để giảm nguy cơ chiếm tài khoản.
6. Là Admin, tôi muốn được yêu cầu xác nhận lại danh tính khi phiên xác thực đã quá 15 phút trước khi thay khóa AI hoặc Nhà cung cấp AI chính.
7. Là Admin, tôi muốn sử dụng menu riêng gồm Tổng quan, Người dùng, Nội dung, Phiên học, Nhiệm vụ tiếng Anh, Thành tích, Nhà cung cấp AI và Nhật ký để điều hướng nhất quán.
8. Là Admin, tôi muốn có liên kết mở giao diện người học trong thẻ mới để kiểm tra trải nghiệm mà không rời khu vực quản trị.
9. Là Admin, tôi muốn xem số người dùng hoạt động để đánh giá mức sử dụng hệ thống.
10. Là Admin, tôi muốn xem số đăng ký mới, phiên học, tỷ lệ hoàn thành, Nhiệm vụ tiếng Anh và tỷ lệ lỗi AI để nắm tình trạng vận hành.
11. Là Admin, tôi muốn lọc chỉ số theo 7, 30 hoặc 90 ngày, mặc định 30 ngày, và so sánh với kỳ liền trước để nhận biết xu hướng.
12. Là Admin, tôi muốn các chỉ số, trạng thái AI, số báo cáo chờ và cảnh báo tự cập nhật mỗi 30 giây mà không tải lại trang.
13. Là Admin, tôi muốn việc tự cập nhật tạm dừng khi thẻ trình duyệt bị ẩn và làm mới ngay khi quay lại để giảm yêu cầu thừa.
14. Là Admin, tôi muốn thấy cảnh báo khi Nhà cung cấp AI chính không hoạt động, tỷ lệ lỗi AI tăng, Báo cáo nội dung chờ quá 24 giờ hoặc đồng bộ thành tích thất bại.
15. Là Admin, tôi muốn cảnh báo tự hết khi nguyên nhân đã được xử lý để danh sách luôn phản ánh trạng thái hiện tại.
16. Là Admin, tôi muốn tìm kiếm toàn cục theo người dùng, email, mã định danh, bộ flashcard và mã Nhiệm vụ tiếng Anh để tìm nhanh đối tượng cần hỗ trợ.
17. Là Admin, tôi muốn nội dung hội thoại không được lập chỉ mục trong tìm kiếm toàn cục để bảo vệ quyền riêng tư.
18. Là Admin, tôi muốn tìm kiếm, lọc, sắp xếp và phân trang danh sách người dùng để làm việc với dữ liệu lớn.
19. Là Admin, tôi muốn xem hồ sơ và trạng thái tài khoản của người dùng để hỗ trợ họ.
20. Là Admin, tôi muốn khóa hoặc mở khóa tài khoản với lý do bắt buộc để ngăn truy cập có kiểm soát.
21. Là Admin, tôi muốn thu hồi toàn bộ phiên đăng nhập của một người dùng để xử lý sự cố bảo mật.
22. Là Admin, tôi muốn hệ thống ngăn tôi tự khóa mình, khóa Admin đang hoạt động cuối cùng hoặc khóa Admin khởi tạo để không tự khóa khu vực quản trị.
23. Là người dùng bị khóa, tôi muốn mọi phiên đăng nhập bị thu hồi ngay và nhận thông báo chung cùng cách liên hệ hỗ trợ khi thử đăng nhập.
24. Là người dùng được mở khóa, tôi muốn đăng nhập lại và giữ nguyên tiến độ, thành tích cùng nội dung của mình.
25. Là Admin, tôi muốn xem số liệu tổng hợp về học tập để điều tra xu hướng và lỗi.
26. Là Admin, tôi muốn mở chi tiết phiên học của một người dùng theo chế độ chỉ đọc và có kiểm toán để hỗ trợ một vụ việc cụ thể.
27. Là người học, tôi muốn Admin không thể sửa điểm, tiến độ hoặc xóa lịch sử phiên học của tôi để dữ liệu học tập giữ nguyên tính toàn vẹn.
28. Là Admin, tôi muốn xem danh sách bộ flashcard cùng trạng thái công khai/cách ly để quản lý nội dung.
29. Là chủ sở hữu bộ riêng tư, tôi muốn danh sách quản trị mặc định chỉ lộ thông tin khái quát để bảo vệ nội dung của mình.
30. Là Admin, tôi muốn phải nhập lý do trước khi mở nội dung chi tiết của bộ riêng tư để lần truy cập được kiểm toán.
31. Là người học đã đăng nhập, tôi muốn báo cáo một bộ flashcard công khai bằng loại lý do và mô tả tùy chọn để thông báo nội dung vi phạm.
32. Là người học, tôi muốn hệ thống ngăn gửi nhiều báo cáo đang mở cho cùng một bộ hoặc tự báo cáo bộ của mình để tránh lạm dụng.
33. Là Admin, tôi muốn xem hàng đợi Báo cáo nội dung và chi tiết cần thiết để xử lý.
34. Là Admin, tôi muốn bác bỏ báo cáo hoặc cách ly bộ flashcard với lý do bắt buộc để kết thúc vụ việc minh bạch.
35. Là Admin, tôi muốn khôi phục bộ đã cách ly với lý do bắt buộc khi nội dung đủ điều kiện xuất hiện lại.
36. Là tác giả, tôi muốn thấy trạng thái cách ly, lý do công khai và thời điểm xử lý để hiểu điều gì đã xảy ra.
37. Là tác giả, tôi muốn vẫn xem và chỉnh sửa được bộ bị cách ly nhưng không thể tự xuất bản lại để quyết định kiểm duyệt không bị vượt qua.
38. Là người học, tôi muốn bộ bị cách ly biến mất khỏi tìm kiếm, chia sẻ và học công khai nhưng lịch sử học cũ vẫn được giữ.
39. Là Admin, tôi muốn mặc định chỉ xem thống kê tổng hợp của Nhiệm vụ tiếng Anh để không đọc hội thoại cá nhân không cần thiết.
40. Là Admin, tôi muốn chỉ mở hội thoại khi có nhiệm vụ bị báo cáo hoặc vụ hỗ trợ cụ thể, bắt buộc nhập lý do và tạo bản ghi kiểm toán.
41. Là người học, tôi muốn hội thoại Admin xem không chứa câu lệnh hệ thống, khóa bí mật hoặc chi tiết nội bộ của Nhà cung cấp AI.
42. Là người học, tôi muốn nội dung hội thoại chi tiết tự xóa sau 90 ngày nhưng kết quả học tập và số liệu tổng hợp vẫn được giữ.
43. Là Admin, tôi muốn hội thoại gắn với vụ điều tra được giữ đến khi vụ việc kết thúc và tối đa 12 tháng để có đủ bằng chứng xử lý.
44. Là Admin, tôi muốn xem danh mục thành tích và người đã nhận để kiểm tra trạng thái hệ thống.
45. Là Admin, tôi muốn chạy Đồng bộ lại thành tích khi dữ liệu bị lệch để khôi phục kết quả được suy ra từ danh mục trong source code.
46. Là người học, tôi muốn Admin không thể cấp, thu hồi hoặc sửa định nghĩa thành tích thủ công để thành tích phản ánh đúng hoạt động học.
47. Là Admin, tôi muốn tạo, sửa, kiểm tra kết nối, khám phá mô hình, chọn mô hình và sắp xếp Nhà cung cấp AI để vận hành các tính năng học tập.
48. Là Admin, tôi muốn đặt một Nhà cung cấp AI chính và danh sách dự phòng đã bật theo thứ tự để hệ thống chuyển hướng có kiểm soát khi có lỗi.
49. Là Admin, tôi muốn khóa bí mật chỉ được nhập hoặc thay mới và không bao giờ hiển thị lại để tránh lộ thông tin xác thực.
50. Là Admin, tôi muốn vô hiệu hóa Nhà cung cấp AI đã có lịch sử thay vì xóa để giữ khả năng truy vết.
51. Là Admin, tôi muốn Nhà cung cấp AI bị vô hiệu hóa ngừng nhận yêu cầu mới nhưng cho phép yêu cầu đang xử lý hoàn tất trong giới hạn thời gian chờ.
52. Là người học, tôi muốn yêu cầu mới tự chuyển sang nhà cung cấp dự phòng đã cấu hình hoặc nhận thông báo thử lại sau mà không thấy chi tiết kỹ thuật.
53. Là Admin, tôi muốn hệ thống ghi lại lỗi AI và lần chuyển dự phòng để điều tra vận hành.
54. Là Admin, tôi muốn Nhà cung cấp AI bị đánh dấu không ổn định sau ba lần kiểm tra thất bại liên tiếp để có tín hiệu rõ ràng.
55. Là Admin, tôi muốn cảnh báo tỷ lệ lỗi khi lỗi vượt 10% trong 5 phút và có ít nhất 20 yêu cầu để tránh báo động từ mẫu quá nhỏ.
56. Là Admin, tôi muốn thấy “chưa đủ dữ liệu” khi chưa đạt kích thước mẫu thay vì tỷ lệ 0% gây hiểu nhầm.
57. Là Admin, tôi muốn mọi thay đổi dữ liệu quản trị và lần truy cập dữ liệu nhạy cảm tạo Bản ghi kiểm toán quản trị để truy vết.
58. Là Admin, tôi muốn tìm kiếm và lọc bản ghi kiểm toán nhưng không thể sửa hay xóa thủ công để giữ tính toàn vẹn.
59. Là người dùng, tôi muốn mật khẩu, khóa bí mật và toàn bộ hội thoại không bao giờ được ghi vào nhật ký kiểm toán để giảm rủi ro rò rỉ.
60. Là người vận hành hệ thống, tôi muốn bản ghi kiểm toán tự xóa sau 12 tháng để tuân thủ thời hạn lưu đã chốt.
61. Là Admin, tôi muốn xuất CSV các chỉ số tổng hợp và danh sách kiểm toán đã lọc để phục vụ báo cáo an toàn.
62. Là người dùng, tôi muốn Admin không thể xuất hàng loạt hồ sơ, lịch sử học hoặc hội thoại để giảm nguy cơ rò rỉ dữ liệu cá nhân.
63. Là Admin, tôi muốn thao tác xuất dữ liệu cũng được kiểm toán để biết dữ liệu đã rời giao diện khi nào.
64. Là Admin, tôi muốn thao tác nhạy cảm có hộp xác nhận, lý do bắt buộc và phát hiện xung đột cập nhật để tránh ghi đè thay đổi của Admin khác.
65. Là Admin, tôi muốn danh sách dữ liệu mặc định 25 dòng và cho chọn tối đa 100 dòng mỗi trang để giao diện phản hồi ổn định.
66. Là Admin, tôi muốn giao diện dùng tiếng Việt và hiển thị thời gian theo múi giờ Việt Nam để đọc nhất quán.
67. Là người sử dụng bàn phím hoặc công nghệ hỗ trợ, tôi muốn mọi chức năng Admin có nhãn, trạng thái lấy nét, độ tương phản và dữ liệu thay thế biểu đồ phù hợp.
68. Là Admin dùng điện thoại từ 360 px, tôi muốn có đầy đủ thao tác quản trị với menu thu gọn và bảng thích ứng để xử lý tình huống khẩn cấp.
69. Là người nhạy cảm với chuyển động, tôi muốn giao diện tôn trọng tùy chọn giảm chuyển động của thiết bị.
70. Là người dùng, tôi muốn Admin không thể đăng nhập dưới danh nghĩa của tôi để mọi hoạt động luôn được ghi nhận đúng chủ thể.

## Quyết định triển khai

### Kiến trúc và định tuyến

- Dùng ASP.NET Core MVC Area `Admin`, Razor Views và JavaScript tăng cường; không tách ứng dụng một trang hoặc API riêng.
- Đăng ký định tuyến Area trước tuyến mặc định. Trang gốc của Area là bảng tổng quan.
- Chuyển chức năng Nhà cung cấp AI hiện có vào Area và dùng layout Admin; không duy trì hai bộ đường dẫn/chức năng song song.
- Phương án A của prototype là cơ sở bố cục. Giao diện sản xuất loại bỏ bộ chuyển biến thể và dữ liệu giả, nhưng giữ hệ thống thiết kế của project.
- Các khu vực chức năng tách theo module nghiệp vụ, còn phân quyền, kiểm toán, phân trang, thời gian và xác nhận thao tác dùng thành phần chung.

### Xác thực và phân quyền

- Chỉ có một vai trò `Admin`; mọi Admin có cùng quyền. Không có ma trận quyền hoặc thao tác cấp/thu hồi vai trò trong giao diện.
- Vai trò Admin khởi tạo tiếp tục được cấp ngoài dashboard bằng cấu hình/deployment.
- Áp dụng chính sách Admin cho toàn Area thay vì dựa vào từng controller tự nhớ gắn thuộc tính.
- Admin phải bật xác thực hai bước. Nếu đã đăng nhập nhưng chưa hoàn tất thiết lập, chuyển đến luồng thiết lập/xác minh trước khi vào Area.
- Ghi nhận thời điểm xác thực gần nhất trong phiên; thao tác thay khóa AI hoặc đổi Nhà cung cấp AI chính yêu cầu xác nhận lại nếu quá 15 phút.
- Khách được chuyển đến đăng nhập; người đã đăng nhập nhưng không phải Admin nhận mã 403 và trang từ chối truy cập, không bị chuyển nhầm về trang đăng nhập.
- Sau đăng nhập, Admin về `/Admin`, người học về `/Set`. Trang đăng nhập, đăng ký và trang chủ công khai cũng điều hướng người đã đăng nhập theo vai trò.
- Không hỗ trợ đăng nhập thay người dùng.

### Dữ liệu và thay đổi lược đồ

- Thêm dữ liệu cách ly cho bộ flashcard: trạng thái kiểm duyệt, lý do công khai, ghi chú nội bộ tùy chọn, người xử lý, thời điểm xử lý và dấu hiệu đồng thời.
- Thêm Báo cáo nội dung với người báo cáo, bộ được báo cáo, loại lý do, mô tả, trạng thái, người xử lý, kết quả, lý do xử lý và các mốc thời gian. Ràng buộc ngăn một người có nhiều báo cáo đang mở cho cùng một bộ.
- Thêm Bản ghi kiểm toán quản trị dạng chỉ ghi thêm, gồm Admin, hành động, loại/mã đối tượng, thời gian UTC, kết quả, lý do, mã tương quan và siêu dữ liệu an toàn có giới hạn kích thước.
- Thêm dữ liệu vận hành AI đủ để tính số yêu cầu, số lỗi, độ trễ, nhà cung cấp/mô hình và lần chuyển dự phòng mà không lưu câu lệnh hay nội dung hội thoại.
- Mở rộng Nhà cung cấp AI để thể hiện thứ tự chính/dự phòng, trạng thái vô hiệu hóa, số lần kiểm tra thất bại liên tiếp và dấu hiệu đồng thời. Tận dụng thứ tự ưu tiên hiện có khi phù hợp.
- Dùng trường khóa phiên bản cho các đối tượng có thao tác nhạy cảm để phát hiện thay đổi đồng thời.
- Thêm chỉ mục phục vụ lọc/phân trang theo trạng thái, thời gian, người dùng, đối tượng, báo cáo đang mở và các khoảng đo KPI.
- Dùng migration có thể nâng cấp dữ liệu hiện có an toàn: bộ flashcard mặc định ở trạng thái bình thường; Nhà cung cấp AI hiện có giữ thứ tự và trạng thái; dữ liệu lịch sử không bị xóa khi triển khai.

### Người dùng và khóa tài khoản

- Dùng cơ chế khóa của ASP.NET Core Identity cho Khóa tài khoản có thời hạn vô hạn đến khi Admin khôi phục.
- Khi khóa hoặc yêu cầu thu hồi phiên, cập nhật dấu bảo mật để cookie hiện có mất hiệu lực ngay ở lần yêu cầu tiếp theo.
- Kiểm tra bất biến trước khi khóa: không phải chính mình, không phải Admin khởi tạo và không làm số Admin hoạt động giảm về 0.
- Không cho Admin xem/đặt mật khẩu, sửa hồ sơ người dùng, xóa vĩnh viễn tài khoản hoặc thay đổi vai trò.
- Việc khóa không tự động cách ly nội dung công khai; kiểm duyệt nội dung là quyết định riêng.

### Kiểm duyệt nội dung

- Chỉ người dùng đã đăng nhập được báo cáo bộ công khai không thuộc sở hữu của mình.
- Loại lý do dùng danh mục cố định ở phiên bản 1; mô tả là tùy chọn và có giới hạn độ dài.
- Trạng thái báo cáo tối thiểu gồm đang chờ, đã bác bỏ và đã xử lý bằng cách ly.
- Cách ly loại bộ khỏi mọi truy vấn công khai và ngăn tác giả tự đổi lại trạng thái công khai. Tác giả vẫn được đọc/sửa nội dung.
- Khôi phục chỉ do Admin thực hiện. Mỗi lần cách ly/khôi phục/bác bỏ phải có lý do và Bản ghi kiểm toán quản trị.
- Danh sách Admin chỉ hiện thông tin khái quát của bộ riêng tư. Mở chi tiết yêu cầu lý do và tạo bản ghi truy cập nhạy cảm.
- Không có sửa nội dung thay tác giả, xóa vĩnh viễn hoặc quy trình kháng nghị tự động.

### Hồ sơ học tập, Nhiệm vụ tiếng Anh và thành tích

- Truy vấn hồ sơ học tập ở chế độ chỉ đọc; mở chi tiết theo người dùng tạo Bản ghi kiểm toán quản trị.
- Không cung cấp lệnh sửa điểm, tiến độ hoặc xóa phiên học.
- Danh sách Nhiệm vụ tiếng Anh mặc định chỉ hiển thị dữ liệu khái quát. Mở hội thoại yêu cầu loại vụ việc và lý do.
- Phản hồi hội thoại cho Admin loại bỏ câu lệnh hệ thống, khóa bí mật và chi tiết nội bộ Nhà cung cấp AI; chỉ trả về phần hội thoại cần thiết.
- Tác vụ nền xóa nội dung hội thoại quá 90 ngày. Hội thoại có vụ việc đang mở được hoãn xóa, nhưng không quá 12 tháng. Giữ lại điểm, trạng thái, số lượt và số liệu tổng hợp.
- Danh mục thành tích tiếp tục nằm trong source code. Dashboard chỉ đọc định nghĩa và kết quả cấp.
- Đồng bộ lại thành tích gọi cơ chế tính toán hiện có, có trạng thái chạy/kết quả, chống chạy trùng, xác nhận và audit; không cho cấp/thu hồi thủ công.

### Nhà cung cấp AI và vận hành

- Khóa bí mật được bảo vệ bằng cơ chế bảo vệ dữ liệu hiện có, chỉ hiển thị trạng thái đã cấu hình và bốn ký tự cuối; không có API đọc lại khóa gốc.
- Thay thao tác xóa hiện có bằng vô hiệu hóa. Không xóa cứng Nhà cung cấp AI đã từng được dùng.
- Một nhà cung cấp chính được chọn rõ ràng; các nhà cung cấp dự phòng phải đang bật, đã kiểm tra thành công và được sắp thứ tự.
- Khi nhà cung cấp chính lỗi, bộ định tuyến thử nhà cung cấp dự phòng theo thứ tự cấu hình, với giới hạn thời gian tổng thể để tránh nhân thời gian chờ không giới hạn.
- Vô hiệu hóa chặn yêu cầu mới nhưng không hủy yêu cầu đang chạy; kết quả đã hoàn thành không được chạy lại.
- Người học chỉ nhận lỗi nghiệp vụ chung. Chi tiết lỗi kỹ thuật nằm trong dữ liệu vận hành an toàn dành cho Admin.
- Ba lần kiểm tra thất bại liên tiếp đánh dấu nhà cung cấp không ổn định; một lần kiểm tra thành công đặt lại bộ đếm.
- Cảnh báo tỷ lệ lỗi dùng cửa sổ 5 phút, ngưỡng trên 10% và tối thiểu 20 yêu cầu. Các ngưỡng nằm trong cấu hình hệ thống, không sửa trên dashboard ở phiên bản 1.
- Chỉ số chưa đủ mẫu trả về trạng thái riêng, không trả về 0%.

### Chỉ số, cảnh báo, tìm kiếm và xuất dữ liệu

- Người dùng hoạt động là người có ít nhất một phiên học hoàn thành, lượt ôn flashcard hoặc lượt Nhiệm vụ tiếng Anh trong khoảng đã chọn; mỗi người chỉ tính một lần.
- Tỷ lệ hoàn thành bằng số phiên có thời điểm hoàn thành chia tổng số phiên bắt đầu đủ điều kiện. Phiên chưa quá 30 phút và đang hoạt động bị loại tạm thời; phiên quá 30 phút không hoạt động được tính là bỏ dở.
- Mọi ngày giờ lưu theo UTC; hiển thị theo múi giờ Việt Nam. Khoảng thời gian dùng ranh giới UTC được quy đổi từ ngày địa phương để tránh lệch số liệu.
- Các truy vấn KPI trả cả giá trị hiện tại, kỳ liền trước và trạng thái đủ/chưa đủ dữ liệu.
- Cảnh báo được suy ra từ trạng thái hiện tại, không cần thao tác đóng thủ công. Báo cáo quá hạn dùng mốc 24 giờ.
- Tìm kiếm toàn cục chỉ trả về loại đối tượng, thông tin nhận diện an toàn và liên kết đến trang chi tiết; không tìm trong hội thoại.
- Chỉ cho xuất CSV số liệu tổng hợp và danh sách kiểm toán đã lọc. Giới hạn khoảng thời gian/kích thước, xử lý công thức CSV nguy hiểm và audit mỗi lần xuất.

### AJAX, giao diện và khả năng tiếp cận

- Các điểm cuối AJAX chỉ đọc dành cho Ảnh chụp vận hành phải yêu cầu Admin, không lưu cache công khai và trả cấu trúc dữ liệu ổn định.
- Trình duyệt gọi mỗi 30 giây, không chồng yêu cầu nếu lần trước chưa xong, hủy hợp lý khi rời trang, tạm dừng khi thẻ ẩn và làm mới ngay khi quay lại.
- Yêu cầu thay đổi dữ liệu dùng POST, mã chống giả mạo yêu cầu, xác nhận, lý do và khóa phiên bản; AJAX không làm yếu các bảo vệ MVC hiện có.
- Danh sách lọc/sắp xếp/phân trang ở máy chủ, mặc định 25 và tối đa 100 dòng. Giữ bộ lọc trong chuỗi truy vấn để có thể chia sẻ/tải lại trang.
- Giao diện dùng hệ thống token, kiểu chữ, màu, khoảng cách và thành phần của project; layout riêng không tạo hệ thống thiết kế thứ hai.
- Bố cục tối ưu cho máy tính/máy tính bảng và đầy đủ chức năng từ 360 px. Bảng chuyển sang thẻ hoặc cuộn ngang có nhãn hợp lý.
- Mọi chức năng dùng được bằng bàn phím; có focus rõ, nhãn biểu mẫu, lỗi liên kết đúng trường, thông báo trạng thái cho công nghệ hỗ trợ và bảng chữ thay thế biểu đồ.
- Tôn trọng thiết lập giảm chuyển động. Giao diện phiên bản 1 chỉ dùng tiếng Việt.

### Kiểm toán, đồng thời và lưu dữ liệu

- Dịch vụ kiểm toán là cổng chung cho mọi thao tác Admin và truy cập nhạy cảm.
- Bản ghi thao tác thay đổi phải được lưu cùng giao dịch nghiệp vụ khi có thể; không báo thành công nếu dữ liệu đổi mà audit thất bại.
- Bản ghi truy cập nhạy cảm được tạo trước hoặc cùng lúc trả dữ liệu. Thất bại kiểm toán làm yêu cầu truy cập thất bại an toàn.
- Metadata dùng danh sách trường cho phép; không ghi payload tùy ý, mật khẩu, khóa bí mật, câu lệnh AI hoặc toàn bộ hội thoại.
- Tác vụ nền xóa Bản ghi kiểm toán quá 12 tháng theo lô có giới hạn.
- Xung đột khóa phiên bản trả thông báo tiếng Việt và yêu cầu tải lại; không tự ghi đè.

### Trình tự bàn giao

1. Nền tảng Area Admin, phân quyền, xác thực hai bước, layout và kiểm toán.
2. Dashboard chỉ đọc, người dùng, hồ sơ học tập và tìm kiếm.
3. Kiểm duyệt nội dung, báo cáo, Nhiệm vụ tiếng Anh và Đồng bộ lại thành tích.
4. Nhà cung cấp AI, chuyển dự phòng, AJAX, cảnh báo, xuất dữ liệu và hoàn thiện bảo mật/hiệu năng.

Mỗi giai đoạn phải build thành công, migration được kiểm tra và toàn bộ kiểm thử liên quan đạt trước khi bắt đầu giai đoạn sau.

## Quyết định kiểm thử

- Điểm kiểm thử chính là luồng HTTP hoàn chỉnh chạy qua ứng dụng thử nghiệm. Kiểm tra mã trạng thái/chuyển hướng, HTML hoặc JSON, thay đổi cơ sở dữ liệu và Bản ghi kiểm toán từ góc nhìn bên ngoài.
- Dùng SQLite trong bộ nhớ để giữ hành vi quan hệ, chỉ mục và giao dịch gần với cơ sở dữ liệu thật hơn bộ lưu trữ giả lập không quan hệ.
- Tạo người dùng và vai trò Identity trong dữ liệu thử nghiệm, sinh phiên khách/người học/Admin có hoặc không có xác thực hai bước để kiểm tra ma trận truy cập.
- Thay `TimeProvider` để kiểm soát cửa sổ 15 phút, 30 phút, 5 phút, 24 giờ, 90 ngày và 12 tháng mà không chờ thời gian thật.
- Thay adapter AI bằng giả lập xác định được kết quả để kiểm tra thành công, lỗi, thời gian chờ, ba lần thất bại và thứ tự chuyển dự phòng mà không gọi mạng.
- Kiểm thử tích hợp bao phủ ít nhất: định tuyến Area; chuyển hướng sau đăng nhập; 401/403; bắt buộc hai bước; xác nhận lại danh tính; khóa/mở khóa và bất biến Admin; thu hồi cookie; truy cập nhạy cảm; kiểm duyệt; báo cáo trùng; audit; phân trang; xuất CSV; các điểm cuối AJAX và chống giả mạo.
- Kiểm thử dịch vụ tập trung vào thuật toán khó quan sát trực tiếp qua một yêu cầu: tính KPI, ranh giới ngày/múi giờ, chuyển AI dự phòng, ngưỡng cảnh báo, dọn dữ liệu hết hạn, đồng bộ thành tích và xử lý đồng thời.
- Kiểm thử migration xác nhận mô hình mới khớp snapshot, dữ liệu cũ nhận mặc định an toàn và các ràng buộc/chỉ mục quan trọng tồn tại.
- Một nhóm kiểm thử trình duyệt nhỏ xác minh gọi AJAX 30 giây, không chồng yêu cầu, dừng khi ẩn thẻ, làm mới khi quay lại, điều hướng bàn phím, hộp xác nhận và bố cục tại 360 px.
- Kiểm thử giao diện chỉ xác minh hợp đồng người dùng và khả năng tiếp cận; không khóa cứng HTML/CSS nội bộ không quan trọng.
- Mỗi hạng mục triển khai phải thêm kiểm thử ở điểm cao nhất có thể. Chỉ thêm seam mới khi luồng HTTP hoặc seam dịch vụ hiện có không thể quan sát hành vi.

## Ngoài phạm vi

- Nhiều vai trò quản trị, phân quyền chi tiết hoặc giao diện cấp/thu hồi vai trò Admin.
- Đăng nhập thay hoặc chiếm phiên của người dùng.
- Xem/đặt mật khẩu, sửa hồ sơ người dùng hoặc xóa vĩnh viễn tài khoản.
- Admin sửa nội dung flashcard thay tác giả hoặc xóa cứng nội dung.
- Quy trình kháng nghị kiểm duyệt tự động.
- Cấp/thu hồi thành tích thủ công hoặc sửa danh mục thành tích trên dashboard.
- Xuất hàng loạt hồ sơ cá nhân, lịch sử học hoặc hội thoại.
- Tìm kiếm toàn văn trong hội thoại.
- Gửi thư điện tử hoặc thông báo đẩy tự động.
- Giao diện đa ngôn ngữ.
- Ứng dụng một trang tách biệt, API công khai, SignalR hoặc cập nhật tức thời cho dữ liệu cá nhân.
- Cho Admin chỉnh ngưỡng cảnh báo AI trên giao diện.
- Ứng dụng quản trị dành riêng cho thiết bị di động hoặc chế độ ngoại tuyến.

## Ghi chú thêm

- Source code hiện có đã dùng ASP.NET Core Identity với vai trò Admin khởi tạo, bảo vệ khóa AI, kiểm tra mạng riêng, chống giả mạo yêu cầu, giới hạn tần suất, cơ chế thành tích và `TimeProvider`; kế hoạch nên mở rộng các điểm này thay vì thay thế.
- Chức năng Nhà cung cấp AI hiện có cho phép tạo/sửa/xóa, thử kết nối và khám phá mô hình. Khi đưa vào Area, thao tác xóa phải đổi thành vô hiệu hóa và mọi thay đổi phải đi qua xác nhận, xác nhận lại danh tính khi cần và kiểm toán.
- Project hiện chưa đăng ký tuyến Area, chưa có Bản ghi kiểm toán quản trị, Báo cáo nội dung, trạng thái cách ly, nhật ký vận hành AI hay tác vụ lưu trữ. Đây là các phần thay đổi lược đồ chính.
- Prototype chỉ là công cụ quyết định thiết kế. Giao diện sản xuất dùng phương án A và dữ liệu thật; các biến thể thử nghiệm không trở thành chức năng cho người dùng cuối.
