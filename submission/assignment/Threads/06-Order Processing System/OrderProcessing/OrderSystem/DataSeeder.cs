using OrderSystem.Models;

namespace OrderSystem
{
    public sealed class DataSeeder
    {
        private readonly JsonFileStore _store;

        public DataSeeder(JsonFileStore? store = null)
        {
            _store = store ?? new JsonFileStore();
        }

        public async Task ResetAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (Directory.Exists(_store.DataDirectory)) Directory.Delete(_store.DataDirectory, recursive: true);

            Directory.CreateDirectory(_store.DataDirectory);

            var files = CreateSeedData();
            foreach ((string fileName, object records, int count) in files)
            {
                ct.ThrowIfCancellationRequested();
                await WriteSeedAsync(fileName, records, ct);
                Console.WriteLine($"[Seeder] {fileName} -> {count} records");
            }
        }

        private async Task WriteSeedAsync<T>(string fileName, T records, CancellationToken ct) =>
            await _store.WriteAsync(fileName, records, ct);

        private static List<(string FileName, object Records, int Count)> CreateSeedData()
        {
            var customers = new List<Customer>
            {
                CustomerSeed(1, "Ahmed Ali", true, LoyaltyTier.Gold),
                CustomerSeed(2, "Sara Mohamed", true, LoyaltyTier.Silver),
                CustomerSeed(3, "Omar Hassan", true, LoyaltyTier.Bronze),
                CustomerSeed(4, "Mona Ibrahim", false, LoyaltyTier.Silver),
                CustomerSeed(5, "Youssef Adel", true, LoyaltyTier.Gold),
                CustomerSeed(6, "Nour Khaled", true, LoyaltyTier.Bronze),
                CustomerSeed(7, "Karim Samir", true, LoyaltyTier.Silver),
                CustomerSeed(8, "Laila Mahmoud", true, LoyaltyTier.Gold),
                CustomerSeed(9, "Hassan Tarek", true, LoyaltyTier.Bronze),
                CustomerSeed(10, "Dina Fathy", true, LoyaltyTier.Silver)
            };

            var products = new List<Product>
            {
                ProductSeed(1, "Laptop", 1200m, 10, 2.2m),
                ProductSeed(2, "Smartphone", 800m, 15, 0.4m),
                ProductSeed(3, "Wireless Mouse", 25m, 2, 0.1m),
                ProductSeed(4, "Mechanical Keyboard", 90m, 1, 0.9m),
                ProductSeed(5, "USB-C Hub", 45m, 30, 0.2m),
                ProductSeed(6, "Headphones", 150m, 12, 0.3m),
                ProductSeed(7, "27\" Monitor", 350m, 6, 5.0m),
                ProductSeed(8, "Webcam", 70m, 20, 0.2m)
            };

            DateTime today = DateTime.Today;
            var coupons = new List<Coupon>
            {
                CouponSeed("SAVE10", 10, today.AddDays(180)),
                CouponSeed("WELCOME15", 15, today.AddDays(90)),
                CouponSeed("FLASH20", 20, today.AddDays(7)),
                CouponSeed("OLD20", 20, today.AddDays(-30))
            };

            DateTime createdAt = DateTime.Now;
            var incomingOrders = new List<Order>
            {
                OrderSeed(1, 1, "SAVE10", createdAt, (1, 1), (3, 1)),
                OrderSeed(2, 2, null, createdAt, (2, 1)),
                OrderSeed(3, 3, "WELCOME15", createdAt, (5, 2), (8, 1)),
                OrderSeed(4, 4, null, createdAt, (6, 1)),
                OrderSeed(5, 5, null, createdAt, (7, 1)),
                OrderSeed(6, 6, "FLASH20", createdAt, (5, 1)),
                OrderSeed(7, 7, null, createdAt, (6, 1), (8, 1)),
                OrderSeed(8, 8, "SAVE10", createdAt, (1, 1)),
                OrderSeed(9, 9, null, createdAt, (3, 5)),
                OrderSeed(10, 10, null, createdAt, (4, 1)),
                OrderSeed(11, 1, null, createdAt, (4, 1)),
                OrderSeed(12, 2, "OLD20", createdAt, (2, 1)),
                OrderSeed(13, 3, null, createdAt, (6, 2)),
                OrderSeed(14, 5, "WELCOME15", createdAt, (8, 2), (5, 1)),
                OrderSeed(15, 99, null, createdAt, (2, 1)),
                OrderSeed(16, 6, null, createdAt, (7, 1), (5, 1)),
                OrderSeed(17, 7, "SAVE10", createdAt, (2, 1)),
                OrderSeed(18, 8, "FAKE50", createdAt, (6, 1)),
                OrderSeed(19, 9, null, createdAt, (8, 1)),
                OrderSeed(20, 10, "FLASH20", createdAt, (1, 1), (6, 1))
            };

            return new()
            {
                (DataFiles.Customers, customers, customers.Count),
                (DataFiles.Products, products, products.Count),
                (DataFiles.Coupons, coupons, coupons.Count),
                (DataFiles.IncomingOrders, incomingOrders, incomingOrders.Count),
                (DataFiles.Orders, new List<Order>(), 0),
                (DataFiles.Payments, new List<PaymentTransaction>(), 0),
                (DataFiles.Emails, new List<EmailMessage>(), 0),
                (DataFiles.EmailRetries, new List<EmailMessage>(), 0)
            };
        }

        private static Customer CustomerSeed(int id, string name, bool active, LoyaltyTier tier) => new()
        {
            Id = id,
            Name = name,
            Email = string.Join('.', name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + "@example.com",
            IsActive = active,
            LoyaltyTier = tier
        };

        private static Product ProductSeed(int id, string name, decimal price, int stock, decimal weight) => new()
        {
            Id = id, Name = name, Price = price, StockQuantity = stock, WeightKg = weight
        };

        private static Coupon CouponSeed(string code, int discount, DateTime expiry) => new()
        {
            Code = code, DiscountPercent = discount, ExpiryDate = expiry
        };

        private static Order OrderSeed(int id, int customerId, string? coupon, DateTime createdAt, params (int ProductId, int Quantity)[] items) => new()
        {
            Id = id,
            CustomerId = customerId,
            Items = items.Select(item => new OrderItem { ProductId = item.ProductId, Quantity = item.Quantity, UnitPrice = 0m }).ToList(),
            CouponCode = coupon,
            SubTotal = 0m,
            Discount = 0m,
            ShippingCost = 0m,
            TotalAmount = 0m,
            Status = OrderStatus.Pending,
            CreatedAt = createdAt,
            FailureReason = null,
            TransactionId = null
        };
    }
}
