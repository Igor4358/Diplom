using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Terminal.Data;
using WMS.Terminal.Models;

namespace WMS.Terminal.Controllers
{
    public class TerminalController : Controller
    {
        private readonly AppDbContext _db;

        public TerminalController(AppDbContext db)
        {
            _db = db;
        }

        // Страница логина (GET)
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // Обработка логина (POST)
        [HttpPost]
        public async Task<IActionResult> Login(string pinCode)
        {
            if (string.IsNullOrEmpty(pinCode))
            {
                ViewBag.Error = "Введите PIN-код";
                return View();
            }

            // Ищем пользователя по PIN
            var user = await _db.Users
                .Include(u => u.CurrentWarehouse)
                .FirstOrDefaultAsync(u => u.PinCode == pinCode);

            if (user == null)
            {
                ViewBag.Error = "Неверный PIN-код";
                return View();
            }

            // Сохраняем ID пользователя в сессии
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);

            // Если у пользователя нет выбранного склада, отправим на выбор
            if (user.CurrentWarehouseId == 0)
            {
                return RedirectToAction("SelectWarehouse");
            }

            return RedirectToAction("MainMenu");
        }

        // Страница выбора склада
        [HttpGet]
        public async Task<IActionResult> SelectWarehouse()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var warehouses = await _db.Warehouses.ToListAsync();
            return View(warehouses);
        }

        // Обработка выбора склада
        [HttpPost]
        public async Task<IActionResult> SelectWarehouse(int warehouseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var user = await _db.Users.FindAsync(userId);
            var warehouse = await _db.Warehouses.FindAsync(warehouseId);

            if (user != null && warehouse != null)
            {
                user.CurrentWarehouseId = warehouseId;
                await _db.SaveChangesAsync();

                // Сохраняем в сессию
                HttpContext.Session.SetInt32("WarehouseId", warehouseId);
                HttpContext.Session.SetString("WarehouseName", warehouse.Name);
            }

            return RedirectToAction("MainMenu");
        }

        // Главное меню терминала
        [HttpGet]
        public IActionResult MainMenu()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            var warehouseName = HttpContext.Session.GetString("WarehouseName");

            if (userId == null || string.IsNullOrEmpty(userName))
            {
                return RedirectToAction("Login");
            }

            ViewBag.UserName = userName;
            ViewBag.WarehouseId = warehouseName ?? (warehouseId?.ToString() ?? "не выбран");
            return View();
        }

        // Выход (разлогин)
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
        // Список заказов для сборки
        [HttpGet]
        public async Task<IActionResult> PickingOrders()
        {
            // Проверяем, есть ли пользователь в сессии
            var userId = HttpContext.Session.GetInt32("UserId");
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            if (warehouseId == null)
            {
                // Если склад не выбран, возвращаем на выбор
                return RedirectToAction("SelectWarehouse");
            }

            // Получаем задания, которые ещё не выполнены
            var tasks = await _db.PickingTasks
                .Include(t => t.Order)
                .Include(t => t.Product)
                .Include(t => t.Cell)
                .Where(t => t.Status != "Completed" && t.Cell != null && t.Cell.WarehouseId == warehouseId)
                .ToListAsync();

            // Группируем по заказам
            var orders = tasks
                .Where(t => t.Order != null)
                .GroupBy(t => new { t.OrderId, t.Order!.OrderNumber })
                .Select(g => new { g.Key.OrderId, g.Key.OrderNumber, Items = g.ToList() })
                .ToList();

            // Возвращаем как dynamic, чтобы View могла обработать
            return View(orders as IEnumerable<dynamic>);
        }


        // Начать сборку конкретного заказа
        [HttpGet]
        public async Task<IActionResult> StartPicking(int orderId)
        {
            var tasks = await _db.PickingTasks
                .Include(t => t.Product)
                .Include(t => t.Cell)
                .Where(t => t.OrderId == orderId && t.Status != "Completed")
                .ToListAsync();

            if (!tasks.Any())
            {
                return RedirectToAction("PickingOrders");
            }

            // Сохраняем в сессии текущий заказ
            HttpContext.Session.SetInt32("CurrentOrderId", orderId);

            return View(tasks);
        }

        // Сканирование ячейки (первый шаг сборки)
        [HttpGet]
        public async Task<IActionResult> ScanCell(int taskId)
        {
            var task = await _db.PickingTasks
                .Include(t => t.Cell)
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            ViewBag.TaskId = task.Id;
            ViewBag.ExpectedCell = task.Cell?.Address;
            ViewBag.ProductName = task.Product?.Name;
            ViewBag.ProductSku = task.Product?.Sku;
            ViewBag.RequiredQty = task.RequiredQuantity;

            return View();
        }

        // Обработка сканирования ячейки
        [HttpPost]
        public async Task<IActionResult> ScanCell(int taskId, string scannedCell)
        {
            var task = await _db.PickingTasks
                .Include(t => t.Cell)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            // Проверяем, правильная ли ячейка
            if (task.Cell?.Address != scannedCell)
            {
                ViewBag.Error = $"Неверная ячейка! Нужно: {task.Cell?.Address}, Вы отсканировали: {scannedCell}";
                ViewBag.TaskId = task.Id;
                ViewBag.ExpectedCell = task.Cell?.Address;
                return View();
            }

            // Переходим к сканированию товара
            return RedirectToAction("ScanProduct", new { taskId });
        }

        // Сканирование товара
        [HttpGet]
        public async Task<IActionResult> ScanProduct(int taskId)
        {
            var task = await _db.PickingTasks
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            ViewBag.TaskId = task.Id;
            ViewBag.ExpectedSku = task.Product?.Sku;
            ViewBag.ProductName = task.Product?.Name;
            ViewBag.RequiredQty = task.RequiredQuantity;

            return View();
        }

        // Обработка сканирования товара
        [HttpPost]
        public async Task<IActionResult> ScanProduct(int taskId, string scannedSku)
        {
            var task = await _db.PickingTasks
                .Include(t => t.Product)
                .Include(t => t.Order)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            // Проверяем, правильный ли товар
            if (task.Product?.Sku != scannedSku)
            {
                ViewBag.Error = $"Неверный товар! Нужен: {task.Product?.Sku}";
                ViewBag.TaskId = task.Id;
                ViewBag.ExpectedSku = task.Product?.Sku;
                ViewBag.ProductName = task.Product?.Name;
                ViewBag.RequiredQty = task.RequiredQuantity;
                return View();
            }

            // Переходим к подтверждению количества
            return RedirectToAction("ConfirmPick", new { taskId });
        }

        // Подтверждение количества и завершение сборки позиции
        [HttpGet]
        public async Task<IActionResult> ConfirmPick(int taskId)
        {
            var task = await _db.PickingTasks
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            ViewBag.TaskId = task.Id;
            ViewBag.ProductName = task.Product?.Name;
            ViewBag.RequiredQty = task.RequiredQuantity;

            return View();
        }

        // Страница приёмки товара
        [HttpGet]
        public async Task<IActionResult> Receiving()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            if (userId == null) return RedirectToAction("Login");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            return View();
        }

        // Обработка сканирования товара при приёмке
        [HttpPost]
        public async Task<IActionResult> Receiving(string scannedSku)
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            if (string.IsNullOrEmpty(scannedSku))
            {
                ViewBag.Error = "Отсканируйте штрих-код товара";
                return View();
            }

            // Ищем товар в базе
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Sku == scannedSku);

            if (product == null)
            {
                ViewBag.Error = $"Товар с артикулом {scannedSku} не найден в базе";
                return View();
            }

            // Сохраняем товар в сессию для следующего шага
            HttpContext.Session.SetInt32("ReceivingProductId", product.Id);
            HttpContext.Session.SetString("ReceivingProductName", product.Name);
            HttpContext.Session.SetString("ReceivingProductSku", product.Sku);

            // Переходим к выбору ячейки
            return RedirectToAction("SelectCellForReceiving");
        }

        // Выбор ячейки для размещения товара
        [HttpGet]
        public async Task<IActionResult> SelectCellForReceiving()
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            var productId = HttpContext.Session.GetInt32("ReceivingProductId");
            var productName = HttpContext.Session.GetString("ReceivingProductName");

            if (warehouseId == null) return RedirectToAction("SelectWarehouse");
            if (productId == null) return RedirectToAction("Receiving");

            // Находим свободные ячейки (где нет товара или есть место)
            var occupiedCellIds = await _db.Stocks.Select(s => s.CellId).ToListAsync();

            var freeCells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId && !occupiedCellIds.Contains(c.Id))
                .ToListAsync();

            // Если нет полностью свободных, показываем ячейки с небольшим количеством
            if (!freeCells.Any())
            {
                freeCells = await _db.Cells
                    .Where(c => c.WarehouseId == warehouseId)
                    .Take(5)
                    .ToListAsync();
            }

            ViewBag.ProductName = productName;
            ViewBag.ProductSku = HttpContext.Session.GetString("ReceivingProductSku");

            return View(freeCells);
        }

        // Подтверждение размещения в выбранную ячейку
        [HttpPost]
        public async Task<IActionResult> ConfirmReceiving(int cellId, int quantity)
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            var productId = HttpContext.Session.GetInt32("ReceivingProductId");
            var productName = HttpContext.Session.GetString("ReceivingProductName");

            if (warehouseId == null) return RedirectToAction("SelectWarehouse");
            if (productId == null) return RedirectToAction("Receiving");

            // Проверяем, есть ли уже этот товар в этой ячейке
            var existingStock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.CellId == cellId);

            if (existingStock != null)
            {
                existingStock.Quantity += quantity;
            }
            else
            {
                var newStock = new Stock
                {
                    ProductId = productId.Value,
                    CellId = cellId,
                    Quantity = quantity
                };
                _db.Stocks.Add(newStock);
            }

            await _db.SaveChangesAsync();

            // Очищаем сессию
            HttpContext.Session.Remove("ReceivingProductId");
            HttpContext.Session.Remove("ReceivingProductName");
            HttpContext.Session.Remove("ReceivingProductSku");

            TempData["Success"] = $"Товар \"{productName}\" в количестве {quantity} шт принят на склад";

            return RedirectToAction("Receiving");
        }
        // Страница сортировки - выбор товара для перемещения
        [HttpGet]
        public async Task<IActionResult> Sorting()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            if (userId == null) return RedirectToAction("Login");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            return View();
        }

        // Поиск товара по штрих-коду для сортировки
        [HttpPost]
        public async Task<IActionResult> FindProductForSorting(string scannedSku)
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            if (string.IsNullOrEmpty(scannedSku))
            {
                ViewBag.Error = "Отсканируйте штрих-код товара";
                return View("Sorting");
            }

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Sku == scannedSku);
            if (product == null)
            {
                ViewBag.Error = $"Товар с артикулом {scannedSku} не найден";
                return View("Sorting");
            }

            // Ищем текущее местоположение товара
            var currentStock = await _db.Stocks
                .Include(s => s.Cell)
                .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.Cell != null && s.Cell.WarehouseId == warehouseId);

            if (currentStock == null || currentStock.Quantity <= 0)
            {
                ViewBag.Error = $"Товар \"{product.Name}\" не найден на складе";
                return View("Sorting");
            }
            Console.WriteLine($"DEBUG: Quantity = {currentStock.Quantity}");
            // Сохраняем в сессию
            HttpContext.Session.SetInt32("MovingProductId", product.Id);
            HttpContext.Session.SetString("MovingProductName", product.Name);
            HttpContext.Session.SetString("MovingProductSku", product.Sku);
            HttpContext.Session.SetInt32("MovingFromCellId", currentStock.CellId);
            HttpContext.Session.SetInt32("MovingQuantity", currentStock.Quantity);

            // Получаем список ячеек для представления
            var cells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId && c.Id != currentStock.CellId)
                .ToListAsync();

            // Передаём данные через ViewBag и ViewData
            ViewBag.ProductName = product.Name;
            ViewBag.ProductSku = product.Sku;
            ViewBag.CurrentCell = currentStock.Cell?.Address ?? "неизвестно";
            ViewBag.Quantity = currentStock.Quantity;  // ← убедитесь, что это число

            return View("SelectTargetCell", cells);
        }

        // Выбор целевой ячейки
        [HttpGet]
        public async Task<IActionResult> SelectTargetCell()
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            var productId = HttpContext.Session.GetInt32("MovingProductId");
            var currentCellId = HttpContext.Session.GetInt32("MovingFromCellId");
            var quantity = HttpContext.Session.GetInt32("MovingQuantity");

            if (warehouseId == null) return RedirectToAction("SelectWarehouse");
            if (productId == null) return RedirectToAction("Sorting");

            // Получаем все ячейки на складе, кроме текущей
            var cells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId && c.Id != currentCellId)
                .ToListAsync();

            ViewBag.ProductName = HttpContext.Session.GetString("MovingProductName");
            ViewBag.CurrentCell = (await _db.Cells.FindAsync(currentCellId))?.Address;
            ViewBag.Quantity = quantity ?? 0;  // ← если null, то 0

            return View(cells);
        }
        // Подтверждение перемещения
        [HttpPost]
        public async Task<IActionResult> ConfirmMove(int targetCellId, int moveQuantity)
        {
            var productId = HttpContext.Session.GetInt32("MovingProductId");
            var fromCellId = HttpContext.Session.GetInt32("MovingFromCellId");
            var productName = HttpContext.Session.GetString("MovingProductName");
            var totalQuantity = HttpContext.Session.GetInt32("MovingQuantity") ?? 0;

            if (productId == null || fromCellId == null)
            {
                return RedirectToAction("Sorting");
            }

            if (moveQuantity <= 0)
            {
                TempData["SortingError"] = "Количество должно быть больше 0";
                return RedirectToAction("Sorting");
            }

            if (moveQuantity > totalQuantity)
            {
                TempData["SortingError"] = $"Нельзя переместить больше, чем есть ({totalQuantity} шт)";
                return RedirectToAction("Sorting");
            }

            // Проверяем, существует ли целевая ячейка
            var targetCell = await _db.Cells.FindAsync(targetCellId);
            if (targetCell == null)
            {
                TempData["SortingError"] = "Целевая ячейка не найдена";
                return RedirectToAction("Sorting");
            }

            // Уменьшаем количество в исходной ячейке
            var fromStock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.CellId == fromCellId);

            if (fromStock == null)
            {
                TempData["SortingError"] = "Ошибка: товар не найден в исходной ячейке";
                return RedirectToAction("Sorting");
            }

            if (moveQuantity == fromStock.Quantity)
            {
                // Перемещаем полностью - удаляем из исходной ячейки
                _db.Stocks.Remove(fromStock);
            }
            else
            {
                // Перемещаем частично - уменьшаем количество
                fromStock.Quantity -= moveQuantity;
            }

            // Добавляем в целевую ячейку
            var toStock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.CellId == targetCellId);

            if (toStock != null)
            {
                toStock.Quantity += moveQuantity;
            }
            else
            {
                toStock = new Stock
                {
                    ProductId = productId.Value,
                    CellId = targetCellId,
                    Quantity = moveQuantity
                };
                _db.Stocks.Add(toStock);
            }

            await _db.SaveChangesAsync();

            // Очищаем сессию
            HttpContext.Session.Remove("MovingProductId");
            HttpContext.Session.Remove("MovingProductName");
            HttpContext.Session.Remove("MovingProductSku");
            HttpContext.Session.Remove("MovingFromCellId");
            HttpContext.Session.Remove("MovingQuantity");

            // Сообщение об успехе
            TempData["SortingSuccess"] = $"✅ Товар \"{productName}\" в количестве {moveQuantity} шт перемещён из ячейки в ячейку {targetCell.Address}";

            return RedirectToAction("Sorting");
        }

        // Страница поиска
        [HttpGet]
        public IActionResult FindObject()
        {
            return View();
        }

        // Поиск по артикулу товара или адресу ячейки
        [HttpPost]
        public async Task<IActionResult> FindObject(string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery))
            {
                ViewBag.Error = "Введите артикул товара или адрес ячейки";
                return View();
            }

            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            // Пробуем найти как товар
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Sku == searchQuery);
            if (product != null)
            {
                var stock = await _db.Stocks
                    .Include(s => s.Cell)
                    .FirstOrDefaultAsync(s => s.ProductId == product.Id && (warehouseId == null || s.Cell!.WarehouseId == warehouseId));

                if (stock != null)
                {
                    ViewBag.ResultType = "product";
                    ViewBag.ProductName = product.Name;
                    ViewBag.ProductSku = product.Sku;
                    ViewBag.ProductDescription = product.Description;
                    ViewBag.CellAddress = stock.Cell?.Address;
                    ViewBag.Quantity = stock.Quantity;
                }
                else
                {
                    ViewBag.Error = $"Товар \"{product.Name}\" не найден на вашем складе";
                }
                return View();
            }

            // Пробуем найти как ячейку
            var cell = await _db.Cells
                .Include(c => c.Warehouse)
                .FirstOrDefaultAsync(c => c.Address == searchQuery);

            if (cell != null)
            {
                var stocksInCell = await _db.Stocks
                    .Include(s => s.Product)
                    .Where(s => s.CellId == cell.Id)
                    .ToListAsync();

                ViewBag.ResultType = "cell";
                ViewBag.CellAddress = cell.Address;
                ViewBag.Products = stocksInCell;
                return View();
            }

            ViewBag.Error = $"Ничего не найдено по запросу \"{searchQuery}\"";
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> ConfirmPick(int taskId, int pickedQuantity)
        {
            var task = await _db.PickingTasks
                .Include(t => t.Product)
                .Include(t => t.Cell)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            if (pickedQuantity != task.RequiredQuantity)
            {
                ViewBag.Error = $"Количество не совпадает! Нужно: {task.RequiredQuantity}, Вы взяли: {pickedQuantity}";
                ViewBag.TaskId = task.Id;
                ViewBag.ProductName = task.Product?.Name;
                ViewBag.RequiredQty = task.RequiredQuantity;
                return View();
            }

            // Обновляем остаток на складе
            var stock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == task.ProductId && s.CellId == task.CellId);

            if (stock != null)
            {
                stock.Quantity -= pickedQuantity;
            }

            // Отмечаем задание выполненным
            task.Status = "Completed";
            task.PickedQuantity = pickedQuantity;

            await _db.SaveChangesAsync();

            // Показываем сообщение об успехе
            TempData["Success"] = $"Товар \"{task.Product?.Name}\" собран!";

            // Возвращаемся к списку заданий текущего заказа
            var orderId = HttpContext.Session.GetInt32("CurrentOrderId");
            if (orderId.HasValue)
            {
                // Проверяем, остались ли ещё незавершённые задания
                var remainingTasks = await _db.PickingTasks
                    .AnyAsync(t => t.OrderId == orderId && t.Status != "Completed");

                if (!remainingTasks)
                {
                    TempData["Success"] = $"Заказ полностью собран!";
                    return RedirectToAction("PickingOrders");
                }
            }

            return RedirectToAction("StartPicking", new { orderId });
        }
    }
}
