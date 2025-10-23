// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Функции для работы с корзиной
function addToCart(computerId, quantity = 1, computerName = '') {
    $.post('/Cart/AddToCart', { computerId, quantity })
        .done(function (response) {
            if (response.success) {
                // Показываем уведомление
                showNotification('success', `Товар "${computerName}" добавлен в корзину`);

                // Обновляем счетчик в корзине
                updateCartCounter(response.totalItems);
            } else {
                showNotification('error', response.message);
            }
        })
        .fail(function () {
            showNotification('error', 'Ошибка при добавлении в корзину');
        });
}

function updateCartCounter(count) {
    // Можно добавить счетчик в навигации
    const cartCounter = $('#cart-counter');
    if (cartCounter.length) {
        cartCounter.text(count);
    }
}

function showNotification(type, message) {
    // Простое уведомление через alert
    alert(message);
}

// Обработчики для кнопок "В корзину"
$(document).ready(function () {
    $('.add-to-cart').on('click', function () {
        const computerId = $(this).data('computer-id');
        const computerName = $(this).data('computer-name');
        addToCart(computerId, 1, computerName);
    });
});