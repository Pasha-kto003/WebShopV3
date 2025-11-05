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


// Анимация при скролле для главной страницы
document.addEventListener('DOMContentLoaded', function () {
    // Анимация при скролле
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.group, section').forEach((el) => {
        observer.observe(el);
    });

    // Плавная прокрутка для якорных ссылок
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });
});


// Функциональность для страницы каталога компьютеров
function initializeCatalogPage() {
    let searchTimeout;

    // Функция переключения вида
    function switchView(viewType) {
        const cardsView = document.getElementById('cardsView');
        const listViewContainer = document.getElementById('listViewContainer');
        const cardViewBtn = document.getElementById('cardViewBtn');
        const listViewBtn = document.getElementById('listViewBtn');

        if (viewType === 'cards') {
            cardsView.classList.remove('hidden');
            listViewContainer.classList.add('hidden');
            cardViewBtn.classList.add('bg-primary', 'text-dark');
            cardViewBtn.classList.remove('text-gray-400');
            listViewBtn.classList.remove('bg-primary', 'text-dark');
            listViewBtn.classList.add('text-gray-400');
        } else {
            cardsView.classList.add('hidden');
            listViewContainer.classList.remove('hidden');
            listViewBtn.classList.add('bg-primary', 'text-dark');
            listViewBtn.classList.remove('text-gray-400');
            cardViewBtn.classList.remove('bg-primary', 'text-dark');
            cardViewBtn.classList.add('text-gray-400');
        }
    }

    function performSearch() {
        // Показываем индикатор загрузки
        $('#loadingIndicator').removeClass('hidden');
        $('#computerList').addClass('hidden');

        // Отменяем предыдущий таймаут
        if (searchTimeout) {
            clearTimeout(searchTimeout);
        }

        // Устанавливаем новый таймаут для поиска (задержка 500ms)
        searchTimeout = setTimeout(() => {
            const searchParams = {
                search: $('#searchInput').val(),
                sortBy: $('#sortSelect').val(),
                componentType: $('#componentTypeFilter').val(),
                minPrice: $('#minPrice').val(),
                maxPrice: $('#maxPrice').val()
            };

            $.post('/Home/SearchComputers', searchParams)
                .done(function (data) {
                    $('#computerList').html(data);
                    $('#computerList').removeClass('hidden');
                    // Обновляем счетчик результатов
                    const count = $(data).find('.computer-card').length;
                    $('#resultsCount').text(`Найдено ${count} готовых сборок`);
                })
                .fail(function () {
                    $('#computerList').html('<div class="bg-red-500/20 border border-red-500/30 text-red-500 px-4 py-3 rounded-lg">Ошибка при загрузке данных</div>');
                    $('#computerList').removeClass('hidden');
                })
                .always(function () {
                    $('#loadingIndicator').addClass('hidden');
                });
        }, 500);
    }

    // События для элементов фильтрации
    $('#searchInput').on('input', performSearch);
    $('#sortSelect').on('change', performSearch);
    $('#componentTypeFilter').on('change', performSearch);
    $('#minPrice, #maxPrice').on('input', performSearch);

    // Сброс фильтров
    $('#resetFilters').on('click', function () {
        $('#searchInput').val('');
        $('#componentTypeFilter').val('all');
        $('#minPrice').val('');
        $('#maxPrice').val('');
        $('#sortSelect').val('default');
        performSearch();
    });

    // Обработчики переключения вида
    const cardViewBtn = document.getElementById('cardViewBtn');
    const listViewBtn = document.getElementById('listViewBtn');

    if (cardViewBtn && listViewBtn) {
        cardViewBtn.addEventListener('click', () => switchView('cards'));
        listViewBtn.addEventListener('click', () => switchView('list'));
    }

    // Инициализация при загрузке страницы
    $(document).ready(function () {
        // Устанавливаем выбранные значения из ViewBag
        const sortBy = document.getElementById('sortSelect').dataset.sortBy || 'default';
        const componentType = document.getElementById('componentTypeFilter').dataset.componentType || 'all';

        $('#sortSelect').val(sortBy);
        $('#componentTypeFilter').val(componentType);

        // Инициализация вида по умолчанию
        switchView('cards');

        // Анимация появления элементов
        const observerOptions = {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        };

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('animate-fade-in');
                }
            });
        }, observerOptions);

        document.querySelectorAll('.computer-card').forEach((el) => {
            observer.observe(el);
        });
    });
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице каталога
    if (document.getElementById('computerList')) {
        initializeCatalogPage();
    }
});


// Функция смены главного изображения
function changeMainImage(src) {
    const mainImage = document.getElementById('mainImage');
    if (mainImage) {
        mainImage.style.opacity = '0';

        setTimeout(() => {
            mainImage.src = src;
            mainImage.style.opacity = '1';
        }, 200);
    }
}

// Обработчик добавления в корзину
$(document).ready(function () {
    // Обработчик для миниатюр
    $('.thumbnail-button').on('click', function () {
        const imageSrc = $(this).data('image-src');
        changeMainImage(imageSrc);
    });

    // Обработчик для кнопки добавления в корзину
    $('.add-to-cart-details').on('click', function () {
        const computerId = $(this).data('computer-id');
        const computerName = $(this).data('computer-name');
        const quantity = $('#quantity').val();

        if (typeof addToCart === 'function') {
            addToCart(computerId, quantity, computerName);
        }
    });
});

// Анимация появления элементов
document.addEventListener('DOMContentLoaded', function () {
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.bg-dark-card').forEach((el) => {
        observer.observe(el);
    });
});


// Функция для страницы ошибки 403
function initErrorPage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.bg-dark-card').forEach((el) => {
        observer.observe(el);
    });

    // Пульсирующая анимация для иконки ошибки
    const errorIcon = document.querySelector('.error-icon');
    if (errorIcon) {
        setInterval(() => {
            errorIcon.classList.toggle('bg-red-500/20');
            errorIcon.classList.toggle('bg-red-500/30');
        }, 2000);
    }
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице ошибки 403
    const errorTitle = document.querySelector('h1');
    if (errorTitle && errorTitle.textContent.includes('Доступ запрещен')) {
        initErrorPage();
    }
});


