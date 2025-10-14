// sidebar menu
const hamBurger = document.querySelector(".toggle-btn");

hamBurger.addEventListener("click", function () {
    const sidebar = document.querySelector("#sidebar");
    const navbar = document.querySelector("#navbar");

    sidebar.classList.toggle("expand");
    navbar.classList.toggle("expand");
});

function handleResize() {
    const sidebar = document.getElementById("sidebar");

    if (window.innerWidth <= 768) {
        sidebar.classList.remove("expand");
    } else {
        sidebar.classList.add("expand");
    }
}

// Attach the resize event listener
window.addEventListener("resize", handleResize);

// Run on initial load
document.addEventListener("DOMContentLoaded", handleResize);

// active sidebar links
const sidebarLinks = document.querySelectorAll('.sidebar-link');

sidebarLinks.forEach(link => {
    link.addEventListener('click', function () {
        sidebarLinks.forEach(link => link.classList.remove('active'));

        this.classList.add('active');
    });
});

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