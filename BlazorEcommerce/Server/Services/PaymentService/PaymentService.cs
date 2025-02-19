﻿using Stripe;
using Stripe.Checkout;

namespace BlazorEcommerce.Server.Services.PaymentService;

public class PaymentService : IPaymentService
{
    const string secret = "whsec_cq1WH9CX9U1zxU9EpbBVvWOhfb6e5ysR";

    private readonly ICartService _cartService;
    private readonly IAuthService _authService;
    private readonly IOrderService _orderService;
    public PaymentService(ICartService cartService, IAuthService authService, IOrderService orderService)
    {
        StripeConfiguration.ApiKey = "sk_test_51OfnS5AKaWMljN9iEXfF65LTZ1JtCcHHjV9BK2swGrvpwvq3SniEU8PUpNRIHyLKNvo3sIeJ9DBb7jMyy3BqktYr00GgmOaxU9";

        _cartService = cartService;
        _authService = authService;
        _orderService = orderService;
    }

    public async Task<Session> CreateCheckoutSession()
    {
        var products = (await _cartService.GetDbCartProducts()).Data;

        var lineItems = new List<SessionLineItemOptions>();
        products.ForEach(product => lineItems.Add(new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                UnitAmountDecimal = product.Price * 100,
                Currency = "usd",
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = product.Title,
                    Images = new List<string> { product.ImageUrl }
                }
            },
            Quantity = product.Quantity
        }));

        var options = new SessionCreateOptions
        {
            CustomerEmail = _authService.GetUserEmail(),
            ShippingAddressCollection = new SessionShippingAddressCollectionOptions
            {
                AllowedCountries = new List<string> { "US" }
            },
            PaymentMethodTypes = new List<string>
            {
                "card"
            },
            LineItems = lineItems,
            Mode = "payment",
            SuccessUrl = "https://localhost:7226/order-success",
            CancelUrl = "https://localhost:7226/cart"
        };

        var service = new SessionService();
        Session session = service.Create(options);
        return session;
    }

    public async Task<ServiceResponse<bool>> FulfillOrder(HttpRequest request)
    {
        var json = await new StreamReader(request.Body).ReadToEndAsync();
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    request.Headers["Stripe-Signature"],
                    secret
                );

            if (stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                var user = await _authService.GetUserByEmail(session.CustomerEmail);
                await _orderService.PlaceOrder(user.Id);
            }

            return new ServiceResponse<bool> { Data = true };
        }
        catch (StripeException e)
        {
            return new ServiceResponse<bool> { Data = false, Success = false, Message = e.Message };
        }
    }
}