// Функция для страницы входа
function initLoginPage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.login-form').forEach((el) => {
        observer.observe(el);
    });

    // Валидация формы
    const form = document.getElementById('loginForm');
    if (form) {
        form.addEventListener('submit', function (e) {
            const username = document.getElementById('username').value.trim();
            const password = document.getElementById('password').value.trim();

            if (!username || !password) {
                e.preventDefault();
                // Добавляем визуальную обратную связь
                if (!username) {
                    document.getElementById('username').classList.add('border-red-500');
                }
                if (!password) {
                    document.getElementById('password').classList.add('border-red-500');
                }
            }
        });

        // Убираем красную обводку при вводе
        const usernameInput = document.getElementById('username');
        const passwordInput = document.getElementById('password');

        if (usernameInput) {
            usernameInput.addEventListener('input', function () {
                this.classList.remove('border-red-500');
            });
        }

        if (passwordInput) {
            passwordInput.addEventListener('input', function () {
                this.classList.remove('border-red-500');
            });
        }
    }
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице входа
    const loginTitle = document.querySelector('h1');
    if (loginTitle && loginTitle.textContent.includes('Вход в систему')) {
        initLoginPage();
    }
});



// Функция для страницы личного кабинета
function initUserProfilePage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    // Наблюдаем за всеми карточками на странице профиля
    document.querySelectorAll('.user-profile-card, .quick-actions-card, .orders-card, .activity-card, .stats-card').forEach((el) => {
        observer.observe(el);
    });

    // Анимация для строк таблицы заказов
    const orderRows = document.querySelectorAll('.order-row');
    orderRows.forEach((row, index) => {
        row.style.animationDelay = `${index * 0.1}s`;
        row.classList.add('animate-table-row');
    });

    // Обработчики для кнопок быстрых действий
    const quickActionBtns = document.querySelectorAll('.quick-action-btn');
    quickActionBtns.forEach(btn => {
        btn.addEventListener('mouseenter', function () {
            this.style.transform = 'translateY(-2px)';
        });
        btn.addEventListener('mouseleave', function () {
            this.style.transform = 'translateY(0)';
        });
    });

    // Анимация для кнопки первого заказа
    const firstOrderBtn = document.querySelector('.first-order-btn');
    if (firstOrderBtn) {
        firstOrderBtn.addEventListener('mouseenter', function () {
            this.style.transform = 'scale(1.05)';
        });
        firstOrderBtn.addEventListener('mouseleave', function () {
            this.style.transform = 'scale(1)';
        });
    }

    // Подсветка последней активности
    const lastActivity = document.querySelector('.last-activity');
    if (lastActivity) {
        setInterval(() => {
            lastActivity.classList.toggle('bg-gray-800/50');
            lastActivity.classList.toggle('bg-primary/10');
        }, 3000);
    }
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице личного кабинета
    const profileTitle = document.querySelector('h1');
    if (profileTitle && profileTitle.textContent.includes('Личный кабинет')) {
        initUserProfilePage();
    }
});



// Функция для страницы регистрации
function initRegisterPage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.register-form').forEach((el) => {
        observer.observe(el);
    });

    // Валидация формы при отправке
    const form = document.getElementById('registerForm');
    if (form) {
        form.addEventListener('submit', function (e) {
            const requiredFields = form.querySelectorAll('input[required]');
            let hasEmptyFields = false;

            requiredFields.forEach(field => {
                if (!field.value.trim()) {
                    field.classList.add('border-red-500');
                    hasEmptyFields = true;
                }
            });

            if (hasEmptyFields) {
                e.preventDefault();
            }
        });

        // Убираем красную обводку при вводе
        const inputs = form.querySelectorAll('.register-input');
        inputs.forEach(input => {
            input.addEventListener('input', function () {
                this.classList.remove('border-red-500');
            });
        });

        // Валидация email в реальном времени
        const emailInput = document.querySelector('input[type="email"]');
        if (emailInput) {
            emailInput.addEventListener('blur', function () {
                const email = this.value.trim();
                if (email && !isValidEmail(email)) {
                    this.classList.add('border-red-500');
                }
            });
        }

        // Валидация пароля
        const passwordInput = document.querySelector('input[type="password"]');
        if (passwordInput) {
            passwordInput.addEventListener('blur', function () {
                const password = this.value.trim();
                if (password && password.length < 6) {
                    this.classList.add('border-red-500');
                }
            });
        }
    }

    // Анимация для иконки регистрации
    const registerIcon = document.querySelector('.register-icon');
    if (registerIcon) {
        registerIcon.style.animation = 'bounceIn 1s ease-in-out';
    }
}

// Функция проверки email
function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице регистрации
    const registerTitle = document.querySelector('h1');
    if (registerTitle && registerTitle.textContent.includes('Регистрация')) {
        initRegisterPage();
    }
});



