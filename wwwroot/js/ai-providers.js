(function () {
    var page = document.querySelector(".provider-page");
    if (!page) {
        return;
    }

    var tokenInput = page.querySelector("input[name=\"__RequestVerificationToken\"]");
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
        var card = target.closest(".provider-card");
        if (!card) {
            return;
        }

        var id = card.dataset.providerId;
        try {
            if (target.closest(".provider-test")) {
                target.disabled = true;
                await postProviderCommand(id, "Test");
                location.reload();
                return;
            }

        }
        catch (error) {
            alert(error.message);
        }
        finally {
            if (target) {
                target.disabled = false;
            }
        }
    });
})();
