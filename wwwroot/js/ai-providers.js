(function () {
    var page = document.querySelector(".provider-page");
    if (!page) {
        return;
    }

    var tokenInput = page.querySelector("input[name=\"__RequestVerificationToken\"]");
    var notice = page.querySelector("[data-provider-notice]");
    var noticeText = notice.querySelector("[data-provider-notice-text]");
    var noticeIcon = notice.querySelector("[data-provider-notice-icon]");

    function showNotice(message, type, shouldFocus) {
        notice.classList.remove("is-pending", "is-success", "is-error");
        notice.classList.add("is-" + type);
        noticeText.textContent = message;
        noticeIcon.className = type === "success"
            ? "ph ph-check-circle"
            : type === "error" ? "ph ph-warning-circle" : "ph ph-circle-notch";
        notice.setAttribute("aria-live", type === "error" ? "assertive" : "polite");
        notice.hidden = false;
        if (shouldFocus !== false) notice.focus();
    }

    // Gửi POST để kiểm tra kết nối mà không làm lộ khóa API.
    async function postProviderCommand(id, action) {
        var body = new URLSearchParams();
        body.append("__RequestVerificationToken", tokenInput.value);

        var response = await fetch("/Admin/AiProviders/" + id + "/" + action, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded"
            },
            body: body.toString()
        });
        var data = await response.json();
        if (!response.ok) {
            var message = "Yêu cầu thất bại.";
            if (data.error) {
                message = data.error;
            }

            throw new Error(message);
        }

        return data;
    }

    // Bắt click trong danh sách nhà cung cấp để gọi đúng action của từng card.
    page.addEventListener("click", async function (event) {
        var target = event.target;
        if (target.closest("[data-provider-notice-dismiss]")) {
            notice.hidden = true;
            return;
        }

        var card = target.closest(".provider-card");
        if (!card) {
            return;
        }

        var id = card.dataset.providerId;
        var testButton = target.closest(".provider-test");
        if (!testButton) return;

        try {
            testButton.disabled = true;
            showNotice("Đang kiểm tra kết nối tới nhà cung cấp...", "pending", false);
            await postProviderCommand(id, "Test");
            showNotice("Kết nối thành công. Đang cập nhật trạng thái...", "success");
            window.setTimeout(function () { location.reload(); }, 700);
        }
        catch (error) {
            showNotice(error.message, "error");
        }
        finally {
            testButton.disabled = false;
        }
    });
})();