// Функция для страницы оформления заказа
function initCheckoutPage() {
    // Автоподстановка адреса, если пользователь ранее его указывал
    function loadSavedAddress() {
        const savedAddress = localStorage.getItem('userAddress');
        if (savedAddress) {
            $('textarea[name="address"]').val(savedAddress);
        }
    }

    // Сохранение адреса при изменении
    $('textarea[name="address"]').on('input', function () {
        const address = $(this).val();
        if (address.length > 5) {
            localStorage.setItem('userAddress', address);
        }
    });

    // Валидация формы
    $('#checkout-form').on('submit', function (e) {
        const address = $('textarea[name="address"]').val().trim();
        if (address.length < 10) {
            e.preventDefault();
            showCheckoutNotification('Пожалуйста, укажите полный адрес доставки (не менее 10 символов)', 'error');
            $('textarea[name="address"]').focus();
            $('textarea[name="address"]').addClass('border-red-500');
            return false;
        } else {
            $('textarea[name="address"]').removeClass('border-red-500');
        }

        const phone = $('input[name="phone"]').val().trim();
        if (!isValidPhone(phone)) {
            e.preventDefault();
            showCheckoutNotification('Пожалуйста, укажите корректный номер телефона', 'error');
            $('input[name="phone"]').focus();
            $('input[name="phone"]').addClass('border-red-500');
            return false;
        } else {
            $('input[name="phone"]').removeClass('border-red-500');
        }

        // Показываем индикатор загрузки
        $('.checkout-submit').prop('disabled', true).html(`
            <div class="inline-block animate-spin rounded-full h-6 w-6 border-b-2 border-dark mr-2"></div>
            Оформляем заказ...
        `);
    });

    function isValidPhone(phone) {
        // Простая валидация телефона
        const phoneRegex = /^[\d\s\-\+\(\)]{10,}$/;
        return phoneRegex.test(phone);
    }

    function showCheckoutNotification(message, type = 'success') {
        const alertClass = type === 'success' ? 'bg-green-500/20 border-green-500/30 text-green-400' : 'bg-red-500/20 border-red-500/30 text-red-400';

        const notification = $(`
            <div class="fixed top-4 right-4 z-50 p-4 rounded-lg border backdrop-blur-sm transition-transform duration-300 transform translate-x-full ${alertClass}">
                ${message}
            </div>
        `);

        $('body').append(notification);

        setTimeout(() => {
            notification.removeClass('translate-x-full');
        }, 100);

        setTimeout(() => {
            notification.addClass('translate-x-full');
            setTimeout(() => {
                notification.remove();
            }, 300);
        }, 4000);
    }

    // Загружаем сохраненный адрес при загрузке страницы
    loadSavedAddress();

    // Подсказка для адреса
    $('textarea[name="address"]').on('focus', function () {
        if (!$(this).val()) {
            $(this).attr('placeholder', 'Например: г. Москва, ул. Примерная, д. 123, кв. 45');
        }
    });

    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    // Наблюдаем за всеми карточками на странице оформления заказа
    document.querySelectorAll('.checkout-form-card, .checkout-summary, .checkout-item, .checkout-notification, .checkout-security').forEach((el) => {
        observer.observe(el);
    });

    // Анимация для кнопок действий
    const actionBtns = document.querySelectorAll('.checkout-action-btn');
    actionBtns.forEach(btn => {
        btn.addEventListener('mouseenter', function () {
            this.style.transform = 'translateY(-2px)';
        });
        btn.addEventListener('mouseleave', function () {
            this.style.transform = 'translateY(0)';
        });
    });

    // Подсветка полей при фокусе
    const inputs = document.querySelectorAll('.checkout-input');
    inputs.forEach(input => {
        input.addEventListener('focus', function () {
            this.parentElement.classList.add('checkout-input-focused');
        });
        input.addEventListener('blur', function () {
            this.parentElement.classList.remove('checkout-input-focused');
        });
    });
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице оформления заказа
    const checkoutTitle = document.querySelector('h1');
    if (checkoutTitle && checkoutTitle.textContent.includes('Оформление заказа')) {
        initCheckoutPage();
    }
});



