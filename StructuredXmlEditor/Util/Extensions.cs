﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace StructuredXmlEditor
{
	public static class Extensions
	{
		//-----------------------------------------------------------------------
		public static string SerializeObject<T>(this T toSerialize)
		{
			XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());

			using (StringWriter textWriter = new StringWriter())
			{
				xmlSerializer.Serialize(textWriter, toSerialize);
				return textWriter.ToString();
			}
		}

		//-----------------------------------------------------------------------
		public static T DeserializeObject<T>(this string toDeserialize)
		{
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));

			using (TextReader reader = new StringReader(toDeserialize))
			{
				return (T)xmlSerializer.Deserialize(reader);
			}
		}

		//-----------------------------------------------------------------------
		public static string Capitalise(this string input)
		{
			if (String.IsNullOrEmpty(input))
				throw new ArgumentException("ARGH!");
			return input.First().ToString().ToUpper() + input.Substring(1);
		}

		//-----------------------------------------------------------------------
		public static string GetDescription<T>(this T e) where T : IConvertible
		{
			string description = null;

			if (e is Enum)
			{
				Type type = e.GetType();
				Array values = System.Enum.GetValues(type);

				foreach (int val in values)
				{
					if (val == e.ToInt32(CultureInfo.InvariantCulture))
					{
						var memInfo = type.GetMember(type.GetEnumName(val));
						var descriptionAttributes = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
						if (descriptionAttributes.Length > 0)
						{
							// we're only getting the first description we find
							// others will be ignored
							description = ((DescriptionAttribute)descriptionAttributes[0]).Description;
						}

						break;
					}
				}
			}

			return description;
		}
	}
}
