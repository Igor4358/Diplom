using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
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
        // Список всех товаров на складе
        [HttpGet]
        public async Task<IActionResult> AllProducts(string search = "")
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            var stocks = await _db.Stocks
                .Include(s => s.Product)
                .Include(s => s.Cell)
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0)
                .ToListAsync();

            if (!string.IsNullOrEmpty(search))
            {
                stocks = stocks.Where(s =>
                    s.Product!.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    s.Product!.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    s.Cell!.Address.Contains(search, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            ViewBag.Search = search;
            return View(stocks);
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

        // Список ожидаемых поставок
        [HttpGet]
        public async Task<IActionResult> ExpectedShipments()
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            var shipments = await _db.ExpectedShipments
                .Include(s => s.Product)
                .Where(s => s.Status != "Completed")
                .OrderBy(s => s.ExpectedDate)
                .ToListAsync();

            return View(shipments);
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
        // Экспорт остатков в Excel
        [HttpGet]
        [Obsolete]
        public async Task<IActionResult> ExportStockToExcel()
        {

            ExcelPackage.License.SetNonCommercialPersonal("Иван Петров");

            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Получаем все товары на складе
            var stocks = await _db.Stocks
                .Include(s => s.Product)
                .Include(s => s.Cell)
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0)
                .OrderBy(s => s.Cell!.Address)
                .ToListAsync();

            // Создаём Excel файл
            using var package = new OfficeOpenXml.ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Остатки на складе");

            // Заголовки
            worksheet.Cells[1, 1].Value = "Ячейка";
            worksheet.Cells[1, 2].Value = "Артикул";
            worksheet.Cells[1, 3].Value = "Наименование товара";
            worksheet.Cells[1, 4].Value = "Количество";
            worksheet.Cells[1, 5].Value = "Описание";

            // Стиль заголовков
            using (var range = worksheet.Cells[1, 1, 1, 5])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            // Заполняем данные
            int row = 2;
            foreach (var stock in stocks)
            {
                worksheet.Cells[row, 1].Value = stock.Cell?.Address ?? "неизвестно";
                worksheet.Cells[row, 2].Value = stock.Product?.Sku ?? "";
                worksheet.Cells[row, 3].Value = stock.Product?.Name ?? "";
                worksheet.Cells[row, 4].Value = stock.Quantity;
                worksheet.Cells[row, 5].Value = stock.Product?.Description ?? "";
                row++;
            }

            // Автоширина колонок
            worksheet.Cells.AutoFitColumns();

            // Информация о дате выгрузки
            worksheet.Cells[row + 1, 1].Value = $"Дата выгрузки: {DateTime.Now:dd.MM.yyyy HH:mm}";
            worksheet.Cells[row + 1, 1, row + 1, 5].Merge = true;

            // Возвращаем файл
            var bytes = package.GetAsByteArray();
            var fileName = $"Остатки_склада_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
        // Дашборд с графиками
        [HttpGet]
        public async Task<IActionResult> WarehouseDashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            if (userId == null) return RedirectToAction("Login");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Общее количество ячеек на складе
            var totalCells = await _db.Cells.CountAsync(c => c.WarehouseId == warehouseId);

            // Количество занятых ячеек (где есть товар)
            var occupiedCells = await _db.Stocks
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0)
                .Select(s => s.CellId)
                .Distinct()
                .CountAsync();

            // Количество пустых ячеек
            var emptyCells = totalCells - occupiedCells;

            // Загруженность в процентах
            var occupancyPercent = totalCells > 0 ? (occupiedCells * 100 / totalCells) : 0;

            // Топ-5 самых заполненных ячеек
            var topCells = await _db.Stocks
                .Include(s => s.Cell)
                .Include(s => s.Product)
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0)
                .GroupBy(s => new { s.CellId, s.Cell!.Address })
                .Select(g => new { CellAddress = g.Key.Address, TotalQuantity = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5)
                .ToListAsync();

            // Общее количество товаров на складе
            var totalProducts = await _db.Stocks
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId)
                .SumAsync(s => s.Quantity);

            ViewBag.TotalCells = totalCells;
            ViewBag.OccupiedCells = occupiedCells;
            ViewBag.EmptyCells = emptyCells;
            ViewBag.OccupancyPercent = occupancyPercent;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.TopCells = topCells;

            return View();
        }


        // История операций (логирование)
        [HttpGet]
        public async Task<IActionResult> OperationLogs()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var logs = await _db.OperationLogs
                .OrderByDescending(l => l.CreatedAt)
                .Take(100)  // Последние 100 записей
                .ToListAsync();

            return View(logs);
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
        // Приёмка по накладной (выбор накладной)
        [HttpGet]
        public async Task<IActionResult> ReceiveByInvoice()
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            var shipments = await _db.ExpectedShipments
                .Include(s => s.Product)
                .Where(s => s.WarehouseId == warehouseId && s.Status != "Completed")
                .OrderBy(s => s.ExpectedDate)
                .ToListAsync();
            // Добавьте эту строку для отладки:
            Console.WriteLine($"Найдено накладных для склада {warehouseId}: {shipments.Count}");

            return View(shipments);
        }

        // Выбрали накладную - начинаем приёмку
        [HttpGet]
        public async Task<IActionResult> ReceiveShipment(int shipmentId)
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            var shipment = await _db.ExpectedShipments
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.Id == shipmentId && s.WarehouseId == warehouseId);

            if (shipment == null) return NotFound();

            HttpContext.Session.SetInt32("CurrentShipmentId", shipment.Id);
            HttpContext.Session.SetInt32("ShipmentProductId", shipment.ProductId);
            HttpContext.Session.SetString("ShipmentProductName", shipment.Product?.Name);
            HttpContext.Session.SetInt32("ShipmentExpectedQty", shipment.ExpectedQuantity);
            HttpContext.Session.SetInt32("ShipmentReceivedQty", shipment.ReceivedQuantity);

            ViewBag.ProductName = shipment.Product?.Name;
            ViewBag.ExpectedQuantity = shipment.ExpectedQuantity;
            ViewBag.ReceivedQuantity = shipment.ReceivedQuantity;
            ViewBag.RemainingQuantity = shipment.ExpectedQuantity - shipment.ReceivedQuantity;

            return View();
        }

        // Сканирование товара и ввод количества для приёмки
        [HttpPost]
        public async Task<IActionResult> ReceiveShipment(string scannedSku, int receivedQuantity)
        {
            var shipmentId = HttpContext.Session.GetInt32("CurrentShipmentId");
            var productId = HttpContext.Session.GetInt32("ShipmentProductId");
            var productName = HttpContext.Session.GetString("ShipmentProductName");
            var expectedQty = HttpContext.Session.GetInt32("ShipmentExpectedQty") ?? 0;
            var receivedSoFar = HttpContext.Session.GetInt32("ShipmentReceivedQty") ?? 0;
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");

            if (shipmentId == null || productId == null) return RedirectToAction("ReceiveByInvoice");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Проверяем, что сканируют правильный товар
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Sku == scannedSku);
            if (product == null || product.Id != productId)
            {
                ViewBag.Error = $"Неверный товар! Ожидается: {productName}";
                ViewBag.ProductName = productName;
                ViewBag.ExpectedQuantity = expectedQty;
                ViewBag.ReceivedQuantity = receivedSoFar;
                ViewBag.RemainingQuantity = expectedQty - receivedSoFar;
                return View();
            }

            // Проверяем, что количество не превышает остаток
            int remaining = expectedQty - receivedSoFar;
            if (receivedQuantity > remaining)
            {
                ViewBag.Error = $"Слишком много! Осталось принять: {remaining} шт";
                ViewBag.ProductName = productName;
                ViewBag.ExpectedQuantity = expectedQty;
                ViewBag.ReceivedQuantity = receivedSoFar;
                ViewBag.RemainingQuantity = remaining;
                return View();
            }

            // Обновляем поставку
            var shipment = await _db.ExpectedShipments.FindAsync(shipmentId);
            if (shipment != null)
            {
                shipment.ReceivedQuantity += receivedQuantity;

                if (shipment.ReceivedQuantity >= shipment.ExpectedQuantity)
                    shipment.Status = "Completed";
                else if (shipment.ReceivedQuantity > 0)
                    shipment.Status = "Partial";

                await _db.SaveChangesAsync();
            }

            // Добавляем товар на склад (в свободную ячейку)
            var freeCells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId)
                .ToListAsync();

            var targetCell = freeCells.FirstOrDefault();
            if (targetCell != null)
            {
                var existingStock = await _db.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.CellId == targetCell.Id);

                if (existingStock != null)
                    existingStock.Quantity += receivedQuantity;
                else
                {
                    _db.Stocks.Add(new Stock
                    {
                        ProductId = productId.Value,
                        CellId = targetCell.Id,
                        Quantity = receivedQuantity
                    });
                }
                await _db.SaveChangesAsync();
            }

            // Логируем операцию
            if (_db.OperationLogs != null)
            {
                _db.OperationLogs.Add(new OperationLog
                {
                    UserId = userId ?? 0,
                    UserName = userName ?? "Неизвестный",
                    OperationType = "Receiving",
                    Details = $"Принят товар \"{productName}\" по накладной {shipment?.InvoiceNumber} в количестве {receivedQuantity} шт"
                });
                await _db.SaveChangesAsync();
            }

            // Проверяем, завершена ли поставка
            var updatedShipment = await _db.ExpectedShipments.FindAsync(shipmentId);
            if (updatedShipment?.Status == "Completed")
            {
                TempData["Success"] = $"✅ Поставка \"{productName}\" полностью принята!";
                HttpContext.Session.Remove("CurrentShipmentId");
                HttpContext.Session.Remove("ShipmentProductId");
                HttpContext.Session.Remove("ShipmentProductName");
                HttpContext.Session.Remove("ShipmentExpectedQty");
                HttpContext.Session.Remove("ShipmentReceivedQty");
                return RedirectToAction("ReceiveByInvoice");
            }

            // Обновляем сессию
            HttpContext.Session.SetInt32("ShipmentReceivedQty", (receivedSoFar + receivedQuantity));

            TempData["Success"] = $"✅ Принято {receivedQuantity} шт. Осталось: {expectedQty - (receivedSoFar + receivedQuantity)} шт";

            return RedirectToAction("ReceiveShipment", new { shipmentId });
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
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");
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

            if (_db.OperationLogs != null)
            {
                var cell = await _db.Cells.FindAsync(cellId);
                _db.OperationLogs.Add(new OperationLog
                {
                    UserId = userId ?? 0,
                    UserName = userName ?? "Неизвестный",
                    OperationType = "Receiving",
                    Details = $"Принят товар \"{productName}\" в количестве {quantity} шт в ячейку {cell?.Address ?? cellId.ToString()}"
                });
                await _db.SaveChangesAsync();
            }

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
            var fromCell = await _db.Cells.FindAsync(fromCellId);
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");
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

            _db.OperationLogs.Add(new OperationLog
            {
                UserId = userId ?? 0,
                UserName = HttpContext.Session.GetString("UserName") ?? "Неизвестный",
                OperationType = "Sorting",
                Details = $"Перемещён товар \"{productName}\" в количестве {moveQuantity} шт из ячейки {fromCell?.Address} в ячейку {targetCell.Address}"
            });
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
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");

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

            _db.OperationLogs.Add(new OperationLog
            {
                UserId = userId ?? 0,
                UserName = HttpContext.Session.GetString("UserName") ?? "Неизвестный",
                OperationType = "Picking",
                Details = $"Собран товар \"{task.Product?.Name}\" в количестве {pickedQuantity} шт из ячейки {task.Cell?.Address}"
            });
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