// Функция для страницы корзины
function initCartPage() {
    // Обновление количества
    $('.quantity-increase').on('click', function () {
        const computerId = $(this).data('computer-id');
        const quantityElement = $(this).siblings('.quantity');
        let quantity = parseInt(quantityElement.text());
        quantityElement.text(quantity + 1);
        updateCartQuantity(computerId, quantity + 1);
    });

    $('.quantity-decrease').on('click', function () {
        const computerId = $(this).data('computer-id');
        const quantityElement = $(this).siblings('.quantity');
        let quantity = parseInt(quantityElement.text());
        if (quantity > 1) {
            quantityElement.text(quantity - 1);
            updateCartQuantity(computerId, quantity - 1);
        }
    });

    // Удаление из корзины
    $('.remove-from-cart').on('click', function () {
        const computerId = $(this).data('computer-id');
        removeFromCart(computerId);
    });

    // Очистка корзины
    $('#clear-cart').on('click', function () {
        if (confirm('Вы уверены, что хотите очистить корзину?')) {
            clearCart();
        }
    });

    // Добавление в избранное
    $('.add-to-favorites').on('click', function () {
        const computerId = $(this).data('computer-id');
        addToFavorites(computerId);
    });

    // Добавление аксессуаров
    $('.add-accessory').on('click', function () {
        const name = $(this).data('name');
        const price = $(this).data('price');
        showCartNotification(`Товар "${name}" добавлен в корзину!`);
        // Здесь можно добавить логику добавления аксессуара в корзину
    });

    // Обновление дополнительных услуг
    $('.service-checkbox').on('change', function () {
        updateCartTotals();
    });

    // Применение промокода
    $('.apply-promo').on('click', function () {
        const promoInput = $(this).siblings('input');
        const promoCode = promoInput.val().trim();
        if (promoCode) {
            applyPromoCode(promoCode);
        }
    });

    function updateCartQuantity(computerId, quantity) {
        $.post('/Cart/UpdateQuantity', { computerId, quantity })
            .done(function (response) {
                if (response.success) {
                    updateCartTotals();
                    showCartNotification('Количество обновлено');
                } else {
                    showCartNotification(response.message, 'error');
                }
            })
            .fail(function () {
                showCartNotification('Ошибка при обновлении количества', 'error');
            });
    }

    function removeFromCart(computerId) {
        $.post('/Cart/RemoveFromCart', { computerId })
            .done(function (response) {
                if (response.success) {
                    $(`.cart-item[data-computer-id="${computerId}"]`).fadeOut(300, function () {
                        $(this).remove();
                        if ($('.cart-item').length === 0) {
                            location.reload();
                        } else {
                            updateCartTotals();
                        }
                    });
                    showCartNotification('Товар удален из корзины');
                } else {
                    showCartNotification(response.message, 'error');
                }
            })
            .fail(function () {
                showCartNotification('Ошибка при удалении товара', 'error');
            });
    }

    function clearCart() {
        $.post('/Cart/Clear')
            .done(function (response) {
                if (response.success) {
                    location.reload();
                }
            })
            .fail(function () {
                showCartNotification('Ошибка при очистке корзины', 'error');
            });
    }

    function addToFavorites(computerId) {
        $.post('/Favorites/Add', { computerId })
            .done(function (response) {
                if (response.success) {
                    showCartNotification('Товар добавлен в избранное');
                } else {
                    showCartNotification(response.message, 'error');
                }
            })
            .fail(function () {
                showCartNotification('Ошибка при добавлении в избранное', 'error');
            });
    }

    function applyPromoCode(promoCode) {
        // Здесь можно добавить логику применения промокода
        showCartNotification('Промокод применен!');
        updateCartTotals();
    }

    function updateCartTotals() {
        let subtotal = 0;

        // Сумма товаров
        $('.cart-item').each(function () {
            const price = parseFloat($(this).data('price'));
            const quantity = parseInt($(this).find('.quantity').text());
            subtotal += price * quantity;
        });

        // Сумма дополнительных услуг
        let servicesTotal = 0;
        $('.service-checkbox:checked').each(function () {
            servicesTotal += parseFloat($(this).data('price'));
        });

        // Итоговая сумма
        const total = subtotal + servicesTotal;

        // Обновление UI
        $('#subtotal').text(formatPrice(subtotal));
        $('#servicesTotal').text(formatPrice(servicesTotal));
        $('#total').text(formatPrice(total));
    }

    function formatPrice(price) {
        return new Intl.NumberFormat('ru-RU', {
            style: 'currency',
            currency: 'RUB'
        }).format(price);
    }

    function showCartNotification(message, type = 'success') {
        const alertClass = type === 'success' ? 'bg-green-500/20 border-green-500/30 text-green-400' : 'bg-red-500/20 border-red-500/30 text-red-400';

        const notification = $(`
            <div class="fixed top-4 right-4 z-50 p-4 rounded-lg border backdrop-blur-sm transition-transform duration-300 transform translate-x-full ${alertClass}">
                ${message}
            </div>
        `);

        $('body').append(notification);

        setTimeout(() => {
            notification.removeClass('translate-x-full');
        }, 100);

        setTimeout(() => {
            notification.addClass('translate-x-full');
            setTimeout(() => {
                notification.remove();
            }, 300);
        }, 3000);
    }

    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    // Наблюдаем за всеми элементами корзины
    document.querySelectorAll('.cart-item, .cart-summary, .services-section, .accessory-item, .empty-cart').forEach((el) => {
        observer.observe(el);
    });

    // Инициализация
    updateCartTotals();
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице корзины
    const cartTitle = document.querySelector('h1');
    if (cartTitle && cartTitle.textContent.includes('Корзина покупок')) {
        initCartPage();
    }
});


// Функция для страницы добавления комплектующего
function initComponentCreatePage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.component-form').forEach(function (el) {
        observer.observe(el);
    });

    // Валидация формы при отправке
    const form = document.getElementById('componentForm');
    if (form) {
        form.addEventListener('submit', function (e) {
            const requiredFields = form.querySelectorAll('input[required], select[required], textarea[required]');
            let hasEmptyFields = false;

            requiredFields.forEach(function (field) {
                if (!field.value.trim()) {
                    field.classList.add('border-red-500');
                    hasEmptyFields = true;
                }
            });

            if (hasEmptyFields) {
                e.preventDefault();
                // Прокрутка к первой ошибке
                const firstError = form.querySelector('.border-red-500');
                if (firstError) {
                    firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }
        });

        // Убираем красную обводку при вводе
        const inputs = form.querySelectorAll('.component-input');
        inputs.forEach(function (input) {
            input.addEventListener('input', function () {
                this.classList.remove('border-red-500');
                // Убираем сообщение об ошибке поля
                const fieldError = this.parentNode.querySelector('.field-error');
                if (fieldError) {
                    fieldError.remove();
                }
            });
        });

        // Форматирование цены
        const priceInput = document.querySelector('input[asp-for="Price"]');
        if (priceInput) {
            priceInput.addEventListener('blur', function () {
                let value = this.value.replace(/[^\d.]/g, '');
                if (value) {
                    value = parseFloat(value).toFixed(2);
                    this.value = value;
                }
            });
        }

        // Валидация количества
        const quantityInput = document.querySelector('input[asp-for="Quantity"]');
        if (quantityInput) {
            quantityInput.addEventListener('blur', function () {
                const value = parseInt(this.value);
                if (value < 0) {
                    this.classList.add('border-red-500');
                    showComponentFieldError(this, 'Количество не может быть отрицательным');
                }
            });
        }

        // Подсветка выбранного типа
        const typeSelect = document.querySelector('select[asp-for="Type"]');
        if (typeSelect) {
            typeSelect.addEventListener('change', function () {
                if (this.value) {
                    this.classList.add('border-green-500');
                } else {
                    this.classList.remove('border-green-500');
                }
            });
        }

        // Счетчик символов для текстовых полей
        const textareas = form.querySelectorAll('textarea');
        textareas.forEach(function (textarea) {
            const counter = textarea.parentNode.querySelector('.char-counter');
            if (counter) {
                updateCharCounter(counter, textarea.value.length);

                textarea.addEventListener('input', function () {
                    updateCharCounter(counter, this.value.length);
                });
            }
        });

        // Анимация для кнопки отправки
        const submitBtn = document.querySelector('.component-submit');
        if (submitBtn) {
            submitBtn.addEventListener('mouseenter', function () {
                this.style.transform = 'translateY(-2px)';
            });
            submitBtn.addEventListener('mouseleave', function () {
                this.style.transform = 'translateY(0)';
            });
        }

        // Анимация для кнопки отмены
        const cancelBtn = document.querySelector('.cancel-btn');
        if (cancelBtn) {
            cancelBtn.addEventListener('mouseenter', function () {
                this.style.transform = 'translateX(-5px)';
            });
            cancelBtn.addEventListener('mouseleave', function () {
                this.style.transform = 'translateX(0)';
            });
        }
    }

    function showComponentFieldError(field, message) {
        // Убираем предыдущее сообщение об ошибке
        const existingError = field.parentNode.querySelector('.field-error');
        if (existingError) {
            existingError.remove();
        }

        const errorElement = document.createElement('div');
        errorElement.className = 'field-error text-red-400 text-sm mt-1';
        errorElement.textContent = message;
        field.parentNode.appendChild(errorElement);
    }

    function updateCharCounter(counter, length) {
        counter.textContent = length + ' символов';
        if (length > 500) {
            counter.classList.add('text-yellow-400');
        } else {
            counter.classList.remove('text-yellow-400');
        }
    }

    // Анимация для иконки
    const componentIcon = document.querySelector('.component-icon');
    if (componentIcon) {
        componentIcon.style.animation = 'bounceIn 1s ease-in-out';
    }
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице создания комплектующего
    const componentTitle = document.querySelector('h1');
    if (componentTitle && componentTitle.textContent.includes('Добавить комплектующее')) {
        initComponentCreatePage();
    }
});



