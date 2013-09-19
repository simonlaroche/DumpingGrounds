﻿namespace stoplosskata.lessstate
{


		using System.Collections.Generic;
		using NUnit.Framework;
		using System.Linq;

		[TestFixture]
		public class MovingStoplossSpecs
		{
			[Test]
			public void When_position_is_acquired()
			{
				var bus = new FakeBus();
				var processManager = new StoplossProcessManager(bus);
				processManager.Handle(new PositionAcquired { Price = 15, Symbol = "ABC" });

				Assert.That(bus.PublishedMessages.Count, Is.EqualTo(0));
			}

			[Test]
			public void When_low_price_is_sustained_below_trigger_we_sell()
			{
				var bus = new FakeBus();
				var processManager = new StoplossProcessManager(bus);
				processManager.Handle(new PositionAcquired { Price = 1, Symbol = "ABC" });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 0.89m });
				var first = (WakeMeUpIn15Seconds)bus.PublishedMessages.First();
				bus.PublishedMessages.Clear();
				processManager.Handle((ShouldWeSell)first.Message);

				Assert.That(bus.SentMessages.Count, Is.EqualTo(1));

			}


			[Test]
			public void When_low_price_is_sustained_below_trigger_we_sell_once()
			{
				var bus = new FakeBus();
				var processManager = new StoplossProcessManager(bus);
				processManager.Handle(new PositionAcquired { Price = 1, Symbol = "ABC" });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 0.89m });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 0.88m });

				var first = (WakeMeUpIn15Seconds)bus.PublishedMessages[0];
				var second = (WakeMeUpIn15Seconds)bus.PublishedMessages[2];

				bus.PublishedMessages.Clear();
				processManager.Handle((ShouldWeSell)first.Message);
				processManager.Handle((ShouldWeSell)second.Message);

				Assert.That(bus.SentMessages.Count, Is.EqualTo(1));

			}


			[Test]
			public void When_low_price_is_not_sustained_below_trigger_we_do_nothing()
			{
				var bus = new FakeBus();
				var processManager = new StoplossProcessManager(bus);
				processManager.Handle(new PositionAcquired { Price = 1, Symbol = "ABC" });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 0.89m });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 0.90m });

				var first = (WakeMeUpIn15Seconds)bus.PublishedMessages.First();
				bus.PublishedMessages.Clear();
				processManager.Handle((ShouldWeSell)first.Message);

				Assert.That(bus.SentMessages.Count, Is.EqualTo(0));

			}



			[Test]
			public void When_high_price_is_sustained_trigger_value_goes_up()
			{
				var bus = new FakeBus();
				var processManager = new StoplossProcessManager(bus);
				processManager.Handle(new PositionAcquired { Price = 1, Symbol = "ABC" });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 1.01m });

				var fisrt = (WakeMeUpIn15Seconds)bus.PublishedMessages[0];
				var second = (WakeMeUpIn20Seconds)bus.PublishedMessages[1];

				bus.PublishedMessages.Clear();

				processManager.Handle((ShouldWeSell)fisrt.Message);
				processManager.Handle((ShouldWeMoveTriggerPrice)second.Message);

				Assert.That(bus.PublishedMessages.Count, Is.EqualTo(1));
				var triggerValueRaised = (TriggerValueRaised)bus.PublishedMessages[0];
				Assert.That(triggerValueRaised.TriggerValue, Is.EqualTo(0.91m));
			}

			[Test]
			public void When_high_price_is_not_sustained_trigger_stays_the_same()
			{
				var bus = new FakeBus();
				var processManager = new StoplossProcessManager(bus);
				processManager.Handle(new PositionAcquired { Price = 1, Symbol = "ABC" });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 1.01m });
				processManager.Handle(new PriceChanged { Symbol = "ABC", Price = 0.99m });

				var fisrt = (WakeMeUpIn15Seconds)bus.PublishedMessages[0];
				var second = (WakeMeUpIn20Seconds)bus.PublishedMessages[1];

				bus.PublishedMessages.Clear();

				processManager.Handle((ShouldWeSell)fisrt.Message);
				processManager.Handle((ShouldWeMoveTriggerPrice)second.Message);

				Assert.That(bus.PublishedMessages.Count, Is.EqualTo(0));
			}


		}

		public class TriggerValueRaised : IMessage
		{
			public decimal TriggerValue;
		}

		public class FakeBus : IBus
		{
			public IList<IMessage> PublishedMessages = new List<IMessage>();
			public IList<IMessage> SentMessages = new List<IMessage>();

			public void Publish(IMessage message)
			{
				PublishedMessages.Add(message);
			}

			public void Send(IMessage message)
			{
				SentMessages.Add(message);
			}
		}

		public interface IBus
		{
			void Publish(IMessage message);
			void Send(IMessage sellPosition);
		}

		public class StoplossProcessManager
		{
			public enum State
			{
				Even, Up, Down, Completed 
			}
			private readonly IBus bus;
			private decimal stopLossPrice;
			private List<decimal> sellList = new List<decimal>();
			private List<decimal> moveList = new List<decimal>();
			private decimal initialDelta;
			private State state = State.Even;
			private decimal previousPrice;
			private decimal currentPrice;

			public StoplossProcessManager(IBus bus)
			{
				this.bus = bus;
			}

			public void Handle(PositionAcquired message)
			{
				stopLossPrice = message.Price * 0.9m;
				initialDelta = message.Price - stopLossPrice;
				currentPrice = message.Price;
			}

			public void Handle(PriceChanged message)
			{
				if (state == State.Completed) return;
				
				currentPrice = message.Price;
				var direction = message.Price > currentPrice ? State.Up : (message.Price < currentPrice ? State.Down : State.Even);
				
					
				Publish(new WakeMeUpIn15Seconds { Message = new ShouldWeSell { Direction = direction,   Price = message.Price, Symbol = message.Symbol } });
				Publish(new WakeMeUpIn20Seconds { Message = new ShouldWeMoveTriggerPrice() { Direction = direction, Price = message.Price, Symbol = message.Symbol } });
			}

			public void Handle(ShouldWeSell message)
			{
				if (state == State.Completed) return;

				if (sellList.All(x => x < stopLossPrice))
				{
					bus.Send(new SellPosition { Price = message.Price, Symbol = message.Symbol });
					state= State.Completed;
				}
				sellList.Remove(message.Price);
			}

			public void Handle(ShouldWeMoveTriggerPrice message)
			{
				if (state == State.Completed) return;
				if (moveList.All(x => x >= message.Price))
				{

					stopLossPrice = message.Price - initialDelta;

					bus.Publish(new TriggerValueRaised() { TriggerValue = stopLossPrice });
				}
				moveList.Remove(message.Price);
			}

			private void Publish(IMessage message)
			{
				bus.Publish(message);
			}
		}



		public class PositionAcquired : IMessage
		{
			public decimal Price;
			public string Symbol;
		}

	public class PriceChanged : IMessage
		{
			public decimal Price;
			public string Symbol;
		}

		public class SellPosition : IMessage
		{
			public decimal Price;
			public string Symbol;
		}

		public class WakeMeUpIn15Seconds : IMessage
		{
			public IMessage Message;
		}

		public class ShouldWeSell : IMessage
		{
			public decimal Price;
			public string Symbol;
			public StoplossProcessManager.State Direction;
		}

		public class ShouldWeMoveTriggerPrice : IMessage
		{
			public decimal Price;
			public string Symbol;
			public StoplossProcessManager.State Direction;
		}


		public interface IMessage
		{
		}



		public class WakeMeUpIn20Seconds : IMessage
		{
			public IMessage Message;
		}
	}
