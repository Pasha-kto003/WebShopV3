using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebShopV3.Json.Services;
using WebShopV3.Json.Models;

namespace WebShopV3.Json.Controllers
{
    [Authorize(Roles = "Админ")]
    public class UserController : Controller
    {
        private readonly JsonDataService _jsonData;

        public UserController(JsonDataService jsonData)
        {
            _jsonData = jsonData;
        }

        // GET: User
        public async Task<IActionResult> Index()
        {
            var users = await GetUsersWithUserTypes();
            return View(users);
        }

        // GET: User/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await GetUserWithUserType(id.Value);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: User/Create
        public async Task<IActionResult> Create()
        {
            await LoadUserTypes();
            return View();
        }

        // POST: User/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            await LoadUserTypes();

            if (ModelState.IsValid)
            {
                await _jsonData.CreateAsync("Users", user);
                TempData["SuccessMessage"] = $"Пользователь {user.Username} создан";
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }

        // GET: User/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _jsonData.GetByIdAsync<User>("Users", id.Value);
            if (user == null)
            {
                return NotFound();
            }

            await LoadUserTypes();
            return View(user);
        }

        // POST: User/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            try
            {
                // При редактировании обновляем все поля, включая пароль
                var success = await _jsonData.UpdateAsync("Users", user);
                if (!success)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = $"Пользователь {user.Username} обновлен";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                if (!await UserExists(user.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // GET: User/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await GetUserWithUserType(id.Value);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: User/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var success = await _jsonData.DeleteAsync<User>("Users", id);
            if (!success)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = "Пользователь удален";
            return RedirectToAction(nameof(Index));
        }

        // GET: User/ChangeRole/5 - Изменение роли пользователя
        public async Task<IActionResult> ChangeRole(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await GetUserWithUserType(id.Value);
            if (user == null)
            {
                return NotFound();
            }

            await LoadUserTypes();
            return View(user);
        }

        // POST: User/ChangeRole/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(int id, int userTypeId)
        {
            try
            {
                var user = await _jsonData.GetByIdAsync<User>("Users", id);
                if (user == null)
                {
                    return NotFound();
                }

                user.UserTypeId = userTypeId;
                var success = await _jsonData.UpdateAsync("Users", user);
                if (!success)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Роль пользователя изменена";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при изменении роли: {ex.Message}";
                return RedirectToAction(nameof(ChangeRole), new { id });
            }
        }

        // GET: User/ResetPassword/5 - Сброс пароля
        public async Task<IActionResult> ResetPassword(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await GetUserWithUserType(id.Value);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: User/ResetPassword/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            try
            {
                var user = await _jsonData.GetByIdAsync<User>("Users", id);
                if (user == null)
                {
                    return NotFound();
                }

                // Здесь должна быть логика хеширования пароля
                // user.PasswordHash = HashPassword(newPassword);

                // Временно просто сохраняем как есть (в реальном приложении нужно хешировать!)
                if (!string.IsNullOrEmpty(newPassword))
                {
                    user.PasswordHash = newPassword;
                }

                var success = await _jsonData.UpdateAsync("Users", user);
                if (!success)
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "Пароль пользователя сброшен";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Ошибка при сбросе пароля: {ex.Message}";
                return RedirectToAction(nameof(ResetPassword), new { id });
            }
        }

        // GET: User/Statistics - Статистика пользователей
        public async Task<IActionResult> Statistics()
        {
            var statistics = await GetUserStatistics();
            return View(statistics);
        }

        private async Task<bool> UserExists(int id)
        {
            var user = await _jsonData.GetByIdAsync<User>("Users", id);
            return user != null;
        }

        private async Task LoadUserTypes()
        {
            var userTypes = await _jsonData.GetAllAsync<UserType>("UserTypes");
            ViewBag.UserTypes = userTypes;
        }

        // Вспомогательные методы

        private async Task<List<User>> GetUsersWithUserTypes()
        {
            var users = await _jsonData.GetAllAsync<User>("Users");
            var userTypes = await _jsonData.GetAllAsync<UserType>("UserTypes");

            foreach (var user in users)
            {
                user.UserType = userTypes.FirstOrDefault(ut => ut.Id == user.UserTypeId);
            }

            return users;
        }

        private async Task<User?> GetUserWithUserType(int userId)
        {
            var user = await _jsonData.GetByIdAsync<User>("Users", userId);
            if (user == null) return null;

            var userTypes = await _jsonData.GetAllAsync<UserType>("UserTypes");
            user.UserType = userTypes.FirstOrDefault(ut => ut.Id == user.UserTypeId);

            return user;
        }

        private async Task<UserStatisticsViewModel> GetUserStatistics()
        {
            var users = await _jsonData.GetAllAsync<User>("Users");
            var userTypes = await _jsonData.GetAllAsync<UserType>("UserTypes");

            return new UserStatisticsViewModel
            {
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.LastLoginAt.HasValue &&
                    u.LastLoginAt.Value > DateTime.Now.AddDays(-30)),
                NewUsersThisMonth = users.Count(u => u.CreatedAt > DateTime.Now.AddDays(-30)),
                UsersByType = userTypes
                    .Select(ut => new UserTypeStatistic
                    {
                        UserTypeName = ut.Name,
                        Count = users.Count(u => u.UserTypeId == ut.Id)
                    })
                    .ToList()
            };
        }
    }

    // ViewModel для статистики
    public class UserStatisticsViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; } // Активные за последние 30 дней
        public int NewUsersThisMonth { get; set; }
        public List<UserTypeStatistic> UsersByType { get; set; } = new();
    }

    public class UserTypeStatistic
    {
        public string UserTypeName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // ViewModel для изменения пароля
    public class ResetPasswordViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}