// Функция для страницы удаления комплектующего
function initComponentDeletePage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    // Наблюдаем за всеми элементами страницы удаления
    document.querySelectorAll('.component-info, .delete-form, .delete-warning, .component-stats, .component-description').forEach(function (el) {
        observer.observe(el);
    });

    // Подтверждение удаления с дополнительной проверкой
    const deleteForm = document.getElementById('deleteForm');
    if (deleteForm) {
        deleteForm.addEventListener('submit', function (e) {
            e.preventDefault();

            const componentName = '@Model.Name';
            const componentType = '@GetTypeDisplayName(Model.Type)';
            const componentPrice = '@Model.Price.ToString("C")';
            const componentQuantity = @Model.Quantity;

            const confirmationMessage =
                `Вы уверены, что хотите удалить комплектующее?\n\n` +
                `Название: ${componentName}\n` +
                `Тип: ${componentType}\n` +
                `Цена: ${componentPrice}\n` +
                `Количество: ${componentQuantity} шт.\n\n` +
                `Это действие невозможно отменить!`;

            if (confirm(confirmationMessage)) {
                // Дополнительная проверка для комплектующих в наличии
                if (componentQuantity > 0) {
                    const stockCheck = confirm(
                        'Внимание! На складе осталось ' + componentQuantity + ' шт. этого комплектующего. ' +
                        'Вы абсолютно уверены, что хотите его удалить?'
                    );
                    if (stockCheck) {
                        deleteForm.submit();
                    }
                } else {
                    deleteForm.submit();
                }
            }
        });
    }

    // Пульсирующая анимация для кнопки удаления
    const deleteButton = document.querySelector('.delete-btn');
    if (deleteButton) {
        setInterval(function () {
            deleteButton.classList.toggle('shadow-red-500/25');
            deleteButton.classList.toggle('shadow-red-500/40');
        }, 2000);
    }

    // Анимация для иконки удаления
    const deleteIcon = document.querySelector('.delete-icon');
    if (deleteIcon) {
        setInterval(function () {
            deleteIcon.classList.toggle('bg-red-500/20');
            deleteIcon.classList.toggle('bg-red-500/30');
        }, 1500);
    }

    // Анимация для предупреждения
    const warning = document.querySelector('.delete-warning');
    if (warning) {
        setInterval(function () {
            warning.classList.toggle('border-red-500/30');
            warning.classList.toggle('border-red-500/50');
        }, 2000);
    }

    // Анимация для кнопки отмены
    const cancelBtn = document.querySelector('.cancel-btn');
    if (cancelBtn) {
        cancelBtn.addEventListener('mouseenter', function () {
            this.style.transform = 'translateX(-5px)';
        });
        cancelBtn.addEventListener('mouseleave', function () {
            this.style.transform = 'translateX(0)';
        });
    }

    // Подсветка статистики
    const stats = document.querySelector('.component-stats');
    if (stats) {
        stats.addEventListener('mouseenter', function () {
            this.style.transform = 'scale(1.02)';
        });
        stats.addEventListener('mouseleave', function () {
            this.style.transform = 'scale(1)';
        });
    }
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице удаления комплектующего
    const deleteTitle = document.querySelector('h1');
    if (deleteTitle && deleteTitle.textContent.includes('Удалить комплектующее')) {
        initComponentDeletePage();
    }
});



