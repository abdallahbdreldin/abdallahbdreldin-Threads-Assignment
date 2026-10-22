using OrderSystem.Interfaces.IRepositories;
using OrderSystem.Models;

namespace OrderSystem.Validators
{
    public sealed class OrderValidator
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICouponRepository _couponRepository;
        private readonly IOrderRepository _orderRepository;

        public OrderValidator(
            ICustomerRepository customerRepository,
            IProductRepository productRepository,
            ICouponRepository couponRepository,
            IOrderRepository orderRepository)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _couponRepository = couponRepository ?? throw new ArgumentNullException(nameof(couponRepository));
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        }

        public async Task ValidateOrderAsync(Order order, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(order);
            ct.ThrowIfCancellationRequested();

            string? failureReason = null;

            Customer? customer = await _customerRepository.GetByIdAsync(order.CustomerId, ct);
            if (customer is null)
            {
                failureReason = $"Customer #{order.CustomerId} not found";
            }
    
            else if (!customer.IsActive)
            {
                failureReason = "Customer is inactive";
            }

            var productsByItem = new List<Product>(order.Items.Count);
            if (failureReason is null)
            {
                foreach (OrderItem item in order.Items)
                {
                    Product? product = await _productRepository.GetByIdAsync(item.ProductId, ct);
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
                    OrderItem item = order.Items[i];
                    Product product = productsByItem[i];
                    if (product.StockQuantity < item.Quantity)
                    {
                        failureReason = $"Insufficient stock for '{product.Name}'";
                        break;
                    }
                }
            }

            if (failureReason is null && order.CouponCode is not null)
            {
                Coupon? coupon = await _couponRepository.GetByCodeAsync(order.CouponCode, ct);
                if (coupon is null)
                    failureReason = $"Coupon '{order.CouponCode}' does not exist";
                else if (coupon.ExpiryDate < DateTime.Today)
                    failureReason = $"Coupon '{order.CouponCode}' has expired";
            }

            if (failureReason is null)
            {
                order.Status = OrderStatus.Validated;
                order.FailureReason = null;
            }
            else
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = failureReason;
            }

            await _orderRepository.SaveAsync(order, ct);
        }
    }
}
