using OrderSystem.Models;
using OrderSystem.Repositories;

namespace OrderSystem.Validators
{
    public sealed class SyncOrderValidator
    {
        private readonly SyncCustomerRepository _customerRepository;
        private readonly SyncProductRepository _productRepository;
        private readonly SyncCouponRepository _couponRepository;

        public SyncOrderValidator(
            SyncCustomerRepository customerRepository,
            SyncProductRepository productRepository,
            SyncCouponRepository couponRepository)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _couponRepository = couponRepository ?? throw new ArgumentNullException(nameof(couponRepository));
        }

        public void ValidateOrder(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            ct.ThrowIfCancellationRequested();

            string? failureReason = null;

            Customer? customer = _customerRepository.GetById(order.CustomerId, ct);

            if (customer is null)
                failureReason = $"Customer #{order.CustomerId} not found";
            else if (!customer.IsActive)
                failureReason = "Customer is inactive";

            var productsByItem = new List<Product>(order.Items.Count);

            if (failureReason is null)
            {
                foreach (OrderItem item in order.Items)
                {
                    Product? product = _productRepository.GetById(item.ProductId, ct);
                    if (product is null)
                    {
                        failureReason = $"Product #{item.ProductId} not found";
                        break;
                    }

                    productsByItem.Add(product);
                }
            }

            if (failureReason is null)
            {
                for (int i = 0; i < order.Items.Count; i++)
                {
                    if (productsByItem[i].StockQuantity < order.Items[i].Quantity)
                    {
                        failureReason = $"Insufficient stock for '{productsByItem[i].Name}'";
                        break;
                    }
                }
            }

            if (failureReason is null && order.CouponCode is not null)
            {
                Coupon? coupon = _couponRepository.GetByCode(order.CouponCode, ct);
                if (coupon is null)
                    failureReason = $"Coupon '{order.CouponCode}' does not exist";
                else if (coupon.ExpiryDate < DateTime.Today)
                    failureReason = $"Coupon '{order.CouponCode}' has expired";
            }

            order.Status = failureReason is null ? OrderStatus.Validated : OrderStatus.Rejected;
            order.FailureReason = failureReason;
        }
    }
}
