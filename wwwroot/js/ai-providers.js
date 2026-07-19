(function () {
    var page = document.querySelector(".provider-page");
    if (!page) {
        return;
    }

    var tokenInput = page.querySelector("input[name=\"__RequestVerificationToken\"]");
    var dialog = document.getElementById("provider-dialog");

    // Gửi POST nhẹ cho các thao tác không đổi cấu hình: test kết nối và lấy danh sách model.
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

    // Đổ model vào dialog bằng textContent để không render HTML từ dữ liệu provider trả về.
    function renderModels(models) {
        var summary = dialog.querySelector("p");
        var list = dialog.querySelector("ul");
        summary.textContent = models.length + " model được tìm thấy.";
        list.innerHTML = "";

        models.forEach(function (model) {
            var item = document.createElement("li");
            item.textContent = model;
            list.appendChild(item);
        });

        dialog.hidden = false;
    }

    // Bắt click trong danh sách provider để gọi đúng action của từng card.
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

            if (target.closest(".provider-models")) {
                target.disabled = true;
                var data = await postProviderCommand(id, "Models");
                renderModels(data.models);
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

    // Đóng dialog model khi Admin đã xem xong.
    dialog.querySelector(".provider-dialog-close").addEventListener("click", function () {
        dialog.hidden = true;
    });
})();
