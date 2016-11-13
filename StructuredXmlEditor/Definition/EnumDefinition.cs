﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using StructuredXmlEditor.Data;
using StructuredXmlEditor.View;

namespace StructuredXmlEditor.Definition
{
	public class EnumDefinition : PrimitiveDataDefinition
	{
		public string Key { get; set; }

		public List<string> EnumValues { get; set; }
		public string Default { get; set; }

		public override DataItem CreateData(UndoRedoManager undoRedo)
		{
			var item = new EnumItem(this, undoRedo);
			item.Value = Default;
			return item;
		}

		public override DataItem LoadData(XElement element, UndoRedoManager undoRedo)
		{
			var item = new EnumItem(this, undoRedo);

			item.Value = element.Value;

			return item;
		}

		public override void Parse(XElement definition)
		{
			Key = definition.Attribute("Key")?.Value?.ToString();

			var rawEnumValues = definition.Attribute("EnumValues")?.Value;
			if (rawEnumValues == null && definition.Value != null) rawEnumValues = definition.Value;
			if (rawEnumValues != null) EnumValues = rawEnumValues.Split(new char[] { ',' }).ToList();

			Default = definition.Attribute("Default")?.Value?.ToString();
			if (Default == null && EnumValues != null) Default = EnumValues[0];
		}

		public override void DoSaveData(XElement parent, DataItem item)
		{
			var si = item as EnumItem;

			parent.Add(new XElement(Name, si.Value));
		}

		public override string WriteToString(DataItem item)
		{
			var i = item as EnumItem;
			return i.Value;
		}

		public override DataItem LoadFromString(string data, UndoRedoManager undoRedo)
		{
			var item = new EnumItem(this, undoRedo);
			item.Value = data;
			return item;
		}

		public override void RecursivelyResolve(Dictionary<string, DataDefinition> defs)
		{
			if (Key != null)
			{
				if (defs.ContainsKey(Key.ToLower()))
				{
					var def = defs[Key.ToLower()] as EnumDefinition;
					EnumValues = def.EnumValues;
					if (Default == null) Default = EnumValues[0];
				}
				else
				{
					Message.Show("Failed to find key " + Key + "!", "Reference Resolve Failed", "Ok");
				}
			}
		}

		public override string DefaultValueString()
		{
			return Default;
		}

		public override object DefaultValue()
		{
			return Default;
		}
	}
}
