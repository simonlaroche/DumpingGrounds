﻿namespace Dinner
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using Messaging;

	public class Manager : IHandle<OrderCompleted>
	{
		

		public void Handle(OrderCompleted message)
		{
			message.Order.Completed = DateTime.Now;

			Console.WriteLine("Processing time for order {0} was {1}", message.Order.Id,
			                  message.Order.Completed.Subtract(message.Order.Created));
		}
	}

	public class Cashier : IHandle<PrepareForPayment>
	{
		private readonly Dispatcher dispatcher;
		private Dictionary<Guid, Order> ordersAwaitingPaymentByOrderNumber = new Dictionary<Guid, Order>();

		public Cashier(Dispatcher dispatcher)
		{
			this.dispatcher = dispatcher;
		}

		public void Handle(PrepareForPayment message)
		{
			ordersAwaitingPaymentByOrderNumber.Add(message.Order.Id, message.Order);
		}


		public void PayForOrder(Guid orderNumber)
		{
			var order = ordersAwaitingPaymentByOrderNumber[orderNumber];
			order.IsPaid = true;

			dispatcher.Handle(new OrderPaid {CausationId = Guid.NewGuid(), CorrelationId = order.Id, Order = order});
		}
	}

	public class AssMan : IHandle<PriceOrder>
	{
		private decimal taxRate = 0.07m;
		private Dictionary<string, decimal> pricesByDishName = new Dictionary<string, decimal>();
		private Dispatcher dispatcher;

		public AssMan(Dispatcher dispatcher)
		{
			this.dispatcher = dispatcher;
			pricesByDishName.Add("Burger", 10);
		}

	
		public void Handle(PriceOrder message)
		{
			var order =  message.Order;
			foreach (var item in order.Items)
			{
				if (pricesByDishName.ContainsKey(item.Name))
				{
					item.Price = pricesByDishName[item.Name]*item.Qty;
				}
				else
				{
					throw new Exception("I have no idea what the price is!!!!");
				}
			}

			order.SubTotal = order.Items.Sum(i => i.Price);
			order.Total = order.SubTotal + (order.SubTotal*taxRate);

			dispatcher.Publish(new OrderPriced {CausationId = message.Id, CorrelationId = message.CorrelationId, Order = order});
		}
	}

	public class Cook : IHandle<CookFood>
	{
		private Dictionary<string, List<string>> ingredientsByDishName = new Dictionary<string, List<string>>();
		private Dispatcher dispatcher;
		private readonly int speed;

		public Cook(Dispatcher dispatcher, int speed)
		{
			ingredientsByDishName.Add("Burger", new List<string> {"Bun", "Meat"});

			this.dispatcher = dispatcher;
			this.speed = speed;
		}

		public void Handle(CookFood message)
		{
			var order = message.Order;
			foreach (var item in order.Items)
			{
				if (ingredientsByDishName.ContainsKey(item.Name))
				{
					item.Ingredients = ingredientsByDishName[item.Name].ToList();
				}
				else
				{
					throw new Exception("I have no idea how to cook this!!!!");
				}
			}

			Thread.Sleep(speed);
			dispatcher.Handle(new FoodPrepared
				{
					CausationId = message.Id,
					CorrelationId = message.CorrelationId,
					Order = message.Order
				});
		}
	}

	public class Waiter
	{
		private readonly Dispatcher dispatcher;
		private int lastOrderNumber;

		public Waiter(Dispatcher dispatcher)
		{
			this.dispatcher = dispatcher;
		}

		public Guid PlaceOrder(IEnumerable<Tuple<string, int>> items, int tableNumber)
		{
			var order = new Order();
			order.Id = Guid.NewGuid();
			order.TableNumber = tableNumber;
			order.Created = DateTime.Now;
			order.TTL = order.Created.AddSeconds(1000);
			foreach (var item in items)
			{
				order.AddItem(item.Item1, item.Item2);
			}
			dispatcher.Handle(new OrderPlaced {Id = order.Id, CorrelationId = order.Id, CausationId = Guid.Empty, Order = order});
			return order.Id;
		}

		public Guid PlaceDodgyOrder(IEnumerable<Tuple<string, int>> items, int tableNumber)
		{
			var order = new Order();
			order.Id = Guid.NewGuid();
			order.TableNumber = tableNumber;
			order.Created = DateTime.Now;
			order.TTL = order.Created.AddSeconds(1000);
			foreach (var item in items)
			{
				order.AddItem(item.Item1, item.Item2);
			}
			dispatcher.Publish(new DodgyOrderPlaced() { Id = order.Id, CorrelationId = order.Id, CausationId = Guid.Empty, Order = order });
			return order.Id;
		}
	}
}