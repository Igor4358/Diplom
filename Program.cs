using Microsoft.EntityFrameworkCore;
using WMS.Terminal.Data;
using WMS.Terminal.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Подключаем PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Добавляем MVC (контроллеры и представления)
builder.Services.AddControllersWithViews();

// 3. Сессии (для хранения ID пользователя после логина)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// 4. Middleware (ВАЖНО: порядок имеет значение!)
app.UseHttpsRedirection();
app.UseStaticFiles();       // для CSS, JS, PWA файлов
app.UseRouting();
app.UseSession();           // включаем сессии (ДО UseAuthorization)
app.UseAuthorization();

// 5. Маршруты
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Terminal}/{action=Login}/{id?}");

// 6. Инициализация базы данных и тестовых данных
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Создаём БД, если её нет
    dbContext.Database.EnsureCreated();

    // === СОЗДАЁМ ТЕСТОВЫЙ СКЛАД (если нет) ===
    if (!dbContext.Warehouses.Any())
    {
        var warehouse = new Warehouse { Name = "406" };
        dbContext.Warehouses.Add(warehouse);
        dbContext.SaveChanges();
        Console.WriteLine("Склад 406 создан");
    }

    // === СОЗДАЁМ ТЕСТОВЫЕ ЯЧЕЙКИ (если нет) ===
    if (!dbContext.Cells.Any())
    {
        var warehouse = dbContext.Warehouses.First();
        var cells = new[]
        {
            new Cell { Address = "406a231", WarehouseId = warehouse.Id },
            new Cell { Address = "406a232", WarehouseId = warehouse.Id },
            new Cell { Address = "406b101", WarehouseId = warehouse.Id }
        };
        dbContext.Cells.AddRange(cells);
        dbContext.SaveChanges();
        Console.WriteLine("Создано 3 тестовые ячейки");
    }

    // === СОЗДАЁМ ТЕСТОВЫЕ ТОВАРЫ (если нет) ===
    if (!dbContext.Products.Any())
    {
        var products = new[]
        {
            new Product { Sku = "WA22724", Name = "Омыватель зима", Description = "Альфа Lux -17°C 4л" },
            new Product { Sku = "BR90912", Name = "Тормозные колодки", Description = "Bosch передние" },
            new Product { Sku = "OL10234", Name = "Моторное масло", Description = "Castrol 5W-40 4л" }
        };
        dbContext.Products.AddRange(products);
        dbContext.SaveChanges();
        Console.WriteLine("Создано 3 тестовых товара");
    }
    // === СОЗДАЁМ ТЕСТОВЫЕ ОЖИДАЕМЫЕ ПОСТАВКИ ===
    if (!dbContext.ExpectedShipments.Any())
    {
        var warehouse = dbContext.Warehouses.FirstOrDefault();
        var product1 = dbContext.Products.FirstOrDefault(p => p.Sku == "WA22724");
        var product2 = dbContext.Products.FirstOrDefault(p => p.Sku == "BR90912");

        Console.WriteLine($"DEBUG: Склад найден: {warehouse?.Name}, ID={warehouse?.Id}");
        Console.WriteLine($"DEBUG: Товар WA22724 найден: {product1 != null}");

        if (warehouse != null && product1 != null)
        {
            var shipments = new List<ExpectedShipment>();

            shipments.Add(new ExpectedShipment
            {
                InvoiceNumber = "INV-001",
                ProductId = product1.Id,
                ExpectedQuantity = 20,
                ReceivedQuantity = 0,
                ExpectedDate = DateTime.UtcNow.AddDays(3),
                Status = "Pending",
                WarehouseId = warehouse.Id  // ← используем реальный ID
            });

            shipments.Add(new ExpectedShipment
            {
                InvoiceNumber = "INV-002",
                ProductId = product1.Id,
                ExpectedQuantity = 50,
                ReceivedQuantity = 30,
                ExpectedDate = DateTime.UtcNow.AddDays(-1),
                Status = "Partial",
                WarehouseId = warehouse.Id  // ← используем реальный ID
            });

            if (product2 != null)
            {
                shipments.Add(new ExpectedShipment
                {
                    InvoiceNumber = "INV-003",
                    ProductId = product2.Id,
                    ExpectedQuantity = 100,
                    ReceivedQuantity = 0,
                    ExpectedDate = DateTime.UtcNow.AddDays(5),
                    Status = "Pending",
                    WarehouseId = warehouse.Id  // ← используем реальный ID
                });
            }

            dbContext.ExpectedShipments.AddRange(shipments);
            dbContext.SaveChanges();

            Console.WriteLine($"✅ Создано {shipments.Count} тестовых поставок для склада {warehouse.Name} (ID={warehouse.Id})");
        }
        else
        {
            Console.WriteLine("❌ Не удалось создать поставки: склад или товар не найден");
        }
    }

    // === СОЗДАЁМ ОСТАТКИ (товары на ячейках) ===
    if (!dbContext.Stocks.Any())
    {
        var product1 = dbContext.Products.First(p => p.Sku == "WA22724");
        var product2 = dbContext.Products.First(p => p.Sku == "BR90912");
        var cell1 = dbContext.Cells.First(c => c.Address == "406a231");
        var cell2 = dbContext.Cells.First(c => c.Address == "406a232");

        dbContext.Stocks.AddRange(
            new Stock { ProductId = product1.Id, CellId = cell1.Id, Quantity = 10 },
            new Stock { ProductId = product2.Id, CellId = cell2.Id, Quantity = 5 }
        );
        dbContext.SaveChanges();
        Console.WriteLine("Созданы остатки товаров на ячейках");
    }

    // === СОЗДАЁМ ТЕСТОВОГО ПОЛЬЗОВАТЕЛЯ (PIN: 1234) ===
    if (!dbContext.Users.Any(u => u.PinCode == "1234"))
    {
        var warehouse = dbContext.Warehouses.First();
        var testUser = new User
        {
            FullName = "Иван Петров",
            PinCode = "1234",
            CurrentWarehouseId = warehouse.Id
        };
        dbContext.Users.Add(testUser);
        dbContext.SaveChanges();
        Console.WriteLine("Пользователь с PIN 1234 создан");
    }
    // === СОЗДАЁМ ТЕСТОВЫЙ ЗАКАЗ ДЛЯ СБОРКИ ===
    if (!dbContext.Orders.Any())
    {
        var product = dbContext.Products.FirstOrDefault();
        var cell = dbContext.Cells.FirstOrDefault();

        if (product != null && cell != null)
        {
            var order = new Order
            {
                OrderNumber = "126083522",
                Status = "New",
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Orders.Add(order);
            dbContext.SaveChanges();  

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = 1,
                PickedQuantity = 0
            };
            dbContext.OrderItems.Add(orderItem);
            dbContext.SaveChanges();

            var pickingTask = new PickingTask
            {
                OrderId = order.Id,
                ProductId = product.Id,
                CellId = cell.Id,
                RequiredQuantity = 1,
                Status = "New"
            };
            dbContext.PickingTasks.Add(pickingTask);
            dbContext.SaveChanges();

            Console.WriteLine("Тестовый заказ 126083522 создан");
        }
    }
    // === ВЫВОДИМ ИНФОРМАЦИЮ В КОНСОЛЬ (для отладки) ===
    var users = dbContext.Users.ToList();
    Console.WriteLine($"\n=== ИТОГО В БАЗЕ ДАННЫХ ===");
    Console.WriteLine($"Пользователей: {users.Count}");
    foreach (var u in users)
    {
        Console.WriteLine($"  - {u.FullName} (PIN: {u.PinCode})");
    }
    Console.WriteLine($"Складов: {dbContext.Warehouses.Count()}");
    Console.WriteLine($"Ячеек: {dbContext.Cells.Count()}");
    Console.WriteLine($"Товаров: {dbContext.Products.Count()}");
    Console.WriteLine($"Остатков: {dbContext.Stocks.Count()}");
    Console.WriteLine($"========================\n");
}

app.Run();