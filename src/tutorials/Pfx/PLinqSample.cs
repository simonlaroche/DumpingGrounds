﻿namespace tutorials.Pfx
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Reflection;
	using NUnit.Framework;

	[TestFixture]
	public class PLinqSample
	{
		[Test]
		public void Simple_parallel_prime()
		{
			var numbers = Enumerable.Range(3, 100000 - 3);
			var parallelQuery = from n in numbers.AsParallel()
			                    where Enumerable.Range(2, (int) Math.Sqrt(n)).All(i => n%i > 0)
			                    select n;

			int[] primes = parallelQuery.ToArray();

			foreach (var prime in primes)
			{
				Console.Write("{0} ", prime);
			}

		}

		[Test]
		public void As_parallel_out_of_sequence()
		{
			const string alpha = "abcdefgh";
			var pq = from c in alpha.AsParallel() select char.ToUpper(c);
			var uppered = pq.ToArray();
			foreach (var c in uppered)
			{
				Console.Write(c);
			}


		}

		[Test]
		public void Join_on_non_paprallel_squence_throws_exception()
		{
			var alpha = "abc".AsParallel();
			IEnumerable<char> beta = "def".AsEnumerable();

			Assert.Throws<NotSupportedException>(() => alpha.Union(beta));
		}

		[Test]
		public void Exception_in_parallel_processing_is_wrapped_in_aggregate_exception()
		{
			var list = new List<object> {1, 2, null};
			var parallelQuery = from o in list.AsParallel() select o.ToString();

			var aggregateException = Assert.Throws<AggregateException>(() => parallelQuery.ToArray());
			Assert.That(aggregateException.InnerException, Is.InstanceOf<NullReferenceException>());
		}

		[Test]
		public void Spellchecker_example()
		{
			const string wordlookupTxt = "WordLookup.txt";
			if (!File.Exists(wordlookupTxt))
			{
				new WebClient().DownloadFile("http://www.albahari.com/ispell/allwords.txt", wordlookupTxt);
			}

			var lookUp = new HashSet<string>(File.ReadAllLines(wordlookupTxt), StringComparer.CurrentCultureIgnoreCase);

			var random = new Random();

			var wordList = lookUp.ToArray();

			var wordsToTest = Enumerable.Range(0, 1000000).Select(i => wordList[random.Next(0, wordList.Length)]).ToArray();

			wordsToTest[12345] = "woozsh";
			wordsToTest[23456] = "wubsie";

			var query =
				wordsToTest.AsParallel()
				           .Select((word, index) => new IndexedWord {Word = word, Index = index})
				           .Where(iword => !lookUp.Contains(iword.Word))
				           .OrderBy(iword => iword.Index);

			query.Dump();
		}
	}

	public static class EnumExtentions
	{
		public static void Dump<T>( this IEnumerable<T> enumerable)
		{
			var type = typeof (T);

			var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);

			foreach (var fieldInfo in fields)
			{
				Console.Write(fieldInfo.Name.Substring(0, Math.Min(fieldInfo.Name.Length, 25)).PadRight(25));
				Console.Write(" ");
			}

			Console.WriteLine();

			foreach (var fieldInfo in fields)
			{
				Console.Write("".PadRight(25, '-'));
				Console.Write(" ");
			}

			Console.WriteLine();



			foreach (var x in enumerable)
			{
				foreach (var fieldInfo in fields)
				{
					var value = fieldInfo.GetValue(x).ToString();
					Console.Write(value.Substring(0,Math.Min(value.Length, 25)).PadRight(25));
					Console.Write(" ");
				}

				Console.WriteLine();
			}


		}
	}

	public struct IndexedWord
	{
		public string Word;
		public int Index;
	}

}