// Функция для страницы подробностей о комплектующем
function initComponentDetailsPage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    // Наблюдаем за всеми элементами страницы подробностей
    document.querySelectorAll('.component-main-info, .component-description, .component-specifications, .component-actions, .component-stats, .component-type-info, .component-quick-actions').forEach(function (el) {
        observer.observe(el);
    });

    // Анимация для иконки
    const detailsIcon = document.querySelector('.component-details-icon');
    if (detailsIcon) {
        detailsIcon.style.animation = 'bounceIn 1s ease-in-out';
    }

    // Обработчики для быстрых действий
    const quickActionBtns = document.querySelectorAll('.quick-action-btn');
    quickActionBtns.forEach(function (btn) {
        btn.addEventListener('click', function () {
            const action = this.getAttribute('data-action');
            handleQuickAction(action);
        });
    });

    // Анимация для кнопки редактирования
    const editBtn = document.querySelector('.edit-btn');
    if (editBtn) {
        editBtn.addEventListener('mouseenter', function () {
            this.style.transform = 'translateY(-2px)';
        });
        editBtn.addEventListener('mouseleave', function () {
            this.style.transform = 'translateY(0)';
        });
    }

    // Анимация для кнопки назад
    const backBtn = document.querySelector('.back-btn');
    if (backBtn) {
        backBtn.addEventListener('mouseenter', function () {
            this.style.transform = 'translateX(-5px)';
        });
        backBtn.addEventListener('mouseleave', function () {
            this.style.transform = 'translateX(0)';
        });
    }

    // Подсветка статистики при наведении
    const stats = document.querySelector('.component-stats');
    if (stats) {
        stats.addEventListener('mouseenter', function () {
            this.style.transform = 'scale(1.02)';
        });
        stats.addEventListener('mouseleave', function () {
            this.style.transform = 'scale(1)';
        });
    }

    // Анимация для бейджей статуса
    const statusBadges = document.querySelectorAll('.bg-red-500\\/20, .bg-yellow-500\\/20, .bg-green-500\\/20');
    statusBadges.forEach(function (badge) {
        badge.addEventListener('mouseenter', function () {
            this.style.transform = 'scale(1.05)';
        });
        badge.addEventListener('mouseleave', function () {
            this.style.transform = 'scale(1)';
        });
    });

    function handleQuickAction(action) {
        const componentId = @Model.Id;
        const componentName = '@Model.Name';

        switch (action) {
            case 'update-quantity':
                const newQuantity = prompt(`Введите новое количество для "${componentName}":`, @Model.Quantity);
                if (newQuantity !== null && !isNaN(newQuantity) && newQuantity >= 0) {
                    updateComponentQuantity(componentId, parseInt(newQuantity));
                }
                break;

            case 'update-price':
                const newPrice = prompt(`Введите новую цену для "${componentName}":`, @Model.Price);
                if (newPrice !== null && !isNaN(newPrice) && newPrice >= 0) {
                    updateComponentPrice(componentId, parseFloat(newPrice));
                }
                break;
        }
    }

    function updateComponentQuantity(componentId, quantity) {
        // Здесь можно добавить AJAX запрос для обновления количества
        showComponentNotification(`Количество обновлено до ${quantity} шт.`);
        // Для обновления страницы:
        // location.reload();
    }

    function updateComponentPrice(componentId, price) {
        // Здесь можно добавить AJAX запрос для обновления цены
        showComponentNotification(`Цена обновлена до ${price.toFixed(2)}₽`);
        // Для обновления страницы:
        // location.reload();
    }

    function showComponentNotification(message, type = 'success') {
        const alertClass = type === 'success' ? 'bg-green-500/20 border-green-500/30 text-green-400' : 'bg-red-500/20 border-red-500/30 text-red-400';

        const notification = document.createElement('div');
        notification.className = `fixed top-4 right-4 z-50 p-4 rounded-lg border backdrop-blur-sm transition-transform duration-300 transform translate-x-full ${alertClass}`;
        notification.textContent = message;

        document.body.appendChild(notification);

        setTimeout(function () {
            notification.classList.remove('translate-x-full');
        }, 100);

        setTimeout(function () {
            notification.classList.add('translate-x-full');
            setTimeout(function () {
                notification.remove();
            }, 300);
        }, 3000);
    }

    // Подсветка изменяющихся значений
    const priceElement = document.querySelector('.text-primary.font-semibold');
    if (priceElement) {
        priceElement.addEventListener('mouseenter', function () {
            this.classList.add('component-value-highlight');
        });
        priceElement.addEventListener('mouseleave', function () {
            this.classList.remove('component-value-highlight');
        });
    }
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице подробностей о комплектующем
    const detailsTitle = document.querySelector('h1');
    if (detailsTitle && detailsTitle.textContent.includes('Подробности о комплектующем')) {
        initComponentDetailsPage();
    }
});



