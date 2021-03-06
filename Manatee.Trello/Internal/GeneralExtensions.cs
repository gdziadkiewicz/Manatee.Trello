﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Manatee.Trello.Exceptions;

namespace Manatee.Trello.Internal
{
	internal static class GeneralExtensions
	{
		private class Description
		{
			public object Value { get; set; }
			public string String { get; set; }
		}

		private static readonly Dictionary<Type, List<Description>> _descriptions = new Dictionary<Type, List<Description>>();
		private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1);
		private static readonly DateTime _trelloMinDate = DateTime.MinValue.ToUniversalTime().AddHours(12);

		public static string ToLowerString<T>(this T item)
		{
			return item.ToString().ToLower();
		}
		public static void Replace<T>(this List<T> list, T replace, T with)
		{
			var i = list.IndexOf(replace);
			if (i == -1) return;
			list[i] = with;
		}
		public static bool BeginsWith(this string str, string beginning)
		{
			return (beginning.Length > str.Length) || (str.Substring(0, beginning.Length) == beginning);
		}
		public static bool IsNullOrWhiteSpace(this string value)
		{
#if NET35 || NET35C
			return string.IsNullOrEmpty(value) || string.IsNullOrEmpty(value.Trim());
#elif NET4 || NET4C || NET45
			return string.IsNullOrWhiteSpace(value);
#endif
		}
		public static string Join(this IEnumerable<string> segments, string separator)
		{
#if NET35 || NET35C
			return string.Join(separator, segments.ToArray());
#elif NET4 || NET4C || NET45
			return string.Join(separator, segments);
#endif
		}
		public static string GetDescription<T>(this T enumerationValue)
			where T : struct
		{
			var type = enumerationValue.GetType();
			if (!type.IsEnum)
				throw new ArgumentException("EnumerationValue must be of Enum type", nameof(enumerationValue));

			EnsureDescriptions<T>();

			var attributes = typeof(T).GetCustomAttributes(typeof(FlagsAttribute), false);
			return !attributes.Any()
				? _descriptions[typeof(T)].First(d => Equals(d.Value, enumerationValue)).String
				: BuildFlagsValues(enumerationValue, ",");
		}
		public static string GetName<T>(this Expression<Func<T>> property)
		{
			var lambda = (LambdaExpression)property;

			MemberExpression memberExpression;
			var body = lambda.Body as UnaryExpression;
			if (body != null)
			{
				var unaryExpression = body;
				memberExpression = (MemberExpression)unaryExpression.Operand;
			}
			else
				memberExpression = (MemberExpression)lambda.Body;

			return memberExpression.Member.Name;
		}
		public static bool IsNotFoundError(this TrelloInteractionException e)
		{
			return e.InnerException != null && e.InnerException.Message.ToLower().Contains("not found");
		}
		public static string FlagsEnumToCommaList<T>(this T value)
		{
			return value.ToLowerString().Replace(" ", string.Empty);
		}
		public static bool In<T>(this T obj, params T[] values)
		{
			return values.Contains(obj);
		}
		public static DateTime ExtractCreationDate(this string id)
		{
			if (id.IsNullOrWhiteSpace())
				throw new InvalidOperationException("Cannot extract creation date until ID is downloaded.");
			var asHex = id.Substring(0, 8);
			var timeStamp = int.Parse(asHex, NumberStyles.HexNumber);
			return _unixEpoch.AddSeconds(timeStamp);
		}
		public static DateTime Encode(this DateTime date)
		{
			return date <= _trelloMinDate ? _trelloMinDate : date;
		}
		public static DateTime Decode(this DateTime date)
		{
			return date == _trelloMinDate ? DateTime.MinValue : date;
		}
		public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var item in collection)
			{
				action(item);
				yield return item;
			}
		}

		private static void EnsureDescriptions<T>()
		{
			var type = typeof(T);
			if (_descriptions.ContainsKey(type)) return;
			lock (_descriptions)
			{
				if (_descriptions.ContainsKey(type)) return;
				var names = Enum.GetValues(type).Cast<T>();
				var descriptions = names.Select(n => new Description {Value = n, String = GetDescription<T>(n.ToString())}).ToList();
				_descriptions.Add(type, descriptions);
			}
		}
		private static string GetDescription<T>(string name)
		{
			var type = typeof(T);
			var memInfo = type.GetMember(name);
			var attributes = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
			return attributes.Any() ? ((DescriptionAttribute)attributes[0]).Description : name;
		}
		private static string BuildFlagsValues<T>(T obj, string separator)
		{
			var descriptions = _descriptions[typeof(T)];
			var value = Convert.ToInt64(obj);
			var index = descriptions.Count - 1;
			var names = new List<string>();
			while (value > 0 && index >= 0)
			{
				var compare = Convert.ToInt64(descriptions[index].Value);
				if (value >= compare)
				{
					names.Insert(0, descriptions[index].String);
					value -= compare;
				}
				index--;
			}
			return names.Distinct().Join(separator);
		}
	}
}
