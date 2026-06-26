using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using WMS.Terminal.Data;
using WMS.Terminal.Models;
using ZXing;
using ZXing.Common;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ZXing.QrCode;
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
        // Генерация штрих-кода для товара или ячейки
        [HttpGet]
        public IActionResult GenerateBarcode(string data, int width = 300, int height = 150)
        {
            if (string.IsNullOrEmpty(data))
            {
                return BadRequest("Не указаны данные для штрих-кода");
            }

            var writer = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 10
                }
            };

            var bitmap = writer.Write(data);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return File(stream.ToArray(), "image/png");
        }
        [HttpGet]
        public async Task<IActionResult> AllBarcodes()
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            var model = new BarcodeViewModel();

            // Товары на складе с уникальными штрих-кодами
            var products = await _db.Stocks
                .Include(s => s.Product)
                .Include(s => s.Cell)
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0)
                .Select(s => new BarcodeProduct
                {
                    Name = s.Product!.Name,
                    Sku = s.Product!.Sku,
                    CellAddress = s.Cell!.Address,
                    Barcode = s.Barcode ?? s.Product!.Sku // Если нет штрих-кода — используем артикул
                })
                .ToListAsync();

            model.Products = products;

            // Все ячейки склада
            var cells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId)
                .Select(c => new BarcodeCell
                {
                    Address = c.Address,
                    IsOccupied = _db.Stocks.Any(s => s.CellId == c.Id && s.Quantity > 0)
                })
                .ToListAsync();

            model.Cells = cells;

            return View(model);
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
        // Страница ожидаемых поставок
        [HttpGet]
        public async Task<IActionResult> ExpectedReceipts()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            if (userId == null) return RedirectToAction("Login");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Получаем все ожидаемые поставки для этого склада
            var receipts = await _db.ExpectedReceipts
                .Where(e => e.Status != "Completed")
                .OrderBy(e => e.ExpectedDate)
                .ToListAsync();

            return View(receipts);
        }
        // ============================================================
        //  ОЖИДАЕМЫЕ ПОСТАВКИ
        // ============================================================


        // Страница добавления новой поставки (GET)
        [HttpGet]
        public IActionResult AddExpectedReceipt()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            return View();
        }

        // Обработка добавления поставки (POST)
        [HttpPost]
        public async Task<IActionResult> AddExpectedReceipt(string barcode, string sku, string productName, int quantity, string supplier)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            if (string.IsNullOrEmpty(barcode) || string.IsNullOrEmpty(sku) || string.IsNullOrEmpty(productName))
            {
                ViewBag.Error = "Заполните все обязательные поля";
                return View();
            }

            var receipt = new ExpectedReceipt
            {
                Barcode = barcode,
                Sku = sku,
                ProductName = productName,
                ExpectedQuantity = quantity > 0 ? quantity : 1,
                ReceivedQuantity = 0,
                Status = "Pending",
                ExpectedDate = DateTime.UtcNow,
                Supplier = supplier ?? ""
            };

            _db.ExpectedReceipts.Add(receipt);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Поставка '{productName}' добавлена!";
            return RedirectToAction("ExpectedReceipts");
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

            var emptyCells = totalCells - occupiedCells;
            var occupancyPercent = totalCells > 0 ? (occupiedCells * 100 / totalCells) : 0;

            // Топ-5 самых заполненных ячеек
            var topCells = await _db.Stocks
                .Include(s => s.Cell)
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0)
                .GroupBy(s => new { s.CellId, s.Cell!.Address })
                .Select(g => new { CellId = g.Key.CellId, CellAddress = g.Key.Address, TotalQuantity = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5)
                .ToListAsync();

            var totalProducts = await _db.Stocks
                .Where(s => s.Cell != null && s.Cell.WarehouseId == warehouseId)
                .SumAsync(s => s.Quantity);

            // Получаем все ячейки для страницы "Все ячейки"
            var allCells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId)
                .Select(c => new { c.Id, c.Address })
                .ToListAsync();

            ViewBag.TotalCells = totalCells;
            ViewBag.OccupiedCells = occupiedCells;
            ViewBag.EmptyCells = emptyCells;
            ViewBag.OccupancyPercent = occupancyPercent;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.TopCells = topCells;
            ViewBag.AllCells = allCells;

            return View();
        }
        // Список всех ячеек склада
        [HttpGet]
        public async Task<IActionResult> AllCells()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            if (userId == null) return RedirectToAction("Login");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Получаем все ячейки склада с информацией о заполненности
            var cells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId)
                .Select(c => new
                {
                    c.Id,
                    c.Address,
                    c.IsLocked,
                    ProductsCount = _db.Stocks.Where(s => s.CellId == c.Id && s.Quantity > 0).Sum(s => s.Quantity)
                })
                .OrderBy(c => c.Address)
                .ToListAsync();

            return View(cells);
        }

        // Просмотр содержимого ячейки
        [HttpGet]
        public async Task<IActionResult> CellContents(string cellAddress = "")
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Если адрес не передан — просто показываем страницу с полем ввода
            if (string.IsNullOrEmpty(cellAddress))
            {
                return View(new List<Stock>());
            }

            // Ищем ячейку по адресу
            var cell = await _db.Cells
                .FirstOrDefaultAsync(c => c.Address == cellAddress && c.WarehouseId == warehouseId);

            if (cell == null)
            {
                ViewBag.Error = $"Ячейка '{cellAddress}' не найдена на складе";
                ViewBag.CellAddress = cellAddress;
                return View(new List<Stock>());
            }

            // Получаем все товары в этой ячейке
            var stocks = await _db.Stocks
                .Include(s => s.Product)
                .Where(s => s.CellId == cell.Id && s.Quantity > 0)
                .ToListAsync();

            ViewBag.CellAddress = cell.Address;
            ViewBag.CellId = cell.Id;

            if (!stocks.Any())
            {
                ViewBag.Info = "Ячейка пуста";
            }

            return View(stocks);
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

            // ============================================================
            // 1. Ищем товар на складе ПО ШТРИХ-КОДУ
            // ============================================================
            var existingStock = await _db.Stocks
                .Include(s => s.Product)
                .Include(s => s.Cell)
                .FirstOrDefaultAsync(s => s.Barcode == scannedSku && s.Quantity > 0);

            if (existingStock != null)
            {
                // Товар уже есть на складе! Показываем уведомление и останавливаем приёмку.
                TempData["Warning"] = $"⚠️ Товар '{existingStock.Product?.Name}' (арт. {existingStock.Product?.Sku}) УЖЕ ПРИНЯТ на склад!";
                TempData["WarningDetails"] = $"📍 Ячейка: {existingStock.Cell?.Address}, Количество: {existingStock.Quantity} шт";
                return RedirectToAction("Receiving");
            }

            // ============================================================
            // 2. Ищем товар в ожидаемых поставках
            // ============================================================
            var expected = await _db.ExpectedReceipts
                .FirstOrDefaultAsync(e => e.Barcode == scannedSku && e.Status != "Completed");

            if (expected != null)
            {
                // Нашли в ожидаемых поставках → принимаем
                var existingProduct = await _db.Products
                    .FirstOrDefaultAsync(p => p.Sku == expected.Sku);

                int productId;
                if (existingProduct != null)
                {
                    productId = existingProduct.Id;
                }
                else
                {
                    var newProduct = new Product
                    {
                        Sku = expected.Sku,
                        Name = expected.ProductName,
                        Description = ""
                    };
                    _db.Products.Add(newProduct);
                    await _db.SaveChangesAsync();
                    productId = newProduct.Id;
                }

                HttpContext.Session.SetInt32("ExpectedReceiptId", expected.Id);
                HttpContext.Session.SetInt32("ReceivingProductId", productId);
                HttpContext.Session.SetString("ReceivingProductName", expected.ProductName);
                HttpContext.Session.SetString("ReceivingProductSku", expected.Sku);
                HttpContext.Session.SetString("ReceivingBarcode", scannedSku);

                int remaining = expected.ExpectedQuantity - expected.ReceivedQuantity;
                HttpContext.Session.SetInt32("ReceivingQuantity", remaining);

                return RedirectToAction("SelectCellForReceiving");
            }

            // ============================================================
            // 3. Проверяем, есть ли товар с таким артикулом (если штрих-код = артикул)
            // ============================================================
            var productBySku = await _db.Products.FirstOrDefaultAsync(p => p.Sku == scannedSku);
            if (productBySku != null)
            {
                ViewBag.NewBarcode = scannedSku;
                ViewBag.ExistingProductSku = productBySku.Sku;
                ViewBag.ExistingProductName = productBySku.Name;
                ViewBag.Error = $"Товар '{productBySku.Name}' уже есть в базе, но без штрих-кода. Добавьте штрих-код к существующему товару.";
                return View("LinkBarcodeToExistingProduct");
            }

            // ============================================================
            // 4. Товар не найден → предлагаем создать новый
            // ============================================================
            ViewBag.NewBarcode = scannedSku;
            ViewBag.Error = $"Товар со штрих-кодом '{scannedSku}' не найден. Заполните форму для создания нового товара.";
            return View("CreateNewProduct");
        }
        // Страница создания нового товара (GET)
        [HttpGet]
        public IActionResult CreateNewProduct(string? barcode)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            // Если передан штрих-код, подставляем его в поле
            ViewBag.NewBarcode = barcode ?? "";
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> CompletedReceipts()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var receipts = await _db.ExpectedReceipts
                .Where(e => e.Status == "Completed")
                .OrderByDescending(e => e.ExpectedDate)
                .Take(50)
                .ToListAsync();

            return View(receipts);
        }
        [HttpPost]
        public async Task<IActionResult> LinkBarcodeToExistingProduct(string barcode, string sku, int quantity)
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Находим существующий товар
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Sku == sku);
            if (product == null)
            {
                ViewBag.Error = "Товар не найден";
                return RedirectToAction("Receiving");
            }

            // Проверяем, есть ли уже товар на складе с таким штрих-кодом
            var existingStock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.Barcode == barcode && s.Quantity > 0);

            if (existingStock != null)
            {
                TempData["Success"] = $"Товар со штрих-кодом '{barcode}' уже есть на складе! Количество увеличено.";
                existingStock.Quantity += quantity;
                await _db.SaveChangesAsync();
                return RedirectToAction("Receiving");
            }

            // Сохраняем в сессии для выбора ячейки
            HttpContext.Session.SetInt32("ReceivingProductId", product.Id);
            HttpContext.Session.SetString("ReceivingProductName", product.Name);
            HttpContext.Session.SetString("ReceivingProductSku", product.Sku);
            HttpContext.Session.SetInt32("ReceivingQuantity", quantity);
            HttpContext.Session.SetString("ReceivingBarcode", barcode);

            // Добавляем штрих-код в ExpectedReceipts для истории
            var expected = new ExpectedReceipt
            {
                Barcode = barcode,
                Sku = sku,
                ProductName = product.Name,
                ExpectedQuantity = quantity,
                ReceivedQuantity = 0,
                Status = "Pending",
                ExpectedDate = DateTime.UtcNow,
                Supplier = "Привязка штрих-кода"
            };
            _db.ExpectedReceipts.Add(expected);
            await _db.SaveChangesAsync();
            HttpContext.Session.SetInt32("ExpectedReceiptId", expected.Id);

            TempData["Success"] = $"Штрих-код '{barcode}' связан с товаром '{product.Name}'! Выберите ячейку для размещения.";
            return RedirectToAction("SelectCellForReceiving");
        }
        /*  [HttpGet]
          public async Task<IActionResult> ConfirmExistingStockReceiving()
          {
              var productName = HttpContext.Session.GetString("ReceivingProductName");
              var productSku = HttpContext.Session.GetString("ReceivingProductSku");
              var quantity = HttpContext.Session.GetInt32("ReceivingQuantity") ?? 1;
              var cellId = HttpContext.Session.GetInt32("ReceivingCellId");

              ViewBag.ProductName = productName;
              ViewBag.ProductSku = productSku;
              ViewBag.Quantity = quantity;

              var cell = await _db.Cells.FindAsync(cellId);
              ViewBag.CellAddress = cell?.Address ?? "неизвестно";

              return View();
          }
        */

        /*  [HttpPost]
          public async Task<IActionResult> ConfirmExistingStockReceiving(int confirmQuantity)
          {
              var existingStockId = HttpContext.Session.GetInt32("ExistingStockId");
              var productName = HttpContext.Session.GetString("ReceivingProductName");
              var userId = HttpContext.Session.GetInt32("UserId");
              var userName = HttpContext.Session.GetString("UserName");

              if (existingStockId == null)
              {
                  return RedirectToAction("Receiving");
              }

              var stock = await _db.Stocks
                  .Include(s => s.Product)
                  .Include(s => s.Cell)
                  .FirstOrDefaultAsync(s => s.Id == existingStockId);

              if (stock == null)
              {
                  TempData["Error"] = "Товар не найден на складе";
                  return RedirectToAction("Receiving");
              }

              stock.Quantity += confirmQuantity;
              await _db.SaveChangesAsync();

              // Логируем
              if (_db.OperationLogs != null)
              {
                  _db.OperationLogs.Add(new OperationLog
                  {
                      UserId = userId ?? 0,
                      UserName = userName ?? "Неизвестный",
                      OperationType = "Receiving",
                      Details = $"Добавлено {confirmQuantity} шт к товару '{productName}' в ячейку {stock.Cell?.Address}"
                  });
                  await _db.SaveChangesAsync();
              }

              HttpContext.Session.Remove("ExistingStockId");
              HttpContext.Session.Remove("ReceivingProductName");
              HttpContext.Session.Remove("ReceivingProductSku");
              HttpContext.Session.Remove("ReceivingQuantity");
              HttpContext.Session.Remove("ReceivingCellId");

              TempData["Success"] = $"✅ К товару '{productName}' добавлено {confirmQuantity} шт.";
              return RedirectToAction("Receiving");
          }
        */
        // ============================================================
        //  СОЗДАНИЕ НОВОГО ТОВАРА (POST)
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CreateNewProduct(string barcode, string sku, string productName, string description, int quantity)
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            try
            {
                // Проверяем, не существует ли уже товар с таким артикулом
                var existingProduct = await _db.Products.FirstOrDefaultAsync(p => p.Sku == sku);
                if (existingProduct != null)
                {
                    ViewBag.Error = $"Товар с артикулом '{sku}' уже существует!";
                    ViewBag.NewBarcode = barcode;
                    return View("CreateNewProduct");
                }

                // Проверяем, не существует ли уже товар с таким штрих-кодом на складе
                var existingStock = await _db.Stocks.FirstOrDefaultAsync(s => s.Barcode == barcode && s.Quantity > 0);
                if (existingStock != null)
                {
                    TempData["Warning"] = $"Товар со штрих-кодом '{barcode}' уже есть на складе!";
                    return RedirectToAction("Receiving");
                }

                // 1. Создаём новый товар
                var product = new Product
                {
                    Sku = sku,
                    Name = productName,
                    Description = description ?? ""
                };
                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                // 2. Сохраняем в сессии для следующего шага (выбор ячейки)
                HttpContext.Session.SetInt32("ReceivingProductId", product.Id);
                HttpContext.Session.SetString("ReceivingProductName", product.Name);
                HttpContext.Session.SetString("ReceivingProductSku", product.Sku);
                HttpContext.Session.SetInt32("ReceivingQuantity", quantity);
                HttpContext.Session.SetString("ReceivingBarcode", barcode);

                // 3. Добавляем в ожидаемые поставки (для истории)
                var expected = new ExpectedReceipt
                {
                    Barcode = barcode,
                    Sku = sku,
                    ProductName = productName,
                    ExpectedQuantity = quantity,
                    ReceivedQuantity = 0,
                    Status = "Pending",
                    ExpectedDate = DateTime.UtcNow,
                    Supplier = "Создание нового товара"
                };
                _db.ExpectedReceipts.Add(expected);
                await _db.SaveChangesAsync();
                HttpContext.Session.SetInt32("ExpectedReceiptId", expected.Id);

                TempData["Success"] = $"Товар '{productName}' создан! Теперь выберите ячейку для размещения.";
                return RedirectToAction("SelectCellForReceiving");
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ошибка: {ex.Message}";
                ViewBag.NewBarcode = barcode;
                return View("CreateNewProduct");
            }
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
        private string GenerateEan13Barcode()
        {
            var random = new Random();

            // Генерируем 12 цифр
            var digits = string.Join("", Enumerable.Range(0, 12).Select(_ => random.Next(0, 10).ToString()));

            // Вычисляем контрольную сумму
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int digit = int.Parse(digits[i].ToString());
                if (i % 2 == 0) sum += digit;
                else sum += digit * 3;
            }
            int checkDigit = (10 - (sum % 10)) % 10;

            return digits + checkDigit.ToString(); // 13 цифр
        }
        [HttpPost]
        public async Task<IActionResult> ConfirmReceiving(int cellId, int quantity)
        {
            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            var productId = HttpContext.Session.GetInt32("ReceivingProductId");
            var productName = HttpContext.Session.GetString("ReceivingProductName");
            var expectedReceiptId = HttpContext.Session.GetInt32("ExpectedReceiptId");
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");

            if (warehouseId == null) return RedirectToAction("SelectWarehouse");
            if (productId == null) return RedirectToAction("Receiving");

            // 1. Добавляем/обновляем товар в ячейке
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
                    Quantity = quantity,
                    Barcode = GenerateEan13Barcode()
                };
                _db.Stocks.Add(newStock);
            }

            // 2. Если приёмка была из ожидаемой поставки — обновляем количество
            if (expectedReceiptId.HasValue)
            {
                var expected = await _db.ExpectedReceipts.FindAsync(expectedReceiptId.Value);
                if (expected != null)
                {
                    expected.ReceivedQuantity += quantity;

                    if (expected.ReceivedQuantity >= expected.ExpectedQuantity)
                        expected.Status = "Completed";
                    else if (expected.ReceivedQuantity > 0)
                        expected.Status = "Partial";
                }
            }

            await _db.SaveChangesAsync();

            // 3. Логируем операцию
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

            // 4. Очищаем сессию
            HttpContext.Session.Remove("ReceivingProductId");
            HttpContext.Session.Remove("ReceivingProductName");
            HttpContext.Session.Remove("ReceivingProductSku");
            HttpContext.Session.Remove("ExpectedReceiptId");

            TempData["Success"] = $"Товар \"{productName}\" в количестве {quantity} шт принят на склад";
            return RedirectToAction("Receiving");
        }
        // Страница сортировки - выбор товара для перемещения
        [HttpGet]
        public async Task<IActionResult> Sorting(string sku = "")
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
            if (warehouseId == null) return RedirectToAction("SelectWarehouse");

            // Если передан штрих-код — ищем товар
            if (!string.IsNullOrEmpty(sku))
            {
                // Ищем товар по штрих-коду в таблице Stocks
                var stock = await _db.Stocks
                    .Include(s => s.Product)
                    .Include(s => s.Cell)
                    .FirstOrDefaultAsync(s => s.Barcode == sku && s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0);

                if (stock != null)
                {
                    // Сохраняем в сессию
                    HttpContext.Session.SetInt32("MovingProductId", stock.ProductId);
                    HttpContext.Session.SetString("MovingProductName", stock.Product?.Name ?? "Неизвестный товар");
                    HttpContext.Session.SetString("MovingProductSku", stock.Product?.Sku ?? "");
                    HttpContext.Session.SetInt32("MovingFromCellId", stock.CellId);
                    HttpContext.Session.SetInt32("MovingQuantity", stock.Quantity);
                    HttpContext.Session.SetString("MovingBarcode", stock.Barcode ?? "");

                    var cells = await _db.Cells
                        .Where(c => c.WarehouseId == warehouseId && c.Id != stock.CellId)
                        .ToListAsync();

                    ViewBag.ProductName = stock.Product?.Name;
                    ViewBag.ProductSku = stock.Product?.Sku;
                    ViewBag.CurrentCell = stock.Cell?.Address ?? "неизвестно";
                    ViewBag.Quantity = stock.Quantity;
                    ViewBag.Barcode = stock.Barcode ?? "";

                    return View("SelectTargetCell", cells);
                }

                TempData["SortingError"] = $"Товар со штрих-кодом '{sku}' не найден на складе";
                return RedirectToAction("Sorting");
            }

            // Если штрих-код не передан — показываем страницу ввода
            return View();
        }
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

            var stock = await _db.Stocks
                .Include(s => s.Product)
                .Include(s => s.Cell)
                .FirstOrDefaultAsync(s => s.Barcode == scannedSku && s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0);

            if (stock == null)
            {
                ViewBag.Error = $"Товар со штрих-кодом '{scannedSku}' не найден на складе";
                return View("Sorting");
            }

            HttpContext.Session.SetInt32("MovingProductId", stock.ProductId);
            HttpContext.Session.SetString("MovingProductName", stock.Product?.Name ?? "Неизвестный товар");
            HttpContext.Session.SetString("MovingProductSku", stock.Product?.Sku ?? "");
            HttpContext.Session.SetInt32("MovingFromCellId", stock.CellId);
            HttpContext.Session.SetInt32("MovingQuantity", stock.Quantity);
            HttpContext.Session.SetString("MovingBarcode", stock.Barcode ?? ""); // ← ДОБАВЛЯЕМ

            var cells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId && c.Id != stock.CellId)
                .ToListAsync();

            ViewBag.ProductName = stock.Product?.Name;
            ViewBag.ProductSku = stock.Product?.Sku;
            ViewBag.CurrentCell = stock.Cell?.Address ?? "неизвестно";
            ViewBag.Quantity = stock.Quantity;

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
            var barcode = HttpContext.Session.GetString("MovingBarcode");

            if (warehouseId == null) return RedirectToAction("SelectWarehouse");
            if (productId == null) return RedirectToAction("Sorting");

            var cells = await _db.Cells
                .Where(c => c.WarehouseId == warehouseId && c.Id != currentCellId)
                .ToListAsync();

            ViewBag.ProductName = HttpContext.Session.GetString("MovingProductName");
            ViewBag.CurrentCell = (await _db.Cells.FindAsync(currentCellId))?.Address;
            ViewBag.Quantity = quantity ?? 0;
            ViewBag.Barcode = barcode ?? ""; 

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
            var movingBarcode = HttpContext.Session.GetString("MovingBarcode"); // ← ДОБАВЛЯЕМ
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

            // Сохраняем штрих-код из исходной записи
            string barcodeToMove = fromStock.Barcode ?? GenerateEan13Barcode();

            if (moveQuantity == fromStock.Quantity)
            {
                _db.Stocks.Remove(fromStock);
            }
            else
            {
                fromStock.Quantity -= moveQuantity;
            }

            // Добавляем в целевую ячейку с сохранением штрих-кода
            var toStock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.CellId == targetCellId);

            if (toStock != null)
            {
                toStock.Quantity += moveQuantity;
                // Если в целевой ячейке нет штрих-кода, добавляем
                if (string.IsNullOrEmpty(toStock.Barcode))
                {
                    toStock.Barcode = barcodeToMove;
                }
            }
            else
            {
                toStock = new Stock
                {
                    ProductId = productId.Value,
                    CellId = targetCellId,
                    Quantity = moveQuantity,
                    Barcode = barcodeToMove // ← СОХРАНЯЕМ ШТРИХ-КОД!
                };
                _db.Stocks.Add(toStock);
            }

            await _db.SaveChangesAsync();

            // Логируем
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
            HttpContext.Session.Remove("MovingBarcode");

            TempData["SortingSuccess"] = $"✅ Товар \"{productName}\" в количестве {moveQuantity} шт перемещён из ячейки {fromCell?.Address} в ячейку {targetCell.Address}";

            return RedirectToAction("Sorting");
        }

        // Страница поиска
        [HttpGet]
        public IActionResult FindObject()
        {
            return View();
        }

        // Поиск по штрих-коду или адресу ячейки (ТОЛЬКО ПО ШТРИХ-КОДУ)
        [HttpPost]
        public async Task<IActionResult> FindObject(string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery))
            {
                ViewBag.Error = "Введите штрих-код товара или адрес ячейки";
                return View();
            }

            var warehouseId = HttpContext.Session.GetInt32("WarehouseId");

            // ============================================================
            // 1. Ищем товар ПО ШТРИХ-КОДУ в таблице Stocks
            // ============================================================
            var stock = await _db.Stocks
                .Include(s => s.Product)
                .Include(s => s.Cell)
                .FirstOrDefaultAsync(s => s.Barcode == searchQuery && s.Cell != null && s.Quantity > 0);

            if (stock != null)
            {
                ViewBag.ResultType = "product";
                ViewBag.ProductName = stock.Product?.Name;
                ViewBag.ProductSku = stock.Product?.Sku;
                ViewBag.ProductDescription = stock.Product?.Description;
                ViewBag.CellAddress = stock.Cell?.Address;
                ViewBag.Quantity = stock.Quantity;
                ViewBag.Barcode = stock.Barcode;
                ViewBag.SearchQuery = searchQuery;
                return View();
            }

            // 2. Пробуем найти как ячейку
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
                ViewBag.SearchQuery = searchQuery;
                return View();
            }

            // 3. Ничего не найдено
            ViewBag.Error = $"Ничего не найдено по запросу \"{searchQuery}\"";
            ViewBag.SearchQuery = searchQuery;
            return View();
        }
        //  ПЕЧАТЬ ШТРИХ-КОДА
        // Страница поиска товара для печати этикетки
        [HttpGet]
        public async Task<IActionResult> PrintBarcode(string barcode = "")
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            // Если передан штрих-код — ищем товар
            if (!string.IsNullOrEmpty(barcode))
            {
                var warehouseId = HttpContext.Session.GetInt32("WarehouseId");
                if (warehouseId == null) return RedirectToAction("SelectWarehouse");

                // Ищем ТОЛЬКО ПО ШТРИХ-КОДУ в таблице Stocks
                var stock = await _db.Stocks
                    .Include(s => s.Product)
                    .Include(s => s.Cell)
                    .FirstOrDefaultAsync(s => s.Barcode == barcode && s.Cell != null && s.Cell.WarehouseId == warehouseId && s.Quantity > 0);

                if (stock != null)
                {
                    ViewBag.ProductName = stock.Product?.Name;
                    ViewBag.ProductSku = stock.Product?.Sku;
                    ViewBag.Barcode = stock.Barcode;
                    ViewBag.CellAddress = stock.Cell?.Address;
                    ViewBag.Quantity = stock.Quantity;
                    ViewBag.Found = true;
                    return View();
                }

                // Товар не найден
                ViewBag.Error = $"Товар со штрих-кодом '{barcode}' не найден на складе";
                ViewBag.Found = false;
                return View();
            }

            // Если штрих-код не передан — показываем страницу поиска
            ViewBag.Found = false;
            return View();
        }

        // Поиск товара по штрих-коду (POST)
        [HttpPost]
        public async Task<IActionResult> PrintBarcode(string barcode, string dummy)
        {
            // Просто перенаправляем на GET-версию с переданным штрих-кодом
            return RedirectToAction("PrintBarcode", new { barcode });
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