// Функция для страницы редактирования комплектующего
function initComponentEditPage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    // Наблюдаем за всеми элементами страницы редактирования
    document.querySelectorAll('.component-edit-form, .current-values, .form-actions').forEach(function (el) {
        observer.observe(el);
    });

    // Валидация формы при отправке
    const form = document.getElementById('componentEditForm');
    if (form) {
        form.addEventListener('submit', function (e) {
            const requiredFields = form.querySelectorAll('input[required], select[required], textarea[required]');
            let hasEmptyFields = false;

            requiredFields.forEach(function (field) {
                if (!field.value.trim()) {
                    field.classList.add('border-red-500');
                    hasEmptyFields = true;
                }
            });

            if (hasEmptyFields) {
                e.preventDefault();
                // Прокрутка к первой ошибке
                const firstError = form.querySelector('.border-red-500');
                if (firstError) {
                    firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }
        });

        // Убираем красную обводку при вводе
        const inputs = form.querySelectorAll('.component-edit-input');
        inputs.forEach(function (input) {
            input.addEventListener('input', function () {
                this.classList.remove('border-red-500');
                // Убираем сообщение об ошибке поля
                const fieldError = this.parentNode.querySelector('.field-error');
                if (fieldError) {
                    fieldError.remove();
                }
            });
        });

        // Форматирование цены
        const priceInput = document.querySelector('input[asp-for="Price"]');
        if (priceInput) {
            priceInput.addEventListener('blur', function () {
                let value = this.value.replace(/[^\d.]/g, '');
                if (value) {
                    value = parseFloat(value).toFixed(2);
                    this.value = value;
                }
            });
        }

        // Валидация количества
        const quantityInput = document.querySelector('input[asp-for="Quantity"]');
        if (quantityInput) {
            quantityInput.addEventListener('blur', function () {
                const value = parseInt(this.value);
                if (value < 0) {
                    this.classList.add('border-red-500');
                    showComponentFieldError(this, 'Количество не может быть отрицательным');
                }
            });
        }

        // Подсветка выбранного типа
        const typeSelect = document.querySelector('select[asp-for="Type"]');
        if (typeSelect) {
            // Устанавливаем выбранное значение при загрузке
            setTimeout(function () {
                if (typeSelect.value) {
                    typeSelect.classList.add('border-green-500');
                }
            }, 100);

            typeSelect.addEventListener('change', function () {
                if (this.value) {
                    this.classList.add('border-green-500');
                    // Показываем описание типа
                    showTypeDescription(this.value);
                } else {
                    this.classList.remove('border-green-500');
                }
            });
        }

        // Счетчик символов для текстовых полей
        const textareas = form.querySelectorAll('textarea');
        textareas.forEach(function (textarea) {
            const counter = textarea.parentNode.querySelector('.char-counter');
            if (counter) {
                updateCharCounter(counter, textarea.value.length);

                textarea.addEventListener('input', function () {
                    updateCharCounter(counter, this.value.length);
                });
            }
        });

        // Анимация для кнопки сохранения
        const saveBtn = document.querySelector('.component-save-btn');
        if (saveBtn) {
            saveBtn.addEventListener('mouseenter', function () {
                this.style.transform = 'translateY(-2px)';
            });
            saveBtn.addEventListener('mouseleave', function () {
                this.style.transform = 'translateY(0)';
            });
        }

        // Анимация для кнопки отмены
        const cancelBtn = document.querySelector('.cancel-btn');
        if (cancelBtn) {
            cancelBtn.addEventListener('mouseenter', function () {
                this.style.transform = 'translateX(-5px)';
            });
            cancelBtn.addEventListener('mouseleave', function () {
                this.style.transform = 'translateX(0)';
            });
        }

        // Подсветка измененных полей
        const originalValues = {
            name: '@Model.Name',
            type: '@Model.Type',
            price: '@Model.Price',
            quantity: '@Model.Quantity',
            description: '@Model.Description',
            specifications: '@Model.Specifications'
        };

        inputs.forEach(function (input) {
            input.addEventListener('input', function () {
                const fieldName = this.getAttribute('asp-for') || this.name;
                const originalValue = originalValues[fieldName.toLowerCase()];

                if (this.value !== originalValue) {
                    this.classList.add('border-yellow-500');
                } else {
                    this.classList.remove('border-yellow-500');
                }
            });
        });
    }

    // Анимация для иконки
    const editIcon = document.querySelector('.component-edit-icon');
    if (editIcon) {
        editIcon.style.animation = 'bounceIn 1s ease-in-out';
    }

    // Анимация для блока текущих значений
    const currentValues = document.querySelector('.current-values');
    if (currentValues) {
        currentValues.addEventListener('mouseenter', function () {
            this.style.transform = 'scale(1.02)';
        });
        currentValues.addEventListener('mouseleave', function () {
            this.style.transform = 'scale(1)';
        });
    }

    function showComponentFieldError(field, message) {
        // Убираем предыдущее сообщение об ошибке
        const existingError = field.parentNode.querySelector('.field-error');
        if (existingError) {
            existingError.remove();
        }

        const errorElement = document.createElement('div');
        errorElement.className = 'field-error text-red-400 text-sm mt-1';
        errorElement.textContent = message;
        field.parentNode.appendChild(errorElement);
    }

    function updateCharCounter(counter, length) {
        counter.textContent = length + ' символов';
        if (length > 500) {
            counter.classList.add('text-yellow-400');
        } else {
            counter.classList.remove('text-yellow-400');
        }
    }

    function showTypeDescription(type) {
        const typeDescriptions = {
            'CPU': 'Центральный процессор - основной вычислительный компонент компьютера',
            'GPU': 'Графический процессор - обработка графики и визуальных вычислений',
            'RAM': 'Оперативная память - временное хранение данных для быстрого доступа',
            'SSD': 'Твердотельный накопитель - быстрое энергонезависимое хранилище',
            'HDD': 'Жесткий диск - магнитное хранилище данных большой емкости',
            'MB': 'Материнская плата - основная плата для соединения компонентов',
            'PSU': 'Блок питания - обеспечение электроэнергией всех компонентов',
            'Case': 'Корпус - защита и охлаждение компонентов компьютера'
        };

        // Можно добавить отображение описания типа, например, в tooltip или отдельном блоке
        console.log(typeDescriptions[type] || 'Комплектующее для компьютера');
    }
}

// Инициализация при загрузке документа
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице редактирования комплектующего
    const editTitle = document.querySelector('h1');
    if (editTitle && editTitle.textContent.includes('Редактировать комплектующее')) {
        initComponentEditPage();
    }
});



// Функция расчета цены компьютера
function calculatePrice() {
    let total = 0;
    let selectedCount = 0;

    // Суммируем цены выбранных комплектующих
    document.querySelectorAll('.component-checkbox:checked').forEach(checkbox => {
        const label = checkbox.closest('label');
        const priceElement = label.querySelector('.text-primary');
        if (priceElement) {
            const priceText = priceElement.textContent;
            const price = parseFloat(priceText.replace(/[^\d.,]/g, '').replace(',', '.'));
            total += price;
            selectedCount++;
        }
    });

    // Добавляем 10%
    const markup = total * 0.1;
    const finalPrice = total + markup;

    // Обновляем отображение расчета
    const calculationElement = document.getElementById('price-calculation');
    if (calculationElement) {
        if (selectedCount > 0) {
            calculationElement.innerHTML = `
                <div class="text-primary text-sm">
                    <strong>Расчет цены:</strong><br>
                    Выбрано комплектующих: ${selectedCount} шт.<br>
                    Сумма комплектующих: ${total.toFixed(2)} ₽<br>
                    Наценка 10%: ${markup.toFixed(2)} ₽<br>
                    <strong class="text-lg">Итоговая цена: ${finalPrice.toFixed(2)} ₽</strong>
                </div>
            `;
        } else {
            calculationElement.innerHTML = `
                <div class="text-primary text-sm">
                    <strong>Расчет цены:</strong><br>
                    Выберите комплектующие для расчета стоимости
                </div>
            `;
        }
    }
}

// Инициализация страницы создания компьютера
function initComputerCreatePage() {
    const checkboxes = document.querySelectorAll('.component-checkbox');
    checkboxes.forEach(checkbox => {
        checkbox.addEventListener('change', calculatePrice);
    });

    // Первоначальный расчет
    calculatePrice();

    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.bg-dark-card').forEach((el) => {
        observer.observe(el);
    });
}

