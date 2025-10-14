document.addEventListener('DOMContentLoaded', function () {
    var toastEl = document.querySelector('.toast');
    var toastBody = toastEl.querySelector('.toast-body').textContent.trim();

    if (toastBody) {
        var toast = new bootstrap.Toast(toastEl, {
            delay: 3000,
            autohide: true
        });
        toast.show();
    }
});