// Запуск при загрузке DOM
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице создания компьютера
    if (document.querySelector('.component-checkbox')) {
        initComputerCreatePage();
    }
});



// Функции для страницы удаления компьютера
function initComputerDeletePage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.bg-dark-card').forEach((el) => {
        observer.observe(el);
    });

    // Подтверждение при отправке формы
    const form = document.querySelector('form');
    const deleteButton = document.querySelector('.delete-computer-btn');

    if (form && deleteButton) {
        form.addEventListener('submit', function (e) {
            const computerName = '@Model.Name';
            const isConfirmed = confirm(`Вы уверены, что хотите удалить компьютер "${computerName}"? Это действие нельзя отменить.`);
            if (!isConfirmed) {
                e.preventDefault();
            }
        });

        // Дополнительное подтверждение при клике на кнопку
        deleteButton.addEventListener('click', function (e) {
            const computerName = '@Model.Name';
            const isConfirmed = confirm(`Вы уверены, что хотите удалить компьютер "${computerName}"? Это действие нельзя отменить.`);
            if (!isConfirmed) {
                e.preventDefault();
            }
        });
    }
}

// Запуск при загрузке DOM для страницы удаления
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице удаления компьютера
    if (document.querySelector('.delete-computer-btn')) {
        initComputerDeletePage();
    }
});


// Функции для страницы деталей компьютера
function initComputerDetailsPage() {
    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.bg-dark-card').forEach((el) => {
        observer.observe(el);
    });

    // Добавляем hover эффекты для строк таблицы
    const tableRows = document.querySelectorAll('tbody tr');
    tableRows.forEach(row => {
        row.addEventListener('mouseenter', function () {
            this.style.transform = 'translateX(4px)';
        });
        row.addEventListener('mouseleave', function () {
            this.style.transform = 'translateX(0)';
        });
    });

    // Плавное появление изображения
    const images = document.querySelectorAll('img');
    images.forEach(img => {
        img.addEventListener('load', function () {
            this.classList.add('image-loaded');
        });
    });
}

// Запуск при загрузке DOM для страницы деталей
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице деталей компьютера
    if (document.querySelector('table') && document.querySelector('.bg-dark-card')) {
        initComputerDetailsPage();
    }
});


// Функции для страницы редактирования компьютера
function calculatePrice() {
    let total = 0;
    let selectedCount = 0;

    // Суммируем цены выбранных комплектующих
    document.querySelectorAll('.component-checkbox:checked').forEach(checkbox => {
        const label = checkbox.closest('label');
        const priceElement = label.querySelector('.text-primary');
        if (priceElement) {
            const priceText = priceElement.textContent;
            console.log('Текст цены:', priceText); // Для отладки

            // Улучшенный парсинг цены
            const priceMatch = priceText.match(/(\d+(?:[.,]\d+)?)/);
            if (priceMatch) {
                // Заменяем запятую на точку и убираем пробелы
                const priceString = priceMatch[1].replace(/\s/g, '').replace(',', '.');
                const price = parseFloat(priceString);
                console.log('Распарсенная цена:', price); // Для отладки

                if (!isNaN(price)) {
                    total += price;
                    selectedCount++;
                }
            }
        }
    });

    // Добавляем 10%
    const markup = total * 0.1;
    const finalPrice = total + markup;

    // Обновляем отображение расчета
    const calculationElement = document.getElementById('price-calculation');
    if (calculationElement) {
        if (selectedCount > 0) {
            calculationElement.innerHTML = `
                <div class="text-primary text-sm">
                    <strong>Расчет новой цены:</strong><br>
                    Выбрано комплектующих: ${selectedCount} шт.<br>
                    Сумма комплектующих: ${total.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₽<br>
                    Наценка 10%: ${markup.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₽<br>
                    <strong class="text-lg">Итоговая цена: ${finalPrice.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₽</strong>
                </div>
            `;
        } else {
            calculationElement.innerHTML = `
                <div class="text-primary text-sm">
                    <strong>Расчет новой цены:</strong><br>
                    Измените комплектующие для пересчета стоимости
                </div>
            `;
        }
    }
}

function initComputerEditPage() {
    const checkboxes = document.querySelectorAll('.component-checkbox');

    // Слушаем изменения чекбоксов
    checkboxes.forEach(checkbox => {
        checkbox.addEventListener('change', calculatePrice);

        // Добавляем визуальную обратную связь при изменении состояния
        checkbox.addEventListener('change', function () {
            const label = this.closest('label');
            if (this.checked) {
                label.classList.add('bg-primary/10', 'border', 'border-primary/20');
            } else {
                label.classList.remove('bg-primary/10', 'border', 'border-primary/20');
            }
        });
    });

    // Первоначальный расчет
    calculatePrice();

    // Анимация появления элементов
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-fade-in');
            }
        });
    }, observerOptions);

    document.querySelectorAll('.bg-dark-card').forEach((el) => {
        observer.observe(el);
    });

    // Валидация формы
    const form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function (e) {
            const nameInput = document.querySelector('#Name');
            const quantityInput = document.querySelector('#Quantity');
            let isValid = true;

            // Проверка названия
            if (!nameInput.value.trim()) {
                isValid = false;
                nameInput.classList.add('border-red-500');
            } else {
                nameInput.classList.remove('border-red-500');
            }

            // Проверка количества
            if (!quantityInput.value || parseInt(quantityInput.value) < 0) {
                isValid = false;
                quantityInput.classList.add('border-red-500');
            } else {
                quantityInput.classList.remove('border-red-500');
            }

            if (!isValid) {
                e.preventDefault();
                alert('Пожалуйста, заполните все обязательные поля корректно.');
            }
        });
    }
}

// Запуск при загрузке DOM для страницы редактирования
document.addEventListener('DOMContentLoaded', function () {
    // Проверяем, находимся ли мы на странице редактирования компьютера
    if (document.querySelector('.component-checkbox') && document.getElementById('price-calculation')) {
        initComputerEditPage();
    }
});


